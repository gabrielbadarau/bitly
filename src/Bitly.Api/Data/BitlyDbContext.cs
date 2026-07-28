using Bitly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bitly.Api.Data;

public class BitlyDbContext(DbContextOptions<BitlyDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
}
