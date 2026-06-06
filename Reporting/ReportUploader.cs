using System.Linq;
using System.Net.Http.Json;
using System.Reflection;

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

    // Version of the running TokenSaver build that produced this report, sent
    // so the dashboard can attribute savings to a release. Resolved once.
    private static readonly Lazy<string?> McpVersion = new(ResolveMcpVersion);

    // In-flight uploads, tracked so a short-lived CLI process can flush them
    // before exiting (otherwise the background Task is killed on process exit).
    private static readonly object PendingLock = new();
    private static readonly List<Task> Pending = new();
    private static int ProcessExitHooked;

    public static void FireAndForget(ReportEntry entry)
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiUrlEnv);
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        if (TelemetryDisabled()) return;

        EnsureProcessExitHook();

        // On a confirmed (2xx) upload, mark the local row so it is never resent. On any
        // failure the row stays Uploaded == false and the startup resend pass retries it.
        Track(Task.Run(async () =>
        {
            if (await TryUploadAsync(entry, baseUrl).ConfigureAwait(false))
                ReportWriter.MarkUploaded(entry);
        }));
    }

    /// <summary>
    /// Resends rows whose upload was never confirmed (Uploaded == false) — dropped by a
    /// transient failure, a non-2xx response, or a process exit mid-flight. Safe to call on
    /// startup; a no-op when telemetry is disabled or nothing is pending. Runs in the
    /// background so it never delays startup.
    /// </summary>
    public static void ResendPendingInBackground()
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiUrlEnv);
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        if (TelemetryDisabled()) return;

        EnsureProcessExitHook();

        Track(Task.Run(async () =>
        {
            try
            {
                var pending = ReportWriter.LoadAll().Where(e => e.Uploaded == false).ToList();
                await ResendPendingAsync(pending, e => TryUploadAsync(e, baseUrl), e => ReportWriter.MarkUploaded(e))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Telemetry must never break startup.
            }
        }));
    }

    /// <summary>
    /// Pure resend loop, isolated from disk and network so it can be unit-tested: uploads
    /// each entry whose <c>Uploaded</c> flag is false, calling <paramref name="onUploaded"/>
    /// on each success. Legacy (null) and already-confirmed (true) rows are skipped. Returns
    /// the number successfully resent.
    /// </summary>
    public static async Task<int> ResendPendingAsync(
        IEnumerable<ReportEntry> entries,
        Func<ReportEntry, Task<bool>> upload,
        Action<ReportEntry> onUploaded)
    {
        int sent = 0;
        foreach (var entry in entries)
        {
            if (entry.Uploaded != false) continue;
            if (await upload(entry).ConfigureAwait(false))
            {
                onUploaded(entry);
                sent++;
            }
        }
        return sent;
    }

    // POSTs one entry; returns true only on a 2xx response. Network/timeout/non-2xx all
    // return false so the caller leaves the row pending for a later resend. Notes is never
    // uploaded — it can carry user code identifiers and is local-only.
    private static async Task<bool> TryUploadAsync(ReportEntry entry, string baseUrl)
    {
        try
        {
            var payload = new
            {
                entry.ToolName,
                entry.Language,
                entry.TokensWithoutTool,
                entry.TokensWithTool,
                ClientId = ClientId.Value,
                McpVersion = McpVersion.Value,
            };
            var endpoint = baseUrl.TrimEnd('/') + "/api/reports";
            using var resp = await Http.PostAsJsonAsync(endpoint, payload).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool TelemetryDisabled()
    {
        var v = Environment.GetEnvironmentVariable("TOKENSAVER_NO_TELEMETRY");
        return !string.IsNullOrWhiteSpace(v) && v.Trim() != "0";
    }

    private static void Track(Task task)
    {
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

    private static string? ResolveMcpVersion()
    {
        // The assembly this shared reporting code is compiled into is the
        // running tool (the MCP server or the CLI), so its version is the
        // version that produced the report. Mirrors mcp/Program.cs.
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        }
        catch
        {
            return null;
        }
    }
}
