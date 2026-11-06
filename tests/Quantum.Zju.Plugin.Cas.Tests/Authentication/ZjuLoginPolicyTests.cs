using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class ZjuLoginPolicyTests
{
    [Fact]
    public void FreshLoginPreservesTheStandardServiceWithoutForcingRenewal()
    {
        var parameters = CasTicketClient.ReadQuery(ZjuLoginPolicy.CreateFreshSignIn());
        Assert.Contains(("service", ZjuLoginPolicy.CasOrigin.AbsoluteUri), parameters);
        Assert.DoesNotContain(parameters, parameter => parameter.Key == "renew");
    }

    [Theory]
    [InlineData("zjuam.zju.edu.cn", "/", true)]
    [InlineData(".zjuam.zju.edu.cn", "/cas", true)]
    [InlineData("zjuam.zju.edu.cn", "/cas/", true)]
    [InlineData(".zju.edu.cn", "/", true)]
    [InlineData("ZJUAM.ZJU.EDU.CN", "/cas/", true)]
    [InlineData("service.zju.edu.cn", "/", false)]
    [InlineData("zjuam.zju.edu.cn.attacker.invalid", "/", false)]
    [InlineData("zjuam.zju.edu.cn", "/zjuam-main", false)]
    [InlineData("zjuam.zju.edu.cn", "/cas-other", false)]
    [InlineData("zjuam.zju.edu.cn", "/ca", false)]
    [InlineData("zjuam.zju.edu.cn", "/cas/login", false)]
    [InlineData("zjuam.zju.edu.cn", "", false)]
    public void BrowserCleanupOnlyRemovesCookiesApplicableToCas(string domain, string path, bool expected)
        => Assert.Equal(expected, ZjuLoginPolicy.IsCasCookie(domain, path));

    [Theory]
    [InlineData("https://zjuam.zju.edu.cn/cas/?ticket=ST-fictional", true)]
    [InlineData("https://zjuam.zju.edu.cn/cas/", false)]
    [InlineData("https://zjuam.zju.edu.cn/cas/login?service=https://zjuam.zju.edu.cn/cas/", false)]
    [InlineData("https://service.zju.edu.cn/cas/?ticket=ST-fictional", false)]
    [InlineData("https://zjuam.zju.edu.cn/cas/?ticket=ST-", false)]
    [InlineData("https://zjuam.zju.edu.cn/cas/?ticket=ST-fictional&ticket=ST-other", false)]
    [InlineData("https://zjuam.zju.edu.cn/cas/?ticket=ST-fictional#fragment", false)]
    [InlineData("http://zjuam.zju.edu.cn/cas/?ticket=ST-fictional", false)]
    public void OnlyTheCasPortalTicketCallbackTriggersAutomaticCompletion(string url, bool expected)
        => Assert.Equal(expected, ZjuLoginPolicy.IsSignInCallback(url));

    [Theory]
    [InlineData("https://zjuam.zju.edu.cn/cas/", true)]
    [InlineData("https://service.zju.edu.cn/callback?mode=read", true)]
    [InlineData("http://service.zju.edu.cn/", false)]
    [InlineData("https://zju.edu.cn.attacker.invalid/", false)]
    [InlineData("https://attacker.invalid@zjuam.zju.edu.cn/", false)]
    [InlineData("https://zjuam.zju.edu.cn:444/", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("https://service.zju.edu.cn/callback#fragment", false)]
    [InlineData("https://service.zju.edu.cn/callback?ticket=ST-input", false)]
    [InlineData("https://service.zju.edu.cn/callback?%74icket=ST-input", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void ServiceMustBeAnOfficialHttpsCallback(string? url, bool expected)
        => Assert.Equal(expected, ZjuLoginPolicy.IsAllowedService(url));
}
