using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Polaris.Blazor.Components;

/// <summary>
/// A dual-handle numeric range control: one track, two handles and the interval between them.
/// </summary>
/// <remarks>
/// <para>
/// Polaris has no slider of any kind — the runtime that this toolkit wraps defines no range, slider
/// or track element — so this is composed from two native <c>&lt;input type="range"&gt;</c> elements
/// laid over one track. That is deliberate rather than convenient: a native range input already has
/// what a hand-built one has to recreate, and usually gets wrong — arrow-key stepping, Home and End,
/// touch dragging, a platform thumb, an announced role and value, and a focus ring.
/// </para>
/// <para>
/// The two inputs cannot cross. Each end is clamped against the other, so the control always
/// describes a real interval; and when the interval has collapsed to a point, the handle that can
/// still move is raised above the other, because otherwise the thumb on top is the one that cannot
/// move in the direction the merchant needs.
/// </para>
/// <para>
/// Everything visual that is not a native control lives in <c>polaris-range-slider.css</c>, and the
/// interval itself is written inline as two custom properties. That split is the same one the
/// accordion uses: with no stylesheet the control is still a working slider that merely looks
/// unpolished, rather than a track with no handles.
/// </para>
/// <para>
/// The slider knows nothing about grids or filters. It emits a <see cref="PolarisNumberRange"/>, and
/// turning that into a filter is <see cref="PolarisDataFilter"/>'s job — which is what makes it
/// reusable for a price band on a product screen as well.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisRangeSlider Label="Silver content"
///                      Min="0" Max="100" Step="0.5"
///                      Unit="%" Format="N1"
///                      @bind-Value="Silver" /&gt;
/// </code>
/// </example>
public sealed class PolarisRangeSlider : ComponentBase
{
    private static int _instanceCount;

    private readonly string _instanceId = string.Create(
        CultureInfo.InvariantCulture,
        $"polaris-range-slider-{Interlocked.Increment(ref _instanceCount)}");

    private PolarisNumberRange _current;
    private PolarisNumberRange _domain;
    private PolarisNumberRange _committed;
    private PolarisNumberRange? _seenValue;
    private bool _initialised;

    /// <summary>The lower end of the domain. A value above <see cref="Maximum"/> swaps the two.</summary>
    [Parameter] public decimal Minimum { get; set; }

    /// <summary>The upper end of the domain.</summary>
    [Parameter] public decimal Maximum { get; set; } = 100m;

    /// <summary>
    /// The increment a handle moves by. <c>0</c> allows any value, which is what a fine-grained
    /// control over a measured quantity needs.
    /// </summary>
    [Parameter] public decimal Step { get; set; } = 1m;

    /// <summary>
    /// The selected interval. <c>null</c> means the whole domain, which is what a filter that is not
    /// applied yet should look like.
    /// </summary>
    [Parameter] public PolarisNumberRange? Value { get; set; }

    /// <summary>Raised with the new interval whenever a handle or a number moves.</summary>
    [Parameter] public EventCallback<PolarisNumberRange> ValueChanged { get; set; }

    /// <summary>Prevents every interaction, including the numeric inputs.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows a numeric field for each end, for values a handle cannot land on precisely.</summary>
    [Parameter] public bool ShowInputs { get; set; } = true;

    /// <summary>Shows tick marks along the track, at whole steps.</summary>
    [Parameter] public bool ShowTicks { get; set; }

    /// <summary>
    /// The control's title: shown above the track and used as the accessible name of the group the two
    /// handles belong to.
    /// </summary>
    /// <remarks>
    /// It has to be both. As an accessible name alone it is invisible, so a merchant sees two unnamed
    /// sliders in a filter panel — which is how this parameter came to be reported as "not displaying".
    /// The visible run is hidden from assistive technology, because the group already carries the name
    /// and announcing it twice says nothing new.
    /// </remarks>
    [Parameter] public string? Label { get; set; }

    /// <summary>The accessible name of the lower handle.</summary>
    [Parameter] public string LowerLabel { get; set; } = "Minimum";

    /// <summary>The accessible name of the upper handle.</summary>
    [Parameter] public string UpperLabel { get; set; } = "Maximum";

    /// <summary>A unit shown after each numeric input, such as <c>%</c> or <c>ct</c>.</summary>
    [Parameter] public string? Unit { get; set; }

    /// <summary>A standard or custom numeric format for the displayed values.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>The current interval, clamped to the domain.</summary>
    public PolarisNumberRange Current => _current;

    private decimal Lower => _current.Lower;

