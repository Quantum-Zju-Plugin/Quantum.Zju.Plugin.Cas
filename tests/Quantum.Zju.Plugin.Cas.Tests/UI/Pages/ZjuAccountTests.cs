using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quantum.Zju.Plugin.Cas.UI.Pages;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.UI.Pages;

public sealed class ZjuAccountTests
{
    [Theory]
    [InlineData("en", "Sign in")]
    [InlineData("zh-CN", "登录")]
    public async Task DisconnectedAccountOnlyShowsTheStyledSignInAction(string culture, string signIn)
    {
        using var session = new ZjuCasSession(new Browser());
        var html = await RenderAsync(session, culture);
        var text = WebUtility.HtmlDecode(html);
        Assert.Contains(signIn, text);
        Assert.DoesNotContain("Not signed in", text);
        Assert.DoesNotContain("未登录", text);
        Assert.Single(Regex.Matches(html, @"<button\b"));
        Assert.Contains("class=\"primary-button\"", html);
        Assert.DoesNotContain("secondary-button", html);
        Assert.DoesNotContain("password", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Theory]
    [InlineData("en", "Sign in", "Sign out")]
    [InlineData("zh-CN", "登录", "退出")]
    public async Task ReopeningSettingsShowsOnlyTheAccountAndSignOut(string culture, string signIn, string signOut)
    {
        using var session = new ZjuCasSession(new Browser(), _ => new HttpClient(new Endpoint()));
        await session.SignInAsync(default);
        for (var visit = 0; visit < 2; visit++)
        {
            var html = await RenderAsync(session, culture);
            var text = WebUtility.HtmlDecode(html);
            Assert.Contains("&lt;fictional-student&gt;", html);
            Assert.Contains(signOut, text);
            Assert.DoesNotContain(signIn, text);
            Assert.DoesNotContain("Signed in", text);
            Assert.DoesNotContain("已登录", text);
            Assert.DoesNotContain("Switch account", text);
            Assert.DoesNotContain("切换账号", text);
            Assert.Single(Regex.Matches(html, @"<button\b"));
            Assert.Contains("class=\"secondary-button\"", html);
        }
        await session.SignOutAsync(default);
        var signedOut = WebUtility.HtmlDecode(await RenderAsync(session, culture));
        Assert.DoesNotContain("fictional-student", signedOut);
        Assert.Contains(signIn, signedOut);
        Assert.DoesNotContain(signOut, signedOut);
    }

    private static async Task<string> RenderAsync(ZjuCasSession session, string culture)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            await using var services = new ServiceCollection().AddLogging().AddSingleton(session).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<ZjuAccount>()).ToHtmlString());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private sealed class Browser : IZjuBrowser
    {
        public async Task<CasBrowserSession> SignInAsync(Func<CasBrowserSession, CancellationToken, Task> verifySession, CancellationToken cancellationToken)
        {
            var snapshot = new CasBrowserSession(new CookieContainer(), "FictionalBrowser/1.0");
            await verifySession(snapshot, cancellationToken);
            return snapshot;
        }
    }

    private sealed class Endpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(request.RequestUri!.AbsolutePath == "/cas/login"
                ? new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://zjuam.zju.edu.cn/cas/?ticket=ST-fictional") }
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        <cas:serviceResponse xmlns:cas="http://www.yale.edu/tp/cas">
                          <cas:authenticationSuccess><cas:user>&lt;fictional-student&gt;</cas:user></cas:authenticationSuccess>
                        </cas:serviceResponse>
                        """)
                });
    }
}
