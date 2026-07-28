# CLAUDE.md

Working notes for Claude when assisting on this repo. Keep this updated as the project evolves — see the "Update this file" note at the bottom.

## What this is

A Bitly (URL shortener) clone built as a step-by-step learning project for .NET 10 and system design, following
the HelloInterview Bitly breakdown: https://www.hellointerview.com/learn/system-design/problem-breakdowns/bitly

Full requirements/data model/API summary lives in [README.md](README.md) — don't duplicate it here, link to it.

## Ground rules for this project

- **Local-only, zero-cost.** No cloud services, no paid tiers. Docker via **Rancher Desktop** (not Docker Desktop).
- **.NET 10.**
- **Step-by-step process.** Before making any change, present a short plan/summary in chat and wait for
  confirmation before touching files or running mutating commands. Keep each step scoped to roughly one concept.
- After each step: commit, push, then give a deep-dive explanation of what changed and why.
- **Controllers, not Minimal APIs** — user is explicitly learning the Controller-based ASP.NET Core style.
- **No test project for now** — user opted out of tests at this stage of the learning project.
- **Git identity:** this repo's local `user.name`/`user.email` is set to `Gabriel Badarau <badaraugabriel95@gmail.com>`
  (repo-local config, not global — the machine's global git email is a separate work identity). Never touch global
  git config for this repo's commits.

## Repo structure

```
src/
  Bitly.Api/       ASP.NET Core Web API, Controllers (not Minimal APIs)
  Bitly.Domain/    Domain entities/logic, no external dependencies
Bitly.slnx         Solution file (new .slnx format, .NET 9+)
```

## Commands

```bash
dotnet build
dotnet run --project src/Bitly.Api     # serves on the port in src/Bitly.Api/Properties/launchSettings.json
```

Health check: `GET /health` → `{"status":"healthy"}` (`src/Bitly.Api/Controllers/HealthController.cs`)

## Step plan status

See the progress log in [README.md](README.md) for the authoritative checklist. Currently on: **Step 1 (scaffold) — done, pending commit/push.**

## Key decisions log

- **Controllers over Minimal APIs** (Step 1): user wants to learn the Controller-based style specifically.
- **`Bitly.Domain` split from `Bitly.Api` from day one** (Step 1): keeps entities framework-agnostic ahead of Step 2's EF Core work.
- **Postgres/Redis/Docker deferred to Step 2+** (Step 1): kept the first step to pure scaffolding so the first deep dive is small and focused.
- **`.slnx` solution format** (Step 1): this is what `dotnet new sln` produces by default on the .NET 10 SDK.

## Update this file

Update this file whenever a step changes: stack/tooling decisions, structure, commands, or the "currently on" line.
