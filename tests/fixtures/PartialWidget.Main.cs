namespace Fixtures;

// Part one of a partial type. The Render method lives in the sibling file
// PartialWidget.Render.cs, so a focus_method call against THIS file for "Render"
// must miss — and the NOT FOUND diagnostic should hint that the type is partial
// and the method may live in a sibling file.
public partial class PartialWidget
{
    private int _renderCount;

    public int RenderCount => _renderCount;

    public void Reset() => _renderCount = 0;
}
