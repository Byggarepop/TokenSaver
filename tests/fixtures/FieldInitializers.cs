namespace FieldSample;

public class FieldInitializers
{
    // Simple initializer
    private int _count = 0;

    // Long complex initializer — the noise we want to strip
    private readonly Dictionary<string, List<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Multiple declarators on one line
    private int _min = int.MinValue, _max = int.MaxValue;

    // String initializer
    private string _label = "default label value";

    // Const — should preserve keyword, drop initializer in signature
    private const int Limit = 100;

    public void Touch()
    {
        _ = _count;
        _ = _cache;
        _ = _min;
        _ = _max;
        _ = _label;
        _ = Limit;
    }
}
