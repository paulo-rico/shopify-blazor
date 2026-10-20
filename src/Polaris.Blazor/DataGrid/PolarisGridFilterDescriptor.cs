namespace Polaris.Blazor.DataGrid;

/// <summary>
/// One column filter, as data rather than as a control.
/// </summary>
/// <remarks>
/// <para>
/// The filter a merchant sets is a value, not a piece of UI state: it can be built by a component,
/// by a test or by a saved view, and the query engine is the only thing that interprets it. That is
/// what lets <see cref="PolarisDataFilter"/> be replaced or omitted without the grid changing
/// behaviour.
/// </para>
/// <para>
/// Values are typed or textual, and the engine coerces them against the column's own type before
/// comparing — so a numeric column never silently compares as a string, and a value that cannot be
/// coerced removes the filter rather than matching nothing.
/// </para>
/// </remarks>
/// <param name="Field">The <see cref="PolarisGridColumnDefinition{TItem}.Id"/> of the column filtered.</param>
/// <param name="Operator">How values are compared.</param>
/// <param name="Value">The value, or the lower bound of <see cref="PolarisGridOperator.Between"/>.</param>
/// <param name="SecondValue">The upper bound of <see cref="PolarisGridOperator.Between"/>.</param>
/// <param name="Values">The accepted values of <see cref="PolarisGridOperator.In"/>.</param>
/// <param name="IncludeNulls">Keeps empty values in the result even when they match nothing.</param>
public sealed record PolarisGridFilterDescriptor(
    string Field,
    PolarisGridOperator Operator,
    object? Value = null,
    object? SecondValue = null,
    IReadOnlyList<object?>? Values = null,
    bool IncludeNulls = false)
{
    /// <summary>A case-insensitive contains filter over a text column.</summary>
    /// <param name="field">The column to filter.</param>
    /// <param name="term">The text to look for. Whitespace-only text means no filter.</param>
    public static PolarisGridFilterDescriptor Text(string field, string? term)
        => new(field, PolarisGridOperator.Contains, term);

    /// <summary>An equality filter. Text compares case-insensitively.</summary>
    /// <param name="field">The column to filter.</param>
    /// <param name="value">The value required.</param>
    public static PolarisGridFilterDescriptor Equals(string field, object? value)
        => new(field, PolarisGridOperator.Equals, value);

    /// <summary>A filter that accepts any of <paramref name="values"/>.</summary>
    /// <param name="field">The column to filter.</param>
    /// <param name="values">The accepted values.</param>
    public static PolarisGridFilterDescriptor In(string field, IEnumerable<object?> values)
        => new(field, PolarisGridOperator.In, Values: [.. values]);

    /// <summary>An inclusive interval filter.</summary>
    /// <param name="field">The column to filter.</param>
    /// <param name="lower">The inclusive lower bound.</param>
    /// <param name="upper">The inclusive upper bound.</param>
    public static PolarisGridFilterDescriptor Between(string field, object? lower, object? upper)
        => new(field, PolarisGridOperator.Between, lower, upper);

    /// <summary>A filter over an inclusive numeric range.</summary>
    /// <param name="field">The column to filter.</param>
    /// <param name="range">The accepted range.</param>
    public static PolarisGridFilterDescriptor InRange(string field, PolarisNumberRange range)
        => new(field, PolarisGridOperator.Between, range.Lower, range.Upper);

    /// <summary>
    /// True when this filter cannot restrict anything, so the caller can drop it rather than send a
    /// redundant criterion — a full-range slider must not become a filter that still triggers a
    /// refresh.
    /// </summary>
    public bool IsEmpty => Operator switch
    {
        PolarisGridOperator.Contains or PolarisGridOperator.StartsWith or PolarisGridOperator.EndsWith
            => Value is null || string.IsNullOrWhiteSpace(Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture)),
        PolarisGridOperator.In => Values is null || Values.Count == 0,
        PolarisGridOperator.Between => Value is null && SecondValue is null,
        PolarisGridOperator.IsNull or PolarisGridOperator.IsNotNull => false,
        _ => Value is null,
    };
}
