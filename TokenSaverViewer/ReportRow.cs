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

    /// <summary>
    /// Version of the TokenSaver MCP/CLI build that produced this report
    /// (e.g. "1.13.2"). Null for reports ingested before this field existed.
    /// </summary>
    [MaxLength(32)]
    public string? McpVersion { get; set; }

    public DateTime ReceivedUtc { get; set; }

    /// <summary>
    /// Client-generated idempotency key. The durable resend (and concurrently
    /// spawned MCP server processes) can POST the same logical row more than
    /// once; a unique index on this column lets the ingest endpoint dedupe so a
    /// re-send never creates a duplicate. Null for rows ingested before this
    /// field existed, or from clients that don't send one.
    /// </summary>
    public Guid? EventId { get; set; }
}
