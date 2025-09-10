# PriceFeed R3E SDK

A .NET SDK for integrating with the R3E-optimized PriceFeed Oracle on Neo N3 blockchain.

## Installation

```bash
dotnet add package PriceFeed.R3E.SDK
```

## Quick Start

```csharp
using PriceFeed.R3E.SDK;
using PriceFeed.R3E.SDK.Models;

// Configure the client
var config = new PriceFeedConfig
{
    RpcEndpoint = "http://seed1t5.neo.org:20332",
    ContractHash = "0xYOUR_CONTRACT_HASH",
    CacheDurationSeconds = 60
};

// Create client
using var client = new PriceFeedClient(config);

// Get single price
var btcPrice = await client.GetPriceAsync("BTCUSDT");
Console.WriteLine($"BTC Price: ${btcPrice.Price:F2} (Confidence: {btcPrice.Confidence}%)");

// Get multiple prices
var symbols = new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" };
var prices = await client.GetPricesAsync(symbols);

foreach (var (symbol, priceData) in prices)
{
    Console.WriteLine($"{symbol}: ${priceData.Price:F2}");
}
```

## Features

### Price Data Retrieval

```csharp
// Get current price with caching
var priceData = await client.GetPriceAsync("BTCUSDT");

// Get fresh price (bypass cache)
var freshPrice = await client.GetPriceAsync("BTCUSDT", useCache: false);

// Access detailed price information
Console.WriteLine($"Symbol: {priceData.Symbol}");
Console.WriteLine($"Price: ${priceData.Price:F2}");
Console.WriteLine($"Raw Price: {priceData.RawPrice}"); // BigInteger with 8 decimals
Console.WriteLine($"Confidence: {priceData.Confidence}%");
Console.WriteLine($"Age: {priceData.AgeSeconds} seconds");
Console.WriteLine($"Quality Score: {priceData.QualityScore:F2}");
Console.WriteLine($"Is Fresh: {priceData.IsFresh}");
Console.WriteLine($"High Confidence: {priceData.IsHighConfidence}");
```

### Batch Operations

```csharp
// Get multiple prices efficiently
var symbols = new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT", "GASUSDT" };
var prices = await client.GetPricesAsync(symbols);

// Filter successful results
var validPrices = prices.Where(p => p.Value.IsHighConfidence && p.Value.IsFresh);

foreach (var (symbol, priceData) in validPrices)
{
    Console.WriteLine($"{symbol}: ${priceData.Price:F2} (Quality: {priceData.QualityScore:F2})");
}
```

### Contract Information

```csharp
// Get contract metadata
var contractInfo = await client.GetContractInfoAsync();
Console.WriteLine($"Contract Version: {contractInfo.Version}");
Console.WriteLine($"Framework: {contractInfo.Framework}");
Console.WriteLine($"Is Initialized: {contractInfo.IsInitialized}");
Console.WriteLine($"Is Paused: {contractInfo.IsPaused}");

// Check contract health
var isHealthy = await client.IsHealthyAsync();
Console.WriteLine($"Contract is healthy: {isHealthy}");
```

### Real-time Price Updates

```csharp
// Subscribe to all price updates
await client.SubscribeToPriceUpdatesAsync(
    symbols: null, // null = all symbols
    onPriceUpdate: priceUpdate =>
    {
        Console.WriteLine($"Price Update: {priceUpdate.Symbol} = ${priceUpdate.Price:F2}");
        Console.WriteLine($"  Confidence: {priceUpdate.Confidence}%");
        Console.WriteLine($"  Transaction: {priceUpdate.TransactionHash}");
        Console.WriteLine($"  Block: {priceUpdate.BlockIndex}");
    },
    cancellationToken: cancellationToken
);

// Subscribe to specific symbols
var watchSymbols = new[] { "BTCUSDT", "ETHUSDT" };
await client.SubscribeToPriceUpdatesAsync(
    symbols: watchSymbols,
    onPriceUpdate: priceUpdate =>
    {
        // Handle price updates for BTC and ETH only
        ProcessPriceUpdate(priceUpdate);
    },
    cancellationToken: cancellationToken
);
```

## Configuration

### Basic Configuration

