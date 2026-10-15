using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>What the offline check of the Polaris interop asset found.</summary>
public enum StaticAssetOutcome
{
    /// <summary>The file was found on disk, so the asset will be published.</summary>
    Found,

    /// <summary>It could not be decided, so the report says how to confirm it in a browser.</summary>
    Indeterminate,

    /// <summary>Polaris.Blazor is referenced but the file is definitely not there.</summary>
    Missing,
}

/// <summary>The result of looking for <c>polaris-interop.js</c>.</summary>
/// <param name="Outcome">What was found.</param>
/// <param name="Path">Where it was found, or the place that was searched.</param>
/// <param name="Reason">One line explaining it.</param>
/// <param name="Hint">What to do, or how to confirm it, when it is not a clean PASS.</param>
public sealed record StaticAssetCheckResult(StaticAssetOutcome Outcome, string? Path, string Reason, string? Hint);

/// <summary>
/// Checks, entirely offline, that <c>/_content/Polaris.Blazor/polaris-interop.js</c> will resolve.
/// </summary>
/// <remarks>
/// <para>
/// The failure this prevents is the worst one in the toolkit: the app builds, the page renders, and
/// every control does nothing because the interop module 404'd. It is logged as
/// <c>Polaris interop module … could not be loaded</c> and nowhere else (AGENTS.md #3).
/// </para>
/// <para>
/// The check is a proxy, and it says so when it cannot decide. It finds the file the way MSBuild
/// would: through the NuGet cache for a <c>PackageReference</c>, or in the source project's
/// <c>wwwroot</c> for a <c>ProjectReference</c>. It never fetches anything — a browser check of
/// the real URL is still the only conclusive test, so the WARN branch names the URL to open.
/// </para>
/// </remarks>
public static class StaticAssetCheck
{
    /// <summary>The URL the browser must be able to fetch.</summary>
    public const string PublicUrl = "/_content/Polaris.Blazor/polaris-interop.js";

    /// <summary>The file that has to ship.</summary>
    public const string FileName = "polaris-interop.js";

