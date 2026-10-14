namespace Polaris.Blazor.Components;

/// <summary>
/// Wraps <c>&lt;s-table-body&gt;</c>.
/// </summary>
/// <remarks>
/// Rows belong here, not on the table: appending <c>s-table-row</c> directly to <c>s-table</c>
/// renders nothing.
/// </remarks>
public sealed class PolarisTableBody : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-table-body";
}
