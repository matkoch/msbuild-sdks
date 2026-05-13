using System.Runtime.CompilerServices;
using System.Text;
using CliWrap;
using Xunit;

namespace Org.Sdk.Testing;

/// <summary>
/// Base class for SDK integration tests.
/// Provides solution creation and CLI command execution.
/// </summary>
public abstract class SdkTestBase
{
    private const string SarifFileName = "output.sarif";
    private const string BinlogFileName = "msbuild.binlog";

    private readonly string[] _sdkNames;
    private readonly PackageFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly SdkImportStyle _defaultSdkImportStyle;

    private NetSdkVersion _sdkVersion = NetSdkVersion.Default;

    private static readonly Lock IsolationFilesLock = new();
    private static bool _isolationFilesCreated;

    /// <summary>
    /// Gets the package fixture providing SDK packages.
    /// </summary>
    protected PackageFixture Fixture => _fixture;

    /// <summary>
    /// Gets the combined SDK name (semicolon-separated for multiple SDKs).
    /// </summary>
    private string SdkName => string.Join(";", _sdkNames);

    /// <summary>
    /// Creates a new SDK test base.
    /// </summary>
    /// <param name="sdkNames">SDK names to use for projects.</param>
    /// <param name="fixture">The package fixture providing SDK packages.</param>
    /// <param name="output">Test output helper for logging.</param>
    /// <param name="defaultSdkImportStyle">Default SDK import style for projects.</param>
    protected SdkTestBase(
        string[] sdkNames,
        PackageFixture fixture,
        ITestOutputHelper output,
        SdkImportStyle defaultSdkImportStyle = SdkImportStyle.ProjectElement)
    {
        _sdkNames = sdkNames;
        _fixture = fixture;
        _output = output;
        _defaultSdkImportStyle = defaultSdkImportStyle;
    }

    /// <summary>
    /// Sets the .NET SDK version to use for building.
    /// </summary>
    protected void SetDotNetSdkVersion(NetSdkVersion version) => _sdkVersion = version;

    /// <summary>
    /// Creates a new solution for the test.
    /// </summary>
    /// <param name="testFilePath">Automatically populated with the test file path.</param>
    /// <param name="testName">Automatically populated with the test method name.</param>
    /// <returns>A new solution instance.</returns>
    protected Solution CreateSolution(
        [CallerFilePath] string testFilePath = "",
        [CallerMemberName] string testName = "")
    {
        var testClassName = Path.GetFileNameWithoutExtension(testFilePath);
        var testProjectDir = FindTestProjectDirectory(testFilePath);

        var outputBaseDir = Path.Combine(testProjectDir, "bin", "_output");
        var outputDir = Path.Combine(outputBaseDir, testClassName, testName);

        // Clean and create directory
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        // Ensure isolation files exist in the output base directory
        EnsureIsolationFiles(outputBaseDir);

        var solutionPath = Path.Combine(outputDir, $"{testName}.slnx");
        _output.WriteLine($"Solution: file://{solutionPath}");
        return new Solution(solutionPath, SdkName, _fixture.Version, _defaultSdkImportStyle);
    }

    #region CLI Execution

