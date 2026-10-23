using System.Globalization;

namespace Polaris.Blazor.Components;

/// <summary>
/// A decimal written as the plain number a control or a filter chip shows.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the two places a range's bounds are shown cannot disagree. <see cref="PolarisRangeSlider"/>
/// puts them in its Min and Max fields, and <see cref="PolarisDataFilter"/> puts them in the chip for the
/// applied filter; a chip reading "4.1666667" beside fields reading "4.17" looks like the filter is not
/// what the panel said.
/// </para>
/// <para>
/// The rounding is stated rather than left to the format string, because <c>decimal.ToString</c> rounds a
/// halfway value to even — a 4.165 bound would read "4.16" — while anything showing money rounds halves
/// away from zero, so 4.165 is 4.17, the number a merchant would write down.
/// </para>
/// </remarks>
internal static class PolarisNumberText
{
    /// <summary>
    /// Formats a value, rounding a halfway value away from zero at the format's own scale.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="format">A standard or custom numeric format, or <c>null</c> for a plain rendering.</param>
    /// <param name="provider">The culture to write under, or <c>null</c> for the invariant one.</param>
    public static string Format(decimal value, string? format, IFormatProvider? provider)
    {
        provider ??= CultureInfo.InvariantCulture;

        if (string.IsNullOrEmpty(format))
        {
            return value.ToString(provider);
        }

        var shown = ScaleOf(format) is int scale
            ? Math.Round(value, scale, MidpointRounding.AwayFromZero)
            : value;

        return shown.ToString(format, provider);
    }

    /// <summary>
    /// How many decimal places a numeric format can show, or <c>null</c> when it does not fix a number of
    /// them — a general or round-trip format, where pre-rounding would throw away precision the caller
    /// asked to keep.
    /// </summary>
    /// <param name="format">A standard or custom numeric format string.</param>
    public static int? ScaleOf(string format)
    {
        // A standard format is one letter and an optional precision: "F2", "N0", "C2", "E3". A letter
        // alone means the specifier's own default, which for F, N, C and P is two decimals.
        if (char.IsAsciiLetter(format[0]) && format.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0)
        {
            if (format.Length > 1)
            {
                return int.Parse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);
            }

            return format[0] is 'F' or 'f' or 'N' or 'n' or 'C' or 'c' or 'P' or 'p' ? 2 : null;
        }

        // A custom format says it with placeholders: "0.00" and "0.##" both show two decimals, and the
        // section handling the rounding is the one that carries the decimal point.
        var point = format.IndexOf('.', StringComparison.Ordinal);
        if (point < 0)
        {
            return format.Contains('0', StringComparison.Ordinal) || format.Contains('#', StringComparison.Ordinal)
                ? 0
                : null;
        }

        var placeholders = 0;
        for (var index = point + 1; index < format.Length && format[index] is '0' or '#'; index++)
        {
            placeholders++;
        }

        return placeholders;
    }
}
