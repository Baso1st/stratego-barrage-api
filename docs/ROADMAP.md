# Roadmap

The plan from an empty repository to the API running in production, phase by phase. Tick each item when it's done: this is the only place progress is tracked. How the API is built is in [ARCHITECTURE.md](ARCHITECTURE.md), and the rules it enforces are in [RULES.md](RULES.md).

- **Items marked "You"** need the repository owner's admin rights on GitHub, Azure, Entra or Google Cloud. Everything else is done in this repository.
- **Each phase ships something usable** and ends with a release to production.

## Phase 1: Foundations

The solution builds and tests in CI, both environments exist, and every merge and release deploys itself. The apps run on test and production, doing nothing yet but answering health checks.

It lands in two PRs: first the infrastructure and the pipeline that deploys it, so test exists and the deployment path is proven before there's any code; then the solution, and the pipeline steps that build and deploy it.

### GitHub

- [x] **You:** Allow only squash merges, and delete branches automatically after merging.
- [x] **You:** Protect `main` with a ruleset: changes only through pull requests, linear history, and no force pushes or deletion.
- [x] **You:** Protect release tags with a ruleset: `v*` tags can't be moved or deleted.
- [x] **You:** Create the `test` and `production` environments. `test` only accepts `main`; `production` only accepts `v*` tags, and waits for your approval before each deployment.

### Azure

- [x] Bootstrap script in `infra/bootstrap/`: a resource group per environment, and an identity per environment that GitHub Actions signs in as with OIDC, trusted only from its own GitHub environment and given roles only on its own resource group.
- [x] **You:** Run the bootstrap once, signed in as an owner of the subscription.
- [x] **You:** Add what the bootstrap prints to GitHub as variables: the tenant and subscription IDs for the repository, each identity's client ID for its environment, and the email address that alerts go to.

### Infrastructure

`infra/main.bicep`, with a parameter file per environment:

- [x] App Service Plan: B1 on test, P0v3 on production.
- [x] API Web App with WebSockets, Always On and its health check, plus a staging slot on production.
- [x] Worker and bot Function Apps on the same plan, with Always On, and their storage account.
- [x] Cosmos DB: serverless on test; free tier with 1,000 RU/s on the database on production. The four containers, with their partition keys, and `state` and `data` left out of the index.
- [x] Service Bus, Basic tier, with the `bot-turns` and `game-timers` queues.
- [x] Key Vault, and Application Insights with a daily cap.
- [x] A managed identity for each app, with Azure RBAC roles on Cosmos DB, Service Bus, Storage and Key Vault, and no keys or connection strings.
- [x] Alerts by email: the availability target's error budget, and 80% of capacity (Cosmos DB's normalized RU consumption, and the plan's CPU and memory).

### Pipeline for the infrastructure

- [x] CI on every PR, as one `build` check: compile and lint the Bicep.
- [x] Deploy to test on every merge to `main`: sign in to Azure with OIDC, and apply the Bicep.
- [x] **You:** Make CI a required check in the `main` ruleset, with the branch up to date before merging. GitHub only offers a check once it has run.
- [x] **You:** Squash-merge the first PR. Its deployment creates test's resources.

### Solution

- [x] The .NET 10 solution: the API, the worker, the bot, the games module, the Aspire app host, shared service defaults, and the test projects.
- [x] Formatting rules in `.editorconfig`, shared build settings, central package versions, and the SDK version in `global.json`.
- [x] Service defaults for all three apps: OpenTelemetry to Application Insights, with sampling, and health checks.
- [x] The API: its health endpoint, Problem Details for errors, and the OpenAPI document generated at build time.
- [x] The worker and the bot: Function Apps (isolated worker model) that start and send telemetry.
- [x] The Aspire app host: the three apps, with the Cosmos DB and Service Bus emulators.

### Pipeline for the apps

- [x] CI also builds the solution, checks formatting, runs the tests, and checks the OpenAPI document for breaking changes against the last release.
- [x] Deploy to test also publishes the three apps, deploys them, and checks the API's health endpoint.
- [x] Release to production on a published `v*` release: check the tag is on `main`, rebuild that commit, apply the Bicep, deploy the API to the staging slot, warm it up and swap it in, and deploy the Function Apps in place.
- [x] Dependabot (`.github/dependabot.yml`): weekly PRs for NuGet packages, the .NET SDK and GitHub Actions, only for releases at least 2 weeks old.

### Tests

- [x] Unit and integration test projects, run by CI.
- [x] Integration test: the API's health endpoint, through the Aspire app host.

### Release

- [x] The health endpoint answers on test, and telemetry from all three apps reaches Application Insights.
- [ ] **You:** Publish release `v0.1.0` and approve its deployment.
- [ ] The health endpoint answers on production, and a swap back to the previous version works.

## Phase 2: Registration and profiles

Players sign in with Google, and the API registers them and returns their profile.

### Sign-in

- [ ] **You:** Create a Microsoft Entra External ID tenant for each environment.
- [ ] **You:** Create a Google OAuth client for each environment in Google Cloud, and add Google to its tenant as the only sign-in option, with a sign-up and sign-in user flow.
- [ ] **You:** In each tenant, register the API with the scope players' access tokens carry, and a client registration that uses the authorization code flow with PKCE. The exact settings will be written down for you.
- [ ] Tokens carry the player's name and picture from Google.
- [ ] Check whether sign-in can go straight to Google, without a page of External ID's own, and record the outcome in ARCHITECTURE.md section 3.8.

### Infrastructure

- [ ] The API's settings for each environment: its tenant's issuer and the API's audience, and the client origins CORS allows.

### Development

