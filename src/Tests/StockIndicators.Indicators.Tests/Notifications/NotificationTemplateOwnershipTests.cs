using MediatR;
using Moq;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Notifications;
using StockIndicators.Application.Notifications.CreateNotificationTemplate;
using StockIndicators.Application.Notifications.DeleteNotificationTemplate;
using StockIndicators.Application.Notifications.UpdateNotificationTemplate;
using StockIndicators.Domain.Models.NotificationTemplates;
using Xunit;

namespace StockIndicators.Indicators.Tests.Notifications;

/// <summary>
/// Notification templates are isolated per user: a caller edits/deletes their own rows and
/// owner-less legacy rows (claim-on-edit); a foreign row is a 404, a bundled one is a 400.
/// Templates are channel-agnostic, so there is no channel to validate on the write paths.
/// </summary>
public class NotificationTemplateOwnershipTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Guid _otherUserId = Guid.NewGuid();

  private static NotificationTemplate Owned(Guid? userId, bool isBundled = false) => new()
  {
    Id = Guid.NewGuid(),
    Name = "tpl",
    Template = "{{ticker}} {{alertText}}",
    IsBundled = isBundled,
    Created = DateTime.UtcNow,
    UserId = userId,
  };

  // A row is editable only when not bundled and owned or owner-less; the raw read is the
  // second look the handlers use to split "bundled, read-only" (400) from "foreign/missing" (404)
  private static Mock<INotificationTemplateRepository> Repo(Guid userId, NotificationTemplate? editable, NotificationTemplate? any = null)
  {
    var repo = new Mock<INotificationTemplateRepository>();
    repo.Setup(r => r.GetEditableForUserAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(editable);
    repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(any ?? editable);
    return repo;
  }

  // --- Update ---

  [Fact]
  public async Task Update_Owner_WritesAndStampsUpdated()
  {
    var template = Owned(_userId);
    var repo = Repo(_userId, template);

    await new UpdateNotificationTemplateCommandHandler(repo.Object)
      .Handle(new UpdateNotificationTemplateCommand(_userId, template.Id, "renamed", "{{ticker}}"), CancellationToken.None);

    repo.Verify(r => r.UpdateAsync(It.Is<NotificationTemplate>(
      t => t.Name == "renamed" && t.Template == "{{ticker}}" && t.Updated != null && t.UserId == _userId), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Update_LegacyOwnerless_ClaimsForEditor()
  {
    var legacy = Owned(null);
    var repo = Repo(_userId, legacy);

    await new UpdateNotificationTemplateCommandHandler(repo.Object)
      .Handle(new UpdateNotificationTemplateCommand(_userId, legacy.Id, "mine now", "{{ticker}}"), CancellationToken.None);

    repo.Verify(r => r.UpdateAsync(It.Is<NotificationTemplate>(
      t => t.UserId == _userId && t.Name == "mine now"), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Update_Foreign_IsNotFound()
  {
    // Not editable for the caller; the raw read proves the row exists but belongs to someone else
    var repo = Repo(_userId, null, Owned(_otherUserId));

    await Assert.ThrowsAsync<NotificationTemplateNotFoundException>(() =>
      new UpdateNotificationTemplateCommandHandler(repo.Object)
        .Handle(new UpdateNotificationTemplateCommand(_userId, Guid.NewGuid(), "x", "{{ticker}}"), CancellationToken.None));

    repo.Verify(r => r.UpdateAsync(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Update_Bundled_IsReadOnly()
  {
    var bundled = Owned(null, isBundled: true);
    var repo = Repo(_userId, null, bundled);

    var ex = await Assert.ThrowsAsync<ValidationException>(() =>
      new UpdateNotificationTemplateCommandHandler(repo.Object)
        .Handle(new UpdateNotificationTemplateCommand(_userId, bundled.Id, "x", "{{ticker}}"), CancellationToken.None));

    Assert.Contains(ex.ErrorCodes!, e => e.Code == "template.bundledReadOnly");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]
  public async Task Update_InvalidName_IsRejected(string? name)
  {
    var repo = Repo(_userId, Owned(_userId));

    await Assert.ThrowsAsync<ValidationException>(() =>
      new UpdateNotificationTemplateCommandHandler(repo.Object)
        .Handle(new UpdateNotificationTemplateCommand(_userId, Guid.NewGuid(), name!, "{{ticker}}"), CancellationToken.None));

    repo.Verify(r => r.UpdateAsync(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Update_InvalidBody_IsRejected()
  {
    var repo = Repo(_userId, Owned(_userId));

    var ex = await Assert.ThrowsAsync<ValidationException>(() =>
      new UpdateNotificationTemplateCommandHandler(repo.Object)
        .Handle(new UpdateNotificationTemplateCommand(_userId, Guid.NewGuid(), "name", " "), CancellationToken.None));

    Assert.Contains(ex.ErrorCodes!, e => e.Code == "template.bodyRequired");
  }

  // --- Delete ---

  [Fact]
  public async Task Delete_Owner_Removes()
  {
    var template = Owned(_userId);
    var repo = Repo(_userId, template);

    await new DeleteNotificationTemplateCommandHandler(repo.Object)
      .Handle(new DeleteNotificationTemplateCommand(_userId, template.Id), CancellationToken.None);

    repo.Verify(r => r.DeleteAsync(template.Id, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Delete_LegacyOwnerless_Removes()
  {
    var legacy = Owned(null);
    var repo = Repo(_userId, legacy);

    await new DeleteNotificationTemplateCommandHandler(repo.Object)
      .Handle(new DeleteNotificationTemplateCommand(_userId, legacy.Id), CancellationToken.None);

    repo.Verify(r => r.DeleteAsync(legacy.Id, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Delete_Foreign_IsNotFound()
  {
    var repo = Repo(_userId, null, Owned(_otherUserId));

    await Assert.ThrowsAsync<NotificationTemplateNotFoundException>(() =>
      new DeleteNotificationTemplateCommandHandler(repo.Object)
        .Handle(new DeleteNotificationTemplateCommand(_userId, Guid.NewGuid()), CancellationToken.None));

    repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Delete_Bundled_IsReadOnly()
  {
    var bundled = Owned(null, isBundled: true);
    var repo = Repo(_userId, null, bundled);

    var ex = await Assert.ThrowsAsync<ValidationException>(() =>
      new DeleteNotificationTemplateCommandHandler(repo.Object)
        .Handle(new DeleteNotificationTemplateCommand(_userId, bundled.Id), CancellationToken.None));

    Assert.Contains(ex.ErrorCodes!, e => e.Code == "template.bundledReadOnly");
  }

  // --- Create ---

  [Fact]
  public async Task Create_StampsOwner()
  {
    var repo = new Mock<INotificationTemplateRepository>();
    NotificationTemplate? added = null;
    repo.Setup(r => r.AddAsync(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>()))
      .Callback((NotificationTemplate t, CancellationToken _) => added = t)
      .ReturnsAsync((NotificationTemplate t, CancellationToken _) => t);

    await new CreateNotificationTemplateCommandHandler(repo.Object)
      .Handle(new CreateNotificationTemplateCommand(_userId, "mine", "{{alertText}}"), CancellationToken.None);

    Assert.NotNull(added);
    Assert.Equal(_userId, added!.UserId);
    Assert.False(added.IsBundled);
  }

  [Fact]
  public async Task Create_InvalidName_IsRejected()
  {
    var repo = Repo(_userId, null);

    await Assert.ThrowsAsync<ValidationException>(() =>
      new CreateNotificationTemplateCommandHandler(repo.Object)
        .Handle(new CreateNotificationTemplateCommand(_userId, " ", "body"), CancellationToken.None));
  }
}
