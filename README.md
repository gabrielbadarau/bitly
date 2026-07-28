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
- `User`: creator of a short URL

**API**
- `POST /urls` — create a short URL
- `GET /{code}` — 302 redirect to the original URL

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
  Bitly.Api/       ASP.NET Core Web API (controllers)
  Bitly.Domain/    Domain entities, no external dependencies
Bitly.slnx         Solution file
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, e.g. via [Rancher Desktop](https://rancherdesktop.io/) (needed from Step 2 onward, for PostgreSQL/Redis)

## Running locally

```bash
dotnet build
dotnet run --project src/Bitly.Api
```

Health check: `GET http://localhost:5299/health` (ASP.NET Core's built-in Health Checks middleware)

## Progress log

- [x] **Step 1** — Repo scaffold: solution, `Bitly.Api` (Web API, controllers), `Bitly.Domain` (class library), `/health` endpoint
- [ ] **Step 2** — Data model + PostgreSQL via Docker Compose + EF Core migrations
- [ ] **Step 3** — Naive end-to-end create/redirect flow
- [ ] **Step 4** — Deep dive: uniqueness (Redis counter + base62)
- [ ] **Step 5** — Deep dive: fast redirects (Redis cache-aside)
- [ ] **Step 6** — Deep dive: scale (Read/Write service split + local load balancer)
- [ ] **Step 7** — Round out NFRs (expiration cleanup, alias collisions, rate limiting, logging)
- [ ] **Step 8** — Full Docker Compose stack + polish
