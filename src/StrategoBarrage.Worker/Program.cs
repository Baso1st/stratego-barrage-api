using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrategoBarrage.Games;
using StrategoBarrage.Worker;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();

builder.AddAzureCosmosClient("cosmos", configureClientOptions: options =>
    options.UseSystemTextJsonSerializerWithOptions = GameJson.Options);
builder.AddAzureServiceBusClient("servicebus");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(services => new PlayerStore(
    services.GetRequiredService<CosmosClient>().GetContainer("stratego", "players"),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<GameMessages>();

// The API's address, from the app host. Locally the API answers on both; plain HTTP saves trusting the development
// certificate.
builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = new Uri(
    builder.Configuration["services:api:http:0"] ?? builder.Configuration["services:api:https:0"]
    ?? "http://localhost"));

builder.Build().Run();
