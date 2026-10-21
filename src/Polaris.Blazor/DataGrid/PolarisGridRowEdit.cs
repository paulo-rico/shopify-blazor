namespace Polaris.Blazor.DataGrid;

/// <summary>
/// One row's pending edits, handed to <c>PolarisDataGrid&lt;TItem&gt;.SaveChanges</c> so the caller can
/// write them somewhere.
/// </summary>
/// <remarks>
/// <para>
/// The delta, not the record. <see cref="Changes"/> holds only the columns the merchant actually
/// changed, keyed by column id and already converted to the column's own type, so a caller writes
/// <c>UPDATE … SET name = @p0</c> without diffing anything and without parsing text: the merchant's
/// typing was converted and validated by the grid before it got here.
/// </para>
/// <para>
/// <see cref="Original"/> is the record as the query last returned it, which is what identifies the
/// row — a save callback usually needs its primary key far more than its values. It is <em>not</em> a
/// copy: a mutable model is deliberately left alone until the save succeeds, so an edit that is
/// rejected leaves the collection exactly as it was.
/// </para>
/// <para>
/// A record type edited through a compiled property setter is updated in place — the grid cannot copy
/// a type it knows nothing about — so a caller that needs a pristine copy in
/// <see cref="Original"/> declares the column with a
/// <see cref="PolarisGridColumnDefinition{TItem}.ValueSetter"/> that returns a new record.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class PolarisGridRowEdit<TItem>
{
    /// <summary>Creates a row edit.</summary>
    /// <param name="key">The row key, from <see cref="PolarisGridState{TItem}.KeyOf"/>.</param>
    /// <param name="original">The record the changes apply to.</param>
    /// <param name="changes">The changed columns, by column id, already typed.</param>
    public PolarisGridRowEdit(object key, TItem original, IReadOnlyDictionary<string, object?> changes)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(changes);

        Key = key;
        Original = original;
        Changes = changes;
    }

    /// <summary>The row key, so a caller can recognise a record it sees twice.</summary>
    public object Key { get; }

    /// <summary>The record the changes apply to, as the query returned it.</summary>
    public TItem Original { get; }

    /// <summary>The changed columns, keyed by column id, in the columns' own types.</summary>
    public IReadOnlyDictionary<string, object?> Changes { get; }

    /// <summary>The ids of the changed columns, in a stable order.</summary>
    public IEnumerable<string> ChangedColumns => Changes.Keys;
}

/// <summary>What a save callback reports back: whether the row was written, and what to say if not.</summary>
/// <remarks>
/// A result rather than an exception, because a rejected edit is a normal outcome — a duplicate SKU, a
/// price below cost, a row someone else has deleted — and the answer to it is a message under the
/// merchant's cursor, not a stack trace. Throwing out of the callback is still supported and is
/// turned into the same message, so a caller that has not read this can still fail safely.
/// </remarks>
public sealed class PolarisGridSaveResult
{
    private PolarisGridSaveResult(bool succeeded, string? message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>True when the row was written.</summary>
    public bool Succeeded { get; }

    /// <summary>What to show the merchant when it was not, or <c>null</c>.</summary>
    public string? Message { get; }

    /// <summary>The row was written.</summary>
    public static PolarisGridSaveResult Success() => new(true, null);

    /// <summary>
    /// The row was not written, and the draft stays on screen with this message beside the grid.
    /// </summary>
    /// <param name="message">Why, in the merchant's words.</param>
    public static PolarisGridSaveResult Failure(string message)
        => new(false, string.IsNullOrWhiteSpace(message) ? "The row could not be saved." : message);
}
