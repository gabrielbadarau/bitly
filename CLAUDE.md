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
  Bitly.Domain/
    Models/            Entities (ShortUrl)
    Data/              BitlyDbContext + Migrations/ (shared by both services below)
  Bitly.WriteApi/       ASP.NET Core Web API - port 5299
    Controllers/       UrlsController (POST /urls only)
    Contracts/         Request/response DTOs (records)
    Services/          RedisCodeGenerator (Redis INCR + base62 encoding),
                       ExpiredShortUrlCleanupService (BackgroundService, periodic bulk delete)
  Bitly.ReadApi/        ASP.NET Core Web API - port 5300
    Controllers/       RedirectController (GET /{code} only)
    Services/          ShortUrlCache (cache-aside layer: GetAsync/SetAsync, TTL = ExpirationDate or 24h default)
.config/
  dotnet-tools.json     Local tool manifest (dotnet-ef, pinned version)
nginx/nginx.conf         Load balancer config, round-robins across Bitly.ReadApi instances
docker-compose.yml       Local PostgreSQL (postgres:16-alpine) + Redis (redis:7-alpine, AOF persistence) + nginx
Bitly.slnx               Solution file (new .slnx format, .NET 9+)
```

## Commands

```bash
docker compose up -d                                    # start Postgres + Redis + nginx (load balancer, :8080)
dotnet tool restore                                      # restore local tools (dotnet-ef), once per clone
dotnet build
dotnet run --project src/Bitly.WriteApi --urls http://0.0.0.0:5299                        # POST /urls
INSTANCE_NAME=read-a dotnet run --project src/Bitly.ReadApi --urls http://0.0.0.0:5300     # GET /{code}
INSTANCE_NAME=read-b dotnet run --project src/Bitly.ReadApi --urls http://0.0.0.0:5301     # 2nd instance, for the LB

# EF Core migrations - Domain has no Program.cs/connection string of its own,
# so WriteApi is used as the --startup-project
dotnet ef migrations add <Name> --project src/Bitly.Domain --startup-project src/Bitly.WriteApi --output-dir Data/Migrations
dotnet ef database update --project src/Bitly.Domain --startup-project src/Bitly.WriteApi
```

Redirects through the load balancer: `http://localhost:8080/<code>` (nginx round-robins across whichever
`Bitly.ReadApi` instances are running on 5300/5301). Bind services to `0.0.0.0`, not `localhost` - the nginx
container reaches them via `host.docker.internal`, which cannot connect to a port only bound to loopback.

Health check: `GET /health` → plain-text `Healthy` on either service (built-in ASP.NET Core Health Checks middleware)

Connection strings live in **user-secrets**, not `appsettings.json` - each service has its own secrets store, same
values:
```bash
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.WriteApi
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/Bitly.WriteApi
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.ReadApi
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/Bitly.ReadApi
```
(the `bitly_dev_only` password is only ever used inside the local Docker network — fine to keep in this file and in `docker-compose.yml`, but the connection strings themselves still stay out of the committed `appsettings.json`/git history as a matter of habit.)

`Bitly.WriteApi`'s `appsettings.json` also has a `PublicBaseUrl` (`http://localhost:8080`, the load balancer) -
see Key decisions log for why the short URL returned by create must not be built from the Write service's own
request host.

Inspect the Redis counter directly: `docker exec bitly-redis redis-cli GET shorturl:counter`

Inspect a cached redirect directly: `docker exec bitly-redis redis-cli GET shorturl:code:<code>` and
`... TTL shorturl:code:<code>` (seconds until Redis auto-evicts it)

Quick manual test of the end-to-end flow:

```bash
curl -X POST http://localhost:5299/urls -H "Content-Type: application/json" \
  -d '{"longUrl": "https://example.com"}'
# => 201 { "shortUrl": "http://localhost:8080/<code>" }

curl -v http://localhost:8080/<code>
# => 302 Found, Location: https://example.com, X-Instance-Name: read-a or read-b
```

## Step plan status

- [x] **Step 1** — Repo scaffold: solution, `Bitly.Api` (Web API, controllers), `Bitly.Domain` (class library), `/health` endpoint (built-in Health Checks middleware)
- [x] **Step 2** — Data model + PostgreSQL via Docker Compose + EF Core migrations
- [x] **Step 3** — Naive end-to-end create/redirect flow
- [x] **Step 4** — Deep dive: uniqueness (Redis counter + base62)
- [x] **Step 5** — Deep dive: fast redirects (Redis cache-aside)
- [x] **Step 6** — Deep dive: scale (Read/Write service split + local load balancer)
- [ ] **Step 7** — Round out NFRs (expiration cleanup, alias collisions, rate limiting, logging)
- [ ] **Step 8** — Full Docker Compose stack + polish

