namespace Fixtures;

public sealed class PrivatePropConsumer
{
    private readonly int _factor;

    public PrivatePropConsumer(int factor) { _factor = factor; }

    // Accesses the private Scaled property as an identifier (not a receiver),
    // which triggers depth=1 private-property-body expansion.
    public int Compute(int x)
    {
        return x + Scaled;
    }

    private int Scaled => _factor * 10;
}
