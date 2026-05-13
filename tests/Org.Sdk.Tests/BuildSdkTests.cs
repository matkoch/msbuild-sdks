using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.BuildSdk - verifies core build settings.
/// </summary>
public class BuildSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.BuildSdkName], fixture, output)
{
    /// <summary>
    /// Verifies that nullable warnings are not errors in Debug.
    /// </summary>
    [Fact]
    public async Task NullableViolation_Debug()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            string message = null;
            Console.WriteLine(message);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Debug"]);

        // In Debug, nullable warnings are not errors
        Assert.True(result.HasWarning("CS8600"), "Expected CS8600 nullable warning");
        Assert.False(result.HasError("CS8600"), "CS8600 should be warning, not error in Debug");
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that Nullable=enable produces CS8600 errors.
    /// </summary>
    [Fact]
    public async Task NullableViolation_Release()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            // This code intentionally violates nullable rules.
            string message = null; // CS8600: Converting null literal to non-nullable type
            Console.WriteLine(message);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        Assert.True(result.HasError("CS8600"), "Expected CS8600 nullable error");
        Assert.Equal("enable", result.GetMSBuildPropertyValue("Nullable"));
        Assert.Equal(1, result.ExitCode);
    }

    /// <summary>
    /// Verifies that SDK DEFAULTS (Sdk.props) can be overridden by the project.
    /// Nullable is set in Sdk.props, so project can override it.
    /// </summary>
    [Fact]
    public async Task Defaults_CanBeOverridden()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("Nullable", "disable")]);
        project.AddProgram("""
            string message = null; // No error when Nullable=disable
            Console.WriteLine(message);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        // Project successfully overrides the SDK default
        Assert.Equal("disable", result.GetMSBuildPropertyValue("Nullable"));
        Assert.False(result.HasError("CS8600"), "Should not have nullable errors when disabled");
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that SDK ENFORCEMENTS (Sdk.targets) cannot be overridden by the project.
    /// TreatWarningsAsErrors is set in Sdk.targets, so project cannot override it.
    /// </summary>
    [Fact]
    public async Task Enforcements_CannotBeOverridden()
    {
        await using var solution = CreateSolution();
        // Project tries to disable TreatWarningsAsErrors, but SDK enforces it
        var project = solution.AddProject(properties: [("TreatWarningsAsErrors", "false")]);
        project.AddProgram("""
            string message = null; // CS8600 - would be warning if TreatWarningsAsErrors=false
            Console.WriteLine(message);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        // SDK enforces TreatWarningsAsErrors=true in Release, project cannot override
        Assert.Equal("True", result.GetMSBuildPropertyValue("TreatWarningsAsErrors"));
        Assert.True(result.HasError("CS8600"), "Warning should be error because SDK enforces TreatWarningsAsErrors");
        Assert.Equal(1, result.ExitCode);
    }

    /// <summary>
    /// Verifies that analyzers are enabled and produce errors in Release.
    /// </summary>
    [Fact]
    public async Task AnalyzerViolation_Release()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            // CA1822: Mark members as static
            var app = new App();
            app.Run();

            class App
            {
                public void Run() => Console.WriteLine("Hello");
            }
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        Assert.True(result.HasError("CA1822"), "Expected CA1822 analyzer error");
        Assert.Equal(1, result.ExitCode);
    }

    /// <summary>
    /// Verifies that analyzer warnings are not errors in Debug.
    /// </summary>
    [Fact]
    public async Task AnalyzerViolation_Debug()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            var app = new App();
            app.Run();

            class App
            {
                public void Run() => Console.WriteLine("Hello");
            }
            """);

        var result = await DotNetBuild(project, ["--configuration", "Debug"]);

        // In Debug, analyzer warnings are not errors
        Assert.True(result.HasWarning("CA1822"), "Expected CA1822 analyzer warning");
        Assert.False(result.HasError("CA1822"), "CA1822 should be warning, not error in Debug");
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that TreatWarningsAsErrors is enabled in Release.
    /// </summary>
    [Fact]
    public async Task WarningsAsErrors_Release()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            Console.WriteLine(Environment.Version);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        Assert.Equal("True", result.GetMSBuildPropertyValue("TreatWarningsAsErrors"));
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that ImplicitUsings is enabled.
    /// </summary>
    [Fact]
    public async Task ImplicitUsings()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            // Uses List<T> and Console without explicit using directives
            List<string> items = ["one", "two", "three"];
            Console.WriteLine(string.Join(", ", items));
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        Assert.Equal("enable", result.GetMSBuildPropertyValue("ImplicitUsings"));
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies default SDK properties are set correctly.
    /// </summary>
    [Fact]
    public async Task DefaultProperties()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject();
        project.AddProgram("""
            Console.WriteLine(Environment.Version);
            """);

        var result = await DotNetBuild(project, ["--configuration", "Release"]);

        Assert.Equal("enable", result.GetMSBuildPropertyValue("Nullable"));
        Assert.Equal("enable", result.GetMSBuildPropertyValue("ImplicitUsings"));
        Assert.Equal("True", result.GetMSBuildPropertyValue("TreatWarningsAsErrors"));
        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Verifies that file-based apps work with the SDK.
    /// File-based apps are single .cs files with #:sdk directive, run with "dotnet run app.cs".
    /// The SDK directives are automatically injected by AddFileBasedApp.
    /// </summary>
    [Fact]
    public async Task FileBasedApp()
    {
        await using var solution = CreateSolution();
        var app = solution.AddFileBasedProgram("""
            Console.WriteLine(Environment.Version);
            """);

        var result = await DotNetBuild(app);

        Assert.Equal(0, result.ExitCode);
    }
}
