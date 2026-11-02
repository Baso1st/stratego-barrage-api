using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Tokens;
using StrategoBarrage.Games;
using StrategoBarrage.ServiceDefaults;

namespace StrategoBarrage.Api;

// The app roles the Function Apps' tokens carry.
public static class Roles
{
    public const string Bot = "Bot";
    public const string Worker = "Worker";
}

public static class Policies
{
    // Players and the bot, on the game endpoints. The worker only gets the internal ones.
    public const string Player = nameof(Player);

    public const string Worker = nameof(Worker);
}

public static class Authentication
{
    private static readonly TimeSpan DevelopmentTokenLifetime = TimeSpan.FromHours(12);

    // Every endpoint needs a signed-in caller unless it allows anonymous calls, as health checks do. Only the
    // development issuer is trusted so far, and only in development.
    public static void AddAccessTokens(this WebApplicationBuilder builder)
    {
        var signingKey = DevelopmentSigningKey(builder);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claims as the token names them ("sub", "roles"), rather than .NET's long names.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.RoleClaimType = "roles";
                if (signingKey is not null)
                {
                    options.TokenValidationParameters.ValidIssuer = DevelopmentTokens.Issuer;
                    options.TokenValidationParameters.ValidAudience = DevelopmentTokens.Audience;
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(signingKey);
                }
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Player, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => !context.User.IsInRole(Roles.Worker)))
            .AddPolicy(Policies.Worker, policy => policy.RequireRole(Roles.Worker));
    }

    // The player a call acts for. For now a player's id is their token's subject; a Bot token plays as the bot.
    public static string PlayerId(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Bot) ? GamePlayer.Bot.PlayerId : user.FindFirstValue("sub")!;

    // Signs a token for any subject and role, so a client running locally can call the API.
    public static void MapDevelopmentTokens(this WebApplication app)
    {
        var signingKey = DevelopmentSigningKey(app);
        if (signingKey is null)
        {
            return;
        }

        app.MapPost("/development/tokens", Ok<DevelopmentToken> (DevelopmentTokenRequest request, TimeProvider time) =>
        {
            var now = time.GetUtcNow();
            var token = DevelopmentTokens.Create(
                signingKey, request.Subject, request.Role, now, DevelopmentTokenLifetime);
            return TypedResults.Ok(new DevelopmentToken(token, now.Add(DevelopmentTokenLifetime)));
        })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static byte[]? DevelopmentSigningKey(IHostApplicationBuilder builder) =>
        DevelopmentSigningKey(builder.Environment, builder.Configuration);

    private static byte[]? DevelopmentSigningKey(WebApplication app) =>
        DevelopmentSigningKey(app.Environment, app.Configuration);

    private static byte[]? DevelopmentSigningKey(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration[DevelopmentTokens.SigningKeySetting] is { Length: > 0 } key
            ? Convert.FromBase64String(key)
            : null;
}

public sealed record DevelopmentTokenRequest(string Subject, string? Role = null);

public sealed record DevelopmentToken(string AccessToken, DateTimeOffset ExpiresAt);
