using FluentValidation;
using LazyCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenTelemetry.Metrics;
using Serilog;
using StackExchange.Redis;
using StockIndicators;
using StockIndicators.Application.Account;
using StockIndicators.Application.Auth;
using StockIndicators.Application.Users;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Email;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.CryptoNames;
using StockIndicators.Application.Instruments.Logos;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Application.EventBus.InMemory;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Domain.Models.Users;
using StockIndicators.Application.Rules;
using StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;
using StockIndicators.Envelope;
using StockIndicators.Filters;
using StockIndicators.HealthChecks;
using StockIndicators.Hubs;
using StockIndicators.Infrastructure.Configuration;
using StockIndicators.Infrastructure.Data;
using StockIndicators.Infrastructure.Database;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Infrastructure.Domain.Indicators;
using StockIndicators.Infrastructure.Domain.Instruments;
using StockIndicators.Infrastructure.Domain.Metrics;
using StockIndicators.Infrastructure.Domain.Rules;
using StockIndicators.Infrastructure.Domain.Subscriptions;
using StockIndicators.Infrastructure.Domain.Telegram;
using StockIndicators.Infrastructure.Domain.Users;
using StockIndicators.Infrastructure.Processing;
using StockIndicators.Application.Providers;
using StockIndicators.Application.Providers.TestProvider;
using StockIndicators.Application.PublicSite;
using StockIndicators.Enrichers.Yahoo;
using StockIndicators.Infrastructure.Domain.Providers;
using StockIndicators.Internal;
using StockIndicators.Providers.Alpaca;
using StockIndicators.Providers.Binance;
using StockIndicators.Providers.BusinessQuant;
using StockIndicators.Providers.Tinkoff;
using StockIndicators.Providers.Base.Exchange;
using StockIndicators.PublicSite;

// `dotnet run -- hash-password [password]` - the setup helper the README points at: prints the
// digest for LocalAuth:PasswordHash. Without an argument the password is read from stdin, so
// it never has to end up in shell history.
if (args.Length > 0 && args[0] == "hash-password")
{
  var plain = args.Length > 1
    ? args[1]
    : Console.In.ReadLine() ?? throw new InvalidOperationException("No password on stdin");

  Console.WriteLine(LocalPasswordHash.Create(plain));
  return;
}

var webBuilder = WebApplication.CreateBuilder(args);

// Per-machine overrides: appsettings.Local.json is git- and docker-ignored, so a developer
// keeps their keys and connection strings next to appsettings.json instead of exporting
// environment variables. It is the topmost layer - it also wins over environment variables.
webBuilder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);

// Whether this instance publishes the public site only - landing, indicator catalog, legal,
// link-preview pictures - or the whole self-hosted product. Decided before the container is
// built, because it is what keeps a public instance away from the database entirely.
var publicSite = webBuilder.Configuration.GetSection(PublicSiteOptions.SectionName).Get<PublicSiteOptions>() is { LandingOnly: true };

// Which providers get wired at all is decided while the container is being built, so the
// stored settings are read before it exists. A missing table (first start) or an
// unreachable database simply means the environment carries everything, as it did before
// the settings page existed. A public-site instance has no providers to wire and no database
// to read from, so it starts on an empty snapshot without asking one.
var providerSettings = publicSite
  ? ProviderSettingsBootstrap.Empty()
  : ProviderSettingsBootstrap.Load(webBuilder.Configuration.GetConnectionString("CandleStorage"));

webBuilder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());

if (publicSite)
{
  // The marketing surface over the static frontend: no session, no database, no providers,
  // no notifications, no hub. PublicSiteGate keeps everything else off the wire.
  webBuilder.Services.AddPublicSite();
}
else
{
  ConfigureServices(webBuilder.Services, webBuilder.Configuration, webBuilder.Environment);
  ConfigureContainer(webBuilder.Services, webBuilder.Configuration);
}

var webApplication = webBuilder.Build();

await Configure(webApplication);

