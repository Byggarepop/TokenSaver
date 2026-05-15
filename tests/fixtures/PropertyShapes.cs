namespace PropertySample;

public class PropertyShapes
{
    // Full read-write property
    public string ReadWrite { get; set; } = "";

    // Get-only auto property
    public int GetOnly { get; }

    // Init-only property
    public string InitOnly { get; init; } = "";

    // Expression-bodied (computed, no setter possible)
    public string Computed => ReadWrite.ToUpper();

    // Protected setter
    public int ProtectedSet { get; protected set; }

    // Private setter
    public string PrivateSet { get; private set; } = "";

    public void Touch()
    {
        _ = ReadWrite;
        _ = GetOnly;
        _ = InitOnly;
        _ = Computed;
        _ = ProtectedSet;
        _ = PrivateSet;
    }
}
