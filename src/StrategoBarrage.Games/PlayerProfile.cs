using System.Text.Json.Serialization;

namespace StrategoBarrage.Games;

// A player's profile, in their own partition of the players container. Its stats are split by opponent.
public sealed record PlayerProfile
{
    public const int CurrentSchemaVersion = 1;

    public string Id => PlayerId;

    public required string PlayerId { get; init; }

    public string Type => "profile";

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public required PlayerStats Stats { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}

public sealed record PlayerStats(OpponentStats VsComputer, OpponentStats VsPeople)
{
    public static PlayerStats None { get; } = new(OpponentStats.None, OpponentStats.None);

    public PlayerStats Add(FinishedGame game) => game.Mode == GameMode.VsBot
        ? this with { VsComputer = VsComputer.Add(game) }
        : this with { VsPeople = VsPeople.Add(game) };
}

public sealed record OpponentStats(int Played, int Won, int Lost, int Drawn, long SecondsPlayed)
{
    public static OpponentStats None { get; } = new(0, 0, 0, 0, 0);

    public OpponentStats Add(FinishedGame game) => new(
        Played + 1,
        Won + (game.Result == GameOutcome.Won ? 1 : 0),
        Lost + (game.Result == GameOutcome.Lost ? 1 : 0),
        Drawn + (game.Result == GameOutcome.Drawn ? 1 : 0),
        SecondsPlayed + game.DurationSeconds);
}

// One per finished game, in the player's partition. Its id is the game's, so a game is only ever recorded once.
public sealed record FinishedGame
{
    public const int CurrentSchemaVersion = 1;

    public string Id => GameId;

    public required string GameId { get; init; }

    public required string PlayerId { get; init; }

    public string Type => "finished-game";

    public required GameMode Mode { get; init; }

    public required Side Side { get; init; }

    public required GamePlayer Opponent { get; init; }

    public required GameOutcome Result { get; init; }

    public required EndReason Reason { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset EndedAt { get; init; }

    // From the first move allowed to the end.
    public required long DurationSeconds { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    // The record of a game that's over, for the player on that side.
    public static FinishedGame For(Game game, Side side)
    {
        var result = game.Result ?? throw new InvalidOperationException($"Game {game.GameId} isn't over.");

        // In UTC, like every stored time. The Functions host reads change feed documents with Newtonsoft.Json,
        // which hands times over in the machine's local time zone.
        var startedAt = (game.StartedAt ?? game.CreatedAt).ToUniversalTime();
        var endedAt = (game.EndedAt ?? game.UpdatedAt).ToUniversalTime();
        return new FinishedGame
        {
            GameId = game.GameId,
            PlayerId = game.Players[side].PlayerId,
            Mode = game.Mode,
            Side = side,
            Opponent = game.Players[side.Opponent],
            Result = result.Winner is null ? GameOutcome.Drawn
                : result.Winner == side ? GameOutcome.Won
                : GameOutcome.Lost,
            Reason = result.Reason,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationSeconds = (long)(endedAt - startedAt).TotalSeconds,
        };
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<GameOutcome>))]
public enum GameOutcome
{
    [JsonStringEnumMemberName("won")]
    Won,

    [JsonStringEnumMemberName("lost")]
    Lost,

    [JsonStringEnumMemberName("drawn")]
    Drawn,
}
