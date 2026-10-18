# CLAUDE.md

Instructions for Claude Code when working in this repository.

## Project

The back end for Stratego Barrage, in .NET 10 on Azure: an ASP.NET Core API, plus a worker and a bot as Azure Functions, with Cosmos DB and Service Bus. Players' clients play every game through it. See `README.md` for an overview.

## Rules

- **Never run `git add`, `git commit` or `git push`, and never create or switch branches, on your own.** The user works alongside you and handles git. When something needs one of these, say so and wait: the user does it, says no, or tells you to do it. Each phase is done on its own branch.
- **No Claude attribution, anywhere.** The user owns every change. Commit messages get no `Co-Authored-By` trailer, and PR descriptions get no "Generated with Claude Code" line.
- **Settle choices in chat before they go in the docs.** When a decision is open, lay out the options with their costs and a recommendation, and write it into the docs only once the user decides. If you fill a gap yourself, say so in chat so the user can veto it.
- **Check prices and limits before quoting them.** Use Microsoft Learn and the Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`, filtered to `armRegionName eq 'centralus'`). The Azure pricing pages load their prices with script, so they can't be read.

## Roadmap

`docs/ROADMAP.md` is the plan from an empty repository to production, and the only place progress is tracked. It's imported below so it loads at the start of every session.

- Work through it in order, one item at a time, and check it before starting new work to see where things stand.
- Tick an item off as soon as it's done, and change the plan when the work changes it.
- Items marked **You** need the user's admin rights on GitHub, Azure, Entra or Google Cloud. When one is next, tell the user exactly what to do, step by step, and wait for them to confirm it's done.

@docs/ROADMAP.md

## Docs

- `docs/ARCHITECTURE.md` is how the API is built. Read it before working on the API and build to match it. Update it whenever a design decision is made or changes.
- `docs/RULES.md` is the rules the API enforces. The engine implements exactly these, with a test for each.
- `README.md` is the overview for someone new to the repo.

How the docs are written:

- **The final spec, not a discussion.** They say exactly what we're building now, as settled decisions: no "proposed", no alternatives considered, no trade-off comparisons, no future plans ("later", "we'll check when…"). Trade-offs are discussed in chat; the docs record the outcome.
- **About the API only.** The API doesn't know about its clients. Say "clients", never a particular app, framework, screen, button or page.
- **Written fresh.** Nothing says code is ported, copied or carried over from another version ("existing", "today's", "already does").
- **Each fact in one place.** Don't restate a fact in another section, a summary or a decisions list, and don't add bullets that only point to another section. Cross-reference inline, like "(section 3.9)", only where a reader needs it.
- **No notes about how we work with Claude,** like who commits.
- **Plain style,** matching what's there: short bullets with a bold lead-in, tables for lists and comparisons, plain words, and "we" for the team.

## Context

- **Azure:** one Pay-As-You-Go subscription, with a resource group per environment. Its Cosmos DB free tier is unused and goes to production's account (confirmed 2026-10-18).
- **No load testing for now.** Don't add it back unless the user asks.
- **Tools:** Windows with PowerShell. The .NET 10 SDK and the Azure CLI are installed; the GitHub CLI (`gh`) isn't (as of 2026-10-17).
