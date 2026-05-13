using System.Diagnostics.CodeAnalysis;

namespace Org.Sdk.Testing;

/// <summary>
/// Represents a project within a test solution.
/// Can be either a traditional .csproj-based project or a file-based app.
/// </summary>
public sealed class Project : IFileSystemEntry
{
    /// <summary>
    /// Gets the absolute path to the project file (.csproj) or source file (.cs for file-based apps).
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the directory containing the project.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Gets the type of project.
    /// </summary>
    public ProjectType ProjectType { get; }

    internal Project(string path, ProjectType projectType)
    {
        Path = path;
        Directory = System.IO.Path.GetDirectoryName(path)!;
        ProjectType = projectType;
    }

    /// <summary>
    /// Adds a C# source file to the project directory.
    /// </summary>
    /// <param name="relativePath">Relative path within the project directory.</param>
    /// <param name="content">C# source code content.</param>
    /// <returns>The absolute path to the created file.</returns>
    public string AddCSharpFile(string relativePath, [StringSyntax("csharp")] string content) =>
        AddFile(relativePath, content, ensureNewline: true);

    /// <summary>
    /// Adds a Program.cs file to the project directory.
    /// Convenience method equivalent to AddCSharpFile("Program.cs", content).
    /// </summary>
    /// <param name="content">C# source code content.</param>
    /// <returns>The absolute path to the created file.</returns>
    public string AddProgram([StringSyntax("csharp")] string content) =>
        AddCSharpFile("Program.cs", content);

    /// <summary>
    /// Adds an XML file to the project directory.
    /// </summary>
    /// <param name="relativePath">Relative path within the project directory.</param>
    /// <param name="content">XML content.</param>
    /// <returns>The absolute path to the created file.</returns>
    public string AddXmlFile(string relativePath, [StringSyntax("xml")] string content) =>
        AddFile(relativePath, content, ensureNewline: false);

    /// <summary>
    /// Adds a text file to the project directory.
    /// </summary>
    /// <param name="relativePath">Relative path within the project directory.</param>
    /// <param name="content">Text content.</param>
    /// <returns>The absolute path to the created file.</returns>
    public string AddTextFile(string relativePath, string content) =>
        AddFile(relativePath, content, ensureNewline: false);

    private string AddFile(string relativePath, string content, bool ensureNewline)
    {
        var fullPath = System.IO.Path.Combine(Directory, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Ensure source files end with a newline
        if (ensureNewline && content.Length > 0 && content[^1] is not '\n')
        {
            content += '\n';
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
