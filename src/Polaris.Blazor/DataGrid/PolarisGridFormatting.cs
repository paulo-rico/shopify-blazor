using System.Globalization;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// Turns a value into the text a cell shows.
/// </summary>
/// <remarks>
/// Invariant by default, for the same reason every attribute in this library is formatted
/// invariantly: a comma decimal separator produces text that other code — a test, an exported CSV, a
/// comparison — reads differently from what was rendered. A caller who wants the merchant's own
/// conventions passes <see cref="PolarisGridColumnDefinition{TItem}.FormatProvider"/>.
/// </remarks>
internal static class PolarisGridFormatting
{
    /// <summary>The display text of a value.</summary>
    /// <param name="value">The value to render.</param>
    /// <param name="format">A standard or custom format string, or <c>null</c>.</param>
    /// <param name="provider">The format provider, or <c>null</c> for the invariant culture.</param>
    public static string Format(object? value, string? format, IFormatProvider? provider)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, provider ?? CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }
}
