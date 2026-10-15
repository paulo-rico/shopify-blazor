using System.Text;
using Shopify.Blazor.Cli.Catalogue;

namespace Shopify.Blazor.Cli;

/// <summary>
/// The Razor snippet <c>add component</c> writes for one catalogue entry.
/// </summary>
/// <remarks>
/// The snippet is generated from the catalogue rather than stored per component, so it cannot drift
/// from the wrapper's real parameters. It is deliberately <em>prose about</em> the component rather
/// than a runnable fragment: the catalogue records a parameter's name and type but not a valid
/// value for it, and a generated file full of <c>Variant="…"</c> looks like working code while
/// failing to compile. Every line below is therefore either valid Razor or a comment.
/// </remarks>
public static class ComponentUsageTemplate
{
    /// <summary>Slot names that mean "the element's ordinary children".</summary>
    private static readonly string[] ContentSlots = ["default", "children"];

    /// <summary>Renders the snippet for <paramref name="component"/>.</summary>
    public static string Render(CatalogueComponent component)
    {
        var element = component.Element ?? "s-" + NameHelper.ToKebabCase(StripPrefix(component.Wrapper));
        var builder = new StringBuilder();

        builder.AppendLine($"@* Usage of {component.Wrapper}, which renders <{element}>. *@");

        if (component.Summary is { Length: > 0 } summary)
        {
            builder.AppendLine($"@* {summary} *@");
        }

        if (component.Namespace is { Length: > 0 } ns)
        {
            // Stated explicitly so the snippet compiles in a file whose _Imports.razor does not
            // already pull in the component namespace. The enum types live in Polaris.Blazor, which
            // the toolkit's own _Imports.razor also imports.
            builder.AppendLine($"@using {ns}");
        }

        builder.AppendLine();
        builder.AppendLine($"@* A minimal runnable start. Grow it from the parameters below. *@");
        builder.AppendLine($"<{component.Wrapper} />");

        var parameters = component.Parameters
            .Where(parameter => !IsContent(parameter.Type))
            .ToList();

        if (parameters.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("@* Parameters. A value is not invented here: the catalogue records the type, *@");
            builder.AppendLine("@* not a legal value, and a made-up one compiles or fails unpredictably. *@");

            foreach (var parameter in parameters)
            {
                var note = IsBoolean(parameter.Type)
                    ? $"{parameter.Name}=\"true\"  (presence attribute: false must omit it, or it is still true)"
                    : $"{parameter.Name} ({parameter.Type ?? "?"})";

                builder.AppendLine($"@*   {note} *@");
            }
        }

        var slots = component.Slots.Where(slot => !ContentSlots.Contains(slot, StringComparer.Ordinal)).ToList();

        if (slots.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("@* Named slots. Put the slot name on the child, never on a wrapper element: *@");
            builder.AppendLine("@* Polaris rejects a div that wraps a slot's child. *@");

            foreach (var slot in slots)
            {
                builder.AppendLine($"@*   <PolarisButton Slot=\"{slot}\">…</PolarisButton> *@");
            }
        }

        if (component.Events.Count > 0)
        {
            var handlers = component.Events
                .Select(@event => $"On{NameHelper.ToPascalCase(@event)}=\"Handle{NameHelper.ToPascalCase(@event)}\"")
                .ToList();

            builder.AppendLine();
            builder.AppendLine($"@* Events the wrapper raises: {string.Join(", ", component.Events)}. *@");
            builder.AppendLine($"@* Wire one as {string.Join(" / ", handlers)}, *@");
            builder.AppendLine("@* with a matching 'private void HandleX(PolarisEventArgs args) { }' in @code. *@");
        }

        return builder.ToString();
    }

    /// <summary>Whether the parameter carries child content or a callback rather than a scalar attribute.</summary>
    private static bool IsContent(string? type) =>
        type is not null
        && (type.Contains("RenderFragment", StringComparison.Ordinal)
            || type.StartsWith("EventCallback", StringComparison.Ordinal)
            || type.StartsWith("Func<", StringComparison.Ordinal));

    private static bool IsBoolean(string? type) =>
        type is not null && type.Contains("bool", StringComparison.Ordinal);

    private static string StripPrefix(string wrapper) =>
        wrapper.StartsWith("Polaris", StringComparison.Ordinal) ? wrapper["Polaris".Length..] : wrapper;
}
