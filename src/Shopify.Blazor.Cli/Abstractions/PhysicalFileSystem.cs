namespace Shopify.Blazor.Cli.Abstractions;

/// <summary>
/// The filesystem, for real. Every path is normalised to a full path so that a later
/// <see cref="IFileSystem.FileExists(string)"/> on the same string means the same thing.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <inheritdoc />
    public void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFileSystemEntries(string path) =>
        Directory.Exists(path) ? Directory.EnumerateFileSystemEntries(path) : [];

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern) =>
        Directory.Exists(path) ? Directory.EnumerateFiles(path, searchPattern) : [];

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFilesRecursive(string path, string searchPattern) =>
        Directory.Exists(path)
            ? Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories)
            : [];

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.EnumerateDirectories(path) : [];
}
