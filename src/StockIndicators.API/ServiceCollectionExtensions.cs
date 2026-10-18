using System.Data;
using Drenalol.Telegram.Bot.Forms;
using Drenalol.Telegram.Bot.Forms.Localization;
using Drenalol.Telegram.Bot.Forms.Profile;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSwag;
using NSwag.Generation.Processors.Security;
using Refit;
using StockIndicators.Abstract;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Instruments.Logos;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Notifications.Rendering;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Infrastructure.Charts;
using StockIndicators.Internal;
using StockIndicators.Infrastructure.Database;
using StockIndicators.Infrastructure.Domain.NotificationTemplates;
using StockIndicators.Infrastructure.Domain.PushSubscriptions;
using WebPush;
using StockIndicators.Providers.Base.Exchange;
using StockIndicators.Providers.Tinkoff;
using StockIndicators.Providers.Tinkoff.Internal;
using Tinkoff.InvestApi;

namespace StockIndicators;

public static class ConfigureOptionsExtension
{
  public static IServiceCollection RegisterOptions<T>(
    this IServiceCollection services,
    string? configSectionPath = null,
    bool validateDataAnnotations = true
  ) where T : class
  {
    var optionsBuilder = services
      .AddOptions<T>()
      .BindConfiguration(configSectionPath ?? typeof(T).Name);

    if (validateDataAnnotations)
      optionsBuilder
        .ValidateDataAnnotations()
        .ValidateOnStart();

    return services;
  }
}

public static class ServiceCollectionExtensions
{
  public static void SeedContext(this IApplicationBuilder app)
  {
    using var scope = app.ApplicationServices.CreateScope();
    scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>().Database.Migrate();
  }

  public static IServiceCollection AddTinkoffProvider(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<TinkoffProviderOptions>()
      .Bind(configuration.GetSection(nameof(TinkoffProviderOptions)))
      .ValidateDataAnnotations()
      .ValidateOnStart();
    services.AddExchangeSession(configuration, ExchangeSessions.Moex);
    services.AddSingleton<TinkoffProvider>();
    services.AddHostedService<TinkoffProviderConsumerProducer>();
    services.AddInvestApiClient(
      (provider, settings) =>
      {
        var options = provider.GetRequiredService<IOptions<TinkoffProviderOptions>>().Value;
        settings.AccessToken = options.Token;
        settings.AppName = "StockIndicators";
      });

    // Dev-only TLS bridge (tools/tinkoff-tls-bridge), see TinkoffTlsBridgeHandler
    var tlsBridgeAddress = configuration[$"{nameof(TinkoffProviderOptions)}:{nameof(TinkoffProviderOptions.TlsBridgeAddress)}"];
    if (!string.IsNullOrEmpty(tlsBridgeAddress))
      services.AddGrpcClient<InvestApiClient>("", options =>
      {
        options.ChannelOptionsActions.Add(channelOptions =>
          channelOptions.HttpHandler = TinkoffTlsBridgeHandler.Create(tlsBridgeAddress));
      });
    services.AddSingleton<IInstrumentSource, TinkoffProviderClient>();
    services.AddSingleton<IInstrumentBrandLogoDirectory>(provider => provider.GetRequiredService<TinkoffProviderClient>());
    services.AddSingleton<TinkoffProviderClient>();
    services.AddMediatR(conf => conf.RegisterServicesFromAssembly(typeof(TinkoffProviderClient).Assembly));
    services.AddSingleton<CustomConnectionFactory>();
    return services;
  }

  /// <summary>
  /// The alert pipeline: dispatcher, channel senders, message templates and push
  /// subscriptions. Deliberately independent of the telegram bot - an instance without a bot
  /// token still boots and delivers on every other channel (a Telegram output simply has no
  /// sender, which the dispatcher records as "channel unavailable").
  /// </summary>
  public static IServiceCollection AddNotifications(this IServiceCollection services)
  {
    services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
    services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
    services.AddScoped<IAlertDispatcher, AlertDispatcher>();
    services.AddScoped<IAlertChannelSender, WebPushAlertChannelSender>();
    services.AddScoped<IAlertChannelSender, WebhookAlertChannelSender>();
    services.AddScoped<IAlertChannelSender, DiscordAlertChannelSender>();
    services.AddScoped<IAlertChannelSender, WhatsAppAlertChannelSender>();
    services.AddSingleton<IAlertTemplateRenderer, AlertTemplateRenderer>();
    services.AddSingleton<WebPushClient>();
    services.RegisterOptions<WebPushOptions>();
    services.RegisterOptions<AlertOptions>();
    services.RegisterOptions<WhatsAppOptions>();

    // User-targeted deliveries (Webhook/Discord/WhatsApp): the timeout is short because a
    // hanging destination must not stall the scheduler's flush loop
    services.AddHttpClient(AlertHttpClients.Delivery, client => client.Timeout = TimeSpan.FromSeconds(15));

    return services;
  }

