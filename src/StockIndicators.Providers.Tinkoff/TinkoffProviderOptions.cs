using System.ComponentModel.DataAnnotations;
using StockIndicators.Providers.Base;

namespace StockIndicators.Providers.Tinkoff;

public class TinkoffProviderOptions : ProviderOptions
{
  [Required]
  public string? Token { get; set; }

  public bool IsSandbox { get; set; }

  /// <summary>
  /// Dev-only: address of the local tinkoff-tls-bridge (tools/tinkoff-tls-bridge) to route
  /// gRPC through instead of the real host. Needed on machines whose OS TLS stack cannot
  /// parse the Russian Trusted Root CA chain Tinkoff serves since June 2026 (.NET on macOS
  /// dies with "bad certificate format" before any validation callback). Null = direct.
  /// </summary>
  public string? TlsBridgeAddress { get; set; }

  [Range(1, 6)]
  public int ConnectionCount { get; set; } = 6;
}