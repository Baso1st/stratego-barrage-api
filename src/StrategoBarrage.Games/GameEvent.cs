using System.Text.Json.Serialization;

namespace StrategoBarrage.Games;

// One thing that happened in a game, in the game's partition. Append-only: an event is created
// once and never changed. Its sequence number is the game's version once it's applied.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GameStarted), "game-started")]
[JsonDerivedType(typeof(MoveMade), "move-made")]
[JsonDerivedType(typeof(GameEnded), "game-ended")]
public abstract record GameEvent
{
    public string Id => $"{GameId}:{Seq:D6}";

    public required string GameId { get; init; }

    public string Type => "event";

    public required int Seq { get; init; }

    // The side that acted, or null when nobody did (the game starting, or a turn running out).
    public Side? By { get; init; }

    public required DateTimeOffset At { get; init; }

    public int SchemaVersion { get; init; } = Game.CurrentSchemaVersion;
}

public sealed record GameStarted : GameEvent
{
    public required GameStartedData Data { get; init; }
}

public sealed record GameStartedData(GameMode Mode, GamePlayers Players);

public sealed record MoveMade : GameEvent
{
    public required Move Data { get; init; }
}

public sealed record GameEnded : GameEvent
{
    public required GameResult Data { get; init; }
}
