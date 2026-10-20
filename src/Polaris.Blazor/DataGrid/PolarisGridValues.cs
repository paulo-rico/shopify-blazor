using System.Globalization;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The comparison and conversion rules the query engine applies to values of any type.
/// </summary>
/// <remarks>
/// <para>
/// Centralised because every one of these rules is a silent failure when it is wrong: a numeric
/// filter compared as text puts 9 above 18, a culture-sensitive parse reads "1,5" differently
/// depending on the server's locale, and a <c>null</c> treated as zero sorts an unfilled column to
/// the top of the grid.
/// </para>
/// <para>
/// The invariants: comparison is case-insensitive for text, numeric for numbers, chronological for
/// dates, and an empty value is the <em>greatest</em> value — so ascending order puts blanks last
/// and descending order puts them first, which is the spreadsheet convention and is stated here
/// because it is not obvious from the code.
/// </para>
/// </remarks>
internal static class PolarisGridValues
{
    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/> when it is not already of
    /// that type.
    /// </summary>
    /// <param name="value">The value to convert, usually a string from a filter control.</param>
    /// <param name="targetType">The type wanted.</param>
    /// <param name="result">The converted value, or <c>null</c> when the value was empty.</param>
    /// <returns>
    /// False when a non-empty value could not be converted. The caller drops the filter rather than
    /// applying one that matches nothing: a filter the merchant cannot see failing is worse than a
    /// filter that does not apply.
    /// </returns>
    public static bool TryCoerce(object? value, Type targetType, out object? result)
    {
        result = null;

        if (value is null)
        {
            return true;
        }

        var target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (target == typeof(object) || target.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (target == typeof(string))
        {
            result = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            return true;
        }

        var text = value as string;

        if (target == typeof(bool))
        {
            if (value is bool already)
            {
                result = already;
                return true;
            }

            if (text is not null)
            {
                if (bool.TryParse(text, out var parsed))
                {
                    result = parsed;
                    return true;
                }

                // A tri-state choice that reported "true"/"false" is handled above; anything else is
                // not a boolean, so the filter is dropped rather than read as false.
                return false;
            }

            result = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            return true;
        }

        if (target == typeof(DateOnly))
        {
            if (value is DateTime dateTime)
            {
                result = DateOnly.FromDateTime(dateTime);
                return true;
            }

            if (text is not null && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                result = dateOnly;
                return true;
            }

            return false;
        }

        if (target == typeof(DateTime))
        {
            if (value is DateOnly only)
            {
                result = only.ToDateTime(TimeOnly.MinValue);
                return true;
            }

            if (text is not null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        if (target == typeof(TimeOnly))
        {
            if (text is not null && TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        if (target == typeof(Guid))
        {
            if (text is not null && Guid.TryParse(text, out var parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        if (target.IsEnum)
        {
            if (text is not null)
            {
                if (Enum.TryParse(target, text, ignoreCase: true, out var parsed))
                {
                    result = parsed;
                    return true;
                }

                return false;
            }

            try
            {
                result = Enum.ToObject(target, value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        try
        {
            result = text is not null
                // NumberStyles.Any plus the invariant culture: a filter typed as "1.5" must mean the
                // same thing on a server whose locale writes "1,5".
                ? Convert.ChangeType(text, target, CultureInfo.InvariantCulture)
                : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Orders two values with the grid's rules: text case-insensitively, numbers numerically, dates
    /// chronologically, and empty values after everything else.
    /// </summary>
    /// <param name="left">The value on the left.</param>
    /// <param name="right">The value on the right.</param>
    /// <param name="targetType">
    /// The type to compare in, usually the column's declared type. When omitted, the left value's
    /// own type is used, which is what sorting needs.
    /// </param>
    public static int Compare(object? left, object? right, Type? targetType = null)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        object rightValue = right;

        if (targetType is not null && !targetType.IsInstanceOfType(right))
        {
            if (TryCoerce(right, targetType, out var coerced) && coerced is not null)
            {
                rightValue = coerced;
            }
            else if (targetType == typeof(object) && !IsNumeric(left))
            {
                // Nothing to coerce to; the runtime types decide below.
            }
        }

        if (left is string leftText)
        {
            return string.Compare(
                leftText,
                rightValue as string ?? Convert.ToString(rightValue, CultureInfo.InvariantCulture) ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        if (IsNumeric(left) && IsNumeric(rightValue))
        {
            return ToDecimal(left).CompareTo(ToDecimal(rightValue));
        }

        // A filter value that arrived as text — every Polaris form control reports a string — still
        // compares numerically against a numeric column, so "9" and "18" order as numbers here as
        // well. Without this the fallback below orders them as text, which puts 9 above 18.
        if (IsNumeric(left)
            && rightValue is string numericText
            && decimal.TryParse(numericText, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber))
        {
            return ToDecimal(left).CompareTo(parsedNumber);
        }

        return (left, rightValue) switch
        {
            (DateTime leftDate, DateTime rightDate) => leftDate.CompareTo(rightDate),
            (DateOnly leftDate, DateOnly rightDate) => leftDate.CompareTo(rightDate),
            (TimeOnly leftTime, TimeOnly rightTime) => leftTime.CompareTo(rightTime),
            (TimeSpan leftSpan, TimeSpan rightSpan) => leftSpan.CompareTo(rightSpan),
            (Guid leftId, Guid rightId) => leftId.CompareTo(rightId),
            (Enum leftEnum, Enum rightEnum) => leftEnum.CompareTo(rightEnum),
            (bool leftFlag, bool rightFlag) => leftFlag.CompareTo(rightFlag),
            _ => string.Compare(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(rightValue, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>True when the value is one of the numeric primitives the grid compares numerically.</summary>
    /// <param name="value">The value to test.</param>
    public static bool IsNumeric(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    /// <summary>
    /// Reads a value as a decimal number, for a range control's bounds. Text is parsed invariantly,
    /// so the same dataset yields the same bounds on every machine.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <param name="result">The number, when the read succeeded.</param>
    public static bool TryToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case null:
                result = 0m;
                return false;
            case decimal already:
                result = already;
                return true;
            case string text:
                return decimal.TryParse(text, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        if (IsNumeric(value))
        {
            result = ToDecimal(value);
            return true;
        }

        result = 0m;
        return false;
    }

    /// <summary>
    /// Reads a value as a date without a time, for a date range's bounds. A date and time is
    /// truncated rather than rounded, because a filter bound of "the 5th" must not exclude the 5th.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <param name="result">The date, when the read succeeded.</param>
    public static bool TryToDateOnly(object? value, out DateOnly result)
    {
        switch (value)
        {
            case null:
                result = default;
                return false;
            case DateOnly already:
                result = already;
                return true;
            case DateTime dateTime:
                result = DateOnly.FromDateTime(dateTime);
                return true;
            case string text:
                return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
            default:
                result = default;
                return false;
        }
    }

    private static decimal ToDecimal(object value)
        => value is decimal already ? already : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
}

/// <summary>
/// The ordering the grid sorts with, as an <see cref="IComparer{T}"/> over boxed values.
/// </summary>
internal sealed class PolarisGridValueComparer : IComparer<object?>
{
    /// <summary>The single instance. The comparer is stateless.</summary>
    public static readonly PolarisGridValueComparer Instance = new();

    private PolarisGridValueComparer()
    {
    }

    /// <inheritdoc />
    public int Compare(object? x, object? y) => PolarisGridValues.Compare(x, y);
}
