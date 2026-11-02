namespace StrategoBarrage.Games.Tests;

public class GameDeciderTests
{
    private const string GameId = "0192f3c4-0000-7000-8000-000000000001";
    private const string PlayerId = "0192f3a1-0000-7000-8000-000000000001";
    private static readonly TimeSpan TurnLimit = TimeSpan.FromMinutes(10);
    private static readonly Move RedOpening = new(new Square(6, 1), new Square(5, 1));
    private static readonly Move BlueOpening = new(new Square(3, 1), new Square(4, 1));

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly GameDecider _decider;

    public GameDeciderTests()
    {
        _decider = new GameDecider(new StandInRules(), new GameOptions { TurnLimit = TurnLimit }, _clock);
    }

    [Fact]
    public void StartingAGameMakesThePlayerRedAgainstTheBot()
    {
        var events = Accept(null, new StartGame(GameId, PlayerId));
        var game = _decider.Apply(null, events);

        var started = events.ShouldHaveSingleItem().ShouldBeOfType<GameStarted>();
        started.Id.ShouldBe($"{GameId}:000001");
        started.By.ShouldBeNull();
        game.Id.ShouldBe(GameId);
        game.Mode.ShouldBe(GameMode.VsBot);
        game.Status.ShouldBe(GameStatus.Playing);
        game.Players.Red.ShouldBe(new GamePlayer(PlayerId, PlayerKind.Human));
        game.Players.Blue.ShouldBe(GamePlayer.Bot);
        game.Version.ShouldBe(1);
        game.State.ToMove.ShouldBe(Side.Red);
        game.TurnDeadline.ShouldBe(_clock.Now + TurnLimit);
        game.CreatedAt.ShouldBe(_clock.Now);
        game.StartedAt.ShouldBe(_clock.Now);
        game.SchemaVersion.ShouldBe(1);
    }

    [Fact]
    public void AGameCantBeStartedTwice()
    {
        Reject(Start(), new StartGame(GameId, PlayerId)).ShouldBe(RejectionReason.GameExists);
    }

    [Fact]
    public void AMoveAddsOneEventPassesTheTurnAndResetsTheDeadline()
    {
        var game = Start();
        _clock.Now += TimeSpan.FromMinutes(3);

        var events = Accept(game, new MakeMove(PlayerId, RedOpening));
        var next = _decider.Apply(game, events);

        var made = events.ShouldHaveSingleItem().ShouldBeOfType<MoveMade>();
        made.Id.ShouldBe($"{GameId}:000002");
        made.By.ShouldBe(Side.Red);
        made.Data.ShouldBe(RedOpening);
        next.Version.ShouldBe(2);
        next.State.ToMove.ShouldBe(Side.Blue);
        next.TurnDeadline.ShouldBe(_clock.Now + TurnLimit);
        next.UpdatedAt.ShouldBe(_clock.Now);
    }

    [Fact]
    public void TheBotMovesAsBlue()
    {
        var game = Play(Start(), new MakeMove(PlayerId, RedOpening));

        var next = Play(game, new MakeMove(GamePlayer.Bot.PlayerId, BlueOpening));

        next.Version.ShouldBe(3);
        next.State.ToMove.ShouldBe(Side.Red);
    }

    [Fact]
    public void AMoveOutOfTurnIsRefused()
    {
        Reject(Start(), new MakeMove(GamePlayer.Bot.PlayerId, BlueOpening)).ShouldBe(RejectionReason.NotYourTurn);
    }

    [Fact]
    public void AMoveTheRulesDontAllowIsRefused()
    {
        var sideways = new Move(new Square(6, 1), new Square(6, 2));

        Reject(Start(), new MakeMove(PlayerId, sideways)).ShouldBe(RejectionReason.IllegalMove);
    }

    [Fact]
    public void AMoveAfterTheDeadlineIsRefusedEvenBeforeTheTimerIsHandled()
    {
        var game = Start();
        _clock.Now += TurnLimit;

        Reject(game, new MakeMove(PlayerId, RedOpening)).ShouldBe(RejectionReason.TurnTimedOut);
    }

