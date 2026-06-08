namespace TypeMapFixture;

// A dedicated fixture for MapTypes kind/modifier/base-list coverage. Kept out of traversal/ so the
// exact-count assertions in the existing MapTypes tests there are not perturbed.

public enum Color { Red, Green, Blue }

internal struct Point { public int X; public int Y; }

public sealed record Money(decimal Amount, string Currency);

public abstract class Animal { }

public interface IWalk { }
public interface IRun { }

// Multiple base types: a base class plus two interfaces — exercises the comma-joined Bases string.
public sealed class Dog : Animal, IWalk, IRun { }

public static class Helpers { }
