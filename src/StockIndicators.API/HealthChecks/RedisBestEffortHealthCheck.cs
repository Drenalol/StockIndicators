using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StockIndicators.Infrastructure.Configuration;

namespace StockIndicators.HealthChecks;

/// <summary>
/// Redis is best-effort for this app (the candle L2 cache works without it) and may be
/// intentionally not configured. It never takes the instance down: unconfigured → Healthy,
/// unreachable → Degraded (the endpoint still answers 200).
/// </summary>
public class RedisBestEffortHealthCheck(
  IConnectionMultiplexer connection,
  IOptions<RedisConfiguration> configuration) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(configuration.Value.BuildConnectionString()))
      return HealthCheckResult.Healthy("Redis is not configured (in-memory fallbacks only)");

    try
    {
      await connection.GetDatabase().PingAsync();
      return HealthCheckResult.Healthy();
    }
    catch
    {
      return HealthCheckResult.Degraded("Redis is unavailable (in-memory fallbacks active)");
    }
  }
}
