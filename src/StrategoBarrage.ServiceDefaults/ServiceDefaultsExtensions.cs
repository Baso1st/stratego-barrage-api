using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// What the API, the worker and the bot all start with: telemetry, health checks, and resilient HTTP calls.
// Each app adds the instrumentation for how it's hosted (ASP.NET Core, or the Functions worker).
public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddTelemetry();
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy());

        // Every HTTP call retries a few times with backoff, with a timeout on each attempt and a circuit breaker.
        // The calls between the apps are all safe to repeat: each is keyed by game and version.
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    private static void AddTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddHttpClientInstrumentation());

        // Locally, the Aspire app host sets the OTLP endpoint of its dashboard.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // In Azure, Application Insights only accepts Entra ID tokens, so each app signs in with its
        // managed identity. Traces are sampled at up to 5 a second (the exporter's default), and logs
        // are kept or dropped with their trace. Metrics aren't sampled. Performance counters are off:
        // CPU and memory come from the App Service Plan's platform metrics, which cost nothing.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            var identity = ManagedIdentityId.FromUserAssignedClientId(builder.Configuration["AZURE_CLIENT_ID"]);
            builder.Services.AddOpenTelemetry().UseAzureMonitorExporter(options =>
            {
                options.Credential = new ManagedIdentityCredential(identity);
                options.EnablePerformanceCounters = false;
            });
        }
    }
}
