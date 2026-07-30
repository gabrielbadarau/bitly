using System.Threading.RateLimiting;
using Bitly.Domain.Data;
using Bitly.ReadApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<BitlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BitlyDb")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<ShortUrlCache>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("redirect", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

var instanceName = Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? Environment.MachineName;
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Instance-Name"] = instanceName;
    await next();
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
