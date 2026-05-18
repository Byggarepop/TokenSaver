using System.ComponentModel.DataAnnotations;

namespace TokenSaverViewer;

public sealed class ReportRow
{
    public long Id { get; set; }

    [MaxLength(64)]
    public string ToolName { get; set; } = "";

    [MaxLength(32)]
    public string Language { get; set; } = "";

    public int TokensWithoutTool { get; set; }
    public int TokensWithTool { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    [MaxLength(64)]
    public string? ClientId { get; set; }

    public DateTime ReceivedUtc { get; set; }
}
