using Bitly.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<BitlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BitlyDb")));

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
