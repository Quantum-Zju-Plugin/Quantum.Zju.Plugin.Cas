using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Infrastructure;
using Quantum.Plugin.Abstraction;
using System.Text.Json;
namespace Quantum.OfficialPlugins.Zjuam;

public sealed class ZjuPlugin : IQuantumPlugin, IAssemblyInitializer
{
    public static void Initialize(IServiceCollection services)
    {
        services.AddSingleton<IZjuBrowser, NativeZjuBrowser>();
        services.AddSingleton<ZjuamSession>();
        services.AddSingleton<AccountContribution>();
        services.AddRpcServer<ZjuamRpcService>();
        services.AddScoped<IZjuamRpcServiceClient, LocalZjuamRpcServiceClient>();
    }
    public static Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        services.GetRequiredService<AccountContribution>().Start();
        return Task.CompletedTask;
    }
    public static Task StopAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        services.GetRequiredService<ZjuamSession>().CancelSignIn();
        services.GetRequiredService<AccountContribution>().Dispose();
        return services.GetRequiredService<ZjuamSession>().SignOutAsync(cancellationToken);
    }
}