```csharp
var config = new PriceFeedConfig
{
    RpcEndpoint = "http://seed1t5.neo.org:20332",  // Neo RPC endpoint
    ContractHash = "0xYOUR_CONTRACT_HASH",         // R3E contract hash
    TimeoutSeconds = 30,                           // RPC timeout
    CacheDurationSeconds = 60                      // Cache TTL
};
```

### Advanced Configuration

```csharp
var config = new PriceFeedConfig
{
    RpcEndpoint = "http://seed1t5.neo.org:20332",
    ContractHash = "0xYOUR_CONTRACT_HASH",
    TimeoutSeconds = 30,
    CacheDurationSeconds = 60,
    ValidateFreshness = true,                      // Validate data freshness
    MaxDataAgeSeconds = 3600,                      // Max acceptable age (1 hour)
    MinConfidenceScore = 50                        // Min confidence threshold
};
```

### With Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.Configure<PriceFeedConfig>(configuration.GetSection("PriceFeed"));
services.AddSingleton<PriceFeedClient>();

// Usage
public class MyService
{
    private readonly PriceFeedClient _priceFeedClient;

    public MyService(PriceFeedClient priceFeedClient)
    {
        _priceFeedClient = priceFeedClient;
    }

    public async Task<decimal> GetBitcoinPriceAsync()
    {
        var priceData = await _priceFeedClient.GetPriceAsync("BTCUSDT");
        return priceData.Price;
    }
}
```

## Error Handling

```csharp
try
{
    var priceData = await client.GetPriceAsync("BTCUSDT");
    Console.WriteLine($"BTC: ${priceData.Price:F2}");
}
catch (PriceDataException ex)
{
    Console.WriteLine($"Price data error for {ex.Symbol}: {ex.Message}");
}
catch (ContractException ex)
{
    Console.WriteLine($"Contract error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"General error: {ex.Message}");
}
```

## Best Practices

### 1. Use Caching Wisely

```csharp
// For frequent requests, use caching (default)
var price1 = await client.GetPriceAsync("BTCUSDT", useCache: true);

// For critical operations, get fresh data
var price2 = await client.GetPriceAsync("BTCUSDT", useCache: false);
```

### 2. Validate Data Quality

```csharp
var priceData = await client.GetPriceAsync("BTCUSDT");

if (!priceData.IsHighConfidence)
{
    Console.WriteLine($"Warning: Low confidence price for {priceData.Symbol}");
}

if (!priceData.IsFresh)
{
    Console.WriteLine($"Warning: Stale price data for {priceData.Symbol}");
}

// Use quality score for decisions
if (priceData.QualityScore < 0.7)
{
    Console.WriteLine("Price data quality is below threshold");
}
```

### 3. Handle Network Issues

```csharp
var maxRetries = 3;
var retryDelay = TimeSpan.FromSeconds(1);

for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        var priceData = await client.GetPriceAsync("BTCUSDT");
        return priceData.Price;
    }
    catch (Exception ex) when (attempt < maxRetries - 1)
    {
        Console.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}");
        await Task.Delay(retryDelay);
        retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2); // Exponential backoff
    }
}
```

### 4. Efficient Batch Processing

```csharp
// Don't do this (inefficient)
var btcPrice = await client.GetPriceAsync("BTCUSDT");
var ethPrice = await client.GetPriceAsync("ETHUSDT");
var neoPrice = await client.GetPriceAsync("NEOUSDT");

// Do this instead (efficient)
var symbols = new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" };
var prices = await client.GetPricesAsync(symbols);
```

## Performance Considerations

- **Caching**: The SDK automatically caches price data for the configured duration
- **Connection Pooling**: Uses HttpClient connection pooling for RPC calls
- **Concurrent Requests**: Batch operations use controlled concurrency (max 5 parallel requests)
- **Memory Management**: Cache cleanup runs automatically every 5 minutes

## Thread Safety

The `PriceFeedClient` is thread-safe and can be used from multiple threads simultaneously. It uses internal synchronization for cache operations and RPC calls.

## Supported Networks

- **Neo N3 TestNet**: `http://seed1t5.neo.org:20332`
- **Neo N3 MainNet**: `http://seed1.neo.org:10332`
- **Local Neo Node**: Configure your local RPC endpoint

## License

MIT License - See LICENSE file for details

## Support

- GitHub Issues: https://github.com/r3e-network/neo-price-feed/issues
- Documentation: https://docs.r3e.network
- Discord: https://discord.gg/r3e-network