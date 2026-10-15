using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// Wraps <c>&lt;s-app-window&gt;</c>: a fullscreen window, hosted by the admin, that loads one of the
/// app's own routes.
/// </summary>
/// <remarks>
/// <para>
/// This is an <strong>App Bridge</strong> component, so the window it opens belongs to the admin: the
/// content named by <see cref="Src"/> is loaded in an iframe covering the screen, with a top bar the
/// admin controls so a merchant can always leave. Use it for a workflow that genuinely needs the whole
/// screen — a multi-step editor, a bulk editor, a long form.
/// </para>
/// <para>
/// <strong>Prefer <see cref="PolarisModal"/> for an ordinary dialog.</strong> The Polaris modal decides
/// for itself whether to render inside the app or as a fullscreen overlay, and it needs no App Bridge
/// API at all; this element is for the cases where that is not enough.
/// </para>
/// <para>
/// There are two ways to open it, and they are not interchangeable:
/// </para>
/// <list type="bullet">
/// <item>
/// <em>Declaratively</em>, which needs no code: give the window an <see cref="PolarisElementComponentBase.Id"/>
/// and point a button at it with <c>command="--show"</c> and <c>commandFor="that-id"</c>.
/// </item>
/// <item>
/// <em>From C#</em>, with <see cref="ShowAsync"/>, <see cref="HideAsync"/> or <see cref="ToggleAsync"/>,
/// which reach the element's own methods through interop.
/// </item>
/// </list>
/// <para>
/// A save bar for a form <em>inside</em> the window must be on that form
/// (<c>data-save-bar</c>), not on the parent page: a save bar on the parent belongs to the page and
/// renders behind the window, where Save and Discard cannot be clicked.
/// </para>
/// </remarks>
public sealed class PolarisAppWindow : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-app-window";

    /// <summary>
    /// The route inside this app whose content the window displays. Required.
    /// </summary>
    /// <remarks>
    /// A relative path such as <c>/products/select</c>. It is loaded in an iframe, so it must be a
    /// route this app serves rather than an external URL.
    /// </remarks>
    [Parameter] public string? Src { get; set; }

    /// <summary>Raised when the window opens.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnShow { get; set; }

    /// <summary>Raised when the window closes.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnHide { get; set; }

    /// <summary>Opens the window and shows the content at <see cref="Src"/>.</summary>
    /// <returns>A task that completes when the request has been sent.</returns>
    /// <remarks>Any unsaved changes in a form carrying <c>data-save-bar</c> are confirmed before it closes.</remarks>
    public ValueTask ShowAsync() => Interop.InvokeElementMethodAsync(Element, "show");

    /// <summary>Closes the window.</summary>
    /// <returns>A task that completes when the request has been sent.</returns>
    public ValueTask HideAsync() => Interop.InvokeElementMethodAsync(Element, "hide");

    /// <summary>Opens the window if it is closed, and closes it if it is open.</summary>
    /// <returns>A task that completes when the request has been sent.</returns>
    public ValueTask ToggleAsync() => Interop.InvokeElementMethodAsync(Element, "toggle");

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Show, OnShow);
        events.On(PolarisEvents.Hide, OnHide);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["src"] = Src;
    }
}
