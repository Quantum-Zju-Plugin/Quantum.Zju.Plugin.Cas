using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Infrastructure;
using Quantum.Plugin.Abstraction;
using System.Text.Json;
namespace Quantum.Zju.Plugin.Cas;

public sealed class ZjuPlugin : IQuantumPlugin, IAssemblyInitializer
{
    public static void Initialize(IServiceCollection services)
    {
        services.AddSingleton<IZjuBrowser, NativeZjuBrowser>();
        services.AddSingleton<ZjuCasSession>();
        services.AddSingleton<AccountContribution>();
        services.AddRpcServer<ZjuCasRpcService>();
        services.AddScoped<IZjuCasRpcServiceClient, LocalZjuCasRpcServiceClient>();
    }
    public static Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        services.GetRequiredService<AccountContribution>().Start();
        return Task.CompletedTask;
    }
    public static Task StopAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        services.GetRequiredService<ZjuCasSession>().CancelSignIn();
        services.GetRequiredService<AccountContribution>().Dispose();
        return services.GetRequiredService<ZjuCasSession>().SignOutAsync(cancellationToken);
    }
}
