namespace StockIndicators.Domain.Models.Telegram;

public interface ITelegramChatRepository
{
  Task<TelegramChat?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken = default);

  Task<List<TelegramChat>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

  Task<TelegramChat> AddAsync(TelegramChat chat, CancellationToken cancellationToken = default);

  Task UpdateAsync(TelegramChat chat, CancellationToken cancellationToken = default);

  Task DeleteAsync(TelegramChat chat, CancellationToken cancellationToken = default);

  Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
