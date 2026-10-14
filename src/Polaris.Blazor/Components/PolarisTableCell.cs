namespace Polaris.Blazor.Components;

/// <summary>
/// Wraps <c>&lt;s-table-cell&gt;</c>.
/// </summary>
/// <remarks>
/// Render one cell per header, in the same order. A mismatch does not raise: the browser shifts
/// every later column, so values appear under the wrong headings and the last heading is empty.
/// </remarks>
public sealed class PolarisTableCell : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-table-cell";
}
