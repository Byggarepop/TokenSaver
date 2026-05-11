using System.Text.Json;

namespace TokenSaver;

public sealed record ReportEntry(
    string ToolName,
    int TokensWithoutTool,
    int TokensWithTool,
    string? Notes,
    string Source,                // "cli" | "mcp"
    DateTime TimestampUtc);

/// <summary>
/// Single sink for every "I just saved tokens" event across the toolkit.
/// CLI `--report`, MCP server invocations, and any future surfaces all
/// append to the same file so the Blazor viewer sees a unified history.
/// </summary>
public static class ReportWriter
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "token-saver-report.json");

    private static readonly object FileLock = new();

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
    };

    public static void Append(
        string toolName,
        int tokensWithoutTool,
        int tokensWithTool,
        string? notes,
        string source,
        string? path = null)
    {
        var target = path ?? DefaultPath;
        var entry = new ReportEntry(
            toolName,
            tokensWithoutTool,
            tokensWithTool,
            notes,
            source,
            DateTime.UtcNow);

        lock (FileLock)
        {
            var entries = LoadOrRecover(target);
            entries.Add(entry);
            File.WriteAllText(target, JsonSerializer.Serialize(entries, WriteOpts));
        }
    }

    private static List<ReportEntry> LoadOrRecover(string path)
    {
        if (!File.Exists(path)) return new List<ReportEntry>();

        try
        {
            var existing = JsonSerializer.Deserialize<List<ReportEntry>>(
                File.ReadAllText(path), ReadOpts);
            return existing ?? new List<ReportEntry>();
        }
        catch (JsonException)
        {
            // Corrupt file — back it up and start fresh rather than dropping data silently.
            File.Move(path, path + ".bak", overwrite: true);
            return new List<ReportEntry>();
        }
    }
}
