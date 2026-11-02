using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using StrategoBarrage.Games;
using StrategoBarrage.ServiceDefaults;

namespace StrategoBarrage.Worker;

// Calls the API's internal endpoints as the worker.
public sealed class ApiClient(HttpClient http, IConfiguration configuration, TimeProvider time)
{
    public async Task TurnExpiredAsync(TurnExpired timer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/games/{timer.GameId}/turn-expired")
        {
            Content = JsonContent.Create(new { version = timer.Version }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", DevelopmentTokens.CreateForApp(configuration, "worker", "Worker", time.GetUtcNow()));
        using var response = await http.SendAsync(request, cancellationToken);

        // 204 when the turn ran out, or had already been played; 404 when the game doesn't exist. Either way the
        // timer is done.
        if (response.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotFound))
        {
            response.EnsureSuccessStatusCode();
        }
    }
}
