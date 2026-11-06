using NOF.Contract;

namespace Quantum.OfficialPlugins.Zjuam;

public sealed class ReadResource(ZjuamSession session) : ZjuamRpcService.ReadResource
{
    public override async Task<Result<ZjuamResource>> HandleAsync(
        ReadZjuamResourceRequest request, Context context, CancellationToken cancellationToken)
        => await session.ReadAsync(request, cancellationToken);
}