    [Fact]
    public void SomeoneNotInTheGameIsRefused()
    {
        var game = Play(Start(), new Resign(PlayerId));

        // Even once the game is over, so they learn nothing about it.
        Reject(game, new MakeMove("someone-else", RedOpening)).ShouldBe(RejectionReason.NotAPlayer);
        Reject(game, new Resign("someone-else")).ShouldBe(RejectionReason.NotAPlayer);
    }

    [Fact]
    public void ResigningEndsTheGameAndTheOpponentWins()
    {
        var game = Start();
        _clock.Now += TimeSpan.FromMinutes(1);

        var events = Accept(game, new Resign(PlayerId));
        var over = _decider.Apply(game, events);

        var ended = events.ShouldHaveSingleItem().ShouldBeOfType<GameEnded>();
        ended.By.ShouldBe(Side.Red);
        ended.Data.ShouldBe(new GameResult(Side.Blue, EndReason.Resigned));
        over.Status.ShouldBe(GameStatus.Over);
        over.Version.ShouldBe(2);
        over.Result.ShouldBe(ended.Data);
        over.TurnDeadline.ShouldBeNull();
        over.EndedAt.ShouldBe(_clock.Now);
    }

    [Fact]
    public void APlayerCanResignOnTheirOpponentsTurn()
    {
        var over = Play(Start(), new Resign(GamePlayer.Bot.PlayerId));

        over.Result.ShouldBe(new GameResult(Side.Red, EndReason.Resigned));
    }

    [Fact]
    public void NothingIsAcceptedOnceTheGameIsOver()
    {
        var game = Play(Start(), new Resign(PlayerId));

        Reject(game, new MakeMove(PlayerId, RedOpening)).ShouldBe(RejectionReason.GameOver);
        Reject(game, new Resign(GamePlayer.Bot.PlayerId)).ShouldBe(RejectionReason.GameOver);
        Reject(game, new TimeOut(game.Version)).ShouldBe(RejectionReason.TimerPassed);
    }

    [Fact]
    public void RunningOutOfTimeEndsTheGameAndTheOpponentWins()
    {
        var game = Play(Start(), new MakeMove(PlayerId, RedOpening));
        _clock.Now += TurnLimit;

        var events = Accept(game, new TimeOut(game.Version));
        var over = _decider.Apply(game, events);

        var ended = events.ShouldHaveSingleItem().ShouldBeOfType<GameEnded>();
        ended.By.ShouldBeNull();
        ended.Data.ShouldBe(new GameResult(Side.Red, EndReason.TimedOut));
        over.Status.ShouldBe(GameStatus.Over);
        over.Version.ShouldBe(3);
    }

    [Fact]
    public void ATimerForATurnAlreadyPlayedChangesNothing()
    {
        var game = Start();
        var timerVersion = game.Version;
        game = Play(game, new MakeMove(PlayerId, RedOpening));

        Reject(game, new TimeOut(timerVersion)).ShouldBe(RejectionReason.TimerPassed);
    }

    [Fact]
    public void EventsMustFollowTheGamesVersion()
    {
        var game = Start();
        var events = Accept(game, new MakeMove(PlayerId, RedOpening));

        Should.Throw<InvalidOperationException>(() => _decider.Apply(_decider.Apply(game, events), events));
    }

    private Game Start() => Play(null, new StartGame(GameId, PlayerId));

    private Game Play(Game? game, GameCommand command) => _decider.Apply(game, Accept(game, command));

    private IReadOnlyList<GameEvent> Accept(Game? game, GameCommand command) =>
        _decider.Decide(game, command).ShouldBeOfType<Accepted>().Events;

    private RejectionReason Reject(Game game, GameCommand command) =>
        _decider.Decide(game, command).ShouldBeOfType<Rejected>().Reason;

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
