using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// A pick-a-known-value-or-type-a-new-one field: a search box over a list of the values already known,
/// with an "Add ..." entry appended for whatever has been typed, and the chosen value shown as a
/// removable chip.
/// </summary>
/// <remarks>
/// <para>
/// This is the select-shaped sibling of <see cref="PolarisSuggestField"/>, and the difference is what
/// the merchant is looking at. That one draws the known values as chips and keeps the field as the
/// value; this one keeps the field as a search box, draws the values as a list of rows underneath, and
/// shows the picked value as a chip in the field's place. Both exist because the right shape depends on
/// whether the list is a handful of tags or a directory of suppliers.
/// </para>
/// <para>
/// <b>Why it is not an <c>s-select</c>.</b> A native select cannot be typed into, so it cannot offer
/// "add the thing I have just typed" — which is the whole point of a field whose list is learned from
/// use. Polaris 1 has no combobox and no programmatic popover opening (a popover is opened by a
/// button's <c>commandFor</c>), so the list is drawn inline, beneath the field, in a bordered panel.
/// That is also why this component's name says "inline": the panel pushes the page down instead of
/// floating over it, and it never needs a focus trap or a click-outside handler to dismiss.
/// </para>
/// <para>
/// The rows are buttons, not <see cref="PolarisClickable"/>s: a clickable surface is not focusable and is
/// not announced as interactive, and a list of choices a keyboard cannot reach is not a list. The add
/// entry is a button for the same reason, and it reads as an action rather than as another choice.
/// </para>
/// <para>
/// <b>Not verified in a real browser.</b> The panel's appearance in particular — a bordered box of
/// full-width tertiary buttons — is the part a component test cannot judge.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisSuggestFieldInline Label="Supplier"
///                             Value="@_supplier"
///                             Suggestions="_suppliers"
///                             CollectionName="your suppliers"
///                             Placeholder="Search or add a supplier"
///                             ValueChanged="@(value => _supplier = value ?? string.Empty)"
///                             SuggestionAdded="@AddSupplierAsync" /&gt;
/// </code>
/// </example>
public sealed class PolarisSuggestFieldInline : ComponentBase
{
    /// <summary>The <see cref="Value"/> the previous parameter pass carried, so a caller's change is told apart from an echo.</summary>
    private string? _lastParameter;

    /// <summary>What is in the search box. Kept locally so filtering does not wait for a round trip.</summary>
    private string _query = string.Empty;

    /// <summary>The field's accessible label, used as the search box's label and the chip's caption.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the search box.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Placeholder shown while the search box is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The chosen value, which the caller owns. Empty or null means nothing is chosen.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>The known values, offered as rows. They are filtered by what is typed.</summary>
    [Parameter] public IReadOnlyList<string> Suggestions { get; set; } = [];

    /// <summary>
    /// What the list is called in the add entry's accessible name — "Add the supplier <em>Acme</em> to
    /// <em>your suppliers</em>". Lower case and with no article: it continues the sentence.
    /// </summary>
    [Parameter] public string? CollectionName { get; set; }

    /// <summary>
    /// Offers the "Add ..." entry for a value the list has not got. True by default: without it the list
    /// can never grow, and the merchant types the same new name into every record.
    /// </summary>
    [Parameter] public bool AllowAdd { get; set; } = true;

    /// <summary>Gives the chosen value's chip a remove control, which clears the value.</summary>
    [Parameter] public bool Removable { get; set; } = true;

    /// <summary>Prevents the search box and every row from being used.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Shown when there is nothing to choose from and nothing typed. Without it the panel is simply
    /// absent, which on a first use looks like a control that failed to load rather than an empty list.
    /// </summary>
    [Parameter] public string? EmptyLabel { get; set; }

    /// <summary>
    /// Replaces the chosen chip's accessible name, formatted with the value. Set it when the chip's own
    /// text does not say what removing it removes — for example <c>"Supplier: {0}"</c>.
    /// </summary>
    [Parameter] public string? ValueAccessibilityLabelFormat { get; set; }

