using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

const string HealthPath = "/health";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
        options.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath)))
    .WithMetrics(metrics => metrics.AddMeter("Microsoft.AspNetCore.Hosting"));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Stratego Barrage API";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// App Service's health check calls this every minute; it's left out of traces and metrics.
app.MapHealthChecks(HealthPath).DisableHttpMetrics();

app.Run();
