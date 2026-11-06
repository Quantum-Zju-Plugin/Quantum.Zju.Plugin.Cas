using NOF.Contract;
using Quantum.Plugin.Abstraction;
using System.ComponentModel;

namespace Quantum.Zju.Plugin.Cas;

[Description("Provide the local ZJU CAS account and service-bound authentication tickets.")]
[TransportOverQuantum]
public interface IZjuCasRpcService : IRpcService
{
    [Description("Read local account connection metadata without opening a browser or exposing credentials.")]
    [RequestCapability("zju.cas.account", QuantumDispatch.Ordered, UniqueOwner = true, RequireSingleProvider = true)]
    Result<CasAccount> GetAccount(GetCasAccountRequest request);

    [Description("Obtain a single-use ticket callback for an official HTTPS ZJU service. The result is a credential: never log or persist it.")]
    [RequestCapability("zju.cas.service-ticket", QuantumDispatch.Ordered, UniqueOwner = true, RequireSingleProvider = true)]
    Result<CasServiceTicket> GetServiceTicket(GetCasServiceTicketRequest request);
}
