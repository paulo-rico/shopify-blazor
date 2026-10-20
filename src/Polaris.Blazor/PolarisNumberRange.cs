using System.Globalization;

namespace Polaris.Blazor;

/// <summary>
/// An inclusive interval between two numbers, with a lower bound that is never greater than the
/// upper one.
/// </summary>
/// <remarks>
/// <para>
/// A single value rather than two unrelated bindable numbers: a range control has one state, and
/// two parameters can be observed half-updated — a lower bound above the upper one is a state the
/// next keystroke silently corrects. <see cref="Create"/> normalises the two ends, so a component
/// can hand over whatever the merchant typed without validating it first.
/// </para>
/// <para>
/// <see cref="decimal"/> because money and percentages are the values this library handles, and a
/// binary floating point type cannot represent 0.1 of a percent without a rounding story.
/// </para>
/// </remarks>
public readonly record struct PolarisNumberRange
{
    /// <summary>Creates a range from two ends, swapping them when they arrive the wrong way round.</summary>
    /// <param name="first">One end of the range.</param>
    /// <param name="second">The other end of the range.</param>
    public PolarisNumberRange(decimal first, decimal second)
    {
        Lower = Math.Min(first, second);
        Upper = Math.Max(first, second);
    }

    /// <summary>The lower end, inclusive.</summary>
    public decimal Lower { get; init; }

    /// <summary>The upper end, inclusive.</summary>
    public decimal Upper { get; init; }

    /// <summary>The distance between the two ends, never negative.</summary>
    public decimal Span => Upper - Lower;

    /// <summary>Builds a range, ordering its ends.</summary>
    /// <param name="first">One end.</param>
    /// <param name="second">The other end.</param>
    public static PolarisNumberRange Create(decimal first, decimal second) => new(first, second);

    /// <summary>True when <paramref name="value"/> lies inside the range, ends included.</summary>
    /// <param name="value">The value to test.</param>
    public bool Contains(decimal value) => value >= Lower && value <= Upper;

    /// <summary>
    /// Snaps both ends to a grid of <paramref name="step"/> steps, and widens the result when
    /// snapping would collapse a non-empty range to a single point.
    /// </summary>
    /// <param name="step">The step size. Values of zero or less leave the range untouched.</param>
    public PolarisNumberRange Snap(decimal step)
    {
        if (step <= 0m)
        {
            return this;
        }

        var lower = Math.Floor(Lower / step) * step;
        var upper = Math.Ceiling(Upper / step) * step;
        return new PolarisNumberRange(lower, upper);
    }

    /// <summary>
    /// Formats both ends for display, with the invariant culture unless a provider is supplied.
    /// </summary>
    /// <param name="format">A standard or custom numeric format string. <c>null</c> formats plainly.</param>
    /// <param name="provider">The format provider. <c>null</c> means invariant.</param>
    public string ToString(string? format, IFormatProvider? provider = null)
        => string.Concat(
            Lower.ToString(format, provider ?? CultureInfo.InvariantCulture),
            " – ",
            Upper.ToString(format, provider ?? CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override string ToString() => ToString(null);
}
