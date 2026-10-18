using Microsoft.AspNetCore.JsonPatch;
using Moq;
using Newtonsoft.Json;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Subscriptions.PatchSubscription;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// The subscription is a pure rule container: only interval/outputs/rule-link are patchable.
/// Instruments and indicators live on the rule and are edited through the rule endpoints,
/// so targeting them on the subscription patch fails in ApplyTo (or in the restricted-path
/// guard for the legacy /Indicator path).
/// </summary>
public class PatchSubscriptionInstrumentTests
{
  private readonly Guid _userId = Guid.NewGuid();

  private static ISerializer Serializer =>
    new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Log.Logger);

  private static Subscription Stored() =>
    new()
    {
      Id = Guid.NewGuid(),
      UserId = Guid.NewGuid(),
      Cooldown = TimeSpan.FromHours(1),
      Status = SubscriptionStatus.Active,
      Outputs = [new SubscriptionOutput { Channel = NotificationChannel.Telegram, ChatId = 1 }]
    };

  private static PatchSubscriptionCommand Patch(Guid subscriptionId, Guid userId, string path, object? value)
  {
    var patch = new JsonPatchDocument();
    patch.Replace(path, value);
    return new PatchSubscriptionCommand(subscriptionId, userId, patch);
  }

  private static Mock<ISubscriptionRepository> Repo(Subscription subscription)
  {
    var subscriptions = new Mock<ISubscriptionRepository>();
    subscriptions.Setup(r => r.GetByIdAsync(subscription.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
    return subscriptions;
  }

  [Fact]
  public async Task Patch_ChangesCooldownSeconds_Passes()
  {
    var subscription = Stored();
    var validator = new PatchSubscriptionCommandValidator(Repo(subscription).Object, new Mock<IRuleRepository>().Object, Serializer);

    var result = await validator.ValidateAsync(Patch(subscription.Id, _userId, "/CooldownSeconds", 1800));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")));
  }

  [Fact]
  public async Task Patch_ChangesTicker_IsInvalid()
  {
    // Instruments are edited by rebuilding the rule tree, not on the subscription
    var subscription = Stored();
    var validator = new PatchSubscriptionCommandValidator(Repo(subscription).Object, new Mock<IRuleRepository>().Object, Serializer);

    var result = await validator.ValidateAsync(Patch(subscription.Id, _userId, "/Ticker", "MSFT"));

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, error =>
      error.PropertyName == "PatchDocument" && error.ErrorMessage.Contains("target location") && error.ErrorMessage.Contains("not found"));
  }

  [Fact]
  public async Task Patch_ChangesExchangeName_IsInvalid()
  {
    var subscription = Stored();
    var validator = new PatchSubscriptionCommandValidator(Repo(subscription).Object, new Mock<IRuleRepository>().Object, Serializer);

    var result = await validator.ValidateAsync(Patch(subscription.Id, _userId, "/ExchangeName", "NASDAQ"));

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, error =>
      error.PropertyName == "PatchDocument" && error.ErrorMessage.Contains("target location") && error.ErrorMessage.Contains("not found"));
  }

  [Fact]
  public async Task Patch_ChangesIndicator_IsDenied()
  {
    // /Indicator stays on the restricted list: a flat-patch era client gets the same refusal
    var subscription = Stored();
    var validator = new PatchSubscriptionCommandValidator(Repo(subscription).Object, new Mock<IRuleRepository>().Object, Serializer);

    var result = await validator.ValidateAsync(Patch(subscription.Id, _userId, "/Indicator", "SMA"));

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, error => error.ErrorMessage == "Denied access to protected property");
  }
}
