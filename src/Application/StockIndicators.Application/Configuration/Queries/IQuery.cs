using MediatR;

namespace StockIndicators.Application.Configuration.Queries;

public interface IQuery<out TResult> : IRequest<TResult>;