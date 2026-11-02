using System.Text.Json;
using System.Text.Json.Nodes;

namespace StrategoBarrage.Games.Tests;

// The documents' JSON, as stored in Cosmos DB and described in the data model.
public class GameJsonTests
{
    private const string GameId = "0192f3c4-0000-7000-8000-000000000001";
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly GameDecider _decider =
        new(new StandInRules(), new GameOptions(), TimeProvider.System);

    [Fact]
    public void TheGameIsStoredWithTheDataModelsNamesAndValues()
    {
        var json = ToJson(Game());

        json["id"]!.GetValue<string>().ShouldBe(GameId);
        json["gameId"]!.GetValue<string>().ShouldBe(GameId);
        json["type"]!.GetValue<string>().ShouldBe("game");
        json["mode"]!.GetValue<string>().ShouldBe("vs-bot");
        json["status"]!.GetValue<string>().ShouldBe("playing");
        json["players"]!["red"]!["kind"]!.GetValue<string>().ShouldBe("human");
        json["players"]!["blue"]!["playerId"]!.GetValue<string>().ShouldBe("bot/rules-of-thumb");
        json["version"]!.GetValue<int>().ShouldBe(1);
        json["state"]!["toMove"]!.GetValue<string>().ShouldBe("red");
        json["schemaVersion"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void AGameReadsBackAsItWasWritten()
    {
        var game = Game();
        var text = JsonSerializer.Serialize(game, GameJson.Options);

        var read = JsonSerializer.Deserialize<Game>(text, GameJson.Options).ShouldNotBeNull();

        JsonSerializer.Serialize(read, GameJson.Options).ShouldBe(text);
    }

    [Fact]
    public void AnEventIsStoredWithItsKindAndData()
    {
        var json = ToJson(Move());

        json["id"]!.GetValue<string>().ShouldBe($"{GameId}:000002");
        json["type"]!.GetValue<string>().ShouldBe("event");
        json["kind"]!.GetValue<string>().ShouldBe("move-made");
        json["seq"]!.GetValue<int>().ShouldBe(2);
        json["by"]!.GetValue<string>().ShouldBe("red");
        json["data"]!["to"]!["row"]!.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public void AnEventReadsBackAsItsOwnKindWhereverItsKindIs()
    {
        var json = ToJson(Move());
        var kind = json["kind"]!.DeepClone();
        json.Remove("kind");
        json["kind"] = kind;

        var read = json.Deserialize<GameEvent>(GameJson.Options).ShouldBeOfType<MoveMade>();

        read.ShouldBe(Move());
    }

    private Game Game() => _decider.Apply(null, new GameStarted
    {
        GameId = GameId,
        Seq = 1,
        At = Now,
        Data = new GameStartedData(
            GameMode.VsBot,
            new GamePlayers(new GamePlayer("player-1", PlayerKind.Human), GamePlayer.Bot)),
    });

    // As the base type, as the store writes it: serialized as MoveMade, it would have no "kind".
    private static GameEvent Move() => new MoveMade
    {
        GameId = GameId,
        Seq = 2,
        By = Side.Red,
        At = Now,
        Data = new Move(new Square(6, 1), new Square(5, 1)),
    };

    private static JsonObject ToJson<T>(T document) =>
        JsonSerializer.SerializeToNode(document, GameJson.Options)!.AsObject();
}
