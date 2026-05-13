using Microsoft.Build.Logging.StructuredLogger;
using NuGet.Packaging;

namespace Org.Sdk.Testing;

/// <summary>
/// Represents the result of executing a CLI command.
/// Provides access to output, exit code, and build artifacts.
/// </summary>
public sealed record ExecutionResult(
    int ExitCode,
    string Output,
    string? BinlogPath = null,
    string? SarifPath = null,
    string? PackagePath = null) : IDisposable
{
    /// <summary>
    /// Gets the parsed MSBuild binary log, if available.
    /// </summary>
    public Build? Binlog { get; init; } = BinlogPath != null && File.Exists(BinlogPath)
        ? Serialization.Read(BinlogPath)
        : null;

    /// <summary>
    /// Gets the parsed SARIF diagnostics file, if available.
    /// </summary>
    public SarifFile? Sarif { get; init; } = SarifPath != null
        ? SarifFile.Load(SarifPath)
        : null;

    /// <summary>
    /// Gets the NuGet package archive reader, if available.
    /// </summary>
    public PackageArchiveReader? Package { get; init; } = PackagePath != null && File.Exists(PackagePath)
        ? new PackageArchiveReader(PackagePath)
        : null;

    /// <inheritdoc />
    public void Dispose() => Package?.Dispose();
}
