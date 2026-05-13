using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

namespace Org.Sdk.Testing;

/// <summary>
/// Represents a test solution that can contain projects and file-based apps.
/// The solution file (.slnx) is written immediately upon creation.
/// </summary>
public sealed class Solution : IFileSystemEntry, IAsyncDisposable
{
    private readonly string _sdkName;
    private readonly string _sdkVersion;
    private readonly SdkImportStyle _defaultSdkImportStyle;
    private readonly List<(string RelativePath, bool IsFolder)> _entries = [];

    /// <summary>
    /// Gets the absolute path to the solution file (.slnx).
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the directory containing the solution.
    /// </summary>
    public string Directory { get; }

    internal Solution(
        string solutionPath,
        string sdkName,
        string sdkVersion,
        SdkImportStyle defaultSdkImportStyle)
    {
        Path = solutionPath;
        Directory = System.IO.Path.GetDirectoryName(solutionPath)!;
        _sdkName = sdkName;
        _sdkVersion = sdkVersion;
        _defaultSdkImportStyle = defaultSdkImportStyle;

        // Create directory and write initial empty solution
        System.IO.Directory.CreateDirectory(Directory);
        WriteSolutionFile();
    }

    /// <summary>
    /// Adds a .csproj-based project to the solution.
    /// </summary>
    /// <param name="properties">Optional MSBuild properties to set in the project.</param>
    /// <param name="nuGetPackages">Optional NuGet package references.</param>
    /// <param name="projectName">Project name (without extension). Defaults to "TestProject".</param>
    /// <param name="importStyle">SDK import style. Defaults to the solution's default.</param>
    /// <returns>The created project.</returns>
    public Project AddProject(
        (string Name, string Value)[]? properties = null,
        NuGetReference[]? nuGetPackages = null,
        string projectName = "TestProject",
        SdkImportStyle importStyle = SdkImportStyle.Default)
    {
        var projectDir = System.IO.Path.Combine(Directory, projectName);
        System.IO.Directory.CreateDirectory(projectDir);

        var projectFileName = $"{projectName}.csproj";
        var projectPath = System.IO.Path.Combine(projectDir, projectFileName);

        // Build property group
        var propertiesElement = new XElement("PropertyGroup");
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                propertiesElement.Add(new XElement(prop.Name, prop.Value));
            }
        }

        // Build package references
        var packagesElement = new XElement("ItemGroup");
        if (nuGetPackages != null)
        {
            foreach (var package in nuGetPackages)
            {
                packagesElement.Add(new XElement("PackageReference",
                    new XAttribute("Include", package.Name),
                    new XAttribute("Version", package.Version)));
            }
        }

        // Resolve import style - versions come from global.json (required for SDK-to-SDK chains)
        importStyle = importStyle == SdkImportStyle.Default ? _defaultSdkImportStyle : importStyle;
        var rootSdkName = importStyle == SdkImportStyle.ProjectElement
            ? _sdkName
            : "Microsoft.NET.Sdk";

        // Build inner SDK element for SdkElement style
        var innerSdkContent = importStyle == SdkImportStyle.SdkElement
            ? $"""<Sdk Name="{_sdkName}" />"""
            : "";

        var content = $"""
            <Project Sdk="{rootSdkName}">
                {innerSdkContent}
                <PropertyGroup>
                    <OutputType>exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                {propertiesElement}
                {packagesElement}
            </Project>
            """;

        File.WriteAllText(projectPath, content);

        // Add to solution
        var relativePath = System.IO.Path.Combine(projectName, projectFileName);
        _entries.Add((relativePath, false));
        WriteSolutionFile();

        return new Project(projectPath, ProjectType.ProjectBased);
    }

    /// <summary>
    /// Adds a file-based app to the solution.
    /// The file is placed in a "Solution Items" solution folder.
    /// </summary>
    /// <param name="filename">The .cs filename.</param>
    /// <param name="content">The C# source code.</param>
    /// <returns>The created project representing the file-based app.</returns>
    public Project AddFileBasedApp(string filename, [StringSyntax("csharp")] string content)
    {
        // Generate #:sdk directives from the configured SDK name(s)
        var sdkNames = _sdkName.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var directives = string.Join(Environment.NewLine, sdkNames.Select(sdk => $"#:sdk {sdk}@{_sdkVersion}"));

        // Ensure content ends with newline
        if (content.Length > 0 && content[^1] is not '\n')
        {
            content += '\n';
        }

        var fullContent = $"{directives}{Environment.NewLine}{content}";
        var filePath = System.IO.Path.Combine(Directory, filename);
        File.WriteAllText(filePath, fullContent);

        // Add to solution under Solution Items folder
        _entries.Add((filename, true));
        WriteSolutionFile();

        return new Project(filePath, ProjectType.FileBased);
    }

    /// <summary>
    /// Adds a file-based app named "Program.cs" to the solution.
    /// Convenience method equivalent to AddFileBasedApp("Program.cs", content).
    /// </summary>
    /// <param name="content">The C# source code.</param>
    /// <returns>The created project representing the file-based app.</returns>
    public Project AddFileBasedProgram([StringSyntax("csharp")] string content) =>
        AddFileBasedApp("Program.cs", content);

    /// <summary>
    /// Adds a file to the solution directory.
    /// Useful for files like README.md, LICENSE, etc.
    /// </summary>
    /// <param name="filename">The filename (relative to solution directory).</param>
    /// <param name="content">The file content.</param>
    /// <returns>The absolute path to the created file.</returns>
    public string AddFile(string filename, string content)
    {
        var filePath = System.IO.Path.Combine(Directory, filename);
        var dir = System.IO.Path.GetDirectoryName(filePath);
        if (dir != null && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, content);
        return filePath;
    }

    private void WriteSolutionFile()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Solution>");

        // Group entries
        var projects = _entries.Where(e => !e.IsFolder).ToList();
        var solutionItems = _entries.Where(e => e.IsFolder).ToList();

        // Write projects
        foreach (var (relativePath, _) in projects)
        {
            sb.AppendLine($"""  <Project Path="{relativePath}" />""");
        }

        // Write solution items folder if any
        if (solutionItems.Count > 0)
        {
            sb.AppendLine("""  <Folder Name="/Solution Items/">""");
            foreach (var (relativePath, _) in solutionItems)
            {
                sb.AppendLine($"""    <File Path="{relativePath}" />""");
            }
            sb.AppendLine("  </Folder>");
        }

        sb.AppendLine("</Solution>");
        File.WriteAllText(Path, sb.ToString());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Don't delete - keep for IDE exploration
        return ValueTask.CompletedTask;
    }
}
