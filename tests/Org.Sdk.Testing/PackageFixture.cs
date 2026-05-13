using System.Text;
using System.Text.Json;
using CliWrap;
using Xunit;

namespace Org.Sdk.Testing;

/// <summary>
/// Assembly-level fixture that builds SDK NuGet packages once before all tests run.
/// Inherit from this class and provide solution file name and SDK names via constructor.
/// Test projects must register the derived fixture via [assembly: AssemblyFixture(typeof(MyPackageFixture))].
/// </summary>
public abstract class PackageFixture(string solutionFileName, params string[] sdkNames) : IAsyncLifetime
{
    private string? _packageDirectory;

    /// <summary>
    /// Gets the directory containing the built NuGet packages.
    /// </summary>
    public string PackageDirectory => _packageDirectory ?? throw new InvalidOperationException("PackageFixture not initialized");

    /// <summary>
    /// Gets the package version being tested.
    /// </summary>
    public string Version { get; } = Environment.GetEnvironmentVariable("PACKAGE_VERSION") ?? "0.0.9999";

    /// <summary>
    /// Gets the SDK names managed by this fixture.
    /// </summary>
    public IReadOnlyList<string> SdkNames => sdkNames;

    /// <summary>
    /// Gets the solution file name used to find the repository root.
    /// </summary>
    public string SolutionFileName => solutionFileName;

    public async ValueTask InitializeAsync()
    {
        // Check for CI environment with pre-built packages
        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            if (Environment.GetEnvironmentVariable("NUGET_DIRECTORY") is { } path)
            {
                var files = Directory.GetFiles(path, "*.nupkg", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    _packageDirectory = path;
                    return;
                }

                throw new InvalidOperationException("No .nupkg files found in " + path);
            }

            throw new InvalidOperationException("NUGET_DIRECTORY environment variable not set in CI");
        }

        // Build NuGet packages locally
        var rootDir = FindRootDirectory(solutionFileName);
        var artifactsDir = Path.Combine(rootDir, "artifacts", "packages");

        // Clean old packages
        if (Directory.Exists(artifactsDir))
        {
            foreach (var file in Directory.GetFiles(artifactsDir, "*.nupkg"))
                File.Delete(file);
        }
        Directory.CreateDirectory(artifactsDir);

        // Clear NuGet cache for our packages
        var nugetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        if (Directory.Exists(nugetCache))
        {
            foreach (var sdkName in sdkNames)
            {
                var sdkCacheDir = Path.Combine(nugetCache, sdkName.ToLowerInvariant());
                var versionDir = Path.Combine(sdkCacheDir, Version);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, recursive: true);
                }
            }
        }

        // Pack using solution
        var solutionFile = Path.Combine(rootDir, solutionFileName);
        var output = new StringBuilder();

        var result = await Cli.Wrap("dotnet")
            .WithArguments([
                "pack", solutionFile,
                "--configuration", "Release",
                $"-p:Version={Version}",
                "--output", artifactsDir
            ])
            .WithWorkingDirectory(rootDir)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
            .WithEnvironmentVariables(env =>
            {
                env.Set("MSBUILDDISABLENODEREUSE", "1");
                env.Set("DOTNET_CLI_USE_MSBUILDNOINPROCNODE", "1");
            })
            .ExecuteAsync();

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to pack SDKs.\nOutput: {output}");
        }

        _packageDirectory = artifactsDir;
    }

    public ValueTask DisposeAsync()
    {
        // Package directory is in artifacts folder, don't delete it
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Generates the msbuild-sdks section for global.json.
    /// </summary>
    internal string GenerateMSBuildSdksJson()
    {
        var sdks = new Dictionary<string, string>();
        foreach (var sdk in sdkNames)
        {
            sdks[sdk] = Version;
        }
        return JsonSerializer.Serialize(sdks, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FindRootDirectory(string solutionFileName)
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, solutionFileName)))
        {
            var parent = Directory.GetParent(directory);
            if (parent == null)
            {
                throw new InvalidOperationException($"Could not find repository root ({solutionFileName})");
            }
            directory = parent.FullName;
        }
        return directory;
    }
}
