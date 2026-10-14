namespace Polaris.Blazor;

/// <summary>Size of an <c>s-spinner</c>.</summary>
public enum PolarisSpinnerSize
{
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
