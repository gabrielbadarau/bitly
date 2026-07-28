using Bitly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bitly.Api.Data;

public class BitlyDbContext(DbContextOptions<BitlyDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortUrl>()
            .HasIndex(s => s.Code)
            .IsUnique();
    }
}
