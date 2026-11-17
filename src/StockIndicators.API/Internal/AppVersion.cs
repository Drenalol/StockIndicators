using System.Reflection;

namespace StockIndicators.Internal;

/// <summary>
/// What this process was built from. A published image carries the release tag as its version and
/// the commit that tag pointed at as its build id (the <c>APP_VERSION</c>/<c>GIT_SHA</c> build args
/// of the Dockerfile, which the release workflow passes); a checkout built from source keeps the
/// pre-release version of <c>Directory.Build.props</c> and usually has no commit at all. Both parts
/// reach the web through <c>GET /api/config</c> and are shown under Settings -&gt; About, so an
/// installation can answer "which build is this" without anyone reading container logs.
/// </summary>
public static class AppVersion
{
  /// <summary>What an unstamped assembly says about itself.</summary>
  public const string Development = "1.0.0-dev";

  /// <summary>How much of a commit id is worth showing; a full one is noise in a settings row.</summary>
  public const int CommitLength = 12;

  private static readonly (string Version, string? Commit) Current = Parse(ReadInformational());

  /// <summary>The semantic part: "1.0.0" for a release, <see cref="Development"/> for a source build.</summary>
  public static string Version => Current.Version;

  /// <summary>Short commit id, or null when the build did not say which commit it was.</summary>
  public static string? Commit => Current.Commit;

  /// <summary>
  /// Splits an informational version ("1.2.3+0123456789abcdef0123456789abcdef0123") into its two
  /// parts. Anything without metadata ("1.2.3", "1.0.0-dev", nothing at all) just has no commit,
  /// and the "sha:" prefix some build tools emit is not part of the id.
  /// </summary>
  public static (string Version, string? Commit) Parse(string? informationalVersion)
  {
    var value = informationalVersion?.Trim();
    if (string.IsNullOrEmpty(value)) return (Development, null);

    var separator = value.IndexOf('+');
    if (separator < 0) return (value, null);

    var version = value[..separator];
    var commit = value[(separator + 1)..];
    if (commit.StartsWith("sha:", StringComparison.OrdinalIgnoreCase)) commit = commit["sha:".Length..];

    commit = commit.Trim();
    return (
      version.Length == 0 ? Development : version,
      commit.Length == 0 ? null : commit[..Math.Min(commit.Length, CommitLength)]);
  }

  private static string? ReadInformational() =>
    typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
