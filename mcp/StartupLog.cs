namespace TokenSaver.Mcp;

internal static class StartupLog
{
    static readonly string _path = Path.Combine(
        TokenSaver.ReportWriter.DataDir, "mcp.log");

    internal static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(TokenSaver.ReportWriter.DataDir);
            if (!File.Exists(_path))
                return;

            var lines = File.ReadAllLines(_path);
            if (lines.Length > 1000)
                File.WriteAllLines(_path, lines[^500..]);
        }
        catch { }
    }

    internal static void Write(string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z pid={Environment.ProcessId} {message}";
            File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch { }
    }
}
