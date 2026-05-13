using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.AnalyzersSdk - verifies analyzer diagnostic severity configuration.
/// AnalyzersSdk extends CodeStyleSdk which extends BuildSdk.
/// </summary>
public class AnalyzersSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.AnalyzersSdkName], fixture, output)
{
    /// <summary>
    /// Verifies that AnalyzersSdk inherits CodeStyleSdk's naming conventions.
    /// EnforceCodeStyleInBuild is only enabled in Release by BuildSdk.
    /// </summary>
    [Fact]
    public async Task InheritsNamingConventionsFromCodeStyleSdk()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);

        // Private field without underscore prefix violates naming convention
        project.AddCSharpFile("Test.cs", """
            namespace TestNamespace
            {
                public class Test
                {
                    private int myField; // Should be _myField

                    public int GetValue() => myField;
                }
            }
            """);

        // Build in Release mode to enable EnforceCodeStyleInBuild
        var result = await DotNetBuild(project, ["-c", "Release"]);

        // In Release, TreatWarningsAsErrors is enabled, so IDE1006 becomes an error
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("IDE1006"), "Should have IDE1006 error for naming violation");
    }

    /// <summary>
    /// Verifies that Meziantou.Analyzer is included and MA0003 severity is escalated from suggestion to warning.
    /// MA0003 default severity is 'suggestion' - AnalyzersSdk escalates it to 'warning' via globalconfig.
    /// </summary>
    [Fact]
    public async Task MeziantouAnalyzer_MA0003_EscalatedToWarning()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);

        // Calling method with literal null without naming the parameter triggers MA0003
        project.AddCSharpFile("Test.cs", """
            namespace TestNamespace
            {
                public class Test
                {
                    public void Process(string? value) { }
                    public void Run() => Process(null);
                }
            }
            """);

        // Build in Release mode (TreatWarningsAsErrors enabled)
        var result = await DotNetBuild(project, ["-c", "Release"]);

        // MA0003 should be reported as error (suggestion -> warning via globalconfig + TreatWarningsAsErrors)
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("MA0003"), "Should have MA0003 error - SDK escalates from suggestion to warning");
    }

    /// <summary>
    /// Verifies that using directives outside namespace triggers IDE0065 warning in Release mode.
    /// EnforceCodeStyleInBuild is only enabled in Release by BuildSdk.
    /// </summary>
    [Fact]
    public async Task UsingDirectiveOutsideNamespace_TriggersWarning()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);

        // Using inside namespace violates the style rule
        project.AddCSharpFile("Test.cs", """
            namespace TestNamespace
            {
                using System.Collections.Generic;

                public class Test
                {
                    public List<int> Items { get; } = new();
                }
            }
            """);

        // Build in Release mode to enable EnforceCodeStyleInBuild
        var result = await DotNetBuild(project, ["-c", "Release"]);

        // In Release, TreatWarningsAsErrors is enabled, so IDE0065 becomes an error
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("IDE0065"), "Should have IDE0065 error for using inside namespace");
    }

    /// <summary>
    /// Verifies that AnalyzersSdk inherits BuildSdk's language settings.
    /// </summary>
    [Fact]
    public async Task InheritsBuildSdkSettings()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);

        // Use nullable reference types (enabled by BuildSdk)
        project.AddCSharpFile("Test.cs", """
            namespace TestNamespace
            {
                public class Test
                {
                    public string? NullableProperty { get; set; }
                }
            }
            """);

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);
    }
}
