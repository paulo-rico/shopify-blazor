namespace Polaris.Blazor.DataGrid;

/// <summary>
/// A column as the non-generic parts of the grid see it: a filter control, a header cell, a chip.
/// </summary>
/// <remarks>
/// <see cref="PolarisGridColumnDefinition{TItem}"/> is the typed form, and it is what the query
/// engine works with. This interface exists so <see cref="PolarisDataFilter"/> and
/// <see cref="PolarisDataPager"/> can be plain components rather than generic ones: neither of them
/// ever touches an item, only the metadata and the filter values the merchant chose.
/// </remarks>
public interface IPolarisGridColumn
{
    /// <summary>The stable identifier the query state refers to. Unique within one grid.</summary>
    string Id { get; }

    /// <summary>The heading shown in the table and used in filter chip labels.</summary>
    string Title { get; }

    /// <summary>
    /// Where this column's filter control appears in the filter bar, independent of the column's
    /// place in the table. Lower values come first; equal values keep their declaration order.
    /// </summary>
    int FilterIndex { get; }

    /// <summary>The type of the column's values, used to coerce filter values before comparing.</summary>
    Type ValueType { get; }

    /// <summary>Which filter control the column offers. <see cref="PolarisGridFilterType.None"/> means none.</summary>
    PolarisGridFilterType FilterType { get; }

    /// <summary>Whether the header can be activated to sort.</summary>
    bool Sortable { get; }

    /// <summary>Whether the global search box looks inside this column.</summary>
    bool Searchable { get; }

    /// <summary>Whether the column is rendered at all.</summary>
    bool Visible { get; }

    /// <summary>Whether the merchant can edit this column's cells in the grid.</summary>
    /// <remarks>
    /// Only half the answer: the grid must also be editable, and the column must have somewhere to
    /// write the value. A column that declares itself editable and cannot be written is refused when
    /// it mounts rather than drawn as a control that silently loses what is typed into it.
    /// </remarks>
    bool Editable { get; }

    /// <summary>Which editor the cell draws, or <c>Auto</c> to choose one from the value's type.</summary>
    PolarisGridEditorType EditorType { get; }

    /// <summary>Explicit choices for a <see cref="PolarisGridEditorType.Select"/> editor.</summary>
    IReadOnlyList<object?>? EditorOptions { get; }

    /// <summary>Explicit choices for a <see cref="PolarisGridFilterType.MultiSelect"/> column.</summary>
    IReadOnlyList<object?>? FilterOptions { get; }

    /// <summary>A standard or custom numeric or date format string, or <c>null</c> for a plain rendering.</summary>
    string? Format { get; }

    /// <summary>
    /// The column's own filter markup, for a column that declared a <c>Custom</c> filter type.
    /// </summary>
    /// <remarks>
    /// Part of the metadata because the filter bar builds one control per column from metadata
    /// alone; a column that draws its own control is recognised here rather than by casting the
    /// column back to its typed definition.
    /// </remarks>
    Microsoft.AspNetCore.Components.RenderFragment? FilterTemplate { get; }

    /// <summary>How values are formatted for display. Defaults to the invariant culture.</summary>
    IFormatProvider? FormatProvider { get; }

    /// <summary>How the header aligns the column, and the cells beneath it.</summary>
    PolarisTableHeaderFormat Alignment { get; }

    /// <summary>What the column becomes when the table collapses to a list on a narrow screen.</summary>
    PolarisTableListSlot ListSlot { get; }

    /// <summary>Reads this column's value from a boxed item. Used only by metadata helpers.</summary>
    /// <param name="item">The item, already known to be of the grid's type.</param>
    object? ReadValue(object item);

    /// <summary>Renders a value the way the cell does, so a chip and a cell agree on what a value looks like.</summary>
    /// <param name="value">The value to format. <c>null</c> renders as an empty string.</param>
    string FormatValue(object? value);
}
