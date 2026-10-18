using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace StockIndicators.Application.Configuration.Email;

/// <summary>
/// The single branded shell for every outgoing email (login codes, account notices,
/// future alert mails): brand accent bar, logo header, a card with the localized body,
/// an optional CTA button and a small footer. Markup is table-based with inline styles
/// only - the subset that mainstream mail clients (Gmail/Outlook/Apple Mail) render.
///
/// The logo is hotlinked from the web frontend root (<c>{siteBaseUrl}/email-logo.png</c>);
/// the image is intentionally an <c>img</c> because mail clients do not inline SVG.
/// The plain-text alternative is derived from the same content so the two parts of the
/// multipart message can never drift apart.
/// </summary>
public static class EmailLayout
{
  /// <summary>File name of the brand logo served from the frontend root.</summary>
  public const string LogoFile = "email-logo.png";

  private const string BrandName = "Stock Indicators";

  private const string FontStack = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

  /// <summary>
  /// The login code rendered as a centered, spaced-out block - a template element
  /// (not a localized string), dropped into the body through its <c>{code}</c> placeholder.
  /// </summary>
  public static string CodeBlock(string code) =>
    "<p style=\"margin:24px 0;text-align:center;\">" +
    "<span style=\"display:inline-block;padding:10px 24px;background-color:#f8fafc;border:1px solid #e2e8f0;" +
    $"border-radius:8px;font-family:'Courier New',Consolas,monospace;font-size:32px;font-weight:700;" +
    $"letter-spacing:10px;color:#0f172a;\">{Escape(code)}</span></p>";

  /// <summary>
  /// Wraps a localized subject/heading/body into the branded shell and derives the
  /// plain-text alternative. <paramref name="bodyHtml"/> is trusted markup from
  /// <c>LocalizationCatalog</c>; callers pre-encode any user-provided values substituted
  /// into it. <paramref name="siteBaseUrl"/> is the frontend origin (logo + footer link),
  /// the optional CTA is a centered button pointing at <paramref name="ctaUrl"/>.
  /// </summary>
  public static EmailContent Compose(
    string language,
    string subject,
    string heading,
    string bodyHtml,
    string siteBaseUrl,
    string? ctaUrl = null,
    string? ctaText = null,
    string? footerNote = null)
  {
    var site = siteBaseUrl.TrimEnd('/');
    var headingHtml = $"<h1 style=\"margin:0 0 16px;font-family:{FontStack};font-size:20px;line-height:28px;font-weight:700;color:#0f172a;\">{Escape(heading)}</h1>";

    var ctaHtml = ctaUrl is null
      ? string.Empty
      : "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\" style=\"padding:8px 0 4px;\">" +
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
        $"<td align=\"center\" bgcolor=\"#3b82f6\" style=\"background-color:#3b82f6;border-radius:8px;\">" +
        $"<a href=\"{Escape(ctaUrl)}\" style=\"display:inline-block;padding:12px 28px;font-family:{FontStack};" +
        $"font-size:15px;line-height:22px;font-weight:600;color:#ffffff;text-decoration:none;\">{Escape(ctaText ?? string.Empty)}</a>" +
        "</td></tr></table></td></tr></table>";

    var host = TryHost(site);
    var footerHtml =
      "<hr style=\"border:none;border-top:1px solid #e2e8f0;margin:28px 0 0;\">" +
      $"<p style=\"margin:16px 0 0;font-family:{FontStack};font-size:12px;line-height:18px;color:#94a3b8;\">" +
      Escape(footerNote ?? string.Empty) + "<br>" +
      $"<a href=\"{Escape(site)}\" style=\"color:#3b82f6;text-decoration:none;\">{Escape(host)}</a></p>";

    var html =
      "<!DOCTYPE html>" +
      $"<html lang=\"{language}\" xmlns=\"http://www.w3.org/1999/xhtml\">" +
      "<head>" +
      "<meta charset=\"utf-8\">" +
      "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
      "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
      $"<title>{Escape(subject)}</title>" +
      "</head>" +
      "<body style=\"margin:0;padding:0;background-color:#f1f5f9;-webkit-text-size-adjust:100%;\">" +
      "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f1f5f9;\">" +
      "<tr><td align=\"center\" style=\"padding:24px 12px;\">" +
      "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background-color:#ffffff;border-radius:12px;overflow:hidden;\">" +
      "<tr><td style=\"height:4px;line-height:4px;font-size:0;background-color:#16a34a;\">&nbsp;</td></tr>" +
      "<tr><td align=\"center\" style=\"padding:28px 32px 8px;\">" +
      $"<img src=\"{Escape(site)}/{LogoFile}\" width=\"180\" alt=\"{BrandName}\" style=\"display:block;width:180px;max-width:70%;height:auto;border:0;\">" +
      "</td></tr>" +
      $"<tr><td style=\"padding:16px 32px 0;\">{headingHtml}</td></tr>" +
      $"<tr><td style=\"padding:0 32px;font-family:{FontStack};font-size:15px;line-height:24px;color:#334155;\">{bodyHtml}</td></tr>" +
      $"<tr><td style=\"padding:8px 32px 0;\">{ctaHtml}</td></tr>" +
      $"<tr><td style=\"padding:0 32px 24px;\">{footerHtml}</td></tr>" +
      "</table>" +
      "</td></tr></table>" +
      "</body></html>";

    var text = BuildText(heading, bodyHtml, ctaUrl, ctaText, footerNote, host);

    return new EmailContent(subject, html, text);
  }

