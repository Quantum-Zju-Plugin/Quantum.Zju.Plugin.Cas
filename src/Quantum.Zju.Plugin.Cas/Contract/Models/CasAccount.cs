namespace Quantum.Zju.Plugin.Cas;

public sealed record CasAccount
{
    public string Provider { get; set; } = "zju";
    public string Protocol { get; set; } = "cas";
    public bool IsSignedIn { get; set; }
    public string? SessionId { get; set; }
    public string? Username { get; set; }
}
