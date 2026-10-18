using MediatR;

namespace StockIndicators.Application.Configuration.Commands;

public interface ICommand : IRequest;

public interface ICommand<out TResult> : IRequest<TResult>;

public interface IBackgroundCommand : INotification;