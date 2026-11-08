using System.Net;
using System.Text.Json;

namespace Quantum.Zju.Plugin.Cas;

internal static class SavedCasSession
{
    internal static string? Serialize(CasBrowserSession session)
    {
        var cookies = session.Cookies.GetCookies(ZjuLoginPolicy.CasOrigin).Cast<Cookie>()
            .Where(cookie => ZjuLoginPolicy.IsCasCookie(cookie.Domain, cookie.Path))
            .Select(cookie => new SavedCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path,
                cookie.Expires, cookie.Secure, cookie.HttpOnly)).ToArray();
        return cookies.Length == 0 ? null : JsonSerializer.Serialize(new Snapshot(session.UserAgent, cookies));
    }

    internal static CasBrowserSession? Deserialize(string value)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(value);
        if (string.IsNullOrWhiteSpace(snapshot?.UserAgent) || snapshot.Cookies is not { Length: > 0 })
        {
            return null;
        }
        var cookies = new CookieContainer();
        foreach (var item in snapshot.Cookies)
        {
            if (item is { Domain: not null, Path: not null }
                && ZjuLoginPolicy.IsCasCookie(item.Domain, item.Path))
            {
                var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain)
                {
                    Expires = item.Expires,
                    Secure = item.Secure,
                    HttpOnly = item.HttpOnly
                };
                if (!cookie.Expired)
                {
                    cookies.Add(ZjuLoginPolicy.CasOrigin, cookie);
                }
            }
        }
        return cookies.GetCookies(ZjuLoginPolicy.CasOrigin).Count > 0
            ? new CasBrowserSession(cookies, snapshot.UserAgent) : null;
    }

    private sealed record Snapshot(string UserAgent, SavedCookie[] Cookies);
    private sealed record SavedCookie(string Name, string Value, string Domain, string Path, DateTime Expires, bool Secure, bool HttpOnly);
}
