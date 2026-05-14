namespace RegionSample;

/// <summary>A class that uses #region directives heavily, as seen in legacy enterprise code.</summary>
public class RegionHeavy
{
    #region Fields

    private int _value;
    private string _name = "default";

    #endregion

    #region Constructor

    public RegionHeavy(int value, string name)
    {
        _value = value;
        _name = name;
    }

    #endregion

    #region Public API

    /// <summary>Returns the stored value doubled.</summary>
    public int Double() => _value * 2;

    /// <summary>Returns the name in upper case.</summary>
    public string UpperName() => _name.ToUpper();

    #endregion

    #region Private helpers

    private int Add(int a, int b) => a + b;

    #endregion
}
