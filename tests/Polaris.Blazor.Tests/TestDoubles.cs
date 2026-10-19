using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Polaris.Blazor.Events;
using Polaris.Blazor.Interop;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// An <see cref="IJSRuntime"/> that records calls and returns nothing.
/// </summary>
/// <remarks>
/// The component tests care about the markup a wrapper produces, not about JavaScript. bUnit's
/// default strict-mode runtime throws on any interop call, which would make every test of a
/// component that attaches events fail for the wrong reason, so this records instead.
/// </remarks>
public sealed class RecordingJSRuntime : IJSRuntime
{
    /// <summary>Every invocation, as (identifier, arguments).</summary>
    public List<(string Identifier, object?[] Args)> Invocations { get; } = [];

    /// <summary>Stable ids handed back by the interop module, so ids are predictable in assertions.</summary>
    public Func<string> IdGenerator { get; set; } = () => "polaris-blazor-test-1";

    /// <summary>When true, every call throws <see cref="JSDisconnectedException"/>.</summary>
    public bool SimulateDisconnectedCircuit { get; set; }

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (SimulateDisconnectedCircuit)
        {
            throw new JSDisconnectedException("The circuit is gone.");
        }

        Invocations.Add((identifier, args ?? []));

        // The only call whose result the components consume is the module import and the id mint.
        object? result = identifier switch
        {
            "import" => new NoopJSObjectReference(),
            "ensureId" => IdGenerator(),
            _ => null,
        };

        return ValueTask.FromResult((TValue)result!);
    }

    /// <summary>True when the identifier was invoked.</summary>
    public bool WasInvoked(string identifier) => Invocations.Any(i => i.Identifier == identifier);

    /// <summary>The arguments of the first invocation of <paramref name="identifier"/>.</summary>
    public object?[]? ArgumentsOf(string identifier)
        => Invocations.FirstOrDefault(i => i.Identifier == identifier).Args;

    private sealed class NoopJSObjectReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// A <see cref="IPolarisInterop"/> that records what components asked for, without JavaScript.
/// </summary>
public sealed class RecordingPolarisInterop : IPolarisInterop
{
    // Registrations are held per element, in the order each element first attached, because a
    // component re-attaches on every render: a plain list would accumulate a duplicate per render and
    // make "the second button" mean something different after the first click.
    private readonly List<(ElementReference Element, string TagName, IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> Registrations)> _attached = [];

    /// <summary>Events attached, keyed by tag name.</summary>
    public Dictionary<string, List<string>> AttachedEvents { get; } = new(StringComparer.Ordinal);

    /// <summary>Properties set, keyed by tag name.</summary>
    public Dictionary<string, List<IReadOnlyDictionary<string, object?>>> SetProperties { get; } = new(StringComparer.Ordinal);

    /// <summary>Property names cleared, keyed by tag name.</summary>
    public Dictionary<string, List<string>> ClearedProperties { get; } = new(StringComparer.Ordinal);

    /// <summary>Tag names whose events were detached.</summary>
    public List<string> Detached { get; } = [];

    /// <summary>Element methods invoked through interop, in the order they were called.</summary>
    public List<string> Methods { get; } = [];

    /// <summary>How many times the module was initialized.</summary>
    public int InitializeCount { get; private set; }

    /// <summary>Whether the page reported itself as embedded.</summary>
    public bool Embedded { get; set; }

    /// <summary>Whether initialization succeeds.</summary>
    public bool InitializeSucceeds { get; set; } = true;

    /// <summary>How many distinct elements currently have listeners attached.</summary>
    public int AttachedElementCount => _attached.Count;

    /// <summary>
    /// Raises a DOM event on an element that attached listeners, the way the interop module does when
    /// the merchant really clicks it.
    /// </summary>
    /// <remarks>
    /// Polaris events are DOM events forwarded through JavaScript, not Blazor <c>@onclick</c>
    /// handlers, so there is no built-in way to raise one in a component test. Without this, the
    /// whole interaction path — clicking a section, the sibling collapsing — would be untestable
    /// outside a browser, which in practice means untested.
    /// </remarks>
    /// <param name="tagName">Custom element name, for example <c>s-button</c>.</param>
    /// <param name="index">Which of that tag's elements, in the order they first attached. Zero is the first.</param>
    /// <param name="eventName">DOM event name.</param>
    public async Task RaiseAsync(string tagName, int index = 0, string eventName = PolarisEvents.Click)
    {
        var matching = _attached
            .Where(entry => string.Equals(entry.TagName, tagName, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            index < matching.Count,
            $"No {tagName} at index {index}: {matching.Count} element(s) of that tag attached events " +
            $"out of {_attached.Count} in total.");

        var registrations = matching[index].Registrations;

        Assert.True(
            registrations.TryGetValue(eventName, out var callback),
            $"The {tagName} at index {index} has no '{eventName}' handler; it has " +
            $"[{string.Join(", ", registrations.Keys)}].");

        await callback.InvokeAsync(new PolarisEventArgs { Type = eventName });
    }

    /// <inheritdoc />
    public ValueTask<bool> EnsureInitializedAsync()
    {
        InitializeCount++;
        return ValueTask.FromResult(InitializeSucceeds);
    }

    /// <inheritdoc />
    public ValueTask<string?> EnsureIdAsync(ElementReference element) => ValueTask.FromResult<string?>("polaris-blazor-test-1");

    /// <inheritdoc />
    public ValueTask AttachEventsAsync(
        ElementReference element,
        string tagName,
        IEnumerable<string> eventNames,
        IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)
    {
        AttachedEvents[tagName] = eventNames.ToList();

        var existing = _attached.FindIndex(entry => entry.Element.Equals(element));
        var record = (element, tagName, registrations);

        if (existing >= 0)
        {
            _attached[existing] = record;
        }
        else
        {
            _attached.Add(record);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DetachEventsAsync(ElementReference element)
    {
        Detached.Add("detached");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask InvokeElementMethodAsync(ElementReference element, string methodName, params object?[] arguments)
    {
        // Recorded separately from Detached: a method call and an event detach are different things, and
        // a component that calls show() is saying something a teardown does not.
        Methods.Add(methodName);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SetPropertiesAsync(ElementReference element, string tagName, IReadOnlyDictionary<string, object?> values)
    {
        if (!SetProperties.TryGetValue(tagName, out var list))
        {
            list = [];
            SetProperties[tagName] = list;
        }

        list.Add(values);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearPropertiesAsync(ElementReference element, string tagName, IEnumerable<string> names)
    {
        if (!ClearedProperties.TryGetValue(tagName, out var list))
        {
            list = [];
            ClearedProperties[tagName] = list;
        }

        list.AddRange(names);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, PolarisComponentStatus>> GetComponentStatusAsync(IEnumerable<string> tagNames)
        => ValueTask.FromResult<IReadOnlyDictionary<string, PolarisComponentStatus>>(
            new Dictionary<string, PolarisComponentStatus>(StringComparer.Ordinal));

    /// <inheritdoc />
    public ValueTask<bool> IsEmbeddedAsync() => ValueTask.FromResult(Embedded);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// A wrapper whose element has an array-valued property that must be cleared between renders.
/// </summary>
/// <remarks>
/// Nothing in the library uses <see cref="PolarisElementComponentBase.ClearedProperties"/> yet, and
/// the base class documented it for a long time without ever calling the interop to honour it. This
/// double exists so that contract has a test: the next component that needs it will be relying on
/// behaviour that was once silently absent.
/// </remarks>
public sealed class TestClearableButton : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-button";

    /// <inheritdoc />
    protected override IEnumerable<string> ClearedProperties => ["values"];
}
