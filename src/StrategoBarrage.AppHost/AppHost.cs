// Runs the API, the worker and the bot locally, with emulators for Cosmos DB and Service Bus in Docker.
// The databases, containers and queues match infra/main.bicep.

var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(emulator => emulator.WithDataExplorer());
var database = cosmos.AddCosmosDatabase("stratego");
database.AddContainer("games", "/gameId");
database.AddContainer("players", "/playerId");
database.AddContainer("logins", "/id");
database.AddContainer("leases", "/id");

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();
serviceBus.AddServiceBusQueue("bot-turns");
serviceBus.AddServiceBusQueue("game-timers");

builder.AddProject<Projects.StrategoBarrage_Api>("api")
    .WithHttpHealthCheck("/health");
builder.AddAzureFunctionsProject<Projects.StrategoBarrage_Worker>("worker");
builder.AddAzureFunctionsProject<Projects.StrategoBarrage_Bot>("bot");

builder.Build().Run();
