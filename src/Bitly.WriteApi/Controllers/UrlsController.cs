using Bitly.Domain.Data;
using Bitly.Domain.Models;
using Bitly.WriteApi.Contracts;
using Bitly.WriteApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bitly.WriteApi.Controllers;

[ApiController]
[Route("urls")]
public class UrlsController(BitlyDbContext db, RedisCodeGenerator codeGenerator, IConfiguration configuration)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateShortUrlResponse>> Create(CreateShortUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LongUrl) ||
            !Uri.TryCreate(request.LongUrl, UriKind.Absolute, out _))
        {
            return BadRequest("longUrl must be a valid absolute URL.");
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
            ExpirationDate = request.ExpirationDate,
        };

        db.ShortUrls.Add(shortUrl);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict($"The code or alias '{code}' is already in use.");
        }

        var shortUrlValue = $"{configuration["PublicBaseUrl"]}/{shortUrl.Code}";
        return Created(shortUrlValue, new CreateShortUrlResponse(shortUrlValue));
    }
}
