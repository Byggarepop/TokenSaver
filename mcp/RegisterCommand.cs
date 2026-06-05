using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TokenSaver.Mcp;

/// <summary>
/// Implements `tokensaver-mcp register [--local] [--claude-desktop] [--vs]`.
/// Safely injects the tokensaver MCP server entry into the relevant config
/// files without overwriting unrelated entries.
/// </summary>
internal static class RegisterCommand
{
    const string ServerName = "tokensaver";
    const string ApiUrl = "https://tokensavermcp.com";

    internal static int Run(string[] args)
    {
        bool local = args.Contains("--local");
        bool claudeOnly = args.Contains("--claude-desktop");
        bool claudeCodeOnly = args.Contains("--claude-code");
        bool vsOnly = args.Contains("--vs");
        bool vsCodeOnly = args.Contains("--vscode");
        bool doAll = !claudeOnly && !claudeCodeOnly && !vsOnly && !vsCodeOnly;

        int failures = 0;

        if (doAll || claudeOnly)
        {
            if (!RegisterClaudeDesktop())
                failures++;
        }

        if (doAll || claudeCodeOnly)
        {
            if (!RegisterClaudeCode())
                failures++;
        }

        if (doAll || vsCodeOnly)
        {
            if (!RegisterVsCode())
                failures++;
        }

        if (local)
        {
            // --local: write mcp.json in the current working directory
            string localPath = Path.Combine(Directory.GetCurrentDirectory(), "mcp.json");
            if (!RegisterVs(localPath, label: "VS (solution-local)"))
                failures++;
        }
        else if (doAll || vsOnly)
        {
            if (!RegisterVsGlobal())
                failures++;
        }

        if (failures == 0)
            Console.WriteLine("\nDone. Restart your MCP host (Claude Desktop / Claude Code / Visual Studio) to pick up the change.");
        else
            Console.Error.WriteLine($"\n{failures} registration(s) failed — see errors above.");

        return failures == 0 ? 0 : 1;
    }

    // -------------------------------------------------------------------------
    // Claude Desktop
    // -------------------------------------------------------------------------

    static bool RegisterClaudeDesktop()
    {
        string configPath = GetClaudeDesktopConfigPath();
        Console.WriteLine($"Claude Desktop  →  {configPath}");

        try
        {
            var root = LoadOrCreate(configPath);

            if (root["mcpServers"] is not JsonObject servers)
            {
                servers = [];
                root["mcpServers"] = servers;
            }

            bool existed = servers.ContainsKey(ServerName);
            servers[ServerName] = BuildClaudeEntry();

            Save(configPath, root);
            Console.WriteLine(existed ? $"  Updated existing '{ServerName}' entry." : $"  Added '{ServerName}' entry.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR: {ex.Message}");
            return false;
        }
    }

    static string GetClaudeDesktopConfigPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude", "claude_desktop_config.json");

