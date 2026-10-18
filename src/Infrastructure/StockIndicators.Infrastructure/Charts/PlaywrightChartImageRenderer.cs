using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Infrastructure.Charts;

/// <summary>
/// Renders chart PNGs for the alert channels by driving a headless Chromium over the
/// frontend's self-contained chart renderer page (Microsoft.Playwright). The page draws
/// through the same chart core as the web app and receives the recipient's chart options
/// in the injected request, so an alert cannot show a different chart than the app.
/// The browser process is launched lazily on the first render and reused; a crashed or
/// never-launched browser makes renders fail (the callers send a text-only alert), and a
/// missing configuration silently switches image rendering off.
/// </summary>
public class PlaywrightChartImageRenderer : IChartImageRenderer, IDisposable
{
  /// <summary>
  /// Concurrent pictures the one browser renders. Alerts are bursty and the public link
  /// preview endpoint is anonymous (every messenger unfurl reaches it), while a chart costs
  /// a page, a layout and a screenshot: the gate keeps a link shared into a large group
  /// from queueing behind itself and from starving the alert pipeline.
  /// </summary>
  private const int MaxConcurrentRenders = 2;

  /// <summary>How long a render waits for a free slot before the caller degrades.</summary>
  private static readonly TimeSpan RenderSlotTimeout = TimeSpan.FromSeconds(15);

  // The renderer page protocol (frontend render/main.ts + scripts/inline-render.mjs)
  private const string RequestScriptName = "__SI_REQUEST__";
  private const string DoneFunction = "() => window.__SI_DONE__ === true";
  private const string ErrorExpression = "() => window.__SI_ERROR__ ?? null";
  private const string ScreenshotSelector = "#si-root";

  private const float RenderTimeoutMs = 20_000f;

  // The page's chart colors are the light-theme palette (the app theme is not persisted);
  // the image is always light like the alert images have always been
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private readonly ChartImageOptions _options;
  private readonly ILogger _logger;
  private readonly SemaphoreSlim _launchLock = new(1, 1);
  private readonly SemaphoreSlim _renderGate = new(MaxConcurrentRenders, MaxConcurrentRenders);
  private readonly object _browserGate = new();
  private IPlaywright? _playwright;
  private IBrowser? _browser;
  private bool _pageWarned;

  public PlaywrightChartImageRenderer(IOptions<ChartImageOptions> options, ILogger logger)
  {
    _options = options.Value;
    _logger = logger.ForContext<PlaywrightChartImageRenderer>();
  }

