namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-app-nav&gt;</c>: the navigation menu for your app inside the Shopify admin.</summary>
/// <remarks>
/// <para>
/// This is an <strong>App Bridge</strong> component, not a Polaris component: it configures part of
/// the admin that <em>surrounds</em> your app rather than drawing inside it. On desktop it renders in
/// the admin's left sidebar; on mobile it appears in a dropdown from the title bar. Because it belongs
/// to the admin's chrome, it draws nothing when the app is opened outside the admin — which is why a
/// standalone browser session shows no navigation rather than a broken one.
/// </para>
/// <para>
/// Add one <see cref="PolarisLink"/> per item. The app's name in the sidebar already links to your
/// home route (<c>/</c>), so a link carrying <c>rel="home"</c> is only needed when the home page lives
/// somewhere else — and that link is then <em>hidden</em> from the rendered menu, because the admin
/// draws the landing page itself. Pass it through <see cref="PolarisElementComponentBase.AdditionalAttributes"/>:
/// </para>
/// <code>
/// &lt;PolarisAppNav&gt;
///     &lt;PolarisLink Href="/app" rel="home"&gt;Home&lt;/PolarisLink&gt;
///     &lt;PolarisLink Href="/app/templates"&gt;Templates&lt;/PolarisLink&gt;
/// &lt;/PolarisAppNav&gt;
/// </code>
/// <para>
/// Three rules that fail quietly: there must be <strong>exactly one</strong> app nav per document and
/// it must be in the body; navigation <strong>cannot be nested</strong> (one level only, so a submenu
/// silently does nothing); and a <c>href</c> must be a relative path inside the app, because the admin
/// navigates the app's own frame rather than following a URL.
/// </para>
/// </remarks>
public sealed class PolarisAppNav : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-app-nav";
}
