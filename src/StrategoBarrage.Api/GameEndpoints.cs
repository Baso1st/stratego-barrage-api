using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using StrategoBarrage.Games;

namespace StrategoBarrage.Api;

public sealed record StartGameRequest(GameMode Mode);

public sealed record MoveRequest(Square From, Square To, int ExpectedVersion);

public sealed record TurnExpiredRequest(int Version);

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var games = app.MapGroup("/v1/games")
            .RequireAuthorization(Policies.Player)
            .WithTags("Games")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        games.MapPost("", StartAsync)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        games.MapGet("/{id}", GetAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        games.MapPost("/{id}/moves", MoveAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        games.MapPost("/{id}/resign", ResignAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/internal/games/{id}/turn-expired", TurnExpiredAsync)
            .RequireAuthorization(Policies.Worker)
            .ExcludeFromDescription();
    }

    private static async Task<Results<Created<PlayerView>, ProblemHttpResult>> StartAsync(
        StartGameRequest request, ClaimsPrincipal user, GameService games, Views views, CancellationToken cancellationToken)
    {
        if (user.IsInRole(Roles.Bot))
        {
            return Problem(StatusCodes.Status403Forbidden, "The bot doesn't start games.");
        }

        if (request.Mode != GameMode.VsBot)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "Only games against the computer can be started.");
        }

        var game = (await games.StartAsync(user.PlayerId(), cancellationToken)).Game;
        return TypedResults.Created($"/v1/games/{game.GameId}", views.For(game, user.PlayerId()));
    }

    private static async Task<Results<Ok<PlayerView>, ProblemHttpResult>> GetAsync(
        string id, ClaimsPrincipal user, GameStore store, Views views, CancellationToken cancellationToken)
    {
        var read = await store.FindAsync(id, cancellationToken);
        if (read is null)
        {
            return GameNotFound();
        }

        return read.Game.SideOf(user.PlayerId()) is null
            ? NotAPlayer()
            : TypedResults.Ok(views.For(read.Game, user.PlayerId()));
    }

    private static async Task<Results<Ok<PlayerView>, ProblemHttpResult>> MoveAsync(
        string id,
        MoveRequest request,
        ClaimsPrincipal user,
        GameService games,
        Views views,
        CancellationToken cancellationToken)
    {
        var command = new MakeMove(user.PlayerId(), new Move(request.From, request.To), request.ExpectedVersion);
        return ToResponse(await games.ExecuteAsync(id, command, cancellationToken), user, views);
    }

    private static async Task<Results<Ok<PlayerView>, ProblemHttpResult>> ResignAsync(
        string id, ClaimsPrincipal user, GameService games, Views views, CancellationToken cancellationToken)
    {
        var result = await games.ExecuteAsync(id, new Resign(user.PlayerId()), cancellationToken);
        return ToResponse(result, user, views);
    }

    // The player to move loses, if the game is still at that version. A timer that has passed is not an error:
    // the worker's work is done either way, so a retried timer changes nothing.
    private static async Task<Results<NoContent, ProblemHttpResult>> TurnExpiredAsync(
        string id, TurnExpiredRequest request, GameService games, CancellationToken cancellationToken)
    {
        var result = await games.ExecuteAsync(id, new TimeOut(request.Version), cancellationToken);
        return result is GameNotFound ? GameNotFound() : TypedResults.NoContent();
    }

    private static Results<Ok<PlayerView>, ProblemHttpResult> ToResponse(
        CommandResult result, ClaimsPrincipal user, Views views) => result switch
        {
            Applied applied => TypedResults.Ok(views.For(applied.Game, user.PlayerId())),
            Refused { Rejection.Reason: RejectionReason.NotAPlayer } => NotAPlayer(),

            Refused refused when IsOutOfDate(refused.Rejection.Reason) => Problem(
                StatusCodes.Status409Conflict,
                refused.Rejection.Detail,
                refused.Rejection.Reason,
                views.For(refused.Game, user.PlayerId())),
            Refused refused => Problem(
                StatusCodes.Status422UnprocessableEntity, refused.Rejection.Detail, refused.Rejection.Reason),
            _ => GameNotFound(),
        };

    // The game changed since the caller's view: a `409` with the current view, to try again from. Anything else
    // refused is the caller's mistake whatever the version, so it's a `422`.
    private static bool IsOutOfDate(RejectionReason reason) =>
        reason is RejectionReason.StaleVersion or RejectionReason.GameOver or RejectionReason.TurnTimedOut;

    private static ProblemHttpResult GameNotFound() =>
        Problem(StatusCodes.Status404NotFound, "There's no game with that id.");

    private static ProblemHttpResult NotAPlayer() =>
        Problem(StatusCodes.Status403Forbidden, "You're not playing in this game.");

    private static ProblemHttpResult Problem(
        int status, string detail, RejectionReason? reason = null, PlayerView? view = null)
    {
        var extensions = new Dictionary<string, object?>();
        if (reason is not null)
        {
            extensions["reason"] = reason;
        }

        if (view is not null)
        {
            extensions["view"] = view;
        }

        return TypedResults.Problem(statusCode: status, detail: detail, extensions: extensions);
    }
}

// Builds a player's view of a game at the server's current time.
public sealed class Views(IGameRules rules, TimeProvider time)
{
    public PlayerView For(Game game, string playerId) =>
        PlayerView.For(game, game.SideOf(playerId)!.Value, rules, time.GetUtcNow());
}
