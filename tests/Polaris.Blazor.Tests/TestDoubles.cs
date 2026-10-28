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
    /// <summary>Events attached, keyed by tag name.</summary>
    public Dictionary<string, List<string>> AttachedEvents { get; } = new(StringComparer.Ordinal);

    /// <summary>Properties set, keyed by tag name.</summary>
    public Dictionary<string, List<IReadOnlyDictionary<string, object?>>> SetProperties { get; } = new(StringComparer.Ordinal);

    /// <summary>Property names cleared, keyed by tag name.</summary>
    public Dictionary<string, List<string>> ClearedProperties { get; } = new(StringComparer.Ordinal);

    /// <summary>Tag names whose events were detached.</summary>
    public List<string> Detached { get; } = [];

    /// <summary>
    /// Element methods invoked through interop, in the order they were called.
    /// </summary>
    public List<string> Methods { get; } = [];

    /// <summary>
    /// How many times focus was moved into an element, and whether the caller was told it worked.
    /// </summary>
    /// <remarks>
    /// Counted rather than recorded as an element, because an <see cref="ElementReference"/> outside a
    /// browser is an empty struct: what a test can assert is that the grid asked, which is the half of
    /// "the caret really is in the new editor" that a component test can own.
    /// </remarks>
    public int FocusWithinCount { get; private set; }

    /// <summary>Whether <see cref="FocusWithinAsync"/> reports success.</summary>
    public bool FocusWithinSucceeds { get; set; } = true;

    /// <summary>The selector the last focus call was narrowed by, or <c>null</c> for the whole element.</summary>
    public string? LastFocusSelector { get; private set; }

    /// <summary>
    /// The expanded states written onto a shadow button, in the order they were written.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SetProperties"/> on purpose: this is not a Polaris property but an
    /// <c>aria-expanded</c> attribute written onto the button inside the element's shadow root, which
    /// is the only way an <c>s-clickable</c> can carry the state.
    /// </remarks>
    public List<bool> AriaExpanded { get; } = [];

    /// <summary>
    /// Popup shows and hides, in the order they were asked for: <c>show:below</c>, <c>show:above</c>
    /// or <c>hide</c>.
    /// </summary>
    /// <remarks>
    /// The popup cannot be asserted through the DOM alone: whether it is really in the browser's top
    /// layer is a browser fact. What a component test can own is that the right call was made — and
    /// that closing asks for the hide, which is what releases the positioning listeners.
    /// </remarks>
    public List<string> PopupCalls { get; } = [];

    /// <summary>How many times the module was initialized.</summary>
    public int InitializeCount { get; private set; }

    /// <summary>Whether the page reported itself as embedded.</summary>
    public bool Embedded { get; set; }

    /// <summary>Whether initialization succeeds.</summary>
    public bool InitializeSucceeds { get; set; } = true;

    /// <inheritdoc />
    public ValueTask<bool> EnsureInitializedAsync()
    {
        InitializeCount++;
        return ValueTask.FromResult(InitializeSucceeds);
    }

    /// <inheritdoc />
    public ValueTask<string?> EnsureIdAsync(ElementReference element) => ValueTask.FromResult<string?>("polaris-blazor-test-1");

    /// <summary>
    /// The callbacks each component attached, keyed by tag name and appended in attach order.
    /// </summary>
    /// <remarks>
    /// Appended rather than replaced, unlike <see cref="AttachedEvents"/>: a component re-attaches after
    /// every render, so the LAST entry for a tag is the one a live listener would be — and this is what
    /// makes an interaction test possible at all. Without it a test can only assert that a control was
    /// wired, never what pressing it does.
    /// </remarks>
    public Dictionary<string, List<IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>>>> Registrations { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Raises an event on the last component of a tag, the way the browser would.
    /// </summary>
    /// <param name="tagName">The custom element's tag name, such as <c>s-select</c>.</param>
    /// <param name="eventName">The DOM event name, such as <c>change</c>.</param>
    /// <param name="value">The value the element would report.</param>
    /// <param name="key">The key a keyboard event would report, such as <c>ArrowDown</c>.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="InvalidOperationException">Nothing of that tag attached, or it has no such event.</exception>
    public async Task RaiseAsync(string tagName, string eventName, string? value = null, string? key = null)
    {
        if (!Registrations.TryGetValue(tagName, out var registrations) || registrations.Count == 0)
        {
            throw new InvalidOperationException($"Nothing of tag '{tagName}' attached an event.");
        }

        var latest = registrations[^1];

        if (!latest.TryGetValue(eventName, out var callback))
        {
            throw new InvalidOperationException($"'{tagName}' did not attach '{eventName}'; it attached {string.Join(", ", latest.Keys)}.");
        }

        await callback.InvokeAsync(new PolarisEventArgs { Value = value, Key = key });
    }

    /// <inheritdoc />
    public ValueTask AttachEventsAsync(
        ElementReference element,
        string tagName,
        IEnumerable<string> eventNames,
        IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)
    {
        AttachedEvents[tagName] = eventNames.ToList();

        if (!Registrations.TryGetValue(tagName, out var list))
        {
            list = [];
            Registrations[tagName] = list;
        }

        list.Add(registrations);
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
    public ValueTask<bool> FocusWithinAsync(ElementReference element, string? selector = null)
    {
        FocusWithinCount++;
        LastFocusSelector = selector;
        return ValueTask.FromResult(FocusWithinSucceeds);
    }

    /// <inheritdoc />
    public ValueTask<bool> ShowPopupAsync(ElementReference panel, ElementReference anchor, bool above)
    {
        PopupCalls.Add(above ? "show:above" : "show:below");
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask HidePopupAsync(ElementReference panel)
    {
        PopupCalls.Add("hide");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> SetAriaExpandedAsync(ElementReference element, bool expanded)
    {
        AriaExpanded.Add(expanded);
        return ValueTask.FromResult(true);
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
