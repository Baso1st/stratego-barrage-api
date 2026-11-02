using Azure.Messaging.ServiceBus;
using StrategoBarrage.Games;

namespace StrategoBarrage.Worker;

// Sends what a new turn needs: the bot's move when it's the bot's turn, and the turn's timer.
public sealed class GameMessages(ServiceBusClient client)
{
    private readonly ServiceBusSender _botTurns = client.CreateSender(Queues.BotTurns);
    private readonly ServiceBusSender _timers = client.CreateSender(Queues.GameTimers);

    public async Task TurnStartedAsync(Game game, CancellationToken cancellationToken)
    {
        // The Basic tier has no duplicate detection, so the message id only helps tracing; the API refuses a
        // second bot move or timer for the same version.
        var messageId = $"{game.GameId}:{game.Version}";

        if (game.Players[game.State.ToMove].Kind == PlayerKind.Bot)
        {
            await _botTurns.SendMessageAsync(
                Message(new BotTurn(game.GameId, game.Version), messageId), cancellationToken);
        }

        if (game.TurnDeadline is { } deadline)
        {
            var timer = Message(new TurnExpired(game.GameId, game.Version), messageId);
            timer.Subject = TurnExpired.Subject;
            await _timers.ScheduleMessageAsync(timer, deadline, cancellationToken);
        }
    }

    private static ServiceBusMessage Message<T>(T body, string messageId) =>
        new(BinaryData.FromObjectAsJson(body, GameJson.Options))
        {
            MessageId = messageId,
            ContentType = "application/json",
        };
}
