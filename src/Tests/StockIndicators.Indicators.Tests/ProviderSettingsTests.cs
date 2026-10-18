using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Providers;
using StockIndicators.Application.Providers.GetProviders;
using StockIndicators.Application.Providers.SaveProviderSettings;
using StockIndicators.Application.Providers.TestProvider;
using StockIndicators.Contracts.Providers;
using StockIndicators.Internal;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The settings page's backend: effective state (database over environment, secrets
/// masked), save semantics (blank keeps, clear removes) and the connection-test plumbing.
/// </summary>
public class ProviderSettingsTests
{
  private sealed class FakeStore : IProviderSettingsStore
  {
    public Dictionary<string, ProviderSettingValues> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyDictionary<string, ProviderSettingValues>> LoadAllAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyDictionary<string, ProviderSettingValues>>(Rows);

    public Task SaveAsync(string code, bool isEnabled, IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken = default)
    {
      Rows[code] = new ProviderSettingValues(isEnabled, new Dictionary<string, string>(credentials));

      return Task.CompletedTask;
    }
  }

  private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
    new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

  // ---------- GET ----------

  [Fact]
  public async Task SecretsComeBackInFullForTheRevealToggle()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "abcdefghijklmnop", ["IsSandbox"] = "true" });

    var providers = await new GetProvidersQueryHandler(store, Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    var tinkoff = providers.Single(p => p.Code == "tinkoff");

    var token = tinkoff.Fields.Single(f => f.Name == "Token");

    Assert.True(token.HasStored);
    Assert.Equal("abcdefghijklmnop", token.Value);

    // a non-secret round-trips in full so the UI can edit it
    Assert.Equal("true", tinkoff.Fields.Single(f => f.Name == "IsSandbox").Value);
    Assert.True(tinkoff.Enabled);
    Assert.True(tinkoff.Configured);
  }

  [Fact]
  public async Task WhatsAppIsDisabledUntilTheOperatorTurnsItOn()
  {
    // WhatsApp is the one channel that must not look "on" by default: Meta credentials are
    // paid and rare, so the toggle starts off and the sender honors it
    var providers = await new GetProvidersQueryHandler(new FakeStore(), Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.False(providers.Single(provider => provider.Code == "whatsapp").Enabled);
    // the free channels keep coming up ready when configured
    Assert.True(providers.Single(provider => provider.Code == "telegram").Enabled);
  }

  [Fact]
  public async Task RestartPendingMarksOnlyProvidersWhoseStoredRowMovedSinceStartup()
  {
    var startup = new ProviderSettingsSnapshot();
    startup.Set("whatsapp", new ProviderSettingValues(false, new Dictionary<string, string> { ["AccessToken"] = "old", ["Blank"] = "" }));
    startup.Freeze();

    var store = new FakeStore();
    // untouched row (a blank stored value was never wired, so it is not a change either)
    store.Rows["whatsapp"] = new ProviderSettingValues(false, new Dictionary<string, string> { ["AccessToken"] = "old", ["Blank"] = "" });
    // saved after startup: the toggle flipped
    store.Rows["telegram"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "t" });

    var providers = await new GetProvidersQueryHandler(store, Configuration(), startup).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.False(providers.Single(provider => provider.Code == "whatsapp").RestartPending);
    Assert.True(providers.Single(provider => provider.Code == "telegram").RestartPending);

    // and without a frozen startup copy nothing can honestly claim to wait for a restart
    var withoutStartup = await new GetProvidersQueryHandler(store, Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.All(withoutStartup, provider => Assert.False(provider.RestartPending));
  }

  [Fact]
  public async Task SavingTheEnvironmentValuesBackIsNotARestartChange()
  {
    // the reported bug: nothing was stored at startup, the operator saved and then reverted -
    // the row exists but repeats what the environment already gives, so nothing waits for a restart
    var startup = new ProviderSettingsSnapshot();
    startup.Freeze();

    var configuration = Configuration(
      ("TinkoffProviderOptions:IsEnabled", "true"), ("TinkoffProviderOptions:Token", "env-token"), ("TinkoffProviderOptions:IsSandbox", "True"));

    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "env-token", ["IsSandbox"] = "true" });

    var reverted = await new GetProvidersQueryHandler(store, configuration, startup).Handle(new GetProvidersQuery(), CancellationToken.None);

    // the stored bool ("true") against the environment one ("True") is still the same value
    Assert.False(reverted.Single(provider => provider.Code == "tinkoff").RestartPending);

    // the same row with a value the environment does not give is a real change
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "another-token", ["IsSandbox"] = "true" });

    var changed = await new GetProvidersQueryHandler(store, configuration, startup).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.True(changed.Single(provider => provider.Code == "tinkoff").RestartPending);
  }

  [Fact]
  public async Task ClearingAStoredFieldBackToTheSameEnvironmentValueIsNotARestartChange()
  {
    // the mirror case: a stored token equal to the environment one is cleared - the field now
    // reads from the environment, the effective state never moved, so no restart is pending
    var startup = new ProviderSettingsSnapshot();
    startup.Set("tinkoff", new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "env-token" }));
    startup.Freeze();

    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string>());

    var configuration = Configuration(("TinkoffProviderOptions:IsEnabled", "true"), ("TinkoffProviderOptions:Token", "env-token"));

    var providers = await new GetProvidersQueryHandler(store, configuration, startup).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.False(providers.Single(provider => provider.Code == "tinkoff").RestartPending);
  }

  [Fact]
  public async Task TheDatabaseToggleWinsOverTheEnvironment()
  {
    var store = new FakeStore();
    store.Rows["alpaca"] = new ProviderSettingValues(false, new Dictionary<string, string> { ["ApiKey"] = "k", ["ApiSecret"] = "s" });

    var configuration = Configuration(("AlpacaProviderOptions:IsEnabled", "true"));

    var providers = await new GetProvidersQueryHandler(store, configuration).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.False(providers.Single(p => p.Code == "alpaca").Enabled);
  }

  [Fact]
  public async Task EnvironmentValuesCountAsConfiguredAndAreShownAsEnvSourced()
  {
    var configuration = Configuration(("TinkoffProviderOptions:IsEnabled", "true"), ("TinkoffProviderOptions:Token", "env-token"));

    var providers = await new GetProvidersQueryHandler(new FakeStore(), configuration).Handle(new GetProvidersQuery(), CancellationToken.None);

    var tinkoff = providers.Single(p => p.Code == "tinkoff");

    Assert.True(tinkoff.Enabled);
    Assert.True(tinkoff.Configured);

    var token = tinkoff.Fields.Single(f => f.Name == "Token");

    Assert.False(token.HasStored);
    Assert.True(token.HasEnv);
  }

  [Fact]
  public async Task EnabledWithoutKeysIsNotConfigured()
  {
    var store = new FakeStore();
    store.Rows["alpaca"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["ApiKey"] = "only-one-of-two" });

    var providers = await new GetProvidersQueryHandler(store, Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    var alpaca = providers.Single(p => p.Code == "alpaca");

    Assert.True(alpaca.Enabled);
    Assert.False(alpaca.Configured);
  }

  [Fact]
  public async Task ChannelsAreEnabledByDefaultWhenTheEnvironmentNeverHadAToggle()
  {
    // before the settings page the bot simply ran whenever a token was present; the
    // absence of a stored row must not silently switch it off
    var providers = await new GetProvidersQueryHandler(new FakeStore(), Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    Assert.True(providers.Single(p => p.Code == "telegram").Enabled);
    // data providers, in contrast, opt in through their section
    Assert.False(providers.Single(p => p.Code == "tinkoff").Enabled);
  }

  [Fact]
  public async Task TheBotAddressesAreExposedAsUrlFieldsSoThePageCanCheckThem()
  {
    // Telegram validates a button URL on its own servers, so a loopback or LAN address has to
    // surface in the settings page - the field kind is what tells the UI to check the value
    var providers = await new GetProvidersQueryHandler(new FakeStore(), Configuration()).Handle(new GetProvidersQuery(), CancellationToken.None);

    var telegram = providers.Single(p => p.Code == "telegram");

    Assert.Equal("url", telegram.Fields.Single(f => f.Name == "InstanceUrl").Kind);
    Assert.Equal("url", telegram.Fields.Single(f => f.Name == "BotLink").Kind);

    // the rest of the panel is untouched by the new kind
    Assert.Equal("secret", telegram.Fields.Single(f => f.Name == "Token").Kind);

    // and the toggle that decides whether the address matters at all is an ordinary switch,
    // off unless the operator asks for links
    var links = telegram.Fields.Single(f => f.Name == "IncludeWebLinks");

    Assert.Equal("bool", links.Kind);
    Assert.False(links.Required);
  }

  // ---------- PUT ----------

  [Fact]
  public async Task EmptiedFieldsFallBackToTheEnvironmentAndClearFieldsRemoveThem()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "stored-token", ["IsSandbox"] = "true" });

    await new SaveProviderSettingsCommandHandler(store).Handle(
      new SaveProviderSettingsCommand("tinkoff", true, new Dictionary<string, string?> { ["Token"] = "  ", ["IsSandbox"] = "false" }, ["Nonsense"]),
      CancellationToken.None);

    // the UI only sends fields that moved, so an emptied token input is the operator removing
    // it on purpose - the stored value goes and the environment shows through again
    Assert.False(store.Rows["tinkoff"].Credentials.ContainsKey("Token"));
    Assert.Equal("false", store.Rows["tinkoff"].Credentials["IsSandbox"]);
  }

  [Fact]
  public async Task FieldsTheUiNeverSentStayUntouched()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "stored-token", ["IsSandbox"] = "true" });

    // saving only the toggle must not drop credentials the diff left out
    await new SaveProviderSettingsCommandHandler(store).Handle(
      new SaveProviderSettingsCommand("tinkoff", false, new Dictionary<string, string?>(), []),
      CancellationToken.None);

    Assert.Equal("stored-token", store.Rows["tinkoff"].Credentials["Token"]);
    Assert.Equal("true", store.Rows["tinkoff"].Credentials["IsSandbox"]);
    Assert.False(store.Rows["tinkoff"].IsEnabled);
  }

  [Fact]
  public async Task ClearedFieldsLeaveTheRow()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "stored", ["IsSandbox"] = "true" });

    await new SaveProviderSettingsCommandHandler(store).Handle(
      new SaveProviderSettingsCommand("tinkoff", true, new Dictionary<string, string?>(), ["IsSandbox"]),
      CancellationToken.None);

    Assert.False(store.Rows["tinkoff"].Credentials.ContainsKey("IsSandbox"));
    Assert.Equal("stored", store.Rows["tinkoff"].Credentials["Token"]);
  }

  [Fact]
  public async Task AnUnknownProviderIsNotFound()
  {
    await Assert.ThrowsAsync<UnknownProviderException>(() =>
      new SaveProviderSettingsCommandHandler(new FakeStore()).Handle(
        new SaveProviderSettingsCommand("yahoo-finance-pro", true, new Dictionary<string, string?>(), []),
        CancellationToken.None));
  }

  // ---------- test connection ----------

  private sealed class RecordingTester(Func<ProviderDescriptor, IReadOnlyDictionary<string, string>, Task<ProviderTestResultDto>> probe) : IProviderConnectionTester
  {
    public Func<ProviderDescriptor, IReadOnlyDictionary<string, string>, Task<ProviderTestResultDto>> Probe { get; } = probe;

    public Task<ProviderTestResultDto> TestAsync(ProviderDescriptor descriptor, IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken) =>
      Probe(descriptor, credentials);
  }

  [Fact]
  public async Task TypedValuesOnScreenWinOverStoredOnes()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "stored" });

    Dictionary<string, string> seen = [];

    var handler = new TestProviderCommandHandler(store, new RecordingTester((_, credentials) =>
    {
      foreach (var (key, value) in credentials)
        seen[key] = value;

      return Task.FromResult(new ProviderTestResultDto(true, "ok", 1));
    }), Configuration());

    var result = await handler.Handle(
      new TestProviderCommand("tinkoff", new Dictionary<string, string?> { ["Token"] = " typed ", ["IsSandbox"] = "" }),
      CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal("typed", seen["Token"]);
    Assert.False(seen.ContainsKey("IsSandbox"));
  }

  [Fact]
  public async Task MissingRequiredFieldsAreReportedBeforeAnyCall()
  {
    var probed = false;

    var handler = new TestProviderCommandHandler(
      new FakeStore(),
      new RecordingTester((_, _) =>
      {
        probed = true;

        return Task.FromResult(new ProviderTestResultDto(true, "ok", 1));
      }),
      Configuration());

    var result = await handler.Handle(new TestProviderCommand("alpaca", null), CancellationToken.None);

    Assert.False(result.Success);
    Assert.Contains("API key ID", result.Message);
    Assert.False(probed);
  }

  [Fact]
  public async Task AThrowingProbeBecomesAReadableFailure()
  {
    var store = new FakeStore();
    store.Rows["tinkoff"] = new ProviderSettingValues(true, new Dictionary<string, string> { ["Token"] = "t" });

    var handler = new TestProviderCommandHandler(
      store,
      new RecordingTester((_, _) => throw new InvalidOperationException("channel refused")),
      Configuration());

    var result = await handler.Handle(new TestProviderCommand("tinkoff", null), CancellationToken.None);

    Assert.False(result.Success);
    Assert.Contains("channel refused", result.Message);
  }

  // ---------- overlay ----------

  private sealed class ProbeOptions
  {
    public string? Token { get; set; }

    public bool IsSandbox { get; set; }

    public int Port { get; set; }
  }

  [Fact]
  public void StoredValuesAreLaidOverTheBoundOptions()
  {
    var snapshot = new ProviderSettingsSnapshot();
    snapshot.Set("tinkoff", new ProviderSettingValues(true, new Dictionary<string, string>
    {
      ["Token"] = "db-token",
      ["IsSandbox"] = "true",
      // unknown fields are ignored, they cannot break resolution
      ["Nonsense"] = "x",
    }));

    var services = new ServiceCollection();

    services.Configure<ProbeOptions>(o => o.Token = "env-token");
    services.OverlayProviderOptions<ProbeOptions>(snapshot, "tinkoff");

    var options = services.BuildServiceProvider().GetRequiredService<IOptions<ProbeOptions>>().Value;

    Assert.Equal("db-token", options.Token);
    Assert.True(options.IsSandbox);
  }

  [Fact]
  public void WithoutARowTheBoundValuesSurvive()
  {
    var services = new ServiceCollection();

    services.Configure<ProbeOptions>(o => o.Token = "env-token");
    services.OverlayProviderOptions<ProbeOptions>(new ProviderSettingsSnapshot(), "tinkoff");

    var options = services.BuildServiceProvider().GetRequiredService<IOptions<ProbeOptions>>().Value;

    Assert.Equal("env-token", options.Token);
  }

  // ---------- VAPID generation ----------

  [Fact]
  public void GeneratedVapidPairIsAnUncompressedPointAndAScalar()
  {
    var (publicKey, privateKey) = WebPushKeys.Generate();

    var point = Base64Url.Decode(publicKey);
    var scalar = Base64Url.Decode(privateKey);

    Assert.Equal(65, point.Length);
    Assert.Equal(0x04, point[0]);
    Assert.Equal(32, scalar.Length);
    Assert.DoesNotContain('=', publicKey);
    Assert.DoesNotContain('+', publicKey);
  }
}
