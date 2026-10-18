using System.Text.Json;
using MediatR;
using Moq;
using StockIndicators.Application.Account.UpdateUserPreferences;
using StockIndicators.Application.Auth.GetMe;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Users;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class UpdateUserPreferencesTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Mock<IUserRepository> _users = new();
  private readonly Mock<IMediator> _mediator = new();
  private readonly UpdateUserPreferencesCommandHandler _handler;
  private readonly User _user;

  public UpdateUserPreferencesTests()
  {
    _user = new User { Id = _userId };
    _handler = new UpdateUserPreferencesCommandHandler(_users.Object, _mediator.Object);

    _users
      .Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(_user);

    var profile = new AuthProfileDto(
      _userId.ToString(), null, "ru", "Europe/Moscow", false, []);

    _mediator
      .Setup(m => m.Send(It.IsAny<GetMeQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(profile);
  }

  [Fact]
  public async Task BothProvided_SetsBothFields_AndCommits()
  {
    var result = await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, "ru", "Europe/Moscow"), CancellationToken.None);

    Assert.Equal("ru", _user.Language);
    Assert.Equal("Europe/Moscow", _user.Timezone);
    _users.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    Assert.NotNull(result);
  }

  [Fact]
  public async Task OnlyLanguageProvided_LeavesTimezoneUntouched()
  {
    _user.Timezone = "America/New_York";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, "es", null), CancellationToken.None);

    Assert.Equal("es", _user.Language);
    Assert.Equal("America/New_York", _user.Timezone);
  }

  [Fact]
  public async Task OnlyTimezoneProvided_LeavesLanguageUntouched()
  {
    _user.Language = "ru";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, "Asia/Tokyo"), CancellationToken.None);

    Assert.Equal("ru", _user.Language);
    Assert.Equal("Asia/Tokyo", _user.Timezone);
  }

  [Fact]
  public async Task OnlySubscriptionsSortProvided_LeavesOthersUntouched()
  {
    _user.Language = "ru";
    _user.Timezone = "America/New_York";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, SubscriptionsSort: "ticker"), CancellationToken.None);

    Assert.Equal("ticker", _user.SubscriptionsSort);
    Assert.Equal("ru", _user.Language);
    Assert.Equal("America/New_York", _user.Timezone);
  }

  [Fact]
  public async Task OnlyChartPreferencesProvided_StoresDocument_LeavesOthersUntouched()
  {
    _user.Language = "ru";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, ChartPreferences: new ChartPreferencesDto(OhlcTooltip: false)), CancellationToken.None);

    var stored = ChartPreferencesDto.Parse(_user.ChartPreferences);
    Assert.Equal(false, stored?.OhlcTooltip);
    Assert.Null(stored?.Magnet);
    Assert.Equal("ru", _user.Language);
    _users.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ChartPreferencesPatch_MergesFlagByFlag_KeepsStoredFlags()
  {
    _user.ChartPreferences = "{\"OhlcTooltip\":false,\"Magnet\":false}";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, ChartPreferences: new ChartPreferencesDto(Magnet: true)), CancellationToken.None);

    var stored = ChartPreferencesDto.Parse(_user.ChartPreferences);
    Assert.Equal(false, stored?.OhlcTooltip);
    Assert.Equal(true, stored?.Magnet);
  }

  [Fact]
  public async Task ChartPreferencesEmptyPatch_KeepsStoredDocument()
  {
    _user.ChartPreferences = "{\"OhlcTooltip\":false,\"Magnet\":true}";

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, ChartPreferences: new ChartPreferencesDto()), CancellationToken.None);

    Assert.Equal("{\"OhlcTooltip\":false,\"Magnet\":true}", _user.ChartPreferences);
  }

  [Fact]
  public async Task ChartPreferencesAllNullPatch_OnUnsetUser_StoresNull()
  {
    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, ChartPreferences: new ChartPreferencesDto()), CancellationToken.None);

    Assert.Null(_user.ChartPreferences);
  }

  [Fact]
  public void ChartPreferences_CamelCasePatchBinds_UnknownMemberIsRejected_StoredDocumentRoundTrips()
  {
    // The frontend PATCHes camelCase; MVC binds with the web defaults
    var bindingOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    var patch = JsonSerializer.Deserialize<ChartPreferencesDto>("{\"magnet\":true}", bindingOptions);
    Assert.Equal(true, patch?.Magnet);

    // A typo in a preference key must fail the request, not silently drop the setting
    Assert.ThrowsAny<JsonException>(() =>
      JsonSerializer.Deserialize<ChartPreferencesDto>("{\"magnett\":true}", bindingOptions));

    // The handler stores PascalCase default json; GetMe reads it back through Parse
    var stored = JsonSerializer.Serialize(new ChartPreferencesDto(OhlcTooltip: false, Magnet: true));
    var parsed = ChartPreferencesDto.Parse(stored);
    Assert.Equal(false, parsed?.OhlcTooltip);
    Assert.Equal(true, parsed?.Magnet);
    Assert.Null(ChartPreferencesDto.Parse("not json"));
  }

  [Fact]
  public async Task CompleteSubscriptionTourOnly_SetsTimestamp()
  {
    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, CompleteSubscriptionTour: true), CancellationToken.None);

    Assert.NotNull(_user.SubscriptionOnboardingCompletedAt);
    Assert.Null(_user.InitOnboardingCompletedAt);
    _users.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task CompleteSubscriptionTour_KeepsExistingTimestamp()
  {
    var first = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    _user.SubscriptionOnboardingCompletedAt = first;

    await _handler.Handle(
      new UpdateUserPreferencesCommand(_userId, null, null, CompleteSubscriptionTour: true), CancellationToken.None);

    Assert.Equal(first, _user.SubscriptionOnboardingCompletedAt);
  }

  [Fact]
  public async Task UnknownUser_ThrowsUserNotFound()
  {
    _users
      .Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    await Assert.ThrowsAsync<UserNotFoundException>(() =>
      _handler.Handle(new UpdateUserPreferencesCommand(Guid.NewGuid(), "ru", null), CancellationToken.None));
  }
}

