using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quantum.Plugin.Abstraction;
using Quantum.Plugins;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Xunit;
namespace Quantum.HostIntegration.Tests.Authentication;

public sealed class IdentityHostTests
{
    [Fact]
    public async Task RealHostDiscoversAccountsAndDispatchesReadOnlyResourcesAcrossReload()
    {
        var root = Directory.CreateTempSubdirectory("quantum-zju-host-");
        var modules = Path.Combine(root.FullName, "Modules");
        var pluginRoot = Directory.CreateDirectory(Path.Combine(modules, "quantum.zju.plugin.oidc"));
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
            Assert.Equal("quantum.zju.plugin.oidc", contribution.PluginId);
            Assert.Equal("Quantum.OfficialPlugins.Zjuam.UI.Pages.ZjuAccount", contribution.Value.GetProperty("component").GetString());
            Assert.Equal(3, contribution.Value.EnumerateObject().Count());
            using var router = new PluginRpcRouter(catalog);
            var context = new PluginRpcCallContext("test.caller", "test", new Dictionary<string, JsonElement>());
            var capabilities = await CallAsync(QuantumCapabilityCatalog.CapabilityName, new
            {
            });
            Assert.Contains(capabilities.Value!.Value.GetProperty("capabilities").EnumerateArray(), item => item.GetProperty("name").GetString() == "zjuam.read-resource");
            var anonymous = await CallAsync("zjuam.read-resource", new
            {
                resource = "xzzd-todos"
            });
            Assert.Equal("zjuam_sign_in_required", anonymous.ErrorCode);
            var invalid = await CallAsync("zjuam.read-resource", new
            {
                resource = "https://example.invalid/"
            });
            Assert.Equal("zjuam_invalid_resource", invalid.ErrorCode);
            var loaded = catalog.FindPlugin("quantum.zju.plugin.oidc")!;
            var sessionType = loaded.EntryAssembly!.GetType("Quantum.OfficialPlugins.Zjuam.ZjuamSession", true)!;
            var session = loaded.Services!.GetRequiredService(sessionType);
            using var endpoint = new TodoEndpoint();
            sessionType.GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, new HttpClient(endpoint));
            sessionType.GetField("_accountKey", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, "fictional-account");
            var read = await CallAsync("zjuam.read-resource", new
            {
                resource = "xzzd-todos"
            });
            Assert.Equal(QuantumRequestStatus.Handled, read.Status);
            Assert.Equal("fictional-account", read.Value!.Value.GetProperty("accountKey").GetString());
            Assert.Empty(read.Value.Value.GetProperty("payload").GetProperty("todo_list").EnumerateArray());
            Assert.True((await manager.ReloadAsync("quantum.zju.plugin.oidc")).Succeeded);
            Assert.Single(accounts.Contributions);
            var impact = manager.GetDisableImpact("quantum.zju.plugin.oidc");
            Assert.True((await manager.DisableAsync("quantum.zju.plugin.oidc", impact.CatalogRevision)).Succeeded);
            Assert.Empty(accounts.Contributions);
            Assert.True((await manager.EnableAsync("quantum.zju.plugin.oidc")).Succeeded);
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
    private sealed class TodoEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://courses.zju.edu.cn/api/todos?no-intercept=true", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = JsonContent.Create(new { todo_list = Array.Empty<object>() }) });
        }
    }
}
