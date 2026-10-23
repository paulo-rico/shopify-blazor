using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// A select of the values a record can carry, with an "Add new" button beside it that opens a dialog
/// holding the list of values already saved and a field for a new one.
/// </summary>
/// <remarks>
/// <para>
/// The third shape for the same problem, and deliberately the plainest of the three: the list is a real
/// <see cref="PolarisSelect"/>, so choosing is a native dropdown that a keyboard and a screen reader
/// already understand, and adding is a separate, explicit act rather than something a search box infers
/// from what has been typed. Use it where the vocabulary is a directory — a supplier, a warehouse, a
/// courier — rather than a handful of tags.
/// </para>
/// <para>
/// <b>The dialog is opened declaratively.</b> <see cref="PolarisModal"/> can only be opened by a user
/// gesture, and a button's <c>commandFor</c> is the sanctioned way; it is also the only way that cannot
/// fall foul of the rule, because the open is the press. Closing after a save goes through the element's
/// own <c>hideOverlay()</c>, since that is code rather than a gesture.
/// </para>
/// <para>
/// <b>OK and Cancel are direct children of the modal, not of its body.</b> Polaris assigns a
/// <c>slot</c> attribute to the host's named slot only for the host's own children, so a button nested
/// inside the dialog body's stack has its slot attribute ignored and the dialog renders with no actions
/// at all — measured in a browser, and the reason those two buttons are siblings of the body.
/// </para>
/// <para>
/// <b>A name that already exists is not added twice.</b> Comparison ignores case and surrounding spaces
/// — the same rule the database and the service apply — and the existing spelling is selected instead,
/// so "acme metals" typed by hand lands on "Acme Metals" rather than beside it. The caller is told
/// through <see cref="DuplicateIgnored"/> rather than being left to wonder why the list did not grow.
/// </para>
/// <para>
/// <b>The "nothing chosen" option carries a sentinel, not an empty value.</b> Measured in the browser:
/// an <c>s-option</c> whose <c>value</c> is empty does not report an empty string — the select reports the
/// option's own <em>label</em>, so a "No supplier" row would have handed the caller the literal supplier
/// "No supplier". The sentinel is unmistakeably not a value, and <see cref="Value"/> reports <c>null</c>
/// for it, so the rest of the app keeps one spelling for "nothing chosen".
/// </para>
/// <para>
/// The component never touches a store: <see cref="OptionAdded"/> is the caller's cue to persist, and it
/// is raised <em>before</em> <see cref="ValueChanged"/>, so a caller that saves and then selects works in
/// the order the dialog implies.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisAddableSelect Label="Supplier"
///                        Value="@_supplier"
///                        Options="_suppliers"
///                        EmptyOptionLabel="No supplier"
///                        DialogHeading="Add a supplier"
///                        DialogLabel="New supplier"
///                        DialogListLabel="Your suppliers"
///                        DialogConfirmLabel="Add supplier"
///                        ValueChanged="@(value => _supplier = value ?? string.Empty)"
///                        OptionAdded="@AddSupplierAsync" /&gt;
/// </code>
/// </example>
public sealed class PolarisAddableSelect : ComponentBase
{
    /// <summary>
    /// The value the "nothing chosen" option carries.
    /// </summary>
    /// <remarks>
    /// Not an empty string, and not by preference: <c>s-option</c> with an empty <c>value</c> reports its
    /// own label as the value, measured in a browser — which would have made the merchant's supplier the
    /// words "No supplier". The sentinel also cannot collide with a real value, because a name is what a
    /// merchant types into the dialog and this is not one.
    /// </remarks>
    public const string EmptyOptionValue = "__polaris-addable-select:none__";

    /// <summary>A reference to the dialog, so a save can close it.</summary>
    private PolarisModal? _dialog;

    /// <summary>What has been typed into the dialog's field.</summary>
    private string _typed = string.Empty;

    /// <summary>Why the last save was refused, shown on the field.</summary>
    private string? _error;

    /// <summary>The dialog's id, fixed for the component's life so the button can address it.</summary>
    private string? _generatedId;

    /// <summary>The select's accessible label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text below the select.</summary>
    [Parameter] public string? Details { get; set; }

    /// <summary>
    /// The label of the "nothing chosen" option, which is also how a chosen value is cleared. Null
    /// renders no such option, which means the value can be set once and never unset.
    /// </summary>
    [Parameter] public string? EmptyOptionLabel { get; set; }

