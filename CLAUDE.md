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
  Bitly.Api/
    Controllers/       API endpoints (UrlsController: POST /urls, GET /{code})
    Models/            Entities (ShortUrl)
    Contracts/         Request/response DTOs (records)
    Data/              BitlyDbContext + Migrations/
.config/
  dotnet-tools.json     Local tool manifest (dotnet-ef, pinned version)
docker-compose.yml       Local PostgreSQL (postgres:16-alpine)
Bitly.slnx               Solution file (new .slnx format, .NET 9+)
```

No separate domain project right now — see Key decisions log for why, and when to reintroduce one.

## Commands

```bash
docker compose up -d                                    # start Postgres
dotnet tool restore                                      # restore local tools (dotnet-ef), once per clone
dotnet build
dotnet run --project src/Bitly.Api                        # serves on the port in launchSettings.json

# EF Core migrations (run from src/Bitly.Api, or add --project src/Bitly.Api)
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef database update
```

Health check: `GET /health` → plain-text `Healthy` (built-in ASP.NET Core Health Checks middleware, `Program.cs`)

Connection string lives in **user-secrets**, not `appsettings.json`:
`dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.Api`
(the `bitly_dev_only` password is only ever used inside the local Docker network — fine to keep in this file and in `docker-compose.yml`, but the connection string itself still stays out of the committed `appsettings.json`/git history as a matter of habit.)

Quick manual test of the end-to-end flow:

```bash
curl -X POST http://localhost:5299/urls -H "Content-Type: application/json" \
  -d '{"longUrl": "https://example.com"}'
# => 201 { "shortUrl": "http://localhost:5299/<code>" }

