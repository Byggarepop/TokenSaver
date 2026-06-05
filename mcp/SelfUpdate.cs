using System.Diagnostics;

namespace TokenSaver.Mcp;

/// <summary>
/// Background, off-the-critical-path self-update.
///
/// After the MCP host is already serving, this checks the configured NuGet feed(s)
/// for a newer TokenSaver.Mcp version, prefetches it into the dnx tool cache, and
/// only then re-pins the <c>--version</c> in the registered config files. Because the
/// new version is on disk before any launch points at it, upgrades never stall the
/// first query — unlike an unpinned <c>dotnet tool execute</c>, which downloads on the
/// launch critical path and trips the host's startup timeout.
///
/// Opt-out: set TOKENSAVER_DISABLE_AUTOUPDATE=1.
/// Cadence: TOKENSAVER_UPDATE_INTERVAL_MINUTES (default 360; 0 forces every startup).
/// </summary>
internal static class SelfUpdate
{
    static readonly string StampPath = Path.Combine(ReportWriter.DataDir, "lastUpdateCheck");

    internal static async Task RunInBackgroundAsync(bool force = false)
    {
        try
        {
            if (!force && Environment.GetEnvironmentVariable("TOKENSAVER_DISABLE_AUTOUPDATE") == "1")
                return;
            if (!force && !DueForCheck())
                return;

            // Claim the slot up front so sibling server processes don't all check at once.
            TouchStamp();

            string current = RegisterCommand.CurrentPackageVersion();
            string? latest = await DiscoverAndPrefetchLatestAsync().ConfigureAwait(false);
            if (latest is null)
            {
                StartupLog.Write("self-update: discovery failed or reported no version");
                return;
            }

            // Re-pin based on what the host configs actually point at, not on the
            // running process version. When this command is invoked via unpinned
            // `dotnet tool execute`, dnx resolves and runs the *latest* package, so
            // the running version always equals `latest` by construction — comparing
            // them would skip the re-pin even when the configs still pin an older
            // version. Pinning is idempotent (SetPinnedVersion no-ops when already at
            // the target), so re-pinning unconditionally only rewrites stale configs.
            bool repinned = RegisterCommand.PinDnxEntriesToVersion(latest);
            if (repinned)
                StartupLog.Write($"self-update: prefetched {latest} (running {current}); configs re-pinned");
            else
                StartupLog.Write($"self-update: up to date (configs already pinned to {latest}, running {current})");
        }
        catch (Exception ex)
        {
            StartupLog.Write($"self-update: error {ex.Message}");
        }
    }

    static int IntervalMinutes()
    {
        string? raw = Environment.GetEnvironmentVariable("TOKENSAVER_UPDATE_INTERVAL_MINUTES");
        return int.TryParse(raw, out int m) && m >= 0 ? m : 360;
    }

    static bool DueForCheck()
    {
        int interval = IntervalMinutes();
        if (interval == 0) return true;
        try
        {
            if (!File.Exists(StampPath)) return true;
            if (!long.TryParse(File.ReadAllText(StampPath).Trim(), out long ticks)) return true;
            var last = new DateTime(ticks, DateTimeKind.Utc);
            return DateTime.UtcNow - last >= TimeSpan.FromMinutes(interval);
        }
        catch { return true; }
    }

    static void TouchStamp()
    {
        try
        {
            Directory.CreateDirectory(ReportWriter.DataDir);
            File.WriteAllText(StampPath, DateTime.UtcNow.Ticks.ToString());
        }
        catch { }
    }

    /// <summary>
    /// Runs an UNPINNED <c>dotnet tool execute TokenSaver.Mcp --yes -- print-version</c>,
    /// which makes dnx resolve the latest version from the configured feeds, download
    /// it (the prefetch), and print its version. Returns that version, or null on failure.
    /// </summary>
    static async Task<string?> DiscoverAndPrefetchLatestAsync()
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in new[] { "tool", "execute", "TokenSaver.Mcp", "--yes", "--", "print-version" })
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        try
        {
            if (!proc.Start()) return null;
        }
        catch (Exception ex)
        {
            StartupLog.Write($"self-update: failed to start dotnet ({ex.Message})");
            return null;
        }

        Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = proc.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            StartupLog.Write("self-update: discovery timed out");
            return null;
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        if (proc.ExitCode != 0)
        {
            string stderr = await stderrTask.ConfigureAwait(false);
            StartupLog.Write($"self-update: discovery exit {proc.ExitCode}: {Truncate(stderr)}");
            return null;
        }

        // print-version writes the version as the last non-empty stdout line.
        string? version = stdout
            .Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.Length > 0);
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    static string Truncate(string s) =>
        s.Length <= 200 ? s.Trim() : s[..200].Trim() + "…";
}
