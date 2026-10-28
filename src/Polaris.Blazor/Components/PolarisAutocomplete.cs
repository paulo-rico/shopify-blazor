using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor.Components;

/// <summary>
/// A text field that suggests the items matching what has been typed and reports the item the merchant
/// chooses.
/// </summary>
/// <remarks>
/// <para>
/// This is the generic, single-selection autocomplete: it filters a list of <typeparamref name="TItem"/>
/// locally (or asks an optional <see cref="SearchFunction"/>), draws the matches beneath the field, and
/// reports the choice through <see cref="ValueChanged"/>. It is <em>composed</em> rather than a wrapper,
/// because Polaris has no autocomplete, combobox or listbox element — the same measured absence the
/// suggest fields document — so it is built from an <c>s-search-field</c> and a bordered <c>s-box</c> of
/// <c>s-button</c> rows.
/// </para>
/// <para>
/// <b>The list floats over the page, in a popup this library owns.</b> Polaris's own popover cannot be
/// opened from code and a text field cannot anchor one — both measured in Chrome — but that rules out
/// borrowing Shopify's popover, not having a panel at all. <see cref="PolarisPopup"/> is an absolutely
/// positioned surface anchored to the field: it covers what follows the field instead of pushing it
/// down, follows the page on scroll without measurement, and paints itself with the browser's system
/// colours so it follows the Admin's theme. Everything inside it is still a Polaris element.
/// </para>
/// <para>
/// <b>Keyboard navigation is real focus, not a highlight.</b> The arrow keys move focus onto the row
/// buttons; Enter and Space then activate the focused button natively and Escape closes the list and
/// returns focus to the field. That is deliberately not the <c>aria-activedescendant</c> combobox
/// pattern: the active option would have to be announced by an <c>aria-activedescendant</c> written onto
/// the input inside the field's shadow root, which no light-DOM attribute can reach. Moving real
/// focus means a screen reader announces the row's own text with no invented ARIA, which is why the rows
/// are buttons rather than clickable surfaces. The price is that typing does not reach the field while a
/// row holds focus — Escape takes the merchant back to it.
/// </para>
/// <para>
/// <b>Text and value are separate state.</b> <see cref="Value"/> is the chosen item; the field's text is
/// reported through <see cref="ValueChanged"/> only when a selection is made. Typing that has not been
/// confirmed is not a value: a commit of text that matches no item restores the chosen item's text (or
/// clears the field when nothing is chosen), unless <see cref="AllowCustomValue"/> asks for the text to
/// be handed over instead. Treating half-typed text as a selection is what makes a dependent form
/// validate a value the merchant never picked.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisAutocomplete TItem="MaterialRow"
///                       Items="_materials"
///                       TextSelector="row =&gt; row.Title"
///                       Label="Material"
///                       Value="_chosen"
///                       ValueChanged="Chosen" /&gt;
/// </code>
/// </example>
/// <typeparam name="TItem">The type of the items offered. It is never mutated or persisted here.</typeparam>
public sealed class PolarisAutocomplete<TItem> : ComponentBase, IDisposable
{
    /// <summary>
    /// How long a blur waits before the list closes, so focus moving from the field to a row (or from one
    /// row to the next) can cancel it.
    /// </summary>
    /// <remarks>
    /// A blur that closes the list immediately would remove the row the browser is about to focus, which
    /// is the classic "the arrow key does nothing" autocomplete fault. The delay is short enough to read
    /// as immediate when the merchant really does click away.
    /// </remarks>
    private const int CloseDelayMilliseconds = 150;

    /// <summary>The element of each drawn row, so focus can be moved onto it after the render that drew it.</summary>
    private readonly Dictionary<int, ElementReference> _rowElements = [];

    /// <summary>The interop service, used only to move focus across the field's shadow boundary.</summary>
    [Inject] private IPolarisInterop Interop { get; set; } = default!;

    /// <summary>What is in the field. Kept locally so filtering does not wait for a round trip.</summary>
    private string _query = string.Empty;

    /// <summary>What an asynchronous <see cref="SearchFunction"/> last returned.</summary>
    private IReadOnlyList<TItem> _remoteItems = [];

    /// <summary>Whether the list is drawn under the field.</summary>
    private bool _open;

    /// <summary>Whether an asynchronous search is in flight.</summary>
    private bool _searching;

    /// <summary>Whether this component has been disposed, so a pending timer or search can stop.</summary>
    private bool _disposed;

