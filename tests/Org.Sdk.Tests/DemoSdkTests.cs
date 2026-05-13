using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.DemoSdk - demonstrates MSBuild SDK evaluation order.
/// </summary>
public class DemoSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.DemoSdkName], fixture, output)
{
    /// <summary>
    /// Verifies the evaluation order: Sdk.props → Project → Sdk.targets.
    /// </summary>
    [Fact]
    public async Task ShowsEvaluationOrder()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("EvaluationOrder", "$(EvaluationOrder)Project\n")]);
        project.AddProgram("""
            System.Console.WriteLine("Hello");
            """);

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);

        // Verify the output shows the evaluation order
        Assert.True(result.OutputContains("""
            === SDK Evaluation Order ===
              Sdk.props
              Project
              Sdk.targets
            """));
    }
}
