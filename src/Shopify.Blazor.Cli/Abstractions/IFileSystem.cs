namespace Shopify.Blazor.Cli.Abstractions;

/// <summary>
/// The narrow slice of the filesystem the CLI needs, so a command can be tested without touching
/// a real disk.
/// </summary>
/// <remarks>
/// Deliberately small and synchronous. Every method here exists because a command calls it; a
/// broad <c>System.IO</c> mirror would make the fake harder to trust than the code it stands in
/// for. Nothing here deletes: the CLI must never remove anything.
/// </remarks>
public interface IFileSystem
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Whether a directory exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary>Reads a UTF-8 text file.</summary>
    string ReadAllText(string path);

    /// <summary>
    /// Writes UTF-8 text to <paramref name="path"/>, creating parent directories as needed.
    /// </summary>
    void WriteAllText(string path, string contents);

    /// <summary>Creates <paramref name="path"/> and any missing parents.</summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Returns the immediate child paths (files and directories) of <paramref name="path"/>, or an
    /// empty sequence when the directory does not exist.
    /// </summary>
    IEnumerable<string> EnumerateFileSystemEntries(string path);

    /// <summary>Enumerates files matching <paramref name="searchPattern"/> directly in <paramref name="path"/>.</summary>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);

    /// <summary>Enumerates files matching <paramref name="searchPattern"/> in <paramref name="path"/> and below.</summary>
    IEnumerable<string> EnumerateFilesRecursive(string path, string searchPattern);

    /// <summary>Enumerates immediate child directories of <paramref name="path"/>.</summary>
    IEnumerable<string> EnumerateDirectories(string path);
}
