using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments.Logos;

namespace StockIndicators.Application.Instruments.GetInstrumentLogo;

public sealed class GetInstrumentLogoQuery : IQuery<InstrumentLogoData?>
{
  public string Ticker { get; }
  public string? ExchangeName { get; }

  public GetInstrumentLogoQuery(string ticker, string? exchangeName = null)
  {
    Ticker = ticker;
    ExchangeName = exchangeName;
  }
}
