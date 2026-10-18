using StockIndicators.Application.Providers;

namespace StockIndicators.Internal;

/// <summary>
/// Lays the stored credentials over the options the environment bound. The provider field
/// names are the options property names, so a stored value lands exactly where an
/// environment variable of the same section would have. Registered after the provider's own
/// binding, it wins; values saved while the app runs reach options resolved later (the
/// snapshot is live), while registration decisions themselves wait for the next start.
/// </summary>
public static class ProviderOptionsOverlay
{
  public static IServiceCollection OverlayProviderOptions<TOptions>(
    this IServiceCollection services,
    ProviderSettingsSnapshot snapshot,
    string code
  ) where TOptions : class =>
    services.Configure<TOptions>(options => Apply(options, snapshot, code));

  private static void Apply(object options, ProviderSettingsSnapshot snapshot, string code)
  {
    var descriptor = ProviderRegistry.Find(code);

    if (descriptor is null || !snapshot.TryGet(code, out var values))
      return;

    foreach (var field in descriptor.Fields)
    {
      if (!values.Credentials.TryGetValue(field.Name, out var raw) || raw.Length == 0)
        continue;

      var property = options.GetType().GetProperty(field.Name);

      if (property is null || !property.CanWrite)
        continue;

      property.SetValue(options, Convert(raw, property.PropertyType));
    }
  }

  private static object? Convert(string raw, Type targetType) =>
    targetType == typeof(bool) || targetType == typeof(bool?)
      ? bool.Parse(raw)
      : targetType == typeof(int) || targetType == typeof(int?)
        ? int.Parse(raw)
        : raw;
}