webApplication.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment hostEnvironment)
{
  // the live view of the settings page's rows: bootstrap-loaded, updated in place on save,
  // read by the options overlays when options are first resolved
  services.AddSingleton(providerSettings);

  services.AddResponseCompression();
  services.AddHttpContextAccessor();

  services.AddDbContext<StockIndicatorsContext>(
    builder => builder
      .UseNpgsql(configuration.GetConnectionString("CandleStorage"), optionsBuilder => { optionsBuilder.MigrationsAssembly("StockIndicators.Infrastructure"); })
      .EnableSensitiveDataLogging(hostEnvironment.IsDevelopment())
      .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
  );
  
  // Restore active subscription instruments before providers start (hosted services run in registration order)
  services.AddHostedService<InitInstruments>();

  // Warm up the instruments list cache first among the hosted services: the host awaits
  // each service's StartAsync in registration order, so providers' consumers and all
  // other background services start only after the initial load completes (or hits
  // InstrumentsWarmupOptions:InitialWarmupTimeout).
  services.AddHostedService<InstrumentsWarmupService>();
  services.RegisterOptions<InstrumentsWarmupOptions>();

  // Providers wire in only when enabled and configured: enabled-without-keys is a state to
  // announce, not to half-build (a keyless client would spend the log arguing with the API).
  // The decision is made once per start; the settings page says "restart to apply".
  var wiredProviderCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  WireProvider("tinkoff", () => services.AddTinkoffProvider(configuration), () => services.OverlayProviderOptions<TinkoffProviderOptions>(providerSettings, "tinkoff"));
  WireProvider("yahoo", () => services.AddYahooEnricher(configuration), () => services.OverlayProviderOptions<YahooEnricherOptions>(providerSettings, "yahoo"));
  WireProvider("alpaca", () => services.AddAlpacaProvider(configuration), () => services.OverlayProviderOptions<AlpacaProviderOptions>(providerSettings, "alpaca"));
  WireProvider("businessquant", () => services.AddBusinessQuantProvider(configuration), () => services.OverlayProviderOptions<BusinessQuantProviderOptions>(providerSettings, "businessquant"));
  WireProvider("binance", () => services.AddBinanceProvider(configuration), () => services.OverlayProviderOptions<BinanceProviderOptions>(providerSettings, "binance"));

  void WireProvider(string code, Action register, Action overlay)
  {
    if (!providerSettings.IsEnabled(configuration, code))
      return;

    if (!providerSettings.IsConfigured(configuration, code))
    {
      Log.Warning(
        "Provider '{Code}' is enabled but its required credentials are missing - it stays off until they are set (Settings > Providers) and the instance restarts",
        code);

      return;
    }

    register();
    overlay();
    wiredProviderCodes.Add(code);
  }

  // Data providers absent from the process: their rows in the persistent Instruments table
  // are dead catalog links (the detail/chart path has no source to answer), so the warm-up
  // sync prunes them. Wired-but-failing providers are not here and keep their rows.
  services.AddSingleton<IUnwiredDataProviders>(new UnwiredDataProviders(
    ProviderRegistry.All
      .Where(d => d.Kind == ProviderKind.Data && !wiredProviderCodes.Contains(d.Code))
      .Select(d => d.Code)));

  services.RegisterOptions<RedisConfiguration>();

  // Health checks for the frontend overlay (it polls /api/health): Postgres is hard-required
  // (Unhealthy → 503), Redis is best-effort (Degraded at worst, see RedisBestEffortHealthCheck).
  // ";Timeout=5" overrides Npgsql's 15s connection timeout so the endpoint stays fast when the
  // database is unreachable (the frontend gives up after 8s).
  services
    .AddHealthChecks()
    .AddNpgSql(configuration.GetConnectionString("CandleStorage")! + ";Timeout=5", name: "postgres")
    .AddCheck<RedisBestEffortHealthCheck>("redis");

  // Metrics: OpenTelemetry collects the business meter (BusinessMetrics + the observable
  // database gauges) plus the .NET runtime (GC, thread pool), ASP.NET Core and HttpClient
  // request metrics, and the built-in EF Core / Npgsql meters (both emit on net10 without
  // extra packages). The Prometheus exporter serves them on /metrics, behind a switch:
  // an instance on someone's LAN has no prometheus until the owner adds one, and until
  // Metrics:Enabled says otherwise the endpoint stays a 404.
  services
    .AddOpenTelemetry()
    .WithMetrics(
      metrics => metrics
        .AddMeter(StockIndicatorsMetrics.MeterName)
        .AddMeter("Microsoft.EntityFrameworkCore")
        .AddMeter("Npgsql")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        // The default histogram buckets are millisecond-oriented (HTTP). The business
        // histograms live in seconds (and the delayed US candle lag far beyond 10s), so give
        // them explicit second-scale boundaries; otherwise every poll/check lands in one bucket.
        .AddView("si_provider_poll_duration", new ExplicitBucketHistogramConfiguration { Boundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 20, 30, 60] })
        .AddView("si_candle_finalizer_duration", new ExplicitBucketHistogramConfiguration { Boundaries = [0.005, 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5] })
        .AddView("si_check_batch_duration", new ExplicitBucketHistogramConfiguration { Boundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60] })
        .AddView("si_condition_evaluation_duration", new ExplicitBucketHistogramConfiguration { Boundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.25, 0.5, 1] })
        .AddView("si_alert_send_duration", new ExplicitBucketHistogramConfiguration { Boundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10] })
        .AddView("si_alert_flush_size", new ExplicitBucketHistogramConfiguration { Boundaries = [1, 5, 10, 25, 50, 100, 250] })
        .AddPrometheusExporter());

  // Notifications: the alert dispatcher and its channels. The telegram bot is one optional
  // channel among them - a self-hosted instance without a bot token must still boot, so the
  // bot (and its TelegramBotClient, which rejects an empty token) registers only when configured.
  services.RegisterOptions<TelegramBotOptions>();
  services.OverlayProviderOptions<TelegramBotOptions>(providerSettings, "telegram");
  services.AddNotifications();

  // the channel options the notifications pipeline sends through are overlaid too
  services.OverlayProviderOptions<StockIndicators.Application.Notifications.Channels.WebPushOptions>(providerSettings, "webpush");
  services.OverlayProviderOptions<StockIndicators.Application.Notifications.Channels.WhatsAppOptions>(providerSettings, "whatsapp");

  // The bot token may come from the settings page; the guard validates it, so even a bad
  // one leaves the instance serving - only the bot stays silent.
  if (providerSettings.IsEnabled(configuration, "telegram") &&
      !string.IsNullOrWhiteSpace(providerSettings.Value("telegram", "Token") ?? configuration[$"{nameof(TelegramBotOptions)}:{nameof(TelegramBotOptions.Token)}"]))
    services.AddTelegramBot();

  // Trigger charts: candle-cache charts + the alert image renderer (headless Chromium over
  // the frontend's renderer page; ChartImage:PageHtmlPath, null - images off). The config
  // section is "ChartImage" (not the class name), so it must be named explicitly
  services.RegisterOptions<ChartImageOptions>("ChartImage");
  services.AddTriggerCharts();

  // The anonymous link-preview pictures (og:image of the public pages): what they show is
  // fixed here, never by the request - see ShareImageOptions
  services.RegisterOptions<ShareImageOptions>("ShareImage");

  // Swagger
  services.AddSwaggerDocument();

  // Controllers
  services.AddControllers().AddNewtonsoftJson();

  services
    .AddOptions<MvcOptions>()
    .Configure(
      options =>
      {
        options.Filters.Add<EnvelopeExceptionFilter>();
        options.Filters.Add<EnvelopeActionFilter>();
      }
    );

  // How the instance faces the network. A self-hosted installation always answers false here (the
  // flag selects this whole branch), but /api/site/config reads the option in both shapes.
  services.RegisterOptions<PublicSiteOptions>(PublicSiteOptions.SectionName);

  // One account, one password, one cookie. The cookie is httpOnly + SameSite=Lax; the CSRF
  // origin guard below stays as defense-in-depth. 401 instead of a redirect - this is an API.
  services.RegisterOptions<LocalAuthOptions>(LocalAuthOptions.SectionName);

  var sessionLifetime = configuration.GetSection(LocalAuthOptions.SectionName).Get<LocalAuthOptions>()?.SessionLifetime
    ?? TimeSpan.FromDays(14);

  services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
      options.Cookie.Name = LocalAuthOptions.CookieName;
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
      options.ExpireTimeSpan = sessionLifetime;
      options.SlidingExpiration = true;
      options.Events.OnRedirectToLogin = context =>
      {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
      };
      options.Events.OnRedirectToAccessDenied = context =>
      {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
      };
    });

  services.AddAuthorization();

  // The single account of the installation, created before the first sign-in so that linking a
  // telegram chat or opening a subscription always has an owner to point at
  services.AddScoped<LocalUserProvisioner>();

  // Live chart updates hub (/hubs/chart): open pages join per-instrument groups, the candle
  // pipeline nudges them when an instrument's candles change (see ChartHub)
  services.AddSignalR();

  // MediatR

  services
    .AddMediatR(
      config => config
        .RegisterServicesFromAssemblies(typeof(StockIndicators.Application.AssemblyMarker).Assembly, typeof(StockIndicators.Infrastructure.AssemblyMarker).Assembly)
        .AddOpenBehavior(typeof(ValidationBehavior<,>))
    );

  // Event Bus — in-process event distribution

  services.AddSingleton<IEventBus, InMemoryEventBus>();

  // Subscribers are registered as keyed services, keyed by the handled event type
  services.Scan(scan => scan
    .FromAssemblyOf<StockIndicators.Application.AssemblyMarker>()
    .AddClasses(classes => classes.Where(type => type.GetInterfaces()
      .Any(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEventSubscriber<>))))
    .AsImplementedInterfaces(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEventSubscriber<>))
    .WithServiceKey(implementationType => implementationType.GetInterfaces()
      .First(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEventSubscriber<>))
      .GetGenericArguments()[0])
    .WithTransientLifetime());

  // FluentValidation

  services.AddValidatorsFromAssembly(typeof(StockIndicators.Application.AssemblyMarker).Assembly);

  // CORS for a frontend served from a different origin than the API (vite dev, or a separate
  // static host); a single-origin deployment never triggers it. No credentials: the session
  // cookie only rides same-origin requests.
  services.AddCors(options =>
  {
    options.AddPolicy("AllowFrontend", policy =>
      policy.WithOrigins([.. FrontendOrigins.ForEnvironment(configuration, hostEnvironment)])
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
  });
}


