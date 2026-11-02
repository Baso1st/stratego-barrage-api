namespace StrategoBarrage.Games;

// What one player sees of a game. The stand-in rules have nothing hidden, so every piece is shown; the
// engine's view hides the opponent's ranks. Legal moves are listed only on the player's own turn.
public sealed record PlayerView(
    string GameId,
    GameMode Mode,
    GameStatus Status,
    Side You,
    GamePlayers Players,
    int Version,
    Side ToMove,
    IReadOnlyList<Piece> Pieces,
    IReadOnlyList<Move> LegalMoves,
    DateTimeOffset? TurnDeadline,
    GameResult? Result,
    DateTimeOffset ServerTime)
{
    public static PlayerView For(Game game, Side side, IGameRules rules, DateTimeOffset serverTime) => new(
        game.GameId,
        game.Mode,
        game.Status,
        side,
        game.Players,
        game.Version,
        game.State.ToMove,
        game.State.Pieces,
        game.Status == GameStatus.Playing && game.State.ToMove == side ? rules.LegalMoves(game.State) : [],
        game.TurnDeadline,
        game.Result,
        serverTime);
}
