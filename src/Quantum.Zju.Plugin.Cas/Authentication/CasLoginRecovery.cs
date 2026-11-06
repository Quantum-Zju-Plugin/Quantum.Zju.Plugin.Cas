namespace Quantum.Zju.Plugin.Cas;

public sealed class CasLoginRecovery
{
    // Inspect rendered error text only. Input values and browser credentials are never read.
    public const string ExpirationProbe = """
        (() => {
            const text = (document.body?.innerText || '').replace(/\s/g, '');
            return text.includes('认证登录页面闲置时间过长')
                && text.includes('请打开新页面') && text.includes('F5刷新无效');
        })()
        """;

    private bool _automaticRestartUsed;

    public static bool IsExpired(string url, string? probeResult) => ZjuLoginPolicy.IsCasPage(url)
        && string.Equals(probeResult?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase);

    public Uri? GetAutomaticRestart(string url, string? probeResult)
    {
        if (_automaticRestartUsed || !IsExpired(url, probeResult))
        {
            return null;
        }
        _automaticRestartUsed = true;
        return ZjuLoginPolicy.CreateFreshSignIn();
    }
}
