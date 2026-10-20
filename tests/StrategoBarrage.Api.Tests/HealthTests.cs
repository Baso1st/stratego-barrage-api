namespace StrategoBarrage.Api.Tests;

public class HealthTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task HealthEndpointAnswersHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.StrategoBarrage_AppHost>(cancellationToken);

        // Only the API is needed, so the emulators and the Function Apps aren't started.
        foreach (var resource in builder.Resources.Where(resource => resource.Name != "api").ToList())
        {
            builder.Resources.Remove(resource);
        }

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(StartTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(StartTimeout, cancellationToken);

        using var client = app.CreateHttpClient("api");
        using var response = await client.GetAsync("/health", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe("Healthy");
    }
}
