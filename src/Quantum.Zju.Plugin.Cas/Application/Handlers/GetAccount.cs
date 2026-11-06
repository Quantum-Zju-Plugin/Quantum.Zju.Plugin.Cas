using NOF.Contract;

namespace Quantum.Zju.Plugin.Cas;

public sealed class GetAccount(ZjuCasSession session) : ZjuCasRpcService.GetAccount
{
    public override async Task<Result<CasAccount>> HandleAsync(
        GetCasAccountRequest request, Context context, CancellationToken cancellationToken)
        => await session.GetAccountAsync(cancellationToken);
}
