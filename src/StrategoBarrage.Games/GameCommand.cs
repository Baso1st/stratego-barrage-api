namespace StrategoBarrage.Games;

public abstract record GameCommand;

// A game against the computer: the player is Red, and the bot is Blue.
public sealed record StartGame(string GameId, string PlayerId) : GameCommand;

public sealed record MakeMove(string PlayerId, Move Move) : GameCommand;

public sealed record Resign(string PlayerId) : GameCommand;

// A turn deadline falling due. Version is the game's version when the deadline was set, so a timer
// for a turn that has already been played changes nothing.
public sealed record TimeOut(int Version) : GameCommand;

// What a command leads to: the events it causes, or why it's refused.
public abstract record Decision;

public sealed record Accepted(IReadOnlyList<GameEvent> Events) : Decision;

public sealed record Rejected(RejectionReason Reason, string Detail) : Decision;

public enum RejectionReason
{
    GameExists,
    NotAPlayer,
    GameOver,
    NotYourTurn,
    TurnTimedOut,
    IllegalMove,
    TimerPassed,
}
