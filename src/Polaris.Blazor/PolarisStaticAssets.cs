namespace Polaris.Blazor;

/// <summary>
/// The static files this package serves, addressed with the build's version stamp.
/// </summary>
/// <remarks>
/// <para>
/// A static web asset is served with an <c>ETag</c> and no <c>Cache-Control</c>, so a browser's heuristic
/// freshness rule may reuse a copy from before a deploy for hours. Every URL this package hands out
/// therefore carries <see cref="Version"/>: the assembly's module version id, which changes on every
/// build. One deploy keeps its cache; the next gets new URLs.
/// </para>
/// <para>
/// This is the one place that stamp is read. The module import (<see cref="Interop.PolarisInterop"/>)
/// and the head links (<c>PolarisStylesheets</c>) must agree, or a browser can end up holding a
/// stylesheet from one build beside a module from another.
/// </para>
/// </remarks>
public static class PolarisStaticAssets
{
    /// <summary>The stamp appended to every asset URL in this build.</summary>
    public static string Version { get; } =
        typeof(PolarisStaticAssets).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    /// <summary>
    /// The URL of a file in this package's <c>wwwroot</c>, with the build stamp appended.
    /// </summary>
    /// <param name="fileName">The file's name, for example <c>polaris-accordion.css</c>.</param>
    /// <returns>A root-relative URL, stamped.</returns>
    public static string Url(string fileName) => $"_content/Polaris.Blazor/{fileName}?v={Version}";
}