    /// <summary>Whether <see cref="_lastValue"/> holds a value from an earlier parameter pass.</summary>
    private bool _hasLastValue;

    /// <summary>The <see cref="Value"/> the previous parameter pass carried, so an echo is told from an instruction.</summary>
    private TItem? _lastValue;

    /// <summary>
    /// Suppresses the commit that follows a selection: the field raises <c>change</c> after Enter as well
    /// as after blur, and the value has already been reported.
    /// </summary>
    private bool _suppressNextCommit;

    /// <summary>
    /// Whether this component is moving focus itself, in which case the field's blur is not a commit.
    /// </summary>
    /// <remarks>
    /// The browser fires <c>change</c> when the field loses focus to a row, and treating that as a commit
    /// restored the chosen text and closed the list the arrow key had just opened — measured in Chrome:
    /// <c>document.activeElement</c> came back as <c>BODY</c>, because the button the focus was headed for
    /// had been removed. The flag is set immediately before the programmatic focus and cleared by the
    /// focus event that follows it.
    /// </remarks>
    private bool _movingFocus;

    /// <summary>The wrapper around the search field, the element focus returns to.</summary>
    private ElementReference _fieldElement;

    /// <summary>The row the next render should move focus onto, or null when focus must not move.</summary>
    private int? _pendingFocusRow;

    /// <summary>Whether the next render should move focus back into the field.</summary>
    private bool _pendingFieldFocus;

    /// <summary>The pending delayed close, cancelled when focus arrives inside the component.</summary>
    private CancellationTokenSource? _closeCts;

    /// <summary>The search in flight, cancelled by the next keystroke and by disposal.</summary>
    private CancellationTokenSource? _searchCts;

