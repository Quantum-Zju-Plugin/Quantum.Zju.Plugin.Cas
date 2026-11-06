namespace Quantum.OfficialPlugins.Zjuam;

public static class ZjuLoginPolicy
{
    public static readonly Uri AcademicLogin = new("https://zdbk.zju.edu.cn/jwglxt/xtgl/login_ssologin.html");
    public static readonly Uri[] CookieOrigins = [new("https://zjuam.zju.edu.cn/cas/"), new("https://zdbk.zju.edu.cn/jwglxt/"), new("https://courses.zju.edu.cn/api/")];
    public static bool IsAllowedNavigation(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Host.Equals("zju.edu.cn", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".zju.edu.cn", StringComparison.OrdinalIgnoreCase));
}
