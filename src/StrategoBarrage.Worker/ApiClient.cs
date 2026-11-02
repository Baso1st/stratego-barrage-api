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
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    public async Task TurnExpiredAsync(TurnExpired timer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/games/{timer.GameId}/turn-expired")
        {
            Content = JsonContent.Create(new { version = timer.Version }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var response = await http.SendAsync(request, cancellationToken);

        // 204 when the turn ran out, or had already been played; 404 when the game doesn't exist. Either way the
        // timer is done.
        if (response.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotFound))
        {
            response.EnsureSuccessStatusCode();
        }
    }

    // For now the worker signs in with a development token, so it only calls an API running locally.
    private string Token()
    {
        var key = configuration[DevelopmentTokens.SigningKeySetting]
            ?? throw new InvalidOperationException("The worker has no way to sign in to the API yet.");
        return DevelopmentTokens.Create(
            Convert.FromBase64String(key), "worker", "Worker", time.GetUtcNow(), TokenLifetime);
    }
}
