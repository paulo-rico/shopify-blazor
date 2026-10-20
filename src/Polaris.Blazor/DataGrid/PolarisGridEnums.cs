namespace Polaris.Blazor.DataGrid;

/// <summary>The kind of filter control a column offers.</summary>
/// <remarks>
/// The filter type is declared rather than inferred from the CLR type on purpose: a string column
/// may be free text or one of five categories, and only the caller knows which. See
/// <see cref="PolarisGridColumnDefinition{TItem}.FilterType"/>.
/// </remarks>
public enum PolarisGridFilterType
{
    /// <summary>Not filterable. The default.</summary>
    None,

    /// <summary>A case-insensitive contains search box.</summary>
    Text,

    /// <summary>A list of choices; the selected ones are OR-ed together.</summary>
    MultiSelect,

    /// <summary>A three-state choice: all, true or false.</summary>
    Boolean,

    /// <summary>A dual-handle numeric range, ends inclusive.</summary>
    Range,

    /// <summary>An inclusive interval between two dates.</summary>
    DateRange,

    /// <summary>Caller-supplied markup, from <see cref="PolarisGridColumnDefinition{TItem}.FilterTemplate"/>.</summary>
    Custom,
}

/// <summary>How a filter descriptor compares a column's value.</summary>
public enum PolarisGridOperator
{
    /// <summary>Equal to the value. Text compares case-insensitively.</summary>
    Equals,

    /// <summary>Not equal to the value.</summary>
    NotEquals,

    /// <summary>Contains the value as a substring, case-insensitively.</summary>
    Contains,

    /// <summary>Starts with the value, case-insensitively.</summary>
    StartsWith,

    /// <summary>Ends with the value, case-insensitively.</summary>
    EndsWith,

    /// <summary>Greater than the value.</summary>
    GreaterThan,

    /// <summary>Greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary>Less than the value.</summary>
    LessThan,

    /// <summary>Less than or equal to the value.</summary>
    LessThanOrEqual,

    /// <summary>Inside the inclusive interval from <see cref="PolarisGridFilterDescriptor.Value"/> to its second value.</summary>
    Between,

    /// <summary>One of the descriptor's values.</summary>
    In,

    /// <summary>The column has no value.</summary>
    IsNull,

    /// <summary>The column has a value.</summary>
    IsNotNull,
}

/// <summary>Which way a column's values are ordered.</summary>
public enum PolarisGridSortDirection
{
    /// <summary>Smallest first, with empty values last.</summary>
    Ascending,

    /// <summary>Largest first, with empty values first.</summary>
    Descending,
}

/// <summary>When a filter change reaches the query.</summary>
public enum PolarisGridFilterApplyMode
{
    /// <summary>On every change.</summary>
    Immediate,

    /// <summary>After typing stops, for a configurable interval. The default for text.</summary>
    Debounced,

    /// <summary>Only when the merchant presses Apply, or a choice is made.</summary>
    Manual,
}

