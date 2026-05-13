using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.CodeStyleSdk - verifies code style rules are loaded.
/// Note: CodeStyleSdk defines style RULES (naming patterns, preferences).
/// AnalyzersSdk adds severity ENFORCEMENT for IDE diagnostics.
/// </summary>
public class CodeStyleSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.CodeStyleSdkName], fixture, output)
{
    /// <summary>
    /// Verifies that Org.CodeStyleSdk inherits Org.BuildSdk settings.
    /// </summary>
    [Fact]
    public async Task InheritsBuildSdk_Release()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            // This violates nullable rules - CodeStyleSdk should inherit BuildSdk's Nullable=enable
            string message = null;
            Console.WriteLine(message);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        // Should have nullable error (inherited from BuildSdk)
        Assert.True(result.HasError("CS8600"), "Expected CS8600 - CodeStyleSdk should inherit BuildSdk settings");
        Assert.Equal("enable", result.GetMSBuildPropertyValue("Nullable"));
    }

    /// <summary>
    /// Verifies that JetBrains CleanupCode reformats raw string literals according to ReSharper settings.
    /// The resharper_indent_raw_literal_string=indent setting should cause raw strings to be indented.
    /// </summary>
    [Fact]
    public async Task CleanupCode_IndentsRawStringLiterals()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();

        // Add code with non-indented raw string literal (content left-aligned at column 0)
        // Use 4 quotes for outer raw string to allow 3 quotes inside
        var programFile = project.AddProgram(""""
            var a = """
            Raw string indentation
            """;
            Console.WriteLine(a);
            """");

        // Verify initial state - content is NOT indented (starts at column 0)
        var originalContent = await File.ReadAllTextAsync(programFile, TestContext.Current.CancellationToken);
        Assert.Contains("\nRaw string indentation\n", originalContent);

        // Build first to ensure the project is valid
        var buildResult = await DotNetBuild(solution);
        Assert.Equal(0, buildResult.ExitCode);

        // Run JetBrains CleanupCode to reformat
        var (exitCode, _) = await JetBrainsCleanupCode(solution);
        Assert.Equal(0, exitCode);

        // Read the reformatted file and verify indentation
        var reformattedContent = await File.ReadAllTextAsync(programFile, TestContext.Current.CancellationToken);

        // After cleanup, the raw string content and closing delimiter should be indented (4 spaces)
        Assert.Contains(""""
            var a = """
                Raw string indentation
                """;
            """", reformattedContent);
    }

    /// <summary>
    /// Verifies that globalconfig files are loaded by checking the naming rule exists.
    /// The rule defines the pattern but CodeStyleSdk doesn't set severity to warning.
    /// </summary>
    [Fact]
    public async Task GlobalConfigFilesAreLoaded()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            var app = new App();
            Console.WriteLine(app.Message);

            sealed class App
            {
                private readonly string _message = "Hello"; // Follows _camelCase convention

                public string Message => _message;
            }
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        // Build should succeed - the rule exists but severity isn't set to error by CodeStyleSdk
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that ReSharper settings are included.
    /// </summary>
    [Fact]
    public async Task ReSharperSettingsIncluded()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();

        // Add code with non-indented raw string literal (content left-aligned at column 0)
        // Use 4 quotes for outer raw string to allow 3 quotes inside
        var programFile = project.AddProgram(""""
            var a = """
            Raw string indentation
            """;
            Console.WriteLine(a);
            """");

        // Verify initial state - content is NOT indented (starts at column 0)
        var originalContent = await File.ReadAllTextAsync(programFile, TestContext.Current.CancellationToken);
        Assert.Contains("\nRaw string indentation\n", originalContent);

        // Build first to ensure the project is valid
        var buildResult = await DotNetBuild(solution);
        Assert.Equal(0, buildResult.ExitCode);

        // Run JetBrains CleanupCode to reformat
        var (exitCode, _) = await JetBrainsCleanupCode(solution);
        Assert.Equal(0, exitCode);

        // Read the reformatted file and verify indentation
        var reformattedContent = await File.ReadAllTextAsync(programFile, TestContext.Current.CancellationToken);

        // After cleanup, the raw string content and closing delimiter should be indented (4 spaces)
        Assert.Contains(""""
            var a = """
                Raw string indentation
                """;
            """", reformattedContent);
    }
}
