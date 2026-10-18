using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Notifications.Rendering;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;
using Xunit;

namespace StockIndicators.Indicators.Tests.Notifications;

public class AlertTemplateRendererTests
{
  private static readonly DateTime TriggeredAt = new(2024, 1, 15, 21, 30, 0, DateTimeKind.Utc);
  private static readonly UserLocale MoscowLocale = new(UserLocale.Russian, TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));
  private static readonly UserLocale NewYorkLocale = new(UserLocale.English, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));

  private static AlertItem CreateItem() =>
    new(
      Guid.NewGuid(),
      Guid.NewGuid(),
      "SBER",
      "MOEX",
      "SMA",
      "100.0000 (вверх)",
      TriggeredAt,
      Guid.NewGuid(),
      null
    );

  private static NotificationTemplate CreateTemplate(string template) =>
    new()
    {
      Id = Guid.NewGuid(),
      Template = template
    };

  [Fact]
  public void RenderLine_WithoutTemplate_TelegramUsesDefaultBulletLine()
  {
    var result = new AlertTemplateRenderer().RenderLine(NotificationChannel.Telegram, null, CreateItem(), UserLocale.Default);

    Assert.Equal("• <b>SBER</b> (SMA): 100.0000 (вверх)", result);
  }

  [Fact]
  public void RenderLine_WithoutTemplate_WebPushUsesAlertText()
  {
    var result = new AlertTemplateRenderer().RenderLine(NotificationChannel.WebPush, null, CreateItem(), UserLocale.Default);

    Assert.Equal("100.0000 (вверх)", result);
  }

  [Fact]
  public void RenderLine_FormatsTimeInUserTimezone()
  {
    // 21:30 UTC on 15 Jan is 00:30 on 16 Jan in Moscow
    var template = CreateTemplate("{{ticker}} {{indicator}} {{time}}");

    var result = new AlertTemplateRenderer().RenderLine(NotificationChannel.Telegram, template, CreateItem(), MoscowLocale);

    Assert.Equal("SBER SMA 2024-01-16 00:30", result);
  }

  [Fact]
  public void RenderLine_FormatsTimeInUserTimezoneNegativeOffset()
  {
    // 21:30 UTC on 15 Jan is 16:30 on 15 Jan in New York (EST)
    var template = CreateTemplate("{{time}}");

    var result = new AlertTemplateRenderer().RenderLine(NotificationChannel.Telegram, template, CreateItem(), NewYorkLocale);

    Assert.Equal("2024-01-15 16:30", result);
  }

  [Fact]
  public void RenderLine_ReplacesAllTokens()
  {
    var item = CreateItem();
    var template = CreateTemplate("{{ticker}}|{{exchange}}|{{indicator}}|{{alertText}}|{{time}}|{{subscriptionId}}");

    var result = new AlertTemplateRenderer().RenderLine(NotificationChannel.Telegram, template, item, UserLocale.Default);

    Assert.Equal(
      $"SBER|MOEX|SMA|100.0000 (вверх)|{TriggeredAt:yyyy-MM-dd HH:mm}|{item.SubscriptionId}",
      result
    );
  }
}
