using System.Security.Cryptography;
using Bitly.Api.Contracts;
using Bitly.Api.Data;
using Bitly.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bitly.Api.Controllers;

[ApiController]
[Route("urls")]
public class UrlsController(BitlyDbContext db) : ControllerBase
{
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int CodeLength = 7;

    [HttpPost]
    public async Task<ActionResult<CreateShortUrlResponse>> Create(CreateShortUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LongUrl) ||
            !Uri.TryCreate(request.LongUrl, UriKind.Absolute, out _))
        {
            return BadRequest("longUrl must be a valid absolute URL.");
        }

        var code = string.IsNullOrWhiteSpace(request.CustomAlias)
            ? RandomNumberGenerator.GetString(Base62Alphabet, CodeLength)
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
        var shortUrl = await db.ShortUrls.FirstOrDefaultAsync(s => s.Code == code);

        if (shortUrl is null)
        {
            return NotFound();
        }

        if (shortUrl.ExpirationDate is { } expiration && expiration <= DateTime.UtcNow)
        {
            return StatusCode(StatusCodes.Status410Gone);
        }

        return Redirect(shortUrl.LongUrl);
    }
}