    /// <summary>Raised with the chosen value, or with <c>null</c> when the chip is removed.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Raised with the typed value when the merchant asks for it to be remembered.</summary>
    [Parameter] public EventCallback<string> SuggestionAdded { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Adopted when the PARAMETER changes, never when it differs from what was typed — the rule the
        // suggest field and the range slider both paid for: a caller that leaves the field unbound, or
        // re-renders with a stale value while a new one is in flight, must not have its text reset.
        if (!string.Equals(_lastParameter, Value, StringComparison.Ordinal))
        {
            _lastParameter = Value;
            _query = string.Empty;
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var chosen = string.IsNullOrWhiteSpace(Value) ? null : Value;

        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(2, nameof(PolarisStack.ChildContent), (RenderFragment)(stack =>
        {
            if (chosen is not null)
            {
                RenderChosen(stack, chosen);
            }
            else
            {
                RenderPicker(stack);
            }
        }));
        builder.CloseComponent();
    }

    /// <summary>Draws the picked value as the field's chip, with the label above it so the chip is not unattributed.</summary>
    private void RenderChosen(RenderTreeBuilder stack, string chosen)
    {
        if (!string.IsNullOrWhiteSpace(Label))
        {
            stack.OpenComponent<PolarisText>(10);
            stack.AddAttribute(11, nameof(PolarisText.Color), PolarisColor.Subdued);
            stack.AddAttribute(12, nameof(PolarisText.ChildContent), (RenderFragment)(caption =>
                caption.AddContent(13, Label)));
            stack.CloseComponent();
        }

        stack.OpenComponent<PolarisClickableChip>(20);
        stack.AddAttribute(21, nameof(PolarisClickableChip.Removable), Removable);

        if (!string.IsNullOrEmpty(ValueAccessibilityLabelFormat))
        {
            stack.AddAttribute(
                22,
                nameof(PolarisClickableChip.AccessibilityLabel),
                string.Format(System.Globalization.CultureInfo.InvariantCulture, ValueAccessibilityLabelFormat, chosen));
        }

        stack.AddAttribute(23, nameof(PolarisClickableChip.OnRemove), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            async _ => await ClearAsync()));
        stack.AddAttribute(24, nameof(PolarisClickableChip.ChildContent), (RenderFragment)(chip =>
            chip.AddContent(25, chosen)));
        stack.CloseComponent();
    }

    /// <summary>Draws the search box and, beneath it, the rows it can offer.</summary>
    private void RenderPicker(RenderTreeBuilder stack)
    {
        var matches = Matches(Suggestions, _query);

        // The add entry exists only for a value the list has not got. Typing "cook" when "Cookson Gold"
        // is already known shows that supplier and nothing to add, which is what stops the same name
        // accumulating two spellings.
        var canAdd = AllowAdd && !Disabled && IsNew(Suggestions, _query);

        stack.OpenComponent<PolarisSearchField>(30);
        stack.AddAttribute(31, nameof(PolarisSearchField.Label), Label);
        stack.AddAttribute(32, nameof(PolarisSearchField.Details), Details);
        stack.AddAttribute(33, nameof(PolarisSearchField.Placeholder), Placeholder);
        stack.AddAttribute(34, nameof(PolarisSearchField.Value), _query);
        stack.AddAttribute(35, nameof(PolarisSearchField.Disabled), Disabled);
        stack.AddAttribute(36, nameof(PolarisSearchField.OnInput), TypedCallback());
        stack.AddAttribute(37, nameof(PolarisSearchField.OnChange), TypedCallback());
        stack.CloseComponent();

        if (matches.Count == 0 && !canAdd)
        {
            if (!string.IsNullOrWhiteSpace(EmptyLabel))
            {
                stack.OpenComponent<PolarisText>(40);
                stack.AddAttribute(41, nameof(PolarisText.Color), PolarisColor.Subdued);
                stack.AddAttribute(42, nameof(PolarisText.ChildContent), (RenderFragment)(empty =>
                    empty.AddContent(43, EmptyLabel)));
                stack.CloseComponent();
            }

            return;
        }

        // One bordered panel holding the rows, so the list reads as a menu hanging off the field rather
        // than as a set of stray buttons. s-box paints the frame; the stack inside it paints the rows.
        stack.OpenComponent<PolarisBox>(50);
        stack.AddAttribute(51, nameof(PolarisBox.Border), "base");
        stack.AddAttribute(52, nameof(PolarisBox.BorderRadius), "base");
        stack.AddAttribute(53, nameof(PolarisBox.Overflow), "hidden");
        stack.AddAttribute(54, nameof(PolarisBox.ChildContent), (RenderFragment)(box =>
        {
            box.OpenComponent<PolarisStack>(60);
            box.AddAttribute(61, nameof(PolarisStack.Gap), PolarisGap.None);
            box.AddAttribute(62, nameof(PolarisStack.ChildContent), (RenderFragment)(rows =>
            {
                foreach (var suggestion in matches)
                {
                    // Tertiary and filling the row: a full-width, low-emphasis action, which is what a
                    // row in a menu of choices is. It stays a real button, so it is reachable with Tab
                    // and announced as a button.
                    rows.OpenComponent<PolarisButton>(70);
                    rows.AddAttribute(71, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
                    rows.AddAttribute(72, nameof(PolarisButton.InlineSize), PolarisButtonInlineSize.Fill);
                    rows.AddAttribute(73, nameof(PolarisButton.Disabled), Disabled);
                    rows.AddAttribute(74, nameof(PolarisButton.OnClick), ChooseCallback(suggestion));
                    rows.AddAttribute(75, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
                        label.AddContent(76, suggestion)));
                    rows.CloseComponent();
                }

                if (canAdd)
                {
                    rows.OpenComponent<PolarisButton>(80);
                    rows.AddAttribute(81, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
                    rows.AddAttribute(82, nameof(PolarisButton.InlineSize), PolarisButtonInlineSize.Fill);
                    rows.AddAttribute(83, nameof(PolarisButton.Icon), "plus");
                    rows.AddAttribute(84, nameof(PolarisButton.AccessibilityLabel), AddAccessibilityLabel());
                    rows.AddAttribute(85, nameof(PolarisButton.OnClick), AddCallback());
                    rows.AddAttribute(86, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
                        label.AddContent(87, AddLabelText())));
                    rows.CloseComponent();
                }
            }));
            box.CloseComponent();
        }));
        stack.CloseComponent();
    }

