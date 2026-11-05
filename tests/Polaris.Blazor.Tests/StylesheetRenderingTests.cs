using Bunit;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// The head links for this package's stylesheets.
/// </summary>
/// <remarks>
/// <para>
/// These are asserted because their absence is invisible here and obvious on screen. The interop module
/// loads the same three files, but only once the Blazor circuit starts, and a full page load paints long
/// before that: measured on the deployed jeweller app, the page rendered its 53 disclosures at 3.5 s with
/// the accordion's rules absent and the stylesheet arrived at 6.8 s — 15–20 seconds on a slower
/// connection. The page looked like it had lost its styling while the markup was correct.
/// </para>
/// <para>
/// A page renders these links in its head, so they are part of the first paint; the module's own
/// injection stays as the fallback and skips a file that is already linked.
/// </para>
/// </remarks>
public sealed class StylesheetRenderingTests
{
    [Fact]
    public void Every_stylesheet_is_a_link_in_the_document()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisStylesheets>();

        var links = component.FindAll("link");

        Assert.Equal(3, links.Count);
        Assert.All(links, link => Assert.Equal("stylesheet", link.GetAttribute("rel")));
        Assert.Contains(links, link => link.GetAttribute("href")!.Contains("polaris-accordion.css"));
        Assert.Contains(links, link => link.GetAttribute("href")!.Contains("polaris-range-slider.css"));
        Assert.Contains(links, link => link.GetAttribute("href")!.Contains("polaris-grid.css"));
    }

    [Fact]
    public void Every_link_carries_the_build_stamp()
    {
        // A static web asset is served with an ETag and no Cache-Control, so an unstamped href lets a
        // returning browser apply the previous deploy's rules to the new markup — which is exactly the
        // "no styling" report that produced this component.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisStylesheets>();

        foreach (var link in component.FindAll("link"))
        {
            var href = link.GetAttribute("href")!;

            Assert.StartsWith("_content/Polaris.Blazor/", href);
            Assert.EndsWith($"?v={PolarisStaticAssets.Version}", href);
            Assert.Equal(href, link.GetAttribute("data-polaris-stylesheet"));
        }
    }

    [Fact]
    public void One_stamp_covers_the_links_and_the_module()
    {
        // The stamp is the assembly's module version id: it changes on every build, so one deploy keeps
        // its cache and the next gets new URLs. The links and the interop module must read the same
        // value, or a browser can hold a stylesheet from one build beside a module from another.
        Assert.Matches("^[0-9a-f]{32}$", PolarisStaticAssets.Version);
        Assert.Contains(
            $"?v={PolarisStaticAssets.Version}",
            PolarisStaticAssets.Url("polaris-accordion.css"));
    }
}