    private decimal Upper => _current.Upper;

    private decimal Span => DomainMaximum - DomainMinimum;

    private decimal DomainMinimum => Math.Min(Minimum, Maximum);

    private decimal DomainMaximum => Math.Max(Minimum, Maximum);

    /// <summary>True when both handles sit on the same value, so the interval cannot be narrowed.</summary>
    private bool IsCollapsed => Lower >= Upper;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var domain = new PolarisNumberRange(DomainMinimum, DomainMaximum);

        // The caller's value is adopted when it *changes*, and ignored when it is the same one it sent
        // last time. That distinction is the difference between a handle that follows the mouse and one
        // that fights it:
        //
        //   - A parent that applies a change lazily — a debounced filter, for instance — still holds the
        //     old value while the round trip is in flight. Comparing against "the value we last
        //     reported" treated that stale echo as an instruction and pulled the handle back to where it
        //     started, so each key press undid the previous one and a drag had to be released twice.
        //     Measured in a browser: the DOM value reverted to the pre-press value on every second press.
        //   - A genuine change — a cleared filter, a restored saved view, a page setting the range —
        //     always arrives as a different parameter, so it is still adopted.
        var callerChanged = !_initialised || Value != _seenValue;
        var domainChanged = domain != _domain;

        _domain = domain;
        _seenValue = Value;

        // A null value means "the whole domain", so a domain that arrives, grows or is replaced moves
        // the interval with it. Without that, a filter panel — which renders before its grid's rows do —
        // kept the placeholder range it first saw for ever.
        if (Value is null)
        {
            _current = !_initialised || callerChanged || domainChanged ? domain : Clamp(_current, domain);
        }
        else if (callerChanged)
        {
            _current = Clamp(Value.Value, domain);
        }
        else
        {
            // Nothing new from the caller: keep what the handles are showing, but not a value outside
            // the domain, which a reload can shrink under a handle that is already placed.
            _current = Clamp(_current, domain);
        }

        if (!_initialised || callerChanged)
        {
            // The interval is now the caller's own instruction, so the next committed change to the same
            // value is not news and must not be reported again.
            _committed = _current;
        }

        _initialised = true;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-polaris-range-slider", true);
        builder.AddAttribute(2, "data-collapsed", IsCollapsed ? "true" : "false");

        // The interval as two percentages: layout in the markup, paint in the stylesheet.
        builder.AddAttribute(3, "style", string.Create(
            CultureInfo.InvariantCulture,
            $"--polaris-range-start:{Percent(Lower):0.###}%;--polaris-range-end:{Percent(Upper):0.###}%"));

        if (Label is not null)
        {
            builder.AddAttribute(4, "role", "group");
            builder.AddAttribute(5, "aria-label", Label);

            BuildLabel(builder);
        }

        BuildTrack(builder);

        if (ShowTicks)
        {
            BuildTicks(builder);
        }

        if (ShowInputs)
        {
            BuildInputs(builder);
        }

