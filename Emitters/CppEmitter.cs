namespace RoslynLean;

public sealed class CppEmitter : ILanguageEmitter
{
    public string Language => "C++";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".cpp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cc",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cxx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".hpp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".hh",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".hxx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".inl", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = CppMinifier.StripAndCollapse(source);
        var notes = $"// C++ minify of {Path.GetFileName(filePath)} — comments stripped, whitespace collapsed, #directives preserved\n";
        return new LanguageEmitResult(Found: true, Output: output,
            OriginalChars: source.Length, OutputChars: output.Length, Notes: notes);
    }
}
