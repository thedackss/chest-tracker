using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChestTracker;

/// <summary>
/// The overlay for OBS (Browser Source): a tiny HTTP server on 127.0.0.1 only. GET /overlay is the page, which polls
/// GET /overlay.json every second. Requests whose Host isn't this PC are refused (no DNS rebinding from web pages).
/// </summary>
public sealed class ObsServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private volatile string _json = "{}";

    private ObsServer(TcpListener listener)
    {
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoop);
    }

    public int Port { get; }
    public string Url => $"http://localhost:{Port}/overlay";

    /// <summary>Starts on <paramref name="port"/>, or one of the next ports if it is taken. Null if none is free.</summary>
    public static ObsServer? Start(int port)
    {
        for (var p = port; p < port + 10; p++)
        {
            var listener = new TcpListener(IPAddress.Loopback, p);
            try
            {
                listener.Start();
                return new ObsServer(listener);
            }
            catch (SocketException)
            {
                listener.Stop();
            }
        }
        return null;
    }

    /// <summary>What the page shows (from the UI thread).</summary>
    public void Update(string json) => _json = json;

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch
            {
                return; // stopped
            }
            _ = Task.Run(() => Serve(client));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadHead(stream, timeout.Token);
                var lines = request.Split("\r\n");
                var parts = lines[0].Split(' ');
                var host = lines.Skip(1).FirstOrDefault(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))?[5..].Trim() ?? "";
                var hostName = host.Split(':')[0];
                var (status, type, body) =
                    parts.Length < 2 || parts[0] != "GET" ? (405, "text/plain", "GET only")
                    : hostName is not ("localhost" or "127.0.0.1") ? (403, "text/plain", "This PC only")
                    : parts[1].Split('?')[0] switch
                    {
                        "/overlay" or "/" => (200, "text/html; charset=utf-8", ObsPage.Html),
                        "/overlay.json" => (200, "application/json; charset=utf-8", _json),
                        _ => (404, "text/plain", "Not found"),
                    };
                var bytes = Encoding.UTF8.GetBytes(body);
                var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\nContent-Type: {type}\r\nContent-Length: {bytes.Length}\r\n"
                    + "Cache-Control: no-store\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), timeout.Token);
                await stream.WriteAsync(bytes, timeout.Token);
            }
            catch
            {
                // Client went away or sent garbage: nothing to do.
            }
        }
    }

    /// <summary>Request line and headers (up to 8 KB).</summary>
    private static async Task<string> ReadHead(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[8192];
        var length = 0;
        while (length < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(length), token);
            if (n == 0) break;
            length += n;
            if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n")) break;
        }
        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
