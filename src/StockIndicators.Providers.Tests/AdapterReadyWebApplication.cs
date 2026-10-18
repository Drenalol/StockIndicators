using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using StockIndicators.Providers.Tinkoff;

namespace StockIndicators.Providers.Tests;

public class AdapterReadyWebApplication : WebApplicationFactory<Program>
{
  public static readonly Lazy<AdapterReadyWebApplication> Instance = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    builder.UseConfiguration(configuration);

    var testEnv = Environment.GetEnvironmentVariable("TEST_ENV");

    if (testEnv is { } env)
      builder.UseEnvironment(env);
    else
      builder.UseEnvironment("Development");
    
    base.ConfigureWebHost(builder);

    builder.ConfigureServices(
      services =>
      {
        services.Remove<TinkoffProviderConsumerProducer>();
      }
    );
  }
}