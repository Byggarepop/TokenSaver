using System.Text.Json;

namespace TokenSaver;

public sealed record ReportEntry(
    string ToolName,
    string Language,              // "C#", "TypeScript", "Python", ...
    int TokensWithoutTool,
    int TokensWithTool,
    string? Notes,
    string Source,                // "cli" | "mcp"
    DateTime TimestampUtc,
    // Local-only upload-tracking flag (never sent in the upload payload).
    //   null  = legacy row written before durable resend existed — never resent.
    //   false = written by a current build, upload not yet confirmed — a resend candidate.
    //   true  = settled: upload confirmed (2xx), or permanently rejected by a 4xx that
    //           retrying can't fix — never resent either way.
    bool? Uploaded = null,
    // Stable, client-generated idempotency key, sent with the upload payload.
    // Durable resend plus concurrently-spawned server processes can POST the
    // same logical row more than once; the server dedupes on this key so a
    // re-send never creates a duplicate. Guid.Empty on legacy rows written
    // before this field existed (those are Uploaded == null and never resent).
    Guid EventId = default);

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
            DateTime.UtcNow,
            Uploaded: false,
            EventId: Guid.NewGuid());

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

    /// <summary>
    /// Removes entries older than <paramref name="keepDays"/> days from the report file.
    /// </summary>
    public static int Prune(int keepDays, string? path = null)
    {
        var target = path ?? DefaultPath;
        lock (FileLock)
        {
            var entries = LoadOrRecover(target);
            var cutoff = DateTime.UtcNow.AddDays(-keepDays);
            var before = entries.Count;
            entries.RemoveAll(e => e.TimestampUtc < cutoff);
            var removed = before - entries.Count;
            if (removed > 0)
                File.WriteAllText(target, JsonSerializer.Serialize(entries, WriteOpts));
            return removed;
        }
    }

    /// <summary>
    /// Returns every recorded entry (used by the uploader's startup resend pass).
    /// </summary>
    public static List<ReportEntry> LoadAll(string? path = null)
    {
        lock (FileLock) return LoadOrRecover(path ?? DefaultPath);
    }

    /// <summary>
    /// Marks the row matching <paramref name="entry"/> as uploaded (Uploaded = true) so it
    /// is never resent. Matches on the immutable fields of the row; a no-op if no pending
    /// row matches. Re-reads under the file lock so concurrent appends are preserved.
    /// </summary>
    public static void MarkUploaded(ReportEntry entry, string? path = null)
    {
        var target = path ?? DefaultPath;
        lock (FileLock)
        {
            if (!File.Exists(target)) return;
            var entries = LoadOrRecover(target);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                // Prefer the stable EventId; fall back to the immutable-field
                // tuple for legacy rows written before EventId existed.
                var matches = entry.EventId != Guid.Empty
                    ? e.EventId == entry.EventId
                    : e.TimestampUtc == entry.TimestampUtc
                        && e.ToolName == entry.ToolName
                        && e.TokensWithoutTool == entry.TokensWithoutTool
                        && e.TokensWithTool == entry.TokensWithTool
                        && e.Source == entry.Source;
                if (e.Uploaded == false && matches)
                {
                    entries[i] = e with { Uploaded = true };
                    File.WriteAllText(target, JsonSerializer.Serialize(entries, WriteOpts));
                    return;
                }
            }
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
