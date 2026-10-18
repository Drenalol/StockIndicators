namespace StockIndicators.Infrastructure.Configuration;

/// <summary>
/// Bound from the `RedisConfiguration` appsettings section: `Hosts: [{ Host, Port }]`.
/// </summary>
public class RedisConfiguration
{
  public List<RedisHost> Hosts { get; set; } = [];

  /// <summary>
  /// Comma-separated `host:port` list; empty when no usable host is configured.
  /// </summary>
  public string BuildConnectionString() =>
    string.Join(
      ',',
      Hosts
        .Where(host => !string.IsNullOrWhiteSpace(host.Host))
        .Select(host => $"{host.Host}:{host.Port}")
    );
}

public class RedisHost
{
  public string Host { get; set; } = "";

  public int Port { get; set; } = 6379;
}
