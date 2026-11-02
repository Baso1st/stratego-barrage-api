using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace StrategoBarrage.Games;

// Players' profiles and finished games in the players container, each player in their own partition.
public sealed class PlayerStore(Container container, TimeProvider time)
{
    // Each lost race means another of the player's games was recorded at the same moment.
    private const int Attempts = 3;

    // Adds a finished game to the player's record and stats, in one transactional batch in their partition. The
    // record's id is the game's, so if it's already there the game was counted before, and nothing changes.
    // Returns whether the game was recorded now.
    public async Task<bool> RecordAsync(FinishedGame game, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (profile, etag) = await FindAsync(game.PlayerId, cancellationToken);

            // For now a player's profile is created when their first game ends.
            profile ??= new PlayerProfile
            {
                PlayerId = game.PlayerId,
                Stats = PlayerStats.None,
                CreatedAt = time.GetUtcNow(),
            };
            var updated = profile with { Stats = profile.Stats.Add(game) };

            var batch = container.CreateTransactionalBatch(new PartitionKey(game.PlayerId))
                .CreateItem(game);
            batch = etag is null
                ? batch.CreateItem(updated)
                : batch.ReplaceItem(
                    updated.Id, updated, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // A failed batch reports the status of the operation that failed; the others report 424.
            if (response[0].StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }

            // The profile was created or changed since it was read: read it again, and try again.
            var profileChanged = response[1].StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
            if (!profileChanged || attempt == Attempts)
            {
                throw new InvalidOperationException(
                    $"Recording game {game.GameId} for player {game.PlayerId} failed with " +
                    $"{(int)response.StatusCode}: {response.ErrorMessage}");
            }
        }
    }

    public async Task<(PlayerProfile? Profile, string? ETag)> FindAsync(
        string playerId, CancellationToken cancellationToken)
    {
        using var response = await container.ReadItemStreamAsync(
            playerId, new PartitionKey(playerId), cancellationToken: cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }

        response.EnsureSuccessStatusCode();
        var profile = await JsonSerializer.DeserializeAsync<PlayerProfile>(
            response.Content, GameJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Player {playerId}'s profile is empty.");
        return (profile, response.Headers.ETag);
    }
}
