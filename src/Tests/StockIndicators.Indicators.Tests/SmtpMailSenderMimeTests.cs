using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Configuration.Email;
using StockIndicators.Infrastructure.Processing;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class SmtpMailSenderMimeTests
{
  [Fact]
  public async Task SendAsync_DeliversMultipartAlternative_WithTextAndHtmlParts()
  {
    var received = await ServeOneSessionAsync(async port =>
    {
      var sender = new SmtpMailSender(
        Options.Create(new SmtpOptions { Host = "127.0.0.1", Port = port, From = "sender@example.com" }),
        Log.Logger);

      await sender.SendAsync("user@example.com", new EmailContent("Login code", "<p>hello 123456</p>", "hello 123456"));
    });

    Assert.Contains("From: sender@example.com", received);
    Assert.Contains("To: user@example.com", received);
    Assert.Contains("Subject: Login code", received);
    Assert.Contains(@"multipart/alternative", received);
    Assert.Contains("Content-Type: text/plain", received);
    Assert.Contains("Content-Type: text/html", received);
    Assert.Contains("hello 123456", received);

    // the plain part must come before the html part (RFC 2046: best alternative last)
    Assert.True(received.IndexOf("Content-Type: text/plain", StringComparison.Ordinal)
      < received.IndexOf("Content-Type: text/html", StringComparison.Ordinal));
  }

  private static async Task<string> ServeOneSessionAsync(Func<int, Task> client)
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;

    var server = Task.Run(async () =>
    {
      using var socket = await listener.AcceptTcpClientAsync();
      using var stream = socket.GetStream();
      using var reader = new StreamReader(stream, Encoding.UTF8);
      using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n" };

      await writer.WriteLineAsync("220 test ESMTP");
      await writer.FlushAsync();

      var data = new StringBuilder();

      while (await reader.ReadLineAsync() is { } line)
      {
        if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
        {
          await writer.WriteLineAsync("354 send data");
          await writer.FlushAsync();

          string? bodyLine;
          while ((bodyLine = await reader.ReadLineAsync()) != null && bodyLine != ".")
            data.AppendLine(bodyLine);

          await writer.WriteLineAsync("250 ok");
          await writer.FlushAsync();
        }
        else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
        {
          await writer.WriteLineAsync("221 bye");
          await writer.FlushAsync();
          break;
        }
        else
        {
          await writer.WriteLineAsync("250 test");
          await writer.FlushAsync();
        }
      }

      return data.ToString();
    });

    await client(port);

    var timeout = Task.WhenAny(server, Task.Delay(TimeSpan.FromSeconds(10)));
    Assert.Same(server, await timeout);
    listener.Stop();

    return await server;
  }
}
