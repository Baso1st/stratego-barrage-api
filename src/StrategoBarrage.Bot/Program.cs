using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrategoBarrage.Bot;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();

builder.Services.AddSingleton(TimeProvider.System);

// The API's address, from the app host. Locally the API answers on both; plain HTTP saves trusting the development
// certificate.
builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = new Uri(
    builder.Configuration["services:api:http:0"] ?? builder.Configuration["services:api:https:0"]
    ?? "http://localhost"));

builder.Build().Run();
