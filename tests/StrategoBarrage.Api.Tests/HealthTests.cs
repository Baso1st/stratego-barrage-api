namespace StrategoBarrage.Api.Tests;

public class HealthTests(TestApp app)
{
    [Fact]
    public async Task HealthEndpointAnswersHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateApiClient();

        using var response = await client.GetAsync("/health", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe("Healthy");
    }
}
