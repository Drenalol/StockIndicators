using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Controllers;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The degradation path of the public preview pictures: with no chart renderer configured (which
/// is how a public-site instance runs) the endpoint sends the crawler to the static brand card on
/// this origin. A 404 here would leave every shared link of the landing and the catalog without a
/// picture - the one thing this endpoint exists to prevent.
/// </summary>
public class ShareControllerBrandCardTests
{
  private static ShareController CreateController(string publicBaseUrl = "")
  {
    var mediator = new Mock<IMediator>();

    mediator
      .Setup(m => m.Send(It.IsAny<IRequest<byte[]?>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null);

    return new ShareController(mediator.Object, Options.Create(new AlertOptions { PublicBaseUrl = publicBaseUrl }))
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          Request = { Scheme = "https", Host = new HostString("stockindicators.app") }
        }
      }
    };
  }

  [Fact]
  public async Task Without_a_renderer_the_card_is_served_from_the_request_origin()
  {
    var result = await CreateController().GetLandingImageAsync("en", CancellationToken.None);

    Assert.Equal("https://stockindicators.app/og/brand.png", Assert.IsType<RedirectResult>(result).Url);
  }

  [Fact]
  public async Task An_indicator_page_degrades_the_same_way()
  {
    var result = await CreateController()
      .GetIndicatorImageAsync(StockIndicators.Domain.Models.Indicators.Indicator.RSI, "ru", CancellationToken.None);

    Assert.Equal("https://stockindicators.app/og/brand.png", Assert.IsType<RedirectResult>(result).Url);
  }

  [Fact]
  public async Task A_configured_public_base_url_still_wins()
  {
    var result = await CreateController("https://example.test/").GetLandingImageAsync("en", CancellationToken.None);

    Assert.Equal("https://example.test/og/brand.png", Assert.IsType<RedirectResult>(result).Url);
  }
}
