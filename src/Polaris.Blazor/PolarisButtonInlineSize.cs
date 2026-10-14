namespace Polaris.Blazor;

/// <summary>Width behaviour of an <c>s-button</c>.</summary>
public enum PolarisButtonInlineSize
{
    /// <summary>Sized by its content.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Fills the available inline space.</summary>
    [PolarisValue("fill")]
    Fill,

    /// <summary>Sized to fit its content exactly.</summary>
    [PolarisValue("fit-content")]
    FitContent,
}