Currently on: **Step 7, part 3 of 5 (expiration cleanup) — done.**

**Known limitations carried forward on purpose:**
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
- **New in Step 6 (counter batching)**: if a `WriteApi` process crashes or restarts while it still holds unused
  values from its current reserved batch, those values are permanently skipped - the next process reserves a
  fresh batch starting after the shared counter, not after the last value actually used. This produces gaps in
  the sequence (e.g. codes might jump from `50` to `1051`), which the reference article treats as an accepted
  cost of batching, not a bug - uniqueness is preserved either way, just not perfect density.
- **New in Step 6 (load balancer)**: only the Read service sits behind the load balancer; `Bitly.WriteApi` still
  runs as a single instance with no LB in front of it. This matches the article's own premise (reads scale
  independently because they vastly outnumber writes) rather than being an oversight, but it does mean the
  Write path has no redundancy - if that one instance goes down, creates stop working even though redirects
  keep serving fine from the cache/Postgres via the Read instances.

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
- **`Bitly.Domain` reintroduced, `Bitly.Api` retired, split into `Bitly.WriteApi` + `Bitly.ReadApi`** (Step 6):
  this is exactly the trigger condition written down in Step 1's decision to collapse `Bitly.Domain` -
  "split out a `Bitly.Domain` project if/when Step 6 actually needs a second consumer." Both new services need
  `ShortUrl`/`BitlyDbContext`, so the domain project is real this time, not speculative. `RedisCodeGenerator`
  stays in `WriteApi` only and `ShortUrlCache` stays in `ReadApi` only - neither is actually shared between the
  two services, so neither belongs in `Bitly.Domain` (would repeat the earlier over-eager-sharing mistake).
- **The short URL returned by create points at the Read service, not the Write service's own host** (Step 6):
  `UrlsController` used to build `shortUrl` from `Request.Scheme`/`Request.Host`, which after the split would
  produce a link back to `Bitly.WriteApi` (port 5299) - the one service that can never serve `GET /{code}`.
  Replaced with a `PublicBaseUrl` config value (`Bitly.WriteApi/appsettings.json`, currently
  `http://localhost:5300`) pointing at wherever redirects are actually served. Verified live: created a URL via
  `POST` on 5299, confirmed the response and `Location` header pointed at 5300, and that the code actually
  redirected correctly from there. This value will need to become the load balancer's address once part 3 of
  this step adds one in front of multiple Read instances.
- **Each service gets its own `dotnet user-secrets` store** (Step 6): `UserSecretsId` is per-`.csproj`, so
  `WriteApi` and `ReadApi` each needed their own `dotnet user-secrets init` + `set` calls, even though the actual
  connection string values are identical. This is expected, not a workaround - two independently deployable
  services should not share a secrets file even in dev.
- **EF Core package versions had to be pinned explicitly across projects** (Step 6, real gotcha hit): after the
  split, `dotnet build` failed with `CS1705` - `Bitly.Domain` (which references `Microsoft.EntityFrameworkCore
  .Design`) resolved `Microsoft.EntityFrameworkCore` to `10.0.10`, while `WriteApi`/`ReadApi` (which only
  reference `Npgsql.EntityFrameworkCore.PostgreSQL`) independently resolved it to a lower transitive `10.0.4`.
  Project references do not force version alignment across a solution the way you might expect. Fixed by adding
  explicit `PackageReference`s for `Microsoft.EntityFrameworkCore` and `.Relational` at `10.0.10` to both
  service projects, converging all three projects on the same version.
- **Namespace rename during the move (`Bitly.Api.*` → `Bitly.Domain.*`) does not break existing migrations**
  (Step 6): confirmed live - `dotnet ef migrations list` still showed both prior migrations as applied, and
  `dotnet ef database update` reported a clean no-op, after moving and renaming the entity/DbContext/migration
  files. EF's `__EFMigrationsHistory` table only stores the `MigrationId` string, not a fully-qualified type
  name, so this kind of structural refactor is safe as long as the migration ID itself is untouched.
