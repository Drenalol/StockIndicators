using System.Reflection;
using StockIndicators.Internal;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The build stamp the About page reports: the release workflow passes the tag and the commit as
/// build properties, so the parsing of the resulting informational version is a release contract.
/// </summary>
public class AppVersionTests
{
  [Theory]
  [InlineData("1.0.0+0123456789abcdef0123456789abcdef0123", "1.0.0", "0123456789ab")]
  [InlineData("1.0.0+abc", "1.0.0", "abc")]
  [InlineData("1.0.0-dev+deadbeef", "1.0.0-dev", "deadbeef")]
  [InlineData("1.0.0+sha:abcdef1234567890", "1.0.0", "abcdef123456")]
  [InlineData("1.0.0+ abcdef ", "1.0.0", "abcdef")]
  [InlineData("1.0.0", "1.0.0", null)]
  [InlineData("1.0.0-dev", "1.0.0-dev", null)]
  [InlineData("1.0.0+", "1.0.0", null)]
  [InlineData("+abcdef", AppVersion.Development, "abcdef")]
  [InlineData("", AppVersion.Development, null)]
  [InlineData("   ", AppVersion.Development, null)]
  [InlineData(null, AppVersion.Development, null)]
  public void Parse_SplitsTheVersionFromTheCommit(
    string? informational, string expectedVersion, string? expectedCommit)
  {
    var (version, commit) = AppVersion.Parse(informational);

    Assert.Equal(expectedVersion, version);
    Assert.Equal(expectedCommit, commit);
  }

  [Fact]
  public void Parse_NeverReportsAVersionCarryingMetadata()
  {
    // a "+" in the version would be pasted into the About page as part of the release name
    var (version, _) = AppVersion.Parse("2.1.0+2b85fb5");

    Assert.DoesNotContain("+", version);
  }

  [Fact]
  public void Current_ReportsTheStampOfTheRunningAssembly()
  {
    var informational = typeof(AppVersion).Assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
      .InformationalVersion;

    var (version, commit) = AppVersion.Parse(informational);

    Assert.Equal(version, AppVersion.Version);
    Assert.Equal(commit, AppVersion.Commit);
  }

  [Fact]
  public void Current_ReportsAVersionNumberAndAShortCommit()
  {
    // Directory.Build.props declares the version an unstamped build carries; the release workflow
    // replaces it with the tag. Either way the About page gets "major.minor.patch" and, when the
    // build said, a commit short enough to read.
    Assert.Matches(@"^\d+\.\d+\.\d+", AppVersion.Version);
    Assert.True(AppVersion.Commit is null || AppVersion.Commit.Length <= AppVersion.CommitLength);
  }
}
