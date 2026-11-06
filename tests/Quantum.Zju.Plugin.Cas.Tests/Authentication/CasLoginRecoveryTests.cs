using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class CasLoginRecoveryTests
{
    private const string ExpiredFlow = "https://zjuam.zju.edu.cn/cas/login?execution=expired-flow&_eventId=submit";

    [Fact]
    public void ExpirationStartsANewLoginWithoutReusingTheFailedFlow()
    {
        var recovery = new CasLoginRecovery();
        var fresh = recovery.GetAutomaticRestart(ExpiredFlow, "true");
        Assert.NotNull(fresh);
        Assert.Equal("/cas/login", fresh.AbsolutePath);
        Assert.Equal(ZjuLoginPolicy.CasOrigin.Host, fresh.Host);
        var parameters = CasTicketClient.ReadQuery(fresh);
        Assert.Contains(("service", ZjuLoginPolicy.CasOrigin.AbsoluteUri), parameters);
        Assert.Contains(parameters, parameter => parameter.Key == "_quantum_login" && Guid.TryParseExact(parameter.Value, "N", out _));
        Assert.DoesNotContain(parameters, parameter => parameter.Key is "execution" or "_eventId");
        Assert.NotEqual(fresh, ZjuLoginPolicy.CreateFreshSignIn());
    }

    [Fact]
    public void RepeatedExpirationStopsAutomaticNavigationUntilTheUserReopens()
    {
        var recovery = new CasLoginRecovery();
        Assert.NotNull(recovery.GetAutomaticRestart(ExpiredFlow, "true"));
        Assert.Null(recovery.GetAutomaticRestart(ExpiredFlow, "true"));
        Assert.Null(recovery.GetAutomaticRestart(ZjuLoginPolicy.CreateFreshSignIn().AbsoluteUri, "true"));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("\"false\"")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("truefalse")]
    public void AnActiveOrUnavailablePageDoesNotConsumeTheRecoveryAttempt(string? probe)
    {
        var recovery = new CasLoginRecovery();
        Assert.Null(recovery.GetAutomaticRestart(ExpiredFlow, probe));
        Assert.NotNull(recovery.GetAutomaticRestart(ExpiredFlow, "\"true\""));
    }

    [Theory]
    [InlineData("https://service.zju.edu.cn/cas/login")]
    [InlineData("https://zjuam.zju.edu.cn.attacker.invalid/cas/login")]
    [InlineData("https://zjuam.zju.edu.cn:444/cas/login")]
    [InlineData("http://zjuam.zju.edu.cn/cas/login")]
    [InlineData("https://zjuam.zju.edu.cn/zjuam-main/index")]
    [InlineData("https://zjuam.zju.edu.cn/cas-other/login")]
    public void OnlyTheOfficialCasPageCanTriggerRecovery(string url)
    {
        var recovery = new CasLoginRecovery();
        Assert.False(CasLoginRecovery.IsExpired(url, "true"));
        Assert.Null(recovery.GetAutomaticRestart(url, "true"));
        Assert.NotNull(recovery.GetAutomaticRestart(ExpiredFlow, "true"));
    }
}
