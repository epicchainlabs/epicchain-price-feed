# Accessible API Data Sources

This document provides guidance on using accessible cryptocurrency price data APIs with the Neo Price Feed system.

## Overview

Many popular cryptocurrency APIs have accessibility restrictions due to regional regulations, API key requirements, or domain blocks. This guide identifies accessible alternatives and provides configuration examples.

## Data Source Accessibility Status

### ✅ Accessible APIs (Recommended)

#### 1. CoinGecko
- **Status**: ✅ Fully Accessible
- **API Key Required**: No (optional for Pro tier)
- **Rate Limits**: 10-50 calls/minute (depending on plan)
- **Coverage**: Comprehensive cryptocurrency data
- **Reliability**: High
- **Cost**: Free tier available

**Advantages:**
- No API key required for basic usage
- Comprehensive coin coverage
- Reliable and well-documented API
- Good rate limits for free tier
- Global accessibility

**Configuration:**
```json
"CoinGecko": {
  "BaseUrl": "https://api.coingecko.com",
  "SimplePriceEndpoint": "/api/v3/simple/price",
  "CoinListEndpoint": "/api/v3/coins/list",
  "TimeoutSeconds": 30
}
```

#### 2. Kraken
- **Status**: ✅ Fully Accessible
- **API Key Required**: No (for public endpoints)
- **Rate Limits**: 1 call/second for public endpoints
- **Coverage**: Major cryptocurrencies
- **Reliability**: High
- **Cost**: Free for public data

**Advantages:**
- High-quality exchange data
- No API key required for price data
- Good uptime and reliability
- Real trading volume data

**Configuration:**
```json
"Kraken": {
  "BaseUrl": "https://api.kraken.com",
  "TickerEndpoint": "/0/public/Ticker",
  "AssetPairsEndpoint": "/0/public/AssetPairs",
  "TimeoutSeconds": 30
}
```

#### 3. Coinbase (Limited)
- **Status**: ✅ Partially Accessible
- **API Key Required**: No (for exchange rates endpoint)
- **Rate Limits**: Moderate
- **Coverage**: Limited to exchange rates
- **Reliability**: High
- **Cost**: Free for exchange rates

**Advantages:**
- Reliable exchange data
- No API key required for exchange rates
- Good for major cryptocurrencies

**Limitations:**
- Spot price endpoint requires authentication
- Limited to exchange rates format

**Configuration:**
```json
"Coinbase": {
  "BaseUrl": "https://api.coinbase.com",
  "ExchangeRatesEndpoint": "/v2/exchange-rates",
  "TimeoutSeconds": 30
}
```

### ❌ Restricted/Inaccessible APIs

#### 1. Binance
- **Status**: ❌ Regionally Restricted
- **Issues**: Blocked in many regions due to regulatory restrictions
- **Alternative**: Use CoinGecko or Kraken instead

#### 2. CoinMarketCap
- **Status**: ❌ Requires Paid API Key
- **Issues**: Free tier has very limited calls, requires subscription for meaningful usage
- **Alternative**: Use CoinGecko for comprehensive data

#### 3. OKEx
- **Status**: ❌ Domain Restrictions
- **Issues**: Domain blocks and regulatory restrictions in many regions
- **Alternative**: Use Kraken for exchange data

## Recommended Configuration

### Primary Setup (Accessible APIs Only)

Use this configuration for maximum accessibility:

```json
{
  "PriceFeed": {
    "Symbols": ["BTCUSDT", "ETHUSDT", "ADAUSDT", "SOLUSDT"],
    "SymbolMappings": {
      "Mappings": {
        "BTCUSDT": {
          "CoinGecko": "bitcoin",
          "Kraken": "XBTUSD",
          "Coinbase": "BTC-USD"
        },
        "ETHUSDT": {
          "CoinGecko": "ethereum",
          "Kraken": "ETHUSD",
          "Coinbase": "ETH-USD"
        }
      }
    }
  }
}
```

### Fallback Configuration

If you have access to restricted APIs in your region:

```json
{
  "DataSourcePriority": {
    "Primary": ["CoinGecko", "Kraken"],
    "Secondary": ["Coinbase"],
    "Fallback": ["Binance"]
  }
}
```

## Symbol Mapping Guide

### CoinGecko Mappings
CoinGecko uses coin IDs instead of trading pairs:
- Bitcoin: `bitcoin`
- Ethereum: `ethereum`
- Cardano: `cardano`
- Solana: `solana`

