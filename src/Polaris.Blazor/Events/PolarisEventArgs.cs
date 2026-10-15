using System.Text.Json.Serialization;

namespace Polaris.Blazor.Events;

/// <summary>
/// An event raised by a Polaris web component, normalized for .NET.
/// </summary>
/// <remarks>
/// Every field is nullable because the shape of a DOM event depends on the component and on the
/// event: <c>click</c> carries no value, <c>input</c> carries a string, and a custom event such
/// as <c>dismiss</c> carries neither. Check what the component you are wrapping actually sends
/// rather than assuming.
/// </remarks>
public sealed class PolarisEventArgs
{
    /// <summary>The DOM event name, for example <c>click</c>, <c>input</c>, <c>change</c>, <c>dismiss</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The field value as a string, for the form components. Polaris form values are always
    /// strings, even for numeric fields.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Selected values for a multi-select component such as <c>s-choice-list</c>.</summary>
    [JsonPropertyName("values")]
    public string[]? Values { get; set; }

    /// <summary>Checked state, for <c>s-checkbox</c> and <c>s-switch</c>.</summary>
    [JsonPropertyName("checked")]
    public bool? Checked { get; set; }

    /// <summary>The custom event's <c>detail</c> payload, with non-serializable members removed.</summary>
    [JsonPropertyName("detail")]
    public Dictionary<string, object>? Detail { get; set; }

    /// <summary>The key pressed, for keyboard-driven events.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>True when Alt was held.</summary>
    [JsonPropertyName("altKey")]
    public bool AltKey { get; set; }

    /// <summary>True when Ctrl was held.</summary>
    [JsonPropertyName("ctrlKey")]
    public bool CtrlKey { get; set; }

    /// <summary>True when the platform meta key was held.</summary>
    [JsonPropertyName("metaKey")]
    public bool MetaKey { get; set; }

    /// <summary>True when Shift was held.</summary>
    [JsonPropertyName("shiftKey")]
    public bool ShiftKey { get; set; }
}

/// <summary>Event name constants, so a component and its test agree on the spelling.</summary>
public static class PolarisEvents
{
    /// <summary><c>click</c> — raised by buttons, links and clickable surfaces.</summary>
    public const string Click = "click";

    /// <summary><c>input</c> — raised on every keystroke by the form fields.</summary>
    public const string Input = "input";

    /// <summary><c>change</c> — raised when a value is committed (blur or Enter, immediately for checkbox and select).</summary>
    public const string Change = "change";

    /// <summary><c>focus</c>.</summary>
    public const string Focus = "focus";

    /// <summary><c>blur</c>.</summary>
    public const string Blur = "blur";

    /// <summary><c>dismiss</c> — raised by <c>s-banner</c> when the merchant closes it.</summary>
    public const string Dismiss = "dismiss";

    /// <summary><c>show</c> — raised by <c>s-modal</c> and <c>s-popover</c>.</summary>
    public const string Show = "show";

    /// <summary><c>hide</c> — raised by <c>s-modal</c> and <c>s-popover</c>.</summary>
    public const string Hide = "hide";

    /// <summary><c>remove</c> — raised by removable chips.</summary>
    public const string Remove = "remove";

    /// <summary><c>nextpage</c> — raised by a paginated <c>s-table</c>.</summary>
    public const string NextPage = "nextpage";

    /// <summary><c>previouspage</c> — raised by a paginated <c>s-table</c>.</summary>
    public const string PreviousPage = "previouspage";

    /// <summary><c>afterhide</c> — raised once an overlay or a dismissible element has finished hiding.</summary>
    public const string AfterHide = "afterhide";

    /// <summary><c>load</c> — raised by <c>s-image</c>, <c>s-avatar</c> and <c>s-thumbnail</c> when the resource loads.</summary>
    public const string Load = "load";

    /// <summary>
    /// <c>error</c> — raised by <c>s-image</c>, <c>s-avatar</c> and <c>s-thumbnail</c> when the
    /// resource fails to load.
    /// </summary>
    public const string Error = "error";

    /// <summary><c>invalid</c> — raised by <c>s-date-field</c> when the entered value is not a valid date.</summary>
    public const string Invalid = "invalid";

    /// <summary><c>viewchange</c> — raised by the date components when the visible month changes.</summary>
    public const string ViewChange = "viewchange";

    /// <summary><c>droprejected</c> — raised by <c>s-drop-zone</c> when files do not match <c>accept</c>.</summary>
    public const string DropRejected = "droprejected";
}
