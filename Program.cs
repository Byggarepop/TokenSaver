using RoslynLean;

if (args.Length < 2 || args[0] != "focus")
{
    PrintUsage();
    return 1;
}

var csharpFilePath = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--csharpfile="))
    ?.Substring("--csharpfile=".Length);

var methodName = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--method="))
    ?.Substring("--method=".Length);

var showStats = args.Contains("--stats");
var aliasMode = args.Contains("--alias");
var minifyFlag = args.Contains("--minify");
var depthArg = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--depth="))
    ?.Substring("--depth=".Length);
var depth = int.TryParse(depthArg, out var d) ? Math.Max(0, d) : 0;

// Positional path is whatever non-flag arg sits after "focus"
var positionalPath = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));

string sourcePath;
bool minifyMode;

if (!string.IsNullOrEmpty(csharpFilePath))
{
    sourcePath = csharpFilePath;
    minifyMode = true;
}
else if (!string.IsNullOrEmpty(methodName) && !string.IsNullOrEmpty(positionalPath))
{
    sourcePath = positionalPath;
    minifyMode = false;
}
else
{
    PrintUsage();
    return 1;
}

try
{
    var emitter = new FocusedEmitter(sourcePath);
    var result = minifyMode
        ? (aliasMode ? emitter.EmitAliased() : emitter.EmitMinified())
        : emitter.Emit(methodName!, depth);

    if (!result.Found)
    {
        Console.Error.WriteLine(result.Output);
        return 2;
    }

    // --minify post-processor for --method mode (no-op for --csharpfile, already minified)
    if (minifyFlag && !minifyMode)
    {
        var minified = FocusedEmitter.MinifyText(result.Output);
        result = result with { Output = minified, FocusedChars = minified.Length };
    }

    Console.Write(result.Notes);
    Console.WriteLine();
    Console.Write(result.Output);

    if (showStats)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Original:  {result.OriginalChars,6} chars  (~{result.OriginalTokensEstimate} tokens)");
        Console.Error.WriteLine($"Focused:   {result.FocusedChars,6} chars  (~{result.FocusedTokensEstimate} tokens)");
        Console.Error.WriteLine($"Reduction: {result.ReductionPercent:F1}%");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 3;
}

static void PrintUsage() => Console.Error.WriteLine(
    """
    roslyn-lean — emit a token-reduced view of a C# file for LLM consumption.

    USAGE:
      roslyn-lean focus <path-to-file.cs> --method=<MethodName> [--stats]
      roslyn-lean focus --csharpfile=<path-to-file.cs> [--stats]

    --method=<Name>   Focused-method mode: emits the named method with full body,
                      every other member of its type reduced to a signature.
                      Best when you know which method the AI should reason about.

    --csharpfile=<P>  Lossless minify mode: strips comments, XML docs, and extra
                      whitespace from the whole file. Logic is preserved verbatim
                      (Roslyn parses and re-emits the syntax tree).
                      Best when the AI needs the full file but you want fewer tokens.

    --minify          (with --method) Strip comments and collapse whitespace
                      from the focused output. Lossless, same transform as
                      --csharpfile uses by default. No-op with --csharpfile.

    --depth=<N>       (with --method) Also include the FULL BODIES of private
                      helper methods called from the focus method, up to N
                      transitive levels. Default 0 (signatures only).
                      Use 1 for "translate this method" / refactor tasks where
                      the AI needs to see what helpers actually do, not guess.

    --alias           (with --csharpfile) Also rename PRIVATE methods/properties/
                      fields/events to short codes (M1, P1, F1, E1...). A symbol
                      ledger is prepended so the LLM can map back. Public API is
                      left alone — we can't see callers from a single-file view.

    --stats           Print before/after token estimate to stderr.

    OUTPUT:
      The transformed source goes to stdout. Stats (if --stats) go to stderr.
    """);
