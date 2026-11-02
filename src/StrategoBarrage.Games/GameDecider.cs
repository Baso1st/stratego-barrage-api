namespace StrategoBarrage.Games;

public sealed record GameOptions
{
    public TimeSpan TurnLimit { get; init; } = TimeSpan.FromMinutes(10);
}

// Runs a game's commands in two steps. Decide checks a command against the game as it stands, and
// returns the events it causes or why it's refused. Apply folds an event into the game. Decide is the
// only step that reads the clock, and every event carries its own time, so applying the same events
// always gives the same game.
public sealed class GameDecider(IGameRules rules, GameOptions options, TimeProvider time)
{
    public Decision Decide(Game? game, GameCommand command)
    {
        var now = time.GetUtcNow();
        if (command is StartGame start)
        {
            return game is null
                ? new Accepted([Started(start, now)])
                : new Rejected(RejectionReason.GameExists, $"Game {start.GameId} already exists.");
        }

        ArgumentNullException.ThrowIfNull(game);
        return command switch
        {
            MakeMove move => Move(game, move, now),
            Resign resign => Resign(game, resign, now),
            TimeOut timeOut => TimeOut(game, timeOut, now),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
        };
    }

    public Game Apply(Game? game, GameEvent gameEvent)
    {
        var expected = (game?.Version ?? 0) + 1;
        if (gameEvent.Seq != expected)
        {
            throw new InvalidOperationException($"Event {gameEvent.Id} doesn't follow version {expected - 1}.");
        }

        return (game, gameEvent) switch
        {
            (null, GameStarted started) => new Game
            {
                GameId = started.GameId,
                Mode = started.Data.Mode,
                Status = GameStatus.Playing,
                Players = started.Data.Players,
                Version = started.Seq,
                State = rules.StartingPosition(),
                TurnDeadline = started.At + options.TurnLimit,
                CreatedAt = started.At,
                StartedAt = started.At,
                UpdatedAt = started.At,
            },
            ({ } current, MoveMade made) => current with
            {
                Version = made.Seq,
                State = rules.ApplyMove(current.State, made.Data).State,
                TurnDeadline = made.At + options.TurnLimit,
                UpdatedAt = made.At,
            },
            ({ } current, GameEnded ended) => current with
            {
                Status = GameStatus.Over,
                Version = ended.Seq,
                TurnDeadline = null,
                Result = ended.Data,
                EndedAt = ended.At,
                UpdatedAt = ended.At,
            },
            _ => throw new InvalidOperationException($"Event {gameEvent.Id} can't be applied to this game."),
        };
    }

    public Game Apply(Game? game, IEnumerable<GameEvent> events)
    {
        foreach (var gameEvent in events)
        {
            game = Apply(game, gameEvent);
        }

        return game ?? throw new ArgumentException("There are no events to apply.", nameof(events));
    }

    private static GameStarted Started(StartGame start, DateTimeOffset now) => new()
    {
        GameId = start.GameId,
        Seq = 1,
        At = now,
        Data = new GameStartedData(
            GameMode.VsBot,
            new GamePlayers(new GamePlayer(start.PlayerId, PlayerKind.Human), GamePlayer.Bot)),
    };

    private Decision Move(Game game, MakeMove command, DateTimeOffset now)
    {
        if (CheckPlayer(game, command.PlayerId, out var side) is { } refused)
        {
            return refused;
        }

        if (side != game.State.ToMove)
        {
            return new Rejected(RejectionReason.NotYourTurn, "It's your opponent's turn.");
        }

        // The turn's timer may not have been handled yet, but the turn is over.
        if (now >= game.TurnDeadline)
        {
            return new Rejected(RejectionReason.TurnTimedOut, "Your turn ran out of time.");
        }

        if (!rules.LegalMoves(game.State).Contains(command.Move))
        {
            return new Rejected(RejectionReason.IllegalMove, "The rules don't allow that move.");
        }

        var made = new MoveMade
        {
            GameId = game.GameId,
            Seq = game.Version + 1,
            By = side,
            At = now,
            Data = command.Move,
        };
        var ending = rules.ApplyMove(game.State, command.Move).Ending;
        return ending is null
            ? new Accepted([made])
            : new Accepted([made, Ended(game, made.Seq + 1, by: null, ending, now)]);
    }

    private static Decision Resign(Game game, Resign command, DateTimeOffset now)
    {
        if (CheckPlayer(game, command.PlayerId, out var side) is { } refused)
        {
            return refused;
        }

        var result = new GameResult(side.Opponent, EndReason.Resigned);
        return new Accepted([Ended(game, game.Version + 1, side, result, now)]);
    }

    private static Decision TimeOut(Game game, TimeOut command, DateTimeOffset now)
    {
        if (game.Status != GameStatus.Playing || game.Version != command.Version)
        {
            return new Rejected(RejectionReason.TimerPassed, $"The game has moved on from version {command.Version}.");
        }

        var result = new GameResult(game.State.ToMove.Opponent, EndReason.TimedOut);
        return new Accepted([Ended(game, game.Version + 1, by: null, result, now)]);
    }

    // Someone not in the game is refused first, so they learn nothing about it.
    private static Rejected? CheckPlayer(Game game, string playerId, out Side side)
    {
        if (game.SideOf(playerId) is not { } found)
        {
            side = default;
            return new Rejected(RejectionReason.NotAPlayer, "You're not a player in this game.");
        }

        side = found;
        return game.Status == GameStatus.Playing
            ? null
            : new Rejected(RejectionReason.GameOver, "The game is over.");
    }

    private static GameEnded Ended(Game game, int seq, Side? by, GameResult result, DateTimeOffset now) => new()
    {
        GameId = game.GameId,
        Seq = seq,
        By = by,
        At = now,
        Data = result,
    };
}
