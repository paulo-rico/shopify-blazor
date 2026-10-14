namespace Polaris.Blazor;

/// <summary>Padding inside an <c>s-modal</c>.</summary>
public enum PolarisModalPadding
{
    /// <summary>Standard padding. The default.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>No padding, for a modal that supplies its own layout.</summary>
    [PolarisValue("none")]
    None,
}

/// <summary>Width of an <c>s-modal</c>.</summary>
public enum PolarisModalSize
{
    /// <summary>Small.</summary>
    [PolarisValue("small")]
    Small,

    /// <summary>Small, at the 100 step.</summary>
    [PolarisValue("small-100")]
    Small100,

    /// <summary>The default.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Large.</summary>
    [PolarisValue("large")]
    Large,

    /// <summary>Large, at the 100 step.</summary>
    [PolarisValue("large-100")]
    Large100,
}
