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
        ["command"] = "tokensaver-mcp",
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
        ["command"] = "tokensaver-mcp",
        ["args"] = new JsonArray(),
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
        ["args"] = new JsonArray("tool", "execute", "TokenSaver.Mcp", "--yes"),
        ["env"] = new JsonObject { ["TOKENSAVER_API_URL"] = ApiUrl }
    };

    // -------------------------------------------------------------------------
    // Auto-update on startup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called on every MCP server startup. Silently updates the TOKENSAVER_API_URL
    /// env var in any already-registered config files if it is missing or stale.
    /// Gated by a version sentinel so the work only happens once per installed version.
    /// Logs to stderr only — stdout is reserved for JSON-RPC traffic.
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

        ClearVsCopilotMcpCache();

        try
        {
            Directory.CreateDirectory(TokenSaver.ReportWriter.DataDir);
            File.WriteAllText(sentinelPath, version);
        }
        catch { }
    }

    /// <summary>
    /// Deletes Visual Studio's cached MCP server metadata for this server.
    /// VS caches tool names/schemas keyed by server name and won't re-query
    /// the server until the cache is cleared or the server name changes.
    /// Called once per version upgrade from AutoUpdateRegistrations.
    /// </summary>
    static void ClearVsCopilotMcpCache()
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "VisualStudio", "Copilot", "McpServers");
            if (!Directory.Exists(cacheDir))
                return;
            foreach (var file in Directory.GetFiles(cacheDir, "*.cache"))
            {
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    if (bytes.AsSpan().IndexOf("tokensaver"u8) < 0)
                        continue;
                    File.Delete(file);
                    var msg = $"cleared VS MCP cache: {Path.GetFileName(file)}";
                    Console.Error.WriteLine($"[tokensaver] {msg}");
                    StartupLog.Write(msg);
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>Updates an existing tokensaver entry in a flat config (root → serversKey → ServerName).</summary>
    static void TryUpdateFlatConfig(string path, string serversKey)
    {
        if (!File.Exists(path)) return;
        try
        {
            var root = LoadOrCreate(path);
            if (root[serversKey] is not JsonObject servers) return;
            if (servers[ServerName] is not JsonObject entry) return;
            if (!NeedsUrlUpdate(entry)) return;

            ApplyUrl(entry);
            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] updated TOKENSAVER_API_URL in {path}");
        }
        catch { }
    }

    /// <summary>Updates an existing tokensaver entry in VS Code's nested config (root → mcp → servers → ServerName).</summary>
    static void TryUpdateVsCodeConfig(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var root = LoadOrCreate(path);
            if (root["mcp"] is not JsonObject mcp) return;
            if (mcp["servers"] is not JsonObject servers) return;
            if (servers[ServerName] is not JsonObject entry) return;
            if (!NeedsUrlUpdate(entry)) return;

            ApplyUrl(entry);
            Save(path, root);
            Console.Error.WriteLine($"[tokensaver] updated TOKENSAVER_API_URL in {path}");
        }
        catch { }
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
