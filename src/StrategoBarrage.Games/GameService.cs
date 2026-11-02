namespace StrategoBarrage.Games;

public abstract record CommandResult;

public sealed record Applied(Game Game, IReadOnlyList<GameEvent> Events) : CommandResult;

public sealed record Refused(Rejected Rejection, Game Game) : CommandResult;

public sealed record GameNotFound : CommandResult;

// Runs a command against a stored game: read it, decide, apply, and save it if nothing else has
// written it in the meantime.
public sealed class GameService(GameStore store, GameDecider decider)
{
    // Each lost race means another command was saved, and turns alternate, so a few attempts are plenty.
    private const int Attempts = 3;

    public async Task<Applied> StartAsync(string playerId, CancellationToken cancellationToken)
    {
        var gameId = Guid.CreateVersion7().ToString();
        var events = ((Accepted)decider.Decide(null, new StartGame(gameId, playerId))).Events;
        var game = decider.Apply(null, events);
        _ = await store.CreateAsync(game, events, cancellationToken)
            ?? throw new InvalidOperationException($"Game {gameId} already exists.");
        return new Applied(game, events);
    }

    public async Task<CommandResult> ExecuteAsync(
        string gameId, GameCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var read = await store.FindAsync(gameId, cancellationToken);
            if (read is null)
            {
                return new GameNotFound();
            }

            var decision = decider.Decide(read.Game, command);
            if (decision is Rejected rejected)
            {
                return new Refused(rejected, read.Game);
            }

            var events = ((Accepted)decision).Events;
            var game = decider.Apply(read.Game, events);
            if (await store.SaveAsync(read, game, events, cancellationToken) is not null)
            {
                return new Applied(game, events);
            }

            // Another command was saved first. Deciding again against the game as it is now gets the
            // right answer: a move based on the old version is refused as stale, a timer for a turn
            // that was played is refused, and resigning still goes through.
            if (attempt == Attempts)
            {
                throw new InvalidOperationException($"Game {gameId} kept changing; gave up after {Attempts} attempts.");
            }
        }
    }
}
