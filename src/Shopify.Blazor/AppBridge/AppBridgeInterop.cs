using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Shopify.Blazor.AppBridge;

/// <summary>Options for the App Bridge interop layer.</summary>
public sealed class AppBridgeOptions
{
    /// <summary>
    /// Path of the ES module that ships with this package, served as a static web asset.
    /// </summary>
    public string ModulePath { get; set; } = "./_content/Shopify.Blazor/app-bridge.js";

    /// <summary>
    /// The versioned Polaris script to load, from Shopify's CDN.
    /// </summary>
    /// <remarks>
    /// <c>polaris-2.js</c> tracks the newest stable release inside Polaris 2, the channel Shopify
    /// documents for production; pin <c>polaris-2.0.js</c> instead to control exactly when the app
    /// moves. There is no equivalent channel for App Bridge: that URL always serves the current
    /// version.
    /// </remarks>
    public string PolarisScriptUrl { get; set; } = DesignSystemDefaults.PolarisScriptUrl;

    /// <summary>The App Bridge script, from Shopify's CDN.</summary>
    public string AppBridgeScriptUrl { get; set; } = DesignSystemDefaults.AppBridgeScriptUrl;
}

/// <summary>Shopify's CDN entry points for the embedded admin design system.</summary>
public static class DesignSystemDefaults
{
    /// <summary>App Bridge 4. Unversioned by design — Shopify serves the current release.</summary>
    public const string AppBridgeScriptUrl = "https://cdn.shopify.com/shopifycloud/app-bridge.js";

    /// <summary>Polaris web components, tracking the newest stable release in Polaris 2.</summary>
    public const string PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-2.js";
}

/// <summary>What a call to the app's own backend returned.</summary>
/// <typeparam name="T">The expected payload type.</typeparam>
/// <param name="IsSuccess">True when the response was a success status and the body deserialized.</param>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Data">The deserialized payload, when there was one.</param>
/// <param name="Error">A message suitable for showing the merchant, when the call failed.</param>
public sealed record AppBridgeFetchResult<T>(bool IsSuccess, int StatusCode, T? Data, string? Error)
{
    /// <summary>A result representing a call that never reached the server.</summary>
    public static AppBridgeFetchResult<T> Failed(string error) => new(false, 0, default, error);
}

/// <summary>App Bridge 4 features, reached from C#.</summary>
public interface IAppBridgeInterop
{
    /// <summary>True when the page is embedded in the Admin and App Bridge loaded.</summary>
    ValueTask<bool> IsAvailableAsync();

    /// <summary>
    /// Fetches a fresh session token. Send it to an app backend as a bearer credential; never
    /// treat it as an Admin API access token, and never exchange it in the browser.
    /// </summary>
    ValueTask<string?> GetIdTokenAsync();

    /// <summary>
    /// Calls one of the app's own endpoints with a freshly minted session token.
    /// </summary>
    ValueTask<AppBridgeFetchResult<T>> FetchAsync<T>(string url, string method = "GET", object? body = null);

    /// <summary>Starts or stops the Admin's header progress bar.</summary>
    ValueTask SetLoadingAsync(bool isLoading);

    /// <summary>Runs an action with the Admin's progress bar shown, clearing it in all cases.</summary>
    Task<T> WithLoadingAsync<T>(Func<Task<T>> action);

    /// <summary>Shows a toast. Outside the Admin the call is a no-op.</summary>
    ValueTask ShowToastAsync(string message, bool isError = false, int? durationMs = null);

    /// <summary>
    /// Whether the Admin can draw a contextual save bar on this page.
    /// </summary>
    /// <returns>
    /// True when App Bridge is connected and installed <c>shopify.saveBar</c>; false in a standalone
    /// browser tab, where the app has to draw a save bar of its own.
    /// </returns>
    /// <remarks>
    /// This is narrower than <see cref="IsAvailableAsync"/>: App Bridge can inject the <c>shopify</c>
    /// global on a page it cannot host, and installs the save bar API only once it has connected to the
    /// admin. Asking the wider question would let a page believe a bar it asked for will appear.
    /// </remarks>
    ValueTask<bool> IsSaveBarAvailableAsync();

    /// <summary>
    /// Shows the Admin's contextual save bar — "Unsaved changes", Discard, Save — for a
    /// <c>&lt;ui-save-bar&gt;</c> element on the page.
    /// </summary>
    /// <param name="id">The <c>id</c> of a <c>&lt;ui-save-bar&gt;</c> rendered by the page.</param>
    /// <returns>True when App Bridge was asked to show it; false outside the Admin, where there is no bar to show.</returns>
    /// <remarks>
    /// The bar is App Bridge's, not Polaris's: it draws in the admin's top toolbar, not in the app's
    /// own page, so it cannot be rendered as markup here. The element stays in this document and App
    /// Bridge clicks its button children in place, which is why a caller keeps its own handlers.
    /// Prefer <see cref="Components.ShopifySaveBar"/>, which renders the element and drives this.
    /// </remarks>
    ValueTask<bool> ShowSaveBarAsync(string id);

