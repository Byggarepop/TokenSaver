namespace RoslynLean;

public sealed record LanguageEmitResult(
    bool Found,
    string Output,
    int OriginalChars,
    int OutputChars,
    string Notes)
{
    public int CharsSaved => Math.Max(0, OriginalChars - OutputChars);
    public double ReductionPercent =>
        OriginalChars == 0 ? 0 : (double)CharsSaved / OriginalChars * 100;
    public int OriginalTokensEstimate => Math.Max(1, OriginalChars / 4);
    public int OutputTokensEstimate   => Math.Max(1, OutputChars / 4);
}

public interface ILanguageEmitter
{
    string Language { get; }
    bool CanHandle(string filePath);
    LanguageEmitResult Minify(string filePath);
}

public static class LanguageEmitterRegistry
{
    private static readonly List<ILanguageEmitter> _emitters = new()
    {
        new CSharpEmitter(),
        new TypeScriptEmitter(),
        new JavaScriptEmitter(),
        new PythonEmitter(),
    };

    public static ILanguageEmitter? Find(string filePath) =>
        _emitters.FirstOrDefault(e => e.CanHandle(filePath));
}
