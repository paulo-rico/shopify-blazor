using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shopify.Blazor.AppBridge;
using Shopify.Blazor.Components;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for <see cref="ShopifySaveBar"/>, the wrapper around App Bridge's contextual save bar.
/// </summary>
/// <remarks>
/// The bar itself is drawn by App Bridge inside the admin's toolbar, so what can be proved here is the
/// half that lives in this document: the <c>ui-save-bar</c> element App Bridge reads, the two native
/// buttons it forwards clicks to, and that exactly one show/hide is sent for each change of
/// <see cref="ShopifySaveBar.Visible"/>. App Bridge's own rendering cannot be exercised outside the
/// admin.
/// </remarks>
public sealed class SaveBarTests
{
    [Fact]
    public void Renders_one_save_bar_with_a_primary_and_a_secondary_button()
    {
        using var context = new SaveBarContext();

        var component = context.Render<ShopifySaveBar>();

        // App Bridge finds the bar by id, so an element without one is a bar that show() cannot find;
        // and it derives the two actions from the children, with the primary style from `variant`.
        var bar = component.Find("ui-save-bar");
        Assert.False(string.IsNullOrWhiteSpace(bar.GetAttribute("id")));

        var buttons = component.FindAll("button");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("primary", buttons[0].GetAttribute("variant"));
        Assert.Null(buttons[1].GetAttribute("variant"));
        Assert.Equal("Save", buttons[0].TextContent.Trim());
        Assert.Equal("Discard", buttons[1].TextContent.Trim());
    }

    [Fact]
    public void The_declared_id_names_the_bar_and_a_generated_one_is_used_without_it()
    {
        using var context = new SaveBarContext();

        var named = context.Render<ShopifySaveBar>(parameters => parameters.Add(x => x.Id, "settings-save-bar"));
        Assert.Equal("settings-save-bar", named.Find("ui-save-bar").GetAttribute("id"));

        var generated = context.Render<ShopifySaveBar>();
        Assert.NotEqual("settings-save-bar", generated.Find("ui-save-bar").GetAttribute("id"));
    }

    [Fact]
    public void Save_and_discard_reach_the_page_that_owns_the_form()
    {
        using var context = new SaveBarContext();
        var saves = 0;
        var discards = 0;

        var component = context.Render<ShopifySaveBar>(parameters => parameters
            .Add(x => x.OnSave, () => saves++)
            .Add(x => x.OnDiscard, () => discards++));

        // App Bridge forwards a click by calling click() on the light-DOM button, so a handler that
        // does not reach the page is a save bar whose buttons do nothing while looking perfect.
        component.FindAll("button")[0].Click();
        component.FindAll("button")[1].Click();

        Assert.Equal(1, saves);
        Assert.Equal(1, discards);
    }

    [Fact]
    public void Each_change_of_visibility_is_sent_to_app_bridge_once()
    {
        using var context = new SaveBarContext();

        var component = context.Render<ShopifySaveBar>(parameters => parameters
            .Add(x => x.Id, "form-save-bar")
            .Add(x => x.Visible, false));

        // A bar that is never shown is correct for a clean form; the round trips below are the whole
        // point, so the count matters more than the call.
        Assert.Empty(context.Bridge.Shown);
        Assert.Empty(context.Bridge.Hidden);

        component.Render(parameters => parameters.Add(x => x.Visible, true));
        Assert.Equal(new[] { "form-save-bar" }, context.Bridge.Shown);

        // A re-render with the same value must not ask again: App Bridge keeps its own state and a
        // call per render would fight it, and a page renders on every keystroke.
        component.Render(parameters => parameters.Add(x => x.Visible, true));
        Assert.Single(context.Bridge.Shown);

        component.Render(parameters => parameters.Add(x => x.Visible, false));
        Assert.Equal(new[] { "form-save-bar" }, context.Bridge.Hidden);
    }

