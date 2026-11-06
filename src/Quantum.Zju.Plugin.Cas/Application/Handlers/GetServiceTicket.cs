using NOF.Contract;

namespace Quantum.Zju.Plugin.Cas;

public sealed class GetServiceTicket(ZjuCasSession session) : ZjuCasRpcService.GetServiceTicket
{
    public override async Task<Result<CasServiceTicket>> HandleAsync(
        GetCasServiceTicketRequest request, Context context, CancellationToken cancellationToken)
        => await session.GetServiceTicketAsync(request, cancellationToken);
}
