using MediatR;
using Microsoft.Extensions.Configuration;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Contracts.Providers;

namespace StockIndicators.Application.Providers.TestProvider;

public record TestProviderCommand(string Code, IReadOnlyDictionary<string, string?>? Credentials) : ICommand<ProviderTestResultDto>;

/// <summary>
/// Probes a provider with one real call. Implemented where the provider assemblies are
/// visible (the composition root) - the Application layer only knows the registry.
/// </summary>
public interface IProviderConnectionTester
{
  /// <summary>
  /// Checks the provider with the effective credentials (request values first, stored
  /// second, environment last). Implementations should return a readable failure rather
  /// than throw; throws are reported as failures too.
  /// </summary>
  Task<ProviderTestResultDto> TestAsync(
    ProviderDescriptor descriptor,
    IReadOnlyDictionary<string, string> credentials,
    CancellationToken cancellationToken);
}

/// <summary>
/// "Check connection": one heavy call per provider, run with the credentials as they
/// stand on screen - unsaved values included, so the operator learns about a typo before
/// saving it.
/// </summary>
public class TestProviderCommandHandler : IRequestHandler<TestProviderCommand, ProviderTestResultDto>
{
  private readonly IProviderSettingsStore _store;
  private readonly IProviderConnectionTester _tester;
  private readonly IConfiguration _configuration;

  public TestProviderCommandHandler(
    IProviderSettingsStore store,
    IProviderConnectionTester tester,
    IConfiguration configuration
  )
  {
    _store = store;
    _tester = tester;
    _configuration = configuration;
  }

  public async Task<ProviderTestResultDto> Handle(TestProviderCommand request, CancellationToken cancellationToken)
  {
    var descriptor = ProviderRegistry.Find(request.Code) ?? throw new UnknownProviderException(request.Code);

    var rows = await _store.LoadAllAsync(cancellationToken);
    rows.TryGetValue(descriptor.Code, out var stored);

    var credentials = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var field in descriptor.Fields)
    {
      var value =
        (request.Credentials?.TryGetValue(field.Name, out var typed) == true && !string.IsNullOrWhiteSpace(typed) ? typed.Trim() : null) ??
        (stored is not null && stored.Credentials.TryGetValue(field.Name, out var saved) && saved.Length > 0 ? saved : null) ??
        (_configuration[$"{descriptor.EnvSection}:{field.Name}"] is { Length: > 0 } fromEnv ? fromEnv : null);

      if (value is not null)
        credentials[field.Name] = value;
    }

    var missing = descriptor.Fields
      .Where(field => field.Required && !credentials.ContainsKey(field.Name))
      .Select(field => field.Label)
      .ToList();

    if (missing.Count > 0)
      return new ProviderTestResultDto(false, $"Fill in: {string.Join(", ", missing)}", 0);

    try
    {
      return await _tester.TestAsync(descriptor, credentials, cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      return new ProviderTestResultDto(false, exception.Message, 0);
    }
  }
}
