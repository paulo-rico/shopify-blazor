namespace Polaris.Blazor;

/// <summary>Padding inside an <c>s-section</c>.</summary>
public enum PolarisSectionPadding
{
    /// <summary>Standard padding. The default.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>No padding, for a section whose child supplies its own, such as a table.</summary>
    [PolarisValue("none")]
    None,
}
