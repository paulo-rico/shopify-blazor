namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The values a merchant has committed to cells that have not been saved yet, keyed by row and column.
/// </summary>
/// <remarks>
/// <para>
/// A draft is deliberately not a change to the record. Until it is saved, the collection still holds
/// what the database holds — which is what makes a rejected save leave nothing behind, and what lets a
/// merchant edit a column the grid is sorted or filtered by without the row jumping out from under the
/// cursor mid-row.
/// </para>
/// <para>
/// Keyed by row key rather than by position, for the same reason selection is: a page change, a sort
/// or a filter rebuilds the visible rows, and a draft indexed by position would silently attach itself
/// to a different record. Drafts for rows that are not on the visible page are kept and are saved with
/// the rest.
/// </para>
/// <para>
/// Values are stored already converted to the column's own type. Text the merchant typed is parsed
/// once, on commit, so a save writes 42 and not "42", and a value that cannot be parsed never becomes a
/// draft at all — it stays in the editor with a message instead.
/// </para>
/// </remarks>
internal sealed class PolarisGridEditSession
{
    private readonly Dictionary<object, Dictionary<string, object?>> _rows = [];

    /// <summary>How many rows have at least one draft.</summary>
    public int RowCount => _rows.Count;

    /// <summary>True when nothing has been edited.</summary>
    public bool IsEmpty => _rows.Count == 0;

    /// <summary>The keys of the rows with drafts, in the order they were first edited.</summary>
    public IEnumerable<object> Keys => _rows.Keys;

    /// <summary>True when the row has any draft at all.</summary>
    /// <param name="rowKey">The row key.</param>
    public bool IsRowDirty(object rowKey) => _rows.ContainsKey(rowKey);

    /// <summary>The draft values of one row, by column id. Empty when the row is clean.</summary>
    /// <param name="rowKey">The row key.</param>
    public IReadOnlyDictionary<string, object?> ChangesFor(object rowKey)
        => _rows.TryGetValue(rowKey, out var changes)
            ? changes
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Reads one cell's draft.</summary>
    /// <param name="rowKey">The row key.</param>
    /// <param name="columnId">The column id.</param>
    /// <param name="value">The drafted value, when there is one.</param>
    /// <returns>True when the cell has a draft.</returns>
    public bool TryGetCell(object rowKey, string columnId, out object? value)
    {
        value = null;
        return _rows.TryGetValue(rowKey, out var changes)
            && changes.TryGetValue(columnId, out value);
    }

    /// <summary>
    /// Records a cell's draft, replacing any earlier one — unless the value is the value the record
    /// already has, in which case the draft is dropped instead. Typing a cell's own text back into it
    /// therefore leaves the row clean, and cannot produce a save with an empty delta.
    /// </summary>
    /// <param name="rowKey">The row key.</param>
    /// <param name="columnId">The column id.</param>
    /// <param name="value">The value the merchant committed, already converted to the column's type.</param>
    /// <param name="current">The value the record has.</param>
    public void SetCell(object rowKey, string columnId, object? value, object? current)
    {
        if (Equals(value, current))
        {
            ClearCell(rowKey, columnId);
            return;
        }

        if (!_rows.TryGetValue(rowKey, out var changes))
        {
            changes = new Dictionary<string, object?>(StringComparer.Ordinal);
            _rows[rowKey] = changes;
        }

        changes[columnId] = value;
    }

    /// <summary>Drops one cell's draft, which is what Escape does. The row stays dirty if other cells are.</summary>
    /// <param name="rowKey">The row key.</param>
    /// <param name="columnId">The column id.</param>
    /// <returns>True when there was a draft to drop.</returns>
    public bool ClearCell(object rowKey, string columnId)
    {
        if (!_rows.TryGetValue(rowKey, out var changes) || !changes.Remove(columnId))
        {
            return false;
        }

        if (changes.Count == 0)
        {
            _rows.Remove(rowKey);
        }

        return true;
    }

    /// <summary>Drops every draft for one row, which is what a saved row does.</summary>
    /// <param name="rowKey">The row key.</param>
    /// <returns>True when the row had drafts.</returns>
    public bool ClearRow(object rowKey) => _rows.Remove(rowKey);

    /// <summary>Drops every draft.</summary>
    public void Clear() => _rows.Clear();
}
