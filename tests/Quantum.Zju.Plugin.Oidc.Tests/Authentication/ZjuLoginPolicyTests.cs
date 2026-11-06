using Quantum.OfficialPlugins.Zjuam;
using Xunit;
namespace Quantum.Zju.Plugin.Oidc.Tests.Authentication;

public sealed class ZjuLoginPolicyTests
{
    [Theory]
    [InlineData("https://zjuam.zju.edu.cn/cas/login", true)]
    [InlineData("https://zdbk.zju.edu.cn/jwglxt/", true)]
    [InlineData("http://zjuam.zju.edu.cn/", false)]
    [InlineData("https://zju.edu.cn.attacker.invalid/", false)]
    [InlineData("https://attacker.invalid@zjuam.zju.edu.cn/", false)]
    [InlineData("https://zjuam.zju.edu.cn:444/", false)]
    [InlineData("file:///etc/passwd", false)]
    public void OnlyOfficialHttpsNavigationIsAllowed(string url, bool expected) => Assert.Equal(expected, ZjuLoginPolicy.IsAllowedNavigation(url));
    [Fact]
    public async Task UnauthenticatedResourcesFailWithoutOpeningBrowser()
    {
        using var session = new ZjuamSession(new NoBrowser());
        await Assert.ThrowsAsync<NOF.Domain.DomainException>(() => session.ReadAsync(new ReadZjuamResourceRequest { Resource = "zdbk-selected" }, default));
    }
    [Fact]
    public void CookieQueriesIncludeServiceScopedAuthenticationCookies()
    {
        var jar = new System.Net.CookieContainer();
        jar.Add(new System.Net.Cookie("cas-test", "fictional", "/cas", "zjuam.zju.edu.cn"));
        jar.Add(new System.Net.Cookie("academic-test", "fictional", "/jwglxt", "zdbk.zju.edu.cn"));
        var names = ZjuLoginPolicy.CookieOrigins.SelectMany(origin => jar.GetCookies(origin).Cast<System.Net.Cookie>()).Select(cookie => cookie.Name).ToArray();
        Assert.Contains("cas-test", names);
        Assert.Contains("academic-test", names);
    }
    [Fact]
    public async Task PluginStopCancelsAnOpenLoginBeforeWaitingForSignOut()
    {
        var browser = new PendingBrowser();
        using var session = new ZjuamSession(browser);
        var login = session.SignInAsync("fictional", CancellationToken.None);
        await browser.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.CancelSignIn();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.WaitAsync(TimeSpan.FromSeconds(3)));
        await session.SignOutAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3));
    }
    private sealed class PendingBrowser : IZjuBrowser
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<System.Net.CookieContainer> SignInAsync(CancellationToken token)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new System.Net.CookieContainer();
        }
    }
    private sealed class NoBrowser : IZjuBrowser
    {
        public Task<System.Net.CookieContainer> SignInAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected browser call");
    }
}
