using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quantum.Plugin.Abstraction;
using Quantum.Plugins;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Quantum.HostIntegration.Tests.Authentication;

public sealed class IdentityHostTests
{
    [Fact]
    public async Task RealHostDiscoversCasAccountAndDispatchesServiceTicketsAcrossReload()
    {
        var root = Directory.CreateTempSubdirectory("quantum-zju-host-");
        var modules = Path.Combine(root.FullName, "Modules");
        var pluginRoot = Directory.CreateDirectory(Path.Combine(modules, "quantum.zju.plugin.cas"));
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "PluginFixture")))
        {
            File.Copy(file, Path.Combine(pluginRoot.FullName, Path.GetFileName(file)));
        }
        var catalog = new PluginCatalog([]);
        using var services = new ServiceCollection().AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance).AddQuantumPluginEventBus().BuildServiceProvider();
        var shadow = Path.Combine(root.FullName, "Shadow");
        try
        {
            await using var manager = new PluginRuntimeManager(catalog, new PluginRuntimeOptions(modules, shadow, Path.Combine(root.FullName, "test.db")));
            await manager.InitializeAsync(services);
            Assert.Empty(catalog.Failures);
            QuantumExtensionSnapshot? accounts = null;
            using var observation = catalog.Extensions.Observe("quantum.settings/accounts", 1, snapshot => accounts = snapshot);
            var contribution = Assert.Single(accounts!.Contributions);
            Assert.Equal("quantum.zju.plugin.cas", contribution.PluginId);
            Assert.Equal("Quantum.Zju.Plugin.Cas.UI.Pages.ZjuAccount", contribution.Value.GetProperty("component").GetString());
            Assert.Equal(3, contribution.Value.EnumerateObject().Count());
            using var router = new PluginRpcRouter(catalog);
            var context = new PluginRpcCallContext("test.caller", "test", new Dictionary<string, JsonElement>());
            var capabilities = await CallAsync(QuantumCapabilityCatalog.CapabilityName, new
            {
            });
            var names = capabilities.Value!.Value.GetProperty("capabilities").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
            Assert.Contains("zju.cas.account", names);
            Assert.Contains("zju.cas.service-ticket", names);
            Assert.DoesNotContain("zjuam.read-resource", names);
            var account = await CallAsync("zju.cas.account", new
            {
            });
            Assert.False(account.Value!.Value.GetProperty("isSignedIn").GetBoolean());
            Assert.Equal(JsonValueKind.Null, account.Value.Value.GetProperty("username").ValueKind);
            var anonymous = await CallAsync("zju.cas.service-ticket", new
            {
                sessionId = "fictional",
                serviceUrl = "https://service.zju.edu.cn/callback"
            });
            Assert.Equal("zju_cas_sign_in_required", anonymous.ErrorCode);
            var loaded = catalog.FindPlugin("quantum.zju.plugin.cas")!;
            var sessionType = loaded.EntryAssembly!.GetType("Quantum.Zju.Plugin.Cas.ZjuCasSession", true)!;
            var session = loaded.Services!.GetRequiredService(sessionType);
            using var endpoint = new TicketEndpoint();
            var client = new HttpClient(endpoint);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FictionalBrowser/1.0");
            sessionType.GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, client);
            sessionType.GetField("_sessionId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, "fictional-connection");
            sessionType.GetField("_userAgent", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, "FictionalBrowser/1.0");
            sessionType.GetField("_username", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, "fictional-student");
            Assert.Equal("fictional-student", (await CallAsync("zju.cas.account", new
            {
            })).Value!.Value.GetProperty("username").GetString());
            var invalid = await CallAsync("zju.cas.service-ticket", new
            {
                sessionId = "fictional-connection",
                serviceUrl = "https://attacker.invalid/"
            });
            Assert.Equal("zju_cas_invalid_service", invalid.ErrorCode);
            var ticket = await CallAsync("zju.cas.service-ticket", new
            {
                sessionId = "fictional-connection",
                serviceUrl = "https://service.zju.edu.cn/callback"
            });
            Assert.Equal(QuantumRequestStatus.Handled, ticket.Status);
            Assert.Equal("https://service.zju.edu.cn/callback?ticket=ST-fictional", ticket.Value!.Value.GetProperty("callbackUrl").GetString());
            Assert.Equal("FictionalBrowser/1.0", ticket.Value.Value.GetProperty("userAgent").GetString());
            Assert.Equal(4, ticket.Value.Value.EnumerateObject().Count());
            Assert.True((await manager.ReloadAsync("quantum.zju.plugin.cas")).Succeeded);
            Assert.Single(accounts.Contributions);
            var reloaded = await CallAsync("zju.cas.account", new
            {
            });
            Assert.False(reloaded.Value!.Value.GetProperty("isSignedIn").GetBoolean());
            Assert.Equal(JsonValueKind.Null, reloaded.Value.Value.GetProperty("username").ValueKind);
            var impact = manager.GetDisableImpact("quantum.zju.plugin.cas");
            Assert.True((await manager.DisableAsync("quantum.zju.plugin.cas", impact.CatalogRevision)).Succeeded);
            Assert.Empty(accounts.Contributions);
            Assert.True((await manager.EnableAsync("quantum.zju.plugin.cas")).Succeeded);
            Assert.Single(accounts.Contributions);
            async Task<QuantumRequestOutcome<JsonElement?>> CallAsync(string name, object payload)
            {
                var result = await router.RequestAsync(name, JsonSerializer.SerializeToElement(payload), context, CancellationToken.None);
                Assert.True(result.IsSuccess, result.Message);
                return Assert.Single(result.Value!.Outcomes);
            }
        }
        finally
        {
            if (PluginShadowCopy.TryDelete(shadow))
            {
                root.Delete(true);
            }
        }
    }

    private sealed class TicketEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("zjuam.zju.edu.cn", request.RequestUri!.Host);
            Assert.Equal("/cas/login", request.RequestUri.AbsolutePath);
            Assert.Equal("FictionalBrowser/1.0", request.Headers.UserAgent.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://service.zju.edu.cn/callback?ticket=ST-fictional");
            return Task.FromResult(response);
        }
    }
}
