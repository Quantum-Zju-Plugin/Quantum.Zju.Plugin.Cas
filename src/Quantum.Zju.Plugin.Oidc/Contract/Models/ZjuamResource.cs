using System.ComponentModel;
using System.Text.Json;

namespace Quantum.OfficialPlugins.Zjuam;

public sealed record ZjuamResource
{
    [Description("Stable opaque key for the current account.")]
    public required string AccountKey { get; set; }

    [Description("JSON returned by the approved resource.")]
    public required JsonElement Payload { get; set; }
}
