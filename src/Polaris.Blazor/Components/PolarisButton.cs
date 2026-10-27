using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps Shopify's <c>&lt;s-button&gt;</c>.</summary>
public sealed class PolarisButton : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-button";

    /// <summary>Emphasis of the button.</summary>
    [Parameter] public PolarisButtonVariant Variant { get; set; } = PolarisButtonVariant.Auto;

    /// <summary>Semantic colour, usually <see cref="PolarisTone.Critical"/> for a destructive action.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Name of a predefined admin icon, rendered before the label.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Prevents interaction. A disabled button also leaves the tab order.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Shows the button's own loading state.
    /// </summary>
    /// <remarks>
    /// Polaris documents that this gives feedback but does <em>not</em> debounce or block repeat
    /// clicks. Guard against double submission in C# as well.
    /// </remarks>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Form behaviour: submit or reset the closest form.</summary>
    [Parameter] public PolarisButtonType Type { get; set; } = PolarisButtonType.Button;

    /// <summary>Renders the button as a link. Shopify may block external URLs inside the Admin.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Browsing context for <see cref="Href"/>.</summary>
    [Parameter] public PolarisLinkTarget Target { get; set; } = PolarisLinkTarget.Auto;

    /// <summary>Width behaviour of the button.</summary>
    [Parameter] public PolarisButtonInlineSize InlineSize { get; set; } = PolarisButtonInlineSize.Auto;

    /// <summary>
    /// Id of the element this button controls: the declarative way to open a modal or popover with
    /// no JavaScript at all.
    /// </summary>
    [Parameter] public string? CommandFor { get; set; }

    /// <summary>The command sent to <see cref="CommandFor"/>.</summary>
    [Parameter] public PolarisCommand Command { get; set; } = PolarisCommand.Auto;

    /// <summary>Id of a tooltip or popover shown when the button is hovered or focused.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>
    /// Announces to assistive technology whether what this button controls is open, as
    /// <c>aria-expanded</c> on the <c>&lt;button&gt;</c> Polaris renders inside its shadow root.
    /// Leave it <c>null</c> for a button that controls nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a DOM <em>property</em>, not an attribute. Polaris does not reflect it, so an
    /// attribute would freeze the state at first paint; and the element that carries the semantics
    /// is the shadow button, which no light-DOM attribute can reach — an <c>aria-expanded</c>
    /// attribute on the <c>&lt;s-button&gt;</c> host lands on an element with no role and is ignored.
    /// </para>
    /// <para>
    /// It is also one of the few Polaris properties that does not schedule a render when it changes:
    /// it is read by the render function and nothing else, so writing it alone changes nothing that
    /// is visible to assistive technology. The interop layer therefore asks the element to render
    /// after writing it — see <c>polaris-interop.js</c>.
    /// </para>
    /// <para>
    /// Polaris ignores it when <see cref="CommandFor"/> is set: there, <c>aria-expanded</c> follows
    /// the open state of the target instead, and the value here is dropped with a console warning.
    /// </para>
    /// </remarks>
    [Parameter] public PolarisAccessibilityExpanded? AccessibilityExpanded { get; set; }

    /// <summary>Raised on click. Modifier keys are on the event args.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnClick { get; set; }

    /// <summary>Raised when the button loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <summary>Raised when the button receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>
    /// Raised on every key press while the button has focus, with the key's name in
    /// <see cref="PolarisEventArgs.Key"/>.
    /// </summary>
    /// <remarks>
    /// This is a DOM keyboard event forwarded from the button's own element, so it is how a row in a
    /// list of buttons can follow the arrow keys — Enter and Space already click the button natively
    /// and do not need handling.
    /// </remarks>
    [Parameter] public EventCallback<PolarisEventArgs> OnKeyDown { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Click, OnClick);
        events.On(PolarisEvents.Blur, OnBlur);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.KeyDown, OnKeyDown);
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => AccessibilityExpanded is null
        ? EmptyPropertySet
        : new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            // The element's own spelling ("expanded" / "collapsed"), not a boolean: Polaris reads
            // this string and derives aria-expanded from it, and a bool is read as collapsed.
            ["accessibilityExpanded"] = PolarisAttributeValue.EnumValue(AccessibilityExpanded.Value),
        };

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["variant"] = PolarisAttributeValue.EnumValue(Variant);
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["icon"] = Icon;
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["loading"] = PolarisAttributeValue.Format(Loading);
        attributes["type"] = PolarisAttributeValue.EnumValue(Type);
        attributes["href"] = Href;
        attributes["target"] = Href is null ? null : PolarisAttributeValue.EnumValue(Target);
        attributes["inlineSize"] = PolarisAttributeValue.EnumValue(InlineSize);
        attributes["commandFor"] = CommandFor;
        attributes["command"] = CommandFor is null ? null : PolarisAttributeValue.EnumValue(Command);
        attributes["interestFor"] = InterestFor;
    }
}
