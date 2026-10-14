using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor;

/// <summary>
/// Base class for a wrapper whose children are ordinary content.
/// </summary>
public abstract class PolarisContentComponentBase : PolarisElementComponentBase
{
    /// <summary>The element's children.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override RenderFragment? BuildChildContent() => ChildContent;
}
