using StackExchange.Redis;

namespace Bitly.Api.Services;

public class ShortUrlCache(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public async Task<string?> GetAsync(string code)
    {
        var value = await redis.GetDatabase().StringGetAsync(CacheKey(code));
        return value.IsNull ? null : value.ToString();
    }

    public async Task SetAsync(string code, string longUrl, DateTime? expirationDate)
    {
        var ttl = expirationDate is { } expiration
            ? expiration - DateTime.UtcNow
            : DefaultTtl;

        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await redis.GetDatabase().StringSetAsync(CacheKey(code), longUrl, ttl);
    }

    private static string CacheKey(string code) => $"shorturl:code:{code}";
}
