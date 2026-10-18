using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Refit;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Application.Providers;
using StockIndicators.Application.Providers.TestProvider;
using StockIndicators.Contracts.Providers;
using StockIndicators.Enrichers.Yahoo;
using Alpaca.Markets;
using StockIndicators.Providers.Binance.Api;
using StockIndicators.Providers.Tinkoff;
using StockIndicators.Providers.BusinessQuant.Api;
using Telegram.Bot;
using Tinkoff.InvestApi.V1;

namespace StockIndicators.Internal;

/// <summary>
/// "Check connection" probes: one real, authenticated call per provider, made here and now
/// with the values on screen - deliberately outside the wired-up DI graph, because the
/// provider under test may not be enabled or even configured yet. Failures come back as
/// readable results, not exceptions.
/// </summary>
public class ProviderConnectionTester : IProviderConnectionTester
{
  private readonly IConfiguration _configuration;

  public ProviderConnectionTester(IConfiguration configuration) => _configuration = configuration;

  public async Task<ProviderTestResultDto> TestAsync(
    ProviderDescriptor descriptor,
    IReadOnlyDictionary<string, string> credentials,
    CancellationToken cancellationToken
  )
  {
    var stopwatch = Stopwatch.StartNew();

    var (success, message) = descriptor.Code switch
    {
      "tinkoff" => await TestTinkoff(credentials, cancellationToken),
      "alpaca" => await TestAlpaca(credentials, cancellationToken),
      "businessquant" => await TestBusinessQuant(credentials, cancellationToken),
      "binance" => await TestBinance(cancellationToken),
      "yahoo" => await TestYahoo(cancellationToken),
      "telegram" => await TestTelegram(credentials, cancellationToken),
      "whatsapp" => await TestWhatsApp(credentials, cancellationToken),
      "smtp" => await TestSmtp(credentials, cancellationToken),
      "webpush" => TestWebPush(credentials),
      "logos" => await TestLogos(credentials, cancellationToken),
      _ => (false, $"No connection test for provider '{descriptor.Code}'"),
    };

    return new ProviderTestResultDto(success, message, (int)stopwatch.ElapsedMilliseconds);
  }

  private async Task<(bool, string)> TestTinkoff(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    // probe exactly where the provider itself reaches: the SDK's endpoint (tbank.ru since
    // the rebrand - the gateway routes by authority), through the dev TLS bridge when it is
    // configured (the OS TLS stack cannot finish Tinkoff's chain directly)
    // assemble the channel exactly the way the SDK does - the gateway is picky (a plain
    // metadata call gets Unauthenticated 40003 on the very token the provider trades with):
    // tbank.ru:443 since the rebrand, SslCredentials + per-call plugin interceptor, the dev
    // TLS bridge rerouting TCP when configured
    var channelCredentials = ChannelCredentials.Create(
      new SslCredentials(),
      CallCredentials.FromInterceptor((_, metadata) =>
      {
        metadata.Add("Authorization", $"Bearer {credentials["Token"]}");
        metadata.Add("x-app-name", "StockIndicators");
        return Task.CompletedTask;
      }));

    var options = new GrpcChannelOptions { Credentials = channelCredentials };
    var bridge = _configuration[$"{nameof(TinkoffProviderOptions)}:{nameof(TinkoffProviderOptions.TlsBridgeAddress)}"];
    if (!string.IsNullOrEmpty(bridge))
      options.HttpHandler = TinkoffTlsBridgeHandler.Create(bridge);

    using var channel = GrpcChannel.ForAddress("https://invest-public-api.tbank.ru:443", options);

    // Shares is the exact call the provider makes at startup - the cheapest authenticated
    // request that proves both the token and the scopes the app actually needs (GetAccounts
    // needs a scope many tokens lack; instruments and market data are all this app ever calls).
    var response = await new InstrumentsService.InstrumentsServiceClient(channel)
      .SharesAsync(new InstrumentsRequest(), cancellationToken: cancellationToken);

    return (true, $"Token accepted, {response.Instruments.Count} share(s)");
  }

  private static async Task<(bool, string)> TestAlpaca(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    // the Trading API assets call - the same authenticated call the provider makes at
    // startup; the market-data endpoints probe subscriptions, not the key (the overnight
    // feed 403s on a valid key without that entitlement)
    // (Microsoft.Extensions.Hosting.Environments owns the bare name here)
    var environment = credentials.GetValueOrDefault("Environment", "Paper")
      .Equals("Live", StringComparison.OrdinalIgnoreCase)
      ? Alpaca.Markets.Environments.Live
      : Alpaca.Markets.Environments.Paper;

    using var client = environment.GetAlpacaTradingClient(new SecretKey(credentials["ApiKey"], credentials["ApiSecret"]));

    var assets = await client.ListAssetsAsync(new Alpaca.Markets.AssetsRequest { AssetStatus = AssetStatus.Active }, cancellationToken);

    return (true, $"Trading API answered, {assets.Count} assets");
  }

