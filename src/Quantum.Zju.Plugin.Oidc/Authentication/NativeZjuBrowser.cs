using Quantum.Plugin.Abstraction;
using System.Net;
using System.Runtime.Loader;
namespace Quantum.OfficialPlugins.Zjuam;

public sealed class NativeZjuBrowser(IQuantumPluginRuntimeContext runtime) : IZjuBrowser
{
    public Task<CookieContainer> SignInAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacCatalyst())
        {
            throw new PlatformNotSupportedException("ZJU embedded login requires Windows or Mac Catalyst.");
        }
        var context = AssemblyLoadContext.GetLoadContext(typeof(NativeZjuBrowser).Assembly)!;
        var assembly = context.LoadFromAssemblyPath(Path.Combine(runtime.RootPath, "Quantum.Zju.Plugin.Oidc.Native.dll"));
        var browser = (IZjuBrowser)Activator.CreateInstance(assembly.GetType("Quantum.Zju.Plugin.Oidc.Native.EmbeddedZjuBrowser", true)!)!;
        return browser.SignInAsync(cancellationToken);
    }
}
