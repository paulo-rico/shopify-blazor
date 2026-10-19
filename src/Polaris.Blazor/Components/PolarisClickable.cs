using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-clickable&gt;</c>, a custom interactive surface.</summary>
/// <remarks>
/// <para>
/// This exists for an interaction that neither a button nor a link can express — usually a whole
/// card that is the target. Prefer a <see cref="PolarisButton"/> for an action and a
/// <see cref="PolarisLink"/> for navigation: both bring the keyboard and screen-reader behaviour
/// that a bare clickable surface does not.
/// </para>
/// <para>
/// Because it is a generic element, assistive technology sees nothing interactive unless
/// <c>AccessibilityRole</c> is set, and it is not focusable by default. Anything a merchant must be
/// able to reach by keyboard should not be built on this.
/// </para>
/// <para>
/// It takes child content like any other container. It was originally a bare element wrapper, which
/// meant content written inside it had nowhere to go — a surface with no content, and nothing to say
/// so.
/// </para>
/// </remarks>
public sealed class PolarisClickable : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-clickable";

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows a loading state. It does not block repeat clicks, so guard in C# as well.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Renders the surface as a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Browsing context for <see cref="Href"/>.</summary>
    [Parameter] public PolarisLinkTarget Target { get; set; } = PolarisLinkTarget.Auto;

    /// <summary>Marks the target as a download. Unreliable cross-origin.</summary>
    [Parameter] public string? Download { get; set; }

    /// <summary>
    /// Form behaviour when the surface is inside a form.
    /// </summary>
    /// <remarks>Ignored by Polaris once <see cref="Href"/> or <see cref="CommandFor"/> is set.</remarks>
    [Parameter] public PolarisButtonType Type { get; set; } = PolarisButtonType.Button;

    /// <summary>ARIA role to give the surface, for example <c>button</c>. Without one it is not announced as interactive.</summary>
    [Parameter] public string? AccessibilityRole { get; set; }

    /// <summary>How assistive technology and sighted users see the surface.</summary>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <summary>Id of the element this surface controls, for a declarative command.</summary>
    [Parameter] public string? CommandFor { get; set; }

    /// <summary>The command sent to <see cref="CommandFor"/>.</summary>
    [Parameter] public PolarisCommand Command { get; set; } = PolarisCommand.Auto;

    /// <summary>Id of a tooltip or popover shown when the surface is hovered or focused.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>Background treatment, for example <c>subdued</c> or <c>transparent</c>.</summary>
    [Parameter] public string? Background { get; set; }

    /// <summary>Padding on all edges, in the 1-to-4-value shorthand.</summary>
    [Parameter] public string? Padding { get; set; }

    /// <summary>Block-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingBlock { get; set; }

    /// <summary>Block-start padding, overriding <see cref="PaddingBlock"/>.</summary>
    [Parameter] public string? PaddingBlockStart { get; set; }

    /// <summary>Block-end padding, overriding <see cref="PaddingBlock"/>.</summary>
    [Parameter] public string? PaddingBlockEnd { get; set; }

    /// <summary>Inline-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingInline { get; set; }

    /// <summary>Inline-start padding, overriding <see cref="PaddingInline"/>.</summary>
    [Parameter] public string? PaddingInlineStart { get; set; }

    /// <summary>Inline-end padding, overriding <see cref="PaddingInline"/>.</summary>
    [Parameter] public string? PaddingInlineEnd { get; set; }

    /// <summary>Border shorthand: width, then colour, then style.</summary>
    [Parameter] public string? Border { get; set; }

    /// <summary>Border width, overriding <see cref="Border"/>. Drawn from the small-to-large scale, not the full size scale.</summary>
    [Parameter] public string? BorderWidth { get; set; }

    /// <summary>Border style, overriding <see cref="Border"/>.</summary>
    [Parameter] public string? BorderStyle { get; set; }

    /// <summary>Border colour, overriding <see cref="Border"/>.</summary>
    [Parameter] public string? BorderColor { get; set; }

    /// <summary>Corner radius, in the 1-to-4-value shorthand.</summary>
    [Parameter] public string? BorderRadius { get; set; }

    /// <summary>Block size.</summary>
    [Parameter] public string? BlockSize { get; set; }

    /// <summary>Minimum block size.</summary>
    [Parameter] public string? MinBlockSize { get; set; }

    /// <summary>Maximum block size.</summary>
    [Parameter] public string? MaxBlockSize { get; set; }

    /// <summary>Inline size.</summary>
    [Parameter] public string? InlineSize { get; set; }

    /// <summary>Minimum inline size.</summary>
    [Parameter] public string? MinInlineSize { get; set; }

    /// <summary>Maximum inline size.</summary>
    [Parameter] public string? MaxInlineSize { get; set; }

    /// <summary>Overflow behaviour: <c>visible</c> or <c>hidden</c>.</summary>
    [Parameter] public string? Overflow { get; set; }

    /// <summary>Display: <c>auto</c> or <c>none</c>.</summary>
    [Parameter] public string? Display { get; set; }

    /// <summary>Raised on click.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnClick { get; set; }

    /// <summary>
    /// Announces whether what this surface controls is open, as <c>aria-expanded</c> on the
    /// <c>&lt;button&gt;</c> Polaris renders inside its shadow root. Leave it <c>null</c> for a surface
    /// that controls nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PolarisButton.AccessibilityExpanded"/>, this cannot use a property: measured
    /// from Polaris's render function, <c>s-clickable</c> reads only its accessibility label and its
    /// command/interest props, so the state is written onto the shadow button through the interop
    /// layer. That is the element assistive technology sees; an attribute on the
    /// <c>&lt;s-clickable&gt;</c> host lands on an element with no role and is ignored.
    /// </para>
    /// <para>
    /// The surface has no accessible name of its own either, because a heading set as an attribute
    /// lives in Polaris's shadow root rather than in this element's text. Set
    /// <see cref="PolarisElementComponentBase.AccessibilityLabel"/> alongside this.
    /// </para>
    /// </remarks>
    [Parameter] public PolarisAccessibilityExpanded? AccessibilityExpanded { get; set; }

    /// <summary>Raised when the surface receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <summary>Raised when the surface loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Click, OnClick);
        events.On(PolarisEvents.Focus, OnFocus);
        events.On(PolarisEvents.Blur, OnBlur);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(false);

        if (AccessibilityExpanded is not null)
        {
            // Re-applied on every render, because Polaris re-creates the button when it renders and
            // anything written onto the previous node goes with it. A false return means there was no
            // button to write to, which costs the announcement and nothing else.
            await Interop
                .SetAriaExpandedAsync(Element, AccessibilityExpanded.Value == PolarisAccessibilityExpanded.Expanded)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["loading"] = PolarisAttributeValue.Format(Loading);
        attributes["href"] = Href;
        attributes["target"] = Href is null ? null : PolarisAttributeValue.EnumValue(Target);
        attributes["download"] = Download;
        attributes["type"] = PolarisAttributeValue.EnumValue(Type);
        attributes["accessibilityRole"] = AccessibilityRole;
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
        attributes["commandFor"] = CommandFor;
        attributes["command"] = CommandFor is null ? null : PolarisAttributeValue.EnumValue(Command);
        attributes["interestFor"] = InterestFor;
        attributes["background"] = Background;
        attributes["padding"] = Padding;
        attributes["paddingBlock"] = PaddingBlock;
        attributes["paddingBlockStart"] = PaddingBlockStart;
        attributes["paddingBlockEnd"] = PaddingBlockEnd;
        attributes["paddingInline"] = PaddingInline;
        attributes["paddingInlineStart"] = PaddingInlineStart;
        attributes["paddingInlineEnd"] = PaddingInlineEnd;
        attributes["border"] = Border;
        attributes["borderWidth"] = BorderWidth;
        attributes["borderStyle"] = BorderStyle;
        attributes["borderColor"] = BorderColor;
        attributes["borderRadius"] = BorderRadius;
        attributes["blockSize"] = BlockSize;
        attributes["minBlockSize"] = MinBlockSize;
        attributes["maxBlockSize"] = MaxBlockSize;
        attributes["inlineSize"] = InlineSize;
        attributes["minInlineSize"] = MinInlineSize;
        attributes["maxInlineSize"] = MaxInlineSize;
        attributes["overflow"] = Overflow;
        attributes["display"] = Display;
    }
}
