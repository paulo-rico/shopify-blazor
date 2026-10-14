using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shopify.Blazor.Tests;

/// <summary>
/// A scripted response, so a test can assert on the request that produced it.
/// </summary>
/// <param name="Respond">Builds the response for a request.</param>
public sealed record ScriptedResponse(Func<HttpRequestMessage, HttpResponseMessage> Respond)
{
    /// <summary>Answers with JSON.</summary>
    public static ScriptedResponse Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records requests and answers them from a script.
/// </summary>
/// <remarks>
/// Every Shopify call in this toolkit is an ordinary HTTPS request, so a scripted handler is
/// enough to test the whole authentication path — including the parts that are easiest to get
/// wrong, such as which host receives the API secret and what happens when Shopify refuses.
/// </remarks>
public sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<ScriptedResponse>> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<ScriptedResponse>> _pathRoutes = new(StringComparer.Ordinal);
    private readonly List<CapturedRequest> _requests = [];

    /// <summary>Every request the code under test made.</summary>
    public IReadOnlyList<CapturedRequest> Requests => _requests;

    /// <summary>
    /// Requests to the OAuth token endpoint.
    /// </summary>
    /// <remarks>
    /// Validation necessarily fetches the shop's key set from the same host, so a test about the
    /// exchange has to filter to the exchange or it counts the key fetch as an extra exchange.
    /// </remarks>
    public IReadOnlyList<CapturedRequest> TokenExchangeRequests =>
        _requests.Where(r => r.Uri.AbsolutePath.EndsWith("/admin/oauth/access_token", StringComparison.Ordinal)).ToList();

    /// <summary>Registers responses, consumed in order, for every request to a host.</summary>
    public ScriptedHttpMessageHandler OnHost(string host, params ScriptedResponse[] responses)
    {
        if (!_routes.TryGetValue(host, out var queue))
        {
            queue = new Queue<ScriptedResponse>();
            _routes[host] = queue;
        }

        foreach (var response in responses)
        {
            queue.Enqueue(response);
        }

        return this;
    }

    /// <summary>
    /// Registers responses for requests whose absolute path matches, whatever the host.
    /// </summary>
    /// <remarks>
    /// Needed because Shopify serves the key set and the OAuth token endpoint from the same host,
    /// so routing on host alone cannot distinguish them.
    /// </remarks>
    public ScriptedHttpMessageHandler OnPath(string path, params ScriptedResponse[] responses)
    {
        if (!_pathRoutes.TryGetValue(path, out var queue))
        {
            queue = new Queue<ScriptedResponse>();
            _pathRoutes[path] = queue;
        }

        foreach (var response in responses)
        {
            queue.Enqueue(response);
        }

        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        _requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body, headers));

        // Path routes win: they are the more specific registration.
        if (_pathRoutes.TryGetValue(request.RequestUri!.AbsolutePath, out var pathQueue) && pathQueue.Count > 0)
        {
            return pathQueue.Dequeue().Respond(request);
        }

        var host = request.RequestUri.Host;

        if (_routes.TryGetValue(host, out var queue) && queue.Count > 0)
        {
            return queue.Dequeue().Respond(request);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"{{\"error\":\"no scripted response for {host}\"}}", Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>A request the code under test made.</summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Uri">Target URI.</param>
/// <param name="Body">Request body, when there was one.</param>
/// <param name="Headers">Request headers, keyed case-insensitively.</param>
public sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>Builds a service provider wired to a scripted handler.</summary>
public static class TestServices
{
    /// <summary>Creates a provider with the toolkit registered and option overrides applied.</summary>
    /// <summary>The log sink the last-created provider writes to.</summary>
    public static SharedLogSink? LastLogSink { get; private set; }

    public static ServiceProvider Create(ScriptedHttpMessageHandler handler, Action<ShopifyOptions>? configure = null)
    {
        var services = new ServiceCollection();
        var sink = new SharedLogSink();
        LastLogSink = sink;

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(sink);
        });
        services.AddShopifyBlazor(configuration: null, configure: options =>
        {
            options.ApiKey = "test-api-key";
            options.ApiSecret = "0123456789abcdef0123456789abcdef";
            options.AppUrl = "https://test.ngrok-free.app";

            configure?.Invoke(options);
        }, validateOnStart: false);

        services.AddHttpClient(ShopifyHttpClients.TokenExchange).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(ShopifyHttpClients.AdminApi).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }
}
