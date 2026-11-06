using System.ComponentModel;

namespace Quantum.Zju.Plugin.Cas;

public sealed record GetCasServiceTicketRequest
{
    [Description("The current opaque local connection identifier returned by zju.cas.account.")]
    public required string SessionId { get; set; }

    [Description("The exact registered CAS service URL, using HTTPS on a ZJU domain. Do not include a ticket or fragment.")]
    public required string ServiceUrl { get; set; }
}
