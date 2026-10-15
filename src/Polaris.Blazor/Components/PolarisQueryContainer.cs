using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-query-container&gt;</c>.</summary>
/// <remarks>
/// This establishes a CSS container so a descendant can use a responsive value such as
/// <c>@container (inline-size &gt; 500px) large, small</c>. Give it a
/// <see cref="ContainerName"/> to query it by name from anywhere in the subtree. It exposes no
/// dimensions to JavaScript, so it cannot be used to measure anything in C#.
/// </remarks>
public sealed class PolarisQueryContainer : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-query-container";

    /// <summary>Container name, for a named <c>@container name (...)</c> query.</summary>
    [Parameter] public string? ContainerName { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["containerName"] = ContainerName;
    }
}
