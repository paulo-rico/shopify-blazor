namespace Polaris.Blazor.DataGrid;

/// <summary>
/// Something a <see cref="Polaris.Blazor.Components.PolarisGridColumn{TItem}"/> can register its
/// declaration with: it owns the state the column belongs to, and it can be asked to redraw when a
/// declaration changes.
/// </summary>
/// <remarks>
/// <para>
/// Cascaded rather than passed, for the same reason the grid's own state is: a column is declared in
/// markup, where there is nowhere to put a parameter, and the thing it registers with is whichever
/// host encloses it.
/// </para>
/// <para>
/// Two components are hosts. <see cref="Polaris.Blazor.Components.PolarisDataGrid{TItem}"/> is the
/// obvious one — it draws the table the columns describe. <see
/// cref="Polaris.Blazor.Components.PolarisDataFilter"/> is the other, and it exists for the screen
/// whose rows are not a table at all: cards, tiles or a gallery still need the same filters, and the
/// filter draws its controls from the same declarations.
/// </para>
/// </remarks>
public interface IPolarisGridColumnHost
{
    /// <summary>The state a column declared inside this host registers with.</summary>
    PolarisGridState State { get; }

    /// <summary>
    /// Asks the host to redraw, after a column's declaration changed in place rather than being
    /// registered or unregistered.
    /// </summary>
    void NotifyColumnChanged();
}
