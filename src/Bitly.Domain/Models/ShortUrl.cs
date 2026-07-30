namespace Bitly.Domain.Models;

public class ShortUrl
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string LongUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomAlias { get; set; }
    public DateTime? ExpirationDate { get; set; }
}
