using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// A free-text field with a list of already-known values beneath it, drawn as removable chips.
/// </summary>
/// <remarks>
/// <para>
/// <b>Polaris has no combobox or autocomplete.</b> That is a measured fact about the runtime this
/// library wraps: <c>s-select</c> is single-select with no search, <c>s-search-field</c> is a text
/// field with no list, and neither can be opened from a text field — a popover is opened by a
/// button's <c>commandFor</c>, so the "type ahead and pick" control every admin has does not exist
/// to be wrapped. So this is a <em>composed</em> component, like <see cref="PolarisAccordion"/>: the
/// value stays free text, because a merchant must be able to enter something the list has never
/// heard of, and the list is there to save them from retyping what it has.
/// </para>
/// <para>
/// The list is a vocabulary, not a constraint: choosing a chip only fills the field, and removing one
/// only forgets the name. Neither of those edits the rows that already carry that value, which is
/// deliberate — a chip removed from the list must not silently rewrite a hundred records.
/// </para>
/// <para>
/// The suggestions are filtered by what has been typed (<see cref="Matches"/>), which is this
/// component's stand-in for a search box. A blank field shows the whole list, so the chips double as
/// a picker; typing narrows them.
/// </para>
/// <para>
/// <strong>Not verified in a real browser.</strong> What the component tests can prove is the markup
/// and that every callback is wired; what they cannot is how Polaris's own chip behaves when its remove
/// control is pressed. If that press also raises <c>click</c> on the chip — which the element's
/// documentation does not say either way — the field would end up holding the name that was just
/// removed from the list. That is harmless (the value is free text, and the supplier-plus-code rule is
/// enforced on save), but it is the first thing to check with
/// <c>tools/verify-in-browser.mjs</c> on the sample gallery's section for this control.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisSuggestField Label="Supplier"
///                       Value="@_supplier"
///                       Suggestions="_suppliers"
///                       SuggestionsLabel="Your suppliers"
///                       CollectionName="your suppliers"
///                       ValueChanged="@(value => _supplier = value ?? string.Empty)"
///                       SuggestionChosen="@(name => _supplier = name)"
///                       SuggestionAdded="@AddSupplierAsync"
///                       SuggestionRemoved="@RemoveSupplierAsync" /&gt;
/// </code>
/// </example>
public sealed class PolarisSuggestField : ComponentBase
{
    /// <summary>The <see cref="Value"/> the previous parameter pass carried, so a caller's change is told apart from an echo.</summary>
    private string? _lastParameter;

    /// <summary>What is in the field. Kept locally so filtering does not wait for a round trip.</summary>
    private string _query = string.Empty;

    /// <summary>The field's accessible label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field. This is the natural home for the rule the value must obey.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>The field's value, which the caller owns.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>The known values offered as chips.</summary>
    [Parameter] public IReadOnlyList<string> Suggestions { get; set; } = [];

    /// <summary>
    /// Caption above the chips, for example "Your suppliers". No caption is drawn when this is null,
    /// which is the right choice when the field's own details already explain where the list came from.
    /// </summary>
    [Parameter] public string? SuggestionsLabel { get; set; }

    /// <summary>
    /// What the list is called on the add button, in a sentence such as <c>Add "Acme" to your
    /// suppliers</c>. Lower case and with no article: it continues the sentence rather than starting one.
    /// </summary>
    [Parameter] public string? CollectionName { get; set; }

    /// <summary>
    /// Offers a button that adds the typed value to the list. True by default, because a free-text
    /// field with a list nobody can add to is a field the merchant types the same new value into for ever.
    /// </summary>
    [Parameter] public bool AllowAdd { get; set; } = true;

    /// <summary>Gives each chip a remove control. Wire <see cref="SuggestionRemoved"/> to act on it.</summary>
    [Parameter] public bool Removable { get; set; } = true;

    /// <summary>Prevents the field and the add button from being used.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Replaces each chip's accessible name. A chip's own text is the value, which names what its
    /// remove control removes; set this when activating the chip itself needs saying too, for example
    /// <c>"Use {0}"</c>.
    /// </summary>
    [Parameter] public string? SuggestionAccessibilityLabelFormat { get; set; }

