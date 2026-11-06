using NOF.Domain;
using System.Net;
using Xunit;

namespace Quantum.Zju.Plugin.Cas.Tests.Authentication;

public sealed class CasIdentityClientTests
{
    [Fact]
    public async Task ValidatesThePortalTicketAtCasAndReturnsItsAccount()
    {
        using var endpoint = new IdentityEndpoint(Success("fictional-student"));
        using var client = new HttpClient(endpoint);
        Assert.Equal("fictional-student", await CasIdentityClient.GetUsernameAsync(client, default));
        Assert.Equal(2, endpoint.Calls);
    }

    [Theory]
    [InlineData("<html>Sign in</html>")]
    [InlineData("<cas:serviceResponse")]
    [InlineData("<serviceResponse><authenticationSuccess><user>fictional</user></authenticationSuccess></serviceResponse>")]
    [InlineData("<cas:authenticationFailure code=\"INVALID_TICKET\"/>")]
    [InlineData("<cas:authenticationSuccess><cas:user> </cas:user></cas:authenticationSuccess>")]
    [InlineData("<cas:authenticationSuccess><cas:user>first</cas:user><cas:user>second</cas:user></cas:authenticationSuccess>")]
    [InlineData("<cas:authenticationSuccess><cas:user>first</cas:user></cas:authenticationSuccess><cas:authenticationFailure/>")]
    [InlineData("<cas:authenticationSuccess><cas:user><name>fictional</name></cas:user></cas:authenticationSuccess>")]
    public async Task DoesNotAcceptAnUnverifiedOrAmbiguousAccount(string result)
    {
        var xml = $"<cas:serviceResponse xmlns:cas=\"http://www.yale.edu/tp/cas\">{result}</cas:serviceResponse>";
        using var endpoint = new IdentityEndpoint(xml);
        using var client = new HttpClient(endpoint);
        await Assert.ThrowsAsync<DomainException>(() => CasIdentityClient.GetUsernameAsync(client, default));
    }

    [Fact]
    public async Task RejectsExternalEntitiesAndOversizedIdentities()
    {
        var responses = new[]
        {
            "<!DOCTYPE response [<!ENTITY account SYSTEM 'file:///nonexistent'>]>" + Success("&account;"),
            Success(new string('x', 257)),
            Success(new string('x', 70000))
        };
        foreach (var response in responses)
        {
            using var endpoint = new IdentityEndpoint(response);
            using var client = new HttpClient(endpoint);
            await Assert.ThrowsAsync<DomainException>(() => CasIdentityClient.GetUsernameAsync(client, default));
        }
    }

    private static string Success(string username) => $"""
        <cas:serviceResponse xmlns:cas="http://www.yale.edu/tp/cas">
          <cas:authenticationSuccess><cas:user>{username}</cas:user></cas:authenticationSuccess>
        </cas:serviceResponse>
        """;

    private sealed class IdentityEndpoint(string xml) : HttpMessageHandler
    {
        public int Calls
        {
            get; private set;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("zjuam.zju.edu.cn", request.RequestUri!.Host);
            if (Calls == 1)
            {
                Assert.Equal("/cas/login", request.RequestUri.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://zjuam.zju.edu.cn/cas/?ticket=ST-fictional") }
                });
            }
            Assert.Equal("/cas/serviceValidate", request.RequestUri.AbsolutePath);
            var query = CasTicketClient.ReadQuery(request.RequestUri);
            Assert.Contains(("service", "https://zjuam.zju.edu.cn/cas/"), query);
            Assert.Contains(("ticket", "ST-fictional"), query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
        }
    }
}
