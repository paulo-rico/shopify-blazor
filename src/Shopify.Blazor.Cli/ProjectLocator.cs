using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>A discovered MSBuild project file.</summary>
/// <param name="FilePath">The full path of the <c>.csproj</c>.</param>
/// <param name="Directory">The directory containing it.</param>
public sealed record ProjectFile(string FilePath, string Directory)
{
    /// <summary>The file name, for messages.</summary>
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>The outcome of looking for a project: the file, or one message saying why not.</summary>
/// <param name="Project">The project that was found, or <c>null</c>.</param>
/// <param name="Error">The reason nothing was found, or <c>null</c>.</param>
public sealed record ProjectSearchResult(ProjectFile? Project, string? Error)
{
    /// <summary>Whether a project was found.</summary>
    public bool Found => Project is not null;

    /// <summary>A found project.</summary>
    public static ProjectSearchResult Ok(ProjectFile project) => new(project, null);

    /// <summary>A failed search.</summary>
    public static ProjectSearchResult Fail(string error) => new(null, error);
}

/// <summary>
/// Finds the project a command should act on.
/// </summary>
/// <remarks>
/// The search walks upwards from the given path, so a developer can run <c>shopify-blazor doctor</c>
/// from a page's directory and still get the project. It never guesses: two project files in the
/// same directory is an error naming both, not a coin toss. The walk stops at the first directory
/// that looks like a repository root, so it cannot escape into a parent checkout and act on an
/// unrelated project.
/// </remarks>
public sealed class ProjectLocator
{
    /// <summary>Files and directories that mark the top of the project tree.</summary>
    /// <remarks>
    /// <c>global.json</c> is deliberately not one of them: it usually sits at the top of a
    /// repository and often above several projects, so treating it as a boundary would stop a
    /// search that is about to succeed one level further up.
    /// </remarks>
    private static readonly string[] BoundaryMarkers =
        [".git", ".hg", ".svn", "*.sln", "*.slnx", "*.slnf"];

    private readonly IFileSystem _fileSystem;

    /// <summary>Creates a locator over <paramref name="fileSystem"/>.</summary>
    public ProjectLocator(IFileSystem fileSystem) => _fileSystem = fileSystem;

    /// <summary>
    /// Finds the nearest project file at or above <paramref name="startPath"/>, which may be a
    /// directory, a project file, or <c>null</c> for <paramref name="currentDirectory"/>.
    /// </summary>
    public ProjectSearchResult Find(string? startPath, string currentDirectory)
    {
        var origin = string.IsNullOrWhiteSpace(startPath)
            ? currentDirectory
            : Path.GetFullPath(startPath, currentDirectory);

        // An explicit project file wins outright: the developer already decided.
        if (origin.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && _fileSystem.FileExists(origin))
        {
            return ProjectSearchResult.Ok(new ProjectFile(origin, Path.GetDirectoryName(origin) ?? currentDirectory));
        }

        var directory = _fileSystem.DirectoryExists(origin) ? origin : Path.GetDirectoryName(origin);

        if (directory is null)
        {
            return ProjectSearchResult.Fail($"'{startPath}' is not a directory and not a project file.");
        }

        var current = directory;

        while (!string.IsNullOrEmpty(current))
        {
            List<string> candidates;

            try
            {
                candidates = _fileSystem.EnumerateFiles(current, "*.csproj").ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory the sandbox refuses to list ends the search rather than the command.
                break;
            }

            if (candidates.Count == 1)
            {
                return ProjectSearchResult.Ok(new ProjectFile(candidates[0], current));
            }

            if (candidates.Count > 1)
            {
                return ProjectSearchResult.Fail(
                    $"Found more than one project file in '{current}': " +
                    string.Join(", ", candidates.Select(Path.GetFileName)) +
                    ". Pass --project <path> to choose one.");
            }

            // A repository root is as far as the search goes: above it is somebody else's project.
            if (IsBoundary(current))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }

        return ProjectSearchResult.Fail(
            $"No .csproj file was found in '{directory}' or any parent directory. " +
            "Run this from inside a project, or pass --project <path>.");
    }

    /// <summary>Whether <paramref name="directory"/> marks the top of the project tree.</summary>
    private bool IsBoundary(string directory)
    {
        foreach (var marker in BoundaryMarkers)
        {
            try
            {
                if (marker.Contains('*', StringComparison.Ordinal))
                {
                    if (_fileSystem.EnumerateFiles(directory, marker).Any())
                    {
                        return true;
                    }

                    continue;
                }

                if (_fileSystem.FileExists(Path.Combine(directory, marker))
                    || _fileSystem.DirectoryExists(Path.Combine(directory, marker)))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A parent directory the sandbox refuses to list is a boundary in practice.
                return true;
            }
        }

        return false;
    }
}
