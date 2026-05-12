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

    internal static int Run(string[] args)
    {
        bool local = args.Contains("--local");
        bool claudeOnly = args.Contains("--claude-desktop");
        bool vsOnly = args.Contains("--vs");
        bool doAll = !claudeOnly && !vsOnly;

        int failures = 0;

        if (doAll || claudeOnly)
        {
            if (!RegisterClaudeDesktop())
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
            Console.WriteLine("\nDone. Restart your MCP host (Claude Desktop / Visual Studio) to pick up the change.");
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
        ["command"] = "tokensaver-mcp"
    };

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
        ["command"] = "tokensaver-mcp"
    };

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

        return JsonNode.Parse(text) as JsonObject
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