- [ ] JWT bearer authentication for players' tokens. Every endpoint needs a signed-in player, apart from health checks.
- [ ] A development token issuer, accepted only locally and in tests.
- [ ] Player ids: the `logins` container maps a token's issuer and subject to a `playerId`.
- [ ] `GET /v1/me`: registers the player on their first call, with their name and picture, and returns their profile and stats.
- [ ] Rate limits per player and per IP.

### Tests

- [ ] The Cosmos DB emulator runs in CI.
- [ ] `/v1/me`: `401` without a valid token, registration on the first call, the same player on every call after it, and a different player for a different issuer or subject.

### Release

- [ ] Sign in with Google on test from an API client, such as Postman, and call `/v1/me`.
- [ ] **You:** Publish release `v0.2.0` and approve its deployment.
- [ ] The same on production.

## Phase 3: Games against the computer

A player plays a whole game against the bot on the server, with the time limits, leaving, resigning, and stats.

### Engine

- [ ] Board, pieces and setup: 8 pieces anywhere in a player's own four rows, one per square.
- [ ] Moves: one square at a time, Scouts any distance in a straight line and attacking in the same turn, no lakes, and no more than 3 times back and forth.
- [ ] Combat: the higher rank wins, equal ranks both go, the Spy beats the Marshal when it attacks, a Miner removes a Bomb, capturing the Flag wins, and survivors stay revealed.
- [ ] Endings: the Flag captured, no legal move, a draw when neither side can move, resigning, leaving, and running out of time.
- [ ] Each player's `PlayerView`: hidden ranks, which of the opponent's pieces have moved, captured pieces, everything once the game is over, and the player's legal moves.
- [ ] Events: complete when stored, and filtered for each player when sent.
- [ ] Table-driven tests: a case for each rule in RULES.md.

### Games and the API

- [ ] The games module's storage: the game, its events and its version in the game's partition, one point read and one transactional batch per command, guarded by the ETag, and a `schemaVersion` on every document.
- [ ] Commands: start a game against the computer, place a setup, move, resign, leave, and time out.
- [ ] Endpoints: `POST /v1/games` (`vs-bot`), `GET /v1/games/{id}`, `GET /v1/games/{id}/events`, `PUT /v1/games/{id}/setup`, `POST /v1/games/{id}/moves`, `POST /v1/games/{id}/resign` and `GET /v1/me/games`.
- [ ] Every view carries the version, the turn deadline and `serverTime`.
- [ ] `409` with the current view when `expectedVersion` is stale, and `422` saying why when the rules don't allow a move.

### Real time

- [ ] The SignalR hub at `/hubs/games`, requiring the access token: `EnterGame`, `GameUpdated` filtered for each player, and `OpponentConnection`.
- [ ] Presence: a presence item per player, and a `leave-check` scheduled on `game-timers` when a player's last connection closes.
- [ ] Azure SignalR Service, used whenever its connection setting is present.

### Worker and bot

- [ ] A script that registers the API in the subscription's tenant for each environment, with the `Bot` and `Worker` app roles, and assigns them to the bot's and the worker's managed identities.
- [ ] **You:** Run that script once.
- [ ] The API accepts the Function Apps' tokens: `Bot` on the game endpoints as its bot player, and `Worker` only on the `/internal` endpoints.
- [ ] The worker's change feed trigger, checking every 500 ms: it queues bot turns, schedules turn deadlines, and updates stats and finished games when a game ends, each keyed by game and version.
- [ ] The worker's `game-timers` trigger, passing due timers to `/internal/games/{id}/turn-expired` and `/internal/games/{id}/leave-check`.
- [ ] The bot's `bot-turns` trigger: it fetches its own view, places its setup or chooses a move by rules of thumb, and submits it.

### Observability

- [ ] Game metrics: games started and finished, how they ended, move latency, bot thinking time, connected players, and rejected moves.
- [ ] Measurements for pushes, the bot's reply, timers and stats, and alerts for every target in ARCHITECTURE.md section 8.

### Tests

- [ ] Hidden information: no view, event or push ever shows a rank the player isn't allowed to see.
- [ ] A whole game against the bot through the API, the hub, the worker and the bot, against the emulators.
- [ ] Races and retries: a move racing its turn running out, and a retried move, each get `409`.
- [ ] Time limits and leaving, with the limits shortened in tests.
- [ ] Stats: every ending counts once, even when the worker retries.
- [ ] Budgets: each command's request units, a move at most 30 RU, and the bot at most 250 ms a move. First check that the emulator reports request charges.

### Release

- [ ] Play whole games against the bot on test, including resigning, leaving and running out of time.
- [ ] **You:** Publish release `v0.3.0` and approve its deployment.
- [ ] The same on production.

## Phase 4: Games against a friend

A player invites a friend with a link, and they play a whole game against each other.

### Development

- [ ] `POST /v1/games` (`vs-person`): starts a waiting game, and returns the invite link: the client address with the game id and a random 128-bit code.
- [ ] `POST /v1/games/{id}/join`: joins with the code and draws sides at random. A used link, or the creator's own game, is refused.
- [ ] `DELETE /v1/games/{id}`: the creator cancels while it waits. The creator leaving while it waits cancels it too.
- [ ] A game where neither player sets up in time is cancelled.
- [ ] Stats against people, kept apart from stats against the computer.

### Infrastructure

- [ ] The client address for each environment, used in invite links.

### Tests

- [ ] Inviting, joining, a used link, joining your own game, cancelling, and leaving while waiting.
- [ ] A whole game between two players through the API and the hub.
- [ ] Cancelled games don't count in anyone's stats.

### Release

- [ ] Play whole games between two players on test.
- [ ] **You:** Publish release `v1.0.0` and approve its deployment.
- [ ] The same on production.
