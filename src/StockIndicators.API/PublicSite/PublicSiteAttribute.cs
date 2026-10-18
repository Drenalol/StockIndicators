namespace StockIndicators.PublicSite;

/// <summary>
/// Marks a controller as part of the public site: the landing page, the indicator catalog, the
/// legal pages and the link-preview pictures are all a public deployment publishes - the rest
/// of the product is what a self-hosted installation runs.
/// <para>
/// The marker is what the request gate of a public-site instance lets through, so an endpoint
/// nobody marked stays unreachable there: forgetting the attribute hides an endpoint instead of
/// exposing one. A marked controller has to stay anonymous - it answers strangers.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublicSiteAttribute : Attribute;
