namespace Polaris.Blazor;

/// <summary>Direction of text, as the HTML <c>dir</c> attribute.</summary>
/// <remarks>
/// The unset state is the component's own default (an empty <c>dir</c>), which is why this is
/// nullable everywhere it appears rather than having a member for it.
/// </remarks>
public enum PolarisTextDirection
{
    /// <summary>Decided by the browser from the content.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Left to right.</summary>
    [PolarisValue("ltr")]
    Ltr,

    /// <summary>Right to left.</summary>
    [PolarisValue("rtl")]
    Rtl,
}
