using System.Net;
using System.Net.Sockets;
using System.Text;
using ScreentimeTracker.Core;
using ScreentimeTracker.Core.Tests.Harness;

namespace ScreentimeTracker.Core.Tests;

public sealed class LocalTabListenerTests : IDisposable
{
    private readonly FakeClock _clock = new(TrackerHarness.Morning);
    private readonly ActiveTabFeed _feed;
    private readonly LocalTabListener _listener;
    private readonly HttpClient _http = new();

    public LocalTabListenerTests()
    {
        _feed = new ActiveTabFeed(_clock);
        _listener = LocalTabListener.Start(_feed, port: 0);
    }

    public void Dispose()
    {
        _http.Dispose();
        _listener.Dispose();
    }

    [Fact]
    public async Task A_URL_the_extension_posts_reaches_the_active_tab_feed()
    {
        var response = await PostAsync("""{"browser":"brave","url":"https://www.youtube.com/watch?v=1"}""");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new TabReport("https://www.youtube.com/watch?v=1", _clock.Now), _feed.LatestFrom(SupportedBrowser.Brave));
        Assert.Null(_feed.LatestFrom(SupportedBrowser.Edge));
    }

    [Fact]
    public async Task A_posted_report_of_no_Website_reaches_the_active_tab_feed()
    {
        await PostAsync("""{"browser":"edge","url":"https://example.com/"}""");
        _clock.Advance(TimeSpan.FromSeconds(10));
        await PostAsync("""{"browser":"edge","url":null}""");

        Assert.Equal(new TabReport(null, _clock.Now), _feed.LatestFrom(SupportedBrowser.Edge));
    }

    [Fact]
    public void The_listener_binds_to_loopback_only()
    {
        Assert.Equal(IPAddress.Loopback, _listener.Endpoint.Address);

        foreach (var address in NonLoopbackAddresses())
        {
            using var client = new TcpClient(address.AddressFamily);
            Assert.ThrowsAny<SocketException>(() => client.Connect(address, _listener.Endpoint.Port));
        }
    }

    [Fact]
    public async Task A_request_from_a_web_page_is_refused()
    {
        var response = await PostAsync("""{"browser":"brave","url":"https://evil.example/"}""", origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(_feed.LatestFrom(SupportedBrowser.Brave));
    }

    [Theory]
    [InlineData("""{"browser":"firefox","url":"https://example.com/"}""")]
    [InlineData("""{"url":"https://example.com/"}""")]
    [InlineData("""{"browser":"brave","url":42}""")]
    [InlineData("not json")]
    public async Task A_malformed_report_is_rejected(string body)
    {
        var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(_feed.LatestFrom(SupportedBrowser.Brave));
    }

    private Task<HttpResponseMessage> PostAsync(string json, string origin = "chrome-extension://abcdefghijklmnopabcdefghijklmnop")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_listener.Endpoint.Port}/tab")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Origin", origin);
        return _http.SendAsync(request);
    }

    private static IEnumerable<IPAddress> NonLoopbackAddresses() =>
        Dns.GetHostAddresses(Dns.GetHostName())
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
}