  public async Task<byte[]?> RenderAsync(
    ChartData chartData,
    UserLocale locale,
    ChartRenderOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    var shape = options ?? ChartRenderOptions.Alert();

    if (string.IsNullOrWhiteSpace(_options.PageHtmlPath))
    {
      WarnOnce("ChartImage:PageHtmlPath is not configured, sending alerts without a chart image");

      return null;
    }

    // The renderer page is re-read on every render: it is small (~200 KB), and a dev
    // machine rebuilds it with the frontend while the API keeps running
    string pageHtml;

    try
    {
      pageHtml = await File.ReadAllTextAsync(_options.PageHtmlPath!, cancellationToken);
    }
    catch (IOException)
    {
      WarnOnce($"ChartImage:PageHtmlPath points at an unreadable file ({_options.PageHtmlPath}), sending alerts without a chart image");

      return null;
    }

    var browser = await GetBrowserAsync(cancellationToken);

    if (browser is null)
      return null;

    if (!await _renderGate.WaitAsync(RenderSlotTimeout, cancellationToken))
    {
      _logger.Warning("Chart image renderer: no render slot freed within {Timeout}, dropping the picture", RenderSlotTimeout);

      return null;
    }

    try
    {
      var context = await browser.NewContextAsync(new BrowserNewContextOptions
      {
        ViewportSize = new ViewportSize { Width = shape.Width, Height = shape.Height },
        DeviceScaleFactor = shape.DeviceScaleFactor,
        // the chart axis labels and legend numbers localize through Intl
        Locale = locale.Language,
      });

      try
      {
        await context.AddInitScriptAsync(BuildRequestScript(chartData, locale, shape));

        var page = await context.NewPageAsync();

        // SetContent runs a fresh document; the page has no network requests (the bundle and
        // styles are inlined), so Load completes as soon as the entry script ran
        await page.SetContentAsync(pageHtml, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

        await page.WaitForFunctionAsync(DoneFunction, null, new PageWaitForFunctionOptions { Timeout = RenderTimeoutMs });

        var renderError = await page.EvaluateAsync<string?>(ErrorExpression);

        if (!string.IsNullOrEmpty(renderError))
          throw new InvalidOperationException($"Chart renderer page failed: {renderError}");

        return await page.Locator(ScreenshotSelector).ScreenshotAsync();
      }
      finally
      {
        await context.CloseAsync();
      }
    }
    finally
    {
      _renderGate.Release();
    }
  }

  private static string BuildRequestScript(ChartData chartData, UserLocale locale, ChartRenderOptions shape)
  {
    // The page's RenderRequest protocol: the chart payload plus the recipient's language,
    // legend captions and chart display options (magnet is inert on a still image)
    var request = new
    {
      data = chartData,
      locale = locale.Language,
      labels = new
      {
        o = LocalizationCatalog.Get(LocalizationKey.ChartLegendOpen, locale.Language),
        h = LocalizationCatalog.Get(LocalizationKey.ChartLegendHigh, locale.Language),
        l = LocalizationCatalog.Get(LocalizationKey.ChartLegendLow, locale.Language),
        c = LocalizationCatalog.Get(LocalizationKey.ChartLegendClose, locale.Language),
        vol = LocalizationCatalog.Get(LocalizationKey.ChartLegendVolume, locale.Language),
      },
      preferences = new { tooltip = locale.ShowOhlcTooltip, magnet = false },
      // the picture's shape: the alert chart, or the wide link-preview card whose headline
      // the page draws over the chart (only the card carries a title)
      mode = shape.Mode,
      size = new { width = shape.Width, height = shape.Height, scale = shape.DeviceScaleFactor },
      title = shape.Title,
      subtitle = shape.Subtitle,
      eyebrow = shape.Eyebrow,
    };

    // JSON is a valid JS object literal and the serializer escapes every string, so user
    // data (tickers in the chart title) cannot break out of the assignment
    return $"window.{RequestScriptName} = {JsonSerializer.Serialize(request, JsonOptions)};";
  }

  private async Task<IBrowser?> GetBrowserAsync(CancellationToken cancellationToken)
  {
    lock (_browserGate)
    {
      if (_browser is { IsConnected: true })
        return _browser;
    }

    await _launchLock.WaitAsync(cancellationToken);

    try
    {
      lock (_browserGate)
      {
        if (_browser is { IsConnected: true })
          return _browser;
      }

      DisposeBrowser();

      try
      {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
          Headless = true,
          // The renderer only ever loads the locally built, trusted chart page - and a plain
          // Docker container runs as root, where Chromium refuses to start without this flag
          // (its sandbox needs kernel privileges the container does not grant).
          Args = ["--no-sandbox"],
        });

        _logger.Information("Chart image renderer launched headless Chromium");
      }
      catch (PlaywrightException ex)
      {
        WarnOnce($"failed to launch headless Chromium for chart images ({ex.Message.Split('\n')[0]}); install it with the Playwright CLI");
        DisposeBrowser();

        return null;
      }

      return _browser;
    }
    finally
    {
      _launchLock.Release();
    }
  }

  private void DisposeBrowser()
  {
    lock (_browserGate)
    {
      try
      {
        _browser?.CloseAsync().GetAwaiter().GetResult();
      }
      catch (Exception)
      {
        // a dying browser is not worth throwing over
      }

      _browser = null;
      _playwright?.Dispose();
      _playwright = null;
    }
  }

  private void WarnOnce(string message)
  {
    if (_pageWarned)
      return;

    _pageWarned = true;
    _logger.Warning("Chart image renderer: {Message} (warned once)", message);
  }

  public void Dispose()
  {
    DisposeBrowser();
    _launchLock.Dispose();
    _renderGate.Dispose();
  }
}
