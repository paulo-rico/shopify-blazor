namespace Polaris.Blazor;

/// <summary>How an <c>s-image</c> fits its box.</summary>
public enum PolarisImageObjectFit
{
    /// <summary>The whole image is visible, letterboxed if needed. The default.</summary>
    [PolarisValue("contain")]
    Contain,

    /// <summary>The box is filled, cropping the overflow.</summary>
    [PolarisValue("cover")]
    Cover,
}

/// <summary>When the browser fetches an <c>s-image</c>.</summary>
public enum PolarisImageLoading
{
    /// <summary>Fetch immediately. The default.</summary>
    [PolarisValue("eager")]
    Eager,

    /// <summary>Defer until the image is near the viewport.</summary>
    [PolarisValue("lazy")]
    Lazy,
}

/// <summary>How wide an <c>s-image</c> renders.</summary>
public enum PolarisImageInlineSize
{
    /// <summary>Sized by the source image, which ignores the aspect ratio. The default is fill.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Fills the available inline space, honouring the aspect ratio.</summary>
    [PolarisValue("fill")]
    Fill,
}

/// <summary>How assistive technology treats an <c>s-image</c>.</summary>
public enum PolarisImageAccessibilityRole
{
    /// <summary>Removed from the accessibility tree.</summary>
    [PolarisValue("none")]
    None,

    /// <summary>Decorative; announced as nothing.</summary>
    [PolarisValue("presentation")]
    Presentation,

    /// <summary>
    /// An image with an alternative description. The default.
    /// </summary>
    /// <remarks>Pair it with <c>Alt</c>; an <c>img</c> with no alternative text is announced poorly.</remarks>
    [PolarisValue("img")]
    Img,
}
