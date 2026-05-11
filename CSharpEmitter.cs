namespace RoslynLean;

/// <summary>
/// C# / Razor minifier — thin <see cref="ILanguageEmitter"/> adapter over
/// <see cref="FocusedEmitter.EmitMinified"/>. Routes .cs, .razor.cs, and
/// .razor files through Roslyn's syntax-tree-based lossless minify.
///
/// The interface only exposes Minify. Focus and alias remain on their own
/// MCP tools — they require Roslyn's SemanticModel, which no other language
/// emitter has.
/// </summary>
public sealed class CSharpEmitter : ILanguageEmitter
{
    public string Language => "C#";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".cs",    StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".razor", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        var r = new FocusedEmitter(filePath).EmitMinified();
        return new LanguageEmitResult(
            Found: r.Found,
            Output: r.Output,
            OriginalChars: r.OriginalChars,
            OutputChars: r.FocusedChars,
            Notes: r.Notes);
    }
}
