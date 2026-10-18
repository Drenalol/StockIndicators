namespace StockIndicators.Internal;

/// <summary>
/// The origins the frontend is served from. A self-hosted instance lives behind whatever
/// domain its owner chose, so the list comes from configuration instead of being baked in:
/// "Frontend:Origins" is either a JSON array of origins or one string with them separated by
/// semicolons ("https://indicators.lan;https://indicators.example").
/// Shared by the CORS policy and the CSRF origin guard so the two never drift apart.
/// </summary>
public static class FrontendOrigins
{
  /// <summary>
  /// Origins that are always allowed: the app served on localhost (the docker-compose default).
  /// </summary>
  public static readonly string[] LocalDefaults =
  [
    "https://localhost",
    "http://localhost"
  ];

  /// <summary>
  /// A vite dev server left on its default port, in development only: the browser (or a phone
  /// on the same network) reaches the same app there, so the guard must not block what vite
  /// itself happily serves.
  /// </summary>
  public static readonly string[] DevOnlyDefaults =
  [
    "https://localhost:5173", "http://localhost:5173",
    "https://127.0.0.1:5173", "http://127.0.0.1:5173"
  ];

  public static IReadOnlyList<string> FromConfiguration(IConfiguration configuration)
  {
    var configured = configuration.GetSection("Frontend:Origins").Get<string[]>();
    if (configured is null)
    {
      configured = (configuration["Frontend:Origins"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    return [.. LocalDefaults, .. configured];
  }

  public static IReadOnlyList<string> ForEnvironment(IConfiguration configuration, IWebHostEnvironment environment) =>
    environment.IsDevelopment()
      ? [.. FromConfiguration(configuration), .. DevOnlyDefaults]
      : FromConfiguration(configuration);
}
