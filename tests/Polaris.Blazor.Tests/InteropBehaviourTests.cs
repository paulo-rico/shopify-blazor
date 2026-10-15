using Polaris.Blazor.Events;
using Polaris.Blazor.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the behaviour that only happens after the first render: property assignment and event
/// attachment.
/// </summary>
/// <remarks>
/// These are the paths that fail silently in production. A value written as an attribute before the
/// custom element upgrades is discarded without a warning, and an event listener attached to the
/// wrong element simply never fires.
/// </remarks>
public sealed class InteropBehaviourTests
{
    [Fact]
    public void Additional_attributes_are_forwarded_unchanged()
    {
        using var context = new PolarisTestContext();

        // Forwarding is how a caller reaches a Polaris attribute the wrapper does not model yet,
        // rather than forking the component.
        var component = context.Render<TestButton>(parameters => parameters
            .AddUnmatched("interestFor", "bold-tooltip")
            .AddUnmatched("download", "report.csv"));

        var element = component.Find("s-button");
        Assert.Equal("bold-tooltip", element.GetAttribute("interestFor"));
        Assert.Equal("report.csv", element.GetAttribute("download"));
    }

    [Fact]
    public void Cleared_properties_reach_the_interop_layer()
    {
        using var context = new PolarisTestContext();

        context.Render<TestClearableButton>();

        // The base class documented ClearedProperties ("DOM properties cleared on every render") and
        // even gated its property write on the collection, but never called ClearPropertiesAsync — so
        // a component that declared one would have kept its stale value for ever.
        Assert.True(context.Interop.ClearedProperties.TryGetValue("s-button", out var cleared));
        Assert.Contains("values", cleared);
    }

    [Fact]
    public async Task Event_dispatcher_routes_an_event_to_the_registered_callback()
    {
        var dispatcher = new PolarisEventDispatcher();
        PolarisEventArgs? received = null;

        dispatcher.Register("handle-1", new Dictionary<string, EventCallback<PolarisEventArgs>>(StringComparer.Ordinal)
        {
            [PolarisEvents.Click] = EventCallback.Factory.Create<PolarisEventArgs>(
                new object(),
                args => received = args),
        });

        await dispatcher.Dispatch("handle-1", PolarisEvents.Click, new PolarisEventArgs { Value = "42" });

        Assert.NotNull(received);
        Assert.Equal("42", received!.Value);
        Assert.Equal(PolarisEvents.Click, received.Type);
    }

    [Fact]
    public async Task Event_dispatcher_ignores_an_unregistered_handle()
    {
        var dispatcher = new PolarisEventDispatcher();
        var called = false;

        dispatcher.Register("handle-1", new Dictionary<string, EventCallback<PolarisEventArgs>>(StringComparer.Ordinal)
        {
            [PolarisEvents.Click] = EventCallback.Factory.Create<PolarisEventArgs>(new object(), _ => called = true),
        });

        dispatcher.Unregister("handle-1");
        await dispatcher.Dispatch("handle-1", PolarisEvents.Click, new PolarisEventArgs());

        // A component disposed between the event firing and the call arriving is ordinary during
        // navigation; dropping it must not throw.
        Assert.False(called);
        Assert.Equal(0, dispatcher.RegistrationCount);
    }

    [Fact]
    public async Task Event_dispatcher_ignores_an_event_with_no_subscriber()
    {
        var dispatcher = new PolarisEventDispatcher();
        var called = false;

        dispatcher.Register("handle-1", new Dictionary<string, EventCallback<PolarisEventArgs>>(StringComparer.Ordinal)
        {
            [PolarisEvents.Click] = EventCallback.Factory.Create<PolarisEventArgs>(new object(), _ => called = true),
        });

        await dispatcher.Dispatch("handle-1", PolarisEvents.Change, new PolarisEventArgs());

        Assert.False(called);
    }

    [Fact]
    public async Task Interop_reads_the_module_once_and_reuses_it()
    {
        var js = new RecordingJSRuntime();
        await using var interop = new PolarisInterop(js, Microsoft.Extensions.Options.Options.Create(new PolarisInteropOptions()));

        Assert.True(await interop.EnsureInitializedAsync());
        Assert.True(await interop.EnsureInitializedAsync());

        Assert.Equal(1, js.Invocations.Count(i => i.Identifier == "import"));
    }

    [Fact]
    public async Task Interop_survives_a_disconnected_circuit()
    {
        var js = new RecordingJSRuntime { SimulateDisconnectedCircuit = true };
        await using var interop = new PolarisInterop(js, Microsoft.Extensions.Options.Options.Create(new PolarisInteropOptions()));

        // A circuit that has already gone away must not throw into component lifecycle code.
        Assert.False(await interop.EnsureInitializedAsync());
        Assert.Null(await interop.EnsureIdAsync(default));
    }

    [Fact]
    public async Task Interop_routes_a_forwarded_event_through_its_registration()
    {
        var js = new RecordingJSRuntime();
        await using var interop = new PolarisInterop(js, Microsoft.Extensions.Options.Options.Create(new PolarisInteropOptions()));

        // A real DOM node cannot exist here, and ElementReference with no element id cannot be
        // marshalled, so the registration is made directly on the dispatcher. That is the same
        // object, and the same lookup, that a browser-forwarded event reaches.
        Assert.True(await interop.EnsureInitializedAsync());

        PolarisEventArgs? received = null;
        const string handle = "handle-1";

        interop.Dispatcher.Register(handle, new Dictionary<string, EventCallback<PolarisEventArgs>>(StringComparer.Ordinal)
        {
            [PolarisEvents.Click] = EventCallback.Factory.Create<PolarisEventArgs>(new object(), args => received = args),
        });

        Assert.Equal(1, interop.RegistrationCount);

        await interop.DispatchAsync(handle, PolarisEvents.Click, new PolarisEventArgs { Value = "clicked" });

        Assert.NotNull(received);
        Assert.Equal("clicked", received!.Value);
        Assert.Equal(PolarisEvents.Click, received.Type);
    }

    [Fact]
    public void Attribute_values_are_culture_invariant()
    {
        // A machine with a comma decimal separator would otherwise emit value="1,5" and Shopify
        // would reject it.
        Assert.Equal("1.5", PolarisAttributeValue.Format(1.5m));
        Assert.Equal("1234", PolarisAttributeValue.Format(1234));
        Assert.Equal("small-100", PolarisAttributeValue.EnumValue(PolarisSize.Small100));
        Assert.Equal("--toggle", PolarisAttributeValue.EnumValue(PolarisCommand.Toggle));
    }

    [Fact]
    public void False_booleans_format_as_absent()
    {
        Assert.Null(PolarisAttributeValue.Format(false));
        Assert.Equal(string.Empty, PolarisAttributeValue.Format(true));
    }
}
