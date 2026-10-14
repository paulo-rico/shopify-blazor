using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-table-row&gt;</c>.</summary>
public sealed class PolarisTableRow : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-table-row";

    /// <summary>Id of an interactive element in the row to act as the click target.</summary>
    /// <remarks>
    /// Click-only: it adds no keyboard or screen-reader affordance, so the target must also be
    /// reachable and labelled in its own right.
    /// </remarks>
    [Parameter] public string? ClickDelegate { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["clickDelegate"] = ClickDelegate;
    }
}
