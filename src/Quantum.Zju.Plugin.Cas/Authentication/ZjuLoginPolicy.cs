namespace Quantum.Zju.Plugin.Cas;

public static class ZjuLoginPolicy
{
    public static readonly Uri CasOrigin = new("https://zjuam.zju.edu.cn/cas/");
    public static readonly Uri SignIn = new("https://zjuam.zju.edu.cn/cas/login?service=https%3A%2F%2Fzjuam.zju.edu.cn%2Fcas%2F");

    public static Uri CreateFreshSignIn() => new($"{SignIn}&_quantum_login={Guid.NewGuid():N}");

    public static bool IsCasCookie(string domain, string path)
    {
        var cookieDomain = domain.TrimStart('.');
        return (cookieDomain.Equals("zju.edu.cn", StringComparison.OrdinalIgnoreCase)
                || cookieDomain.Equals(CasOrigin.Host, StringComparison.OrdinalIgnoreCase))
            && path.Length > 0 && CasOrigin.AbsolutePath.StartsWith(path, StringComparison.Ordinal)
            && (path.EndsWith('/') || path.Length == CasOrigin.AbsolutePath.Length
                || CasOrigin.AbsolutePath[path.Length] == '/');
    }

    public static bool IsCasPage(string url) => IsAllowedNavigation(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Equals(CasOrigin.Host, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith(CasOrigin.AbsolutePath, StringComparison.Ordinal);

    public static bool IsSignInCallback(string url) => IsAllowedNavigation(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.GetLeftPart(UriPartial.Path) == CasOrigin.AbsoluteUri
        && uri.Fragment.Length == 0
        && uri.Query.StartsWith("?ticket=ST-", StringComparison.Ordinal)
        && uri.Query.Length > "?ticket=ST-".Length
        && !uri.Query.Contains('&');

    public static bool IsAllowedNavigation(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Host.Equals("zju.edu.cn", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".zju.edu.cn", StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedService(string? url) => !string.IsNullOrEmpty(url) && url.Length <= 4096 && !url.Any(char.IsControl)
        && !url.Contains('\\') && IsAllowedNavigation(url) && new Uri(url).Fragment.Length == 0
        && !new Uri(url).Query.TrimStart('?').Split('&').Any(part =>
            Uri.UnescapeDataString(part.Split('=', 2)[0]).Equals("ticket", StringComparison.OrdinalIgnoreCase));
}