        builder.CloseElement();
    }

    /// <summary>
    /// The visible title, above the track.
    /// </summary>
    /// <remarks>
    /// A label that lives only in an <c>aria-label</c> is not a label a merchant can see: in a filter
    /// panel the range control was the one control with no caption, so two sliders sat there unlabelled
    /// to the eye — which is what "the title isn't displaying" was. The run is <c>aria-hidden</c>
    /// because the group already carries the same text as its name.
    /// </remarks>
    private void BuildLabel(RenderTreeBuilder builder)
    {
        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "data-polaris-range-label", true);
        builder.AddAttribute(8, "aria-hidden", "true");

        // The gap is inline, like the interval: a missing stylesheet must cost looks, not layout. 8px is
        // Polaris's space-200 step, the label-to-control gap its own fields use.
        builder.AddAttribute(9, "style", "margin-block-end:8px");

        builder.OpenComponent<PolarisText>(10);
        builder.AddAttribute(11, nameof(PolarisText.Type), PolarisTextType.Strong);
        builder.AddAttribute(12, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, Label)));

        builder.CloseComponent();
        builder.CloseElement();
    }

    private void BuildTrack(RenderTreeBuilder builder)
    {
        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "data-polaris-range-track", true);

        BuildHandle(builder, lower: true);
        BuildHandle(builder, lower: false);

        builder.CloseElement();
    }

    private void BuildHandle(RenderTreeBuilder builder, bool lower)
    {
        builder.OpenElement(20, "input");
        builder.AddAttribute(21, "type", "range");
        builder.AddAttribute(22, "data-polaris-range-handle", lower ? "lower" : "upper");
        builder.AddAttribute(23, "min", Invariant(DomainMinimum));
        builder.AddAttribute(24, "max", Invariant(DomainMaximum));
        builder.AddAttribute(25, "step", Step > 0m ? Invariant(Step) : "any");
        builder.AddAttribute(26, "value", Invariant(lower ? Lower : Upper));
        builder.AddAttribute(27, "aria-label", lower ? LowerLabel : UpperLabel);

        if (Unit is not null || Format is not null)
        {
            builder.AddAttribute(28, "aria-valuetext", ValueText(lower ? Lower : Upper));
        }

        if (Disabled)
        {
            builder.AddAttribute(29, "disabled", true);
        }

        // Which handle wins a click when the interval has collapsed to a point. The thumb that CAN
        // still move is the one raised: at the top of the domain that is the lower handle, anywhere
        // else it is the upper one. Without this the control can be left with both handles on the
        // last value and no way to open them again.
        if (IsCollapsed)
        {
            var raised = Upper >= DomainMaximum ? lower : !lower;
            builder.AddAttribute(30, "style", string.Create(CultureInfo.InvariantCulture, $"z-index:{(raised ? 3 : 2)}"));
        }

        // Two events, and the split is the whole of "do not query while dragging":
        //
        //   - `input` fires on every pixel of a drag (measured: four events across one drag, and no
        //     `change` among them), so it may only preview — move this control's own interval and
        //     re-render, without telling the caller anything.
        //   - `change` fires once, on release (measured alongside pointerup and mouseup), so that is
        //     where the new interval is reported and a caller re-queries the data exactly once.
        //
        // A keyboard user gets both per key press (measured: `input` and `change` at the same instant),
        // which is correct — a key press is a finished change, not a drag.
        builder.AddAttribute(31, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(
            this,
            args => PreviewAsync(args, lower)));

        builder.AddAttribute(32, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
            this,
            args => MoveAsync(args, lower)));

        builder.CloseElement();
    }

    private void BuildTicks(RenderTreeBuilder builder)
    {
        // A datalist is the native way to draw ticks, so the browser places them against the same
        // track geometry it uses for the thumb. It is also inert: nothing here can be tabbed to.
        var ticks = TickValues();

        if (ticks.Count == 0)
        {
            return;
        }

        builder.OpenElement(40, "datalist");
        builder.AddAttribute(41, "id", _instanceId);

        foreach (var tick in ticks)
        {
            builder.OpenElement(42, "option");
            builder.AddAttribute(43, "value", Invariant(tick));
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void BuildInputs(RenderTreeBuilder builder)
    {
        builder.OpenElement(50, "div");
        builder.AddAttribute(51, "data-polaris-range-inputs", true);

        // Two equal columns, stated inline. Measured: an `s-stack direction="inline"` around the two
        // fields laid them out one above the other, at full width each, because a Polaris host computes
        // to `display: contents` and its contents are not the flex items the stack expects. A grid of
        // real cells cannot be flattened away, so each field fills a column and the pair stays on one
        // line whatever the framework does inside its shadow roots. `minmax(0, 1fr)` rather than `1fr`
        // so a field's own minimum does not push the second one onto the next line.
        builder.AddAttribute(52, "style", "display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);column-gap:8px;margin-block-start:8px");

        builder.OpenElement(53, "div");
        builder.AddAttribute(54, "data-polaris-range-input", "lower");
        BuildNumberField(builder, lower: true);
        builder.CloseElement();

        builder.OpenElement(55, "div");
        builder.AddAttribute(56, "data-polaris-range-input", "upper");
        BuildNumberField(builder, lower: false);
        builder.CloseElement();

        builder.CloseElement();
    }

    private void BuildNumberField(RenderTreeBuilder builder, bool lower)
    {
        var label = lower ? LowerLabel : UpperLabel;

        builder.OpenComponent<PolarisNumberField>(0);
        builder.AddAttribute(1, nameof(PolarisNumberField.Label), label);
        builder.AddAttribute(2, nameof(PolarisNumberField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(3, nameof(PolarisNumberField.Value), Invariant(lower ? Lower : Upper));
        builder.AddAttribute(4, nameof(PolarisNumberField.Min), (double)DomainMinimum);
        builder.AddAttribute(5, nameof(PolarisNumberField.Max), (double)DomainMaximum);
        builder.AddAttribute(6, nameof(PolarisNumberField.Step), Step > 0m ? (double)Step : null);
        builder.AddAttribute(7, nameof(PolarisNumberField.Suffix), Unit);
        builder.AddAttribute(8, nameof(PolarisNumberField.Disabled), Disabled);
        builder.AddAttribute(9, nameof(PolarisNumberField.OnChange), EventCallback.Factory.Create<Events.PolarisEventArgs>(
            this,
            lower ? LowerTypedAsync : UpperTypedAsync));
        builder.CloseComponent();
    }

    /// <summary>
    /// Moves one end of the interval without reporting it: the visual half of a drag in progress.
    /// </summary>
    private Task PreviewAsync(ChangeEventArgs args, bool lower)
    {
        if (!TryParse(args.Value, out var value))
        {
            return Task.CompletedTask;
        }

        var clamped = Clamp(RangeFor(lower, value), Domain());

        if (clamped == _current)
        {
            return Task.CompletedTask;
        }

        // Only this component re-renders: the caller is told on release, so nothing upstream re-queries
        // the data on every pixel. The interval is written inline, so the highlighted span follows the
        // thumb from this re-render alone.
        _current = clamped;
        StateHasChanged();

        return Task.CompletedTask;
    }

    /// <summary>Reports the interval a drag or a key press has finished on.</summary>
    private Task MoveAsync(ChangeEventArgs args, bool lower)
        => TryParse(args.Value, out var value)
            ? CommitAsync(RangeFor(lower, value))
            : Task.CompletedTask;

    private Task LowerTypedAsync(Events.PolarisEventArgs args)
        => TryParse(args.Value, out var value)
            ? CommitAsync(RangeFor(lower: true, value))
            : Task.CompletedTask;

    private Task UpperTypedAsync(Events.PolarisEventArgs args)
        => TryParse(args.Value, out var value)
            ? CommitAsync(RangeFor(lower: false, value))
            : Task.CompletedTask;

    /// <summary>
    /// Clamps a new end against the other one: a handle dragged past its neighbour stops there, so the
    /// merchant keeps hold of the handle they grabbed instead of watching the two exchange places.
    /// </summary>
    private PolarisNumberRange RangeFor(bool lower, decimal value)
        => lower
            ? PolarisNumberRange.Create(Math.Min(value, Upper), Upper)
            : PolarisNumberRange.Create(Lower, Math.Max(value, Lower));

    private async Task CommitAsync(PolarisNumberRange range)
    {
        var clamped = Clamp(range, Domain());

        // Compared against the last interval *reported*, not against what the control is showing: a drag
        // has already previewed the value into `_current`, so comparing with that would treat the release
        // as "nothing changed" and never tell the caller at all.
        var changed = clamped != _committed;

        _current = clamped;
        _committed = clamped;

        if (changed && ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(clamped);
        }
    }

    private PolarisNumberRange Domain() => new(DomainMinimum, DomainMaximum);

    private PolarisNumberRange Clamp(PolarisNumberRange range, PolarisNumberRange domain)
    {
        var lower = Math.Clamp(range.Lower, domain.Lower, domain.Upper);
        var upper = Math.Clamp(range.Upper, domain.Lower, domain.Upper);

        if (Step > 0m)
        {
            lower = Snap(lower);
            upper = Snap(upper);
        }

        return PolarisNumberRange.Create(lower, upper);
    }

    private decimal Snap(decimal value)
    {
        var steps = Math.Round((value - DomainMinimum) / Step, MidpointRounding.AwayFromZero);
        return Math.Clamp(DomainMinimum + (steps * Step), DomainMinimum, DomainMaximum);
    }

    private decimal Percent(decimal value)
    {
        if (Span <= 0m)
        {
            return 0m;
        }

        return Math.Clamp((value - DomainMinimum) / Span * 100m, 0m, 100m);
    }

    private List<decimal> TickValues()
    {
        var ticks = new List<decimal>();

        if (Step <= 0m || Span <= 0m)
        {
            return ticks;
        }

        var count = Span / Step;

        // Above a couple of dozen marks a tick chart is a grey smear, and the browser spends time
        // laying out options nobody can count.
        if (count > 24m)
        {
            return ticks;
        }

        for (var tick = DomainMinimum; tick <= DomainMaximum; tick += Step)
        {
            ticks.Add(tick);
        }

        return ticks;
    }

    private string ValueText(decimal value)
    {
        var number = value.ToString(Format, CultureInfo.InvariantCulture);
        return Unit is null ? number : string.Concat(number, " ", Unit);
    }

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool TryParse(object? value, out decimal result)
        => decimal.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Number | NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
}