    /// <summary>The chosen value, which the caller owns. Null means nothing is chosen.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>The values already saved, in the order the select should offer them.</summary>
    [Parameter] public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>Prevents both the select and the add button from being used.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>The add button's visible label.</summary>
    [Parameter] public string? AddButtonLabel { get; set; } = "Add new";

    /// <summary>The dialog's heading. It is also the dialog's accessible name.</summary>
    [Parameter] public string? DialogHeading { get; set; } = "Add a new value";

    /// <summary>The caption above the list of values already saved.</summary>
    [Parameter] public string? DialogListLabel { get; set; } = "Already saved";

    /// <summary>Shown in the dialog when nothing has been saved yet.</summary>
    [Parameter] public string? DialogEmptyLabel { get; set; } = "Nothing has been saved yet.";

    /// <summary>The label of the dialog's text field.</summary>
    [Parameter] public string? DialogFieldLabel { get; set; } = "New value";

    /// <summary>Help text below the dialog's text field.</summary>
    [Parameter] public string? DialogFieldDetails { get; set; }

    /// <summary>The label of the dialog's confirm button.</summary>
    [Parameter] public string? DialogConfirmLabel { get; set; } = "OK";

    /// <summary>
    /// Extra content at the top of the dialog's body, for a caller that needs to manage the values
    /// themselves — an editable grid of what is already saved, for instance.
    /// </summary>
    /// <remarks>
    /// A fragment rather than a slot: the modal body is an ordinary container, so content placed here
    /// lands inside it. The component takes no interest in what the content does; anything it changes is
    /// the caller's to persist, and the dialog reads whatever <see cref="Options"/> the caller passes next.
    /// </remarks>
    [Parameter] public RenderFragment? DialogContent { get; set; }

    /// <summary>The label of the dialog's cancel button.</summary>
    [Parameter] public string? CancelLabel { get; set; } = "Cancel";

    /// <summary>Shown on the field when the confirm button is pressed with it empty.</summary>
    [Parameter] public string? BlankMessage { get; set; } = "Enter a value first.";

    /// <summary>Raised when the select changes, or when the dialog settles on a value. Null clears it.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// Raised with the trimmed new value when the dialog confirms one that is not already in
    /// <see cref="Options"/>. The caller persists it and adds it to its list; the component does not.
    /// </summary>
    [Parameter] public EventCallback<string> OptionAdded { get; set; }

    /// <summary>
    /// Raised with the existing value when the dialog is given something already in
    /// <see cref="Options"/> — which is selected and not added. Wire it if the merchant should be told.
    /// </summary>
    [Parameter] public EventCallback<string> DuplicateIgnored { get; set; }

    /// <summary>The dialog's element id. Generated when the caller does not supply one.</summary>
    [Parameter] public string? Id { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() =>
        _generatedId = Id ?? $"polaris-addable-select-{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The caller may supply Id after the first render, in which case it wins — the attribute is
        // re-read every render anyway, and the button's commandFor is rendered from the same value.
        if (!string.IsNullOrEmpty(Id))
        {
            _generatedId = Id;
        }
    }

    /// <summary>The id the select's dialog carries, and the one the add button addresses.</summary>
    private string DialogId => _generatedId ?? "polaris-addable-select";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisGrid>(0);
        builder.AddAttribute(1, nameof(PolarisGrid.GridTemplateColumns), "1fr auto");
        builder.AddAttribute(2, nameof(PolarisGrid.Gap), "base");

