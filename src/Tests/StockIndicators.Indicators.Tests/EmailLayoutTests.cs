using StockIndicators.Application.Configuration.Email;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class EmailLayoutTests
{
  private const string Site = "https://example.com/";

  [Fact]
  public void Compose_ShellsLocalizedContent_WithLogoHeadingAndFooter()
  {
    var content = EmailLayout.Compose("ru", "Тема", "Заголовок", "<p>Привет</p>", Site, footerNote: "Подвал");

    Assert.Contains("<html lang=\"ru\"", content.Html);
    Assert.Contains("<title>Тема</title>", content.Html);
    Assert.Contains("src=\"https://example.com/email-logo.png\"", content.Html);
    Assert.Contains("Заголовок", content.Html);
    Assert.Contains("<p>Привет</p>", content.Html);
    Assert.Contains("Подвал", content.Html);
    Assert.Contains("href=\"https://example.com\"", content.Html);
    Assert.Contains("viewport", content.Html);

    Assert.StartsWith("Заголовок", content.Text);
    Assert.Contains("Привет", content.Text);
    Assert.Contains("Подвал", content.Text);
    Assert.Contains("example.com", content.Text);
  }

  [Fact]
  public void Compose_AddsCenteredCta_WhenUrlPassed()
  {
    var content = EmailLayout.Compose(
      "en", "s", "h", "<p>b</p>", Site,
      ctaUrl: "https://example.com/en/dashboard/settings", ctaText: "Open account settings");

    Assert.Contains("href=\"https://example.com/en/dashboard/settings\"", content.Html);
    Assert.Contains("Open account settings", content.Html);
    Assert.Contains("Open account settings: https://example.com/en/dashboard/settings", content.Text);
  }

  [Fact]
  public void CodeBlock_EscapesValue()
  {
    var block = EmailLayout.CodeBlock("<b>123456");

    Assert.Contains("&lt;b&gt;123456", block);
    Assert.DoesNotContain("<b>123456", block);
  }

  [Fact]
  public void ToPlainText_FlattensAnchorsBreaksAndBlocks()
  {
    var text = EmailLayout.ToPlainText(
      "<p>a<br>b</p>\n<p><strong>x</strong> see <a href=\"https://e.com\">https://e.com</a></p>" +
      "<p>go <a href=\"https://e.com/2\">Open it</a></p>");

    Assert.Equal("a\nb\n\nx see https://e.com\n\ngo Open it (https://e.com/2)", text);
  }

  [Fact]
  public void Compose_TextAlternative_HasNoMarkup_AndKeepsCodeReadable()
  {
    var content = EmailLayout.Compose(
      "en", "Login code", "Your login code",
      $"<p>You requested a code.</p>\n{EmailLayout.CodeBlock("123456")}", Site);

    Assert.DoesNotContain("<", content.Text);
    Assert.Contains("123456", content.Text);
  }
}
