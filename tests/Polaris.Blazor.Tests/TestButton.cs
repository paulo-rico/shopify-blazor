
namespace Polaris.Blazor.Tests;

/// <summary>
/// A minimal wrapper over the base class, used to test base-class behaviour in isolation.
/// </summary>
/// <remarks>
/// It deliberately renders through the base implementation rather than overriding
/// <see cref="PolarisElementComponentBase.BuildRenderTree"/>. That is the path every real wrapper
/// takes, and the reason the wrappers are plain C# classes: the Razor compiler injects its own
/// <c>BuildRenderTree</c> into every <c>.razor</c> component, which silently replaces the base
/// implementation and renders nothing at all.
/// </remarks>
public sealed class TestButton : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-button";
}
