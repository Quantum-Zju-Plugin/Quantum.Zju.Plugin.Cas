using System.ComponentModel;

namespace Quantum.Zju.Plugin.Cas;

public sealed record CasServiceTicket
{
    public required string SessionId { get; set; }
    public required string ServiceUrl { get; set; }
    [Description("Credential-bearing callback URL. Consume immediately once; never log or persist it.")]
    public required string CallbackUrl { get; set; }
    public required string UserAgent { get; set; }

    public override string ToString() => "CAS service ticket (redacted)";
}
