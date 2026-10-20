using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// Everything the grid knows about one column: how to read it, how to render it, and what — if
/// anything — the merchant can filter it by.
/// </summary>
/// <remarks>
/// <para>
/// Typed on purpose. The value accessor is a compiled delegate built once from the
/// <see cref="System.Linq.Expressions.Expression"/> the column declared, so rendering a page of rows
/// never reflects over a property — the one thing a per-cell reflection call cannot be allowed to
/// do.
/// </para>
/// <para>
/// Filterability is <see cref="FilterType"/>, not a separate flag: a column that declares no filter
/// type simply offers no filter, which removes a way for the two to disagree.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class PolarisGridColumnDefinition<TItem> : IPolarisGridColumn
{
    /// <summary>
    /// The stable identifier. Defaults to the member name the column was declared from, so a
    /// filter or a saved sort order keeps pointing at the same column after a rebuild.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The column heading.</summary>
    public required string Title { get; set; }

    /// <summary>Reads the column's value from one item. Compiled once, never reflected.</summary>
    public required Func<TItem, object?> ValueGetter { get; init; }

    /// <summary>The value's type, used to coerce filter values before they are compared.</summary>
    public Type ValueType { get; init; } = typeof(object);

    /// <summary>Whether the header sorts the grid when activated.</summary>
    public bool Sortable { get; init; } = true;

    /// <summary>Whether the global search box looks inside this column.</summary>
    public bool Searchable { get; init; } = true;

    /// <summary>The filter control this column offers.</summary>
    public PolarisGridFilterType FilterType { get; init; }

    /// <summary>
    /// Explicit choices for a multi-select filter. <c>null</c> derives them from the dataset.
    /// </summary>
    public IReadOnlyList<object?>? FilterOptions { get; init; }

    /// <summary>Custom filter markup, required only when <see cref="FilterType"/> is <c>Custom</c>.</summary>
    public RenderFragment? FilterTemplate { get; set; }

    /// <summary>Custom cell markup. <c>null</c> renders the formatted value as text.</summary>
    public RenderFragment<TItem>? CellTemplate { get; set; }

    /// <summary>A standard or custom format string for the value, such as <c>N2</c> or <c>P0</c>.</summary>
    public string? Format { get; init; }

    /// <summary>
    /// How values are formatted for display. <c>null</c> means the invariant culture, which is what
    /// keeps a rendered value identical in a test, an export and a browser.
    /// </summary>
    public IFormatProvider? FormatProvider { get; init; }

    /// <summary>How the header aligns the column and the cells beneath it.</summary>
    public PolarisTableHeaderFormat Alignment { get; init; } = PolarisTableHeaderFormat.Base;

    /// <summary>What the column becomes when the table collapses to a list on a narrow screen.</summary>
    public PolarisTableListSlot ListSlot { get; init; } = PolarisTableListSlot.Labeled;

    /// <summary>Whether the column is rendered. A hidden column keeps its filter and its sort.</summary>
    public bool Visible { get; set; } = true;

    /// <inheritdoc />
    object? IPolarisGridColumn.ReadValue(object item) => ValueGetter((TItem)item);

    /// <inheritdoc />
    public string FormatValue(object? value) => PolarisGridFormatting.Format(value, Format, FormatProvider);
}
