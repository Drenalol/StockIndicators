using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Candles;

public class UpdateCacheCommandHandler : IBackgroundCommandHandler<UpdateCacheCommand>
{
  private readonly ICandleRepository _candleRepository;

  public UpdateCacheCommandHandler(ICandleRepository candleRepository) => _candleRepository = candleRepository;

  public Task Handle(UpdateCacheCommand request, CancellationToken cancellationToken) => _candleRepository.AddRangeAsync(request.Candles, cancellationToken);
}