  private async Task<(bool, string)> TestBusinessQuant(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    var baseUrl = _configuration["BusinessQuantProviderOptions:BaseUrl"] ?? "https://data.businessquant.com";

    var api = RestService.For<IBusinessQuantApi>(baseUrl);

    await api.GetQuotesAsync(credentials["ApiKey"], "AAPL", "eod", null, null, null, 1, cancellationToken: cancellationToken);

    return (true, "Quote answered");
  }

  private async Task<(bool, string)> TestBinance(CancellationToken cancellationToken)
  {
    var baseUrl = _configuration["BinanceProviderOptions:BaseUrl"] ?? "https://api.binance.com";

    var api = RestService.For<IBinanceApi>(baseUrl);

    var klines = await api.GetKlinesAsync("BTCUSDT", "1d", 1, cancellationToken: cancellationToken);

    return (true, $"{klines.Count} candle(s) received");
  }

  private static async Task<(bool, string)> TestYahoo(CancellationToken cancellationToken)
  {
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    // Yahoo answers anonymous chart requests only to browser-looking clients
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64)");

    var response = await http.GetAsync("https://query1.finance.yahoo.com/v8/finance/chart/AAPL?range=5d&interval=1d", cancellationToken);

    return response.IsSuccessStatusCode
      ? (true, "Chart endpoint answered")
      : (false, $"Yahoo answered {(int)response.StatusCode} {response.ReasonPhrase}");
  }

  private static async Task<(bool, string)> TestTelegram(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    var bot = new TelegramBotClient(credentials["Token"]);

    var me = await bot.GetMe(cancellationToken);

    // link buttons are optional, but when they are asked for, Telegram's own URL check
    // decides whether the bot can deliver anything at all - so an address it refuses is a
    // failure of this probe, not a footnote on a success
    var wantsLinks = string.Equals(credentials.GetValueOrDefault("IncludeWebLinks"), "true", StringComparison.OrdinalIgnoreCase);

    if (wantsLinks && !PublicWebAddress.IsReachable(credentials.GetValueOrDefault("InstanceUrl")))
      return (false, $"Bot @{me.Username} answered, but link buttons need an Instance URL Telegram can reach: a public address, not localhost or a LAN name");

    return (true, $"Bot @{me.Username} answered");
  }

  private static async Task<(bool, string)> TestWhatsApp(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    var version = credentials.GetValueOrDefault("ApiVersion", "v23.0");

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {credentials["AccessToken"]}");

    var response = await http.GetAsync($"https://graph.facebook.com/{version}/{credentials["PhoneNumberId"]}?fields=id,name", cancellationToken);

    return response.IsSuccessStatusCode
      ? (true, "Phone number id confirmed")
      : (false, $"Graph answered {(int)response.StatusCode} {response.ReasonPhrase}");
  }

  private static async Task<(bool, string)> TestSmtp(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    var host = credentials["Host"];
    var port = credentials.TryGetValue("Port", out var portValue) && int.TryParse(portValue, out var parsed) ? parsed : 25;
    var useSsl = credentials.TryGetValue("UseSsl", out var sslValue) && bool.Parse(sslValue);

    using var client = new SmtpClient();

    await client.ConnectAsync(
      host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

    try
    {
      if (credentials.TryGetValue("Username", out var username) && username.Length > 0)
        await client.AuthenticateAsync(username, credentials.GetValueOrDefault("Password", ""), cancellationToken);
    }
    finally
    {
      await client.DisconnectAsync(true, cancellationToken);
    }

    return (true, "Server accepted the credentials");
  }

  private static (bool, string) TestWebPush(IReadOnlyDictionary<string, string> credentials)
  {
    // The keys are generated on first start when absent; the test validates what is there
    if (!credentials.TryGetValue("VapidPublicKey", out var publicKey) || !credentials.TryGetValue("VapidPrivateKey", out var privateKey))
      return (false, "Keys are generated on the next start (or fill them in manually)");

    try
    {
      if (Base64Url.Decode(publicKey).Length != 65 || Base64Url.Decode(privateKey).Length != 32)
        return (false, "VAPID pair is malformed (expected a 65-byte public point and a 32-byte private scalar)");
    }
    catch (Exception exception) when (exception is FormatException or ArgumentException)
    {
      return (false, "VAPID pair is not valid base64url");
    }

    return (true, "VAPID pair is valid");
  }

  private static async Task<(bool, string)> TestLogos(IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken)
  {
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    if (credentials.TryGetValue("LogoDevPublishableKey", out var logoKey))
    {
      var response = await http.GetAsync($"https://img.logo.dev/apple.com?token={Uri.EscapeDataString(logoKey)}", cancellationToken);

      return response.IsSuccessStatusCode
        ? (true, "Logo.dev delivered a logo")
        : (false, $"Logo.dev answered {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    if (credentials.TryGetValue("CoinGeckoApiKey", out var geckoKey))
    {
      http.DefaultRequestHeaders.Add("x-cg-demo-api-key", geckoKey);

      var response = await http.GetAsync("https://api.coingecko.com/api/v3/ping", cancellationToken);

      return response.IsSuccessStatusCode
        ? (true, "CoinGecko answered")
        : (false, $"CoinGecko answered {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    return (false, "Fill in at least one of the two keys");
  }
}
