using Microsoft.EntityFrameworkCore;
using PriceFeed.R3E.EventIndexer.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Add Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=../PriceFeed.R3E.EventIndexer/events.db";

builder.Services.AddDbContext<EventIndexerContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// API endpoints
app.MapGet("/api/stats", async (EventIndexerContext context) =>
{
    var totalEvents = await context.ContractEvents.CountAsync();
    var priceUpdates = await context.ContractEvents
        .Where(e => e.EventName == "PriceUpdated")
        .CountAsync();
    
    var lastUpdate = await context.ContractEvents
        .Where(e => e.EventName == "PriceUpdated")
        .OrderByDescending(e => e.Timestamp)
        .FirstOrDefaultAsync();

    return Results.Ok(new
    {
        TotalEvents = totalEvents,
        PriceUpdates = priceUpdates,
        LastUpdate = lastUpdate?.Timestamp,
        LastSymbol = lastUpdate != null ? 
            System.Text.Json.JsonSerializer.Deserialize<dynamic>(lastUpdate.Data) : null
    });
});

app.MapGet("/api/events/recent", async (EventIndexerContext context, int limit = 50) =>
{
    var events = await context.ContractEvents
        .OrderByDescending(e => e.Timestamp)
        .Take(limit)
        .Select(e => new
        {
            e.EventName,
            e.Timestamp,
            e.TransactionHash,
            e.BlockIndex
        })
        .ToListAsync();

    return Results.Ok(events);
});

app.MapGet("/api/prices/history/{symbol}", async (EventIndexerContext context, string symbol, int days = 7) =>
{
    var cutoffDate = DateTime.UtcNow.AddDays(-days);
    
    var priceEvents = await context.ContractEvents
        .Where(e => e.EventName == "PriceUpdated" && 
                   e.Timestamp >= cutoffDate &&
                   e.Data.Contains($"\"{symbol}\""))
        .OrderBy(e => e.Timestamp)
        .ToListAsync();

    var prices = priceEvents.Select(e =>
    {
        try
        {
            dynamic data = System.Text.Json.JsonSerializer.Deserialize<dynamic>(e.Data);
            return new
            {
                Timestamp = e.Timestamp,
                Price = data[1]?.GetString(),
                Confidence = data[3]?.GetString()
            };
        }
        catch
        {
            return null;
        }
    }).Where(p => p != null).ToList();

    return Results.Ok(prices);
});

Console.WriteLine("🔍 R3E PriceFeed Analytics Dashboard");
Console.WriteLine("Starting on: http://localhost:5000");

app.Run();