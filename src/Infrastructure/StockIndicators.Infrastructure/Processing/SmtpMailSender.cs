using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using Serilog;
using StockIndicators.Application.Configuration.Email;

namespace StockIndicators.Infrastructure.Processing;

public class SmtpMailSender : IMailSender
{
  private readonly SmtpOptions _options;
  private readonly ILogger _logger;

  public SmtpMailSender(IOptions<SmtpOptions> options, ILogger logger)
  {
    _options = options.Value;
    _logger = logger;
  }

  public async Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(_options.Host))
    {
      _logger.Information("SMTP is not configured, mail to {To} dropped. Subject: {Subject} | {Body}", to, content.Subject, content.Text);
      return;
    }

    var message = new MimeMessage { Subject = content.Subject };

    if (string.IsNullOrWhiteSpace(content.Text))
      message.Body = new TextPart(TextFormat.Html) { Text = content.Html };
    else
    {
      // multipart/alternative: mail clients pick the best part they can render,
      // the plain text part keeps the mail readable everywhere
      var alternative = new Multipart("alternative")
      {
        new TextPart(TextFormat.Plain) { Text = content.Text },
        new TextPart(TextFormat.Html) { Text = content.Html }
      };
      message.Body = alternative;
    }

    message.From.Add(InternetAddress.Parse(string.IsNullOrWhiteSpace(_options.From) ? to : _options.From));
    message.To.Add(InternetAddress.Parse(to));

    if (!string.IsNullOrWhiteSpace(_options.ReplyTo))
      message.ReplyTo.Add(InternetAddress.Parse(_options.ReplyTo));

    using var client = new SmtpClient();

    try
    {
      var socketOptions = _options.UseSsl ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None;
      await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

      if (!string.IsNullOrEmpty(_options.Username))
        await client.AuthenticateAsync(_options.Username, _options.Password ?? "", cancellationToken);

      await client.SendAsync(message, cancellationToken);
    }
    finally
    {
      if (client.IsConnected)
        await client.DisconnectAsync(true, cancellationToken);
    }

    _logger.Information("Sent mail {Subject} to {To}", content.Subject, to);
  }
}
