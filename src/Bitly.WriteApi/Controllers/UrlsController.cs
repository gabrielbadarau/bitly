using Bitly.Domain.Data;
using Bitly.Domain.Models;
using Bitly.WriteApi.Contracts;
using Bitly.WriteApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bitly.WriteApi.Controllers;

[ApiController]
[Route("urls")]
public class UrlsController(
    BitlyDbContext db,
    RedisCodeGenerator codeGenerator,
    IConfiguration configuration,
    ILogger<UrlsController> logger) : ControllerBase
{
    // ASP.NET Core route matching is case-insensitive, so "/HEALTH" also reaches the health check -
    // block every casing a caller might try, not just the literal lowercase word. "urls" is reserved
    // too: the nginx gateway (Step 8) routes the exact path "/urls" to this service regardless of HTTP
    // method, so a code equal to "urls" would be permanently unreachable for GET (redirect) requests.
    private static readonly HashSet<string> ReservedCodes = new(StringComparer.OrdinalIgnoreCase) { "health", "urls" };

    [HttpPost]
    [EnableRateLimiting("create")]
    public async Task<ActionResult<CreateShortUrlResponse>> Create(CreateShortUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LongUrl) ||
            !Uri.TryCreate(request.LongUrl, UriKind.Absolute, out _))
        {
            return BadRequest("longUrl must be a valid absolute URL.");
        }

        if (!string.IsNullOrWhiteSpace(request.CustomAlias) && ReservedCodes.Contains(request.CustomAlias))
        {
            logger.LogWarning("Rejected reserved alias {Alias}", request.CustomAlias);
            return BadRequest($"'{request.CustomAlias}' is a reserved word and cannot be used as a custom alias.");
        }

        var code = string.IsNullOrWhiteSpace(request.CustomAlias)
            ? await codeGenerator.NextCodeAsync()
            : request.CustomAlias;

        var shortUrl = new ShortUrl
        {
            Code = code,
            LongUrl = request.LongUrl,
            CreatedAt = DateTime.UtcNow,
            CustomAlias = request.CustomAlias,
            ExpirationDate = NormalizeToUtc(request.ExpirationDate),
        };

        db.ShortUrls.Add(shortUrl);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogWarning("Duplicate code or alias {Code}", code);
            return Conflict($"The code or alias '{code}' is already in use.");
        }

        logger.LogInformation("Created short URL {Code} for {LongUrl}", code, request.LongUrl);

        var shortUrlValue = $"{configuration["PublicBaseUrl"]}/{shortUrl.Code}";
        return Created(shortUrlValue, new CreateShortUrlResponse(shortUrlValue));
    }

    // System.Text.Json parses "...Z" as Kind=Utc directly, but a numeric offset like "...+00:00"
    // (e.g. Python's isoformat()) converts to local time and tags it Kind=Local - and a bare
    // timestamp with no offset at all comes through as Kind=Unspecified. Npgsql only accepts Utc
    // for a "timestamp with time zone" column, so every case must be normalized explicitly here.
    private static DateTime? NormalizeToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
    };
}
