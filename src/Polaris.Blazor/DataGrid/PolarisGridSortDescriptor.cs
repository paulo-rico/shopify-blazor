namespace Polaris.Blazor.DataGrid;

/// <summary>
/// One column of a sort order.
/// </summary>
/// <remarks>
/// More than one descriptor sorts by several columns: <see cref="Priority"/> decides which one is
/// applied first, so "carat, then name" is two descriptors rather than a composite key the caller
/// has to build. Ties at every level keep their source order, because the underlying ordering is
/// stable.
/// </remarks>
/// <param name="Field">The <see cref="PolarisGridColumnDefinition{TItem}.Id"/> of the column sorted.</param>
/// <param name="Direction">Which way the values are ordered.</param>
/// <param name="Priority">Lower numbers sort first. The default is 0.</param>
public sealed record PolarisGridSortDescriptor(
    string Field,
    PolarisGridSortDirection Direction,
    int Priority = 0);
