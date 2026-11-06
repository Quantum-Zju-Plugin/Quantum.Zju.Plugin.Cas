using Quantum.Plugin.Abstraction;
using System.Runtime.Loader;
namespace Quantum.Zju.Plugin.Cas;

public sealed class NativeZjuBrowser(IQuantumPluginRuntimeContext runtime) : IZjuBrowser
{
    public Task<CasBrowserSession> SignInAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacCatalyst())
        {
            throw new PlatformNotSupportedException("ZJU embedded login requires Windows or Mac Catalyst.");
        }
        var context = AssemblyLoadContext.GetLoadContext(typeof(NativeZjuBrowser).Assembly)!;
        var assembly = context.LoadFromAssemblyPath(Path.Combine(runtime.RootPath, "Quantum.Zju.Plugin.Cas.Native.dll"));
        var browser = (IZjuBrowser)Activator.CreateInstance(assembly.GetType("Quantum.Zju.Plugin.Cas.Native.EmbeddedZjuBrowser", true)!)!;
        return browser.SignInAsync(cancellationToken);
    }
}
