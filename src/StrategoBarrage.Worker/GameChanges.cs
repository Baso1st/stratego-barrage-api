using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StrategoBarrage.Games;

namespace StrategoBarrage.Worker;

// Everything that follows a saved command, from the games container's change feed. The feed shows each game
// document's latest version, so two quick changes to a game may show as one; only the latest matters here, since
// only the current turn needs a timer or a bot move. Event documents are on the feed too, and are skipped.
public sealed class GameChanges(GameMessages messages, PlayerStore players, ILogger<GameChanges> logger)
{
    // Every step is keyed by game and version, so a retried batch repeats nothing that counts.
    [Function(nameof(GameChanges))]
    [FixedDelayRetry(5, "00:00:05")]
    public async Task RunAsync(
        [CosmosDBTrigger(
            "stratego",
            "games",
            Connection = "cosmos",
            LeaseContainerName = "leases",
            FeedPollDelay = 500)]
        IReadOnlyList<JsonElement> documents,
        CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            if (document.GetProperty("type").GetString() != "game")
            {
                continue;
            }

            var game = document.Deserialize<Game>(GameJson.Options)
                ?? throw new InvalidOperationException("A game document is empty.");
            switch (game.Status)
            {
                case GameStatus.Playing:
                    await messages.TurnStartedAsync(game, cancellationToken);
                    break;
                case GameStatus.Over:
                    await RecordAsync(game, cancellationToken);
                    break;
            }
        }
    }

    // Adds the game to each person's record and stats. The bot has none.
    private async Task RecordAsync(Game game, CancellationToken cancellationToken)
    {
        foreach (var side in new[] { Side.Red, Side.Blue })
        {
            if (game.Players[side].Kind != PlayerKind.Human)
            {
                continue;
            }

            var finished = FinishedGame.For(game, side);
            if (!await players.RecordAsync(finished, cancellationToken))
            {
                logger.LogInformation(
                    "Game {GameId} was already recorded for player {PlayerId}.", game.GameId, finished.PlayerId);
            }
        }
    }
}
