namespace ScreentimeTracker.Core;

/// <summary>Turns the raw URL a browser extension reports into a Website.</summary>
internal static class Website
{
    /// <summary>
    /// The URL's Website: its lowercased hostname minus any leading "www.". Only http(s) pages are
    /// Websites; browser pages such as settings or a new tab, and anything unparseable, are not.
    /// </summary>
    public static string? FromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        var host = uri.Host.ToLowerInvariant().TrimEnd('.');
        if (host.Length == 0) return null;
        return host.StartsWith("www.", StringComparison.Ordinal) ? host["www.".Length..] : host;
    }
}
