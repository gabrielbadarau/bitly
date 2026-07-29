using Bitly.Api.Data;
using Bitly.Api.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<BitlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BitlyDb")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<RedisCodeGenerator>();
builder.Services.AddSingleton<ShortUrlCache>();

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
