using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application;
using StockIndicators.Application.Providers;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Application.Rules;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

/// <summary>
/// Client-facing product limits: the web reads them so the UI stays in sync with server-side
/// validation instead of hardcoding the values. They used to depend on the caller's plan; a
/// self-hosted installation has one user and one set of limits.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ConfigController : ControllerBase
{
  private readonly IOptions<TelegramBotOptions> _telegramBotOptions;
  private readonly IOptions<WhatsAppOptions> _whatsAppOptions;
  private readonly ProviderSettingsSnapshot _providerSettings;
  private readonly IConfiguration _configuration;

  public ConfigController(
    IOptions<TelegramBotOptions> telegramBotOptions,
    IOptions<WhatsAppOptions> whatsAppOptions,
    ProviderSettingsSnapshot providerSettings,
    IConfiguration configuration)
  {
    _telegramBotOptions = telegramBotOptions;
    _whatsAppOptions = whatsAppOptions;
    _providerSettings = providerSettings;
    _configuration = configuration;
  }

  [HttpGet]
  public IActionResult Get() =>
    Ok(new
    {
      maxConditionsPerRule = Limits.MaxConditionsPerRule,
      maxRuleNameLength = RuleValidation.MaxRuleNameLength,
      maxGroupDepth = Limits.MaxGroupDepth,
      defaultCooldownSeconds = Limits.DefaultCooldownSeconds,
      minCooldownSeconds = Limits.MinCooldownSeconds,
      maxOutputs = Limits.MaxOutputChannels,
      telegramBotLink = _telegramBotOptions.Value.BotLink,
      // WhatsApp needs server-side Meta credentials and the (default-off) toggle; the web
      // hides the channel until both hold - an enabled-but-unwired picker entry is a lie
      whatsappEnabled = _providerSettings.IsEnabled(_configuration, "whatsapp") && _whatsAppOptions.Value.IsConfigured
    });
}
