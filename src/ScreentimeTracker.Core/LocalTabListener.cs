using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ScreentimeTracker.Core;

/// <summary>
/// The localhost-only HTTP endpoint the browser extension posts its active tab to (ADR-0002). It
/// accepts <c>POST /tab</c> with a JSON body <c>{"browser": "edge" | "brave", "url": string | null}</c>
/// and feeds it to the <see cref="ActiveTabFeed"/>.
/// </summary>
/// <remarks>
/// This is a deliberately tiny HTTP/1.1 server on a <see cref="TcpListener"/> bound to 127.0.0.1.
/// <see cref="HttpListener"/> goes through http.sys, which needs admin rights to bind 127.0.0.1 and
/// otherwise only filters on the Host header rather than binding to loopback.
/// Requests must carry an extension Origin, so a web page open in the browser can't post fake reports.
/// </remarks>
public sealed class LocalTabListener : IDisposable
{
    /// <summary>The port the extension posts to. Must match <c>extension/background.js</c>.</summary>
    public const int DefaultPort = 47613;

    private const int MaxHeaderBytes = 8 * 1024;
    private const int MaxBodyBytes = 16 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    private readonly ActiveTabFeed _feed;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _acceptLoop;

    private LocalTabListener(ActiveTabFeed feed, int port)
    {
        _feed = feed;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>Starts listening on 127.0.0.1. Port 0 picks a free port; see <see cref="Endpoint"/>.</summary>
    /// <exception cref="SocketException">The port is already in use.</exception>
    public static LocalTabListener Start(ActiveTabFeed feed, int port = DefaultPort) => new(feed, port);

    public IPEndPoint Endpoint => (IPEndPoint)_listener.LocalEndpoint;

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();
        try { _acceptLoop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            _ = HandleAsync(client);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using var _ = client;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            timeout.CancelAfter(RequestTimeout);
            var stream = client.GetStream();
            var status = await ServeAsync(stream, timeout.Token);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, timeout.Token);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The extension reports again on its next event or heartbeat, so a dropped request costs nothing.
        }
    }

    private async Task<HttpStatusCode> ServeAsync(NetworkStream stream, CancellationToken cancel)
    {
        var (head, bodyStart) = await ReadHeadAsync(stream, cancel);
        if (head is null) return HttpStatusCode.BadRequest;

        var lines = head.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length != 3) return HttpStatusCode.BadRequest;
        if (requestLine[1] != "/tab") return HttpStatusCode.NotFound;
        if (requestLine[0] != "POST") return HttpStatusCode.MethodNotAllowed;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        if (!headers.TryGetValue("Origin", out var origin)
            || !origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.Forbidden;
        }

        if (!headers.TryGetValue("Content-Length", out var lengthText)
            || !int.TryParse(lengthText, out var length) || length < 0 || length > MaxBodyBytes
            || bodyStart.Length > length)
        {
            return HttpStatusCode.BadRequest;
        }

        var body = new byte[length];
        bodyStart.CopyTo(body, 0);
        await stream.ReadExactlyAsync(body.AsMemory(bodyStart.Length), cancel);

        if (!TryParseReport(body, out var browser, out var url)) return HttpStatusCode.BadRequest;
        _feed.Report(browser, url);
        return HttpStatusCode.NoContent;
    }

    /// <summary>Reads up to the blank line that ends the headers. Returns them and any body bytes read past them.</summary>
    private static async Task<(string? Head, byte[] BodyStart)> ReadHeadAsync(NetworkStream stream, CancellationToken cancel)
    {
        var buffer = new byte[MaxHeaderBytes];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancel);
            if (read == 0) return (null, []);
            filled += read;

            var end = buffer.AsSpan(0, filled).IndexOf(HeaderEnd);
            if (end >= 0)
            {
                return (Encoding.ASCII.GetString(buffer, 0, end), buffer[(end + HeaderEnd.Length)..filled]);
            }
        }
        return (null, []);
    }

    private static bool TryParseReport(byte[] body, out SupportedBrowser browser, out string? url)
    {
        browser = default;
        url = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (!root.TryGetProperty("browser", out var name) || name.ValueKind != JsonValueKind.String) return false;
            switch (name.GetString())
            {
                case "edge": browser = SupportedBrowser.Edge; break;
                case "brave": browser = SupportedBrowser.Brave; break;
                default: return false;
            }

            if (!root.TryGetProperty("url", out var value)) return false;
            switch (value.ValueKind)
            {
                case JsonValueKind.Null: return true;
                case JsonValueKind.String: url = value.GetString(); return true;
                default: return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
