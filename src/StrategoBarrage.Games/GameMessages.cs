namespace StrategoBarrage.Games;

// The Service Bus queues, and the messages on them. Delivery is at least once, so each message is keyed by game
// and version, and one for a version that has passed changes nothing.
public static class Queues
{
    // Read by the bot: it's the bot's turn.
    public const string BotTurns = "bot-turns";

    // Scheduled messages, read by the worker when they're due. The message's subject says what kind it is.
    public const string GameTimers = "game-timers";
}

public sealed record BotTurn(string GameId, int Version);

// The player to move at that version runs out of time.
public sealed record TurnExpired(string GameId, int Version)
{
    public const string Subject = "turn-expired";
}
