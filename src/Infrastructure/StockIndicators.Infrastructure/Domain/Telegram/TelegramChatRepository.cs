using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Telegram;

public class TelegramChatRepository : ITelegramChatRepository
{
  private readonly StockIndicatorsContext _stockIndicatorsContext;

  public TelegramChatRepository(StockIndicatorsContext stockIndicatorsContext) => _stockIndicatorsContext = stockIndicatorsContext;

  public async Task<TelegramChat?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.TelegramChats.FirstOrDefaultAsync(chat => chat.ChatId == chatId, cancellationToken);

  public async Task<List<TelegramChat>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    => await _stockIndicatorsContext.TelegramChats
      .Where(chat => chat.UserId == userId)
      .OrderBy(chat => chat.Created)
      .ToListAsync(cancellationToken);

  public async Task<TelegramChat> AddAsync(TelegramChat chat, CancellationToken cancellationToken = default)
  {
    var entry = await _stockIndicatorsContext.TelegramChats.AddAsync(chat, cancellationToken);
    return entry.Entity;
  }

  public Task UpdateAsync(TelegramChat chat, CancellationToken cancellationToken = default)
  {
    _stockIndicatorsContext.TelegramChats.Update(chat);
    return _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
  }

  public async Task DeleteAsync(TelegramChat chat, CancellationToken cancellationToken = default)
  {
    _stockIndicatorsContext.TelegramChats.Remove(chat);
    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
  }

  public Task<int> CommitAsync(CancellationToken cancellationToken = default) => _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
}
