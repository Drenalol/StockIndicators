using MediatR;

namespace StockIndicators.Application.Configuration.Commands;

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult> where TCommand : ICommand<TResult>;

public interface IBackgroundCommandHandler<in TCommand> : INotificationHandler<TCommand> where TCommand : IBackgroundCommand;