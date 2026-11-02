using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace StrategoBarrage.Games;

// A game as it was read, with the ETag that a write of it must still match.
public sealed record StoredGame(Game Game, string ETag);

// A game's documents in the games container, all in the game's own partition. Each command is one
// point read and one transactional batch, which either saves everything or nothing.
public sealed class GameStore(Container container)
{
    public async Task<StoredGame?> FindAsync(string gameId, CancellationToken cancellationToken)
    {
        // The stream API answers 404 with a status, not an exception.
        using var response = await container.ReadItemStreamAsync(
            gameId, new PartitionKey(gameId), cancellationToken: cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var game = await JsonSerializer.DeserializeAsync<Game>(response.Content, GameJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Game {gameId} is empty.");
        return new StoredGame(game, response.Headers.ETag);
    }

    // Creates a new game and its first events. Returns null if the game already exists.
    public Task<StoredGame?> CreateAsync(
        Game game, IReadOnlyList<GameEvent> events, CancellationToken cancellationToken)
    {
        var batch = container.CreateTransactionalBatch(new PartitionKey(game.GameId))
            .CreateItem(game);
        return ExecuteAsync(batch, game, events, cancellationToken);
    }

    // Replaces the game and adds its new events, only if nothing else has written the game since it was read.
    // Returns null if something has.
    public Task<StoredGame?> SaveAsync(
        StoredGame read, Game game, IReadOnlyList<GameEvent> events, CancellationToken cancellationToken)
    {
        var batch = container.CreateTransactionalBatch(new PartitionKey(game.GameId))
            .ReplaceItem(game.Id, game, new TransactionalBatchItemRequestOptions { IfMatchEtag = read.ETag });
        return ExecuteAsync(batch, game, events, cancellationToken);
    }

    // The game's events after a version, oldest first.
    public async Task<IReadOnlyList<GameEvent>> GetEventsAsync(
        string gameId, int afterVersion, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'event' AND c.seq > @after ORDER BY c.seq")
            .WithParameter("@after", afterVersion);
        using var iterator = container.GetItemQueryIterator<GameEvent>(
            query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(gameId) });

        var events = new List<GameEvent>();
        while (iterator.HasMoreResults)
        {
            events.AddRange(await iterator.ReadNextAsync(cancellationToken));
        }

        return events;
    }

    private static async Task<StoredGame?> ExecuteAsync(
        TransactionalBatch batch, Game game, IReadOnlyList<GameEvent> events, CancellationToken cancellationToken)
    {
        foreach (var gameEvent in events)
        {
            // As the base type, so the event's "kind" is written.
            batch.CreateItem<GameEvent>(gameEvent);
        }

        using var response = await batch.ExecuteAsync(cancellationToken);

        // A failed batch reports the status of the operation that failed: 412 when the ETag no longer
        // matches, or 409 when a game or event with that id already exists.
        if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Saving game {game.GameId} failed with {(int)response.StatusCode}: {response.ErrorMessage}");
        }

        return new StoredGame(game, response[0].ETag);
    }
}
