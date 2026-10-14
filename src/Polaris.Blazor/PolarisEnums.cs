namespace Polaris.Blazor;

/// <summary>Emphasis of a button or action.</summary>
public enum PolarisButtonVariant
{
    /// <summary>Let Shopify choose. The default.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>The single most important action on a screen.</summary>
    [PolarisValue("primary")]
    Primary,

    /// <summary>A supporting action.</summary>
    [PolarisValue("secondary")]
    Secondary,

    /// <summary>A low-emphasis action, or one inside a table row.</summary>
    [PolarisValue("tertiary")]
    Tertiary,
}

/// <summary>Semantic colour of a component.</summary>
public enum PolarisTone
{
    /// <summary>Let the component choose. The default.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Neutral emphasis.</summary>
    [PolarisValue("neutral")]
    Neutral,

    /// <summary>Informational.</summary>
    [PolarisValue("info")]
    Info,

    /// <summary>Successful or positive.</summary>
    [PolarisValue("success")]
    Success,

    /// <summary>Needs attention.</summary>
    [PolarisValue("warning")]
    Warning,

    /// <summary>Something failed.</summary>
    [PolarisValue("critical")]
    Critical,

    /// <summary>A softer warning.</summary>
    [PolarisValue("caution")]
    Caution,
}

/// <summary>Size of a component.</summary>
public enum PolarisSize
{
    /// <summary>The standard size.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Small.</summary>
    [PolarisValue("small")]
    Small,

    /// <summary>Large.</summary>
    [PolarisValue("large")]
    Large,

    /// <summary>Large, at the 100 step of the scale.</summary>
    [PolarisValue("large-100")]
    Large100,

    /// <summary>Small, at the 100 step of the scale.</summary>
    [PolarisValue("small-100")]
    Small100,

    /// <summary>Small, at the 200 step of the scale.</summary>
    [PolarisValue("small-200")]
    Small200,

    /// <summary>Large, at the 200 step of the scale.</summary>
    [PolarisValue("large-200")]
    Large200,

    /// <summary>Large, at the 300 step of the scale.</summary>
    [PolarisValue("large-300")]
    Large300,

    /// <summary>Large, at the 400 step of the scale.</summary>
    [PolarisValue("large-400")]
    Large400,
}

/// <summary>Spacing step used by the layout components.</summary>
public enum PolarisGap
{
    /// <summary>No gap.</summary>
    [PolarisValue("none")]
    None,

    /// <summary>The small step.</summary>
    [PolarisValue("small")]
    Small,

    /// <summary>The base step. The default for most components.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>The large step.</summary>
    [PolarisValue("large")]
    Large,
}

/// <summary>Axis a stack lays its children out along.</summary>
public enum PolarisStackDirection
{
    /// <summary>Vertical, top to bottom.</summary>
    [PolarisValue("block")]
    Block,

    /// <summary>Horizontal, inline.</summary>
    [PolarisValue("inline")]
    Inline,
}

/// <summary>Direction of an <c>s-divider</c>.</summary>
/// <remarks>
/// Shopify's names describe the axis the divider <em>spans</em>, not the axis it separates, so
/// <c>Inline</c> is a horizontal rule. The component's own default is <c>Inline</c>.
/// </remarks>
public enum PolarisDividerDirection
{
    /// <summary>A horizontal rule between stacked blocks. The component default.</summary>
    [PolarisValue("inline")]
    Inline,

    /// <summary>A vertical rule between items laid out horizontally.</summary>
    [PolarisValue("block")]
    Block,
}

/// <summary>Width of an <c>s-page</c>.</summary>
public enum PolarisPageInlineSize
{
    /// <summary>Narrow, for focused forms.</summary>
    [PolarisValue("small")]
    Small,

    /// <summary>The default. Required for the <c>aside</c> slot to render.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Wide, for data-heavy screens.</summary>
    [PolarisValue("large")]
    Large,
}

/// <summary>Alignment of a table column, and of the cell content beneath it.</summary>
public enum PolarisTableHeaderFormat
{
    /// <summary>Left aligned. The default.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Right aligned, for counts.</summary>
    [PolarisValue("numeric")]
    Numeric,

    /// <summary>Right aligned, for money.</summary>
    [PolarisValue("currency")]
    Currency,
}

/// <summary>
/// How a table column appears when the table collapses to a list on a narrow screen.
/// </summary>
public enum PolarisTableListSlot
{
    /// <summary>Shown with its header text. The default.</summary>
    [PolarisValue("labeled")]
    Labeled,

