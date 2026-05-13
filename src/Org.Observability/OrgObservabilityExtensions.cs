using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Org.Observability;

/// <summary>
/// Extension methods for configuring OpenTelemetry observability.
/// </summary>
public static class OrgObservabilityExtensions
{
    private static bool _configured;

    /// <summary>
    /// Configures OpenTelemetry observability for the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="enableConsoleExporter">Whether to enable the console exporter (useful for local development).</param>
    public static void ConfigureOrgObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableConsoleExporter = false)
    {
        if (_configured) return;
        _configured = true;

        var serviceName = configuration["OTEL_SERVICE_NAME"]
            ?? Assembly.GetEntryAssembly()?.GetName().Name
            ?? "unknown";

        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion);

        // Configure OpenTelemetry
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resourceBuilder);
                metrics.AddRuntimeInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter("Org.*");

                if (enableConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resourceBuilder);
                tracing.AddSource(serviceName);
                tracing.AddSource("Org.*");
                tracing.AddHttpClientInstrumentation();

                if (enableConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }
            });

        // OTLP exporter (enabled when endpoint is configured)
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.UseOtlpExporter();
        }
    }

    /// <summary>
    /// Configures OpenTelemetry logging.
    /// </summary>
    /// <param name="logging">The logging builder.</param>
    /// <param name="enableConsoleExporter">Whether to enable the console exporter.</param>
    public static void ConfigureOrgObservabilityLogging(
        this ILoggingBuilder logging,
        bool enableConsoleExporter = false)
    {
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            if (enableConsoleExporter)
            {
                options.AddConsoleExporter();
            }
        });
    }
}
