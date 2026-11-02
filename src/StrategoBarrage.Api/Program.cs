using Microsoft.Azure.Cosmos;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StrategoBarrage.Api;
using StrategoBarrage.Games;

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

// A request missing a field the API needs is a 400, rather than a default value. Development answers it the
// same way, instead of throwing.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.RespectNullableAnnotations = true;
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.AddAccessTokens();

// Games: the connection string is the account's endpoint in Azure, signed in with the app's identity, and the
// emulator's address and key locally.
builder.AddAzureCosmosClient("cosmos", configureClientOptions: options =>
    options.UseSystemTextJsonSerializerWithOptions = GameJson.Options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGameRules, StandInRules>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Games").Get<GameOptions>() ?? new GameOptions());
builder.Services.AddSingleton<GameDecider>();
builder.Services.AddSingleton(services =>
    new GameStore(services.GetRequiredService<CosmosClient>().GetContainer("stratego", "games")));
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<Views>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapDevelopmentTokens();
}

// App Service's health check calls this every minute; it's left out of traces and metrics.
app.MapHealthChecks(HealthPath).DisableHttpMetrics().AllowAnonymous();

app.MapGameEndpoints();

app.Run();
