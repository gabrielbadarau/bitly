using Bitly.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Bitly.Domain.Data;

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
