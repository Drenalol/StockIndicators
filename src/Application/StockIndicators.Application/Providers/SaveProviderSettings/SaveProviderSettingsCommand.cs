using System.ComponentModel.DataAnnotations;
using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Providers.SaveProviderSettings;

/// <summary>
/// Stores what the operator typed. The UI only sends fields that moved, so a blank value is
/// the operator emptying the input on purpose: it removes the stored value and the field
/// falls back to the environment again. A field missing from the request is left untouched;
/// <see cref="ClearFields"/> deletes fields outright. Whether the new state is live is
/// decided at the next start.
/// </summary>
public record SaveProviderSettingsCommand(
  string Code,
  bool IsEnabled,
  IReadOnlyDictionary<string, string?> Credentials,
  IReadOnlyList<string> ClearFields
) : ICommand;

public class SaveProviderSettingsCommandHandler : IRequestHandler<SaveProviderSettingsCommand>
{
  private readonly IProviderSettingsStore _store;

  public SaveProviderSettingsCommandHandler(IProviderSettingsStore store) => _store = store;

  public async Task Handle(SaveProviderSettingsCommand request, CancellationToken cancellationToken)
  {
    var descriptor = ProviderRegistry.Find(request.Code) ?? throw new UnknownProviderException(request.Code);

    var rows = await _store.LoadAllAsync(cancellationToken);
    var credentials = new Dictionary<string, string>(
      rows.TryGetValue(descriptor.Code, out var existing) ? existing.Credentials : new Dictionary<string, string>(),
      StringComparer.Ordinal);

    var clearing = new HashSet<string>(request.ClearFields ?? [], StringComparer.OrdinalIgnoreCase);

    foreach (var field in descriptor.Fields)
    {
      if (clearing.Contains(field.Name))
      {
        credentials.Remove(field.Name);
        continue;
      }

      if (!request.Credentials.TryGetValue(field.Name, out var value))
        continue;

      // an emptied input is an explicit removal: the stored value goes and the environment
      // (if it has one) shows through again
      if (string.IsNullOrWhiteSpace(value))
      {
        credentials.Remove(field.Name);
        continue;
      }

      value = value.Trim();

      credentials[field.Name] = field.Kind switch
      {
        ProviderFieldKind.Bool => NormalizeBool(value),
        ProviderFieldKind.Int => NormalizeInt(value, field),
        _ => value,
      };
    }

    await _store.SaveAsync(descriptor.Code, request.IsEnabled, credentials, cancellationToken);
  }

  private static string NormalizeBool(string value) =>
    bool.TryParse(value, out var parsed)
      ? parsed ? "true" : "false"
      : throw new ValidationException($"'{value}' is not true/false");

  private static string NormalizeInt(string value, ProviderField field) =>
    int.TryParse(value, out _)
      ? value
      : throw new ValidationException($"{field.Label} must be a whole number");
}
