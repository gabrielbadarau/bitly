using Bitly.Domain.Data;
using Bitly.ReadApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bitly.ReadApi.Controllers;

[ApiController]
public class RedirectController(BitlyDbContext db, ShortUrlCache cache, ILogger<RedirectController> logger)
    : ControllerBase
{
    [HttpGet("/{code}")]
    [EnableRateLimiting("redirect")]
    public async Task<IActionResult> RedirectToLongUrl(string code)
    {
        var cachedLongUrl = await cache.GetAsync(code);
        if (cachedLongUrl is not null)
        {
            logger.LogInformation("Cache hit for {Code}", code);
            return Redirect(cachedLongUrl);
        }

        logger.LogInformation("Cache miss for {Code}, querying database", code);
        var shortUrl = await db.ShortUrls.FirstOrDefaultAsync(s => s.Code == code);

        if (shortUrl is null)
        {
            logger.LogInformation("Redirect {Code} not found", code);
            return NotFound();
        }

        if (shortUrl.ExpirationDate is { } expiration && expiration <= DateTime.UtcNow)
        {
            logger.LogInformation("Redirect {Code} expired at {ExpirationDate}", code, expiration);
            return StatusCode(StatusCodes.Status410Gone);
        }

        await cache.SetAsync(shortUrl.Code, shortUrl.LongUrl, shortUrl.ExpirationDate);

        return Redirect(shortUrl.LongUrl);
    }
}
