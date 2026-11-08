using Microsoft.Extensions.Logging;
using Quantum.Plugin.Abstraction;
using System.Runtime.Loader;
namespace Quantum.Zju.Plugin.Cas;

public sealed class NativeZjuBrowser(IQuantumPluginRuntimeContext runtime, ILogger<NativeZjuBrowser> logger) : IZjuBrowser
{
    public Task<CasBrowserSession> SignInAsync(
        Func<CasBrowserSession, CancellationToken, Task> verifySession,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacCatalyst())
        {
            throw new PlatformNotSupportedException("ZJU embedded login requires Windows or Mac Catalyst.");
        }
        var context = AssemblyLoadContext.GetLoadContext(typeof(NativeZjuBrowser).Assembly)!;
        var assembly = context.LoadFromAssemblyPath(Path.Combine(runtime.RootPath, "Quantum.Zju.Plugin.Cas.Native.dll"));
        Action<string, string> report = (phase, result) => logger.LogDebug("CAS browser phase {Phase}; result {Result}.", phase, result);
        var browser = (IZjuBrowser)Activator.CreateInstance(assembly.GetType("Quantum.Zju.Plugin.Cas.Native.EmbeddedZjuBrowser", true)!, report)!;
        return browser.SignInAsync(verifySession, cancellationToken);
    }
}
