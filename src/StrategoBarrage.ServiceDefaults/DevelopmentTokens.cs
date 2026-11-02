using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace StrategoBarrage.ServiceDefaults;

// Access tokens for local runs and tests, signed with a key the app host makes up on each run. The API accepts
// them only in development. A token is an ordinary JWT signed with HMAC-SHA256, the same shape as a real one:
// the player is its subject, and the worker and the bot carry their app role in "roles".
public static class DevelopmentTokens
{
    public const string Issuer = "stratego-barrage-development";
    public const string Audience = "stratego-barrage-api";

    // The setting that holds the base64 signing key, in every app that signs or checks these tokens.
    public const string SigningKeySetting = "DevelopmentTokens:SigningKey";

    private static readonly TimeSpan AppTokenLifetime = TimeSpan.FromMinutes(5);

    public static string Create(byte[] signingKey, string subject, string? role, DateTimeOffset now, TimeSpan lifetime)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = subject,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(lifetime).ToUnixTimeSeconds(),
        };
        if (role is not null)
        {
            claims["roles"] = new[] { role };
        }

        var header = new Dictionary<string, string> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var unsigned = $"{Encode(header)}.{Encode(claims)}";
        var signature = HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(unsigned));
        return $"{unsigned}.{Base64Url.EncodeToString(signature)}";
    }

    // A short-lived token for the worker or the bot, signed with the key in configuration. For now they sign in to
    // the API this way, so they only call an API running locally.
    public static string CreateForApp(IConfiguration configuration, string subject, string role, DateTimeOffset now)
    {
        var key = configuration[SigningKeySetting]
            ?? throw new InvalidOperationException($"There's no {SigningKeySetting} to sign in to the API with.");
        return Create(Convert.FromBase64String(key), subject, role, now, AppTokenLifetime);
    }

    private static string Encode<T>(T value) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));
}
