# Bitly Clone

A URL shortener built with **.NET 10**, following the
[HelloInterview Bitly breakdown](https://www.hellointerview.com/learn/system-design/problem-breakdowns/bitly)
as the reference spec, deviating deliberately where noted.

Local-only, zero-cost: everything runs via Docker (Rancher Desktop), no cloud services required.

## Reference spec summary

**Functional requirements**
- Submit a long URL, get back a short one, with optional custom alias and expiration date
- Visiting a short URL redirects to the original long URL
- Out of scope: user authentication, click analytics

**Non-functional requirements**
- Uniqueness: each short code maps to exactly one long URL
- Redirect latency under 100ms
- 99.99% availability (favor availability over strict consistency)
- Scale target: 1B shortened URLs, 100M daily active users
- Read-heavy: redirects vastly outnumber URL creations

**Data model**
- `ShortUrl`: code, long URL, created-at, custom alias, expiration date
- `User`: creator of a short URL (omitted in this implementation — auth is out of scope)

**API**
- `POST /urls` (Write service) — create a short URL (`{ longUrl, customAlias?, expirationDate? }` → `{ shortUrl }`;
  JSON fields are camelCase, .NET's default, rather than the reference spec's snake_case)
- `GET /{code}` (Read service) — `302` redirect to the original URL, `404` if unknown, `410` if past its
  expiration date

**Deep dives covered by the reference article** (built incrementally in this repo):
1. **Uniqueness** — hash+base62 vs. a Redis-backed global counter with base62 encoding
2. **Fast redirects** — DB indexing → in-memory cache (Redis) → CDN/edge
3. **Scale to 1B URLs / 100M DAU** — Read/Write service split, counter coordination, multi-region, HA

## Stack

- .NET 10, ASP.NET Core Web API (Controllers)
- Separate Read and Write services (independent scaling for the read-heavy redirect path vs. the rare create path)
- PostgreSQL + Redis, run via Docker Compose (Rancher Desktop)
- Local only — no cloud dependencies

## Project structure

```
src/
  Bitly.Domain/       Shared entity (ShortUrl) + EF Core DbContext + migrations
  Bitly.WriteApi/      POST /urls - code generation (RedisCodeGenerator)
  Bitly.ReadApi/       GET /{code} - redirects (ShortUrlCache, cache-aside)
docker-compose.yml     Local PostgreSQL + Redis
Bitly.slnx             Solution file
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, e.g. via [Rancher Desktop](https://rancherdesktop.io/)

## Running locally

```bash
# 1. Start PostgreSQL and Redis
docker compose up -d

# 2. Restore local tools (EF Core CLI) and apply migrations
dotnet tool restore
dotnet ef database update --project src/Bitly.Domain --startup-project src/Bitly.WriteApi

# 3. Configure connection strings (once per service)
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.WriteApi
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/Bitly.WriteApi
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.ReadApi
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/Bitly.ReadApi

# 4. Run both services (separate terminals)
dotnet run --project src/Bitly.WriteApi   # http://localhost:5299 - POST /urls
dotnet run --project src/Bitly.ReadApi    # http://localhost:5300 - GET /{code}
```

Health check: `GET /health` on either service (ASP.NET Core's built-in Health Checks middleware)
