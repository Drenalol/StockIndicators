namespace StockIndicators.Application.Configuration.Email;

public interface IMailSender
{
  Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken = default);
}
