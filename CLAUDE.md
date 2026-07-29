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
    Services/          RedisCodeGenerator (Redis INCR + base62 encoding),
                       ShortUrlCache (cache-aside layer: GetAsync/SetAsync, TTL = ExpirationDate or 24h default)
.config/
  dotnet-tools.json     Local tool manifest (dotnet-ef, pinned version)
docker-compose.yml       Local PostgreSQL (postgres:16-alpine) + Redis (redis:7-alpine, AOF persistence)
Bitly.slnx               Solution file (new .slnx format, .NET 9+)
```

No separate domain project right now — see Key decisions log for why, and when to reintroduce one.

## Commands

```bash
docker compose up -d                                    # start Postgres + Redis
dotnet tool restore                                      # restore local tools (dotnet-ef), once per clone
dotnet build
dotnet run --project src/Bitly.Api                        # serves on the port in launchSettings.json

# EF Core migrations (run from src/Bitly.Api, or add --project src/Bitly.Api)
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef database update
```

Health check: `GET /health` → plain-text `Healthy` (built-in ASP.NET Core Health Checks middleware, `Program.cs`)

Connection strings live in **user-secrets**, not `appsettings.json`:
```bash
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.Api
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/Bitly.Api
```
(the `bitly_dev_only` password is only ever used inside the local Docker network — fine to keep in this file and in `docker-compose.yml`, but the connection strings themselves still stay out of the committed `appsettings.json`/git history as a matter of habit.)

Inspect the Redis counter directly: `docker exec bitly-redis redis-cli GET shorturl:counter`

Inspect a cached redirect directly: `docker exec bitly-redis redis-cli GET shorturl:code:<code>` and
`... TTL shorturl:code:<code>` (seconds until Redis auto-evicts it)

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
- [x] **Step 4** — Deep dive: uniqueness (Redis counter + base62)
- [x] **Step 5** — Deep dive: fast redirects (Redis cache-aside)
- [ ] **Step 6** — Deep dive: scale (Read/Write service split + local load balancer)
- [ ] **Step 7** — Round out NFRs (expiration cleanup, alias collisions, rate limiting, logging)
- [ ] **Step 8** — Full Docker Compose stack + polish

Currently on: **Step 5 — done.**

**Known limitations carried forward on purpose:**
- No collision handling on `CustomAlias`. A duplicate alias still throws an unhandled `DbUpdateException` → raw
  `500` with a full stack trace leaked to the client (verified live in Step 3, still true — the counter only
  fixed collisions on *generated* codes, not user-supplied aliases). Proper handling (`409 Conflict`, generic
  exception → problem-details mapping) belongs in Step 7's NFR pass.
- `GET /{code}` is a root-level catch-all route — a code/alias equal to `health` would silently shadow the
  health check and become permanently unreachable (verified live in the routing discussion before Step 4). Not
  addressed yet.
- **New in Step 4**: counter-generated codes are short and strictly sequential — `1, 2, 3, ...` — which means
  they are trivially predictable and enumerable. Anyone can walk `/1`, `/2`, `/3`... and discover every
  short URL ever created, including ones nobody advertised. The reference article explicitly calls this out as
  an accepted tradeoff of the counter approach, not something it solves. Left as-is for now; a cheap future
  mitigation would be reversibly scrambling the counter value (e.g. XOR/Feistel) before base62-encoding it, so
  codes stop being sequential-looking while remaining collision-free and decodable.
- **New in Step 4**: uniqueness is now only guaranteed if the Redis counter is intact. If Redis data were ever
  lost despite the AOF persistence (e.g. the volume itself is deleted), the counter restarts at 0 and could
  hand out a code that already exists in Postgres — which would hit the very same unhandled-`DbUpdateException`
  gap above. The DB-level unique index (Step 3) is the safety net that turns this into a loud `500` instead of
  silent data corruption, which is exactly the role the reference article assigns it ("minor data loss
  acceptable since only uniqueness required; UNIQUE constraint provides safety net") — but a real retry-on-
  collision path still does not exist yet.
- **New in Step 5**: no negative caching. A request for a code that does not exist always falls through to
  Postgres, every time - a real "cache penetration" pattern (repeatedly requesting missing keys bypasses the
  cache entirely). Not addressed now; would pair naturally with Step 7's rate limiting.
- **New in Step 5**: Redis has no `maxmemory`/eviction policy configured, so its default is `noeviction` - under
  memory pressure it would reject writes rather than evict anything. Not a real risk at local/dev scale, but a
  genuine Step 6 (scale) concern once the dataset is large enough for cache memory to matter.

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
- **Redis counter (`INCR`) + base62 encoding replaces random generation** (Step 4): matches the reference
  article's "great solution" for uniqueness — codes cannot collide by construction, since Redis is single-
  threaded and `INCR` is atomic, so no two concurrent requests can ever receive the same counter value. Verified
  live: created 5 URLs in a row and got exactly `1, 2, 3, 4, 5`; pushed the counter past 62 and confirmed the
  base62 rollover math (`63` → `"11"`, i.e. `1×62 + 1`) both in the generated code and by reading the raw
  counter value straight out of Redis.
- **Counter batching deferred to Step 6** (Step 4): the article covers reserving blocks of N values at once as
  an optimization for this same deep dive, but it only pays off once multiple app instances are contending for
  the same Redis counter concurrently — that is Step 6's (scale) concern, not this one, so implementing it now
  would add complexity with nothing to demonstrate it against yet.
- **`RedisCodeGenerator` is a concrete class, no interface** (Step 4): same reasoning as the `Bitly.Domain`
  reversal in Step 1 — no second implementation exists, no test project to mock against, so an interface would
  be pure ceremony. Registered as a singleton in DI, same lifetime as `IConnectionMultiplexer` itself.
- **`IConnectionMultiplexer` registered as a singleton, unlike `BitlyDbContext` (scoped)** (Step 4): StackExchange
  Redis's own guidance is that `ConnectionMultiplexer` is expensive to create and thread-safe, meant to be shared
  for the lifetime of the process — the opposite lifetime model from `DbContext`, which is intentionally cheap
  and per-request/unit-of-work scoped.
- **Redis persistence: AOF (`--appendonly yes`) + a named volume** (Step 4): without persistence, recreating the
  Redis container would reset the counter to 0 while Postgres still has rows using codes `1..N` — a real
  collision risk, not hypothetical, since Redis is now a source of truth for uniqueness, not just a cache.
  Verified live: restarted the `bitly-redis` container mid-session and confirmed the counter value survived.
  This does not eliminate the risk entirely (see Known limitations) — it just makes it very unlikely instead of
  guaranteed on every restart.
- **Cache-aside on the redirect path, not write-through** (Step 5): `GET /{code}` checks Redis first; on a miss
  it queries Postgres and populates the cache for next time. `POST /urls` does not touch the cache at all - the
  first reader for a given code pays the cache-miss cost, matching the article's description of the pattern
  exactly. Verified this is genuinely cache-aside and not accidentally write-through: the cache key did not
  exist immediately after create, only after the first `GET`.
- **Redis key TTL aligned to `ExpirationDate` instead of re-checking expiration on every cache hit** (Step 5):
  when a `ShortUrl` has an `ExpirationDate`, the cache key's TTL is set to exactly that remaining duration, so
  Redis evicts it automatically at the right moment - a cache hit can be trusted as "still valid" with no extra
  logic. Rows with no `ExpirationDate` get a 24h default TTL purely to bound memory over time, not for
  correctness. Verified live end to end: created a link expiring in 5s, confirmed its cache TTL was `5` (not the
  24h default), waited for it to lapse, confirmed Redis had evicted the key, and confirmed the next request fell
  through to Postgres and correctly returned `410` rather than serving a stale cached redirect.
- **Proved a cache hit actually bypasses Postgres, not just "looks correct"** (Step 5): deleted a `ShortUrl` row
  directly from Postgres via `psql` after it was cached, then confirmed the redirect still worked - which is
  only possible if the response came from Redis, since the database row no longer existed.
- **`maxmemory`/LRU eviction policy deliberately not configured** (Step 5, discussed before implementing): Redis
  has built-in approximated-LRU eviction (`maxmemory-policy allkeys-lru`, one config line, no application code)
  as a distinct mechanism from the TTL used here - TTL enforces "must expire at time X regardless of memory,"
  LRU eviction handles "running low on memory, drop what's least-used." Skipped for now since our dataset is far
  too small locally to ever hit a memory cap; real candidate for Step 6 once cache size is a genuine concern.
- **Latency measured before and after, not assumed** (Step 5): baseline (uncached, Postgres-per-request)
  steady-state was ~2.6-3.7ms per redirect; cached path measured ~1.8ms average - a real but modest ~40%
  reduction. Explicitly not claiming the article's dramatic memory-vs-disk numbers here: this is a local
  Postgres with no network hop and a handful of rows, so there is little latency to remove in the first place.
  The mechanism is correct and the improvement is real and measured, just smaller than the article's numbers
  would suggest at a scale we are not actually operating at.

## Update this file

Update this file whenever a step changes: stack/tooling decisions, structure, commands, or the "currently on" line.
