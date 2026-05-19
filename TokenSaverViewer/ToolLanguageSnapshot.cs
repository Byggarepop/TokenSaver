using System.ComponentModel.DataAnnotations;

namespace TokenSaverViewer;

public sealed class ToolLanguageSnapshot
{
    [MaxLength(64)]
    public string ToolName { get; set; } = "";

    [MaxLength(32)]
    public string Language { get; set; } = "";

    public long TokensWithoutTotal { get; set; }
    public long TokensWithTotal { get; set; }
    public long RunCount { get; set; }
}
