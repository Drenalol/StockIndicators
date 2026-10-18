namespace StockIndicators.Application.Providers;

public enum ProviderKind
{
  /// <summary>Candle source (streams or polls market data).</summary>
  Data,

  /// <summary>Enriches instruments with data from a free public source.</summary>
  Enricher,

  /// <summary>Delivers alerts somewhere outside the web app.</summary>
  Channel,

  /// <summary>Supports a feature (logos) rather than data or delivery.</summary>
  Auxiliary,
}

public enum ProviderFieldKind
{
  /// <summary>Masked by the UI (dots) until the operator reveals the provider's keys.</summary>
  Secret,

  Text,
  Bool,
  Int,

  /// <summary>
  /// An absolute address something outside this process opens: a browser link, a Telegram
  /// button. The UI checks the format, and for the address the bot links to it also warns
  /// about loopback and LAN values, because Telegram validates button URLs on its own
  /// servers and refuses them.
  /// </summary>
  Url,

  /// <summary>One value out of <see cref="ProviderField.Options"/>; rendered as a dropdown.</summary>
  Choice,
}

/// <summary>
/// One configuration field of a provider. <see cref="Name"/> is the contract: it is the
/// key in the stored credentials JSON and the property name on the provider's options
/// class, which is how stored values are overlaid onto the options at startup.
/// </summary>
public sealed record ProviderField(
  string Name,
  string Label,
  ProviderFieldKind Kind,
  bool Required = false,
  // For Choice fields: the exact set of accepted values, shown as a dropdown
  string[]? Options = null);

/// <summary>
/// What the code knows about one provider the operator can turn on: what it is good for,
/// which fields it needs and where the app's environment fallback binds them. The registry
/// is the admin page's data source; the frontend localizes display copy by code.
/// </summary>
public sealed record ProviderDescriptor(
  string Code,
  string DisplayName,
  ProviderKind Kind,
  // appsettings/env section the provider's options bind from (the fallback when the database has nothing)
  string EnvSection,
  string DocsUrl,
  string[] Markets,
  ProviderField[] Fields,
  // the app generates the credentials itself (Web Push keys) - nothing to obtain
  bool AutoConfigured = false,
  // what the environment says about enablement: channels and helpers were always on when
  // configured (no IsEnabled flag existed for them), data providers opt in through their
  // section. The database toggle wins over both once the operator touches it.
  bool? EnvEnabledDefault = null);

public static class ProviderRegistry
{
  public static readonly ProviderDescriptor Tinkoff = new(
    "tinkoff", "Tinkoff", ProviderKind.Data, "TinkoffProviderOptions", "https://tinkoff.github.io/investAPI/",
    ["MOEX (stocks, ETFs, futures, currencies)"],
    [
      new ProviderField("Token", "API token", ProviderFieldKind.Secret, Required: true),
      new ProviderField("IsSandbox", "Sandbox", ProviderFieldKind.Bool),
    ]);

  public static readonly ProviderDescriptor Alpaca = new(
    "alpaca", "Alpaca", ProviderKind.Data, "AlpacaProviderOptions", "https://docs.alpaca.markets/us/docs/trading-api",
    ["US stocks (incl. overnight)"],
    [
      new ProviderField("ApiKey", "API key ID", ProviderFieldKind.Secret, Required: true),
      new ProviderField("ApiSecret", "Secret API key", ProviderFieldKind.Secret, Required: true),
      new ProviderField("Environment", "Environment", ProviderFieldKind.Choice, Options: ["Paper", "Live"]),
    ]);

  public static readonly ProviderDescriptor BusinessQuant = new(
    "businessquant", "Business Quant", ProviderKind.Data, "BusinessQuantProviderOptions", "https://businessquant.com/data",
    ["US stocks"],
    [
      new ProviderField("ApiKey", "API key", ProviderFieldKind.Secret, Required: true),
    ]);

  public static readonly ProviderDescriptor Binance = new(
    "binance", "Binance", ProviderKind.Data, "BinanceProviderOptions", "https://developers.binance.com/docs/binance-spot-api-docs",
    ["Crypto"], []);

