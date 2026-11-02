using Aspire.Hosting.Azure;
using Microsoft.Azure.Cosmos;
using StrategoBarrage.Games;

[assembly: AssemblyFixture(typeof(StrategoBarrage.Api.Tests.TestApp))]

namespace StrategoBarrage.Api.Tests;

// The app host's API and Cosmos DB emulator, with its database and containers, started once for all the tests.
public sealed class TestApp : IAsyncLifetime
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;
    private CosmosClient? _client;

    public Container Games => _client!.GetContainer("stratego", "games");

    public HttpClient CreateApiClient() => _app!.CreateHttpClient("api");

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.StrategoBarrage_AppHost>(cancellationToken);

        // Service Bus and the Function Apps aren't started. Aspire's Azure environment stays, since the Azure
        // resources depend on it. Aspire marks its type as experimental.
#pragma warning disable ASPIREAZURE001
        var others = builder.Resources
            .Where(resource => resource is not (AzureEnvironmentResource
                or AzureCosmosDBResource
                or AzureCosmosDBDatabaseResource
                or AzureCosmosDBContainerResource)
                && resource.Name != "api")
            .ToList();
#pragma warning restore ASPIREAZURE001
        foreach (var resource in others)
        {
            builder.Resources.Remove(resource);
        }

        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken).WaitAsync(StartTimeout, cancellationToken);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(StartTimeout, cancellationToken);

        var connectionString = await _app.GetConnectionStringAsync("cosmos", cancellationToken);
        _client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = GameJson.Options,
            ConnectionMode = ConnectionMode.Gateway,
        });
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
