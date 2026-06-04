using System.Net.Http.Json;

namespace TokenSaver;

/// <summary>
/// Fire-and-forget HTTP uploader to a central reporting API. No-op unless
/// the <c>TOKENSAVER_API_URL</c> environment variable is set. Designed so a
/// missing/broken server never blocks or breaks a tool invocation —
/// every failure path is swallowed.
/// </summary>
public static class ReportUploader
{
    private const string ApiUrlEnv = "TOKENSAVER_API_URL";
    private const string ClientIdEnv = "TOKENSAVER_CLIENT_ID";
    private const string ClientIdFileName = "token-saver-client-id";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private static readonly Lazy<string?> ClientId = new(LoadOrCreateClientId);

    // In-flight uploads, tracked so a short-lived CLI process can flush them
    // before exiting (otherwise the background Task is killed on process exit).
    private static readonly object PendingLock = new();
    private static readonly List<Task> Pending = new();
    private static int ProcessExitHooked;

    public static void FireAndForget(ReportEntry entry)
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiUrlEnv);
        if (string.IsNullOrWhiteSpace(baseUrl)) return;

        var noTelemetry = Environment.GetEnvironmentVariable("TOKENSAVER_NO_TELEMETRY");
        if (!string.IsNullOrWhiteSpace(noTelemetry) && noTelemetry.Trim() != "0") return;

        EnsureProcessExitHook();

        var payload = new
        {
            entry.ToolName,
            entry.Language,
            entry.TokensWithoutTool,
            entry.TokensWithTool,
            // Notes is intentionally NOT uploaded: it can contain user code
            // identifiers (method, type, and file names) and is never surfaced
            // on the dashboard. The local report.json still records it in full.
            ClientId = ClientId.Value,
        };

        var task = Task.Run(async () =>
        {
            try
            {
                var endpoint = baseUrl.TrimEnd('/') + "/api/reports";
                using var resp = await Http.PostAsJsonAsync(endpoint, payload).ConfigureAwait(false);
                // Status is ignored on purpose — best-effort telemetry.
            }
            catch
            {
                // Swallow: a slow/dead/firewalled server must not break the tool.
            }
        });

        lock (PendingLock) Pending.Add(task);
        task.ContinueWith(_ => { lock (PendingLock) Pending.Remove(task); },
            TaskScheduler.Default);
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for in-flight uploads to finish.
    /// Long-lived processes (MCP server, Blazor host) don't need to call this;
    /// the ProcessExit hook covers CLI shutdown.
    /// </summary>
    public static void Flush(TimeSpan timeout)
    {
        Task[] snapshot;
        lock (PendingLock) snapshot = Pending.ToArray();
        if (snapshot.Length == 0) return;
        try { Task.WaitAll(snapshot, timeout); }
        catch { /* best-effort */ }
    }

    private static void EnsureProcessExitHook()
    {
        if (Interlocked.Exchange(ref ProcessExitHooked, 1) != 0) return;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush(TimeSpan.FromSeconds(5));
    }

    private static string? LoadOrCreateClientId()
    {
        var envId = Environment.GetEnvironmentVariable(ClientIdEnv);
        if (!string.IsNullOrWhiteSpace(envId)) return envId.Trim();

        try
        {
            var path = Path.Combine(ReportWriter.DataDir, ClientIdFileName);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(existing)) return existing;
            }

            Directory.CreateDirectory(ReportWriter.DataDir);
            var id = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, id);
            return id;
        }
        catch
        {
            return null;
        }
    }
}