        // Bottom-aligned, so the button sits on the select's last line rather than its caption.
        builder.AddAttribute(3, nameof(PolarisGrid.AlignItems), "end");
        builder.AddAttribute(4, nameof(PolarisGrid.ChildContent), (RenderFragment)(grid =>
        {
            grid.OpenComponent<PolarisSelect>(10);
            grid.AddAttribute(11, nameof(PolarisSelect.Label), Label);
            grid.AddAttribute(12, nameof(PolarisSelect.Details), Details);

            // The select is shown the sentinel rather than null when nothing is chosen, so its own state
            // reads "no value" — the display is then the EmptyOptionLabel row, which is the honest way to
            // say "none chosen" once a merchant has been able to choose one.
            grid.AddAttribute(
                13,
                nameof(PolarisSelect.Value),
                string.IsNullOrEmpty(Value)
                    ? (EmptyOptionLabel is null ? null : EmptyOptionValue)
                    : Value);
            grid.AddAttribute(14, nameof(PolarisSelect.Disabled), Disabled);
            grid.AddAttribute(15, nameof(PolarisSelect.OnChange), SelectChangedCallback());
            grid.AddAttribute(16, nameof(PolarisSelect.ChildContent), (RenderFragment)(options =>
            {
                if (EmptyOptionLabel is not null)
                {
                    // The sentinel value is how "nothing chosen" is expressed — not the select's own
                    // placeholder, because a merchant who has chosen a value must be able to choose
                    // "none" again, and a placeholder disappears the moment the first choice is made.
                    options.OpenComponent<PolarisOption>(20);
                    options.AddAttribute(21, nameof(PolarisOption.Value), EmptyOptionValue);
                    options.AddAttribute(22, nameof(PolarisOption.ChildContent), (RenderFragment)(label =>
                        label.AddContent(23, EmptyOptionLabel)));
                    options.CloseComponent();
                }

                foreach (var option in Options)
                {
                    options.OpenComponent<PolarisOption>(30);
                    options.AddAttribute(31, nameof(PolarisOption.Value), option);
                    options.AddAttribute(32, nameof(PolarisOption.ChildContent), (RenderFragment)(label =>
                        label.AddContent(33, option)));
                    options.CloseComponent();
                }
            }));
            grid.CloseComponent();

            grid.OpenComponent<PolarisButton>(40);
            grid.AddAttribute(41, nameof(PolarisButton.Icon), "plus");
            grid.AddAttribute(42, nameof(PolarisButton.Disabled), Disabled);

            // The open is the press: a modal cannot be opened from code on load, and this is the form
            // that cannot break that rule.
            grid.AddAttribute(43, nameof(PolarisButton.CommandFor), DialogId);
            grid.AddAttribute(44, nameof(PolarisButton.Command), PolarisCommand.Show);
            grid.AddAttribute(45, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
                label.AddContent(46, AddButtonLabel)));
            grid.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<PolarisModal>(50);
        builder.AddAttribute(51, nameof(PolarisModal.Id), DialogId);
        builder.AddAttribute(52, nameof(PolarisModal.Heading), DialogHeading);
        builder.AddAttribute(53, nameof(PolarisModal.OnHide), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            async _ => await ResetAsync()));
        builder.AddAttribute(54, nameof(PolarisModal.ChildContent), (RenderFragment)(dialog =>
        {
            dialog.OpenComponent<PolarisStack>(60);
            dialog.AddAttribute(61, nameof(PolarisStack.Gap), PolarisGap.Base);
            dialog.AddAttribute(62, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
            {
                // Whatever the caller wants above the field: a grid of the saved values, a note, nothing.
                // It is here rather than in a slot because the modal body is a plain container and a
                // fragment carries no slot attribute of its own.
                if (DialogContent is not null)
                {
                    content.AddContent(65, DialogContent);
                }

                content.OpenComponent<PolarisText>(70);
                content.AddAttribute(71, nameof(PolarisText.Color), PolarisColor.Subdued);
                content.AddAttribute(72, nameof(PolarisText.ChildContent), (RenderFragment)(caption =>
                    caption.AddContent(73, DialogListLabel)));
                content.CloseComponent();

                if (Options.Count == 0)
                {
                    content.OpenComponent<PolarisText>(80);
                    content.AddAttribute(81, nameof(PolarisText.ChildContent), (RenderFragment)(empty =>
                        empty.AddContent(82, DialogEmptyLabel)));
                    content.CloseComponent();
                }
                else
                {
                    // A list, not another control: the select behind the dialog is where a value is
                    // chosen, and a second chooser in a dialog that also creates would be two ways to
                    // answer one question. The modal body scrolls when the list outgrows it.
                    content.OpenComponent<PolarisUnorderedList>(90);
                    content.AddAttribute(91, nameof(PolarisUnorderedList.ChildContent), (RenderFragment)(list =>
                    {
                        foreach (var option in Options)
                        {
                            list.OpenComponent<PolarisListItem>(100);
                            list.AddAttribute(101, nameof(PolarisListItem.ChildContent), (RenderFragment)(item =>
                                item.AddContent(102, option)));
                            list.CloseComponent();
                        }
                    }));
                    content.CloseComponent();
                }

                content.OpenComponent<PolarisTextField>(110);
                content.AddAttribute(111, nameof(PolarisTextField.Label), DialogFieldLabel);
                content.AddAttribute(112, nameof(PolarisTextField.Details), DialogFieldDetails);
                content.AddAttribute(113, nameof(PolarisTextField.Error), _error);
                content.AddAttribute(114, nameof(PolarisTextField.Value), _typed);
                content.AddAttribute(115, nameof(PolarisTextField.OnInput), TypedCallback());
                content.AddAttribute(116, nameof(PolarisTextField.OnChange), TypedCallback());
                content.CloseComponent();
            }));
            dialog.CloseComponent();

            // The actions are Polaris slots, and BOTH halves of that matter: the slot attribute goes on
            // the button itself (a validated slot rejects a wrapper), and the button must be a DIRECT CHILD
            // of the modal. A slot attribute on a button nested inside the body's stack is ignored, so the
            // dialog renders with no OK and no Cancel at all — measured, and the reason this block is a
            // sibling of the stack above rather than part of it.
            dialog.OpenComponent<PolarisButton>(120);
            dialog.AddAttribute(121, nameof(PolarisButton.Slot), "primary-action");
            dialog.AddAttribute(122, nameof(PolarisButton.Variant), PolarisButtonVariant.Primary);
            dialog.AddAttribute(123, nameof(PolarisButton.OnClick), ConfirmCallback());
            dialog.AddAttribute(124, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
                label.AddContent(125, DialogConfirmLabel)));
            dialog.CloseComponent();

            dialog.OpenComponent<PolarisButton>(130);
            dialog.AddAttribute(131, nameof(PolarisButton.Slot), "secondary-actions");
            dialog.AddAttribute(132, nameof(PolarisButton.CommandFor), DialogId);
            dialog.AddAttribute(133, nameof(PolarisButton.Command), PolarisCommand.Hide);
            dialog.AddAttribute(134, nameof(PolarisButton.ChildContent), (RenderFragment)(label =>
                label.AddContent(135, CancelLabel)));
            dialog.CloseComponent();
        }));

        // The reference comes after every attribute, never between them: a capture is a frame of its own,
        // and an attribute added after one throws "Attributes may only be added immediately after frames
        // of type Element or Component" — at render time, not at compile time.
        builder.AddComponentReferenceCapture(56, reference => _dialog = (PolarisModal)reference);

        builder.CloseComponent();
    }

    private EventCallback<PolarisEventArgs> SelectChangedCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, SelectChangedAsync);

    private EventCallback<PolarisEventArgs> TypedCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, TypedAsync);

    private EventCallback<PolarisEventArgs> ConfirmCallback() =>
        EventCallback.Factory.Create<PolarisEventArgs>(this, ConfirmAsync);

    private Task SelectChangedAsync(PolarisEventArgs args)
    {
        // The sentinel option means "nothing chosen", and that state travels through the app as null. An
        // option the merchant typed can never equal the sentinel, so there is nothing to disambiguate.
        var chosen = string.IsNullOrWhiteSpace(args.Value) || args.Value == EmptyOptionValue
            ? null
            : args.Value;

        return ValueChanged.InvokeAsync(chosen);
    }

    private Task TypedAsync(PolarisEventArgs args)
    {
        _typed = args.Value ?? string.Empty;

        // Editing is an answer to the complaint: the message stands until the text changes, or it
        // accuses a field the merchant has already corrected.
        if (_error is not null)
        {
            _error = null;
        }

        return Task.CompletedTask;
    }

    private async Task ConfirmAsync(PolarisEventArgs args)
    {
        var trimmed = _typed.Trim();

        if (trimmed.Length == 0)
        {
            _error = BlankMessage;

            return;
        }

        // Matched without regard to case or surrounding spaces, exactly as the service and the database
        // compare it: "acme metals" typed by hand must land on "Acme Metals" rather than beside it.
        var existing = Options.FirstOrDefault(option =>
            string.Equals(option.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            await CloseAsync();
            await ValueChanged.InvokeAsync(existing);

            if (DuplicateIgnored.HasDelegate)
            {
                await DuplicateIgnored.InvokeAsync(existing);
            }

            return;
        }

        // Persistence first, selection second: the caller's handler saves the value and refreshes the
        // list, so by the time the select is told what to show, the option it must show exists.
        await OptionAdded.InvokeAsync(trimmed);
        await ValueChanged.InvokeAsync(trimmed);

        await CloseAsync();
    }

    /// <summary>Closes the dialog through the element's own method, since a save is not a gesture.</summary>
    private async Task CloseAsync()
    {
        if (_dialog is not null)
        {
            await _dialog.HideAsync();
        }
    }

    /// <summary>Forgets the half-typed value and any complaint, so a reopened dialog starts clean.</summary>
    private Task ResetAsync()
    {
        _typed = string.Empty;

        if (_error is not null)
        {
            _error = null;
        }

        return Task.CompletedTask;
    }
}