void ConfigureContainer(IServiceCollection services, IConfiguration configuration)
{
  services.AddScoped<IUserRepository, UserRepository>();
  services.AddScoped<IUserLocaleProvider, UserLocaleProvider>();
  services.AddScoped<ITelegramChatRepository, TelegramChatRepository>();
  services.AddScoped<IChatAccountResolver, ChatAccountResolver>();
  services.AddScoped<ISubscriptionRepository, CachingSubscriptionRepository>();
  services.AddScoped<ITriggerHistoryRepository, TriggerHistoryRepository>();
  services.AddScoped<ITriggerDeliveryRepository, TriggerDeliveryRepository>();
  services.AddScoped<ITriggerSnapshotRepository, TriggerSnapshotRepository>();
  services.AddScoped<ISubscriptionSnapshotRenderer, SubscriptionSnapshotRenderer>();
  services.AddScoped<IRuleRepository, RuleRepository>();
  services.AddScoped<ITrailingStopAnchorStore, TrailingStopAnchorStore>();

  // Rule engine: the composite evaluator (AND/OR) wraps the simple leaf evaluator
  services.AddScoped<IConditionEvaluator, SimpleConditionEvaluator>();
  services.AddScoped<IRuleEvaluator, CompositeConditionEvaluator>();
  services.AddScoped<IRuleActionExecutor, RuleActionExecutor>();

  DapperExtensions.DapperAsyncExtensions.Configure(typeof(CustomAutoClassMapper<>), null, new CustomPostgreSqlDialect());
  services.AddScoped<ISqlConnectionFactory>(_ => new NpgsqlConnectionFactory(configuration.GetConnectionString("CandleStorage")!));
  services.AddScoped<ICandleRepository, CachedCandleRepository>();

  services.AddSingleton<IAppCache, CachingService>();
  services.AddSingleton<IInstrumentCacheReader, InstrumentsCacheReader>();

  // Persistent instruments table: the warm-up mirrors provider lists into "Instruments"
  // (bulk copy + merge), CoinGecko supplies the human crypto asset names for the aliases
  services.AddSingleton<ICryptoAssetNames, CoinGeckoCryptoAssetNames>();
  services.AddSingleton<IInstrumentsTableSync, InstrumentsTableSync>();

  // Instrument logos: Redis-cached images resolved from the Tinkoff brand CDN, Logo.dev and
  // CoinGecko (in this order). Unconfigured sources (empty keys) answer without HTTP calls.
  services.RegisterOptions<LogoOptions>();
  services.OverlayProviderOptions<LogoOptions>(providerSettings, "logos");
  services.AddHttpClient(LogoHttpClients.Client, client => client.Timeout = TimeSpan.FromSeconds(10));
  services.AddSingleton<IInstrumentLogoStore, InstrumentLogoRedisStore>();
  services.AddSingleton<IInstrumentLogoSource, TinkoffBrandLogoSource>();
  services.AddSingleton<IInstrumentLogoSource, LogoDevLogoSource>();
  services.AddSingleton<IInstrumentLogoSource, CoinGeckoLogoSource>();
  services.AddSingleton<IConnectionMultiplexer>(provider =>
  {
    var connectionString = provider.GetRequiredService<IOptions<RedisConfiguration>>().Value.BuildConnectionString();

    // When Redis is not configured a dummy endpoint keeps the multiplexer disconnected:
    // the remote candle cache then fails closed and the in-memory cache works on its own.
    var endpoint = string.IsNullOrWhiteSpace(connectionString) ? "127.0.0.1:1" : connectionString;

    var options = new ConfigurationOptions
    {
      ConnectTimeout = 2000,
      SyncTimeout = 2000,
      AbortOnConnectFail = false
    };
    options.EndPoints.Add(endpoint);

    return ConnectionMultiplexer.Connect(options);
  });
  services.AddSingleton<ICandleStore, RedisCandleStore>();

  // Provider settings: the settings page's storage and its "check connection" probes
  services.AddScoped<IProviderSettingsStore, ProviderSettingsStore>();
  services.AddScoped<StockIndicators.Application.Onboarding.IAppSettingsStore, StockIndicators.Infrastructure.Domain.AppSettings.AppSettingsStore>();
  services.AddSingleton<IProviderConnectionTester, ProviderConnectionTester>();

  // Outgoing mail: nothing signs anyone in with it anymore, it is the transport for
  // notifications the operator wires up (and for the email alert channel)
  services.RegisterOptions<SmtpOptions>();
  services.OverlayProviderOptions<SmtpOptions>(providerSettings, "smtp");
  services.AddSingleton<IMailSender, SmtpMailSender>();

  // Refreshes the si_* database gauge snapshot (users, subscriptions by status, push)
  services.AddHostedService<DatabaseGaugeService>();

  // One-time Telegram chat-link deep-link tokens (the only chat-linking credential)
  services.RegisterOptions<TelegramLinkTokenOptions>();
  services.AddSingleton<ITelegramLinkTokenStore, TelegramLinkTokenStore>();
  // The language a chat picked with /language before it was linked to an account
  services.AddSingleton<IChatLanguageStore, ChatLanguageStore>();
  services.AddSingleton<IChartLiveNotifier, ChartHubNotifier>();
  services.AddSingleton<ICandleProvider, CandleProvider>();
  services.AddTradingSessionGate();
  services.AddSingleton<ISerializer>(provider => ActivatorUtilities.CreateInstance<DefaultSerializer>(provider, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }));
  services.AddSingleton<ISubscriptionCache, SubscriptionInMemoryCache>();
  services.AddSingleton<ICandleCache, CandleInMemoryCache>();
  services.AddSingleton<IIndicatorCache, IndicatorInMemoryCache>();

  services.AddSingleton<IInstrumentProvider, InstrumentProvider>();
  services.AddSingleton<InstrumentProvider>(provider => (InstrumentProvider)provider.GetRequiredService<IInstrumentProvider>());
  services.AddSingleton<ChartViewWatchRegistry>();
}