    /// <summary>
    /// The whole list of items to filter, in the caller's order.
    /// </summary>
    /// <remarks>
    /// Ignored while <see cref="SearchFunction"/> is set: a remote search owns the list, and filtering the
    /// stale local copy on top of it would show results the provider did not return.
    /// </remarks>
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];

    /// <summary>The chosen item, which the caller owns. The default means nothing is chosen.</summary>
    /// <remarks>
    /// Adopted when the <em>parameter</em> changes, never when it differs from what the field shows, so a
    /// caller that re-renders with its previous value while a new one is in flight cannot reset the text
    /// mid-typing. A value type has no null state here: its <c>default</c> is simply a selectable item.
    /// </remarks>
    [Parameter] public TItem? Value { get; set; }

    /// <summary>Raised with the item the merchant chose, or with <c>default</c> when the field is cleared.</summary>
    [Parameter] public EventCallback<TItem?> ValueChanged { get; set; }

    /// <summary>
    /// The text shown for an item, used both for filtering and for the field's text after a selection.
    /// Defaults to the item's own <c>ToString()</c>.
    /// </summary>
    [Parameter] public Func<TItem, string> TextSelector { get; set; } =
        item => item is null ? string.Empty : item.ToString() ?? string.Empty;

    /// <summary>
    /// Replaces the built-in matching rule, which keeps the items whose text contains the typed term,
    /// case-insensitively, ignoring surrounding spaces.
    /// </summary>
    /// <remarks>
    /// The term arrives trimmed. A custom rule that matches by prefix, by several fields or by a
    /// catalogue code belongs here; it must not be written to change what <see cref="TextSelector"/>
    /// displays.
    /// </remarks>
    [Parameter] public Func<TItem, string, bool>? FilterFunction { get; set; }

    /// <summary>
    /// An optional asynchronous provider. When set, it is asked for the items matching each typed term and
    /// <see cref="Items"/> is ignored.
    /// </summary>
    /// <remarks>
    /// Each keystroke cancels the call before it, and a result that arrives after a newer term has been
    /// typed is dropped. An exception other than cancellation is left to the caller's circuit: a search
    /// that fails silently is a panel that says "no matches" when the truth is that nobody asked.
    /// </remarks>
    [Parameter] public Func<string, CancellationToken, Task<IReadOnlyList<TItem>>>? SearchFunction { get; set; }

    /// <summary>
    /// Renders one row's contents instead of its bare text, so a suggestion can carry a description, a
    /// badge or an icon.
    /// </summary>
    /// <remarks>
    /// The markup goes inside a button, so it must be inline content. A block element there renders, but
    /// nests a block inside a button, which HTML does not allow — use text, <c>PolarisText</c> or
    /// <c>PolarisIcon</c>.
    /// </remarks>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>The field's visible label. It is drawn by the field itself; do not add a sibling label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the field.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>Placeholder shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// How many characters must be typed before the list opens on its own. The default of one keeps a
    /// page that loads with several autocompletes from opening all their lists at once. It gates opening,
    /// not filtering: an explicitly opened list (ArrowDown) still shows the current matches, and a blank
    /// term matches everything.
    /// </summary>
    [Parameter] public int MinimumLength { get; set; } = 1;

    /// <summary>
    /// The most rows drawn at once. Null draws every match. The rows that are dropped are still matched,
    /// so a page offering a large catalogue should narrow the term rather than rely on the cap.
    /// </summary>
    [Parameter] public int? MaxSuggestions { get; set; } = 10;

    /// <summary>
    /// How long <see cref="SearchFunction"/> waits after the last keystroke before it is called. Zero asks
    /// immediately. It has no effect on local filtering, which is already per keystroke.
    /// </summary>
    [Parameter] public int DebounceMilliseconds { get; set; } = 250;

    /// <summary>
    /// Accepts text that is not one of the items: the commit raises <see cref="CustomValueEntered"/>
    /// instead of restoring the chosen item's text.
    /// </summary>
    /// <remarks>
    /// This is the hook for "create a new category" as much as for free text. The component does not turn
    /// the text into a value — it has no way to make a <typeparamref name="TItem"/> out of it — so the
    /// caller maps the text to an item and sets <see cref="Value"/>.
    /// </remarks>
    [Parameter] public bool AllowCustomValue { get; set; }

    /// <summary>Raised with the trimmed text when a commit finds no item and <see cref="AllowCustomValue"/> is set.</summary>
    [Parameter] public EventCallback<string> CustomValueEntered { get; set; }

    /// <summary>Prevents the field from being typed into and the rows from being used.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows the chosen text without allowing it to be edited or the list to be opened.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Drawn in the panel when the term matches nothing. Null draws no panel at all.</summary>
    [Parameter] public RenderFragment? NoResultsContent { get; set; }

    /// <summary>The sentence drawn beside the spinner while <see cref="SearchFunction"/> is running.</summary>
    [Parameter] public string? LoadingLabel { get; set; } = "Searching\u2026";

    /// <summary>
    /// The items matching a term, in the order given: everything for a blank term, otherwise the items the
    /// filter accepts. Throws <see cref="ArgumentNullException"/> for a null list or selector.
    /// </summary>
    /// <param name="items">The whole list.</param>
    /// <param name="query">What has been typed; blank means everything.</param>
    /// <param name="textSelector">The text matched against the term.</param>
    /// <param name="filter">An optional replacement for the contains rule.</param>
    /// <returns>The matching items.</returns>
    /// <remarks>
    /// Public so a page can fill a list by the same rule this component filters by: two copies of "what
    /// counts as a match" is a difference nobody would test for and the merchant would meet as a list that
    /// disagrees with the one beside it.
    /// </remarks>
    public static IReadOnlyList<TItem> Matches(
        IReadOnlyList<TItem> items,
        string? query,
        Func<TItem, string> textSelector,
        Func<TItem, string, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(textSelector);

        var term = query?.Trim() ?? string.Empty;

        if (term.Length == 0)
        {
            return items;
        }

        var predicate = filter
            ?? ((item, value) => textSelector(item).Contains(value, StringComparison.OrdinalIgnoreCase));

        return [.. items.Where(item => predicate(item, term))];
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Adopted when the PARAMETER changes, never when it differs from what was typed. A caller that
        // applies a selection lazily re-renders with its previous value while a new one is in flight, and
        // treating that echo as an instruction resets the field mid-typing — the failure the suggest
        // field and the range slider both paid for.
        if (_hasLastValue && object.Equals(_lastValue, Value))
        {
            return;
        }

        _hasLastValue = true;
        _lastValue = Value;
        _query = Value is null ? string.Empty : TextSelector(Value!);
        _open = false;
        CancelPendingClose();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // The list is a panel drawn over the page, not a block inserted under the field: an autocomplete
        // that pushes everything below it down every time a letter is typed is a form that jumps while
        // the merchant uses it. See PolarisPopup for why the panel is ours and not Polaris's.
        builder.OpenComponent<PolarisPopup>(0);
        builder.AddAttribute(1, nameof(PolarisPopup.Open), _open);
        builder.AddAttribute(2, nameof(PolarisPopup.Anchor), (RenderFragment)RenderField);
        builder.AddAttribute(3, nameof(PolarisPopup.ChildContent), (RenderFragment)RenderPanel);
        builder.AddAttribute(4, nameof(PolarisPopup.MaxBlockSize), "336px");
        builder.CloseComponent();
    }

    /// <summary>Draws the search field inside the element focus returns to.</summary>
    private void RenderField(RenderTreeBuilder stack)
    {
        // A plain wrapper, not a Polaris component: it exists only so the field's element can be captured
        // and focused, because a custom element's own ElementReference is not exposed by the wrapper.
        stack.OpenElement(10, "div");
        stack.AddAttribute(11, "data-polaris-autocomplete-field", string.Empty);
        stack.AddElementReferenceCapture(12, element => _fieldElement = element);

        // A text field rather than a search field, deliberately: a browser clears an
        // `<input type="search">` when Escape is pressed, which would wipe the chosen value a
        // millisecond after Escape put it back — measured in Chrome, not guessed at. The magnifier is
        // the field's own icon, so the control still reads as a search box.
        stack.OpenComponent<PolarisTextField>(13);
        stack.AddAttribute(14, nameof(PolarisTextField.Label), Label);
        stack.AddAttribute(15, nameof(PolarisTextField.Details), Details);
        stack.AddAttribute(16, nameof(PolarisTextField.Placeholder), Placeholder);
        stack.AddAttribute(17, nameof(PolarisTextField.Value), _query);
        stack.AddAttribute(18, nameof(PolarisTextField.Icon), "search");
        stack.AddAttribute(19, nameof(PolarisTextField.Disabled), Disabled);
        stack.AddAttribute(20, nameof(PolarisTextField.ReadOnly), ReadOnly);
        stack.AddAttribute(21, nameof(PolarisTextField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(this, OnInputAsync));
        stack.AddAttribute(22, nameof(PolarisTextField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, OnCommitAsync));
        stack.AddAttribute(23, nameof(PolarisTextField.OnKeyDown), EventCallback.Factory.Create<PolarisEventArgs>(this, OnFieldKeyDownAsync));
        stack.AddAttribute(24, nameof(PolarisTextField.OnFocus), EventCallback.Factory.Create<PolarisEventArgs>(this, OnFocusAsync));
        stack.AddAttribute(25, nameof(PolarisTextField.OnBlur), EventCallback.Factory.Create<PolarisEventArgs>(this, OnBlurAsync));
        stack.CloseComponent();

        stack.CloseElement();
    }

    /// <summary>Draws the rows, the spinner or the no-results content inside the popup's panel.</summary>
    private void RenderPanel(RenderTreeBuilder panel)
    {
        if (!_open)
        {
            // The popup keeps its panel element in the DOM while closed; the rows are not drawn into it,
            // so a closed autocomplete has no suggestion elements to find, count or tab into.
            return;
        }

        var items = ComputeVisible();

        panel.OpenComponent<PolarisStack>(100);
        panel.AddAttribute(101, nameof(PolarisStack.Gap), PolarisGap.None);
        panel.AddAttribute(102, nameof(PolarisStack.ChildContent), (RenderFragment)(rows => RenderRows(rows, items)));
        panel.CloseComponent();
    }

    /// <summary>Draws one row per match, plus the loading and empty states.</summary>
    // A row's frames are numbered from its index, because one render can draw any number of them and
    // every frame of a scope has to be in non-decreasing sequence order. The analyzer wants literals;
    // this is a case where a literal cannot be right.
#pragma warning disable ASP0006
    private void RenderRows(RenderTreeBuilder rows, IReadOnlyList<TItem> items)
    {
        // Entries are overwritten, never cleared: an element reference capture is not guaranteed to fire
        // again for an element the renderer reuses, so clearing here would wipe the references of rows
        // that are still on screen and leave the arrow keys with nothing to focus. An index beyond the
        // current list is never asked for — FocusRowAsync bounds-checks against the visible items.
        if (_searching)
        {
            RenderLoading(rows);
        }

        // Before the rows, so its frame number stays below theirs: a sequence number may not go
        // backwards inside one scope, and the rows are numbered from 200 up.
        if (items.Count == 0 && !_searching && NoResultsContent is not null)
        {
            rows.AddContent(190, NoResultsContent);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var position = index;
            var sequence = 200 + (index * 20);

            // The row is a plain div around the button for two reasons: it is the element whose reference
            // is captured for focus, and its mousedown selects before the field's blur can close the panel.
            // A click is too late — the mousedown that precedes it has already moved focus and run the
            // close, which would remove the button before its click arrives.
            rows.OpenElement(sequence, "div");
            rows.AddAttribute(sequence + 1, "data-polaris-autocomplete-row", string.Empty);
            rows.AddAttribute(
                sequence + 2,
                "onmousedown",
                EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(
                    this,
                    async _ => await SelectAsync(item)));
            rows.AddElementReferenceCapture(sequence + 3, element => _rowElements[position] = element);

            rows.OpenComponent<PolarisButton>(sequence + 4);
            rows.AddAttribute(sequence + 5, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
            rows.AddAttribute(sequence + 6, nameof(PolarisButton.InlineSize), PolarisButtonInlineSize.Fill);
            rows.AddAttribute(sequence + 7, nameof(PolarisButton.Disabled), Disabled);
            rows.AddAttribute(sequence + 8, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(
                this,
                async _ => await SelectAsync(item)));
            rows.AddAttribute(sequence + 9, nameof(PolarisButton.OnKeyDown), EventCallback.Factory.Create<PolarisEventArgs>(
                this,
                async args => await OnRowKeyDownAsync(position, item, args)));
            rows.AddAttribute(sequence + 10, nameof(PolarisButton.OnFocus), EventCallback.Factory.Create<PolarisEventArgs>(this, OnFocusAsync));
            rows.AddAttribute(sequence + 11, nameof(PolarisButton.OnBlur), EventCallback.Factory.Create<PolarisEventArgs>(this, OnBlurAsync));
            rows.AddAttribute(sequence + 12, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
            {
                if (ItemTemplate is null)
                {
                    label.AddContent(0, TextSelector(item));
                }
                else
                {
                    label.AddContent(1, ItemTemplate(item));
                }
            }));
            rows.CloseComponent();

            rows.CloseElement();
        }
    }
#pragma warning restore ASP0006

    /// <summary>Draws the spinner row shown while an asynchronous search is running.</summary>
    private void RenderLoading(RenderTreeBuilder rows)
    {
        rows.OpenComponent<PolarisStack>(120);
        rows.AddAttribute(121, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        rows.AddAttribute(122, nameof(PolarisStack.Gap), PolarisGap.Base);
        rows.AddAttribute(123, nameof(PolarisStack.ChildContent), (RenderFragment)(line =>
        {
            line.OpenComponent<PolarisSpinner>(0);
            line.AddAttribute(2, nameof(PolarisSpinner.AccessibilityLabel), LoadingLabel);
            line.CloseComponent();

            if (!string.IsNullOrWhiteSpace(LoadingLabel))
            {
                line.OpenComponent<PolarisText>(3);
                line.AddAttribute(4, nameof(PolarisText.Color), PolarisColor.Subdued);
                line.AddAttribute(5, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, LoadingLabel)));
                line.CloseComponent();
            }
        }));
        rows.CloseComponent();
    }

    /// <summary>The items the panel should draw: everything, filtered, then capped.</summary>
    private IReadOnlyList<TItem> ComputeVisible()
    {
        var matches = Matches(SearchFunction is null ? Items : _remoteItems, _query, TextSelector, FilterFunction);

        if (MaxSuggestions is int max && max > 0 && matches.Count > max)
        {
            return [.. matches.Take(max)];
        }

        return matches;
    }

    /// <summary>Finds the item whose text is exactly the committed term, if there is one.</summary>
    private bool TryFindExact(string text, out TItem item)
    {
        foreach (var candidate in SearchFunction is null ? Items : _remoteItems)
        {
            if (string.Equals(TextSelector(candidate).Trim(), text, StringComparison.OrdinalIgnoreCase))
            {
                item = candidate;
                return true;
            }
        }

        item = default!;
        return false;
    }

    /// <summary>Takes what was typed: opens or narrows the list, and asks the provider when there is one.</summary>
    private async Task OnInputAsync(PolarisEventArgs args)
    {
        if (Disabled || ReadOnly)
        {
            return;
        }

        _query = args.Value ?? string.Empty;
        _suppressNextCommit = false;
        _pendingFocusRow = null;
        CancelPendingClose();

        _open = _query.Length >= MinimumLength;

        if (!_open)
        {
            _searching = false;
            CancelSearch();
            return;
        }

        if (SearchFunction is not null)
        {
            await SearchAsync(_query);
        }
    }

    /// <summary>Runs one search, cancelling the one before it and dropping a result that has gone stale.</summary>
    private async Task SearchAsync(string query)
    {
        CancelSearch();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _searching = true;

        // A handler's render happens after it returns, so the spinner needs its own render before the
        // first await — otherwise the panel stays empty for the whole debounce and then shows the result.
        await InvokeAsync(StateHasChanged);

        try
        {
            if (DebounceMilliseconds > 0)
            {
                await Task.Delay(DebounceMilliseconds, cts.Token);
            }

            var results = await SearchFunction!(query, cts.Token);

            if (cts.IsCancellationRequested || _disposed)
            {
                return;
            }

            _remoteItems = results;
        }
        catch (OperationCanceledException)
        {
            // A newer term cancelled this search; the newer one owns the panel's state.
        }
        finally
        {
            if (!cts.IsCancellationRequested && !_disposed)
            {
                _searching = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    /// Takes a committed term — Enter or blur. Chooses an exact match, hands over a custom value, or puts
    /// the field back to what is actually chosen.
    /// </summary>
    private async Task OnCommitAsync(PolarisEventArgs args)
    {
        if (_suppressNextCommit)
        {
            // The change that follows a selection: the value has already been reported.
            _suppressNextCommit = false;
            return;
        }

        if (_movingFocus)
        {
            // The merchant is not committing: focus is on its way to a row, and the browser reports the
            // field's blur as a change on the way out.
            return;
        }

        var text = (args.Value ?? _query).Trim();
        _open = false;
        CancelPendingClose();

        if (text.Length == 0)
        {
            // Clearing the text is how the merchant clears the selection; there is no accessory slot on
            // the search field to hold a clear button, and deleting the text is the same gesture.
            _query = string.Empty;

            if (Value is not null)
            {
                await ValueChanged.InvokeAsync(default);
            }

            return;
        }

        if (TryFindExact(text, out var exact))
        {
            // A commit is allowed to choose with the list already closed: the panel closes as the field
            // blurs, and the exact name that was typed is still a choice the merchant made.
            await SelectAsync(exact, fromField: true);
            return;
        }

        if (AllowCustomValue)
        {
            await CustomValueEntered.InvokeAsync(text);
            return;
        }

        // Selection-only: text that is not an item is not a value, so the field goes back to the chosen
        // item's own text rather than showing a name the caller was never told about.
        RestoreQuery();
    }

    /// <summary>Handles the keys pressed while the field itself has focus.</summary>
    private async Task OnFieldKeyDownAsync(PolarisEventArgs args)
    {
        if (Disabled || ReadOnly)
        {
            return;
        }

        switch (args.Key)
        {
            case "ArrowDown":
                _open = true;
                await FocusRowAsync(0);
                break;

            case "ArrowUp":
                _open = true;
                await FocusRowAsync(ComputeVisible().Count - 1);
                break;

            case "Escape":
                CloseAndRestore();
                break;

            case "Enter":
                // Enter in the field takes an exact match first, then the first match — which is what
                // makes a narrowed list one key press away. The change event that follows finds the text
                // now equal to the chosen item and is suppressed, so the value is reported once.
                if (TryFindExact(_query.Trim(), out var exact))
                {
                    await SelectAsync(exact, fromField: true);
                    break;
                }

                var visible = ComputeVisible();

                if (_open && visible.Count > 0)
                {
                    await SelectAsync(visible[0], fromField: true);
                }

                break;
        }
    }

    /// <summary>Handles the keys pressed while a row button has focus.</summary>
    private async Task OnRowKeyDownAsync(int index, TItem item, PolarisEventArgs args)
    {
        switch (args.Key)
        {
            // Enter and Space are chosen here as well as by the browser's own click on the button. Both
            // are the same choice, and the second one is ignored because the panel has closed — but a
            // headless Enter does not always produce the click at all, so the keyboard path cannot rely
            // on it alone.
            case "Enter":
            case " ":
                await SelectAsync(item);
                break;

            case "ArrowDown":
                await FocusRowAsync(index + 1);
                break;

            case "ArrowUp":
                if (index <= 0)
                {
                    await FocusFieldAsync();
                }
                else
                {
                    await FocusRowAsync(index - 1);
                }

                break;

            case "Escape":
                CloseAndRestore();
                await FocusFieldAsync();
                break;
        }
    }

    /// <summary>Reports the chosen item, closes the list and puts focus back in the field.</summary>
    /// <param name="item">The item to report.</param>
    /// <param name="fromField">
    /// True when the choice came from the field's own commit. A row may only be chosen while the panel is
    /// open, which is what makes the click that follows a selection a no-op rather than a second report.
    /// </param>
    private async Task SelectAsync(TItem item, bool fromField = false)
    {
        if (!_open && !fromField)
        {
            return;
        }

        _open = false;
        _searching = false;
        CancelSearch();
        CancelPendingClose();

        _suppressNextCommit = true;
        _query = TextSelector(item);

        // Only a choice made from a row has displaced focus. Asking for it back after the field's own
        // Enter would call focus() on the element that already has it, which fires no focus event — and
        // the flag that suppresses the blur-as-commit would then never be cleared.
        if (!fromField)
        {
            _pendingFieldFocus = true;
        }

        await ValueChanged.InvokeAsync(item);

        // The EventCallback may have re-rendered the page and this component with it; a render here is
        // what lets OnAfterRenderAsync move the focus the selection displaced.
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Closes the list and puts the chosen item's text back in the field.</summary>
    private void CloseAndRestore()
    {
        _open = false;
        _pendingFocusRow = null;
        CancelPendingClose();
        RestoreQuery();
    }

    /// <summary>Puts the chosen item's text — or nothing — in the field.</summary>
    private void RestoreQuery() => _query = Value is null ? string.Empty : TextSelector(Value!);

    /// <summary>Queues a row to be focused by the render that draws it.</summary>
    private Task FocusRowAsync(int index)
    {
        if (!_open || index < 0 || index >= ComputeVisible().Count)
        {
            return Task.CompletedTask;
        }

        _pendingFocusRow = index;
        _pendingFieldFocus = false;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Queues the field to be focused by the next render.</summary>
    private Task FocusFieldAsync()
    {
        _pendingFocusRow = null;
        _pendingFieldFocus = true;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Cancels the delayed close, because focus has arrived somewhere inside the component.</summary>
    private Task OnFocusAsync(PolarisEventArgs args)
    {
        // Focus has arrived where the programmatic move was aiming, so the next blur is a real one.
        _movingFocus = false;
        CancelPendingClose();
        return Task.CompletedTask;
    }

    /// <summary>Starts the delayed close, because focus has left a control.</summary>
    private Task OnBlurAsync(PolarisEventArgs args)
    {
        ScheduleClose();
        return Task.CompletedTask;
    }

    /// <summary>Closes the list unless focus comes back inside the component first.</summary>
    private void ScheduleClose()
    {
        CancelPendingClose();

        var cts = new CancellationTokenSource();
        _closeCts = cts;
        _ = CloseLaterAsync(cts);
    }

    /// <summary>The delayed half of <see cref="ScheduleClose"/>.</summary>
    private async Task CloseLaterAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(CloseDelayMilliseconds, source.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (source.IsCancellationRequested || _disposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(() =>
            {
                if (!source.IsCancellationRequested && _open)
                {
                    _open = false;
                    StateHasChanged();
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // The circuit went away between the delay and the render.
        }
    }

    /// <summary>Stops a pending delayed close.</summary>
    private void CancelPendingClose()
    {
        _closeCts?.Cancel();
        _closeCts?.Dispose();
        _closeCts = null;
    }

    /// <summary>Stops a search that is still running.</summary>
    private void CancelSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingFieldFocus)
        {
            _pendingFieldFocus = false;
            _movingFocus = true;

            if (!await Interop.FocusWithinAsync(_fieldElement))
            {
                // Nothing took focus, so no focus event will clear the flag.
                _movingFocus = false;
            }

            return;
        }

        if (_pendingFocusRow is not { } index || !_rowElements.TryGetValue(index, out var element))
        {
            // No row has been captured yet — an asynchronous search can open the panel before its rows
            // exist. The request stays queued and is served by the render that draws them.
            return;
        }

        _pendingFocusRow = null;
        _movingFocus = true;

        if (!await Interop.FocusWithinAsync(element))
        {
            _movingFocus = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingClose();
        CancelSearch();
    }
}
