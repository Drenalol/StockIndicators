using StockIndicators.Application.Notifications.Telegram;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// Which instance addresses a link button may point at. Telegram checks button URLs on its
/// own servers (a loopback address is refused with "Wrong HTTP URL") and a LAN address is
/// unreachable for any device outside that network, so both have to be caught in the
/// settings page instead of in a failed delivery.
/// </summary>
public class PublicWebAddressTests
{
  [Theory]
  // the address the whole product is meant to run behind
  [InlineData("https://indicators.example")]
  [InlineData("https://example.com")]
  [InlineData("https://indicators.example.co.uk/")]
  [InlineData("http://example.com:8080/ru/dashboard/settings")]
  [InlineData("HTTPS://Example.COM/Dashboard/Settings")]
  [InlineData("https://[2606:4700:4700::1111]")]
  public void AnAddressReachableFromOutsidePasses(string value) =>
    Assert.True(PublicWebAddress.IsReachable(value));

  [Theory]
  // the reported failure: the dev instance URL, refused by Telegram itself
  [InlineData("https://localhost:5051/dashboard/settings")]
  [InlineData("http://localhost:8080")]
  [InlineData("localhost:5051")]
  // loopback and the private ranges, spelled as an address
  [InlineData("http://127.0.0.1:8080")]
  [InlineData("http://10.0.0.5:8080")]
  [InlineData("http://192.168.1.20:5051")]
  [InlineData("http://172.16.0.1")]
  [InlineData("http://169.254.1.1")]
  [InlineData("http://0.0.0.0:5051")]
  // a VPN mesh address works on the operator's own devices and for nobody else
  [InlineData("http://100.64.0.5:5051")]
  [InlineData("https://[::1]:5051")]
  [InlineData("https://[fe80::1]")]
  [InlineData("https://[fd12:3456::1]")]
  // names that only resolve at home
  [InlineData("http://nas:8080")]
  [InlineData("http://nas.lan:8080")]
  [InlineData("http://macbook.local")]
  [InlineData("http://router.home.arpa")]
  // not an address at all
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("indicators.example")]
  [InlineData("/dashboard/settings")]
  [InlineData("ftp://example.com")]
  public void AnAddressTelegramOrThePhoneCannotReachFails(string value) =>
    Assert.False(PublicWebAddress.IsReachable(value));

  [Fact]
  public void TheDefaultInstanceUrlIsNotAPublicAddress() =>
    // the shipped default is a dev address: nothing may treat it as if buttons would work
    Assert.False(new TelegramBotOptions().InstanceUrlIsPublic);

  [Fact]
  public void APublicInstanceUrlMarksTheBotAsAbleToSendLinks()
  {
    var options = new TelegramBotOptions { InstanceUrl = "https://indicators.example/" };

    Assert.True(options.InstanceUrlIsPublic);
    Assert.Equal("https://indicators.example/dashboard/settings", options.InstanceUrlFor("dashboard/settings"));
  }

  [Fact]
  public void LinkButtonsExistOnlyWhereTheyWereAskedForAndCanActuallyBeOpened()
  {
    // the toggle is the operator's call, the address only decides whether it can be honored
    Assert.False(new TelegramBotOptions { InstanceUrl = "https://indicators.example" }.CanSendWebLinks);
    Assert.False(new TelegramBotOptions { InstanceUrl = "http://nas.lan:8080", IncludeWebLinks = true }.CanSendWebLinks);
    Assert.True(new TelegramBotOptions { InstanceUrl = "https://indicators.example", IncludeWebLinks = true }.CanSendWebLinks);
  }
}
