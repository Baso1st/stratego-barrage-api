using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StrategoBarrage.Games;

namespace StrategoBarrage.Bot;

// Plays the bot's turns. The bot is a player: it fetches its own view, the same one a person gets, and submits its
// move through the same endpoint, so it never sees more than a player would.
public sealed class BotTurns(ApiClient api, ILogger<BotTurns> logger)
{
    [Function(nameof(BotTurns))]
    public async Task RunAsync(
        [ServiceBusTrigger(Queues.BotTurns, Connection = "servicebus")] ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var turn = message.Body.ToObjectFromJson<BotTurn>(GameJson.Options)
            ?? throw new InvalidOperationException($"Bot turn {message.MessageId} is empty.");

        // A message for a turn that's no longer current, such as a retry or a turn that ran out, changes nothing.
        var view = await api.GetViewAsync(turn.GameId, cancellationToken);
        if (view is null
            || view.Status != GameStatus.Playing
            || view.ToMove != view.You
            || view.Version != turn.Version)
        {
            logger.LogInformation("Bot turn {GameId} at version {Version} has passed.", turn.GameId, turn.Version);
            return;
        }

        // For now the bot plays its first legal move; rules of thumb come with the engine.
        if (view.LegalMoves is not [var move, ..])
        {
            logger.LogWarning("The bot has no legal move in game {GameId}.", turn.GameId);
            return;
        }

        if (!await api.MoveAsync(turn.GameId, move, view.Version, cancellationToken))
        {
            logger.LogInformation(
                "Game {GameId} changed before the bot's move at version {Version}.", turn.GameId, turn.Version);
        }
    }
}
