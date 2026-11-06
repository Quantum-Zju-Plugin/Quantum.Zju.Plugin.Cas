using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class ZjuLoginPolicyTests
{
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
