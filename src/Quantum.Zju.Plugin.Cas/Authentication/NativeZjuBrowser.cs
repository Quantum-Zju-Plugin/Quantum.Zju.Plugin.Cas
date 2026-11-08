using Microsoft.Extensions.Logging;
using Quantum.Plugin.Abstraction;
using System.Runtime.Loader;
namespace Quantum.Zju.Plugin.Cas;

public sealed class NativeZjuBrowser(IQuantumPluginRuntimeContext runtime, ILogger<NativeZjuBrowser> logger) : IZjuBrowser, ICasSessionStore
{
    public Task<CasBrowserSession> SignInAsync(
        Func<CasBrowserSession, CancellationToken, Task> verifySession,
        CancellationToken cancellationToken)
    {
        Action<string, string> report = (phase, result) => logger.LogDebug("CAS browser phase {Phase}; result {Result}.", phase, result);
        var browser = (IZjuBrowser)Activator.CreateInstance(LoadNativeType("EmbeddedZjuBrowser"), report)!;
        return browser.SignInAsync(verifySession, cancellationToken);
    }

    Task<string?> ICasSessionStore.ReadAsync() => CreateStore().ReadAsync();
    Task ICasSessionStore.WriteAsync(string value) => CreateStore().WriteAsync(value);
    Task ICasSessionStore.ClearAsync() => CreateStore().ClearAsync();

    private ICasSessionStore CreateStore() => (ICasSessionStore)Activator.CreateInstance(LoadNativeType("SecureCasSessionStore"))!;

    private Type LoadNativeType(string name)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacCatalyst())
        {
            throw new PlatformNotSupportedException("ZJU CAS requires Windows or Mac Catalyst.");
        }
        var context = AssemblyLoadContext.GetLoadContext(typeof(NativeZjuBrowser).Assembly)!;
        var assembly = context.LoadFromAssemblyPath(Path.Combine(runtime.RootPath, "Quantum.Zju.Plugin.Cas.Native.dll"));
        return assembly.GetType($"Quantum.Zju.Plugin.Cas.Native.{name}", true)!;
    }
}
