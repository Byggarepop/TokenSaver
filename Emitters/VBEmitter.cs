namespace RoslynLean;

public sealed class VBEmitter : ILanguageEmitter
{
    public string Language => "VB.NET";

    public bool CanHandle(string filePath) =>
        Path.GetExtension(filePath).Equals(".vb", StringComparison.OrdinalIgnoreCase);

    public LanguageEmitResult Minify(string filePath)
    {
        var result = new VBFocusedEmitter(filePath).EmitMinified();
        return new LanguageEmitResult(
            Found: result.Found,
            Output: result.Output,
            OriginalChars: result.OriginalChars,
            OutputChars: result.FocusedChars,
            Notes: result.Notes);
    }
}
