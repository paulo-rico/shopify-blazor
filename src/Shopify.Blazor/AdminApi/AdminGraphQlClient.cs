using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shopify.Blazor.AdminApi;

/// <summary>One GraphQL error returned by the Admin API.</summary>
/// <param name="Message">The error message.</param>
/// <param name="Path">The response path the error applies to, when Shopify supplied one.</param>
/// <param name="Extensions">Shopify's error code and metadata, when supplied.</param>
public sealed record AdminGraphQlError(string Message, IReadOnlyList<string>? Path, JsonElement? Extensions)
{
    /// <summary>Shopify's machine-readable error code, for example <c>THROTTLED</c>.</summary>
    public string? Code => Extensions is { ValueKind: JsonValueKind.Object } extensions
        && extensions.TryGetProperty("code", out var code)
        && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
}

/// <summary>
/// The outcome of an Admin API GraphQL call.
/// </summary>
/// <remarks>
/// A GraphQL failure is <strong>not</strong> an HTTP failure. Shopify answers most query
/// problems with <c>200 OK</c> and a body containing an <c>errors</c> array, so
/// <see cref="IsSuccess"/> checks both the status code and that array. Testing only the status
/// code is the classic way to treat a failed query as an empty result.
/// </remarks>
public sealed class AdminGraphQlResponse
{
    internal AdminGraphQlResponse(
        HttpStatusCode statusCode,
        string rawBody,
        JsonElement? data,
        IReadOnlyList<AdminGraphQlError> errors,
        string? apiVersionServed)
    {
        StatusCode = statusCode;
        RawBody = rawBody;
        Data = data;
        Errors = errors;
        ApiVersionServed = apiVersionServed;
    }

    /// <summary>HTTP status returned by Shopify.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The untouched response body, for diagnostics and logging.</summary>
    public string RawBody { get; }

    /// <summary>The <c>data</c> element, or <c>null</c> when the response carried none.</summary>
    public JsonElement? Data { get; }

    /// <summary>GraphQL errors, empty when there were none.</summary>
    public IReadOnlyList<AdminGraphQlError> Errors { get; }

    /// <summary>
    /// The API version Shopify actually served, read from the <c>X-Shopify-API-Version</c>
    /// response header. A value different from the one requested means the requested version was
    /// inaccessible and Shopify fell forward, which changes behaviour silently.
    /// </summary>
    public string? ApiVersionServed { get; }

    /// <summary>True only when the call succeeded <em>and</em> carried no GraphQL errors.</summary>
    public bool IsSuccess => StatusCode == HttpStatusCode.OK && Errors.Count == 0 && Data is not null;

    /// <summary>A single-line summary suitable for logs or an error banner.</summary>
    public string DescribeFailure()
        => Errors.Count > 0
            ? string.Join("; ", Errors.Select(e => e.Message))
            : $"The Admin API returned HTTP {(int)StatusCode}.";

    /// <summary>
    /// Deserializes <see cref="Data"/> as <typeparamref name="T"/>. Throws
    /// <see cref="InvalidOperationException"/> when the response failed, so a missed error check
    /// surfaces here rather than as a mysterious <c>null</c>.
    /// </summary>
    public T Deserialize<T>(JsonSerializerOptions? options = null)
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException($"The Admin API call failed, so there is nothing to deserialize: {DescribeFailure()}");
        }

        return Data!.Value.Deserialize<T>(options)
            ?? throw new InvalidOperationException("The Admin API response deserialized to null.");
    }
}

/// <summary>Executes GraphQL requests against the Shopify Admin API.</summary>
public interface IAdminGraphQlClient
{
    /// <summary>Runs a query or mutation against the shop's Admin API.</summary>
    /// <param name="query">The GraphQL document.</param>
    /// <param name="variables">Variables, or <c>null</c>.</param>
    /// <param name="accessToken">The Admin API access token for the target shop.</param>
    /// <param name="shopDomain">The canonical myshopify domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AdminGraphQlResponse> ExecuteAsync(
        string query,
        object? variables,
        ShopifyAccessToken accessToken,
        string shopDomain,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IAdminGraphQlClient"/> implementation over <c>HttpClient</c>.</summary>
public sealed class AdminGraphQlClient : IAdminGraphQlClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ShopifyOptions> _options;
    private readonly ILogger<AdminGraphQlClient> _logger;

    /// <summary>Creates a client.</summary>
    public AdminGraphQlClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ShopifyOptions> options,
        ILogger<AdminGraphQlClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminGraphQlResponse> ExecuteAsync(
        string query,
        object? variables,
        ShopifyAccessToken accessToken,
        string shopDomain,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(accessToken);

        var version = _options.CurrentValue.AdminApiVersion;
        var endpoint = $"https://{shopDomain}/admin/api/{version}/graphql.json";

        var payload = new JsonObject { ["query"] = query };
        if (variables is not null)
        {
            payload["variables"] = JsonSerializer.SerializeToNode(variables, SerializerOptions);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Shopify-Access-Token", accessToken.AccessToken);

        var client = _httpClientFactory.CreateClient(ShopifyHttpClients.AdminApi);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var servedVersion = response.Headers.TryGetValues("X-Shopify-API-Version", out var values)
            ? values.FirstOrDefault()
            : null;

        if (servedVersion is not null && !string.Equals(servedVersion, version, StringComparison.OrdinalIgnoreCase))
        {
            // Shopify fell forward: the requested version was inaccessible and an older stable
            // release served the request. Nothing errors, so this warning is the only signal.
            _logger.LogWarning(
                "Requested Admin API version {Requested} but Shopify served {Served}. The requested version is no longer accessible.",
                version,
                servedVersion);
        }

        JsonElement? data = null;
        var errors = new List<AdminGraphQlError>();
        string? parseError = null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind != JsonValueKind.Null)
                {
                    data = dataElement.Clone();
                }

                if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in errorsElement.EnumerateArray())
                    {
                        var message = item.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                            ? m.GetString() ?? "Unknown GraphQL error."
                            : "Unknown GraphQL error.";

                        IReadOnlyList<string>? path = null;
                        if (item.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.Array)
                        {
                            path = p.EnumerateArray().Select(x => x.ToString()).ToArray();
                        }

                        JsonElement? extensions = null;
                        if (item.TryGetProperty("extensions", out var e) && e.ValueKind == JsonValueKind.Object)
                        {
                            extensions = e.Clone();
                        }

                        errors.Add(new AdminGraphQlError(message, path, extensions));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            parseError = ex.Message;
        }

        if (parseError is not null)
        {
            _logger.LogError("The Admin API response was not valid JSON: {Message}", parseError);
            errors.Add(new AdminGraphQlError("The Admin API response was not valid JSON.", null, null));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The token was revoked or expired. Callers should drop the cached token and
            // exchange again rather than showing the merchant an error.
            _logger.LogWarning("The Admin API rejected the access token for {Shop} with 401.", shopDomain);
        }

        return new AdminGraphQlResponse(response.StatusCode, body, data, errors, servedVersion);
    }
}
