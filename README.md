# Stratego Barrage API

The back end for **Stratego Barrage**, the fast, small-army variant of the Stratego board game: the standard 10×10 board, but only 8 pieces a side. Every game is played through the API. Players sign in with Google and play live games against the computer or against a friend, and the API keeps each player's stats.

## What it does

- **Runs every game.** The server holds the full game, enforces the rules and plays the computer opponent. Each player only ever sees what they're allowed to see, along with their legal moves.
- **Live play.** Clients send commands over REST, and updates are pushed to them over SignalR. Each move must be made within 10 minutes.
- **Friends play by invite link.** A player starts a game and sends the link, and the friend who opens it joins.
- **Players and profiles.** Sign-in is with Google, through Microsoft Entra External ID. Each player has a profile with their stats and finished games.

## Built with

- **.NET 10:** ASP.NET Core for the API, and Azure Functions (isolated worker model) for the worker and the bot
- **Cosmos DB for NoSQL** for games and players, **Azure Service Bus** for bot turns and timers, and **SignalR** for live updates
- **Azure App Service** for hosting, with the infrastructure in **Bicep**
- **GitHub Actions** for CI and deployments, signing in to Azure with OIDC
- **Aspire** for local development

## Documentation

| Doc | What's in it |
| --- | --- |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the API is built: hosting, data model, endpoints, delivery, cost and capacity |
| [docs/RULES.md](docs/RULES.md) | The rules of Barrage as the API enforces them |
| [docs/ROADMAP.md](docs/ROADMAP.md) | The plan, phase by phase, and how far along it is |

## Local development

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), at the version in `global.json` or a later patch of it, and [Docker Desktop](https://www.docker.com/products/docker-desktop/).

An Aspire app host starts the API, the worker and the bot, with emulators for Cosmos DB and Service Bus running in Docker. Its dashboard shows the traces and logs from all three apps in one place. Locally, the API accepts tokens from a development issuer, so the tests and a client running locally work without the real sign-in.

## Delivery

- **Trunk-based.** `main` is protected, and every change reaches it through a pull request. CI builds it, checks formatting, runs the tests and checks the OpenAPI contract for breaking changes, and the PR is squash-merged once CI passes.
- **Test on merge.** Every merge to `main` applies the infrastructure and deploys the three apps to the test environment.
- **Production on release.** Publishing a GitHub Release with a `v*` tag on `main` rebuilds that commit and deploys it to production, after approval.