  public static readonly ProviderDescriptor Yahoo = new(
    "yahoo", "Yahoo Finance", ProviderKind.Enricher, "YahooEnricherOptions", "https://finance.yahoo.com/",
    ["Instrument profiles, logos"], []);

  public static readonly ProviderDescriptor Telegram = new(
    "telegram", "Telegram bot", ProviderKind.Channel, "TelegramBotOptions", "https://t.me/BotFather",
    ["Alerts", "Alert management"],
    [
      new ProviderField("Token", "Bot token", ProviderFieldKind.Secret, Required: true),
      new ProviderField("BotLink", "Bot link (https://t.me/...)", ProviderFieldKind.Url),
      new ProviderField("IncludeWebLinks", "Links to the instance in bot buttons", ProviderFieldKind.Bool),
      new ProviderField("InstanceUrl", "Instance URL (https://indicators.example)", ProviderFieldKind.Url),
    ],
    EnvEnabledDefault: true);

  public static readonly ProviderDescriptor WhatsApp = new(
    "whatsapp", "WhatsApp", ProviderKind.Channel, "WhatsAppOptions", "https://developers.facebook.com/docs/whatsapp/cloud-api",
    ["Alerts"],
    [
      new ProviderField("AccessToken", "Access token", ProviderFieldKind.Secret, Required: true),
      new ProviderField("PhoneNumberId", "Phone number id", ProviderFieldKind.Text, Required: true),
      new ProviderField("TemplateName", "Template name", ProviderFieldKind.Text, Required: true),
      new ProviderField("TemplateLanguage", "Template language", ProviderFieldKind.Text),
      new ProviderField("ApiVersion", "Graph API version", ProviderFieldKind.Text),
    ],
    // unlike the free channels, Meta business messaging is opt-in: an instance that never
    // asked for WhatsApp must not even show a green toggle
    EnvEnabledDefault: false);

  public static readonly ProviderDescriptor Smtp = new(
    "smtp", "Email (SMTP)", ProviderKind.Channel, "SmtpOptions", "https://www.rfc-editor.org/rfc/rfc5321",
    ["Reserved for the email alert channel"],
    [
      new ProviderField("Host", "Host", ProviderFieldKind.Text, Required: true),
      new ProviderField("Port", "Port", ProviderFieldKind.Int),
      new ProviderField("UseSsl", "Use SSL", ProviderFieldKind.Bool),
      new ProviderField("Username", "Username", ProviderFieldKind.Text),
      new ProviderField("Password", "Password", ProviderFieldKind.Secret),
      new ProviderField("From", "From", ProviderFieldKind.Text),
    ],
    EnvEnabledDefault: true);

  public static readonly ProviderDescriptor WebPush = new(
    "webpush", "Web Push", ProviderKind.Channel, "WebPushOptions", "https://web.dev/push-notifications-overview",
    ["Alerts to the browser"],
    [
      new ProviderField("Subject", "Contact (mailto: or https://)", ProviderFieldKind.Text),
      new ProviderField("VapidPublicKey", "Vapid public key", ProviderFieldKind.Text),
      new ProviderField("VapidPrivateKey", "Vapid private key", ProviderFieldKind.Secret),
    ],
    AutoConfigured: true,
    EnvEnabledDefault: true);

  public static readonly ProviderDescriptor Logos = new(
    "logos", "Logos", ProviderKind.Auxiliary, "LogoOptions", "https://logo.dev",
    ["Instrument logos"],
    [
      new ProviderField("LogoDevPublishableKey", "Logo.dev publishable key", ProviderFieldKind.Secret),
      new ProviderField("CoinGeckoApiKey", "CoinGecko API key", ProviderFieldKind.Secret),
    ],
    EnvEnabledDefault: true);

  /// <summary>Every provider the settings page offers, in the order it lists them.</summary>
  public static IReadOnlyList<ProviderDescriptor> All { get; } =
  [
    Tinkoff, Alpaca, BusinessQuant, Binance, Yahoo, Telegram, WhatsApp, Smtp, WebPush, Logos,
  ];

  public static ProviderDescriptor? Find(string code) =>
    All.FirstOrDefault(descriptor => string.Equals(descriptor.Code, code, StringComparison.OrdinalIgnoreCase));
}
