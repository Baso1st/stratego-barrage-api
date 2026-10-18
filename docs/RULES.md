# Rules

The rules of Barrage as the API enforces them. The engine implements exactly these, its tests cover each one, and this document changes whenever a rule does. How the API is built is in [ARCHITECTURE.md](ARCHITECTURE.md).

## Board and pieces

The board is 10×10. Red's setup rows are 6–9 and Blue's are 0–3. The two lakes in the middle can't be entered or crossed.

```
     0 1 2 3 4 5 6 7 8 9
  0  . . . . . . . . . .  ┐
  1  . . . . . . . . . .  │ Blue's setup rows
  2  . . . . . . . . . .  │
  3  . . . . . . . . . .  ┘
  4  . . ~ ~ . . ~ ~ . .  lakes
  5  . . ~ ~ . . ~ ~ . .
  6  . . . . . . . . . .  ┐
  7  . . . . . . . . . .  │ Red's setup rows
  8  . . . . . . . . . .  │
  9  . . . . . . . . . .  ┘
```

Each side has 8 pieces:

| Piece | Rank | Count | Moves |
| --- | --- | --- | --- |
| Marshal | 10 | 1 | 1 square |
| General | 9 | 1 | 1 square |
| Miner | 3 | 1 | 1 square |
| Scout | 2 | 2 | Any distance in a straight line |
| Spy | S | 1 | 1 square |
| Bomb | B | 1 | Never moves |
| Flag | F | 1 | Never moves |

## Sides

- **Against the computer,** the player is Red and the computer is Blue.
- **Against a friend,** sides are drawn at random when the friend joins.
- **Red moves first.**

## Setup

- Each player places all 8 pieces anywhere in their own four setup rows, one piece per square.
- **Both players set up at the same time, in secret.** Neither sees the other's setup.
- **Each player has 10 minutes to set up.** A player who hasn't placed their pieces in time forfeits. If neither has, the game is cancelled.
- The game starts when both setups are placed.

## Moving

- Players alternate turns, and a turn is exactly one move.
- A piece moves one square forward, back, left or right. It can't move diagonally, into a lake, or onto one of its own pieces.
- **A Scout moves like a rook:** any number of empty squares in a straight line, without jumping over pieces or lakes. It can also attack in the same turn, by ending its move on an enemy piece in that line.
- **No endless back-and-forth:** a piece can't move back and forth between the same two squares more than 3 times in a row.
- **Each move must be made within 10 minutes** of the turn starting, or the player forfeits.

## Combat

Moving onto an enemy piece attacks it. Both ranks are revealed, and:

- **The higher rank wins,** and the loser is removed. If the ranks are equal, both are removed.
- **The Spy beats the Marshal,** but only when the Spy attacks. Anything that attacks the Spy wins.
- **Bombs:** a Miner that attacks a Bomb removes it. Anything else that attacks a Bomb is removed, and the Bomb stays.
- **The Flag:** attacking the Flag captures it and wins the game.

A piece that survives a fight **stays revealed** for the rest of the game, for both players.

## What each player can see

- **Their own pieces,** with their ranks.
- **The opponent's pieces** where they stand, but not their ranks, until a fight reveals them.
- **Which of the opponent's pieces have moved.** A piece that has moved can't be a Bomb or the Flag.
- **Every captured piece,** with its rank.

When the game ends, both players see every piece.

## How a game ends

| Ending | Result | Reason |
| --- | --- | --- |
| A player captures the opponent's Flag | That player wins | `flag` |
| A player has no legal move on their turn | The opponent wins | `no-moves` |
| Neither side can ever move again | Draw | `draw` |
| A player resigns | The opponent wins | `resigned` |
| A player's last connection closes, and they don't reconnect within 1 minute | The opponent wins | `left` |
| A player doesn't move, or set up, within 10 minutes | The opponent wins | `timed-out` |

**Cancelled games:** a game against a friend is cancelled if the player who created it cancels, or leaves, before the friend joins. It's also cancelled if neither player sets up in time. A cancelled game has no result and doesn't count in anyone's stats.
