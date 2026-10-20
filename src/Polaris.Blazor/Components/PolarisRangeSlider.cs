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
    private PolarisNumberRange? _reported;
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
    /// The control's label, which names the group the two handles belong to. Without it a screen
    /// reader announces two sliders with no idea what they measure.
    /// </summary>
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

        // No caller value means "the whole domain" — so the interval has to follow the domain when the
        // domain changes, which it does whenever the data behind it arrives or is replaced.
        //
        // This is not a refinement. A filter panel renders before its grid's rows do, so the bounds it
        // reads are a placeholder: a slider that only ever clamped would keep that placeholder range
        // for ever, showing both handles parked at the first value of a domain nobody can widen — a
        // range filter that matches everything, cannot be dragged and looks like a stylesheet fault.
        // It was found by pointing the grid at a real, asynchronously loaded catalogue.
        if (Value is null)
        {
            if (!_initialised || _domain != domain)
            {
                _current = domain;
            }

            _domain = domain;
            _initialised = true;
            return;
        }

        // A value the caller supplies wins — unless it is the one this component has just reported,
        // which is the same value arriving back and must not reset a drag in progress.
        if (!_initialised || Value.Value != _reported)
        {
            _domain = domain;
            _current = Clamp(Value.Value, domain);
            _initialised = true;
            return;
        }

        // The domain moved: a reload can shrink or widen it, and a handle outside it would render
        // off the end of the track.
        _domain = domain;
        _current = Clamp(_current, domain);
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

        builder.AddAttribute(31, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(
            this,
            args => lower ? MoveLowerAsync(args) : MoveUpperAsync(args)));

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

        builder.OpenComponent<PolarisStack>(52);
        builder.AddAttribute(53, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(54, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(55, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<PolarisNumberField>(0);
            content.AddAttribute(1, nameof(PolarisNumberField.Label), LowerLabel);
            content.AddAttribute(2, nameof(PolarisNumberField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
            content.AddAttribute(3, nameof(PolarisNumberField.Value), Invariant(Lower));
            content.AddAttribute(4, nameof(PolarisNumberField.Min), (double)DomainMinimum);
            content.AddAttribute(5, nameof(PolarisNumberField.Max), (double)DomainMaximum);
            content.AddAttribute(6, nameof(PolarisNumberField.Step), Step > 0m ? (double)Step : null);
            content.AddAttribute(7, nameof(PolarisNumberField.Suffix), Unit);
            content.AddAttribute(8, nameof(PolarisNumberField.Disabled), Disabled);
            content.AddAttribute(9, nameof(PolarisNumberField.OnChange), EventCallback.Factory.Create<Events.PolarisEventArgs>(this, LowerTypedAsync));
            content.CloseComponent();

            content.OpenComponent<PolarisNumberField>(10);
            content.AddAttribute(11, nameof(PolarisNumberField.Label), UpperLabel);
            content.AddAttribute(12, nameof(PolarisNumberField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
            content.AddAttribute(13, nameof(PolarisNumberField.Value), Invariant(Upper));
            content.AddAttribute(14, nameof(PolarisNumberField.Min), (double)DomainMinimum);
            content.AddAttribute(15, nameof(PolarisNumberField.Max), (double)DomainMaximum);
            content.AddAttribute(16, nameof(PolarisNumberField.Step), Step > 0m ? (double)Step : null);
            content.AddAttribute(17, nameof(PolarisNumberField.Suffix), Unit);
            content.AddAttribute(18, nameof(PolarisNumberField.Disabled), Disabled);
            content.AddAttribute(19, nameof(PolarisNumberField.OnChange), EventCallback.Factory.Create<Events.PolarisEventArgs>(this, UpperTypedAsync));
            content.CloseComponent();
        }));
        builder.CloseComponent();

        builder.CloseElement();
    }

    private Task MoveLowerAsync(ChangeEventArgs args)
    {
        if (!TryParse(args.Value, out var value))
        {
            return Task.CompletedTask;
        }

        // Clamped rather than swapped: a handle dragged past its neighbour stops there, so the
        // merchant keeps hold of the handle they grabbed instead of watching the two exchange places.
        return CommitAsync(PolarisNumberRange.Create(Math.Min(value, Upper), Upper));
    }

    private Task MoveUpperAsync(ChangeEventArgs args)
    {
        if (!TryParse(args.Value, out var value))
        {
            return Task.CompletedTask;
        }

        return CommitAsync(PolarisNumberRange.Create(Lower, Math.Max(value, Lower)));
    }

    private Task LowerTypedAsync(Events.PolarisEventArgs args)
        => TryParse(args.Value, out var value)
            ? CommitAsync(PolarisNumberRange.Create(Math.Min(value, Upper), Upper))
            : Task.CompletedTask;

    private Task UpperTypedAsync(Events.PolarisEventArgs args)
        => TryParse(args.Value, out var value)
            ? CommitAsync(PolarisNumberRange.Create(Lower, Math.Max(value, Lower)))
            : Task.CompletedTask;

    private async Task CommitAsync(PolarisNumberRange range)
    {
        var clamped = Clamp(range, new PolarisNumberRange(DomainMinimum, DomainMaximum));

        if (clamped == _current)
        {
            return;
        }

        _current = clamped;
        _reported = clamped;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(clamped);
        }
    }

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
