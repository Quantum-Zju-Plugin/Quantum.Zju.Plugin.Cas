namespace Quantum.Zju.Plugin.Cas;

public static class ZjuLoginPolicy
{
    public static readonly Uri CasOrigin = new("https://zjuam.zju.edu.cn/cas/");
    public static readonly Uri SignIn = new("https://zjuam.zju.edu.cn/cas/login?service=https%3A%2F%2Fzjuam.zju.edu.cn%2Fcas%2F");

    public static bool IsAllowedNavigation(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Host.Equals("zju.edu.cn", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".zju.edu.cn", StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedService(string? url) => !string.IsNullOrEmpty(url) && url.Length <= 4096 && !url.Any(char.IsControl)
        && !url.Contains('\\') && IsAllowedNavigation(url) && new Uri(url).Fragment.Length == 0
        && !new Uri(url).Query.TrimStart('?').Split('&').Any(part =>
            Uri.UnescapeDataString(part.Split('=', 2)[0]).Equals("ticket", StringComparison.OrdinalIgnoreCase));
}
