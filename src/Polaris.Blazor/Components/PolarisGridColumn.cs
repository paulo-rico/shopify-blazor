using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.DataGrid;

namespace Polaris.Blazor.Components;

/// <summary>
/// Declares one column of a <see cref="PolarisDataGrid{TItem}"/>: how to read it, how to render it,
/// and what the merchant can filter it by.
/// </summary>
/// <remarks>
/// <para>
/// A declaration, not a region of the page. It renders nothing at all — what appears on screen is
/// drawn by the grid from the <see cref="PolarisGridColumnDefinition{TItem}"/> this component
/// registers — which is why a column can be filtered by a <see cref="PolarisDataFilter"/> that never
/// sees this component, and why hiding a column does not lose its filter.
/// </para>
/// <para>
/// It is declared on the grid that draws the rows, or — on a screen whose rows are cards rather than
/// a table — on the <see cref="PolarisDataFilter"/> itself, whose <c>Columns</c> fragment is the only
/// host there is. Both register with the same state, so the filter's controls are the same in either
/// case.
/// </para>
/// <para>
/// <see cref="Field"/> is an expression rather than a delegate so the grid can read two things from
/// it once: the property's type, which filter values are coerced against, and its name, which is the
/// column's default id. Both are then facts rather than something the caller can mistype.
/// </para>
/// <para>
/// A template should be a function of the row it is given. The declaration is read when the column
/// mounts, so a template that closes over page state is one render behind; pass the state through
/// the context instead of closing over it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisDataGrid TItem="Alloy" State="@State" Columns="@(builder => ...)" /&gt;
/// </code>
/// </example>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class PolarisGridColumn<TItem> : ComponentBase, IDisposable
{
    private PolarisGridColumnDefinition<TItem>? _definition;
    private bool _disposed;

    /// <summary>The host this column belongs to, supplied by whichever component encloses it.</summary>
    /// <remarks>
    /// A <see cref="PolarisDataGrid{TItem}"/> or a <see cref="PolarisDataFilter"/>: the grid draws the
    /// table, and the filter draws the controls — and a screen whose rows are cards declares its
    /// columns on the filter, because there is no table to declare them on.
    /// </remarks>
    [CascadingParameter] private IPolarisGridColumnHost? Grid { get; set; }

    /// <summary>
    /// The property this column shows. <c>x =&gt; x.Name</c>.
    /// </summary>
    /// <remarks>
    /// Typed as <c>object?</c> so the column needs one type parameter rather than two: a
    /// <c>PolarisGridColumn&lt;TItem, TValue&gt;</c> cannot be written in markup without naming both,
    /// and the value's real type is read from the expression anyway.
    /// </remarks>
    [Parameter, EditorRequired]
    public Expression<Func<TItem, object?>> Field { get; set; } = default!;

    /// <summary>
    /// The heading. <c>null</c> — the default — uses the property name.
    /// </summary>
    /// <remarks>
    /// An <em>empty</em> string is not the same as an absent one: it asks for a column with no heading at
    /// all, which is what a thumbnail column wants. Treating the two alike put "ImageUrl" above a column of
    /// pictures, and nothing in the markup said that was not what had been asked for.
    /// </remarks>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The column id, which is what a filter or a saved sort order refers to. Defaults to the
    /// property name.
    /// </summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>
    /// Where this column's filter control appears in the filter bar, independent of the column's
    /// place in the table. Lower values come first.
    /// </summary>
    /// <remarks>
    /// The default, 999, puts a column that states no index after every column that does, and
    /// columns sharing an index keep the order they were declared in.
    /// </remarks>
    [Parameter] public int FilterIndex { get; set; } = 999;

    /// <summary>Whether activating the header sorts by this column.</summary>
    [Parameter] public bool Sortable { get; set; } = true;

    /// <summary>Whether the global search box looks inside this column.</summary>
    [Parameter] public bool Searchable { get; set; } = true;

    /// <summary>
    /// Which filter control this column offers. <c>None</c> offers none.
    /// </summary>
    /// <remarks>
    /// Independent of <see cref="Visible"/> on purpose: this says the column's value can narrow the
    /// result, and <see cref="Visible"/> says whether it is drawn. A column declared with a filter type
    /// and <c>Visible="false"</c> is the way to filter on something the table has no room for — a facet
    /// behind a badge, or a flag the cell already conveys in words — and it gets a control and a chip like
    /// any other column.
    /// </remarks>
    [Parameter] public PolarisGridFilterType FilterType { get; set; }

    /// <summary>
    /// Explicit choices for a multi-select filter. <c>null</c> derives them from the dataset, which
    /// is what a categorical column usually wants.
    /// </summary>
    [Parameter] public IReadOnlyList<object?>? FilterOptions { get; set; }

    /// <summary>
    /// Custom filter markup, required when <see cref="FilterType"/> is <c>Custom</c>. It is rendered
    /// inside the filter panel, and the column's own controls are not drawn.
    /// </summary>
    [Parameter] public RenderFragment? FilterTemplate { get; set; }

    /// <summary>Custom cell markup. <c>null</c> renders the formatted value as text.</summary>
    [Parameter] public RenderFragment<TItem>? CellTemplate { get; set; }

    /// <summary>A standard or custom format string, such as <c>N2</c> or <c>P0</c>.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// How values are formatted. <c>null</c> means the invariant culture, so a rendered value is the
    /// same in a test, an export and a browser.
    /// </summary>
    [Parameter] public IFormatProvider? FormatProvider { get; set; }

    /// <summary>How the header aligns the column and the cells beneath it.</summary>
    [Parameter] public PolarisTableHeaderFormat Alignment { get; set; } = PolarisTableHeaderFormat.Base;

    /// <summary>What the column becomes when the table collapses to a list on a narrow screen.</summary>
    [Parameter] public PolarisTableListSlot ListSlot { get; set; } = PolarisTableListSlot.Labeled;

    /// <summary>
    /// Whether the column is drawn. A hidden column keeps its filter and its sort.
    /// </summary>
    /// <remarks>
    /// Hiding a column is not a way to retire it: the filter it declares still reaches the filter bar, and
    /// the chip for it is still shown, because a filter that narrows the result with nothing on screen to
    /// explain it reads as missing data. To take a column out of the filters as well as the table, declare
    /// it with <see cref="FilterType"/> <c>None</c>.
    /// </remarks>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether the merchant can edit this column's cells, when the grid is editable too.
    /// </summary>
    /// <remarks>
    /// An editable column needs somewhere to write the value: a settable property or field, or
    /// <see cref="ValueSetter"/>. Asking for both without one is refused when the column mounts, because
    /// the alternative is a cell that accepts typing and quietly throws it away.
    /// </remarks>
    [Parameter] public bool Editable { get; set; }

    /// <summary>
    /// Which editor a cell draws. <c>Auto</c> — the default — chooses one from the property's type.
    /// </summary>
    [Parameter] public PolarisGridEditorType EditorType { get; set; }

    /// <summary>
    /// Choices for a <see cref="PolarisGridEditorType.Select"/> editor. <c>null</c> takes them from an
    /// enum's values, or else from the values the dataset contains.
    /// </summary>
    [Parameter] public IReadOnlyList<object?>? EditorOptions { get; set; }

    /// <summary>
    /// Writes a value onto a record, for a column whose property cannot simply be assigned.
    /// </summary>
    /// <remarks>
    /// Needed for a computed column, and for a record that should be replaced rather than mutated:
    /// <c>(alloy, value) =&gt; alloy with { Name = (string)value! }</c>. The value has already been
    /// converted to the property's type when it arrives.
    /// </remarks>
    [Parameter] public Func<TItem, object?, TItem>? ValueSetter { get; set; }

    /// <summary>The definition this column registered, for tests and for the grid.</summary>
    public PolarisGridColumnDefinition<TItem>? Definition => _definition;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Grid is null)
        {
            // A column outside a host is a declaration nobody reads: the filter would not appear and
            // the heading would not be drawn, and nothing would say why.
            throw new InvalidOperationException(
                $"{nameof(PolarisGridColumn<TItem>)} must be declared inside a {nameof(PolarisDataGrid<TItem>)} "
                + $"or a {nameof(PolarisDataFilter)}.");
        }

        var access = PolarisGridExpression.Analyse(Field);

        var id = string.IsNullOrWhiteSpace(Id) ? access.MemberName : Id;

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "The column expression does not name a property, so it has no id of its own. Set Id explicitly for a computed column.",
                nameof(Field));
        }

        if (FilterType == PolarisGridFilterType.Custom && FilterTemplate is null)
        {
            throw new ArgumentException(
                "A Custom filter type needs a FilterTemplate; without one the column would offer a filter control that is not there.",
                nameof(FilterTemplate));
        }

        if (Editable && access.Setter is null && ValueSetter is null)
        {
            throw new ArgumentException(
                $"The column '{id}' asks to be editable but its expression names nothing that can be written, and no {nameof(ValueSetter)} was supplied. A cell nobody can save is worse than a read-only one: it takes the merchant's typing and discards it without a word.",
                nameof(Editable));
        }

        _definition = new PolarisGridColumnDefinition<TItem>
        {
            Id = id,
            Title = Title ?? id,
            ValueGetter = item => access.Getter(item),
            ValueType = access.ValueType,
            Sortable = Sortable,
            Searchable = Searchable,
            FilterType = FilterType,
            FilterIndex = FilterIndex,
            FilterOptions = FilterOptions,
            FilterTemplate = FilterTemplate,
            CellTemplate = CellTemplate,
            Format = Format,
            FormatProvider = FormatProvider,
            Alignment = Alignment,
            ListSlot = ListSlot,
            Visible = Visible,
            Editable = Editable,
            EditorType = EditorType,
            EditorOptions = EditorOptions,
            ValueSetter = ValueSetter,
            CompiledSetter = access.Setter,
        };

        Grid.State.RegisterColumn(_definition);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (_definition is null)
        {
            return;
        }

        // Title, visibility, the filter order and the templates are the parts a page is allowed to
        // change after the column is declared — a column picker, a reordered filter bar, or a template
        // that renders the current row. They are updated in place rather than re-registered, because
        // re-registering on every parameter pass would notify the state on every render and the grid
        // would redraw for ever.
        var title = Title ?? _definition.Id;
        var changed = false;

        if (!string.Equals(_definition.Title, title, StringComparison.Ordinal))
        {
            _definition.Title = title;
            changed = true;
        }

        if (_definition.Visible != Visible)
        {
            _definition.Visible = Visible;
            changed = true;
        }

        if (_definition.FilterIndex != FilterIndex)
        {
            // The filter bar sorts its controls from the definition on every render, so a page that
            // reorders its filters must also have the grid redraw or the new order would wait for
            // some unrelated change.
            _definition.FilterIndex = FilterIndex;
            changed = true;
        }

        if (!ReferenceEquals(_definition.CellTemplate, CellTemplate))
        {
            _definition.CellTemplate = CellTemplate;
            changed = true;
        }

        if (!ReferenceEquals(_definition.FilterTemplate, FilterTemplate))
        {
            _definition.FilterTemplate = FilterTemplate;
            changed = true;
        }

        // Editability and the editor's shape are read from the definition on every cell render, so a
        // page that turns a column editable — or a column picker that swaps an editor — is honoured
        // without re-registering the column.
        if (_definition.Editable != Editable)
        {
            _definition.Editable = Editable;
            changed = true;
        }

        if (_definition.EditorType != EditorType)
        {
            _definition.EditorType = EditorType;
            changed = true;
        }

        if (!ReferenceEquals(_definition.EditorOptions, EditorOptions))
        {
            _definition.EditorOptions = EditorOptions;
            changed = true;
        }

        if (!ReferenceEquals(_definition.ValueSetter, ValueSetter))
        {
            _definition.ValueSetter = ValueSetter;
            changed = true;
        }

        if (changed)
        {
            Grid?.NotifyColumnChanged();
        }
    }

    /// <summary>Removes the column from the grid's state.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_definition is not null)
        {
            Grid?.State.UnregisterColumn(_definition.Id);
        }
    }
}