  /// <summary>
  /// The telegram bot: forms, the update bus and its polling host (the library's). Register
  /// only when a bot token is configured - the library's client rejects an empty one, and
  /// the bot is an optional channel, not a startup requirement.
  /// </summary>
  public static IServiceCollection AddTelegramBot(this IServiceCollection services)
  {
    // Which language to answer each chat in - the linked account's preference, the choice
    // /language stored, or the default. Registered before the library call so its TryAdd
    // defaults step aside; the library asks this resolver for Back/pagination/error texts.
    services.AddSingleton<ITelegramTextLocalizer, BotTextsLocalizer>();
    services.AddScoped<IChatLanguageResolver, ChatLanguageResolver>();

    // The BotFather profile (name, welcome card, "/" menu) stays code-owned, in every locale,
    // and is pushed on each start; BotFather edits survive only until the next restart.
    services.Configure<BotProfileOptions>(profile =>
    {
      profile.Name = "Stock Indicators";
      profile.DefaultLanguage = UserLocale.DefaultLanguage;

      foreach (var descriptor in UserLocale.SupportedLanguages)
      {
        var texts = new BotProfileTexts
        {
          ShortDescription = LocalizationCatalog.Get(LocalizationKey.BotProfileShortDescription, descriptor.Code),
          Welcome = LocalizationCatalog.Get(LocalizationKey.BotProfileWelcome, descriptor.Code),
        };

        texts.Commands.Add(new BotProfileCommand("start", LocalizationCatalog.Get(LocalizationKey.BotCommandStart, descriptor.Code)));
        texts.Commands.Add(new BotProfileCommand("language", LocalizationCatalog.Get(LocalizationKey.BotCommandLanguage, descriptor.Code)));
        texts.Commands.Add(new BotProfileCommand("link", LocalizationCatalog.Get(LocalizationKey.BotCommandLink, descriptor.Code)));

        profile.Languages[descriptor.Code] = texts;
      }
    });

    // Keyboard button texts double as form lookup keys, so every language variant of a
    // button must be registered (localized buttons must resolve to the same form).
    // The loops iterate the language registry - a new language registers itself.
    var bot = services.AddTelegramBotForms()
      .RegisterStartForm<StartForm>()
      .RegisterForm<SubscriptionForm>()
      .RegisterForm<IndicatorDetailForm>()
      .RegisterForm<InstrumentDetailForm>()
      .RegisterForm<LanguageListForm>()
      .RegisterForm<TimeZoneListForm>();

    // Languages may legitimately share a button text ("Abonnements" in French and
    // German, "Indicadores" in Spanish and Portuguese): the configurator registers such
    // a text once and fails only when two different forms claim the same text.
    foreach (var language in UserLocale.SupportedLanguages.Select(descriptor => descriptor.Code))
    {
      bot
        .RegisterFormWithKey<SubscriptionListForm>(BotTexts.SubscriptionsButton(language))
        .RegisterFormWithKey<IndicatorsListForm>(BotTexts.IndicatorsButton(language))
        .RegisterFormWithKey<InstrumentsListForm>(BotTexts.InstrumentsButton(language))
        .RegisterFormWithKey<SettingsForm>(BotTexts.SettingsButton(language));
    }

    // /language is the entry to the bot's own language even for a chat that is not linked
    // yet, so both spellings of the command resolve to the picker (a plain message typed
    // without the leading slash behaves the same); "link <token>" binds a chat with the
    // one-time token from the settings page
    bot
      .RegisterFormWithKey<ChatLanguageForm>("/language")
      .RegisterFormWithKey<ChatLanguageForm>("language")
      .RegisterCommand<LinkCommand>("link");

    bot.Build();

    // The library's polling startup is fatal by design - a console bot with a bad token
    // should fail loudly. Here the bot is one optional channel of a web application, so
    // the guard hosts it instead of the host itself: an unusable token or Telegram
    // unreachable at boot becomes a logged error, not a dead instance.
    // AddHostedService hides the implementation type inside the IHostedService descriptor,
    // so that registration leaves the host's hands and the startup is re-registered under
    // its own type - the guard resolves and hosts that instance.
    var libraryStartup = services.Last(descriptor => descriptor.ImplementationType == typeof(TelegramBotStartup));
    services.Remove(libraryStartup);
    services.AddSingleton<TelegramBotStartup>();
    services.AddHostedService<TelegramBotGuard>();

    // The library rides the app's own bot options: one section keeps token and bot link.
    services.AddSingleton<IConfigureOptions<TelegramBotFormsOptions>>(provider =>
    {
      var options = provider.GetRequiredService<IOptions<TelegramBotOptions>>().Value;

      return new ConfigureOptions<TelegramBotFormsOptions>(libraryOptions =>
      {
        libraryOptions.Token = options.Token;
        libraryOptions.BotLink = options.BotLink;
      });
    });

    services.AddScoped<IAlertChannelSender, TelegramAlertChannelSender>();

    return services;
  }

  public static IServiceCollection AddTriggerCharts(this IServiceCollection services)
  {
    services.AddScoped<ITriggerChartService, TriggerChartService>();
    services.AddSingleton<IChartImageRenderer, PlaywrightChartImageRenderer>();

    return services;
  }

  public static IServiceCollection AddSwagger(this IServiceCollection services)
  {
    services.AddOpenApiDocument(
      d =>
      {
        d.Title = "Stock Indicators API";
        d.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("sso"));
      }
    );
    return services;
  }

  public static IApplicationBuilder UseSwagger(this IApplicationBuilder app)
  {
    app.UseOpenApi(c => c.PostProcess = (document, _) => document.Schemes = new List<OpenApiSchema> { OpenApiSchema.Https, OpenApiSchema.Http })
      .UseReDoc(
        config =>
        {
          config.Path = "/redoc";
          config.DocumentPath = "/swagger/v1/swagger.json";
        }
      );
    return app;
  }

  public static IServiceCollection Remove<T>(this IServiceCollection services)
  {
    if (services.IsReadOnly)
      throw new ReadOnlyException($"{nameof(services)} is read only");

    var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ImplementationType == typeof(T) || descriptor.ServiceType == typeof(T));

    if (serviceDescriptor != null)
      services.Remove(serviceDescriptor);

    return services;
  }
}
