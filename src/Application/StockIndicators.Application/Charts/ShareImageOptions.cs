namespace StockIndicators.Application.Charts;

/// <summary>
/// Configuration of the anonymous link-preview pictures (<c>og:image</c> of the public
/// pages). A messenger crawler has no session, so these pictures are served by an
/// anonymous endpoint; what they show is therefore fixed by the deployment, never by the
/// request - a query parameter that picked the text or the parameters of a picture would
/// hand anyone a way to mint arbitrary text into a public PNG that Cloudflare then keeps
/// under our own domain for a week.
/// </summary>
public sealed class ShareImageOptions
{
  /// <summary>
  /// The indicator whose example chart the landing page and the indicator catalog are
  /// pictured with. The chart is the anchored example fixture, so its parameters are the
  /// indicator's sample defaults and the trigger marker is a candle the production handler
  /// really fires on (a guard test asserts the configured indicator has a fixture at all).
  /// </summary>
  public string LandingIndicator { get; set; } = "EMA";
}
