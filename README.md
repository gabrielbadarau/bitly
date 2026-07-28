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
- `POST /urls` — create a short URL (`{ longUrl, customAlias?, expirationDate? }` → `{ shortUrl }`; JSON fields are
  camelCase, .NET's default, rather than the reference spec's snake_case)
- `GET /{code}` — `302` redirect to the original URL, `404` if unknown, `410` if past its expiration date

**Deep dives covered by the reference article** (built incrementally in this repo):
1. **Uniqueness** — hash+base62 vs. a Redis-backed global counter with base62 encoding
2. **Fast redirects** — DB indexing → in-memory cache (Redis) → CDN/edge
3. **Scale to 1B URLs / 100M DAU** — Read/Write service split, counter coordination, multi-region, HA

## Stack

- .NET 10, ASP.NET Core Web API (Controllers)
- PostgreSQL + Redis, run via Docker Compose (Rancher Desktop)
- Local only — no cloud dependencies

## Project structure

```
src/
  Bitly.Api/
    Controllers/     API endpoints
    Models/          Entities (e.g. ShortUrl)
    Contracts/       Request/response DTOs
    Data/            EF Core DbContext + migrations
docker-compose.yml    Local PostgreSQL
Bitly.slnx            Solution file
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, e.g. via [Rancher Desktop](https://rancherdesktop.io/)

## Running locally

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Restore local tools (EF Core CLI) and apply migrations
dotnet tool restore
dotnet ef database update --project src/Bitly.Api

# 3. Configure the connection string (once)
dotnet user-secrets set "ConnectionStrings:BitlyDb" "Host=localhost;Port=5432;Database=bitly;Username=bitly;Password=bitly_dev_only" --project src/Bitly.Api

# 4. Run the API
dotnet build
dotnet run --project src/Bitly.Api
```

Health check: `GET http://localhost:5299/health` (ASP.NET Core's built-in Health Checks middleware)
