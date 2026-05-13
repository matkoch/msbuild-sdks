using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.ObservabilitySdk - verifies OpenTelemetry auto-configuration via interceptors.
/// Uses BuildSdk + ObservabilitySdk combination to get ImplicitUsings from BuildSdk.
/// </summary>
public class ObservabilitySdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.BuildSdkName, SdkPackageFixture.ObservabilitySdkName], fixture, output)
{
    /// <summary>
    /// Verifies that logging actually works through OpenTelemetry console exporter.
    /// This test uses ILogger and checks that the log message appears in console output.
    /// </summary>
    [Fact]
    public async Task LoggingWorksWithConsoleExporter()
    {
        await using var solution = CreateSolution();
        // Enable console exporter explicitly for this test
        var app = solution.AddProject(
            properties: [("OrgObservabilityConsoleExporter", "true")]);

        app.AddProgram("""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using Microsoft.Extensions.Logging;

            var builder = Host.CreateApplicationBuilder(args);
            using var host = builder.Build();

            // Get logger and write a test message
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("TEST_LOG_MESSAGE_12345");

            // Give OpenTelemetry time to flush
            await Task.Delay(100);
            """);

        var result = await DotNetRun(app);

        Assert.Equal(0, result.ExitCode);
        // OpenTelemetry console exporter outputs LogRecord with telemetry metadata
        Assert.Contains("TEST_LOG_MESSAGE_12345", result.Output);
        // Verify this is actually OTel output (not just standard console logging)
        Assert.Contains("LogRecord.Timestamp:", result.Output);
        Assert.Contains("telemetry.sdk.name: opentelemetry", result.Output);
    }

    /// <summary>
    /// Verifies that metrics are collected and exported through OpenTelemetry console exporter.
    /// Uses ForceFlush to ensure metrics are exported before the app exits.
    /// </summary>
    [Fact]
    public async Task MetricsWorkWithConsoleExporter()
    {
        await using var solution = CreateSolution();
        // Enable console exporter explicitly for this test
        var app = solution.AddProject(
            properties: [("OrgObservabilityConsoleExporter", "true")]);

        app.AddProgram("""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using OpenTelemetry.Metrics;

            var builder = Host.CreateApplicationBuilder(args);
            using var host = builder.Build();

            // Force flush to ensure metrics are exported before exit
            var meterProvider = host.Services.GetRequiredService<MeterProvider>();
            meterProvider.ForceFlush();

            Console.WriteLine("Metrics test completed");
            """);

        var result = await DotNetRun(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Metrics test completed", result.Output);
        // Verify OpenTelemetry runtime metrics are exported (from AddRuntimeInstrumentation)
        Assert.Contains("Metric Name: dotnet.", result.Output);
        Assert.Contains("System.Runtime", result.Output);
        Assert.Contains("telemetry.sdk.name: opentelemetry", result.Output);
    }

    /// <summary>
    /// Verifies that observability can be disabled via OrgObservabilityEnabled=false.
    /// When disabled, OpenTelemetry packages are not included.
    /// </summary>
    [Fact]
    public async Task ObservabilityCanBeDisabled()
    {
        await using var solution = CreateSolution();
        var app = solution.AddProject(
            properties: [("OrgObservabilityEnabled", "false")]);

        // When observability is disabled, hosting packages are not included
        // Test that a simple app without Host usage still works
        app.AddProgram("""
            Console.WriteLine("App runs without observability packages!");
            """);

        var result = await DotNetRun(app);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("App runs without observability packages!", result.Output);

        // Verify the property is set correctly
        Assert.False(result.GetMSBuildPropertyValue<bool>("OrgObservabilityEnabled"));
    }

    /// <summary>
    /// Verifies that console exporter is enabled in Debug mode by default, disabled in Release.
    /// </summary>
    [Fact]
    public async Task ConsoleExporterEnabledInDebugByDefault()
    {
        await using var solution = CreateSolution();
        // Suppress CA1303 for this test (literal strings) to avoid Release mode failures
        var app = solution.AddProject(
            properties: [("NoWarn", "CA1303")]);

        app.AddProgram("""
            using Microsoft.Extensions.Hosting;

            var builder = Host.CreateApplicationBuilder(args);
            using var host = builder.Build();

            #if ORG_OBSERVABILITY_CONSOLE_EXPORTER
            Console.WriteLine("ConsoleExporter: Enabled");
            #else
            Console.WriteLine("ConsoleExporter: Disabled");
            #endif
            """);

        // Debug mode should have console exporter enabled
        var debugResult = await DotNetRun(app, ["-c", "Debug"]);
        Assert.Equal(0, debugResult.ExitCode);
        Assert.Contains("ConsoleExporter: Enabled", debugResult.Output);

        // Release mode should have console exporter disabled
        var releaseResult = await DotNetRun(app, ["-c", "Release"]);
        Assert.Equal(0, releaseResult.ExitCode);
        Assert.Contains("ConsoleExporter: Disabled", releaseResult.Output);
    }
}