    /// <summary>
    /// Executes a command and returns the result.
    /// </summary>
    /// <param name="executable">The executable to run.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="environmentVariables">Environment variables to set.</param>
    /// <returns>The execution result with exit code and output.</returns>
    protected async Task<(int ExitCode, string Output)> Execute(
        string executable,
        string[] arguments,
        string workingDirectory,
        (string Name, string Value)[]? environmentVariables = null)
    {
        _output.WriteLine($"-------- {executable} {string.Join(" ", arguments)}");

        var output = new StringBuilder();

        // Merge stdout and stderr into a single stream to preserve interleaved order
        var cmd = Cli.Wrap(executable)
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output));

        if (environmentVariables != null)
        {
            cmd = cmd.WithEnvironmentVariables(env =>
            {
                foreach (var (name, value) in environmentVariables)
                {
                    env.Set(name, value);
                }
            });
        }

        var result = await cmd.ExecuteAsync();

        var outputStr = output.ToString();
        _output.WriteLine(outputStr);

        return (result.ExitCode, outputStr);
    }

    /// <summary>
    /// Executes 'dotnet build' on the specified target.
    /// </summary>
    protected Task<ExecutionResult> DotNetBuild(
        IFileSystemEntry target,
        string[]? arguments = null,
        (string Name, string Value)[]? environmentVariables = null)
    {
        return DotNet(["build", target, .. arguments ?? []], environmentVariables);
    }

    /// <summary>
    /// Executes 'dotnet pack' on the specified target.
    /// </summary>
    protected async Task<ExecutionResult> DotNetPack(
        IFileSystemEntry target,
        string[]? arguments = null,
        (string Name, string Value)[]? environmentVariables = null)
    {
        var result = await DotNet(["pack", target, .. arguments ?? []], environmentVariables);

        // Find nupkg file and create new result with package
        var nupkgFiles = Directory.GetFiles(target.Directory, "*.nupkg", SearchOption.AllDirectories);
        var packagePath = nupkgFiles.FirstOrDefault();

        return new ExecutionResult(result.ExitCode, result.Output, result.BinlogPath, result.SarifPath, packagePath);
    }

    /// <summary>
    /// Executes 'dotnet test' on the specified target.
    /// </summary>
    protected Task<ExecutionResult> DotNetTest(
        IFileSystemEntry target,
        string[]? arguments = null,
        (string Name, string Value)[]? environmentVariables = null)
    {
        return DotNet(["test", target, .. arguments ?? []], environmentVariables);
    }

    /// <summary>
    /// Executes 'dotnet run' on the specified target.
    /// </summary>
    protected Task<ExecutionResult> DotNetRun(
        IFileSystemEntry target,
        string[]? arguments = null,
        (string Name, string Value)[]? environmentVariables = null)
    {
        return DotNet(["run", target, .. arguments ?? []], environmentVariables);
    }

    /// <summary>
    /// Executes JetBrains CleanupCode (code reformatting) on the specified target.
    /// Uses 'dnx' to run the tool without permanent installation.
    /// </summary>
    /// <param name="target">The solution or project to clean up.</param>
    /// <param name="profile">Optional cleanup profile name (default: Full Cleanup).</param>
    /// <param name="include">Optional semicolon-separated relative paths to include.</param>
    protected async Task<(int ExitCode, string Output)> JetBrainsCleanupCode(
        IFileSystemEntry target,
        string? profile = null,
        string? include = null)
    {
        var args = new List<string>
        {
            "jetbrains.resharper.globaltools",
            "-y",
            "--",
            "cleanupcode",
            "--no-build",
            target.Path
        };

        if (profile != null)
        {
            args.Insert(args.Count - 1, "--profile");
            args.Insert(args.Count - 1, profile);
        }

        if (include != null)
        {
            args.Insert(args.Count - 1, "--include");
            args.Insert(args.Count - 1, include);
        }

        return await Execute("dnx", [.. args], target.Directory);
    }

    /// <summary>
    /// Executes a dotnet command with the specified arguments.
    /// Arguments can be strings or <see cref="IFileSystemEntry"/> objects.
    /// The working directory is derived from the first <see cref="IFileSystemEntry"/> in arguments.
    /// Automatically appends /bl for binary log generation.
    /// </summary>
    /// <param name="arguments">Arguments (strings or IFileSystemEntry). /bl is added automatically.</param>
    /// <param name="environmentVariables">Additional environment variables.</param>
    protected async Task<ExecutionResult> DotNet(
        object[] arguments,
        (string Name, string Value)[]? environmentVariables = null)
    {
        // Find working directory from first IFileSystemEntry
        var entry = arguments.OfType<IFileSystemEntry>().FirstOrDefault()
            ?? throw new ArgumentException("Arguments must contain at least one IFileSystemEntry", nameof(arguments));
        var workingDirectory = entry.Directory;

        // Convert arguments to strings
        var args = arguments.Select(arg => arg switch
        {
            Project { ProjectType: ProjectType.FileBased } fileBased => Path.GetFileName(fileBased.Path),
            IFileSystemEntry fsEntry => fsEntry.Path,
            _ => arg.ToString()!
        }).ToList();

        // Append /bl for binary log generation
        args.Add("/bl");

        // Log files being built
        await LogFilesInDirectory(workingDirectory);

        // Get dotnet executable (either system or downloaded SDK)
        var dotnetPath = _sdkVersion == NetSdkVersion.Default
            ? "dotnet"
            : await DotNetSdkHelpers.Get(_sdkVersion);

        // Build environment variables: isolate from CI + user overrides
        var envVars = BuildDotNetEnvironmentVariables(environmentVariables);

        // Execute with retry for transient SDK resolution failures
        const int maxRetries = 3;

        for (var retry = 0; retry <= maxRetries; retry++)
        {
            var (exitCode, output) = await Execute(dotnetPath, [.. args], workingDirectory, envVars);

            if (exitCode == 0 ||
                (!output.Contains("error MSB4236") &&
                 !output.Contains("error NETSDK1004") &&
                 !output.Contains("Could not resolve SDK")))
            {
                // Find artifact paths
                var binlogPath = Path.Combine(workingDirectory, BinlogFileName);
                var sarifPath = Path.Combine(workingDirectory, SarifFileName);

                return new ExecutionResult(
                    exitCode,
                    output,
                    File.Exists(binlogPath) ? binlogPath : null,
                    File.Exists(sarifPath) ? sarifPath : null);
            }

            if (retry < maxRetries)
            {
                _output.WriteLine($"SDK resolution error detected, retrying ({retry + 1}/{maxRetries})...");
                await Task.Delay(100 * (1 << retry));
            }
        }

        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    private (string Name, string Value)[] BuildDotNetEnvironmentVariables((string Name, string Value)[]? userVariables)
    {
        var envVars = new List<(string Name, string Value)>
        {
            // Clear potentially conflicting CI variables
            ("CI", null!),
            ("GITHUB_ACTIONS", null!),
            ("GITHUB_STEP_SUMMARY", null!),
            ("TF_BUILD", null!),

            // MSBuild isolation
            ("MSBUILDDISABLENODEREUSE", "1"),
            ("DOTNET_CLI_USE_MSBUILDNOINPROCNODE", "1"),
        };

        // Clear MSBuild variables that might affect behavior
        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase))
                envVars.Add((key, null!));
        }

        // Add user-specified variables
        if (userVariables != null)
            envVars.AddRange(userVariables);

        return [.. envVars];
    }

    #endregion

    #region Helper Methods

    private async Task LogFilesInDirectory(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                continue;

            _output.WriteLine($"File: {file}");
            var content = await File.ReadAllTextAsync(file);
            _output.WriteLine(content);
        }
    }

    private void EnsureIsolationFiles(string outputBaseDir)
    {
        lock (IsolationFilesLock)
        {
            if (_isolationFilesCreated)
                return;

            Directory.CreateDirectory(outputBaseDir);

            // NuGet.config - points to local package feed
            File.WriteAllText(Path.Combine(outputBaseDir, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="LocalFeed" value="{_fixture.PackageDirectory}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """);

            // global.json - SDK versions needed for SDK-to-SDK references (e.g., CodeStyleSdk -> BuildSdk)
            var msbuildSdks = _fixture.GenerateMSBuildSdksJson();
            File.WriteAllText(Path.Combine(outputBaseDir, "global.json"), $$"""
                {
                  "sdk": {
                    "allowPrerelease": false,
                    "rollForward": "latestMajor"
                  },
                  "msbuild-sdks": {{msbuildSdks}}
                }
                """);

            // Directory.Build.props - prevents inheritance, sets SARIF output
            File.WriteAllText(Path.Combine(outputBaseDir, "Directory.Build.props"), $"""
                <Project>
                  <PropertyGroup>
                    <AssemblyName>TestFixture</AssemblyName>
                    <ErrorLog>{SarifFileName},version=2.1</ErrorLog>
                  </PropertyGroup>
                </Project>
                """);

            // Directory.Build.targets - prevents inheritance
            File.WriteAllText(Path.Combine(outputBaseDir, "Directory.Build.targets"), "<Project />");

            // Directory.Packages.props - disables CPM
            File.WriteAllText(Path.Combine(outputBaseDir, "Directory.Packages.props"), """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                </Project>
                """);

            _isolationFilesCreated = true;
        }
    }

    private static string FindTestProjectDirectory(string testFilePath)
    {
        var dir = Path.GetDirectoryName(testFilePath)!;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find test project directory");
    }

    #endregion
}
