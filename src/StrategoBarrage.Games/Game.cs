using System.Text.Json.Serialization;

namespace StrategoBarrage.Games;

// A game's current state, in its own partition of the games container. Replaced on every command.
public sealed record Game
{
    public const int CurrentSchemaVersion = 1;

    public string Id => GameId;

    public required string GameId { get; init; }

    public string Type => "game";

    public required GameMode Mode { get; init; }

    public required GameStatus Status { get; init; }

    public required GamePlayers Players { get; init; }

    // The number of events so far.
    public required int Version { get; init; }

    public required GameState State { get; init; }

    // When the side to move runs out of time. Set only while the game is being played.
    public DateTimeOffset? TurnDeadline { get; init; }

    public GameResult? Result { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // When the first move was allowed.
    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Side? SideOf(string playerId) =>
        Players.Red.PlayerId == playerId ? Side.Red
        : Players.Blue.PlayerId == playerId ? Side.Blue
        : null;
}

public sealed record GamePlayers(GamePlayer Red, GamePlayer Blue)
{
    public GamePlayer this[Side side] => side == Side.Red ? Red : Blue;
}

public sealed record GamePlayer(string PlayerId, PlayerKind Kind)
{
    public static GamePlayer Bot { get; } = new("bot/rules-of-thumb", PlayerKind.Bot);
}

// Winner is null for a draw.
public sealed record GameResult(Side? Winner, EndReason Reason);

[JsonConverter(typeof(JsonStringEnumConverter<GameMode>))]
public enum GameMode
{
    [JsonStringEnumMemberName("vs-bot")]
    VsBot,

    [JsonStringEnumMemberName("vs-person")]
    VsPerson,
}

[JsonConverter(typeof(JsonStringEnumConverter<GameStatus>))]
public enum GameStatus
{
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    [JsonStringEnumMemberName("setup")]
    Setup,

    [JsonStringEnumMemberName("playing")]
    Playing,

    [JsonStringEnumMemberName("over")]
    Over,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter<PlayerKind>))]
public enum PlayerKind
{
    [JsonStringEnumMemberName("human")]
    Human,

    [JsonStringEnumMemberName("bot")]
    Bot,
}

[JsonConverter(typeof(JsonStringEnumConverter<EndReason>))]
public enum EndReason
{
    [JsonStringEnumMemberName("flag")]
    Flag,

    [JsonStringEnumMemberName("no-moves")]
    NoMoves,

    [JsonStringEnumMemberName("draw")]
    Draw,

    [JsonStringEnumMemberName("resigned")]
    Resigned,

    [JsonStringEnumMemberName("left")]
    Left,

    [JsonStringEnumMemberName("timed-out")]
    TimedOut,
}