### Kraken Mappings
Kraken uses specific pair formats:
- Bitcoin: `XBTUSD` (not BTCUSD)
- Ethereum: `ETHUSD`
- XRP: `XRPUSD`

### Coinbase Mappings
Coinbase uses hyphenated pairs:
- Bitcoin: `BTC-USD`
- Ethereum: `ETH-USD`
- XRP: `XRP-USD`

## Implementation Steps

### 1. Update Dependencies

Ensure your project includes the new data source adapters:

```csharp
// Add to your DI container
services.Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
services.Configure<KrakenOptions>(configuration.GetSection("Kraken"));
services.AddScoped<IDataSourceAdapter, CoinGeckoDataSourceAdapter>();
services.AddScoped<IDataSourceAdapter, KrakenDataSourceAdapter>();
```

### 2. Update Configuration

Copy the accessible configuration:
```bash
cp src/PriceFeed.Console/appsettings.accessible.json src/PriceFeed.Console/appsettings.json
```

### 3. Test Connectivity

Run the application to test API connectivity:
```bash
dotnet run --project src/PriceFeed.Console
```

### 4. Monitor Rate Limits

Implement appropriate rate limiting:
- CoinGecko: 10-50 calls/minute
- Kraken: 1 call/second
- Coinbase: Monitor response headers

## Rate Limiting Best Practices

### CoinGecko
- Free tier: 10 calls/minute
- Pro tier: 50 calls/minute
- Implement exponential backoff
- Cache responses when possible

### Kraken
- Public endpoints: 1 call/second
- Use batch requests when available
- Implement request queuing

### Coinbase
- Monitor `X-RateLimit-*` headers
- Implement appropriate delays
- Use exchange rates endpoint efficiently

## Error Handling

### Common Issues and Solutions

1. **Rate Limit Exceeded**
   - Implement exponential backoff
   - Use caching to reduce API calls
   - Consider upgrading to paid tiers

2. **Network Connectivity**
   - Implement retry logic
   - Use multiple data sources for redundancy
   - Monitor API status pages

3. **Symbol Not Found**
   - Verify symbol mappings
   - Check API documentation for supported pairs
   - Implement fallback symbols

## Monitoring and Alerting

### Health Checks
Configure health checks for each data source:

```json
"HealthCheck": {
  "Enabled": true,
  "DataSourceThreshold": 0.75,
  "CheckInterval": "00:01:00"
}
```

### Metrics to Monitor
- API response times
- Success/failure rates
- Rate limit usage
- Data freshness

## Cost Considerations

### Free Tier Limitations
- CoinGecko: 10 calls/minute
- Kraken: No cost, rate limited
- Coinbase: Limited endpoints

### Paid Tier Benefits
- CoinGecko Pro: Higher rate limits, additional endpoints
- Consider costs vs. data requirements

## Security Considerations

### API Key Management
- Store API keys in environment variables
- Use Azure Key Vault or similar for production
- Rotate keys regularly

### Network Security
- Use HTTPS only
- Implement proper certificate validation
- Consider API gateway for additional security

## Troubleshooting

### Common Error Messages

1. **"API key required"**
   - Check if endpoint requires authentication
   - Verify API key configuration

2. **"Rate limit exceeded"**
   - Implement rate limiting
   - Consider upgrading API plan

3. **"Symbol not supported"**
   - Verify symbol mappings
   - Check API documentation

### Debug Steps
1. Test API endpoints directly with curl
2. Check application logs for detailed errors
3. Verify network connectivity
4. Validate configuration format

## Migration Guide

### From Restricted APIs

If migrating from Binance/CoinMarketCap:

1. Update symbol mappings
2. Test new data sources
3. Adjust rate limiting
4. Update monitoring

### Configuration Migration
```bash
# Backup current config
cp appsettings.json appsettings.backup.json

# Use accessible config
cp appsettings.accessible.json appsettings.json

# Update specific settings as needed
```

## Support and Resources

### API Documentation
- [CoinGecko API](https://www.coingecko.com/en/api)
- [Kraken API](https://docs.kraken.com/rest/)
- [Coinbase API](https://developers.coinbase.com/api/v2)

### Community Resources
- CoinGecko Discord
- Kraken Support
- Coinbase Developer Forums

### Status Pages
- [CoinGecko Status](https://status.coingecko.com/)
- [Kraken Status](https://status.kraken.com/)
- [Coinbase Status](https://status.coinbase.com/)