    /// <summary>The row's headline. Use for the most identifying column, usually the name.</summary>
    [PolarisValue("primary")]
    Primary,

    /// <summary>Secondary detail.</summary>
    [PolarisValue("secondary")]
    Secondary,

    /// <summary>Leading metadata, such as a status.</summary>
    [PolarisValue("kicker")]
    Kicker,

    /// <summary>Rendered on the same line as the primary value.</summary>
    [PolarisValue("inline")]
    Inline,
}

/// <summary>Weight of text or a heading.</summary>
public enum PolarisFontWeight
{
    /// <summary>Let the component choose.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Normal weight.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Medium weight.</summary>
    [PolarisValue("medium")]
    Medium,

    /// <summary>Semi-bold.</summary>
    [PolarisValue("semibold")]
    Semibold,

    /// <summary>Bold.</summary>
    [PolarisValue("bold")]
    Bold,
}

/// <summary>Size of text, a heading or a number.</summary>
public enum PolarisFontSize
{
    /// <summary>Let the component choose.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>Small, at the 200 step.</summary>
    [PolarisValue("small-200")]
    Small200,

    /// <summary>Small, at the 100 step.</summary>
    [PolarisValue("small-100")]
    Small100,

    /// <summary>Small.</summary>
    [PolarisValue("small")]
    Small,

    /// <summary>The standard size.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>Large.</summary>
    [PolarisValue("large")]
    Large,

    /// <summary>Large, at the 100 step.</summary>
    [PolarisValue("large-100")]
    Large100,
}

/// <summary>Type of an <c>s-text</c> run.</summary>
public enum PolarisTextType
{
    /// <summary>Ordinary inline text.</summary>
    [PolarisValue("generic")]
    Generic,

    /// <summary>Emphasised, as if bold.</summary>
    [PolarisValue("strong")]
    Strong,

    /// <summary>Address text.</summary>
    [PolarisValue("address")]
    Address,

    /// <summary>Text that repeats information already conveyed, for screen readers.</summary>
    [PolarisValue("redundant")]
    Redundant,
}

/// <summary>Colour of an icon or text.</summary>
public enum PolarisColor
{
    /// <summary>Standard contrast.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>De-emphasised.</summary>
    [PolarisValue("subdued")]
    Subdued,

    /// <summary>High contrast.</summary>
    [PolarisValue("strong")]
    Strong,
}

/// <summary>How a value is hidden from, or exposed to, assistive technology.</summary>
public enum PolarisAccessibilityVisibility
{
    /// <summary>Visible to everyone.</summary>
    [PolarisValue("visible")]
    Visible,

    /// <summary>Hidden from sighted users, still announced.</summary>
    [PolarisValue("exclusive")]
    Exclusive,

    /// <summary>Hidden from assistive technology as well.</summary>
    [PolarisValue("hidden")]
    Hidden,
}

/// <summary>Type of a button, controlling form submission.</summary>
public enum PolarisButtonType
{
    /// <summary>No form behaviour. The default.</summary>
    [PolarisValue("button")]
    Button,

    /// <summary>Submits the closest form.</summary>
    [PolarisValue("submit")]
    Submit,

    /// <summary>Resets the closest form.</summary>
    [PolarisValue("reset")]
    Reset,
}

/// <summary>Browsing context a link or button opens.</summary>
public enum PolarisLinkTarget
{
    /// <summary>Decided by the component.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>A new tab.</summary>
    [PolarisValue("_blank")]
    Blank,

    /// <summary>The current context.</summary>
    [PolarisValue("_self")]
    Self,

    /// <summary>The parent frame.</summary>
    [PolarisValue("_parent")]
    Parent,

    /// <summary>The top-level frame.</summary>
    [PolarisValue("_top")]
    Top,
}

/// <summary>A declarative command a button sends to another element.</summary>
/// <remarks>
/// This is how a Polaris modal, popover or menu is opened without any JavaScript: give the target
/// an id, then set <c>CommandFor</c> on the trigger.
/// </remarks>
public enum PolarisCommand
{
    /// <summary>The component's own default behaviour, usually toggle.</summary>
    [PolarisValue("--auto")]
    Auto,

    /// <summary>Show the target.</summary>
    [PolarisValue("--show")]
    Show,

    /// <summary>Hide the target.</summary>
    [PolarisValue("--hide")]
    Hide,

    /// <summary>Toggle the target.</summary>
    [PolarisValue("--toggle")]
    Toggle,

    /// <summary>Copy the target's value.</summary>
    [PolarisValue("--copy")]
    Copy,
}