    /// <summary>What the add row says: the typed value, quoted, so it is obvious what would be created.</summary>
    private string AddLabelText() => $"Add \u201c{_query.Trim()}\u201d";

    /// <summary>
    /// The add row's accessible name. The visible text is the quoted value alone, which does not say what
    /// pressing it does, and a screen reader hears only that.
    /// </summary>
    private string AddAccessibilityLabel() => string.IsNullOrWhiteSpace(CollectionName)
        ? $"Add \"{_query.Trim()}\""
        : $"Add \"{_query.Trim()}\" to {CollectionName}";

    /// <summary>
    /// The suggestions that match what has been typed, by the same rule the suggest field uses.
    /// </summary>
    /// <param name="suggestions">The whole list.</param>
    /// <param name="query">The search box's text, or null.</param>
    /// <returns>The values to draw as rows, in the order they were given.</returns>
    /// <remarks>
    /// Delegated rather than reimplemented: two controls that offer the same vocabulary and filter it
    /// differently is a difference nobody would think to test for, and the merchant would meet it as
    /// "the list is wrong on this screen".
    /// </remarks>
    public static IReadOnlyList<string> Matches(IReadOnlyList<string> suggestions, string? query) =>
        PolarisSuggestField.Matches(suggestions, query);

    /// <summary>Whether the typed value is one the list has not got. Delegated for the same reason as <see cref="Matches"/>.</summary>
    /// <param name="suggestions">The whole list.</param>
    /// <param name="value">The search box's text.</param>
    /// <returns>Whether there is something to add.</returns>
    public static bool IsNew(IReadOnlyList<string> suggestions, string? value) =>
        PolarisSuggestField.IsNew(suggestions, value);

    /// <summary>Written as a method group so it binds to the <c>Func&lt;T, Task&gt;</c> overload rather than to <c>Action&lt;T&gt;</c>.</summary>
    private EventCallback<PolarisEventArgs> TypedCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, OnTypedAsync);

    private EventCallback<PolarisEventArgs> ChooseCallback(string suggestion) =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await ChooseAsync(suggestion));

    private EventCallback<PolarisEventArgs> AddCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await AddAsync());

    /// <summary>
    /// Keeps the search box's text. It is deliberately not reported to the caller: this component's
    /// value is the CHOSEN supplier, and a half-typed name is not a value. <see cref="SuggestionAdded"/>
    /// is what tells the caller about a new one.
    /// </summary>
    private Task OnTypedAsync(PolarisEventArgs args)
    {
        _query = args.Value ?? string.Empty;

        return Task.CompletedTask;
    }

    private async Task ChooseAsync(string suggestion)
    {
        // The search box is emptied as the value is taken: if the chip is later removed, the picker comes
        // back with the whole list rather than with the last search still in it.
        _query = string.Empty;

        await ValueChanged.InvokeAsync(suggestion);
    }

    private async Task AddAsync()
    {
        var trimmed = _query.Trim();

        if (!IsNew(Suggestions, trimmed))
        {
            return;
        }

        // The caller persists the name and sets Value; this component does not add to its own list, so
        // there is exactly one place that owns the vocabulary.
        _query = string.Empty;

        await SuggestionAdded.InvokeAsync(trimmed);
    }

    private async Task ClearAsync() => await ValueChanged.InvokeAsync(null);
}
