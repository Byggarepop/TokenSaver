// Replacement Program.cs for the RoslynLean CLI.
// Always shows the token comparison — no opt-in --stats flag needed.

using RoslynLean;
using TokenStats;

if (args.Length < 2 || args[0] != "focus")
{
    Console.Error.WriteLine(
        """
        roslyn-lean — emit a focused subset of a C# file for LLM consumption.
        
        USAGE:
          roslyn-lean focus <path-to-file.cs> --method=<MethodName>
          roslyn-lean focus <path-to-file.cs> --method=<MethodName> --quiet
        
        BY DEFAULT:
          The focused source goes to stdout.
          Token comparison goes to stderr — visible to you, not piped to clipboard.
          Pass --quiet to suppress the comparison.
        """);
    return 1;
}

var sourcePath = args[1];
var methodName = args.Skip(2)
    .FirstOrDefault(a => a.StartsWith("--method="))
    ?.Substring("--method=".Length);
var quiet = args.Contains("--quiet");

if (string.IsNullOrEmpty(methodName))
{
    Console.Error.WriteLine("Missing --method=<name>");
    return 1;
}

try
{
    var emitter = new FocusedEmitter(sourcePath);
    var result = emitter.Emit(methodName);

    if (!result.Found)
    {
        Console.Error.WriteLine(result.Output);
        return 2;
    }

    // Source code → stdout (so users can pipe to clip / pbcopy / a file)
    Console.Write(result.Notes);
    Console.WriteLine();
    Console.Write(result.Output);

    // Stats → stderr by default. Visible in the terminal, doesn't get piped.
    if (!quiet)
    {
        var report = new TokenReport(
            ToolName: "Focused Emitter",
            TokensWithoutTool: result.OriginalTokensEstimate,
            TokensWithTool: result.FocusedTokensEstimate,
            Notes: $"Focus method: {result.FocusMethodName}. Other members: signatures only.");

        Console.Error.WriteLine();
        Console.Error.WriteLine(report.DetailedBlock());
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 3;
}
