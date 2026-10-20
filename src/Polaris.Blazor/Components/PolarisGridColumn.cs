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

    /// <summary>The grid this column belongs to, supplied by the grid itself.</summary>
    [CascadingParameter] private PolarisDataGrid<TItem>? Grid { get; set; }

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

    /// <summary>The heading. Defaults to the property name.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The column id, which is what a filter or a saved sort order refers to. Defaults to the
    /// property name.
    /// </summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Whether activating the header sorts by this column.</summary>
    [Parameter] public bool Sortable { get; set; } = true;

    /// <summary>Whether the global search box looks inside this column.</summary>
    [Parameter] public bool Searchable { get; set; } = true;

    /// <summary>Which filter control this column offers. <c>None</c> offers none.</summary>
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

    /// <summary>Whether the column is drawn. A hidden column keeps its filter and its sort.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>The definition this column registered, for tests and for the grid.</summary>
    public PolarisGridColumnDefinition<TItem>? Definition => _definition;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Grid is null)
        {
            // A column outside a grid is a declaration nobody reads: the filter would not appear and
            // the heading would not be drawn, and nothing would say why.
            throw new InvalidOperationException(
                $"{nameof(PolarisGridColumn<TItem>)} must be declared inside a {nameof(PolarisDataGrid<TItem>)}.");
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

        _definition = new PolarisGridColumnDefinition<TItem>
        {
            Id = id,
            Title = string.IsNullOrWhiteSpace(Title) ? id : Title,
            ValueGetter = item => access.Getter(item),
            ValueType = access.ValueType,
            Sortable = Sortable,
            Searchable = Searchable,
            FilterType = FilterType,
            FilterOptions = FilterOptions,
            FilterTemplate = FilterTemplate,
            CellTemplate = CellTemplate,
            Format = Format,
            FormatProvider = FormatProvider,
            Alignment = Alignment,
            ListSlot = ListSlot,
            Visible = Visible,
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

        // Title, visibility and the templates are the parts a page is allowed to change after the
        // column is declared — a column picker, or a template that renders the current row. They are
        // updated in place rather than re-registered, because re-registering on every parameter pass
        // would notify the state on every render and the grid would redraw for ever.
        var title = string.IsNullOrWhiteSpace(Title) ? _definition.Id : Title;
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
