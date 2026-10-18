namespace StockIndicators.Contracts.Events;

public record HealthCheckResponse(
  string ServiceName,
  bool Healthy,
  string? Message
);
