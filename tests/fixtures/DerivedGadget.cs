namespace Fixtures;

// Derives from GadgetBase (declared in GadgetBase.cs) and implements IGadget.
// Configure is inherited from GadgetBase, so focusing THIS file for "Configure"
// must miss — and the NOT FOUND diagnostic should hint that the member may live
// on a base type and point at the file declaring it.
public sealed class DerivedGadget : GadgetBase, IGadget
{
    public override string Name => "derived";

    public void Reset() => Configure(0);
}