        // macOS
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Claude", "claude_desktop_config.json");
    }

    static JsonObject BuildClaudeEntry() => new()
    {
        ["command"] = "dotnet",
        ["args"] = BuildDnxArgs(),
        ["env"] = new JsonObject { ["TOKENSAVER_API_URL"] = ApiUrl }
    };

    // -------------------------------------------------------------------------
    // Claude Code CLI (~/.claude.json)
    // -------------------------------------------------------------------------

    static bool RegisterClaudeCode()
    {
        string configPath = GetClaudeCodeConfigPath();
        Console.WriteLine($"Claude Code CLI  →  {configPath}");

        try
        {
            var root = LoadOrCreate(configPath);

            if (root["mcpServers"] is not JsonObject servers)
            {
                servers = [];
                root["mcpServers"] = servers;
            }

            bool existed = servers.ContainsKey(ServerName);
            servers[ServerName] = BuildClaudeCodeEntry();

            Save(configPath, root);
            Console.WriteLine(existed ? $"  Updated existing '{ServerName}' entry." : $"  Added '{ServerName}' entry.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR: {ex.Message}");
            return false;
        }
    }

    static string GetClaudeCodeConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude.json");

    static JsonObject BuildClaudeCodeEntry() => new()
    {
        ["type"] = "stdio",
        ["command"] = "dotnet",
        ["args"] = BuildDnxArgs(),
        ["env"] = new JsonObject { ["TOKENSAVER_API_URL"] = ApiUrl }
    };

    // -------------------------------------------------------------------------
    // VS Code / GitHub Copilot Chat
    // -------------------------------------------------------------------------

    static bool RegisterVsCode()
    {
        string? configPath = GetVsCodeSettingsPath();
        if (configPath is null)
        {
            Console.WriteLine("VS Code  →  skipped (no VS Code installation found)");
            return true;
        }

        Console.WriteLine($"VS Code  →  {configPath}");

        try
        {
            var root = LoadOrCreate(configPath);

            if (root["mcp"] is not JsonObject mcp)
            {
                mcp = [];
                root["mcp"] = mcp;
            }

            if (mcp["servers"] is not JsonObject servers)
            {
                servers = [];
                mcp["servers"] = servers;
            }

            bool existed = servers.ContainsKey(ServerName);
            servers[ServerName] = BuildVsEntry();

            Save(configPath, root);
            Console.WriteLine(existed ? $"  Updated existing '{ServerName}' entry." : $"  Added '{ServerName}' entry.");
            return true;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  ERROR: settings.json contains invalid JSON (line {ex.LineNumber + 1}, pos {ex.BytePositionInLine}).");
            Console.Error.WriteLine($"  Open {configPath} in VS Code, fix the syntax error, then re-run register.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR: {ex.Message}");
            return false;
        }
    }

    static string? GetVsCodeSettingsPath()
    {
        string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string path;
        if (OperatingSystem.IsWindows())
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code", "User", "settings.json");
        else if (OperatingSystem.IsMacOS())
            path = Path.Combine(userDir, "Library", "Application Support", "Code", "User", "settings.json");
        else
            path = Path.Combine(userDir, ".config", "Code", "User", "settings.json");

        // Only register if VS Code is actually installed (settings dir exists or settings file exists).
        // Avoid creating a dangling file for users who don't have VS Code.
        string? dir = Path.GetDirectoryName(path);
        if (!File.Exists(path) && (dir is null || !Directory.Exists(dir)))
            return null;

        return path;
    }

    // -------------------------------------------------------------------------
    // Visual Studio 2026 / generic MCP host (.mcp.json)
    // -------------------------------------------------------------------------

    static bool RegisterVsGlobal()
    {
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mcp.json");
        return RegisterVs(configPath, label: "VS global (~/.mcp.json)");
    }

    static bool RegisterVs(string configPath, string label)
    {
        Console.WriteLine($"{label}  →  {configPath}");

        try
        {
            var root = LoadOrCreate(configPath);

            if (root["servers"] is not JsonObject servers)
            {
                servers = [];
                root["servers"] = servers;
            }

            bool existed = servers.ContainsKey(ServerName);
            servers[ServerName] = BuildVsEntry();

            Save(configPath, root);
            Console.WriteLine(existed ? $"  Updated existing '{ServerName}' entry." : $"  Added '{ServerName}' entry.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR: {ex.Message}");
            return false;
        }
    }

    static JsonObject BuildVsEntry() => new()
    {
        ["type"] = "stdio",
        ["command"] = "dotnet",
        ["args"] = BuildDnxArgs(),
        ["env"] = new JsonObject { ["TOKENSAVER_API_URL"] = ApiUrl }
    };

    // -------------------------------------------------------------------------
    // Version pinning + dnx entry helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// The NuGet package version of the running server (e.g. "1.12.0", or
    /// "1.12.1-localtest" for a prerelease). Uses the informational version,
    /// which preserves any prerelease suffix, and strips build metadata after '+'.
    /// </summary>
    internal static string CurrentPackageVersion()
    {
        string? info = typeof(RegisterCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            int plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return typeof(RegisterCommand).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>The pinned dnx launch args: <c>tool execute TokenSaver.Mcp --version &lt;current&gt; --yes</c>.</summary>
    static JsonArray BuildDnxArgs() =>
        new("tool", "execute", "TokenSaver.Mcp", "--version", CurrentPackageVersion(), "--yes");

    /// <summary>True if the entry launches via <c>dotnet tool execute TokenSaver.Mcp</c> (the dnx model).</summary>
    internal static bool IsDnxEntry(JsonObject entry)
    {
        if (entry["command"]?.GetValue<string>() != "dotnet") return false;
        if (entry["args"] is not JsonArray args) return false;

        bool execute = false, package = false;
        foreach (JsonNode? a in args)
        {
            string? s = a?.GetValue<string>();
            if (s == "execute") execute = true;
            else if (s == "TokenSaver.Mcp") package = true;
        }
        return execute && package;
    }

    /// <summary>
    /// Ensures a dnx entry's args pin <c>--version &lt;version&gt;</c>. Replaces an existing
    /// pinned value, or inserts the flag right after the package id. Returns true if the
    /// args were changed.
    /// </summary>
    internal static bool SetPinnedVersion(JsonObject entry, string version)
    {
        if (entry["args"] is not JsonArray args) return false;

        for (int i = 0; i < args.Count; i++)
        {
            if (args[i]?.GetValue<string>() != "--version") continue;
            if (i + 1 < args.Count && args[i + 1]?.GetValue<string>() == version)
                return false;                        // already pinned to this version
            if (i + 1 < args.Count) args[i + 1] = version;
            else args.Add(version);
            return true;
        }

        int pkg = -1;
        for (int i = 0; i < args.Count; i++)
            if (args[i]?.GetValue<string>() == "TokenSaver.Mcp") { pkg = i; break; }
        if (pkg < 0) return false;

        args.Insert(pkg + 1, "--version");
        args.Insert(pkg + 2, version);
        return true;
    }

    /// <summary>True if <paramref name="candidate"/> is a newer version than <paramref name="current"/> (ignoring prerelease suffixes).</summary>
    internal static bool IsNewer(string candidate, string current)
    {
        static Version Core(string v)
        {
            int dash = v.IndexOf('-');
            string core = dash >= 0 ? v[..dash] : v;
            return Version.TryParse(core, out Version? ver) ? ver : new Version(0, 0, 0);
        }
        return Core(candidate) > Core(current);
    }

    // -------------------------------------------------------------------------
    // Auto-update on startup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called on every MCP server startup. Reconciles any already-registered config
    /// entries: refreshes a missing/stale TOKENSAVER_API_URL and, for dnx entries,
    /// pins <c>--version</c> to the running version (always safe — the running version
    /// is by definition already in the dnx cache). Gated by a version sentinel so the
    /// work only happens once per installed version. Logs to stderr only — stdout is
    /// reserved for JSON-RPC traffic.
    /// </summary>
    internal static void AutoUpdateRegistrations()
    {
        string version = typeof(RegisterCommand).Assembly.GetName().Version?.ToString(3) ?? "0";
        string sentinelPath = Path.Combine(TokenSaver.ReportWriter.DataDir, "registered");

        try
        {
            if (File.Exists(sentinelPath) && File.ReadAllText(sentinelPath).Trim() == version)
                return;
        }
        catch { }

        TryUpdateFlatConfig(GetClaudeDesktopConfigPath(), "mcpServers");
        TryUpdateFlatConfig(GetClaudeCodeConfigPath(), "mcpServers");

        string? vsCodePath = GetVsCodeSettingsPath();
        if (vsCodePath is not null)
            TryUpdateVsCodeConfig(vsCodePath);

        TryUpdateFlatConfig(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mcp.json"),
            "servers");

        try
        {
            Directory.CreateDirectory(TokenSaver.ReportWriter.DataDir);
            File.WriteAllText(sentinelPath, version);
        }
        catch { }
    }

    /// <summary>Reconciles an existing tokensaver entry in a flat config (root → serversKey → ServerName).</summary>
    static void TryUpdateFlatConfig(string path, string serversKey)
    {
        if (!File.Exists(path)) return;
        try
        {
            var root = LoadOrCreate(path);
            if (root[serversKey] is not JsonObject servers) return;
            if (servers[ServerName] is not JsonObject entry) return;
            if (!ReconcileEntry(entry, CurrentPackageVersion())) return;

            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] reconciled entry in {path}");
        }
        catch { }
    }

    /// <summary>Reconciles an existing tokensaver entry in VS Code's nested config (root → mcp → servers → ServerName).</summary>
    static void TryUpdateVsCodeConfig(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var root = LoadOrCreate(path);
            if (root["mcp"] is not JsonObject mcp) return;
            if (mcp["servers"] is not JsonObject servers) return;
            if (servers[ServerName] is not JsonObject entry) return;
            if (!ReconcileEntry(entry, CurrentPackageVersion())) return;

            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] reconciled entry in {path}");
        }
        catch { }
    }

    /// <summary>
    /// Brings an existing entry up to date: refreshes TOKENSAVER_API_URL and, for dnx
    /// entries, pins <paramref name="pinVersion"/>. Returns true if anything changed.
    /// </summary>
    static bool ReconcileEntry(JsonObject entry, string pinVersion)
    {
        bool changed = false;
        if (NeedsUrlUpdate(entry)) { ApplyUrl(entry); changed = true; }
        if (IsDnxEntry(entry) && SetPinnedVersion(entry, pinVersion)) changed = true;
        return changed;
    }

    // -------------------------------------------------------------------------
    // Background self-update support (called from SelfUpdate)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pins every registered dnx-style tokensaver entry to <paramref name="version"/>.
    /// Called by the background self-update only after that version has been confirmed
    /// present in the dnx cache, so the next launch is offline-instant. Safe to call
    /// from a background thread; failures are swallowed per-file.
    /// </summary>
    /// <summary>
    /// Re-pins every discovered host config to <paramref name="version"/>.
    /// Returns true if at least one config was actually changed (a config
    /// already pinned to <paramref name="version"/> is left untouched).
    /// </summary>
    internal static bool PinDnxEntriesToVersion(string version)
    {
        bool changed = false;
        changed |= PinInFlat(GetClaudeDesktopConfigPath(), "mcpServers", version);
        changed |= PinInFlat(GetClaudeCodeConfigPath(), "mcpServers", version);

        string? vsCodePath = GetVsCodeSettingsPath();
        if (vsCodePath is not null)
            changed |= PinInVsCode(vsCodePath, version);

        changed |= PinInFlat(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mcp.json"),
            "servers", version);

        return changed;
    }

    internal static bool PinInFlat(string path, string serversKey, string version)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var root = LoadOrCreate(path);
            if (root[serversKey] is not JsonObject servers) return false;
            if (servers[ServerName] is not JsonObject entry) return false;
            if (!IsDnxEntry(entry) || !SetPinnedVersion(entry, version)) return false;

            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] pinned {version} in {path}");
            return true;
        }
        catch { return false; }
    }

    internal static bool PinInVsCode(string path, string version)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var root = LoadOrCreate(path);
            if (root["mcp"] is not JsonObject mcp) return false;
            if (mcp["servers"] is not JsonObject servers) return false;
            if (servers[ServerName] is not JsonObject entry) return false;
            if (!IsDnxEntry(entry) || !SetPinnedVersion(entry, version)) return false;

            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] pinned {version} in {path}");
            return true;
        }
        catch { return false; }
    }

    static bool NeedsUrlUpdate(JsonObject entry)
    {
        if (entry["env"] is not JsonObject env) return true;
        return env["TOKENSAVER_API_URL"]?.GetValue<string>() != ApiUrl;
    }

    static void ApplyUrl(JsonObject entry)
    {
        if (entry["env"] is not JsonObject env)
        {
            env = [];
            entry["env"] = env;
        }
        env["TOKENSAVER_API_URL"] = ApiUrl;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    static JsonObject LoadOrCreate(string path)
    {
        if (!File.Exists(path))
            return [];

        string text = File.ReadAllText(path).Trim();
        if (text.Length == 0)
            return [];

        var docOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        return JsonNode.Parse(text, nodeOptions: null, documentOptions: docOptions) as JsonObject
            ?? throw new InvalidOperationException("Config file root is not a JSON object.");
    }

    static void Save(string path, JsonObject root)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
