using System.Net;
using System.Net.Sockets;

namespace StockIndicators.Internal;

/// <summary>
/// The TCP-rerouting handler for the dev TLS bridge (tools/tinkoff-tls-bridge): Tinkoff's
/// Russian Trusted CA chain breaks the OS TLS stack before any validation callback can run.
/// The TCP connection is rerouted to the bridge while the real host stays the TLS target
/// and the HTTP/2 authority (the upstream gateway routes on it); the bridge forwards over
/// its own verified connection, and the accept-any callback here only ever sees the
/// bridge's self-signed localhost cert. Shared by the DI wiring and the connection test so
/// both reach Tinkoff exactly the same way.
/// </summary>
public static class TinkoffTlsBridgeHandler
{
  public static SocketsHttpHandler Create(string bridgeAddress)
  {
    var bridge = new Uri(bridgeAddress);

    return new SocketsHttpHandler
    {
      ConnectCallback = async (context, cancellationToken) =>
      {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
          await socket.ConnectAsync(new DnsEndPoint(bridge.Host, bridge.Port), cancellationToken);
        }
        catch (SocketException e)
        {
          socket.Dispose();
          // The generic "connection refused" here always means the dev bridge is down.
          throw new InvalidOperationException($"Tinkoff TLS bridge is not listening on {bridge.Host}:{bridge.Port}. Start it with: go run ./tools/tinkoff-tls-bridge", e);
        }

        return new NetworkStream(socket, ownsSocket: true);
      },
      SslOptions =
      {
        RemoteCertificateValidationCallback = (_, _, _, _) => true
      }
    };
  }
}