async Task Configure(WebApplication app)
{
  // A public-site instance owns no schema and no VAPID pair: both belong to the installation
  // whose account the alerts go to.
  if (!publicSite)
  {
    app.SeedContext();

    // Web Push works out of the box: the VAPID pair is generated once, stored like any
    // other provider's credentials, and readable on the settings page afterwards
    await app.EnsureWebPushKeysAsync();
  }

  // Honor X-Forwarded-For/Proto only for requests coming from trusted proxies (behind a reverse
  // proxy the peer IP otherwise is the proxy itself, which breaks per-IP rate limiting).
  // Without TRUSTED_PROXIES no proxy is trusted and the middleware is a no-op.
  var forwardedHeadersOptions = new ForwardedHeadersOptions
  {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
  };

  var trustedProxies = Environment.GetEnvironmentVariable("TRUSTED_PROXIES");
  if (!string.IsNullOrWhiteSpace(trustedProxies))
  {
    foreach (var entry in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      if (entry.Contains('/'))
        forwardedHeadersOptions.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(entry));
      else if (System.Net.IPAddress.TryParse(entry, out var proxyIp))
        forwardedHeadersOptions.KnownProxies.Add(proxyIp);
    }
  }

  app.UseForwardedHeaders(forwardedHeadersOptions);

  // Prometheus scrape endpoint, served before the request pipeline (auth/CSRF/compression
  // never see it). Off unless Metrics:Enabled is set: a scraper running in another container
  // is not a loopback peer, so the gate is an explicit opt-in rather than a network-position
  // guess; whoever turns it on has decided their network is theirs to scrape from. A
  // public-site instance registers no meters at all, so the endpoint is not offered to it.
  if (!publicSite)
  {
    var metricsEnabled = app.Configuration.GetValue("Metrics:Enabled", false);

    app.UseOpenTelemetryPrometheusScrapingEndpoint(
      context => metricsEnabled && context.Request.Path == "/metrics");
  }

  if (app.Environment.IsDevelopment())
  {
    // One line per request (method/path/status/duration) so mini app and API issues
    // are visible in Seq/console without guessing which calls actually arrive.
    app.UseSerilogRequestLogging();
    app.UseDeveloperExceptionPage();
  }
  else
  {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
  }

  app.UseHttpsRedirection();

  // A public-site instance publishes the marketing surface: the gate answers before anything
  // else touches the pipeline, so a private endpoint is not merely unauthorized here - it is
  // not there at all, and probing /swagger or /api/subscription gets the same 404 envelope an
  // unknown route produces.
  if (publicSite)
    app.UsePublicSiteGate();

  // Single-origin distribution: the published frontend lives in wwwroot (the Dockerfile
  // copies the web build next to the assembly). UseDefaultFiles serves dist/<locale>/index.html
  // SEO shells as directory documents; the SPA fallback below answers every other path, so
  // deep links like /ru/dashboard/settings survive a refresh.
  // Compression runs before the shells are served: the site-mode stamp below must see plain
  // text (and this is also what finally compresses the static assets and the fallback shell).
  app.UseResponseCompression();
  app.UseDefaultFiles();
  app.UseSiteModeScript(publicSite);
  app.UseStaticFiles();

  app.UseRouting();

  // The application's request handling: a public-site instance has no cross-origin frontend to
  // allow, no session to authenticate and no API documentation to publish.
  if (!publicSite)
  {
    app.UseCors("AllowFrontend");

    app.UseAuthentication();
    app.UseAuthorization();
  }

  // CSRF defense-in-depth for cookie-authenticated state-changing requests: SameSite=Lax
  // already stops cross-site posts, this also rejects a same-site request from an origin the
  // instance was not configured to be served from
  app.UseCsrfOriginGuard(FrontendOrigins.ForEnvironment(app.Configuration, app.Environment), LocalAuthOptions.CookieName);

  if (!publicSite)
    app.UseSwagger();

  // The default writer is plain text ("Healthy"); serialize the report instead so per-check
  // statuses (postgres/redis) are visible in the response body.
  app.MapHealthChecks(
    "/api/health",
    new HealthCheckOptions
    {
      ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
      {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.ToString(),
        entries = report.Entries.ToDictionary(
          entry => entry.Key,
          entry => new
          {
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.ToString()
          })
      })
    });

  app.MapControllers();

  // SPA deep links: anything that is not an API route, a hub, a file or the error page is a
  // client route - serve the app shell and let the router sort it out (404 pages included)
  app.MapFallbackToFile("index.html");

  // Live chart updates hub: the hub attribute authenticates with the session cookie,
  // which the browser carries on the WebSocket handshake like on any same-origin request
  if (!publicSite)
    app.MapHub<ChartHub>("/hubs/chart");

  // Fallback target of UseExceptionHandler in production: exceptions that escaped the MVC
  // filter (middleware, hubs, background work reaching the pipeline) surface here; report a
  // generic 500 envelope (same contract as EnvelopeExceptionFilter) and log the real one.
  app.MapGet(
    "/Error",
    (HttpContext httpContext) =>
    {
      var errorFeature = httpContext.Features.Get<IExceptionHandlerPathFeature>();

      Log.Error(
        errorFeature?.Error,
        "Unhandled exception escaped the MVC pipeline on {RequestMethod} {RequestPath}",
        httpContext.Request.Method,
        errorFeature?.Path ?? httpContext.Request.Path);

      return Results.Json(
        ErrorEnvelope.Create(500, "Unhandled exception, please check the application logs"),
        statusCode: StatusCodes.Status500InternalServerError);
    }
  );
}
