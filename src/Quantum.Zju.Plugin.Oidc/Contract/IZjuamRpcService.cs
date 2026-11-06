using NOF.Contract;
using Quantum.Plugin.Abstraction;
using System.ComponentModel;

namespace Quantum.OfficialPlugins.Zjuam;

[Description("Read approved ZJU resources using the current local sign-in session.")]
[TransportOverQuantum]
public interface IZjuamRpcService : IRpcService
{
    [Description("Read an approved resource without returning passwords, cookies, or tokens.")]
    [RequestCapability("zjuam.read-resource", QuantumDispatch.Ordered, UniqueOwner = true, RequireSingleProvider = true)]
    Result<ZjuamResource> ReadResource(ReadZjuamResourceRequest request);
}