curl -v http://localhost:5299/<code>
# => 302 Found, Location: https://example.com
```

## Step plan status

- [x] **Step 1** — Repo scaffold: solution, `Bitly.Api` (Web API, controllers), `Bitly.Domain` (class library), `/health` endpoint (built-in Health Checks middleware)
- [x] **Step 2** — Data model + PostgreSQL via Docker Compose + EF Core migrations
- [x] **Step 3** — Naive end-to-end create/redirect flow
- [ ] **Step 4** — Deep dive: uniqueness (Redis counter + base62)
- [ ] **Step 5** — Deep dive: fast redirects (Redis cache-aside)
- [ ] **Step 6** — Deep dive: scale (Read/Write service split + local load balancer)
- [ ] **Step 7** — Round out NFRs (expiration cleanup, alias collisions, rate limiting, logging)
- [ ] **Step 8** — Full Docker Compose stack + polish

Currently on: **Step 3 — done.**

**Known limitations carried forward on purpose** (naive step; later steps address these):
- No collision handling on create. A duplicate `Code`/`CustomAlias` currently throws an unhandled
  `DbUpdateException` → raw `500` with a full stack trace leaked to the client (verified live — this is real,
  not hypothetical). Proper handling (e.g. `409 Conflict` on alias collision, retry-with-new-code on the rare
  random-code collision) belongs in Step 7's NFR pass; generic exception → problem-details mapping should land
  there too, not just alias-specific handling.
- `GET /{code}` is a root-level catch-all route — a short code equal to `health` or `urls` would collide with
  existing routes. Not addressed yet (naive random codes make this astronomically unlikely at this scale, but
  it is a real gap, not a hypothetical one).

## Key decisions log

- **Controllers over Minimal APIs** (Step 1): user wants to learn the Controller-based style specifically.
- **No separate `Bitly.Domain` project — collapsed back into `Bitly.Api`** (Step 1 cleanup, reversing an earlier
  decision): originally split out on day one on the theory that framework-agnostic entities would pay off once
  Step 6 splits the app into separate Read/Write services sharing domain logic. Reversed because that project was
  sitting empty with a single consumer and no entities yet — premature structure for a need that doesn't exist
  yet. Decided instead to advance in steps like a real project would: keep entities in `Bitly.Api` for now, and
  split out a `Bitly.Domain` project if/when Step 6 actually needs a second consumer. That refactor (move files,
  add a project reference) is mechanical and cheap to do later, so there's no cost to waiting.
- **Postgres/Redis/Docker deferred to Step 2+** (Step 1): kept the first step to pure scaffolding so the first deep dive is small and focused.
- **`.slnx` solution format** (Step 1): this is what `dotnet new sln` produces by default on the .NET 10 SDK.
- **Built-in Health Checks middleware instead of a hand-rolled controller** (Step 1 cleanup): `/health` was
  originally a `HealthController` returning a manual JSON object. Swapped to `builder.Services.AddHealthChecks()`
  + `app.MapHealthChecks("/health")` — ASP.NET Core's built-in liveness mechanism, no extra package needed. Returns
  plain-text `Healthy` by default (not JSON). This matters later: once Postgres/Redis exist, we register a check
  per dependency (`AddNpgsql()`, etc.) so `/health` reflects real dependency health, not just "process is up" —
  a controller can't do that without reimplementing the middleware. Controllers remain the pattern for domain
  resources (`POST /urls`, `GET /{code}`); Health Checks is a framework cross-cutting concern with its own idiom.
- **`User` entity skipped** (Step 2): the reference spec's data model includes a `User` (creator), but auth is
  explicitly out of scope in the functional requirements. Modeled only `ShortUrl` for now; adding `User` later
  (if auth ever gets added) is a small, additive change — no need to build it speculatively.
- **`dotnet-ef` as a local tool, not a global install** (Step 2): `dotnet new tool-manifest` + `dotnet tool install
  dotnet-ef` pins the exact version in `.config/dotnet-tools.json` (committed), similar to a `devDependency` in a
  `package.json` vs. a global `npm install -g`. Anyone cloning the repo runs `dotnet tool restore` once. Note:
  `dotnet new tool-manifest` created the file at the repo root by default — moved it to the conventional
  `.config/dotnet-tools.json` path by hand so `dotnet tool restore` finds it automatically.
- **Connection string via `dotnet user-secrets`, not `appsettings.json`** (Step 2): keeps it out of the committed
  files/git history as a matter of habit, even though this Postgres instance is fully local. Equivalent to a
  gitignored `.env.local` in a Node project — secrets live outside the repo (`~/.microsoft/usersecrets/<id>/`),
  referenced only by the `<UserSecretsId>` GUID in the `.csproj` (that GUID itself is not sensitive).
- **`postgres:16-alpine` via `docker-compose.yml`, named volume for persistence** (Step 2): single service for
  now; Redis gets its own service when Step 4's uniqueness deep dive needs it, kept out until then.
- **`CustomAlias` collapsed into `Code` rather than a separate lookup field** (Step 3): if the caller supplies a
  custom alias, it becomes the row's `Code` directly (and is also kept in `CustomAlias` as a record that it was
  user-chosen). `GET /{code}` only ever does one lookup (`WHERE Code = @code`) instead of `Code OR CustomAlias`.
- **Naive random code generation now, counter-based generation in Step 4** (Step 3): `RandomNumberGenerator
  .GetString(base62Alphabet, 7)` per create call, no collision handling. This is intentionally the "bad" starting
  point the reference article describes — Step 4 replaces the generation strategy itself (Redis counter +
  base62, which cannot collide by construction) rather than bolting retry logic onto random generation.
- **DB-level unique index on `Code` added now, separately from the generation strategy** (Step 3, new migration
  `AddUniqueIndexOnShortUrlCode`): uniqueness *enforcement* at the data layer is a baseline integrity concern
  independent of *how* codes get generated — added regardless of which generation strategy is active. What
  currently has no handling is the application reacting gracefully when that constraint is violated (see Known
  limitations above).
- **`410 Gone` for expired short URLs, `404` for unknown ones** (Step 3): more precise than collapsing both to
  `404` — `410` communicates "this existed and is intentionally gone," which matters once expiration cleanup
  (Step 7) is a real feature, not just "we don't have a row."
- **JSON is camelCase, not the spec's snake_case** (Step 3): ASP.NET Core's default `System.Text.Json` naming
  policy for controllers is camelCase (`shortUrl`, `longUrl`). Not overridden — camelCase is .NET's own idiomatic
  default, and there's no reason to fight the framework to match the reference article's snake_case exactly.

## Update this file

Update this file whenever a step changes: stack/tooling decisions, structure, commands, or the "currently on" line.
