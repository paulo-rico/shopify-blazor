namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-tooltip&gt;</c>.</summary>
/// <remarks>
/// <para>
/// A tooltip has no trigger of its own. Give it an <see cref="PolarisElementComponentBase.Id"/> and
/// point the element it explains at it with <c>InterestFor</c>.
/// </para>
/// <para>
/// Its children may only be text or paragraph components: Polaris renders it as a text-only overlay,
/// so a button or a link placed inside is unreachable. It is also shown on hover and focus only, so
/// a tooltip must never be the sole carrier of information a touch user needs.
/// </para>
/// </remarks>
public sealed class PolarisTooltip : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-tooltip";
}