    /// <summary>Runs the check for the project described by <paramref name="references"/>.</summary>
    public static StaticAssetCheckResult Evaluate(
        IFileSystem fileSystem,
        IEnvironment environment,
        ProjectReferences references,
        string projectDirectory)
    {
        // A project reference wins: that is the shape the template and the sample app use, and
        // the source project's wwwroot is the file that gets served.
        var projectReference = references.Projects
            .Select(reference => ProjectFileReader.ReadProjectReferencePath(reference, projectDirectory))
            .FirstOrDefault(reference => Path.GetFileNameWithoutExtension(reference.FullPath)
                .Equals("Polaris.Blazor", StringComparison.OrdinalIgnoreCase));

        if (projectReference is not null)
        {
            return CheckProjectReference(fileSystem, projectReference);
        }

        if (!references.Packages.Any(reference =>
                reference.Equals("Polaris.Blazor", StringComparison.OrdinalIgnoreCase)))
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Indeterminate,
                null,
                "Polaris.Blazor is not referenced by this project, so the interop asset has no source.",
                $"Add a reference to Polaris.Blazor; a page that uses the components needs {PublicUrl}.");
        }

        return CheckPackageReference(fileSystem, environment, projectDirectory);
    }

    private static StaticAssetCheckResult CheckProjectReference(
        IFileSystem fileSystem,
        ProjectReferencePath reference)
    {
        var projectDirectory = Path.GetDirectoryName(reference.FullPath);

        if (projectDirectory is null || !fileSystem.DirectoryExists(projectDirectory))
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Indeterminate,
                reference.FullPath,
                $"the Polaris.Blazor project referenced by '{reference.Original}' is not on disk.",
                $"Restore it, or build once so the project exists at {reference.FullPath}, then re-run doctor.");
        }

        var candidate = Path.Combine(projectDirectory, "wwwroot", FileName);

        if (fileSystem.FileExists(candidate))
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Found,
                candidate,
                $"found in the referenced project's wwwroot: {candidate}",
                null);
        }

        // A Razor-SDK library must also declare the file, but that is a different failure with a
        // different fix, and it is worth saying which one this is.
        return new StaticAssetCheckResult(
            StaticAssetOutcome.Missing,
            candidate,
            $"Polaris.Blazor is referenced as a project, but {candidate} does not exist.",
            "The interop script did not ship, so every control will render and do nothing. If the library " +
            "was just moved, check that it still contains wwwroot/polaris-interop.js and that its project " +
            "uses Microsoft.NET.Sdk.Razor; otherwise take it from the toolkit's src/Polaris.Blazor/wwwroot.");
    }

    private static StaticAssetCheckResult CheckPackageReference(
        IFileSystem fileSystem,
        IEnvironment environment,
        string projectDirectory)
    {
        if (NuGetPackageRoot.Find(fileSystem, environment, projectDirectory) is not { } root)
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Indeterminate,
                null,
                "Polaris.Blazor is a PackageReference, but the NuGet package folder could not be located offline.",
                $"Set NUGET_PACKAGES, or confirm the asset in the browser: open {PublicUrl} and check that " +
                "the response is JavaScript rather than a 404.");
        }

        var packageDirectory = Path.Combine(root, "polaris.blazor");

        if (!fileSystem.DirectoryExists(packageDirectory))
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Indeterminate,
                packageDirectory,
                $"Polaris.Blazor is a PackageReference but no restored package exists under {packageDirectory}.",
                $"Run 'dotnet restore' first, or confirm the asset in the browser at {PublicUrl}.");
        }

        var candidates = fileSystem
            .EnumerateFilesRecursive(packageDirectory, FileName)
            .ToList();

        if (candidates.Count > 0)
        {
            return new StaticAssetCheckResult(
                StaticAssetOutcome.Found,
                candidates[0],
                $"found in the restored package: {candidates[0]}",
                null);
        }

        return new StaticAssetCheckResult(
            StaticAssetOutcome.Missing,
            packageDirectory,
            $"the restored Polaris.Blazor package under {packageDirectory} does not contain {FileName}.",
            "That package version was packed without its static web assets, so the browser gets a 404 for " +
            $"{PublicUrl} and every control is inert. Upgrade to a version that ships the file, or reference " +
            "the toolkit's src/Polaris.Blazor project directly.");
    }
}

/// <summary>Locates the NuGet global packages folder without any network access.</summary>
public static class NuGetPackageRoot
{
    /// <summary>
    /// Finds the packages folder: the <c>NUGET_PACKAGES</c> variable, then the folder
    /// <c>project.assets.json</c> records, then the platform default under the user profile.
    /// </summary>
    public static string? Find(IFileSystem fileSystem, IEnvironment environment, string projectDirectory)
    {
        if (environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            && fileSystem.DirectoryExists(configured))
        {
            return configured;
        }

        var fromAssets = ReadFromAssetsFile(fileSystem, projectDirectory);

        if (fromAssets is not null)
        {
            return fromAssets;
        }

        var home = environment.GetEnvironmentVariable("HOME")
            ?? environment.GetEnvironmentVariable("USERPROFILE");

        if (home is { Length: > 0 })
        {
            var fallback = Path.Combine(home, ".nuget", "packages");

            if (fileSystem.DirectoryExists(fallback))
            {
                return fallback;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads <c>packageFolders</c> out of <c>obj/project.assets.json</c>, which is where a restore
    /// records the folder it used — authoritative, and available offline.
    /// </summary>
    private static string? ReadFromAssetsFile(IFileSystem fileSystem, string projectDirectory)
    {
        var path = Path.Combine(projectDirectory, "obj", "project.assets.json");

        if (!fileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(fileSystem.ReadAllText(path));

            if (document.RootElement.TryGetProperty("packageFolders", out var folders)
                && folders.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var folder in folders.EnumerateObject())
                {
                    if (fileSystem.DirectoryExists(folder.Name))
                    {
                        return folder.Name;
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // An unreadable assets file just means the next fallback is used.
        }

        return null;
    }
}
