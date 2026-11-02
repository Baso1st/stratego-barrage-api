using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using StrategoBarrage.Games;

namespace StrategoBarrage.Worker;

// Timers from the game-timers queue, passed to the API when they're due. A failure leaves the message on the
// queue to be tried again, up to the queue's delivery count, and then it's dead-lettered.
public sealed class GameTimers(ApiClient api)
{
    [Function(nameof(GameTimers))]
    public async Task RunAsync(
        [ServiceBusTrigger(Queues.GameTimers, Connection = "servicebus")] ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        switch (message.Subject)
        {
            case TurnExpired.Subject:
                var timer = message.Body.ToObjectFromJson<TurnExpired>(GameJson.Options)
                    ?? throw new InvalidOperationException($"Timer {message.MessageId} is empty.");
                await api.TurnExpiredAsync(timer, cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Timer {message.MessageId} has an unknown subject: {message.Subject}");
        }
    }
}
