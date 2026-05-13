namespace Org.Sdk.Testing;

/// <summary>
/// Represents a file system entry with a path.
/// Implemented by <see cref="Solution"/> and <see cref="Project"/>.
/// </summary>
public interface IFileSystemEntry
{
    /// <summary>
    /// Gets the absolute path to the file.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Gets the directory containing the file.
    /// </summary>
    string Directory { get; }
}
