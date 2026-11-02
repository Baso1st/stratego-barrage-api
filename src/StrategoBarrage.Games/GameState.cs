namespace StrategoBarrage.Games;

// Rows and columns count from 0 to 9. Blue's setup rows are 0 to 3, and Red's are 6 to 9.
public sealed record Square(int Row, int Col);

public sealed record Move(Square From, Square To);

public sealed record Piece(Side Side, Square Square);

// The rules' full state of a game, every piece included. It never leaves the server.
public sealed record GameState(Side ToMove, IReadOnlyList<Piece> Pieces);
