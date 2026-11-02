using System.Text.Json.Serialization;

namespace StrategoBarrage.Games;

[JsonConverter(typeof(JsonStringEnumConverter<Side>))]
public enum Side
{
    [JsonStringEnumMemberName("red")]
    Red,

    [JsonStringEnumMemberName("blue")]
    Blue,
}

public static class SideExtensions
{
    extension(Side side)
    {
        public Side Opponent => side == Side.Red ? Side.Blue : Side.Red;
    }
}
