using System.Text.Json;

namespace TokenSaver;

public sealed record ReportEntry(
    string ToolName,
    string Language,              // "C#", "TypeScript", "Python", ...
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
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".tokensaver");

    public static string DefaultPath { get; } = Path.Combine(DataDir, "report.json");

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
        string language,
        int tokensWithoutTool,
        int tokensWithTool,
        string? notes,
        string source,
        string? path = null)
    {
        var target = path ?? DefaultPath;
        var entry = new ReportEntry(
            toolName,
            language,
            tokensWithoutTool,
            tokensWithTool,
            notes,
            source,
            DateTime.UtcNow);

        lock (FileLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var entries = LoadOrRecover(target);
            entries.Add(entry);
            File.WriteAllText(target, JsonSerializer.Serialize(entries, WriteOpts));
        }

        // Best-effort upload to a central API. No-op unless TOKENSAVER_API_URL is set.
        ReportUploader.FireAndForget(entry);
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
