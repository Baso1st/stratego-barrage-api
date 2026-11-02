using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using StrategoBarrage.Games;
using StrategoBarrage.ServiceDefaults;

namespace StrategoBarrage.Bot;

// Calls the API's game endpoints as the bot player.
public sealed class ApiClient(HttpClient http, IConfiguration configuration, TimeProvider time)
{
    // The bot's view of a game, or null if there's no such game.
    public async Task<PlayerView?> GetViewAsync(string gameId, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"v1/games/{gameId}");
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlayerView>(GameJson.Options, cancellationToken);
    }

    // Submits a move. Returns false if the game changed since the version it was based on.
    public async Task<bool> MoveAsync(
        string gameId, Move move, int expectedVersion, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, $"v1/games/{gameId}/moves");
        request.Content = JsonContent.Create(
            new { from = move.From, to = move.To, expectedVersion }, options: GameJson.Options);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", DevelopmentTokens.CreateForApp(configuration, "bot", "Bot", time.GetUtcNow()));
        return request;
    }
}
