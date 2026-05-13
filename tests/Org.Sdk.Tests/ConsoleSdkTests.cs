using Org.Sdk.Testing;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.ConsoleSdk - verifies timing, CancellationToken, and Console alias.
/// Uses BuildSdk + ConsoleSdk combination to get ImplicitUsings from BuildSdk.
/// </summary>
public class ConsoleSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.BuildSdkName, SdkPackageFixture.ConsoleSdkName], fixture, output)
{
    /// <summary>
    /// Verifies Console alias to Spectre.Console, CancellationToken availability, and timing output.
    /// </summary>
    [Fact]
    public async Task SpectreConsoleAndCancellationToken()
    {
        await using var solution = CreateSolution();
        var app = solution.AddFileBasedProgram("""
            Console.MarkupLine("[green]Hello[/] from [blue]Spectre.Console[/]!");
            Console.WriteLine($"CancellationToken available: {!CancellationToken.IsCancellationRequested}");
            """);

        var result = await DotNetRun(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello", result.Output);
        Assert.Contains("Spectre.Console", result.Output);
        Assert.Contains("CancellationToken available: True", result.Output);
        Assert.Contains("Started at", result.Output);
        Assert.Contains("Completed in", result.Output);
    }
}