    /// <summary>Raised with the field's text as it is typed and as it is committed.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Raised with the chip's value when the merchant picks one.</summary>
    [Parameter] public EventCallback<string> SuggestionChosen { get; set; }

    /// <summary>Raised with the typed value when the merchant asks for it to be remembered.</summary>
    [Parameter] public EventCallback<string> SuggestionAdded { get; set; }

    /// <summary>Raised with the chip's value when the merchant removes it from the list.</summary>
    [Parameter] public EventCallback<string> SuggestionRemoved { get; set; }

    /// <summary>
    /// The suggestions that match what has been typed.
    /// </summary>
    /// <param name="suggestions">The whole list.</param>
    /// <param name="query">The field's text, or null.</param>
    /// <returns>The values to draw as chips, in the order they were given.</returns>
    /// <remarks>
    /// A blank query matches everything, so an untouched field shows the whole list and the chips work
    /// as a picker. Matching is "contains", ignoring case: a merchant looking for "Cookson" types
    /// "cook", and a prefix-only rule would hide "Cookson Gold" the moment they typed its second word.
    /// </remarks>
    public static IReadOnlyList<string> Matches(IReadOnlyList<string> suggestions, string? query)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return suggestions;
        }

        return [.. suggestions.Where(suggestion =>
            suggestion.Contains(trimmed, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Whether the typed value is one the list has not got, which is what makes the add button appear.
    /// </summary>
    /// <param name="suggestions">The whole list.</param>
    /// <param name="value">The field's text.</param>
    /// <returns>Whether there is something to add.</returns>
    /// <remarks>
    /// Compared without regard to case and ignoring surrounding spaces, because that is how the
    /// value is matched when it is saved. Without this, a merchant who re-typed "acme" in a different
    /// case would be offered a button that adds a second copy of a supplier they already have.
    /// </remarks>
    public static bool IsNew(IReadOnlyList<string> suggestions, string? value)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        var trimmed = value?.Trim() ?? string.Empty;

        return trimmed.Length > 0
            && !suggestions.Any(suggestion => string.Equals(suggestion.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The caller's value is adopted when the PARAMETER changes, not when it differs from what was
        // last reported. A parent that applies changes lazily re-renders with its previous value while a
        // new one is in flight, and treating that stale echo as an instruction resets the field — measured
        // in this library's range slider, where the handle jumped back on every second key press. This is
        // also what lets a caller leave the field uncontrolled: run it with no Value binding at all and
        // nothing here ever overwrites what the merchant typed.
        if (!string.Equals(_lastParameter, Value, StringComparison.Ordinal))
        {
            _lastParameter = Value;
            _query = Value ?? string.Empty;
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var matches = Matches(Suggestions, _query);

        // The add button is the only way a brand-new value becomes a chip, so it appears exactly when
        // there is one to add: never for blank text, and never for a value the list already holds.
        var canAdd = AllowAdd && !Disabled && IsNew(Suggestions, _query);

        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(2, nameof(PolarisStack.ChildContent), (RenderFragment)(stack =>
        {
            stack.OpenComponent<PolarisTextField>(10);
            stack.AddAttribute(11, nameof(PolarisTextField.Label), Label);
            stack.AddAttribute(12, nameof(PolarisTextField.Details), Details);
            stack.AddAttribute(13, nameof(PolarisTextField.Placeholder), Placeholder);
            stack.AddAttribute(14, nameof(PolarisTextField.Value), _query);
            stack.AddAttribute(15, nameof(PolarisTextField.Disabled), Disabled);

            // Both events go to one handler: input filters the chips as the merchant types, and change
            // (blur or Enter) is the same edit committed. A field that only listened to change would
            // leave the chips stale for the whole time a merchant is still typing.
            stack.AddAttribute(16, nameof(PolarisTextField.OnInput), OnTypedCallback());
            stack.AddAttribute(17, nameof(PolarisTextField.OnChange), OnTypedCallback());
            stack.CloseComponent();

            if (!string.IsNullOrWhiteSpace(SuggestionsLabel))
            {
                stack.OpenComponent<PolarisText>(20);
                stack.AddAttribute(21, nameof(PolarisText.Color), PolarisColor.Subdued);
                stack.AddAttribute(22, nameof(PolarisText.ChildContent), (RenderFragment)(caption =>
                    caption.AddContent(23, SuggestionsLabel)));
                stack.CloseComponent();
            }

            if (matches.Count > 0)
            {
                // A grid rather than an inline stack: s-stack's inline direction does not wrap, so a
                // list longer than the row would run off the edge with no way to reach the end. An
                // auto-filling grid wraps and keeps each chip its own width.
                stack.OpenComponent<PolarisGrid>(30);
                stack.AddAttribute(31, nameof(PolarisGrid.GridTemplateColumns), "repeat(auto-fill, minmax(9rem, max-content))");
                stack.AddAttribute(32, nameof(PolarisGrid.Gap), "small");
                stack.AddAttribute(33, nameof(PolarisGrid.ChildContent), (RenderFragment)(grid =>
                {
                    foreach (var suggestion in matches)
                    {
                        grid.OpenComponent<PolarisClickableChip>(40);
                        grid.AddAttribute(41, nameof(PolarisClickableChip.Removable), Removable);

                        if (!string.IsNullOrEmpty(SuggestionAccessibilityLabelFormat))
                        {
                            grid.AddAttribute(
                                42,
                                nameof(PolarisClickableChip.AccessibilityLabel),
                                string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    SuggestionAccessibilityLabelFormat,
                                    suggestion));
                        }

                        grid.AddAttribute(43, nameof(PolarisClickableChip.OnClick), ChooseCallback(suggestion));
                        grid.AddAttribute(44, nameof(PolarisClickableChip.OnRemove), RemoveCallback(suggestion));
                        grid.AddAttribute(45, nameof(PolarisClickableChip.ChildContent), (RenderFragment)(chip =>
                            chip.AddContent(46, suggestion)));
                        grid.CloseComponent();
                    }
                }));
                stack.CloseComponent();
            }

            if (canAdd)
            {
                stack.OpenComponent<PolarisButton>(50);
                stack.AddAttribute(51, nameof(PolarisButton.Variant), PolarisButtonVariant.Secondary);
                stack.AddAttribute(52, nameof(PolarisButton.ChildContent), (RenderFragment)(button =>
                    button.AddContent(53, AddButtonText())));
                stack.AddAttribute(54, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(
                    this,
                    async _ => await AddAsync()));
                stack.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    /// <summary>The button's own wording, which names the value so the merchant knows what it will add.</summary>
    private string AddButtonText()
    {
        var trimmed = _query.Trim();

        return string.IsNullOrWhiteSpace(CollectionName)
            ? $"Add \u201c{trimmed}\u201d"
            : $"Add \u201c{trimmed}\u201d to {CollectionName}";
    }

    /// <summary>
    /// The handler for typing. Written as a method group so it binds to the <c>Func&lt;T, Task&gt;</c>
    /// overload: a lambda that calls an async method without awaiting it binds to <c>Action&lt;T&gt;</c>,
    /// is fire-and-forget, and loses any exception — measured in this library's data grid.
    /// </summary>
    private EventCallback<PolarisEventArgs> OnTypedCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, OnTypedAsync);

    private EventCallback<PolarisEventArgs> ChooseCallback(string suggestion) =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await ChooseAsync(suggestion));

    private EventCallback<PolarisEventArgs> RemoveCallback(string suggestion) =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await RemoveAsync(suggestion));

    private async Task OnTypedAsync(PolarisEventArgs args)
    {
        _query = args.Value ?? string.Empty;

        await ValueChanged.InvokeAsync(_query);
    }

    private async Task ChooseAsync(string suggestion)
    {
        // Picking a chip is a choice of value, so the field adopts it and the caller is told both ways:
        // through ValueChanged, which every other field raises, and through SuggestionChosen for callers
        // that want to do more than store it.
        _query = suggestion;

        await ValueChanged.InvokeAsync(suggestion);
        await SuggestionChosen.InvokeAsync(suggestion);
    }

    private async Task AddAsync()
    {
        var trimmed = _query.Trim();

        if (!IsNew(Suggestions, trimmed))
        {
            return;
        }

        await SuggestionAdded.InvokeAsync(trimmed);
    }

    private async Task RemoveAsync(string suggestion)
    {
        // The field is deliberately left alone. Its text is free text, so whatever the merchant typed
        // stays typed even when the name it came from has just been removed from the list.
        await SuggestionRemoved.InvokeAsync(suggestion);
    }
}
