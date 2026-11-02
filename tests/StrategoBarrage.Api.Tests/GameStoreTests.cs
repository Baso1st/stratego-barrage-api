using StrategoBarrage.Games;

namespace StrategoBarrage.Api.Tests;

// Games saved to the Cosmos DB emulator: one point read and one transactional batch per command,
// guarded by the ETag.
public class GameStoreTests(TestApp app)
{
    private const string PlayerId = "0192f3a1-0000-7000-8000-000000000001";
    private static readonly Move RedOpening = new(new Square(6, 1), new Square(5, 1));

    private readonly GameStore _store = new(app.Games);
    private readonly GameDecider _decider = new(new StandInRules(), new GameOptions(), TimeProvider.System);

    private GameService Service => new(_store, _decider);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AStartedGameIsSavedWithItsFirstEvent()
    {
        var started = await Service.StartAsync(PlayerId, CancellationToken);

        var read = (await _store.FindAsync(started.Game.GameId, CancellationToken)).ShouldNotBeNull();
        read.Game.Version.ShouldBe(1);
        read.Game.Players.Red.PlayerId.ShouldBe(PlayerId);
        read.ETag.ShouldNotBeNullOrEmpty();
        var events = await _store.GetEventsAsync(started.Game.GameId, afterVersion: 0, CancellationToken);
        events.ShouldHaveSingleItem().ShouldBeOfType<GameStarted>();
    }

    [Fact]
    public async Task AMoveReplacesTheGameAndAddsItsEvent()
    {
        var gameId = (await Service.StartAsync(PlayerId, CancellationToken)).Game.GameId;

        var result = await Service.ExecuteAsync(gameId, new MakeMove(PlayerId, RedOpening, 1), CancellationToken);

        result.ShouldBeOfType<Applied>();
        var read = (await _store.FindAsync(gameId, CancellationToken)).ShouldNotBeNull();
        read.Game.Version.ShouldBe(2);
        read.Game.State.ToMove.ShouldBe(Side.Blue);
        var events = await _store.GetEventsAsync(gameId, afterVersion: 0, CancellationToken);
        events.Select(gameEvent => gameEvent.Seq).ShouldBe([1, 2]);
        events[1].ShouldBeOfType<MoveMade>().Data.ShouldBe(RedOpening);
        (await _store.GetEventsAsync(gameId, afterVersion: 1, CancellationToken)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AWriteBasedOnAnOldReadSavesNothing()
    {
        var gameId = (await Service.StartAsync(PlayerId, CancellationToken)).Game.GameId;
        var first = (await _store.FindAsync(gameId, CancellationToken)).ShouldNotBeNull();
        var second = (await _store.FindAsync(gameId, CancellationToken)).ShouldNotBeNull();

        (await Save(first, new Resign(PlayerId))).ShouldNotBeNull();
        (await Save(second, new MakeMove(PlayerId, RedOpening, 1))).ShouldBeNull();

        var read = (await _store.FindAsync(gameId, CancellationToken)).ShouldNotBeNull();
        read.Game.Result.ShouldBe(new GameResult(Side.Blue, EndReason.Resigned));
        var events = await _store.GetEventsAsync(gameId, afterVersion: 0, CancellationToken);
        events.Select(gameEvent => gameEvent.GetType()).ShouldBe([typeof(GameStarted), typeof(GameEnded)]);
    }

    [Fact]
    public async Task OfAMoveAndATimerAtTheSameVersionOnlyOneGoesThrough()
    {
        var gameId = (await Service.StartAsync(PlayerId, CancellationToken)).Game.GameId;

        var results = await Task.WhenAll(
            Service.ExecuteAsync(gameId, new MakeMove(PlayerId, RedOpening, 1), CancellationToken),
            Service.ExecuteAsync(gameId, new TimeOut(1), CancellationToken));

        results.OfType<Applied>().ShouldHaveSingleItem();
        results.OfType<Refused>().ShouldHaveSingleItem().Rejection.Reason
            .ShouldBeOneOf(RejectionReason.StaleVersion, RejectionReason.TimerPassed);
        (await _store.GetEventsAsync(gameId, afterVersion: 0, CancellationToken)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ResigningGoesThroughEvenWhenAMoveIsSavedFirst()
    {
        var gameId = (await Service.StartAsync(PlayerId, CancellationToken)).Game.GameId;

        var results = await Task.WhenAll(
            Service.ExecuteAsync(gameId, new MakeMove(PlayerId, RedOpening, 1), CancellationToken),
            Service.ExecuteAsync(gameId, new Resign(GamePlayer.Bot.PlayerId), CancellationToken));

        // Whichever is saved first, the resignation is decided again if it loses, and still applies.
        results[1].ShouldBeOfType<Applied>();
        var read = (await _store.FindAsync(gameId, CancellationToken)).ShouldNotBeNull();
        read.Game.Result.ShouldBe(new GameResult(Side.Red, EndReason.Resigned));
    }

    [Fact]
    public async Task AGameThatDoesntExistIsNotFound()
    {
        var gameId = Guid.CreateVersion7().ToString();

        var result = await Service.ExecuteAsync(gameId, new Resign(PlayerId), CancellationToken);

        result.ShouldBeOfType<GameNotFound>();
    }

    private Task<StoredGame?> Save(StoredGame read, GameCommand command)
    {
        var events = _decider.Decide(read.Game, command).ShouldBeOfType<Accepted>().Events;
        return _store.SaveAsync(read, _decider.Apply(read.Game, events), events, CancellationToken);
    }
}
