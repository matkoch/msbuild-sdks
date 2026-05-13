namespace Org.Sdk.Testing;

/// <summary>
/// Specifies the type of project.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// A traditional .csproj-based project.
    /// </summary>
    ProjectBased,

    /// <summary>
    /// A file-based app (single .cs file with #:sdk directive).
    /// </summary>
    FileBased,
}
