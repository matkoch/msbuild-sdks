using NuGet.Packaging;

namespace Org.Sdk.Testing.Extensions;

/// <summary>
/// Extension methods for inspecting NuGet packages in <see cref="ExecutionResult"/>.
/// </summary>
public static class PackageExtensions
{
    /// <summary>
    /// Gets all files in the NuGet package.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>List of file paths within the package, or empty if no package exists.</returns>
    public static IReadOnlyList<string> GetPackageFiles(this ExecutionResult result) =>
        result.Package?.GetFiles().ToList() ?? [];

    /// <summary>
    /// Returns true if the NuGet package contains a file at the specified path.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="path">The path within the package (e.g., "lib/net10.0/MyLib.dll").</param>
    /// <returns>True if the file exists in the package.</returns>
    public static bool PackageHasFile(this ExecutionResult result, string path) =>
        result.Package?.GetFiles().Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>
    /// Gets files in a specific folder within the NuGet package.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="folder">The folder path (e.g., "build", "analyzers/dotnet/cs").</param>
    /// <returns>List of file paths in the folder, or empty if no package exists.</returns>
    public static IReadOnlyList<string> GetPackageFilesInFolder(this ExecutionResult result, string folder)
    {
        if (result.Package == null) return [];

        var prefix = folder.TrimEnd('/') + "/";
        return result.Package.GetFiles()
            .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets the package metadata (ID, version, authors, etc.).
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>The package metadata, or null if no package exists.</returns>
    public static NuspecReader? GetPackageMetadata(this ExecutionResult result) =>
        result.Package?.NuspecReader;

    /// <summary>
    /// Reads the content of a file within the NuGet package as a string.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="path">The path within the package (e.g., "README.md").</param>
    /// <returns>The file content, or null if the file doesn't exist or no package exists.</returns>
    public static string? GetPackageFileContent(this ExecutionResult result, string path)
    {
        if (result.Package == null) return null;

        var entry = result.Package.GetEntry(path);
        if (entry == null) return null;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
