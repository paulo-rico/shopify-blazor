using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-table&gt;</c>.</summary>
public sealed class PolarisTable : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-table";

    /// <summary>
    /// Shows the table's own loading state.
    /// </summary>
    /// <remarks>
    /// <c>loading</c> is a reflected boolean, so clearing it needs both the property and the
    /// attribute cleared; the interop layer does both. Reserve this for the table itself rather
    /// than stacking a spinner on top of it.
    /// </remarks>
    [Parameter] public bool Loading { get; set; }

    /// <summary><c>list</c> renders stacked key and value pairs; <c>auto</c> is the default.</summary>
    [Parameter] public string? Variant { get; set; }

    /// <summary>Renders previous and next controls driven by the page events.</summary>
    [Parameter] public bool Paginate { get; set; }

    /// <summary>Whether a next page exists. Only meaningful with <see cref="Paginate"/>.</summary>
    [Parameter] public bool HasNextPage { get; set; }

    /// <summary>Whether a previous page exists. Only meaningful with <see cref="Paginate"/>.</summary>
    [Parameter] public bool HasPreviousPage { get; set; }

    /// <summary>Filters rendered in the table's header area.</summary>
    [Parameter] public RenderFragment? Filters { get; set; }

    /// <summary>The header row and body.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised when the merchant asks for the next page.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnNextPage { get; set; }

    /// <summary>Raised when the merchant asks for the previous page.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnPreviousPage { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.NextPage, OnNextPage);
        events.On(PolarisEvents.PreviousPage, OnPreviousPage);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["variant"] = Variant;
        attributes["loading"] = PolarisAttributeValue.Format(Loading);
        attributes["paginate"] = PolarisAttributeValue.Format(Paginate);
        attributes["hasNextPage"] = Paginate ? PolarisAttributeValue.Format(HasNextPage) : null;
        attributes["hasPreviousPage"] = Paginate ? PolarisAttributeValue.Format(HasPreviousPage) : null;
    }

    /// <inheritdoc />
    protected override RenderFragment? BuildChildContent() => builder =>
    {
        if (Filters is not null)
        {
            builder.OpenElement(10, "div");
            builder.AddAttribute(20, "slot", "filters");
            builder.AddContent(30, Filters);
            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.AddContent(40, ChildContent);
        }
    };
}
