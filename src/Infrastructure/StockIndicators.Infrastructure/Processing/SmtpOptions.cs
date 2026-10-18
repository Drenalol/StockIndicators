namespace StockIndicators.Infrastructure.Processing;

public class SmtpOptions
{
  /// <summary>
  /// When empty, the sender logs the mail instead of delivering it (dev fallback).
  /// </summary>
  public string Host { get; set; } = "";

  public int Port { get; set; } = 25;

  public bool UseSsl { get; set; }

  public string? Username { get; set; }

  public string? Password { get; set; }

  public string From { get; set; } = "";

  /// <summary>
  /// Optional Reply-To header: when the transactional From is a relay address the user
  /// must not answer (a bare noreply), answers go here instead - typically the support
  /// mailbox the domain forwards to. Empty leaves the default (a reply goes to From).
  /// </summary>
  public string? ReplyTo { get; set; }
}
