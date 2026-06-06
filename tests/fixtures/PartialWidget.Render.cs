namespace Fixtures;

// Part two of the same partial type. Render genuinely belongs to PartialWidget,
// it just lives in a different file than PartialWidget.Main.cs — exactly the case
// the partial NOT FOUND hint is meant to cover.
public partial class PartialWidget
{
    public string Render()
    {
        _renderCount++;
        return $"<widget count=\"{_renderCount}\" />";
    }
}
