namespace Bitly.Api.Contracts;

public record CreateShortUrlRequest(string LongUrl, string? CustomAlias = null, DateTime? ExpirationDate = null);
