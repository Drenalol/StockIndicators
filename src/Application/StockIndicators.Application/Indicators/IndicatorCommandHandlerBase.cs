using Serilog;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators;

public abstract class IndicatorCommandHandlerBase<TCommand, TResult> : ICommandHandler<TCommand, TResult> where TCommand : IndicatorCommandBase<TResult> where TResult : IndicatorResult, new()
{
  protected readonly ILogger Logger;

  protected IndicatorCommandHandlerBase(ILogger logger) => Logger = logger;

  public abstract Task<TResult> Handle(TCommand request, CancellationToken cancellationToken);

  protected TIndicatorTemplate GetTemplate<TIndicatorTemplate>(TCommand request) where TIndicatorTemplate : IIndicatorTemplate => (TIndicatorTemplate)request.Template;

  protected TResult True() => IndicatorResultFactory<TResult>.True;

  protected TResult True(TResult indicatorResult) => Set(indicatorResult);

  protected TResult False() => IndicatorResultFactory<TResult>.False;

  protected TResult False(TResult indicatorResult) => Set(indicatorResult, false);

  protected TResult Set(TResult indicatorResult, bool triggered = true) => (TResult)indicatorResult.Set(triggered);
}