using NOF.Domain;
using System.Net;
using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class ZjuCasSessionTests
{
    [Fact]
    public async Task AccountMetadataDoesNotAuthenticateOrExposeBrowserState()
    {
        using var session = new ZjuCasSession(new NoBrowser());
        var account = await session.GetAccountAsync(default);
        Assert.False(account.IsSignedIn);
        Assert.Null(account.SessionId);
        Assert.Null(account.Username);
        Assert.Equal("zju", account.Provider);
        Assert.Equal("cas", account.Protocol);
        await Assert.ThrowsAsync<DomainException>(() => session.GetServiceTicketAsync(new()
        {
            SessionId = "fictional",
            ServiceUrl = "https://service.zju.edu.cn/callback"
        }, default));
    }

    [Fact]
    public async Task AccountVerificationAndServiceTicketsOnlyContactCasAndPreserveBrowserIdentity()
    {
        var endpoint = new CasEndpoint();
        using var session = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(endpoint, false));
        await session.SignInAsync(default);
        var account = await session.GetAccountAsync(default);
        Assert.True(account.IsSignedIn);
        Assert.NotNull(account.SessionId);
        Assert.Equal("fictional-student", account.Username);
        var first = await session.GetServiceTicketAsync(new()
        {
            SessionId = account.SessionId,
            ServiceUrl = "https://service.zju.edu.cn/callback?mode=read"
        }, default);
        var second = await session.GetServiceTicketAsync(new()
        {
            SessionId = account.SessionId,
            ServiceUrl = first.ServiceUrl
        }, default);
        Assert.Equal(4, endpoint.Calls);
        Assert.NotEqual(first.CallbackUrl, second.CallbackUrl);
        Assert.Equal("FictionalBrowser/1.0", first.UserAgent);
        Assert.DoesNotContain("ST-", first.ToString());
        await session.SignOutAsync(default);
        var disconnected = await session.GetAccountAsync(default);
        Assert.False(disconnected.IsSignedIn);
        Assert.Null(disconnected.SessionId);
        Assert.Null(disconnected.Username);
        await Assert.ThrowsAsync<DomainException>(() => session.GetServiceTicketAsync(new()
        {
            SessionId = account.SessionId,
            ServiceUrl = first.ServiceUrl
        }, default));
    }

    [Fact]
    public async Task FailedReplacementPreservesThePreviousAccountAndReconnectInvalidatesItsConnectionId()
    {
        var endpoint = new CasEndpoint();
        using var session = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(endpoint, false));
        await session.SignInAsync(default);
        var previous = await session.GetAccountAsync(default);
        endpoint.Fail = true;
        await Assert.ThrowsAsync<DomainException>(() => session.SignInAsync(default));
        Assert.Equal(previous.SessionId, (await session.GetAccountAsync(default)).SessionId);
        Assert.Equal(previous.Username, (await session.GetAccountAsync(default)).Username);
        endpoint.Fail = false;
        endpoint.Username = "fictional-replacement";
        await session.SignInAsync(default);
        Assert.NotEqual(previous.SessionId, (await session.GetAccountAsync(default)).SessionId);
        Assert.Equal("fictional-replacement", (await session.GetAccountAsync(default)).Username);
        await Assert.ThrowsAsync<DomainException>(() => session.GetServiceTicketAsync(new()
        {
            SessionId = previous.SessionId!,
            ServiceUrl = "https://service.zju.edu.cn/callback"
        }, default));
    }

    [Fact]
    public async Task InvalidIdentityDoesNotReplaceTheCurrentAccount()
    {
        var endpoint = new CasEndpoint();
        using var session = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(endpoint, false));
        await session.SignInAsync(default);
        var previous = await session.GetAccountAsync(default);
        endpoint.FailIdentity = true;
        await Assert.ThrowsAsync<DomainException>(() => session.SignInAsync(default));
        Assert.Equal(previous, await session.GetAccountAsync(default));
    }

    [Fact]
    public async Task ClosingTheBrowserAfterVerificationPreservesThePreviousAccount()
    {
        var browser = new TestBrowser();
        var endpoint = new CasEndpoint();
        using var session = new ZjuCasSession(browser, _ => new HttpClient(endpoint, false));
        await session.SignInAsync(default);
        var previous = await session.GetAccountAsync(default);
        browser.CancelAfterVerification = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SignInAsync(default));
        Assert.Equal(previous, await session.GetAccountAsync(default));
    }

    [Fact]
    public async Task PluginStopCancelsAnOpenLoginBeforeSignOutWaitsForItsGate()
    {
        var browser = new PendingBrowser();
        using var session = new ZjuCasSession(browser);
        var login = session.SignInAsync(default);
        await browser.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.CancelSignIn();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.WaitAsync(TimeSpan.FromSeconds(3)));
        await session.SignOutAsync(default).WaitAsync(TimeSpan.FromSeconds(3));
    }

    private sealed class CasEndpoint : HttpMessageHandler
    {
        public string Username { get; set; } = "fictional-student";
        public bool FailIdentity
        {
            get; set;
        }

        public int Calls
        {
            get; private set;
        }
        public bool Fail
        {
            get; set;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("zjuam.zju.edu.cn", request.RequestUri!.Host);
            Assert.Equal("FictionalBrowser/1.0", request.Headers.UserAgent.ToString());
            if (request.RequestUri.AbsolutePath == "/cas/serviceValidate")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FailIdentity ? "<authenticationFailure/>" : $"""
                        <cas:serviceResponse xmlns:cas="http://www.yale.edu/tp/cas">
                          <cas:authenticationSuccess><cas:user>{Username}</cas:user></cas:authenticationSuccess>
                        </cas:serviceResponse>
                        """)
                });
            }
            Assert.Equal("/cas/login", request.RequestUri.AbsolutePath);
            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
            var service = Uri.UnescapeDataString(request.RequestUri.Query["?service=".Length..]);
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(service + (service.Contains('?') ? "&" : "?") + "ticket=ST-fictional-" + Calls);
            return Task.FromResult(response);
        }
    }

    private sealed class TestBrowser : IZjuBrowser
    {
        public bool CancelAfterVerification
        {
            get; set;
        }

        public async Task<CasBrowserSession> SignInAsync(Func<CasBrowserSession, CancellationToken, Task> verifySession, CancellationToken token)
        {
            var snapshot = new CasBrowserSession(new CookieContainer(), "FictionalBrowser/1.0");
            await verifySession(snapshot, token);
            if (CancelAfterVerification)
            {
                throw new OperationCanceledException();
            }
            return snapshot;
        }
    }
    private sealed class NoBrowser : IZjuBrowser
    {
        public Task<CasBrowserSession> SignInAsync(Func<CasBrowserSession, CancellationToken, Task> verifySession, CancellationToken token)
            => throw new InvalidOperationException("Unexpected browser call");
    }
    private sealed class PendingBrowser : IZjuBrowser
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CasBrowserSession> SignInAsync(Func<CasBrowserSession, CancellationToken, Task> verifySession, CancellationToken token)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new CasBrowserSession(new CookieContainer(), "FictionalBrowser/1.0");
        }
    }
}
