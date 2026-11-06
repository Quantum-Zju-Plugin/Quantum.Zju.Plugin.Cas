using NOF.Domain;
using System.Net;
using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class CasTicketClientTests
{
    [Theory]
    [InlineData("https://service.zju.edu.cn/callback?ticket=ST-fictional")]
    [InlineData("https://service.zju.edu.cn/callback?ticket=ST-fictional&mode=read&mode=other")]
    public async Task MatchingCallbacksPreserveTheExactServiceQuery(string callback)
    {
        var service = callback.Contains("mode=") ? "https://service.zju.edu.cn/callback?mode=read&mode=other" : "https://service.zju.edu.cn/callback";
        using var client = new HttpClient(new ReplyEndpoint(callback));
        Assert.Equal(callback, await CasTicketClient.RequestCallbackAsync(client, service, default));
    }

    [Theory]
    [InlineData("https://attacker.invalid/callback?mode=read&ticket=ST-fictional")]
    [InlineData("http://service.zju.edu.cn/callback?mode=read&ticket=ST-fictional")]
    [InlineData("https://other.zju.edu.cn/callback?mode=read&ticket=ST-fictional")]
    [InlineData("https://service.zju.edu.cn/other?mode=read&ticket=ST-fictional")]
    [InlineData("https://service.zju.edu.cn/callback?mode=write&ticket=ST-fictional")]
    [InlineData("https://service.zju.edu.cn/callback?mode=read&ticket=ST-one&ticket=ST-two")]
    [InlineData("https://service.zju.edu.cn/callback?mode=read&ticket=ST-")]
    [InlineData("https://service.zju.edu.cn/callback?mode=read&ticket=ST-fictional#fragment")]
    [InlineData("https://service.zju.edu.cn/callback?mode=read")]
    public async Task UnexpectedCallbacksCannotBecomeCredentials(string callback)
    {
        using var client = new HttpClient(new ReplyEndpoint(callback));
        await Assert.ThrowsAsync<DomainException>(() => CasTicketClient.RequestCallbackAsync(client,
            "https://service.zju.edu.cn/callback?mode=read", default));
    }

    [Fact]
    public async Task LoginHtmlDoesNotProveAuthentication()
    {
        using var client = new HttpClient(new ReplyEndpoint(null));
        await Assert.ThrowsAsync<DomainException>(() => CasTicketClient.RequestCallbackAsync(client,
            "https://service.zju.edu.cn/callback", default));
    }

    [Fact]
    public async Task UnapprovedServicesDoNotReachTheNetwork()
    {
        using var client = new HttpClient(new ReplyEndpoint(null));
        await Assert.ThrowsAsync<DomainException>(() => CasTicketClient.RequestCallbackAsync(client,
            "https://attacker.invalid/callback", default));
    }

    private sealed class ReplyEndpoint(string? callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("zjuam.zju.edu.cn", request.RequestUri!.Host);
            Assert.Equal("/cas/login", request.RequestUri.AbsolutePath);
            var response = new HttpResponseMessage(callback is null ? HttpStatusCode.OK : HttpStatusCode.Found);
            if (callback is not null)
            {
                response.Headers.Location = new Uri(callback);
            }
            return Task.FromResult(response);
        }
    }
}