  /// <summary>
  /// The plain-text alternative of a composed body: anchors become their URL (or
  /// "text (url)" when the anchor text differs), <c>&lt;br&gt;</c>/<c>&lt;/p&gt;</c>
  /// become line breaks, everything else is stripped and unescaped.
  /// </summary>
  public static string ToPlainText(string html)
  {
    var text = AnchorRx.Replace(html, match =>
    {
      var url = WebUtility.HtmlDecode(match.Groups[1].Value);
      var label = WebUtility.HtmlDecode(StripRx.Replace(match.Groups[2].Value, string.Empty)).Trim();

      return label.Length == 0 || label == url ? url : $"{label} ({url})";
    });

    text = BreakRx.Replace(text, "\n");
    text = CloseBlockRx.Replace(text, "\n\n");
    text = StripRx.Replace(text, string.Empty);
    text = WebUtility.HtmlDecode(text).Replace("\r\n", "\n", StringComparison.Ordinal);
    text = NewlinesRx.Replace(text, "\n\n");

    return text.Trim();
  }

  private static string BuildText(string heading, string bodyHtml, string? ctaUrl, string? ctaText, string? footerNote, string host)
  {
    var builder = new StringBuilder(heading);
    builder.Append("\n\n").Append(ToPlainText(bodyHtml));

    if (ctaUrl is not null)
      builder.Append("\n\n").Append(string.IsNullOrEmpty(ctaText) ? ctaUrl : $"{ctaText}: {ctaUrl}");

    if (!string.IsNullOrEmpty(footerNote))
      builder.Append("\n\n").Append(footerNote).Append('\n').Append(host);

    return builder.ToString().Trim();
  }

  private static string TryHost(string siteBaseUrl) =>
    Uri.TryCreate(siteBaseUrl, UriKind.Absolute, out var uri) ? uri.Host : siteBaseUrl;

  private static string Escape(string value) => WebUtility.HtmlEncode(value);

  private static readonly Regex AnchorRx = new(@"(?is)<a\b[^>]*\bhref=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Compiled);
  private static readonly Regex BreakRx = new(@"(?i)<br\s*/?>", RegexOptions.Compiled);
  private static readonly Regex CloseBlockRx = new(@"(?i)</(p|div|td|tr|h[1-6]|li|blockquote)>", RegexOptions.Compiled);
  private static readonly Regex StripRx = new(@"(?s)<[^>]+>", RegexOptions.Compiled);
  private static readonly Regex NewlinesRx = new(@"\n{3,}", RegexOptions.Compiled);
}