    /// <summary>Hides the contextual save bar shown by <see cref="ShowSaveBarAsync"/>.</summary>
    /// <param name="id">The <c>id</c> of the <c>&lt;ui-save-bar&gt;</c> element.</param>
    /// <returns>True when App Bridge was asked to hide it; false outside the Admin.</returns>
    /// <remarks>
    /// A bar left showing covers the admin's toolbar for the rest of the session and claims changes
    /// that no longer exist, so a page hides it whenever its form stops being dirty — including when
    /// it is torn down.
    /// </remarks>
    ValueTask<bool> HideSaveBarAsync(string id);

    /// <summary>Navigates within the Admin, or changes location outside it.</summary>
    ValueTask NavigateAsync(string url);

    /// <summary>The merchant using the app, or <c>null</c> outside the Admin.</summary>
    ValueTask<ShopifyAppUser?> GetCurrentUserAsync();

    /// <summary>The scopes currently granted, or <c>null</c> outside the Admin.</summary>
    ValueTask<IReadOnlyList<string>?> GetGrantedScopesAsync();

    /// <summary>
    /// Opens Shopify's product picker and returns what the merchant chose.
    /// </summary>
    /// <param name="multipleLimit">The most products the picker will allow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The picked products, an empty list when the merchant chose none, or <c>null</c> when the picker is
    /// not available at all — a standalone page rather than the Admin.
    /// </returns>
    /// <remarks>
    /// <strong>Null and empty mean different things here, and the difference is the merchant's.</strong> A
    /// cancelled picker resolves <c>undefined</c> rather than an empty array, so "cancelled" arrives as
    /// <c>null</c>: a screen that treated the two as one would clear a selection the merchant never touched.
    /// The action is <c>select</c> rather than App Bridge's default <c>add</c>, so a merchant cannot create
    /// products from inside the picker — this is for choosing what the shop costs, not for editing the
    /// catalogue.
    /// </remarks>
    ValueTask<IReadOnlyList<ShopifyPickedProduct>?> PickProductsAsync(
        int? multipleLimit = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A product the merchant picked out of Shopify's catalogue.</summary>
/// <param name="Id">Its global id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Handle">Its handle.</param>
/// <param name="ImageUrl">Its first image, when it has one.</param>
/// <param name="Variants">How many variants they selected, and which.</param>
/// <remarks>
/// The variant list matters: a shop costs pieces, not products, so a product whose variants were not
/// selected has nothing to price.
/// </remarks>
public sealed record ShopifyPickedProduct(
    string? Id,
    string? Title,
    string? Handle,
    string? ImageUrl,
    IReadOnlyList<ShopifyPickedVariant> Variants);

/// <summary>A variant of a picked product.</summary>
/// <param name="Id">Its global id.</param>
/// <param name="Title">Its title, which is Shopify's own variant label.</param>
/// <param name="Price">Its price as Shopify states it, as a string because that is how it arrives.</param>
/// <param name="Sku">Its SKU, when it has one.</param>
/// <param name="Options">The chosen options, as name/value pairs.</param>
public sealed record ShopifyPickedVariant(
    string? Id,
    string? Title,
    string? Price,
    string? Sku,
    IReadOnlyDictionary<string, string> Options);

/// <summary>The merchant using the app, as App Bridge reports them.</summary>
/// <param name="Id">Shopify's user id.</param>
/// <param name="FirstName">First name, when Shopify provides one.</param>
/// <param name="LastName">Last name, when Shopify provides one.</param>
/// <param name="Email">Email address, when Shopify provides one.</param>
/// <param name="Locale">The user's locale.</param>
public sealed record ShopifyAppUser(string? Id, string? FirstName, string? LastName, string? Email, string? Locale)
{
    /// <summary>A display name, falling back through the available fields.</summary>
    public string DisplayName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } full
            ? full
            : Email ?? Id ?? "Unknown user";
}

/// <summary>Default <see cref="IAppBridgeInterop"/> over <see cref="IJSRuntime"/>.</summary>
public sealed class AppBridgeInterop : IAppBridgeInterop, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;
    private readonly AppBridgeOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IJSObjectReference? _module;
    private bool _disposed;

    /// <summary>Creates the interop service.</summary>
    public AppBridgeInterop(IJSRuntime js, IOptions<AppBridgeOptions> options)
    {
        _js = js;
        _options = options.Value;
    }

