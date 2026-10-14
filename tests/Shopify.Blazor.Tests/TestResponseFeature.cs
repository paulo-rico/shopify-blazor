using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Shopify.Blazor.Tests;

/// <summary>
/// A response feature that really runs <see cref="IHttpResponseFeature.OnStarting"/> callbacks.
/// </summary>
/// <remarks>
/// <see cref="DefaultHttpContext"/> does not: <c>Response.StartAsync()</c> marks the response as
/// started and nothing else, so a callback registered with <c>OnStarting</c> never runs. Middleware
/// that rewrites headers on start therefore looks broken in a test while working perfectly in a
/// browser — and a test that "passes" by asserting untouched headers is worse than no test.
/// </remarks>
public sealed class TestResponseFeature : IHttpResponseFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

    /// <inheritdoc />
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    /// <inheritdoc />
    public string? ReasonPhrase { get; set; }

    /// <inheritdoc />
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    /// <inheritdoc />
    public Stream Body { get; set; } = new MemoryStream();

    /// <inheritdoc />
    public bool HasStarted { get; private set; }

    /// <inheritdoc />
    public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

    /// <inheritdoc />
    public void OnCompleted(Func<object, Task> callback, object state)
    {
        // Not needed by these tests.
    }

    /// <summary>
    /// Starts the response the way a server does: newest callback first, which is the ordering the
    /// framing middleware depends on.
    /// </summary>
    public async Task StartAsync()
    {
        if (HasStarted)
        {
            return;
        }

        HasStarted = true;

        for (var i = _onStarting.Count - 1; i >= 0; i--)
        {
            await _onStarting[i].Callback(_onStarting[i].State).ConfigureAwait(false);
        }
    }
}

/// <summary>Helpers for building a testable <see cref="HttpContext"/>.</summary>
public static class TestHttpContext
{
    /// <summary>Creates a context whose response genuinely fires its start callbacks.</summary>
    public static (HttpContext Context, TestResponseFeature Feature) Create()
    {
        var context = new DefaultHttpContext();
        var feature = new TestResponseFeature();

        context.Features.Set<IHttpResponseFeature>(feature);
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(feature.Body));

        return (context, feature);
    }
}