- **Migrations now require both `--project` and `--startup-project`** (Step 6): `Bitly.Domain` has no
  `Program.cs` or connection string of its own, so `dotnet ef` needs a separate "startup project" to actually
  run against - `Bitly.WriteApi` is used for this by convention, since it is the service that owns writes to
  the schema conceptually. `Bitly.WriteApi` therefore also needed `Microsoft.EntityFrameworkCore.Design` added
  (the tools require it on the startup project specifically, not just the project containing the migrations).
- **`RedisCodeGenerator` now reserves batches of 1000 via a single `INCRBY`, hands out values locally** (Step 6):
  each instance holds `_nextValue`/`_batchEnd` in memory, guarded by a `SemaphoreSlim` (not a plain `lock`, since
  the refill itself is `await`-based and C# does not allow awaiting inside a `lock` block). Verified live:
  creating 10 URLs in a row produced exactly one `INCRBY` call (`redis-cli INFO commandstats`), not ten, and the
  raw counter jumped by 1000 in a single step rather than incrementing one at a time.
- **Real bug found and fixed via live verification**: the initial implementation defaulted `_batchEnd` to `0`
  (the implicit C# default for `long`), and the refill check was `_nextValue > _batchEnd`. On the very first
  call after process start, `_nextValue` is also `0`, so `0 > 0` is `false` - the check silently skipped
  reserving a batch and returned `Encode(0)` = `"0"` directly, using uninitialized state as if it were a real
  reserved value. This was not cosmetic: since these fields reset on every process restart, every restart would
  have handed out `"0"` again, guaranteeing a collision (and the still-unhandled `DbUpdateException` gap) on the
  second restart. Fixed by initializing `_batchEnd = -1` as an explicit "no batch reserved yet" sentinel.
  Verified the fix by restarting the process twice in a row and confirming each restart correctly reserved a
  fresh batch (`"HF"`, then `"XN"`) instead of repeating a prior code.
- **nginx (plain container, no custom image) as the local load balancer** (Step 6): round-robin is nginx's
  default `upstream` behavior, needing zero extra config - just an `upstream` block listing both `ReadApi`
  instances and a `server` block proxying to it. No new .NET project needed for this piece; it is pure
  infrastructure config, mounted read-only into the container via `docker-compose.yml`.
- **`X-Instance-Name` response header added to `Bitly.ReadApi`** (Step 6): reads an `INSTANCE_NAME` environment
  variable and stamps every response with it. Added specifically to make load balancing verifiable rather than
  assumed - without it there would be no way to tell from the outside whether requests were actually hitting
  two different processes or one process handling everything. Kept as a small permanent feature rather than
  ripped out after verifying, similar to how real systems often expose instance/pod identity for debugging.
- **Real networking gotcha hit and fixed**: nginx initially failed with `502 Bad Gateway` / `Connection refused`
  reaching `host.docker.internal`. Two causes, both fixed: (1) `Bitly.ReadApi` was bound to `--urls
  http://localhost:5300`, which only listens on loopback and refuses connections arriving from the Docker
  network - fixed by binding to `0.0.0.0` instead, so the process accepts connections on any interface: (2) an
  explicit `extra_hosts: ["host.docker.internal:host-gateway"]` entry was added defensively (a common idiom for
  Linux Docker hosts) but it actively broke things here - it overrode Rancher Desktop's own correct, built-in
  resolution of `host.docker.internal` with the wrong bridge-gateway IP. Removed the override entirely and let
  Rancher Desktop handle it natively. Verified live: 8 requests through the load balancer alternated perfectly
  between `read-a`/`read-b` via the response header, for both `/health` and real `GET /{code}` redirect traffic.
- **`PublicBaseUrl` updated to the load balancer address (`http://localhost:8080`)** (Step 6): this closes the
  loop flagged when `PublicBaseUrl` was first introduced in part 1 of this step - once multiple Read instances
  exist, the short URL returned by create must point at the address that actually distributes traffic across
  them, not at any single instance directly.
- **Duplicate alias handled explicitly, separately from generic exception handling** (Step 7): a duplicate
  `Code`/`CustomAlias` is a foreseeable business condition, not a crash - `UrlsController.Create` now catches
  `DbUpdateException` specifically when its `InnerException` is a `PostgresException` with
  `SqlState: PostgresErrorCodes.UniqueViolation` (Postgres error code `23505`) and returns a clean `409
  Conflict`. This is deliberately separate from the broader safety net below - business-expected conditions get
  explicit handling with a specific status code, in every environment, rather than falling through to a generic
  handler. Verified live: the exact duplicate-alias scenario reproduced back in Step 3 now returns `409` with a
  clean message instead of a `500` with a leaked stack trace.
- **`AddProblemDetails()` + `UseExceptionHandler()` as a safety net for genuinely unexpected exceptions** (Step
  7, both services): anything not explicitly caught now returns a generic RFC 9110 problem-details response
  (`{"type", "title", "status", "traceId"}`) instead of leaking internals. Verified live with a real failure, not
  a contrived one: stopped the `bitly-postgres` container and attempted a create - got a clean `500`
  problem+json body with a `traceId` for correlation, no connection string or stack trace anywhere in the
  response. Notable, verified behavior: this suppressed ASP.NET Core's automatic Development-mode diagnostic
  page too, not just Production's - explicitly configuring exception handling takes precedence over the
  framework's default Development behavior, not just supplementing it.
- **Reserved-word check on `CustomAlias`, case-insensitive** (Step 7): closes the gap first proven live in the
  routing discussion before Step 4, where a custom alias `"health"` silently shadowed `Bitly.ReadApi`'s own
  `/health` endpoint and became permanently unreachable. `UrlsController` now rejects `"health"` (`400`) before
  ever reaching the database. Verified live first that ASP.NET Core route matching is case-insensitive by
  default - `GET /HEALTH` still hit the health check, not the redirect catch-all - so the check uses
  `StringComparer.OrdinalIgnoreCase` and blocks every casing, not just the literal lowercase word. The reserved
  list currently only needs `"health"`, since that is the only literal route `Bitly.ReadApi` has today; it would
  need updating if a future step ever adds another literal route there.
- **`ExpiredShortUrlCleanupService` as a `BackgroundService` in `Bitly.WriteApi`, using `IServiceScopeFactory`**
  (Step 7): a `BackgroundService` is registered as a singleton for the app's whole lifetime, but `BitlyDbContext`
  is registered scoped - a scoped service cannot be injected directly into a singleton's constructor. The fix is
  the standard .NET pattern: inject `IServiceScopeFactory` instead, and create a fresh scope (and therefore a
  fresh `BitlyDbContext`) on every cleanup cycle via `scopeFactory.CreateScope()`.
- **`ExecuteDeleteAsync` instead of load-then-remove** (Step 7): translates directly to one SQL `DELETE ...
  WHERE ...` statement (confirmed live in the query log) rather than pulling every expired row into memory with
  `ToListAsync()` first, calling `RemoveRange`, then `SaveChangesAsync` - an EF Core 7+ bulk-operation API worth
  knowing about specifically because the naive load-then-remove approach doesn't scale once the expired-row
  count is large.
- **Cleanup interval configurable via `ExpirationCleanup:IntervalSeconds`** (default 300s), overridable via the
  `ExpirationCleanup__IntervalSeconds` environment variable (double underscore is .NET configuration's convention
  for binding env vars to nested config keys) - used a 5s override during verification instead of waiting on the
  real default.
- **Real bug found and fixed during this step verification, unrelated to the cleanup job itself**: the very
  first live test crashed `POST /urls` with `System.ArgumentException: Cannot write DateTime with Kind=Local to
  PostgreSQL type 'timestamp with time zone'`. Cause: the test used Python's `.isoformat()` to generate the
  expiration timestamp, which produces a numeric UTC offset (`...+00:00`) rather than a `Z` suffix.
  `System.Text.Json`'s default `DateTime` parsing treats these differently - `Z` becomes `DateTimeKind.Utc`
  directly, but a numeric offset converts the value to local time and tags it `DateTimeKind.Local`; a bare
  timestamp with no offset at all comes through as `DateTimeKind.Unspecified`. Npgsql only accepts `Utc` for a
  `timestamp with time zone` column. This was a real, pre-existing gap (any client sending a perfectly valid
  ISO-8601 UTC timestamp in offset form would have crashed create), not something specific to this feature -
  fixed with a `NormalizeToUtc` helper in `UrlsController` that converts `Local` values with `ToUniversalTime()`
  and reinterprets `Unspecified` values as UTC via `DateTime.SpecifyKind`. Verified live: the exact request that
  crashed before now succeeds, and the resulting row expires and gets cleaned up correctly.
- **Verified the cleanup job does not touch valid rows** (Step 7): created one `ShortUrl` with no expiration and
  one expiring a day out, waited through two 5s cleanup cycles, confirmed both rows still exist - the `WHERE
  "ExpirationDate" IS NOT NULL AND "ExpirationDate" <= now()` filter is not accidentally broad.

## Update this file

Update this file whenever a step changes: stack/tooling decisions, structure, commands, or the "currently on" line.
