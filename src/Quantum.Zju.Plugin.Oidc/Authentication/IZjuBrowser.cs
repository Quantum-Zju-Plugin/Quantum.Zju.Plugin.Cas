using System.Net;
namespace Quantum.OfficialPlugins.Zjuam;

public interface IZjuBrowser
{
    Task<CookieContainer> SignInAsync(CancellationToken cancellationToken);
}
