# Temporary plan: the game write path, the change feed and timers

A slice taken ahead of the roadmap, to build the Cosmos DB, change feed and Service Bus parts first. The roadmap and the other docs stay as they are while this runs. When the slice is done, we compare it with them, tick what it finished, add what it changed, and delete this file.

**The loop it builds:** a player starts a game against the computer and moves. The move is a point read and one transactional batch in the game's partition, guarded by the ETag. The worker's change feed trigger sees the move, queues a `bot-turn`, and schedules the turn's `turn-expired` timer. The bot replies through the API. A turn that runs out reaches the API through the worker's `game-timers` trigger and ends the game, and the change feed updates both players' stats.

**It runs locally and in CI,** against the emulators, through the Aspire app host. Deploying it waits for the real sign-in and the Function Apps' app roles.

## Stand-ins

These keep the slice small. Each is replaced by its roadmap item later.

| Stand-in | Replaced by |
| --- | --- |
| **Stand-in rules:** no setup. The board is a placeholder, any move onto an empty square is legal, and turns alternate. A game ends only by resigning or running out of time. | The engine (phase 3) |
| **The player id is the development token's subject.** | Player ids from the `logins` container (phase 2) |
| **The worker and the bot get development tokens** with the `Worker` and `Bot` roles. | Managed identity tokens with app roles (phase 3) |
| **A profile is created by the worker** if it doesn't exist yet when a game ends. | `GET /v1/me` (phase 2) |

## Steps

### Games and storage

- [x] The game document, its events and the version in the `games` container, with a `schemaVersion` on each, and stand-in rules behind the seam the engine plugs into.
- [x] Storage: one point read and one transactional batch per command. The state is replaced if its ETag still matches, and each event is created. A lost race is `409`.
- [x] Commands: start a game against the computer, move, resign, and time out.

### The API

- [x] The development token issuer, and JWT bearer authentication on every endpoint apart from health checks.
- [x] `POST /v1/games` (`vs-bot`), `GET /v1/games/{id}`, `POST /v1/games/{id}/moves` and `POST /v1/games/{id}/resign`. Every view carries the version, the turn deadline and `serverTime`.
- [x] `409` with the current view when `expectedVersion` is stale, `422` for a move the rules don't allow, and `403` for a game you're not in.
- [x] `POST /internal/games/{id}/turn-expired`, only for the `Worker` role: the player to move loses, if the game is still at that version.

### The worker

- [x] The change feed trigger on `games`, checking every 500 ms, with its position in `leases`.
- [x] On the bot's turn, it queues a `bot-turn`.
- [x] For each turn, it schedules a `turn-expired` message on `game-timers` for the turn's deadline.
- [x] When a game ends, it updates both players' stats and finished games in one batch per player, keyed by game, so a retry never counts a game twice.
- [x] The `game-timers` trigger, passing each due timer to `/internal/games/{id}/turn-expired`. A timer for a version that has passed changes nothing.

### The bot

- [x] The `bot-turns` trigger: it fetches its own view, makes the first legal move, and submits it. A message for a version that has passed changes nothing.

### Tests and CI

- [x] Unit tests: the stand-in rules, and each command's events.
- [ ] Integration tests through the app host, against the emulators: a game against the bot, a stale `expectedVersion`, a move racing its turn running out, a retried timer and bot turn, and stats counted once when a turn runs out. The turn limit is shortened in tests.
- [x] The emulators run in CI.
