namespace RoslynLean;

/// <summary>
/// JS minifier — delegates lexical work to <see cref="JsLikeMinifier"/>.
/// Handles .js, .mjs, .cjs, .jsx.
///
/// Known limitations:
/// - Regex literals are NOT specially recognized. A regex containing // or
///   /* could confuse the comment stripper. Rare in practice.
/// - JSX is treated lexically (comments/strings stripped/preserved); the
///   JSX-specific {} expression boundaries are not parsed.
/// </summary>
public sealed class JavaScriptEmitter : ILanguageEmitter
{
    public string Language => "JavaScript";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".js",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cjs", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jsx", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = JsLikeMinifier.StripAndCollapse(source);
        var notes =
            $"// JS minify of {Path.GetFileName(filePath)} — comments stripped, whitespace collapsed (POC — no regex-literal awareness)\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }
}
