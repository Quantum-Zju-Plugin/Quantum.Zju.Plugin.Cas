using System.ComponentModel;

namespace Quantum.OfficialPlugins.Zjuam;

public sealed record ReadZjuamResourceRequest
{
    [Description("Approved resource identifier: xzzd-todos or a documented zdbk read resource.")]
    public required string Resource { get; set; }

    [Description("Course category for zdbk-courses.")]
    public string? Category { get; set; }

    [Description("First page for zdbk-courses.")]
    public int Start { get; set; } = 1;

    [Description("Last page for zdbk-courses.")]
    public int End { get; set; } = 1;

    [Description("Course code for zdbk-sections or course ID for zdbk-introduction.")]
    public string? Course { get; set; }
}
