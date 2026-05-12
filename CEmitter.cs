namespace RoslynLean;

public sealed class CEmitter : ILanguageEmitter
{
    public string Language => "C";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".c", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".h", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = CppMinifier.StripAndCollapse(source);
        var notes = $"// C minify of {Path.GetFileName(filePath)}\n" +
                    $"// Comments stripped, whitespace collapsed, #directives preserved\n";
        return new LanguageEmitResult(Found: true, Output: output,
            OriginalChars: source.Length, OutputChars: output.Length, Notes: notes);
    }
}
