using Bitly.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace Bitly.WriteApi.Services;

public class ExpiredShortUrlCleanupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ExpiredShortUrlCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("ExpirationCleanup:IntervalSeconds", 300));

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BitlyDbContext>();

                var deleted = await db.ShortUrls
                    .Where(s => s.ExpirationDate != null && s.ExpirationDate <= DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    logger.LogInformation("Expiration cleanup deleted {Count} expired short URLs", deleted);
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