public class UpdateUserPreferencesValidatorTests
{
  private readonly UpdateUserPreferencesCommandValidator _validator = new();

  [Theory]
  [InlineData(null, null)]
  [InlineData("zz", "Europe/Moscow")]
  [InlineData("ru", "Not/AZone")]
  [InlineData("en", "Europe/Moscow ")]
  public async Task Invalid_ProducesErrors(string? language, string? timezone)
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), language, timezone));

    Assert.False(result.IsValid);
  }

  [Theory]
  [InlineData("ru", "Europe/Moscow")]
  [InlineData("en", "America/New_York")]
  [InlineData("es", "Europe/Madrid")]
  [InlineData("it", "Europe/Rome")]
  [InlineData("de", "Europe/Berlin")]
  [InlineData("fr", "Europe/Paris")]
  [InlineData("pt-BR", "America/Sao_Paulo")]
  [InlineData("uk", "Europe/Kyiv")]
  [InlineData("be", "Europe/Minsk")]
  [InlineData("ru", null)]
  [InlineData(null, "Asia/Tokyo")]
  public async Task Valid_Passes(string? language, string? timezone)
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), language, timezone));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
  }

  [Fact]
  public async Task CompleteOnboardingOnly_Passes()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null, true));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
  }

  [Fact]
  public async Task SubscriptionsSortOnly_Passes()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null, SubscriptionsSort: "alertCount"));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
  }

  [Fact]
  public async Task UnsupportedSubscriptionsSort_ProducesError()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null, SubscriptionsSort: "byVibes"));

    Assert.False(result.IsValid);
  }

  [Fact]
  public async Task ChartPreferencesOnly_Passes()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null, ChartPreferences: new ChartPreferencesDto(Magnet: true)));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
  }

  [Fact]
  public async Task CompleteSubscriptionTourOnly_Passes()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null, CompleteSubscriptionTour: true));

    Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
  }

  [Fact]
  public async Task NothingProvided_ProducesError()
  {
    var result = await _validator.ValidateAsync(
      new UpdateUserPreferencesCommand(Guid.NewGuid(), null, null));

    Assert.False(result.IsValid);
  }
}
