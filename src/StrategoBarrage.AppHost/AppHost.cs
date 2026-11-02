// Runs the API, the worker and the bot locally, with emulators for Cosmos DB and Service Bus in Docker.
// The databases, containers and queues match infra/main.bicep.

using System.Security.Cryptography;

var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(emulator => emulator.WithDataExplorer());
var database = cosmos.AddCosmosDatabase("stratego");
var games = database.AddContainer("games", "/gameId");
database.AddContainer("players", "/playerId");
database.AddContainer("logins", "/id");
var leases = database.AddContainer("leases", "/id");

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();
var botTurns = serviceBus.AddServiceBusQueue("bot-turns");
var gameTimers = serviceBus.AddServiceBusQueue("game-timers");

// The key that signs and checks development access tokens, made up on each run.
var developmentTokenKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

var api = builder.AddProject<Projects.StrategoBarrage_Api>("api")
    .WithReference(cosmos)
    .WaitFor(games)
    .WithEnvironment("DevelopmentTokens__SigningKey", developmentTokenKey)
    .WithHttpHealthCheck("/health");

// A shorter turn limit, such as "00:00:15", for watching turns run out locally and in tests.
if (builder.Configuration["Games:TurnLimit"] is { Length: > 0 } turnLimit)
{
    api.WithEnvironment("Games__TurnLimit", turnLimit);
}

builder.AddAzureFunctionsProject<Projects.StrategoBarrage_Worker>("worker")
    .WithReference(cosmos)
    .WithReference(serviceBus)
    .WithReference(api)
    .WaitFor(games)
    .WaitFor(leases)
    .WaitFor(botTurns)
    .WaitFor(gameTimers)
    .WithEnvironment("DevelopmentTokens__SigningKey", developmentTokenKey);
builder.AddAzureFunctionsProject<Projects.StrategoBarrage_Bot>("bot")
    .WithReference(serviceBus)
    .WithReference(api)
    .WaitFor(botTurns)
    .WithEnvironment("DevelopmentTokens__SigningKey", developmentTokenKey);

builder.Build().Run();
