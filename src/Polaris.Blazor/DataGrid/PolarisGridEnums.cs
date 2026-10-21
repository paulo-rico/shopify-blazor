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

/// <summary>Which editor a cell draws while the merchant is editing it.</summary>
/// <remarks>
/// Declared rather than inferred for the same reason <see cref="PolarisGridFilterType"/> is: a string
/// column may be free text or one of five categories, and only the caller knows which.
/// <see cref="Auto"/> reads the column's value type, which is right for most columns and wrong for
/// exactly those.
/// </remarks>
public enum PolarisGridEditorType
{
    /// <summary>Chosen from the column's value type. The default.</summary>
    Auto,

    /// <summary>A single-line text field.</summary>
    Text,

    /// <summary>A numeric field. The value is converted back to the column's own type on commit.</summary>
    Number,

    /// <summary>A date field. The value is written as an ISO date, which is what the element reads.</summary>
    Date,

    /// <summary>
    /// A checkbox, which commits the moment it is ticked rather than on Enter — a tick that did nothing
    /// until a key was pressed would be indistinguishable from a broken one.
    /// </summary>
    Boolean,

    /// <summary>
    /// A select. Its options are <see cref="PolarisGridColumnDefinition{TItem}.EditorOptions"/> when the
    /// column declares them, otherwise an enum's values, otherwise the dataset's distinct values.
    /// </summary>
    Select,
}

/// <summary>When a committed edit reaches the caller's store.</summary>
/// <remarks>
/// <para>
/// This decides <em>when</em> the edit is written, never <em>whether</em>: an editable grid always
/// edits the records it was given. Without a <c>SaveChanges</c> callback a committed edit reaches
/// <see cref="PolarisGridState{TItem}"/> and stops there, which is what a screen over local data
/// wants.
/// </para>
/// <para>
/// Neither mode is a database dependency. The grid asks the caller to persist a row; what the caller
/// does with it — SQL Server, a service, a file — is not the grid's business.
/// </para>
/// </remarks>
public enum PolarisGridSaveMode
{
    /// <summary>
    /// After every committed cell, so the store and the grid agree at all times. A row that the change
    /// pushes out of the current filter or sort leaves the page as soon as it is saved, which is what
    /// "saved immediately" means.
    /// </summary>
    Immediate,

    /// <summary>
    /// When the merchant presses the grid's Save button, or the page calls the grid's <c>SaveAsync</c>.
    /// Edits are drafts until then: the table keeps the row where it is, so the merchant can finish a
    /// row without it moving under the cursor.
    /// </summary>
    Manual,
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

