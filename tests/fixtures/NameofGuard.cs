namespace Fixtures;

public sealed class NameofGuard
{
    private int _counter;

    public string Describe()
    {
        // The literal "_counter" inside nameof must survive the alias rewrite verbatim.
        return $"field={nameof(_counter)} value={_counter}";
    }

    private void Touch() => _counter++;
}
