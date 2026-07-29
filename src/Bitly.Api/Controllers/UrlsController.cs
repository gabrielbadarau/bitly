using Bitly.Api.Contracts;
using Bitly.Api.Data;
using Bitly.Api.Models;
using Bitly.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bitly.Api.Controllers;

[ApiController]
[Route("urls")]
public class UrlsController(BitlyDbContext db, RedisCodeGenerator codeGenerator, ShortUrlCache cache) : ControllerBase
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
        await db.SaveChangesAsync();

        var shortUrlValue = $"{Request.Scheme}://{Request.Host}/{shortUrl.Code}";
        return Created(shortUrlValue, new CreateShortUrlResponse(shortUrlValue));
    }

    [HttpGet("/{code}")]
    public async Task<IActionResult> RedirectToLongUrl(string code)
    {
        var cachedLongUrl = await cache.GetAsync(code);
        if (cachedLongUrl is not null)
        {
            return Redirect(cachedLongUrl);
        }

        var shortUrl = await db.ShortUrls.FirstOrDefaultAsync(s => s.Code == code);

        if (shortUrl is null)
        {
            return NotFound();
        }

        if (shortUrl.ExpirationDate is { } expiration && expiration <= DateTime.UtcNow)
        {
            return StatusCode(StatusCodes.Status410Gone);
        }

        await cache.SetAsync(shortUrl.Code, shortUrl.LongUrl, shortUrl.ExpirationDate);

        return Redirect(shortUrl.LongUrl);
    }
}
