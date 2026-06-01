namespace RoslynLean;

public sealed class XppEmitter : ILanguageEmitter
{
    public string Language => "X++";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".xpp", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = XppMinifier.StripAndCollapse(source);
        var notes = $"// X++ minify of {Path.GetFileName(filePath)}\n" +
                    $"// Comments stripped, whitespace collapsed, #macro directives preserved\n";
        return new LanguageEmitResult(Found: true, Output: output,
            OriginalChars: source.Length, OutputChars: output.Length, Notes: notes);
    }
}