    private async ValueTask<IJSObjectReference?> ModuleAsync()
    {
        if (_disposed)
        {
            return null;
        }

        if (_module is not null)
        {
            return _module;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_module is not null)
            {
                return _module;
            }

            _module = await _js.InvokeAsync<IJSObjectReference>("import", _options.ModulePath).ConfigureAwait(false);
            return _module;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
        {
            // Prerendering, or a circuit that has already gone away. Callers treat null as
            // "App Bridge is unavailable" and degrade rather than throwing.
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsAvailableAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("isAvailable").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetIdTokenAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return null;
        }

        try
        {
            return await module.InvokeAsync<string?>("idToken").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<AppBridgeFetchResult<T>> FetchAsync<T>(string url, string method = "GET", object? body = null)
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return AppBridgeFetchResult<T>.Failed("JavaScript interop is unavailable.");
        }

        try
        {
            var envelope = await module
                .InvokeAsync<FetchEnvelope>("fetchWithSessionToken", url, new { method, body })
                .ConfigureAwait(false);

            if (envelope is null)
            {
                return AppBridgeFetchResult<T>.Failed("The request returned no result.");
            }

            if (!envelope.Ok)
            {
                return new AppBridgeFetchResult<T>(false, envelope.Status, default, envelope.Error ?? "The request failed.");
            }

            T? data = default;

            if (envelope.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element)
            {
                data = element.Deserialize<T>(SerializerOptions);
            }
            else if (typeof(T) == typeof(JsonElement) || typeof(T) == typeof(object))
            {
                data = (T?)(object?)envelope.Data;
            }

            return new AppBridgeFetchResult<T>(true, envelope.Status, data, null);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            // The usual cause is that the page is open outside the Admin, where shopify.idToken()
            // does not exist. Say so rather than surfacing a JS exception.
            return AppBridgeFetchResult<T>.Failed(
                "App Bridge could not provide a session token. Open this page from inside the Shopify Admin.");
        }
    }

    /// <inheritdoc />
    public async ValueTask SetLoadingAsync(bool isLoading)
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("loading", isLoading).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
        }
    }

    /// <inheritdoc />
    public async Task<T> WithLoadingAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await SetLoadingAsync(true).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            // Not the happy path: the indicator persists until it is explicitly stopped, so
            // missing this call leaves a progress bar running in the merchant's admin.
            await SetLoadingAsync(false).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask ShowToastAsync(string message, bool isError = false, int? durationMs = null)
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("toast", message, new { isError, duration = durationMs }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsSaveBarAvailableAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("canShowSaveBar").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> ShowSaveBarAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("showSaveBar", id).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> HideSaveBarAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return false;
        }

        try
        {
            return await module.InvokeAsync<bool>("hideSaveBar", id).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask NavigateAsync(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("navigate", url).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<ShopifyAppUser?> GetCurrentUserAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return null;
        }

        try
        {
            var user = await module.InvokeAsync<UserDto?>("currentUser").ConfigureAwait(false);

            return user is null
                ? null
                : new ShopifyAppUser(user.Id, user.FirstName, user.LastName, user.Email, user.Locale);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ShopifyPickedProduct>?> PickProductsAsync(
        int? multipleLimit = null,
        CancellationToken cancellationToken = default)
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return null;
        }

        try
        {
            var picked = await module
                .InvokeAsync<PickedProductDto[]?>("pickProducts", cancellationToken, multipleLimit)
                .ConfigureAwait(false);

            return picked?
                .Select(product => new ShopifyPickedProduct(
                    product.Id,
                    product.Title,
                    product.Handle,
                    product.ImageUrl,
                    (product.Variants ?? [])
                        .Select(variant => new ShopifyPickedVariant(
                            variant.Id,
                            variant.Title,
                            variant.Price,
                            variant.Sku,
                            variant.Options ?? new Dictionary<string, string>()))
                        .ToArray()))
                .ToArray();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>?> GetGrantedScopesAsync()
    {
        var module = await ModuleAsync().ConfigureAwait(false);
        if (module is null)
        {
            return null;
        }

        try
        {
            return await module.InvokeAsync<string[]?>("grantedScopes").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException)
            {
            }

            _module = null;
        }

        _gate.Dispose();
    }

    private sealed record FetchEnvelope(bool Ok, int Status, JsonElement Data, string? Error);

    private sealed record UserDto(string? Id, string? FirstName, string? LastName, string? Email, string? Locale);

    private sealed record PickedProductDto(
        string? Id,
        string? Title,
        string? Handle,
        string? ImageUrl,
        PickedVariantDto[]? Variants);

    private sealed record PickedVariantDto(
        string? Id,
        string? Title,
        string? Price,
        string? Sku,
        Dictionary<string, string>? Options);
}
