namespace Quantum.Zju.Plugin.Cas;

public interface IZjuBrowser
{
    Task<CasBrowserSession> SignInAsync(CancellationToken cancellationToken);
}