    [Fact]
    public void A_save_in_flight_marks_save_loading_and_disables_discard()
    {
        using var context = new SaveBarContext();

        var component = context.Render<ShopifySaveBar>(parameters => parameters.Add(x => x.Saving, true));

        // App Bridge reads a bare `loading` attribute on the primary child and the native `disabled`
        // property on the other, so these are the two things that must be in the markup.
        Assert.True(component.FindAll("button")[0].HasAttribute("loading"));
        Assert.True(component.FindAll("button")[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Discard_is_ignored_while_a_save_is_in_flight()
    {
        using var context = new SaveBarContext();
        var discards = 0;

        var component = context.Render<ShopifySaveBar>(parameters => parameters
            .Add(x => x.Saving, true)
            .Add(x => x.OnDiscard, () => discards++));

        component.FindAll("button")[1].Click();

        // The button is disabled and the handler refuses as well: a discard that races a save would
        // reload stored values over the write that is still in flight.
        Assert.Equal(0, discards);
    }

    [Fact]
    public async Task A_visible_bar_is_hidden_when_the_page_goes_away()
    {
        var context = new SaveBarContext();
        context.Render<ShopifySaveBar>(parameters => parameters
            .Add(x => x.Id, "leaving-save-bar")
            .Add(x => x.Visible, true));

        // Disposed asynchronously because the component is IAsyncDisposable: Blazor's synchronous
        // dispose path refuses one, and that refusal would hide a real regression behind a test error.
        await context.DisposeAsync();

        // The bar belongs to the admin, not to the page, so a bar the page forgets to hide covers the
        // next screen's toolbar and claims changes that no longer exist.
        Assert.Equal(new[] { "leaving-save-bar" }, context.Bridge.Hidden);
    }

    [Fact]
    public void Nothing_is_rendered_outside_the_admin()
    {
        using var context = new SaveBarContext(available: false);

        var component = context.Render<ShopifySaveBar>(parameters => parameters.Add(x => x.Visible, true));

        // App Bridge defines ui-save-bar only once it has connected to the admin, so an undefined
        // element would leave its two buttons visible as ordinary buttons at the top of the page.
        Assert.Empty(component.FindAll("ui-save-bar"));
        Assert.Empty(context.Bridge.Shown);
    }

    /// <summary>A bUnit context with the App Bridge interop layer replaced by a recorder.</summary>
    private sealed class SaveBarContext : BunitContext
    {
        public SaveBarContext(bool available = true)
        {
            Bridge = new RecordingAppBridge { Available = available };
            Services.AddSingleton<IAppBridgeInterop>(Bridge);
        }

        public RecordingAppBridge Bridge { get; }
    }

    /// <summary>Records what the save bar asked App Bridge to do.</summary>
    private sealed class RecordingAppBridge : IAppBridgeInterop
    {
        public bool Available { get; init; } = true;

        public List<string> Shown { get; } = [];

        public List<string> Hidden { get; } = [];

        public ValueTask<bool> ShowSaveBarAsync(string id)
        {
            Shown.Add(id);
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> HideSaveBarAsync(string id)
        {
            Hidden.Add(id);
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> IsAvailableAsync() => ValueTask.FromResult(Available);

        public ValueTask<string?> GetIdTokenAsync() => ValueTask.FromResult<string?>(null);

        public ValueTask<AppBridgeFetchResult<T>> FetchAsync<T>(string url, string method = "GET", object? body = null)
            => ValueTask.FromResult(AppBridgeFetchResult<T>.Failed("not used in these tests"));

        public ValueTask SetLoadingAsync(bool isLoading) => ValueTask.CompletedTask;

        public Task<T> WithLoadingAsync<T>(Func<Task<T>> action) => action();

        public ValueTask ShowToastAsync(string message, bool isError = false, int? durationMs = null)
            => ValueTask.CompletedTask;

        public ValueTask NavigateAsync(string url) => ValueTask.CompletedTask;

        public ValueTask<ShopifyAppUser?> GetCurrentUserAsync() => ValueTask.FromResult<ShopifyAppUser?>(null);

        public ValueTask<IReadOnlyList<string>?> GetGrantedScopesAsync()
            => ValueTask.FromResult<IReadOnlyList<string>?>(null);

        public ValueTask<IReadOnlyList<ShopifyPickedProduct>?> PickProductsAsync(
            int? multipleLimit = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ShopifyPickedProduct>?>(null);
    }
}
