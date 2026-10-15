using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the form wrappers added with the second inventory pass.
/// </summary>
/// <remarks>
/// The form components share a shape and a set of traps: a value that is never reflected, a boolean
/// that must be a bare attribute, and — new here — an array-valued property that has to be cleared
/// as well as set. Each test below names the failure it prevents.
/// </remarks>
public sealed class FormComponentRenderingTests
{
    [Fact]
    public void ChoiceList_sets_values_as_a_property_because_no_attribute_can_carry_an_array()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisChoiceList>(parameters => parameters
            .Add(p => p.Label, "Company name")
            .Add(p => p.Multiple, true)
            .Add(p => p.Values, new[] { "hidden", "optional" }));

        var element = component.Find("s-choice-list");
        Assert.Equal("Company name", element.GetAttribute("label"));
        Assert.True(element.HasAttribute("multiple"));

        // An array attribute would render as the type name and select nothing, with no error.
        Assert.False(element.HasAttribute("values"));
        Assert.True(context.Interop.SetProperties.TryGetValue("s-choice-list", out var calls));
        Assert.Contains(calls, call =>
            call["values"] is IReadOnlyList<string> values
            && values.SequenceEqual(new[] { "hidden", "optional" }));
    }

    [Fact]
    public void ChoiceList_clears_a_stale_values_array_when_the_selection_is_dropped()
    {
        using var context = new PolarisTestContext();

        // Null means "no selection at all". Without an explicit clear the element keeps the array
        // from the previous render, so options stay ticked that the server has deselected, and the
        // markup shows nothing wrong.
        context.Render<PolarisChoiceList>(parameters => parameters
            .Add(p => p.Label, "Company name")
            .Add(p => p.Values, null));

        Assert.True(context.Interop.ClearedProperties.TryGetValue("s-choice-list", out var cleared));
        Assert.Contains("values", cleared);
    }

    [Fact]
    public void Choice_keeps_selected_out_of_the_markup_because_it_is_not_reflected()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisChoice>(parameters => parameters
            .Add(p => p.Value, "optional")
            .Add(p => p.Selected, true));

        var element = component.Find("s-choice");
        Assert.Equal("optional", element.GetAttribute("value"));
        Assert.False(element.HasAttribute("selected"));
        Assert.True(context.Interop.SetProperties.TryGetValue("s-choice", out var calls));
        Assert.Contains(calls, call => call["selected"] is true);
    }

    [Fact]
    public void ColorField_renders_alpha_as_a_bare_attribute()
    {
        using var context = new PolarisTestContext();

        var withAlpha = context.Render<PolarisColorField>(parameters => parameters
            .Add(p => p.Label, "Brand colour")
            .Add(p => p.Value, "#FF0000")
            .Add(p => p.Alpha, true));

        var withoutAlpha = context.Render<PolarisColorField>(parameters => parameters
            .Add(p => p.Label, "Brand colour")
            .Add(p => p.Alpha, false));

        Assert.True(withAlpha.Find("s-color-field").HasAttribute("alpha"));
        Assert.False(withoutAlpha.Find("s-color-field").HasAttribute("alpha"));
        Assert.Equal("#FF0000", withAlpha.Find("s-color-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public void ColorPicker_writes_the_initial_colour_and_then_the_property()
    {
        using var context = new PolarisTestContext();

        context.Render<PolarisColorPicker>(parameters => parameters.Add(p => p.Value, "#00FF00"));

        // The picker has no label of its own, so the value is the whole contract.
        Assert.True(context.Interop.SetProperties.TryGetValue("s-color-picker", out var calls));
        Assert.Contains(calls, call => (string?)call["value"] == "#00FF00");
    }

    [Fact]
    public void DateField_renders_a_range_value_and_forwards_the_date_events()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisDateField>(parameters => parameters
            .Add(p => p.Label, "Order window")
            .Add(p => p.Value, "2025-05-28--2025-05-31")
            .Add(p => p.View, "2025-05")
            .Add(p => p.OnInvalid, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { }))
            .Add(p => p.OnViewChange, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { })));

        var element = component.Find("s-date-field");
        Assert.Equal("2025-05-28--2025-05-31", element.GetAttribute("defaultValue"));
        Assert.Equal("2025-05", element.GetAttribute("view"));

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-date-field", out var events));
        Assert.Contains("invalid", events);
        Assert.Contains("viewchange", events);
    }

    [Fact]
    public void DatePicker_renders_range_type_and_the_visible_month_count()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisDatePicker>(parameters => parameters
            .Add(p => p.Type, PolarisDatePickerType.Range)
            .Add(p => p.VisibleMonths, PolarisDatePickerVisibleMonths.Two));

        var element = component.Find("s-date-picker");
        Assert.Equal("range", element.GetAttribute("type"));

        // The union is the strings "auto" | "1" | "2", which only a pinned value can express.
        Assert.Equal("2", element.GetAttribute("visibleMonths"));
    }

    [Fact]
    public void DropZone_renders_accept_multiple_and_forwards_droprejected()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisDropZone>(parameters => parameters
            .Add(p => p.Label, "Upload")
            .Add(p => p.Accept, ".jpg,.png")
            .Add(p => p.Multiple, true)
            .Add(p => p.OnDropRejected, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { })));

        var element = component.Find("s-drop-zone");
        Assert.Equal(".jpg,.png", element.GetAttribute("accept"));
        Assert.True(element.HasAttribute("multiple"));

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-drop-zone", out var events));
        Assert.Equal(["droprejected"], events);
    }

    [Fact]
    public void EmailField_renders_its_length_bounds()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisEmailField>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.MaxLength, 254)
            .Add(p => p.MinLength, 3));

        var element = component.Find("s-email-field");
        Assert.Equal("254", element.GetAttribute("maxLength"));
        Assert.Equal("3", element.GetAttribute("minLength"));
    }

    [Fact]
    public void MoneyField_renders_the_currency_code_and_the_amount_bounds()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisMoneyField>(parameters => parameters
            .Add(p => p.Label, "Regional price")
            .Add(p => p.CurrencyCode, "JPY")
            .Add(p => p.Min, 0)
            .Add(p => p.Max, 1000));

        var element = component.Find("s-money-field");
        Assert.Equal("JPY", element.GetAttribute("currencyCode"));

        // Invariant formatting matters here: a machine with a comma decimal separator would emit
        // "1000" with a separator and Shopify would reject the bound.
        Assert.Equal("0", element.GetAttribute("min"));
        Assert.Equal("1000", element.GetAttribute("max"));
    }

    [Fact]
    public void NumberField_renders_step_bounds_affixes_and_input_mode()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisNumberField>(parameters => parameters
            .Add(p => p.Label, "Quantity")
            .Add(p => p.Step, 5)
            .Add(p => p.Min, 0)
            .Add(p => p.Max, 100)
            .Add(p => p.Prefix, ">")
            .Add(p => p.Suffix, "units")
            .Add(p => p.InputMode, PolarisNumberFieldInputMode.Numeric));

        var element = component.Find("s-number-field");
        Assert.Equal("5", element.GetAttribute("step"));
        Assert.Equal("0", element.GetAttribute("min"));
        Assert.Equal("100", element.GetAttribute("max"));
        Assert.Equal(">", element.GetAttribute("prefix"));
        Assert.Equal("units", element.GetAttribute("suffix"));
        Assert.Equal("numeric", element.GetAttribute("inputMode"));
    }

    [Fact]
    public void PasswordField_renders_its_minimum_length()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPasswordField>(parameters => parameters
            .Add(p => p.Label, "Password")
            .Add(p => p.MinLength, 8));

        Assert.Equal("8", component.Find("s-password-field").GetAttribute("minLength"));
    }

    [Fact]
    public void SearchField_renders_no_icon_prefix_or_suffix()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSearchField>(parameters => parameters
            .Add(p => p.Label, "Search")
            .Add(p => p.Placeholder, "Search items"));

        var element = component.Find("s-search-field");
        Assert.Equal("Search items", element.GetAttribute("placeholder"));

        // Unlike a text field, s-search-field documents no icon, prefix or suffix. Rendering one
        // would be dead markup that looks like a working feature.
        Assert.False(element.HasAttribute("icon"));
        Assert.False(element.HasAttribute("prefix"));
        Assert.False(element.HasAttribute("suffix"));
    }

    [Fact]
    public void UrlField_renders_its_placeholder_and_value()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisUrlField>(parameters => parameters
            .Add(p => p.Label, "Your website")
            .Add(p => p.Placeholder, "https://example.com")
            .Add(p => p.Value, "https://example.com"));

        var element = component.Find("s-url-field");
        Assert.Equal("https://example.com", element.GetAttribute("placeholder"));
        Assert.Equal("https://example.com", element.GetAttribute("defaultValue"));
    }

    [Fact]
    public void Every_new_field_writes_its_value_as_a_dom_property()
    {
        // A field whose value only ever reaches the markup shows a stale value the moment the
        // server changes it, because Polaris does not reflect `value`.
        AssertValueIsAProperty<PolarisColorField>("s-color-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisColorPicker>("s-color-picker", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisDateField>("s-date-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisDatePicker>("s-date-picker", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisEmailField>("s-email-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisMoneyField>("s-money-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisNumberField>("s-number-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisPasswordField>("s-password-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisSearchField>("s-search-field", p => p.Add(x => x.Value, "VALUE"));
        AssertValueIsAProperty<PolarisUrlField>("s-url-field", p => p.Add(x => x.Value, "VALUE"));
    }

    private static void AssertValueIsAProperty<TComponent>(
        string tagName,
        Action<ComponentParameterCollectionBuilder<TComponent>> setValue)
        where TComponent : IComponent
    {
        using var context = new PolarisTestContext();

        context.Render<TComponent>(parameters => setValue(parameters));

        Assert.True(context.Interop.SetProperties.TryGetValue(tagName, out var calls));
        Assert.Contains(calls, call => (string?)call["value"] == "VALUE");
    }
}
