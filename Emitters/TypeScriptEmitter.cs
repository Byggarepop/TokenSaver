namespace RoslynLean;

/// <summary>
/// TypeScript minifier. Same lexical rules as JavaScript (TS is a superset),
/// so this shares <see cref="JsLikeMinifier"/>. A future parser-backed
/// implementation could drop type-only constructs entirely (interface decls,
/// type aliases, type-only imports) for further savings.
/// </summary>
public sealed class TypeScriptEmitter : ILanguageEmitter
{
    public string Language => "TypeScript";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".ts",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tsx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mts", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cts", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = JsLikeMinifier.StripAndCollapse(source);
        var notes =
            $"// TS minify of {Path.GetFileName(filePath)} — comments stripped, whitespace collapsed (POC — lexical only, type-only decls not removed)\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }
}
