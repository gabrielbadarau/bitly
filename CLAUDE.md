# CLAUDE.md

Working notes for Claude when assisting on this repo. Keep this updated as the project evolves — see the "Update this file" note at the bottom.

## What this is

A Bitly (URL shortener) clone built as a step-by-step learning project for .NET 10 and system design, following
the HelloInterview Bitly breakdown: https://www.hellointerview.com/learn/system-design/problem-breakdowns/bitly

Full requirements/data model/API summary lives in [README.md](README.md) — don't duplicate it here, link to it.

User is a JS/TS/React/Node developer, new to .NET/C#. Lean on analogies to that ecosystem when explaining
.NET-specific concepts (e.g. NuGet ~ npm, middleware pipeline ~ Express middleware, EF Core ~ an ORM like
Prisma/TypeORM). Don't assume prior exposure to static typing/compilation, C# value vs. reference semantics, or
the .NET dependency injection container.

## Learning mode ground rules

- **Explain before coding.** Walk through concepts and tradeoffs first — the point is the learning, not just the
  artifact.
- **Verify against the real running system before calling anything done.** Build it, run it, show the actual
  output — not "this should work."
- **Small chunks.** Implement one piece, verify it, then ask what's next. Don't plan the whole thing upfront and
  build it all in one pass.
- **Step-by-step process.** Before making any change, present a short plan/summary in chat and wait for
  confirmation before touching files or running mutating commands. Keep each step scoped to roughly one concept.
- After each step: commit, push, then give a deep-dive explanation of what changed and why.

## Ground rules for this project

- **Local-only, zero-cost.** No cloud services, no paid tiers. Docker via **Rancher Desktop** (not Docker Desktop).
- **.NET 10.**
- **Controllers, not Minimal APIs** — user is explicitly learning the Controller-based ASP.NET Core style.
  (Exception: framework-level cross-cutting concerns like Health Checks use their own idiomatic middleware,
  not a controller — see Key decisions log.)
- **No test project for now** — user opted out of tests at this stage of the learning project.
- **Git identity:** this repo's local `user.name`/`user.email` is set to `Gabriel Badarau <badaraugabriel95@gmail.com>`
  (repo-local config, not global — the machine's global git email is a separate work identity). Never touch global
  git config for this repo's commits.
- **Remote auth:** origin uses **HTTPS** (`https://github.com/gabrielbadarau/bitly.git`) authenticated via the
  `gh` CLI credential helper (`gh auth login` as `gabrielbadarau`, then `gh auth setup-git`) — not SSH. The
  machine's SSH keys are tied up with a separate work GitHub/GHE identity, so SSH push to this repo failed with
  `Permission denied (publickey)` until this was set up (2026-07-28).

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

- [x] **Step 1** — Repo scaffold: solution, `Bitly.Api` (Web API, controllers), `Bitly.Domain` (class library), `/health` endpoint (built-in Health Checks middleware)
- [ ] **Step 2** — Data model + PostgreSQL via Docker Compose + EF Core migrations
- [ ] **Step 3** — Naive end-to-end create/redirect flow
- [ ] **Step 4** — Deep dive: uniqueness (Redis counter + base62)
- [ ] **Step 5** — Deep dive: fast redirects (Redis cache-aside)
- [ ] **Step 6** — Deep dive: scale (Read/Write service split + local load balancer)
- [ ] **Step 7** — Round out NFRs (expiration cleanup, alias collisions, rate limiting, logging)
- [ ] **Step 8** — Full Docker Compose stack + polish

Currently on: **Step 1 — done.**

## Key decisions log

- **Controllers over Minimal APIs** (Step 1): user wants to learn the Controller-based style specifically.
- **`Bitly.Domain` split from `Bitly.Api` from day one** (Step 1): keeps entities framework-agnostic ahead of Step 2's EF Core work.
- **Postgres/Redis/Docker deferred to Step 2+** (Step 1): kept the first step to pure scaffolding so the first deep dive is small and focused.
- **`.slnx` solution format** (Step 1): this is what `dotnet new sln` produces by default on the .NET 10 SDK.
- **Built-in Health Checks middleware instead of a hand-rolled controller** (Step 1 cleanup): `/health` was
  originally a `HealthController` returning a manual JSON object. Swapped to `builder.Services.AddHealthChecks()`
  + `app.MapHealthChecks("/health")` — ASP.NET Core's built-in liveness mechanism, no extra package needed. Returns
  plain-text `Healthy` by default (not JSON). This matters later: once Postgres/Redis exist, we register a check
  per dependency (`AddNpgsql()`, etc.) so `/health` reflects real dependency health, not just "process is up" —
  a controller can't do that without reimplementing the middleware. Controllers remain the pattern for domain
  resources (`POST /urls`, `GET /{code}`); Health Checks is a framework cross-cutting concern with its own idiom.

## Update this file

Update this file whenever a step changes: stack/tooling decisions, structure, commands, or the "currently on" line.
