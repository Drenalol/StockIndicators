using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Telegram;

namespace StockIndicators.Application.Account.GetTelegramChats;

public class GetTelegramChatsQueryHandler : IQueryHandler<GetTelegramChatsQuery, IReadOnlyList<TelegramChatDto>>
{
  private readonly ITelegramChatRepository _chatRepository;

  public GetTelegramChatsQueryHandler(ITelegramChatRepository chatRepository)
  {
    _chatRepository = chatRepository;
  }

  public async Task<IReadOnlyList<TelegramChatDto>> Handle(GetTelegramChatsQuery request, CancellationToken cancellationToken)
  {
    var chats = await _chatRepository.GetByUserIdAsync(request.UserId, cancellationToken);

    return [
      .. chats.Select(
        chat =>
          new TelegramChatDto(
            chat.Id.ToString(),
            chat.ChatId,
            chat.Username,
            chat.Created)),
    ];
  }
}
