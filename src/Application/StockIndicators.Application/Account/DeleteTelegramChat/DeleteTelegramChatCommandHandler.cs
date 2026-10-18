using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Telegram;

namespace StockIndicators.Application.Account.DeleteTelegramChat;

public class DeleteTelegramChatCommandHandler : ICommandHandler<DeleteTelegramChatCommand, Unit>
{
  private readonly ITelegramChatRepository _chatRepository;

  public DeleteTelegramChatCommandHandler(ITelegramChatRepository chatRepository)
  {
    _chatRepository = chatRepository;
  }

  public async Task<Unit> Handle(DeleteTelegramChatCommand request, CancellationToken cancellationToken)
  {
    var chats = await _chatRepository.GetByUserIdAsync(request.UserId, cancellationToken);
    var chat = chats.FirstOrDefault(c => c.Id == request.ChatId)
      ?? throw new TelegramChatNotFoundException();

    await _chatRepository.DeleteAsync(chat, cancellationToken);
    await _chatRepository.CommitAsync(cancellationToken);

    return Unit.Value;
  }
}
