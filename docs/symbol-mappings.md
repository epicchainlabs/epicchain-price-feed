# Symbol Mappings Configuration

This document explains how to configure symbol mappings for different data sources in the price feed service.

## Overview

Different cryptocurrency exchanges and data providers use different formats for trading pair symbols. For example:

- Binance uses `BTCUSDT`
- OKEx uses `BTC-USDT`
- Coinbase uses `BTC-USD`
- CoinMarketCap uses just `BTC` (with conversion parameter)

The symbol mappings feature allows you to define a standard symbol (e.g., `BTCUSDT`) and map it to the specific format required by each data source.

## Configuration

Symbol mappings are configured in the `appsettings.json` file under the `PriceFeed.SymbolMappings.Mappings` section:

```json
"PriceFeed": {
  "Symbols": [
    "BTCUSDT",
    "ETHUSDT",
    "NEOUSDT",
    ...
  ],
  "SymbolMappings": {
    "Mappings": {
      "BTCUSDT": {
        "Binance": "BTCUSDT",
        "OKEx": "BTC-USDT",
        "Coinbase": "BTC-USD",
        "CoinMarketCap": "BTC"
      },
      "ETHUSDT": {
        "Binance": "ETHUSDT",
        "OKEx": "ETH-USDT",
        "Coinbase": "ETH-USD",
        "CoinMarketCap": "ETH"
      },
      ...
    }
  }
}
```

## Structure

The symbol mappings configuration has the following structure:

- **Standard Symbol**: The key in the `Mappings` dictionary is the standard symbol used throughout the application (e.g., `BTCUSDT`).
- **Data Source Mappings**: For each standard symbol, you define a dictionary of data source names to source-specific symbols.

## Adding a New Symbol

To add a new trading pair:

1. Add the standard symbol to the `Symbols` array
2. Add a mapping entry in the `SymbolMappings.Mappings` dictionary
3. For each data source that supports this symbol, add a mapping to the source-specific format

Example for adding FLMUSDT (Flamingo token):

```json
"FLMUSDT": {
  "Binance": "FLMUSDT",
  "OKEx": "FLM-USDT",
  "CoinMarketCap": "FLM"
}
```

## Excluding a Symbol from a Data Source

If a particular data source doesn't support a symbol, you can either:

1. Omit the data source from the mapping (the system will skip this symbol for that data source)
2. Explicitly set an empty string to indicate the symbol is not supported:

```json
"FLMUSDT": {
  "Binance": "FLMUSDT",
  "OKEx": "FLM-USDT",
  "Coinbase": "",  // Explicitly not supported
  "CoinMarketCap": "FLM"
}
```

## Data Source Names

The following data source names are supported:

- `Binance`
- `OKEx`
- `Coinbase`
- `CoinMarketCap`

Make sure to use these exact names in your mappings.

## Symbol Format Guidelines

Each data source expects symbols in a specific format:

- **Binance**: No separators, e.g., `BTCUSDT`
- **OKEx**: Hyphen separator, e.g., `BTC-USDT`
- **Coinbase**: Hyphen separator with USD (not USDT), e.g., `BTC-USD`
- **CoinMarketCap**: Base currency only, e.g., `BTC`

## Testing Symbol Mappings

After configuring symbol mappings, you can test them using the built-in test command:

```bash
dotnet run --project PriceFeed.Console/PriceFeed.Console.csproj -- --test-symbol-mappings
```

This will run a series of tests to verify that the symbol mappings are working correctly:

1. Testing `GetSourceSymbol` - Verifies that the correct source-specific symbol is returned for each data source
2. Testing `IsSymbolSupportedBySource` - Verifies that the system correctly identifies which symbols are supported by each data source
3. Testing `GetSymbolsForDataSource` - Verifies that the system correctly returns all symbols supported by a specific data source

You can also check the logs when running the price feed service. The service will log which symbols are supported by each data source and any errors related to symbol mappings.
