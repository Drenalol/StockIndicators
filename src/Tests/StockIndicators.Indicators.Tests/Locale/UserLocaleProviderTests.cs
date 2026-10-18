using Moq;
using Serilog.Core;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Users;
using Xunit;

namespace StockIndicators.Indicators.Tests.Locale;

public class UserLocaleProviderTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Mock<IUserRepository> _userRepository = new();

  private UserLocaleProvider CreateProvider() =>
    new(_userRepository.Object, Logger.None);

  private void SetupUser(User? user) =>
    _userRepository
      .Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

  [Fact]
  public async Task UserNotFound_ReturnsDefault()
  {
    SetupUser(null);

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.Default, locale);
  }

  [Fact]
  public async Task MissingPreferences_ReturnsDefault()
  {
    SetupUser(new User { Id = _userId });

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.Default, locale);
  }

  [Fact]
  public async Task SupportedLanguageAndTimezone_AreReturned()
  {
    SetupUser(new User
    {
      Id = _userId,
      Language = UserLocale.English,
      Timezone = "Europe/Moscow"
    });

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.English, locale.Language);
    Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"), locale.Timezone);
  }

  [Fact]
  public async Task SupportedSpanishLanguage_AreReturned()
  {
    SetupUser(new User
    {
      Id = _userId,
      Language = UserLocale.Spanish,
      Timezone = "Europe/Madrid"
    });

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.Spanish, locale.Language);
    Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"), locale.Timezone);
  }

  [Fact]
  public async Task UnsupportedLanguage_FallsBackToDefault()
  {
    SetupUser(new User { Id = _userId, Language = "zz" });

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.DefaultLanguage, locale.Language);
  }

  [Fact]
  public async Task InvalidTimezone_FallsBackToUtc()
  {
    SetupUser(new User { Id = _userId, Timezone = "Mars/Olympus_Mons" });

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(TimeZoneInfo.Utc, locale.Timezone);
  }

  [Fact]
  public async Task RepositoryFailure_ReturnsDefault()
  {
    _userRepository
      .Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("db down"));

    var locale = await CreateProvider().GetAsync(_userId);

    Assert.Equal(UserLocale.Default, locale);
  }

  [Fact]
  public async Task ResultsAreCachedWithinTtl()
  {
    SetupUser(new User { Id = _userId, Language = UserLocale.English });

    var provider = CreateProvider();

    await provider.GetAsync(_userId);
    await provider.GetAsync(_userId);

    _userRepository.Verify(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
  }
}
