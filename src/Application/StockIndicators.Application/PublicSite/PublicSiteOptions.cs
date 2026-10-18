namespace StockIndicators.Application.PublicSite;

/// <summary>
/// How this instance faces the network. The image carries the whole product, but a public
/// deployment publishes the marketing surface only: the landing page, the indicator catalog,
/// the legal pages and the link-preview pictures. Everything that touches an account - the
/// dashboard, sign-in, providers, notifications, the alert scheduler - belongs to a self-hosted
/// installation, where this flag stays off.
/// </summary>
public sealed class PublicSiteOptions
{
  public const string SectionName = "PublicSite";

  /// <summary>
  /// Serve the public site and nothing else. The application part of the process is then never
  /// wired: there is no session, no database, no provider and no scheduler to reach, and the
  /// request gate answers everything outside the public surface with a 404. Off by default -
  /// a self-hosted instance is the whole product.
  /// </summary>
  public bool LandingOnly { get; init; }
}
