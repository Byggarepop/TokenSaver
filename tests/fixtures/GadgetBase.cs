namespace Fixtures;

public interface IGadget
{
    string Name { get; }
}

// Base type that actually declares Configure. A focus_method call for "Configure"
// against DerivedGadget.cs must miss — the member is inherited from here, in a
// different file — and the NOT FOUND diagnostic should hint at this base type.
public abstract class GadgetBase : IGadget
{
    public abstract string Name { get; }

    public void Configure(int channel)
    {
        Channel = channel;
    }

    protected int Channel { get; private set; }
}
