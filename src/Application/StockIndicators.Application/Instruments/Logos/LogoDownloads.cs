using Serilog;

namespace StockIndicators.Application.Instruments.Logos;

internal static class LogoDownloads
{
  internal const int MaxBytes = 512_000;

  /// <summary>
  /// GETs a candidate URL and turns a small image response into <see cref="InstrumentLogoData"/>;
  /// any other status, a non-image content type or an oversized body yields null (the caller
  /// tries the next candidate or source).
  /// </summary>
  public static async Task<InstrumentLogoData?> DownloadAsync(HttpClient http, Uri url, string sourceName, CancellationToken cancellationToken)
  {
    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    if (!response.IsSuccessStatusCode)
      return null;

    var contentType = response.Content.Headers.ContentType?.MediaType;
    var length = response.Content.Headers.ContentLength;

    if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || length is > MaxBytes)
      return null;

    var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);

    if (data.Length is 0 or > MaxBytes)
      return null;

    Log.Debug("Downloaded instrument logo {Source} {Url} ({Bytes} bytes)", sourceName, url, data.Length);

    return new InstrumentLogoData(data, contentType, sourceName, DateTime.UtcNow);
  }
}
