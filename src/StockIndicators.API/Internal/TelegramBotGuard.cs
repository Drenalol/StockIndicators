using Drenalol.Telegram.Bot.Forms;
using Telegram.Bot;

namespace StockIndicators.Internal;

/// <summary>
/// Hosts the library's polling startup without the host's "faulted background service
/// stops the application" rule. The library is fatal by design - correct for a console
/// bot whose only job is the bot - but here the bot is one optional channel of a web
/// application: an unusable token or Telegram unreachable at boot must not take the
/// instance down. The token is validated up front so the reason lands in the log the
/// moment it happens; a rejected token disables the bot until the next restart.
/// </summary>
public class TelegramBotGuard : IHostedService
{
  private readonly IServiceProvider _services;
  private readonly Serilog.ILogger _logger;
  private IHostedService? _inner;
  private Task? _startTask;

  public TelegramBotGuard(IServiceProvider services, Serilog.ILogger logger)
  {
    _services = services;
    _logger = logger.ForContext<TelegramBotGuard>();
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    // Kestrel does not accept a single request until every hosted service has finished
    // StartAsync - and validating the token means a round trip to api.telegram.org, which
    // on networks that throttle Telegram hangs for the full HttpClient timeout. The bot is
    // one optional channel of a web application: it starts in the background while the
    // instance is already serving.
    _startTask = StartBotAsync(cancellationToken);
    return Task.CompletedTask;
  }

  private async Task StartBotAsync(CancellationToken cancellationToken)
  {
    var client = _services.GetRequiredService<ITelegramBotClient>();

    try
    {
      await client.GetMe(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      _logger.Error(
        exception,
        "Telegram bot disabled: the token was rejected or Telegram is unreachable. " +
        "The rest of the instance keeps running; check TelegramBotOptions:Token and restart to enable the bot");

      return;
    }

    _inner = _services.GetRequiredService<TelegramBotStartup>();

    try
    {
      await _inner.StartAsync(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      _logger.Error(exception, "Telegram bot failed to start; the rest of the instance keeps running");

      _inner = null;
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    // A fast Ctrl+C must not orphan a start that is still in flight; give it a moment,
    // then stop regardless - a hung GetMe has nothing to shut down
    if (_startTask is { } start)
    {
      try
      {
        await start.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
      }
      catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
      {
        // the start logs its own failures; shutdown does not repeat them
      }
      catch (Exception)
      {
        // StartBotAsync already logged whatever went wrong
      }
    }

    if (_inner is null)
      return;

    try
    {
      await _inner.StopAsync(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      // Usually a polling failure surfacing late (it was already reported by the
      // library's own error handler); shutdown must not repeat it as an error
      _logger.Debug(exception, "Telegram bot shutdown after an earlier failure");
    }
  }
}
