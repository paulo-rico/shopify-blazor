namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-ordered-list&gt;</c>.</summary>
/// <remarks>
/// Polaris accepts only <see cref="PolarisListItem"/> children. Numbering is always decimal starting
/// at one and cannot be restyled, so this is for a genuine sequence rather than for decoration.
/// </remarks>
public sealed class PolarisOrderedList : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-ordered-list";
}
