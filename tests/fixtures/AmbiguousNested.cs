namespace Fixtures;

public sealed class AmbiguousNested
{
    private readonly int _state;

    public AmbiguousNested(int state) { _state = state; }

    public int Read() => Inner.Compute(_state);

    private sealed class Inner
    {
        private static int _state;

        public static int Compute(int x)
        {
            _state = x;
            return _state * 2;
        }
    }

    private sealed class Other
    {
        private static int _state;

        public static int Bump() => ++_state;
    }
}
