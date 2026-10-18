using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Ichimoku;

public class IchimokuIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<IchimokuIndicatorCommand, IchimokuIndicatorResult>
{
  public IchimokuIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<IchimokuIndicatorResult> Handle(IchimokuIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<IchimokuIndicatorTemplate>(request);

    var ichimokuResult = request.Candles
      .ToIchimoku(template.TenkanPeriod, template.KijunPeriod, template.SenkouBPeriod)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new IchimokuIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var price = (double)request.Candles[^1].Close;
    var prevTenkan = indicatorResult.TenkanSen;
    var prevKijun = indicatorResult.KijunSen;

    indicatorResult.TenkanSen = (double)ichimokuResult.TenkanSen.GetValueOrDefault();
    indicatorResult.KijunSen = (double)ichimokuResult.KijunSen.GetValueOrDefault();
    indicatorResult.SenkouSpanA = (double)ichimokuResult.SenkouSpanA.GetValueOrDefault();
    indicatorResult.SenkouSpanB = (double)ichimokuResult.SenkouSpanB.GetValueOrDefault();
    indicatorResult.ChikouSpan = (double)ichimokuResult.ChikouSpan.GetValueOrDefault();

    var top = Math.Max(indicatorResult.SenkouSpanA, indicatorResult.SenkouSpanB);
    var bottom = Math.Min(indicatorResult.SenkouSpanA, indicatorResult.SenkouSpanB);

    var position = GetPosition(price, top, bottom);
    indicatorResult.Position = position;

    var triggered = false;

    if (HasPreviousState())
    {
      var prevCrossUp = prevTenkan <= prevKijun && indicatorResult.TenkanSen > indicatorResult.KijunSen;
      var prevCrossDown = prevTenkan >= prevKijun && indicatorResult.TenkanSen < indicatorResult.KijunSen;

      if (prevCrossUp)
      {
        indicatorResult.Position = IchimokuPosition.TKCrossBullish;
        triggered = true;
      }
      else if (prevCrossDown)
      {
        indicatorResult.Position = IchimokuPosition.TKCrossBearish;
        triggered = true;
      }
    }

    return Set(indicatorResult, triggered);

    static IchimokuPosition GetPosition(double price, double top, double bottom)
    {
      if (price > top)
        return IchimokuPosition.AboveCloud;
      if (price < bottom)
        return IchimokuPosition.BelowCloud;
      return IchimokuPosition.InsideCloud;
    }

    bool HasPreviousState() => prevTenkan != 0 || prevKijun != 0;
  }
}
