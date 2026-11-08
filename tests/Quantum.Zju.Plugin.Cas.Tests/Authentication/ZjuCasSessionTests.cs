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
    public async Task PluginStopCancelsAnOpenLoginBeforeWaitingForItsGate()
    {
        var browser = new PendingBrowser();
        using var session = new ZjuCasSession(browser);
        var login = session.SignInAsync(default);
        await browser.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stop = session.StopAsync(default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.WaitAsync(TimeSpan.FromSeconds(3)));
        await stop.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task RestartRestoresVerifiedAccountWithoutBrowserAndExplicitSignOutClearsIt()
    {
        var store = new TestStore();
        var endpoint = new CasEndpoint();
        string? firstId;
        using (var first = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(endpoint, false), store: store))
        {
            await first.SignInAsync(default);
            firstId = (await first.GetAccountAsync(default)).SessionId;
            await first.StopAsync(default);
        }
        Assert.NotNull(store.Value);
        Assert.DoesNotContain("fictional-student", store.Value);
        Assert.DoesNotContain("ST-fictional", store.Value);
        Assert.DoesNotContain("unrelated", store.Value);

        using (var restarted = new ZjuCasSession(new NoBrowser(), snapshot =>
        {
            var cookie = Assert.Single(snapshot.Cookies.GetCookies(ZjuLoginPolicy.CasOrigin).Cast<Cookie>());
            Assert.Equal("fictional-session", cookie.Value);
            Assert.True(cookie.HttpOnly);
            Assert.True(cookie.Secure);
            Assert.Equal(DateTime.MinValue, cookie.Expires);
            return new HttpClient(endpoint, false);
        }, store: store))
        {
            var account = await restarted.GetAccountAsync(default);
            Assert.True(account.IsSignedIn);
            Assert.Equal("fictional-student", account.Username);
            Assert.NotEqual(firstId, account.SessionId);
            Assert.Equal(4, endpoint.Calls);
            Assert.Equal(account, await restarted.GetAccountAsync(default));
            Assert.Equal(4, endpoint.Calls);
            var ticket = await restarted.GetServiceTicketAsync(new()
            {
                SessionId = account.SessionId!,
                ServiceUrl = "https://service.zju.edu.cn/callback"
            }, default);
            Assert.Equal("FictionalBrowser/1.0", ticket.UserAgent);
            await restarted.SignOutAsync(default);
        }
        Assert.Null(store.Value);
        using var signedOut = new ZjuCasSession(new NoBrowser(), _ => throw new InvalidOperationException(), store: store);
        Assert.False((await signedOut.GetAccountAsync(default)).IsSignedIn);
        Assert.Equal(5, endpoint.Calls);
    }

    [Fact]
    public async Task ExpiredLocalCookiesAreNotSentForRestoration()
    {
        var store = new TestStore
        {
            Value = """
                {"UserAgent":"FictionalBrowser/1.0","Cookies":[{"Name":"TGC","Value":"fictional-expired",
                "Domain":"zjuam.zju.edu.cn","Path":"/cas/","Expires":"2000-01-01T00:00:00Z","Secure":true,"HttpOnly":true}]}
                """
        };
        var calls = 0;
        using var session = new ZjuCasSession(new NoBrowser(), _ =>
        {
            calls++;
            return new HttpClient(new CasEndpoint());
        }, store: store);
        Assert.False((await session.GetAccountAsync(default)).IsSignedIn);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task UnverifiedSavedSessionDoesNotAppearSignedInAndCanBeRetriedAfterRestart()
    {
        var store = new TestStore();
        var endpoint = new CasEndpoint();
        using (var first = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(endpoint, false), store: store))
        {
            await first.SignInAsync(default);
        }
        var saved = store.Value;
        endpoint.Fail = true;
        using (var unavailable = new ZjuCasSession(new NoBrowser(), _ => new HttpClient(endpoint, false), store: store))
        {
            var account = await unavailable.GetAccountAsync(default);
            Assert.False(account.IsSignedIn);
            Assert.Null(account.Username);
            Assert.Equal(saved, store.Value);
            Assert.False((await unavailable.GetAccountAsync(default)).IsSignedIn);
            Assert.Equal(3, endpoint.Calls);
        }
        endpoint.Fail = false;
        using var restored = new ZjuCasSession(new NoBrowser(), _ => new HttpClient(endpoint, false), store: store);
        Assert.True((await restored.GetAccountAsync(default)).IsSignedIn);
    }

    [Fact]
    public async Task CookiesRefreshedByCasAreAvailableAfterRestart()
    {
        var store = new TestStore();
        var endpoint = new CasEndpoint();
        using (var session = new ZjuCasSession(new TestBrowser(), snapshot =>
        {
            endpoint.OnTicketIssued = () =>
            {
                if (endpoint.Calls == 3)
                {
                    snapshot.Cookies.SetCookies(ZjuLoginPolicy.CasOrigin, "TGC=fictional-refreshed; Path=/cas/; Secure; HttpOnly");
                }
            };
            return new HttpClient(endpoint, false);
        }, store: store))
        {
            await session.SignInAsync(default);
            var account = await session.GetAccountAsync(default);
            await session.GetServiceTicketAsync(new()
            {
                SessionId = account.SessionId!,
                ServiceUrl = "https://service.zju.edu.cn/callback"
            }, default);
        }
        using var restarted = new ZjuCasSession(new NoBrowser(), snapshot =>
        {
            Assert.Contains("TGC=fictional-refreshed", snapshot.Cookies.GetCookieHeader(ZjuLoginPolicy.CasOrigin));
            return new HttpClient(endpoint, false);
        }, store: store);
        Assert.True((await restarted.GetAccountAsync(default)).IsSignedIn);
    }

    [Fact]
    public async Task SignOutDoesNotReportSuccessIfSecureStorageCannotClearTheSession()
    {
        var store = new TestStore();
        using var session = new ZjuCasSession(new TestBrowser(), _ => new HttpClient(new CasEndpoint()), store: store);
        await session.SignInAsync(default);
        store.FailClear = true;
        await Assert.ThrowsAsync<IOException>(() => session.SignOutAsync(default));
        Assert.True((await session.GetAccountAsync(default)).IsSignedIn);
        store.FailClear = false;
        await session.SignOutAsync(default);
        Assert.Null(store.Value);
        Assert.False((await session.GetAccountAsync(default)).IsSignedIn);
    }

    private sealed class TestStore : ICasSessionStore
    {
        public string? Value
        {
            get; set;
        }
        public bool FailClear
        {
            get; set;
        }

        public Task<string?> ReadAsync() => Task.FromResult(Value);

        public Task WriteAsync(string value)
        {
            Value = value;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            if (FailClear)
            {
                throw new IOException();
            }
            Value = null;
            return Task.CompletedTask;
        }
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
        public Action? OnTicketIssued
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
            OnTicketIssued?.Invoke();
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
            var cookies = new CookieContainer();
            cookies.Add(new Cookie("TGC", "fictional-session", "/cas/", "zjuam.zju.edu.cn") { Secure = true, HttpOnly = true });
            cookies.Add(new Cookie("unrelated", "fictional-other", "/", "service.zju.edu.cn"));
            var snapshot = new CasBrowserSession(cookies, "FictionalBrowser/1.0");
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
