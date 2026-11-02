namespace StrategoBarrage.Games.Tests;

public class StandInRulesTests
{
    private readonly StandInRules _rules = new();

    [Fact]
    public void EachSideStartsWithEightPiecesOnItsOwnRowAndRedMovesFirst()
    {
        var state = _rules.StartingPosition();

        var red = state.Pieces.Where(piece => piece.Side == Side.Red).ToList();
        var blue = state.Pieces.Where(piece => piece.Side == Side.Blue).ToList();
        state.ToMove.ShouldBe(Side.Red);
        red.Count.ShouldBe(8);
        blue.Count.ShouldBe(8);
        red.ShouldAllBe(piece => piece.Square.Row == 6);
        blue.ShouldAllBe(piece => piece.Square.Row == 3);
    }

    [Fact]
    public void PiecesMoveOneSquareOntoAnEmptySquare()
    {
        var moves = _rules.LegalMoves(_rules.StartingPosition());

        // Red's row is columns 1 to 8: each piece can step up or down, and the two ends can step sideways.
        moves.Count.ShouldBe(18);
        moves.ShouldContain(new Move(new Square(6, 1), new Square(5, 1)));
        moves.ShouldContain(new Move(new Square(6, 1), new Square(7, 1)));
        moves.ShouldContain(new Move(new Square(6, 1), new Square(6, 0)));
        moves.ShouldNotContain(new Move(new Square(6, 1), new Square(6, 2)));
        moves.ShouldAllBe(move =>
            Math.Abs(move.To.Row - move.From.Row) + Math.Abs(move.To.Col - move.From.Col) == 1);
    }

    [Fact]
    public void OnlyTheSideToMoveHasMoves()
    {
        var moves = _rules.LegalMoves(_rules.StartingPosition());

        moves.ShouldAllBe(move => move.From.Row == 6);
    }

    [Fact]
    public void PiecesStayOnTheBoard()
    {
        var state = new GameState(Side.Red, [new Piece(Side.Red, new Square(0, 0))]);

        _rules.LegalMoves(state).ShouldBe(
        [
            new Move(new Square(0, 0), new Square(1, 0)),
            new Move(new Square(0, 0), new Square(0, 1)),
        ]);
    }

    [Fact]
    public void AMoveMovesThePieceAndPassesTheTurn()
    {
        var move = new Move(new Square(6, 1), new Square(5, 1));

        var result = _rules.ApplyMove(_rules.StartingPosition(), move);

        result.Ending.ShouldBeNull();
        result.State.ToMove.ShouldBe(Side.Blue);
        result.State.Pieces.ShouldContain(new Piece(Side.Red, new Square(5, 1)));
        result.State.Pieces.ShouldNotContain(new Piece(Side.Red, new Square(6, 1)));
        result.State.Pieces.Count.ShouldBe(16);
    }
}
