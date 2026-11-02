namespace StrategoBarrage.Games;

// Placeholder rules until the engine is built: 8 pieces a side on facing rows, each moving one square
// up, down, left or right onto an empty square. There are no ranks and no attacks, so a game ends
// only by resigning or running out of time.
public sealed class StandInRules : IGameRules
{
    private const int BoardSize = 10;
    private const int RedRow = 6;
    private const int BlueRow = 3;

    private static readonly (int Rows, int Cols)[] Steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    public GameState StartingPosition() => new(
        Side.Red,
        [
            .. Enumerable.Range(1, 8).Select(col => new Piece(Side.Red, new Square(RedRow, col))),
            .. Enumerable.Range(1, 8).Select(col => new Piece(Side.Blue, new Square(BlueRow, col))),
        ]);

    public IReadOnlyList<Move> LegalMoves(GameState state)
    {
        var occupied = state.Pieces.Select(piece => piece.Square).ToHashSet();
        return
        [
            .. state.Pieces
                .Where(piece => piece.Side == state.ToMove)
                .SelectMany(piece => Steps.Select(step => new Move(
                    piece.Square,
                    new Square(piece.Square.Row + step.Rows, piece.Square.Col + step.Cols))))
                .Where(move => IsOnBoard(move.To) && !occupied.Contains(move.To)),
        ];
    }

    public MoveResult ApplyMove(GameState state, Move move)
    {
        var pieces = state.Pieces
            .Select(piece => piece.Square == move.From ? piece with { Square = move.To } : piece)
            .ToList();
        return new MoveResult(new GameState(state.ToMove.Opponent, pieces), Ending: null);
    }

    private static bool IsOnBoard(Square square) =>
        square.Row is >= 0 and < BoardSize && square.Col is >= 0 and < BoardSize;
}
