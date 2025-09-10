# API Reference

This page provides a detailed API reference for both the Price Oracle Contract and the Price Feed Service.

## Price Oracle Contract API

### Administrative Methods

#### Initialize

Initializes the contract. This method can only be called once after deployment.

```csharp
public static bool Initialize(UInt160 owner)
```

**Parameters:**
- `owner`: The initial contract owner address

**Returns:**
- `bool`: True if initialization was successful

---

#### ChangeOwner

Changes the contract owner. Only the current owner can call this method.

```csharp
public static bool ChangeOwner(UInt160 newOwner)
```

**Parameters:**
- `newOwner`: The new owner address

**Returns:**
- `bool`: True if the owner was changed successfully

---

#### SetPaused

Pauses or unpauses the contract. Only the owner can call this method.

```csharp
public static bool SetPaused(bool paused)
```

**Parameters:**
- `paused`: True to pause, false to unpause

**Returns:**
- `bool`: True if the pause state was changed successfully

---

#### Update

Upgrades the contract to a new version. Only the owner can call this method.

```csharp
public static bool Update(ByteString nefFile, string manifest, object data = null)
```

**Parameters:**
- `nefFile`: The new NEF file
- `manifest`: The new manifest
- `data`: Optional data for the update

**Returns:**
- `bool`: True if the contract was upgraded successfully

---

#### AddOracle

Adds an oracle to the authorized list. Only the owner can call this method.

```csharp
public static bool AddOracle(UInt160 oracleAddress)
```

**Parameters:**
- `oracleAddress`: The oracle address to add

**Returns:**
- `bool`: True if the oracle was added successfully

---

#### RemoveOracle

Removes an oracle from the authorized list. Only the owner can call this method.

```csharp
public static bool RemoveOracle(UInt160 oracleAddress)
```

**Parameters:**
- `oracleAddress`: The oracle address to remove

**Returns:**
- `bool`: True if the oracle was removed successfully

### Oracle Methods

#### UpdatePrice

Updates the price for a single symbol. Only authorized oracles can call this method.

```csharp
public static bool UpdatePrice(string symbol, BigInteger price, BigInteger timestamp, BigInteger confidenceScore)
```

**Parameters:**
- `symbol`: The symbol to update
- `price`: The new price (scaled by 10^8)
- `timestamp`: The timestamp of the price data
- `confidenceScore`: The confidence score (0-100)

**Returns:**
- `bool`: True if the price was updated successfully

---

#### UpdatePriceBatch

Updates prices for multiple symbols in a batch. Only authorized oracles can call this method.

```csharp
public static bool UpdatePriceBatch(string[] symbols, BigInteger[] prices, BigInteger[] timestamps, BigInteger[] confidenceScores)
```

**Parameters:**
- `symbols`: The symbols to update
- `prices`: The new prices (scaled by 10^8)
- `timestamps`: The timestamps of the price data
- `confidenceScores`: The confidence scores (0-100)

**Returns:**
- `bool`: True if at least one price was updated successfully

### Query Methods

#### GetPrice

Gets the current price for a symbol.

```csharp
public static BigInteger GetPrice(string symbol)
```

**Parameters:**
- `symbol`: The symbol to get the price for

**Returns:**
- `BigInteger`: The current price (scaled by 10^8)

---

#### GetTimestamp

Gets the timestamp of the current price for a symbol.

```csharp
public static BigInteger GetTimestamp(string symbol)
```

**Parameters:**
- `symbol`: The symbol to get the timestamp for

**Returns:**
- `BigInteger`: The timestamp of the current price

---

#### GetConfidenceScore

Gets the confidence score of the current price for a symbol.

```csharp
public static BigInteger GetConfidenceScore(string symbol)
```

**Parameters:**
- `symbol`: The symbol to get the confidence score for

**Returns:**
- `BigInteger`: The confidence score of the current price (0-100)

---

#### GetPriceData

Gets the complete price data for a symbol.

```csharp
public static BigInteger[] GetPriceData(string symbol)
```

**Parameters:**
- `symbol`: The symbol to get the price data for

**Returns:**
- `BigInteger[]`: An array containing [price, timestamp, confidenceScore]

---

#### IsOracle

Checks if an address is an authorized oracle.

```csharp
public static bool IsOracle(UInt160 address)
```

**Parameters:**
- `address`: The address to check

**Returns:**
- `bool`: True if the address is an authorized oracle

---

#### GetOwner

Gets the current contract owner.

```csharp
public static UInt160 GetOwner()
```

**Returns:**
- `UInt160`: The owner's address

---

#### IsPaused

Checks if the contract is paused.

```csharp
public static bool IsPaused()
```

**Returns:**
- `bool`: True if the contract is paused

## Price Feed Service API

The Price Feed Service does not expose a public API, as it runs as a GitHub Action. However, it does interact with the Price Oracle Contract and various data source APIs.

### Data Source Adapters

#### IDataSourceAdapter Interface

```csharp
public interface IDataSourceAdapter
{
    string SourceName { get; }
    Task<IEnumerable<string>> GetSupportedSymbolsAsync();
    Task<PriceData> GetPriceDataAsync(string symbol);
    Task<IEnumerable<PriceData>> GetPriceDataBatchAsync(IEnumerable<string> symbols);
}
```

#### Implemented Adapters

- `BinanceDataSourceAdapter`: Fetches price data from Binance
- `CoinMarketCapDataSourceAdapter`: Fetches price data from CoinMarketCap
- `CoinbaseDataSourceAdapter`: Fetches price data from Coinbase
- `OKExDataSourceAdapter`: Fetches price data from OKEx

### Price Aggregation

#### IPriceAggregationService Interface

```csharp
public interface IPriceAggregationService
{
    Task<AggregatedPriceData> AggregateAsync(IEnumerable<PriceData> priceData);
    Task<IEnumerable<AggregatedPriceData>> AggregateBatchAsync(Dictionary<string, IEnumerable<PriceData>> priceDataBySymbol);
}
```

### Batch Processing

#### IBatchProcessingService Interface

```csharp
public interface IBatchProcessingService
{
    Task<bool> ProcessBatchAsync(PriceBatch batch);
    Task<BatchStatus> GetBatchStatusAsync(Guid batchId);
}
```

### Data Models

#### PriceData

```csharp
public class PriceData
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public string Source { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal? Volume { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
```

#### AggregatedPriceData

```csharp
public class AggregatedPriceData
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
    public int ConfidenceScore { get; set; }
    public int SourceCount { get; set; }
    public decimal? StandardDeviation { get; set; }
}
```

#### PriceBatch

```csharp
public class PriceBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<AggregatedPriceData> Prices { get; set; } = new List<AggregatedPriceData>();
}
```
