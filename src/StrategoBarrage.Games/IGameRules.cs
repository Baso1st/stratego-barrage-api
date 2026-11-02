namespace StrategoBarrage.Games;

// What the rules decide. Everything else about a game (players, turns, deadlines, storage)
// works the same whatever implements this.
public interface IGameRules
{
    GameState StartingPosition();

    // The side to move's legal moves.
    IReadOnlyList<Move> LegalMoves(GameState state);

    // Plays a legal move for the side to move, and passes the turn. Ending is set when the move ends the game.
    MoveResult ApplyMove(GameState state, Move move);
}

public sealed record MoveResult(GameState State, GameResult? Ending);
