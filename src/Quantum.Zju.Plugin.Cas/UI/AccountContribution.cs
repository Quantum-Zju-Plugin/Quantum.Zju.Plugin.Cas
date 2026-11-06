using Quantum.Plugin.Abstraction;
using System.Text.Json;
namespace Quantum.Zju.Plugin.Cas;

public sealed class AccountContribution(IQuantumExtensions extensions) : IDisposable
{
    private IQuantumContribution? _registration;
    public void Start()
    {
        if (_registration is not null)
        {
            return;
        }
        using var payload = JsonDocument.Parse("""
            {"title":"ZJU CAS account","titles":{"zh-CN":"浙江大学 CAS 账号"},"component":"Quantum.Zju.Plugin.Cas.UI.Pages.ZjuAccount"}
            """);
        _registration = extensions.Register("quantum.settings/accounts", 1, "zju-account", payload.RootElement);
    }
    public void Dispose()
    {
        _registration?.Dispose();
        _registration = null;
    }
}
