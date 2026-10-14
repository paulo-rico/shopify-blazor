using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-popover&gt;</c>.</summary>
/// <remarks>
/// Give the popover an <see cref="PolarisElementComponentBase.Id"/> and open it with a
/// <see cref="PolarisButton.CommandFor"/>. For a confirmation or a form, use a modal instead: a
/// popover does not block the page, so the merchant can interact underneath and lose work.
/// </remarks>
public sealed class PolarisPopover : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-popover";

    /// <summary>Maximum block size before the content scrolls, for example <c>300px</c>.</summary>
    [Parameter] public string? MaxBlockSize { get; set; }

    /// <summary>Fixed block size.</summary>
    [Parameter] public string? BlockSize { get; set; }

    /// <summary>Maximum inline size.</summary>
    [Parameter] public string? MaxInlineSize { get; set; }

    /// <summary>Fixed inline size.</summary>
    [Parameter] public string? InlineSize { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["maxBlockSize"] = MaxBlockSize;
        attributes["blockSize"] = BlockSize;
        attributes["maxInlineSize"] = MaxInlineSize;
        attributes["inlineSize"] = InlineSize;
    }
}
