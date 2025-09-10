# EpicChain Price Feed Service with Trusted Execution Environment

## Executive Summary

The EpicChain Price Feed Service represents a sophisticated blockchain oracle solution designed to provide reliable, secure, and decentralized price data to the EpicChain ecosystem. This production-grade system leverages Trusted Execution Environment (TEE) technology to ensure the integrity and authenticity of price data while maintaining high availability and resistance to manipulation.

## Comprehensive System Architecture

### Core Architectural Principles

The system is built upon several foundational principles that ensure its reliability and security:

1. **Decentralized Data Sourcing**: Price data is aggregated from multiple independent sources to prevent single points of failure and manipulation
2. **Cryptographic Verification**: All transactions are cryptographically signed within the TEE to prove authentic execution
3. **Dual-Signature Security Model**: Requires both TEE and master account signatures for transaction authorization
4. **Fault Tolerance**: Comprehensive error handling and fallback mechanisms maintain service availability
5. **Scalable Design**: Modular architecture supports easy expansion to additional data sources and cryptocurrencies

### System Components

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Trusted Execution Environment                      │
│                                                                             │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────────┐  │
│  │                 │    │                 │    │                         │  │
│  │  Data Collector │    │  Data Aggregator│    │  Transaction Processor  │  │
│  │  Module         │────│  Module         │────│  Module                 │  │
│  │                 │    │                 │    │                         │  │
│  └─────────────────┘    └─────────────────┘    └─────────────────────────┘  │
│          │                        │                        │                │
│          ▼                        ▼                        ▼                │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────────┐  │
│  │ External Data   │    │ Price Validation│    │ Blockchain Interface     │  │
│  │ Sources         │    │ Engine          │    │ Layer                    │  │
│  └─────────────────┘    └─────────────────┘    └─────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           EpicChain Blockchain Network                       │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                      Price Oracle Smart Contract                      │  │
│  │                                                                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │  │
│  │  │ Price Storage│  │ Access Control│  │ Oracle Management │  │ Emergency │   │  │
│  │  │ Module       │  │ Module       │  │ Module     │  │ Functions  │   │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘   │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Detailed Technical Implementation

### Data Collection Layer

The data collection system implements a sophisticated multi-source aggregation approach with comprehensive fault tolerance:

```csharp
public class MultiSourceDataCollector : IPriceDataCollector
{
    private readonly List<IPriceDataSource> _dataSources;
    private readonly ILogger<MultiSourceDataCollector> _logger;
    private readonly CircuitBreaker _circuitBreaker;
    
    public async Task<CollectionResult> CollectPricesAsync(IEnumerable<string> symbols)
    {
        var allResults = new ConcurrentBag<PriceData>();
        var tasks = _dataSources.Select(source => 
            Task.Run(async () =>
            {
                try
                {
                    if (_circuitBreaker.IsOpen(source.Name))
                        return;
                        
                    var results = await source.GetPricesAsync(symbols);
                    foreach (var result in results.Where(r => r.IsSuccessful))
                    {
                        allResults.Add(result.PriceData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to collect data from {Source}", source.Name);
                    _circuitBreaker.RecordFailure(source.Name);
                }
            }));
            
        await Task.WhenAll(tasks);
        return new CollectionResult(allResults.ToList());
    }
}
```

### Data Aggregation Engine

The aggregation engine employs multiple methodologies to ensure accurate price representation:

```csharp
public class SmartPriceAggregator : IPriceAggregator
{
    public AggregationResult Aggregate(IEnumerable<PriceData> priceDataPoints)
    {
        var groupedData = priceDataPoints.GroupBy(p => p.Symbol);
        var results = new List<AggregatedPrice>();
        
        foreach (var group in groupedData)
        {
            var validPrices = group.Where(p => p.ConfidenceScore > 0.7).ToList();
            
            if (validPrices.Count == 0)
            {
                // Fallback to all prices if none meet confidence threshold
                validPrices = group.ToList();
            }
            
            // Weighted average based on confidence scores and source reliability
            decimal weightedSum = 0;
            decimal totalWeight = 0;
            
            foreach (var price in validPrices)
            {
                decimal weight = price.ConfidenceScore * GetSourceWeight(price.Source);
                weightedSum += price.Price * weight;
                totalWeight += weight;
            }
            
            decimal averagePrice = totalWeight > 0 ? weightedSum / totalWeight : 0;
            
            // Calculate volatility and confidence metrics
            decimal standardDeviation = CalculateStandardDeviation(validPrices, averagePrice);
            decimal confidenceScore = CalculateOverallConfidence(validPrices, standardDeviation);
            
            results.Add(new AggregatedPrice
            {
                Symbol = group.Key,
                Price = averagePrice,
                Timestamp = DateTime.UtcNow,
                ConfidenceScore = confidenceScore,
                StandardDeviation = standardDeviation,
                SourceCount = validPrices.Count,
                MinPrice = validPrices.Min(p => p.Price),
                MaxPrice = validPrices.Max(p => p.Price)
            });
        }
        
        return new AggregationResult(results);
    }
    
    private decimal CalculateStandardDeviation(List<PriceData> prices, decimal mean)
    {
        if (prices.Count <= 1) return 0;
        
        decimal sumOfSquares = 0;
        foreach (var price in prices)
        {
            decimal deviation = price.Price - mean;
            sumOfSquares += deviation * deviation;
        }
        
        decimal variance = sumOfSquares / (prices.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }
}
```

### Transaction Processing System

The transaction processor handles secure communication with the EpicChain blockchain:

```csharp
public class DualSignatureTransactionProcessor : ITransactionProcessor
{
    private readonly IEpicChainService _epicChainService;
    private readonly ISigningService _signingService;
    private readonly ILogger<DualSignatureTransactionProcessor> _logger;
    
    public async Task<TransactionResult> SubmitPriceUpdatesAsync(
        IEnumerable<AggregatedPrice> prices, 
        string contractHash)
    {
        try
        {
            // Prepare batch transaction
            var transaction = await _epicChainService.PrepareBatchTransaction(
                contractHash, 
                prices,
                OperationType.UpdatePrices);
                
            // Generate TEE attestation signature
            var teeSignature = await _signingService.SignWithTeeAccount(transaction);
            transaction.AddSignature(teeSignature);
            
            // Generate master account signature for fee coverage
            var masterSignature = await _signingService.SignWithMasterAccount(transaction);
            transaction.AddSignature(masterSignature);
            
            // Submit transaction
            var result = await _epicChainService.SubmitTransaction(transaction);
            
            if (result.Status == TransactionStatus.Succeeded)
            {
                _logger.LogInformation("Successfully submitted {Count} price updates", prices.Count());
                return TransactionResult.Success(result.TransactionId);
            }
            else
            {
                _logger.LogError("Transaction failed: {Error}", result.Error);
                return TransactionResult.Failure(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit price updates");
            return TransactionResult.Failure(ex.Message);
        }
    }
}
```

## Advanced Security Model

### Trusted Execution Environment Implementation

The TEE implementation provides a secure enclave for critical operations:

```csharp
public class SecureTeeEnclave : ISecureEnclave
{
    private readonly byte[] _sealedStorageKey;
    private readonly IAttestationService _attestationService;
    
    public async Task<TeeContext> InitializeAsync()
    {
        // Generate attestation report to prove TEE authenticity
        var attestationReport = await _attestationService.GenerateReportAsync();
        
        // Unseal persistent storage for TEE account
        var teeAccount = await UnsealAccountFromSecureStorage();
        
        if (teeAccount == null)
        {
            // First run: generate new TEE account
            teeAccount = GenerateNewEpicChainAccount();
            await SealAccountToSecureStorage(teeAccount);
        }
        
        return new TeeContext
        {
            AttestationReport = attestationReport,
            TeeAccount = teeAccount,
            IsInitialized = true,
            EnclaveId = GenerateEnclaveId()
        };
    }
    
    public async Task<byte[]> SignDataAsync(byte[] data, SigningContext context)
    {
        ValidateAttestation(context.AttestationEvidence);
        
        using (var secureScope = EnterSecureScope())
        {
            var privateKey = await RetrievePrivateKeyAsync(context.KeyId);
            using (var keyHandle = ImportPrivateKey(privateKey))
            {
                return SignDataWithKey(keyHandle, data, context.SigningAlgorithm);
            }
        }
    }
}
```

### Dual-Signature Protocol

The dual-signature mechanism ensures transaction integrity:

```csharp
public class DualSignatureProtocol : IDualSignatureProtocol
{
    public async Task<DualSignatureResult> CreateDualSignatureAsync(
        Transaction transaction, 
        TeeContext teeContext,
        MasterAccount masterAccount)
    {
        // Step 1: TEE attests to the transaction content
        var transactionHash = ComputeTransactionHash(transaction);
        var teeAttestation = await _teeService.CreateAttestation(transactionHash, teeContext);
        
        // Step 2: TEE signs the attested transaction
        var teeSignature = await _teeService.SignDataAsync(
            teeAttestation.SignedData, 
            new SigningContext { KeyId = teeContext.TeeAccount.KeyId });
            
        // Step 3: Master account signs the complete package
        var masterSignatureData = CombineSignatures(transactionHash, teeSignature);
        var masterSignature = await _masterSigningService.SignAsync(
            masterSignatureData, 
            masterAccount);
            
        return new DualSignatureResult
        {
            TransactionHash = transactionHash,
            TeeAttestation = teeAttestation,
            TeeSignature = teeSignature,
            MasterSignature = masterSignature,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public async Task<bool> VerifyDualSignatureAsync(
        DualSignatureResult signatureResult,
        string expectedTeeAccount,
        string expectedMasterAccount)
    {
        // Verify TEE attestation
        if (!await _attestationService.VerifyAttestation(signatureResult.TeeAttestation))
            return false;
            
        // Verify TEE signature
        if (!VerifySignature(
            signatureResult.TeeAttestation.SignedData,
            signatureResult.TeeSignature,
            expectedTeeAccount))
            return false;
            
        // Verify master signature
        var expectedMasterSignatureData = CombineSignatures(
            signatureResult.TransactionHash,
            signatureResult.TeeSignature);
            
        if (!VerifySignature(
            expectedMasterSignatureData,
            signatureResult.MasterSignature,
            expectedMasterAccount))
            return false;
            
        return true;
    }
}
```

## Smart Contract Implementation

### Price Oracle Contract Architecture

The smart contract implements a comprehensive price oracle system:

```csharp
[DisplayName("EpicChainPriceOracle")]
[ManifestExtra("Author", "EpicChain Labs")]
[ManifestExtra("Email", "devs@epic-chain.org")]
[ManifestExtra("Description", "Decentralized Price Oracle Contract")]
public class EpicChainPriceOracle : SmartContract
{
    // Storage keys
    private static readonly byte[] PrefixPrices = { 0x01 };
    private static readonly byte[] PrefixOracles = { 0x02 };
    private static readonly byte[] PrefixConfig = { 0x03 };
    private static readonly byte[] PrefixTimestamps = { 0x04 };
    
    // Contract events
    [DisplayName("PriceUpdated")]
    public static event Action<string, decimal, ulong> OnPriceUpdated;
    
    [DisplayName("OracleAdded")]
    public static event Action<UInt160> OnOracleAdded;
    
    [DisplayName("OracleRemoved")]
    public static event Action<UInt160> OnOracleRemoved;
    
    // Initialization method
    public static bool Initialize(UInt160 owner, UInt160 teeAccount)
    {
        if (!Runtime.CheckWitness(owner)) return false;
        
        // Set contract owner
        StoragePut(PrefixConfig, "owner", owner);
        
        // Add TEE account as initial oracle
        AddOracle(teeAccount);
        
        // Set minimum oracles to 1 for initial setup
        StoragePut(PrefixConfig, "minOracles", 1);
        
        // Set price deviation threshold (10%)
        StoragePut(PrefixConfig, "deviationThreshold", 10_00); // 10.00%
        
        // Set update interval (15 minutes)
        StoragePut(PrefixConfig, "updateInterval", 900); // seconds
        
        return true;
    }
    
    // Update prices with dual-signature verification
    public static bool UpdatePrices(
        string[] symbols,
        decimal[] prices,
        byte[] teeSignature,
        byte[] masterSignature,
        byte[] attestationEvidence)
    {
        // Verify dual signature
        if (!VerifyDualSignatures(symbols, prices, teeSignature, masterSignature, attestationEvidence))
            return false;
            
        // Check if oracle is authorized
        var caller = Runtime.CallingScriptHash;
        if (!IsOracle(caller)) return false;
        
        // Check price deviation thresholds
        if (!CheckPriceDeviations(symbols, prices)) return false;
        
        // Update prices
        for (int i = 0; i < symbols.Length; i++)
        {
            var symbol = symbols[i];
            var price = prices[i];
            
            // Store price with timestamp
            var priceData = new PriceData
            {
                Price = price,
                Timestamp = Runtime.Time,
                Oracle = caller
            };
            
            StoragePut(PrefixPrices, symbol, priceData);
            StoragePut(PrefixTimestamps, symbol, Runtime.Time);
            
            OnPriceUpdated(symbol, price, Runtime.Time);
        }
        
        return true;
    }
    
    // Verify dual signatures
    private static bool VerifyDualSignatures(
        string[] symbols,
        decimal[] prices,
        byte[] teeSignature,
        byte[] masterSignature,
        byte[] attestationEvidence)
    {
        // Reconstruct signed data
        var signedData = ConstructSignedData(symbols, prices, Runtime.Time);
        
        // Get authorized TEE account
        var teeAccount = StorageGet(PrefixConfig, "teeAccount");
        if (teeAccount == null) return false;
        
        // Verify TEE signature with attestation
        if (!VerifyTeeSignature(signedData, teeSignature, attestationEvidence, teeAccount))
            return false;
            
        // Get master account
        var masterAccount = StorageGet(PrefixConfig, "masterAccount");
        if (masterAccount == null) return false;
        
        // Verify master signature
        var masterSignedData = Concat(signedData, teeSignature);
        if (!VerifySignature(masterSignedData, masterSignature, masterAccount))
            return false;
            
        return true;
    }
}
```

## Data Source Integration Framework

### Extensible Data Source Architecture

The system supports multiple data sources with a plugin architecture:

```csharp
public interface IPriceDataSource
{
    string Name { get; }
    int Priority { get; }
    TimeSpan Timeout { get; }
    
    Task<PriceResult[]> GetPricesAsync(IEnumerable<string> symbols);
    Task<SourceStatus> GetStatusAsync();
    decimal ReliabilityScore { get; }
}

public abstract class BasePriceDataSource : IPriceDataSource
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;
    protected readonly CircuitBreaker _circuitBreaker;
    
    public abstract string Name { get; }
    public abstract int Priority { get; }
    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);
    
    public virtual decimal ReliabilityScore => 
        _circuitBreaker.IsOpen ? 0 : CalculateCurrentReliability();
    
    protected abstract Task<PriceResult[]> FetchPricesAsync(IEnumerable<string> symbols);
    
    public async Task<PriceResult[]> GetPricesAsync(IEnumerable<string> symbols)
    {
        if (_circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker open for {DataSource}", Name);
            return symbols.Select(s => PriceResult.Failure(s, "Circuit breaker open")).ToArray();
        }
        
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            var results = await FetchPricesAsync(symbols);
            _circuitBreaker.RecordSuccess();
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching prices from {DataSource}", Name);
            _circuitBreaker.RecordFailure();
            return symbols.Select(s => PriceResult.Failure(s, ex.Message)).ToArray();
        }
    }
    
    private decimal CalculateCurrentReliability()
    {
        var successRate = _circuitBreaker.SuccessRate;
        var responseTime = _circuitBreaker.AverageResponseTime;
        
        // Calculate reliability based on success rate and response time
        decimal reliability = successRate;
        
        // Penalize slow responses
        if (responseTime > TimeSpan.FromSeconds(2))
        {
            reliability *= 0.9m;
        }
        if (responseTime > TimeSpan.FromSeconds(5))
        {
            reliability *= 0.7m;
        }
        
        return reliability;
    }
}
```

### Specific Data Source Implementations

#### CoinGecko Implementation

```csharp
public class CoinGeckoDataSource : BasePriceDataSource
{
    public override string Name => "CoinGecko";
    public override int Priority => 1;
    
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.coingecko.com/api/v3";
    
    public CoinGeckoDataSource(HttpClient httpClient, ILogger<CoinGeckoDataSource> logger, 
        string apiKey = null) : base(httpClient, logger)
    {
        _apiKey = apiKey;
        _httpClient.BaseAddress = new Uri(_baseUrl);
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-cg-pro-api-key", _apiKey);
        }
    }
    
    protected override async Task<PriceResult[]> FetchPricesAsync(IEnumerable<string> symbols)
    {
        // Map symbols to CoinGecko IDs
        var coinIds = symbols.Select(s => MapSymbolToCoinId(s)).Where(id => id != null).ToList();
        
        if (coinIds.Count == 0)
            return symbols.Select(s => PriceResult.Failure(s, "Unsupported symbol")).ToArray();
        
        var url = $"/simple/price?ids={string.Join(",", coinIds)}&vs_currencies=usd&include_24hr_change=true";
        var response = await _httpClient.GetAsync(url);
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);
        
        var results = new List<PriceResult>();
        
        foreach (var symbol in symbols)
        {
            var coinId = MapSymbolToCoinId(symbol);
            if (coinId == null || !data.TryGetProperty(coinId, out var coinData))
            {
                results.Add(PriceResult.Failure(symbol, "Symbol not found"));
                continue;
            }
            
            if (coinData.TryGetProperty("usd", out var priceElement) &&
                priceElement.TryGetDecimal(out var price))
            {
                // Calculate confidence based on 24h change and other factors
                decimal confidence = 0.9m; // Base confidence
                
                if (coinData.TryGetProperty("usd_24h_change", out var changeElement) &&
                    changeElement.TryGetDecimal(out var change24h))
                {
                    // Adjust confidence based on volatility
                    confidence *= CalculateVolatilityConfidence(Math.Abs(change24h));
                }
                
                results.Add(PriceResult.Success(symbol, price, DateTime.UtcNow, confidence, Name));
            }
            else
            {
                results.Add(PriceResult.Failure(symbol, "Price data missing"));
            }
        }
        
        return results.ToArray();
    }
    
    private string MapSymbolToCoinId(string symbol)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BTC"] = "bitcoin",
            ["ETH"] = "ethereum",
            ["ADA"] = "cardano",
            ["BNB"] = "binancecoin",
            ["XRP"] = "ripple",
            ["SOL"] = "solana",
            ["MATIC"] = "matic-network",
            ["DOT"] = "polkadot",
            ["LINK"] = "chainlink",
            ["UNI"] = "uniswap"
        };
        
        return mapping.TryGetValue(symbol.Replace("USDT", ""), out var coinId) ? coinId : null;
    }
}
```

#### Kraken Implementation

```csharp
public class KrakenDataSource : BasePriceDataSource
{
    public override string Name => "Kraken";
    public override int Priority => 2;
    
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _baseUrl = "https://api.kraken.com";
    
    public KrakenDataSource(HttpClient httpClient, ILogger<KrakenDataSource> logger,
        string apiKey, string apiSecret) : base(httpClient, logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _httpClient.BaseAddress = new Uri(_baseUrl);
    }
    
    protected override async Task<PriceResult[]> FetchPricesAsync(IEnumerable<string> symbols)
    {
        var results = new List<PriceResult>();
        
        foreach (var symbol in symbols)
        {
            try
            {
                var krakenPair = MapSymbolToKrakenPair(symbol);
                if (krakenPair == null)
                {
                    results.Add(PriceResult.Failure(symbol, "Unsupported symbol"));
                    continue;
                }
                
                var url = $"/0/public/Ticker?pair={krakenPair}";
                var response = await _httpClient.GetAsync(url);
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (data.TryGetProperty("result", out var result) &&
                    result.TryGetProperty(krakenPair, out var pairData) &&
                    pairData.TryGetProperty("c", out var priceArray) &&
                    priceArray.EnumerateArray().FirstOrDefault().TryGetDecimal(out var price))
                {
                    // Calculate confidence based on volume and spread
                    decimal confidence = 0.85m;
                    
                    if (pairData.TryGetProperty("v", out var volumeArray) &&
                        volumeArray.EnumerateArray().FirstOrDefault().TryGetDecimal(out var volume))
                    {
                        confidence *= CalculateVolumeConfidence(volume);
                    }
                    
                    results.Add(PriceResult.Success(symbol, price, DateTime.UtcNow, confidence, Name));
                }
                else
                {
                    results.Add(PriceResult.Failure(symbol, "Invalid response format"));
                }
            }
            catch (Exception ex)
            {
                results.Add(PriceResult.Failure(symbol, ex.Message));
            }
        }
        
        return results.ToArray();
    }
    
    private string MapSymbolToKrakenPair(string symbol)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BTCUSDT"] = "XBTUSDT",
            ["ETHUSDT"] = "ETHUSDT",
            ["ADAUSDT"] = "ADAUSDT",
            ["BNBUSDT"] = "BNBUSDT",
            ["XRPUSDT"] = "XRPUSDT",
            ["SOLUSDT"] = "SOLUSDT",
            ["MATICUSDT"] = "MATICUSDT",
            ["DOTUSDT"] = "DOTUSDT",
            ["LINKUSDT"] = "LINKUSDT",
            ["UNIUSDT"] = "UNIUSDT"
        };
        
        return mapping.TryGetValue(symbol, out var pair) ? pair : null;
    }
}
```

## Configuration Management System

### Hierarchical Configuration

The system uses a sophisticated configuration management approach:

```csharp
public class PriceFeedConfiguration
{
    public DataSourcesConfig DataSources { get; set; } = new DataSourcesConfig();
    public EpicChainConfig EpicChain { get; set; } = new EpicChainConfig();
    public AggregationConfig Aggregation { get; set; } = new AggregationConfig();
    public SchedulingConfig Scheduling { get; set; } = new SchedulingConfig();
    public SecurityConfig Security { get; set; } = new SecurityConfig();
    
    public class DataSourcesConfig
    {
        public List<string> EnabledSources { get; set; } = new List<string>();
        public int RequestTimeoutSeconds { get; set; } = 30;
        public int MaxParallelRequests { get; set; } = 5;
        public CircuitBreakerConfig CircuitBreaker { get; set; } = new CircuitBreakerConfig();
    }
    
    public class EpicChainConfig
    {
        public string RpcEndpoint { get; set; } = "http://localhost:10332";
        public string ContractHash { get; set; }
        public decimal MinimumGasBalance { get; set; } = 1.0m;
        public int MaxTransactionRetries { get; set; } = 3;
        public int BlockConfirmationWait { get; set; } = 1;
    }
    
    public class AggregationConfig
    {
        public decimal MinimumConfidenceThreshold { get; set; } = 0.7m;
        public decimal MaximumDeviationPercentage { get; set; } = 25.0m;
        public int MinimumSourceCount { get; set; } = 2;
        public WeightingStrategy WeightingStrategy { get; set; } = WeightingStrategy.ConfidenceVolume;
    }
    
    public class SchedulingConfig
    {
        public string CronExpression { get; set; } = "0 */2 * * *"; // Every 2 hours
        public int ExecutionTimeoutMinutes { get; set; } = 30;
        public bool RunOnStartup { get; set; } = true;
    }
    
    public class SecurityConfig
    {
        public int AttestationValidityMinutes { get; set; } = 60;
        public int SignatureNonceLength { get; set; } = 16;
        public bool EnforceTeeVerification { get; set; } = true;
        public bool EnableAuditLogging { get; set; } = true;
    }
    
    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; } = 5;
        public int SuccessThreshold { get; set; } = 3;
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}

public enum WeightingStrategy
{
    SimpleAverage,
    ConfidenceWeighted,
    VolumeWeighted,
    ConfidenceVolume
}
```

### Environment-Based Configuration

The system supports multiple environment configurations:

```json
{
  "PriceFeed": {
    "Environment": "Production",
    "DataSources": {
      "EnabledSources": ["CoinGecko", "Kraken", "Coinbase"],
      "RequestTimeoutSeconds": 30,
      "MaxParallelRequests": 5
    },
    "EpicChain": {
      "RpcEndpoint": "https://testnet5-seed.epic-chain.org:20332",
      "ContractHash": "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc",
      "MinimumGasBalance": 5.0
    },
    "Aggregation": {
      "MinimumConfidenceThreshold": 0.7,
      "MaximumDeviationPercentage": 25.0,
      "MinimumSourceCount": 2
    },
    "Scheduling": {
      "CronExpression": "0 */2 * * *",
      "ExecutionTimeoutMinutes": 30
    },
    "Security": {
      "AttestationValidityMinutes": 60,
      "EnforceTeeVerification": true
    }
  }
}
```

## Monitoring and Alerting System

### Comprehensive Health Monitoring

```csharp
public class HealthMonitor : IHealthMonitor
{
    private readonly IEnumerable<IPriceDataSource> _dataSources;
    private readonly IEpiChainService _epicChainService;
    private readonly ILogger<HealthMonitor> _logger;
    
    private readonly Dictionary<string, DataSourceHealth> _dataSourceHealth = 
        new Dictionary<string, DataSourceHealth>();
    private BlockchainHealth _blockchainHealth;
    
    public async Task<SystemHealth> CheckSystemHealthAsync()
    {
        var tasks = new List<Task>
        {
            CheckDataSourcesHealthAsync(),
            CheckBlockchainHealthAsync(),
            CheckContractHealthAsync()
        };
        
        await Task.WhenAll(tasks);
        
        return new SystemHealth
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = CalculateOverallStatus(),
            DataSources = _dataSourceHealth,
            Blockchain = _blockchainHealth,
            Recommendations = GenerateRecommendations()
        };
    }
    
    private async Task CheckDataSourcesHealthAsync()
    {
        foreach (var source in _dataSources)
        {
            try
            {
                var status = await source.GetStatusAsync();
                _dataSourceHealth[source.Name] = new DataSourceHealth
                {
                    Status = status,
                    Reliability = source.ReliabilityScore,
                    LastSuccess = status.LastSuccessfulCall,
                    ErrorRate = status.ErrorRate,
                    ResponseTime = status.AverageResponseTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get health status for {Source}", source.Name);
                _dataSourceHealth[source.Name] = DataSourceHealth.Unhealthy(ex.Message);
            }
        }
    }
    
    private async Task CheckBlockchainHealthAsync()
    {
        try
        {
            var blockchainStatus = await _epicChainService.GetBlockchainStatusAsync();
            var gasBalance = await _epicChainService.GetGasBalanceAsync();
            
            _blockchainHealth = new BlockchainHealth
            {
                Status = blockchainStatus.IsAvailable ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                BlockHeight = blockchainStatus.BlockHeight,
                PeerCount = blockchainStatus.PeerCount,
                GasBalance = gasBalance,
                LastBlockTime = blockchainStatus.LastBlockTime,
                SyncStatus = blockchainStatus.SyncStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check blockchain health");
            _blockchainHealth = BlockchainHealth.Unhealthy(ex.Message);
        }
    }
}
```

### Alerting and Notification System

```csharp
public class AlertManager : IAlertManager
{
    private readonly List<IAlertHandler> _alertHandlers;
    private readonly ILogger<AlertManager> _logger;
    
    private readonly Dictionary<AlertType, AlertThreshold> _thresholds;
    private readonly Dictionary<string, DateTime> _lastAlertTimes = new Dictionary<string, DateTime>();
    
    public async Task ProcessAlertsAsync(SystemHealth healthStatus)
    {
        var alerts = new List<Alert>();
        
        // Check data source alerts
        foreach (var (sourceName, health) in healthStatus.DataSources)
        {
            if (health.Status == HealthStatus.Unhealthy)
            {
                alerts.Add(CreateAlert(AlertType.DataSourceDown, 
                    $"Data source {sourceName} is down", 
                    severity: AlertSeverity.High));
            }
            else if (health.Reliability < 0.5m)
            {
                alerts.Add(CreateAlert(AlertType.DataSourceDegraded,
                    $"Data source {sourceName} reliability is low: {health.Reliability:P0}",
                    severity: AlertSeverity.Medium));
            }
        }
        
        // Check blockchain alerts
        if (healthStatus.Blockchain.Status == HealthStatus.Unhealthy)
        {
            alerts.Add(CreateAlert(AlertType.BlockchainUnavailable,
                "Blockchain connection unavailable",
                severity: AlertSeverity.Critical));
        }
        else if (healthStatus.Blockchain.GasBalance < 1.0m)
        {
            alerts.Add(CreateAlert(AlertType.LowGasBalance,
                $"Low gas balance: {healthStatus.Blockchain.GasBalance}",
                severity: AlertSeverity.High));
        }
        
        // Process alerts through all handlers
        foreach (var alert in alerts)
        {
            if (ShouldSendAlert(alert))
            {
                foreach (var handler in _alertHandlers)
                {
                    try
                    {
                        await handler.HandleAlertAsync(alert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process alert through {Handler}", handler.GetType().Name);
                    }
                }
                
                _lastAlertTimes[alert.Id] = DateTime.UtcNow;
            }
        }
    }
    
    private bool ShouldSendAlert(Alert alert)
    {
        // Check if we recently sent a similar alert
        if (_lastAlertTimes.TryGetValue(alert.Id, out var lastSent))
        {
            var cooldown = GetCooldownPeriod(alert.Severity);
            if (DateTime.UtcNow - lastSent < cooldown)
            {
                return false; // Respect alert cooldown
            }
        }
        
        return true;
    }
}
```

## Advanced Error Handling and Resilience

### Circuit Breaker Pattern Implementation

```csharp
public class CircuitBreaker
{
    private readonly string _name;
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _resetTimeout;
    
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private int _successCount = 0;
    private DateTime _lastStateChange = DateTime.UtcNow;
    private readonly List<DateTime> _recentFailures = new List<DateTime>();
    private readonly List<TimeSpan> _recentResponseTimes = new List<TimeSpan>();
    
    public CircuitBreaker(string name, int failureThreshold = 5, int successThreshold = 3, 
        TimeSpan resetTimeout = default)
    {
        _name = name;
        _failureThreshold = failureThreshold;
        _successThreshold = successThreshold;
        _resetTimeout = resetTimeout == default ? TimeSpan.FromMinutes(5) : resetTimeout;
    }
    
    public bool IsOpen => _state == CircuitState.Open;
    public bool IsHalfOpen => _state == CircuitState.HalfOpen;
    public bool IsClosed => _state == CircuitState.Closed;
    
    public decimal SuccessRate => 
        _recentFailures.Count == 0 ? 1.0m : 
        1.0m - ((decimal)_recentFailures.Count / (_recentFailures.Count + _successCount));
    
    public TimeSpan AverageResponseTime =>
        _recentResponseTimes.Count == 0 ? TimeSpan.Zero :
        TimeSpan.FromMilliseconds(_recentResponseTimes.Average(t => t.TotalMilliseconds));
    
    public void RecordSuccess(TimeSpan responseTime)
    {
        _recentResponseTimes.Add(responseTime);
        if (_recentResponseTimes.Count > 100) _recentResponseTimes.RemoveAt(0);
        
        if (_state == CircuitState.HalfOpen)
        {
            _successCount++;
            if (_successCount >= _successThreshold)
            {
                CloseCircuit();
            }
        }
        else if (_state == CircuitState.Closed)
        {
            _successCount = Math.Min(_successCount + 1, _successThreshold);
        }
    }
    
    public void RecordFailure()
    {
        _recentFailures.Add(DateTime.UtcNow);
        // Keep only failures from the last hour
        _recentFailures.RemoveAll(t => DateTime.UtcNow - t > TimeSpan.FromHours(1));
        
        if (_state == CircuitState.Closed)
        {
            _failureCount++;
            if (_failureCount >= _failureThreshold)
            {
                OpenCircuit();
            }
        }
        else if (_state == CircuitState.HalfOpen)
        {
            OpenCircuit(); // Immediate failure in half-open state
        }
    }
    
    private void OpenCircuit()
    {
        _state = CircuitState.Open;
        _failureCount = 0;
        _successCount = 0;
        _lastStateChange = DateTime.UtcNow;
        
        // Schedule automatic reset attempt
        Task.Delay(_resetTimeout).ContinueWith(_ => AttemptReset());
    }
    
    private void AttemptReset()
    {
        _state = CircuitState.HalfOpen;
        _lastStateChange = DateTime.UtcNow;
    }
    
    private void CloseCircuit()
    {
        _state = CircuitState.Closed;
        _failureCount = 0;
        _successCount = 0;
        _lastStateChange = DateTime.UtcNow;
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (IsOpen)
        {
            throw new CircuitBreakerOpenException(_name);
        }
        
        try
        {
            var startTime = DateTime.UtcNow;
            var result = await action();
            var responseTime = DateTime.UtcNow - startTime;
            
            RecordSuccess(responseTime);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure();
            throw new CircuitBreakerOperationException(_name, ex);
        }
    }
}
```

### Retry Policy Framework

```csharp
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffFactor;
    private readonly TimeSpan _maxDelay;
    private readonly Predicate<Exception> _retryCondition;
    
    public RetryPolicy(int maxRetries = 3, TimeSpan initialDelay = default, 
        double backoffFactor = 2.0, TimeSpan maxDelay = default,
        Predicate<Exception> retryCondition = null)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay == default ? TimeSpan.FromSeconds(1) : initialDelay;
        _backoffFactor = backoffFactor;
        _maxDelay = maxDelay == default ? TimeSpan.FromSeconds(30) : maxDelay;
        _retryCondition = retryCondition ?? (ex => true);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, 
        Action<RetryContext> onRetry = null)
    {
        var retryCount = 0;
        var exceptions = new List<Exception>();
        
        while (true)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (_retryCondition(ex) && retryCount < _maxRetries)
            {
                retryCount++;
                exceptions.Add(ex);
                
                var delay = CalculateDelay(retryCount);
                
                onRetry?.Invoke(new RetryContext
                {
                    RetryCount = retryCount,
                    Exception = ex,
                    Delay = delay
                });
                
                await Task.Delay(delay);
            }
        }
    }
    
    private TimeSpan CalculateDelay(int retryCount)
    {
        if (retryCount == 1) return _initialDelay;
        
        var delay = TimeSpan.FromMilliseconds(
            _initialDelay.TotalMilliseconds * Math.Pow(_backoffFactor, retryCount - 1));
            
        return delay > _maxDelay ? _maxDelay : delay;
    }
}

public class RetryContext
{
    public int RetryCount { get; set; }
    public Exception Exception { get; set; }
    public TimeSpan Delay { get; set; }
}
```

## Performance Optimization and Scaling

### Batch Processing Optimization

```csharp
public class BatchProcessor
{
    private readonly int _maxBatchSize;
    private readonly TimeSpan _maxBatchTime;
    private readonly ILogger<BatchProcessor> _logger;
    
    private readonly List<PriceUpdate> _pendingUpdates = new List<PriceUpdate>();
    private DateTime _lastBatchTime = DateTime.UtcNow;
    private readonly object _lock = new object();
    
    public BatchProcessor(int maxBatchSize = 50, TimeSpan maxBatchTime = default)
    {
        _maxBatchSize = maxBatchSize;
        _maxBatchTime = maxBatchTime == default ? TimeSpan.FromSeconds(30) : maxBatchTime;
    }
    
    public void AddPriceUpdate(PriceUpdate update)
    {
        lock (_lock)
        {
            _pendingUpdates.Add(update);
            
            // Check if we should process batch
            if (_pendingUpdates.Count >= _maxBatchSize || 
                DateTime.UtcNow - _lastBatchTime >= _maxBatchTime)
            {
                ProcessBatch();
            }
        }
    }
    
    public async Task ProcessBatchAsync()
    {
        List<PriceUpdate> batch;
        lock (_lock)
        {
            if (_pendingUpdates.Count == 0) return;
            
            batch = _pendingUpdates.Take(_maxBatchSize).ToList();
            _pendingUpdates.RemoveRange(0, batch.Count);
            _lastBatchTime = DateTime.UtcNow;
        }
        
        if (batch.Count > 0)
        {
            await ProcessBatchInternalAsync(batch);
        }
    }
    
    private async Task ProcessBatchInternalAsync(List<PriceUpdate> batch)
    {
        try
        {
            // Group by symbol to get latest price for each
            var latestPrices = batch
                .GroupBy(u => u.Symbol)
                .Select(g => g.OrderByDescending(u => u.Timestamp).First())
                .ToList();
                
            // Prepare batch transaction
            var symbols = latestPrices.Select(p => p.Symbol).ToArray();
            var prices = latestPrices.Select(p => p.Price).ToArray();
            
            // Use retry policy for robustness
            var retryPolicy = new RetryPolicy(maxRetries: 3, 
                initialDelay: TimeSpan.FromSeconds(1),
                backoffFactor: 2.0);
                
            await retryPolicy.ExecuteAsync(async () =>
            {
                await _transactionProcessor.SubmitPriceUpdatesAsync(
                    latestPrices, _contractHash);
                return true;
            });
            
            _logger.LogInformation("Processed batch of {Count} price updates", latestPrices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch of {Count} updates", batch.Count);
            // Re-add failed updates to pending list
            lock (_lock)
            {
                _pendingUpdates.AddRange(batch);
            }
        }
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch processor loop");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        
        // Process any remaining updates before shutdown
        await ProcessBatchAsync();
    }
}
```

### Memory and Resource Management

```csharp
public class ResourceManager : IResourceManager
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly ILogger<ResourceManager> _logger;
    
    private const long MemoryWarningThreshold = 500 * 1024 * 1024; // 500MB
    private const float CpuWarningThreshold = 80.0f; // 80%
    
    public ResourceManager(ILogger<ResourceManager> logger)
    {
        _logger = logger;
        
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters");
        }
    }
    
    public ResourceUsage GetCurrentUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var cpuUsage = _cpuCounter?.NextValue() ?? 0;
            var availableMemory = _memoryCounter?.NextValue() * 1024 * 1024 ?? 0;
            
            return new ResourceUsage
            {
                ProcessMemory = process.WorkingSet64,
                AvailableSystemMemory = availableMemory,
                CpuUsage = cpuUsage,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get resource usage");
            return ResourceUsage.Unknown;
        }
    }
    
    public bool ShouldThrottle()
    {
        var usage = GetCurrentUsage();
        
        // Throttle if memory is low or CPU is high
        return usage.AvailableSystemMemory < MemoryWarningThreshold || 
               usage.CpuUsage > CpuWarningThreshold;
    }
    
    public void CleanupResources()
    {
        try
        {
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Clear various caches if they exist
            ClearResponseCaches();
            ClearTempData();
            
            _logger.LogInformation("Resource cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup resources");
        }
    }
}
```

## Comprehensive Testing Strategy

### Unit Test Suite

```csharp
[TestFixture]
public class PriceAggregatorTests
{
    private SmartPriceAggregator _aggregator;
    private Mock<ILogger<SmartPriceAggregator>> _loggerMock;
    
    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SmartPriceAggregator>>();
        _aggregator = new SmartPriceAggregator(_loggerMock.Object);
    }
    
    [Test]
    public void Aggregate_WithMultipleSources_ReturnsWeightedAverage()
    {
        // Arrange
        var priceData = new[]
        {
            new PriceData { Symbol = "BTCUSDT", Price = 50000, ConfidenceScore = 0.9m, Source = "CoinGecko" },
            new PriceData { Symbol = "BTCUSDT", Price = 50100, ConfidenceScore = 0.8m, Source = "Kraken" },
            new PriceData { Symbol = "BTCUSDT", Price = 49900, ConfidenceScore = 0.7m, Source = "Coinbase" }
        };
        
        // Act
        var result = _aggregator.Aggregate(priceData);
        
        // Assert
        Assert.That(result.Prices, Has.Exactly(1).Items);
        var btcPrice = result.Prices.First();
        Assert.That(btcPrice.Symbol, Is.EqualTo("BTCUSDT"));
        Assert.That(btcPrice.Price, Is.InRange(49900, 50100));
        Assert.That(btcPrice.ConfidenceScore, Is.GreaterThan(0.7m));
        Assert.That(btcPrice.SourceCount, Is.EqualTo(3));
    }
    
    [Test]
    public void Aggregate_WithLowConfidenceData_FiltersOutliers()
    {
        // Arrange
        var priceData = new[]
        {
            new PriceData { Symbol = "ETHUSDT", Price = 3000, ConfidenceScore = 0.9m },
            new PriceData { Symbol = "ETHUSDT", Price = 3100, ConfidenceScore = 0.8m },
            new PriceData { Symbol = "ETHUSDT", Price = 2000, ConfidenceScore = 0.2m } // Low confidence outlier
        };
        
        // Act
        var result = _aggregator.Aggregate(priceData);
        
        // Assert
        var ethPrice = result.Prices.First();
        Assert.That(ethPrice.Price, Is.InRange(3000, 3100)); // Should exclude the outlier
    }
}

[TestFixture]
public class DualSignatureProtocolTests
{
    private DualSignatureProtocol _protocol;
    private Mock<ITeeService> _teeServiceMock;
    private Mock<IMasterSigningService> _masterSigningServiceMock;
    private Mock<IAttestationService> _attestationServiceMock;
    
    [SetUp]
    public void Setup()
    {
        _teeServiceMock = new Mock<ITeeService>();
        _masterSigningServiceMock = new Mock<IMasterSigningService>();
        _attestationServiceMock = new Mock<IAttestationService>();
        
        _protocol = new DualSignatureProtocol(
            _teeServiceMock.Object,
            _masterSigningServiceMock.Object,
            _attestationServiceMock.Object);
    }
    
    [Test]
    public async Task CreateDualSignatureAsync_ValidTransaction_ReturnsCompleteSignature()
    {
        // Arrange
        var transaction = new Transaction { Data = "test data" };
        var teeContext = new TeeContext { TeeAccount = new Account { Address = "tee_address" } };
        var masterAccount = new MasterAccount { Address = "master_address" };
        
        _teeServiceMock.Setup(t => t.CreateAttestation(It.IsAny<byte[]>(), It.IsAny<TeeContext>()))
            .ReturnsAsync(new AttestationReport { SignedData = new byte[] { 1, 2, 3 } });
        _teeServiceMock.Setup(t => t.SignDataAsync(It.IsAny<byte[]>(), It.IsAny<SigningContext>()))
            .ReturnsAsync(new byte[] { 4, 5, 6 });
        _masterSigningServiceMock.Setup(m => m.SignAsync(It.IsAny<byte[]>(), It.IsAny<MasterAccount>()))
            .ReturnsAsync(new byte[] { 7, 8, 9 });
        
        // Act
        var result = await _protocol.CreateDualSignatureAsync(transaction, teeContext, masterAccount);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.TeeSignature, Is.Not.Empty);
        Assert.That(result.MasterSignature, Is.Not.Empty);
        Assert.That(result.TeeAttestation, Is.Not.Null);
        
        _teeServiceMock.Verify(t => t.CreateAttestation(It.IsAny<byte[]>(), It.IsAny<TeeContext>()), Times.Once);
        _teeServiceMock.Verify(t => t.SignDataAsync(It.IsAny<byte[]>(), It.IsAny<SigningContext>()), Times.Once);
        _masterSigningServiceMock.Verify(m => m.SignAsync(It.IsAny<byte[]>(), It.IsAny<MasterAccount>()), Times.Once);
    }
}
```

### Integration Test Suite

```csharp
[TestFixture]
[Category("Integration")]
public class PriceFeedIntegrationTests
{
    private IServiceProvider _serviceProvider;
    private IPriceFeedService _priceFeedService;
    private Mock<IEpicChainService> _epicChainServiceMock;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Configure services for integration testing
        var services = new ServiceCollection();
        
        // Register real implementations for most services
        services.AddSingleton<IPriceDataCollector, MultiSourceDataCollector>();
        services.AddSingleton<IPriceAggregator, SmartPriceAggregator>();
        services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();
        
        // Mock blockchain service to avoid real network calls
        _epicChainServiceMock = new Mock<IEpicChainService>();
        services.AddSingleton(_epicChainServiceMock.Object);
        
        // Register test data sources
        services.AddSingleton<IPriceDataSource, TestCoinGeckoDataSource>();
        services.AddSingleton<IPriceDataSource, TestKrakenDataSource>();
        
        _serviceProvider = services.BuildServiceProvider();
        _priceFeedService = _serviceProvider.GetRequiredService<IPriceFeedService>();
    }
    
    [Test]
    public async Task FullPriceFeedFlow_WithTestData_CompletesSuccessfully()
    {
        // Arrange
        var symbols = new[] { "BTCUSDT", "ETHUSDT", "XPRUSDT" };
        
        // Mock successful transaction submission
        _epicChainServiceMock.Setup(e => e.SubmitTransaction(It.IsAny<Transaction>()))
            .ReturnsAsync(new TransactionResult { Status = TransactionStatus.Succeeded });
        
        // Act
        var result = await _priceFeedService.ExecutePriceFeedAsync(symbols);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ProcessedSymbols, Is.EquivalentTo(symbols));
        
        _epicChainServiceMock.Verify(e => e.SubmitTransaction(It.IsAny<Transaction>()), Times.AtLeastOnce);
    }
    
    [Test]
    public async Task PriceFeed_WithFailingDataSource_StillCompletesWithOtherSources()
    {
        // Arrange
        var symbols = new[] { "BTCUSDT" };
        
        // Setup one failing data source
        var failingSource = _serviceProvider.GetServices<IPriceDataSource>()
            .First(s => s is TestCoinGeckoDataSource);
        failingSource.SetShouldFail(true);
        
        // Act
        var result = await _priceFeedService.ExecutePriceFeedAsync(symbols);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Warnings, Is.Not.Empty); // Should have warnings about failing source
    }
}

public class TestCoinGeckoDataSource : BasePriceDataSource
{
    private bool _shouldFail = false;
    
    public TestCoinGeckoDataSource() : base(new HttpClient(), NullLogger<TestCoinGeckoDataSource>.Instance)
    {
    }
    
    public void SetShouldFail(bool shouldFail) => _shouldFail = shouldFail;
    
    protected override async Task<PriceResult[]> FetchPricesAsync(IEnumerable<string> symbols)
    {
        if (_shouldFail)
        {
            throw new HttpRequestException("Test failure");
        }
        
        await Task.Delay(10); // Simulate network delay
        
        return symbols.Select(symbol => PriceResult.Success(
            symbol, 
            GetTestPrice(symbol),
            DateTime.UtcNow,
            0.9m,
            "TestCoinGecko")).ToArray();
    }
    
    private decimal GetTestPrice(string symbol)
    {
        return symbol switch
        {
            "BTCUSDT" => 50000 + Random.Shared.Next(-100, 100),
            "ETHUSDT" => 3000 + Random.Shared.Next(-50, 50),
            "XPRUSDT" => 0.5m + (decimal)Random.Shared.NextDouble() * 0.1m,
            _ => 100 + Random.Shared.Next(-10, 10)
        };
    }
}
```

## Deployment and Operations

### Docker Containerization

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/PriceFeed.Core/PriceFeed.Core.csproj", "src/PriceFeed.Core/"]
COPY ["src/PriceFeed.Infrastructure/PriceFeed.Infrastructure.csproj", "src/PriceFeed.Infrastructure/"]
COPY ["src/PriceFeed.Console/PriceFeed.Console.csproj", "src/PriceFeed.Console/"]

# Restore dependencies
RUN dotnet restore "src/PriceFeed.Console/PriceFeed.Console.csproj"

# Copy everything else
COPY . .
WORKDIR "/src/src/PriceFeed.Console"

# Build and publish
RUN dotnet publish "PriceFeed.Console.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Install required system dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libssl-dev \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r pricefeed && useradd -r -g pricefeed pricefeed
USER pricefeed

# Copy published application
COPY --from=build --chown=pricefeed:pricefeed /app/publish .

# Create volume for logs and data
VOLUME ["/app/data", "/app/logs"]

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "PriceFeed.Console.dll"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: epicchain-price-feed
  namespace: blockchain
  labels:
    app: epicchain-price-feed
    component: oracle
spec:
  replicas: 2
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: epicchain-price-feed
  template:
    metadata:
      labels:
        app: epicchain-price-feed
        component: oracle
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: price-feed-service-account
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        runAsGroup: 1000
        fsGroup: 1000
      containers:
      - name: price-feed
        image: ghcr.io/epicchainlabs/epicchain-price-feed:latest
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8080
          name: metrics
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: DOTNET_GCHeapCount
          value: "2"
        - name: DOTNET_ThreadPoolMinThreads
          value: "4"
        - name: DOTNET_ThreadPoolMaxThreads
          value: "16"
        envFrom:
        - secretRef:
            name: price-feed-secrets
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 1
        volumeMounts:
        - name: data-volume
          mountPath: /app/data
          subPath: data
        - name: data-volume
          mountPath: /app/logs
          subPath: logs
      volumes:
      - name: data-volume
        persistentVolumeClaim:
          claimName: price-feed-data-pvc
      - name: config-volume
        configMap:
          name: price-feed-config
---
apiVersion: v1
kind: Service
metadata:
  name: epicchain-price-feed
  namespace: blockchain
  labels:
    app: epicchain-price-feed
    component: oracle
spec:
  selector:
    app: epicchain-price-feed
  ports:
  - port: 8080
    targetPort: 8080
    name: metrics
  type: ClusterIP
```

### GitHub Actions Workflow

```yaml
name: Price Feed Service
on:
  schedule:
    - cron: '*/10 * * * *'  # Every 10 minutes
  workflow_dispatch:        # Manual trigger
  push:
    branches: [ main ]
    paths:
      - 'src/PriceFeed.Console/**'
      - 'src/PriceFeed.Core/**'
      - 'src/PriceFeed.Infrastructure/**'
      - '.github/workflows/price-feed.yml'

jobs:
  price-feed:
    name: Run Price Feed
    runs-on: ubuntu-latest
    timeout-minutes: 30
    environment: production
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      with:
        sparse-checkout: |
          src/PriceFeed.Console
          src/PriceFeed.Core
          src/PriceFeed.Infrastructure
          scripts
        sparse-checkout-cone: false
        
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'
        
    - name: Restore dependencies
      run: dotnet restore src/PriceFeed.Console/PriceFeed.Console.csproj
      
    - name: Build
      run: dotnet build src/PriceFeed.Console/PriceFeed.Console.csproj --configuration Release --no-restore
      
    - name: Test
      run: dotnet test src/PriceFeed.Console/PriceFeed.Console.csproj --configuration Release --no-build --verbosity normal
      
    - name: Run Price Feed
      env:
        EPICCHAIN_RPC_ENDPOINT: ${{ secrets.EPICCHAIN_RPC_ENDPOINT }}
        CONTRACT_SCRIPT_HASH: ${{ secrets.CONTRACT_SCRIPT_HASH }}
        TEE_ACCOUNT_ADDRESS: ${{ secrets.TEE_ACCOUNT_ADDRESS }}
        TEE_ACCOUNT_PRIVATE_KEY: ${{ secrets.TEE_ACCOUNT_PRIVATE_KEY }}
        MASTER_ACCOUNT_ADDRESS: ${{ secrets.MASTER_ACCOUNT_ADDRESS }}
        MASTER_ACCOUNT_PRIVATE_KEY: ${{ secrets.MASTER_ACCOUNT_PRIVATE_KEY }}
        COINGECKO_API_KEY: ${{ secrets.COINGECKO_API_KEY }}
        KRAKEN_API_KEY: ${{ secrets.KRAKEN_API_KEY }}
        KRAKEN_API_SECRET: ${{ secrets.KRAKEN_API_SECRET }}
        SYMBOLS: "BTCUSDT,ETHUSDT,ADAUSDT,BNBUSDT,XRPUSDT,SOLUSDT,MATICUSDT,DOTUSDT,LINKUSDT,UNIUSDT"
      run: |
        dotnet run --project src/PriceFeed.Console/PriceFeed.Console.csproj \
          --configuration Release \
          --continuous \
          --duration 5 \
          --interval 15
          
    - name: Upload logs
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: price-feed-logs
        path: |
          logs/*.log
          data/*.json
        retention-days: 7
```

## Advanced Monitoring and Observability

### Prometheus Metrics Integration

```csharp
public class MetricsCollector : IMetricsCollector
{
    private readonly Counter _priceUpdatesCounter;
    private readonly Counter _dataSourceErrorsCounter;
    private readonly Gauge _dataSourceReliabilityGauge;
    private readonly Histogram _dataRetrievalDuration;
    private readonly Gauge _gasBalanceGauge;
    
    public MetricsCollector()
    {
        var factory = Metrics.DefaultFactory;
        
        _priceUpdatesCounter = factory.CreateCounter(
            "pricefeed_updates_total", 
            "Total number of price updates processed",
            "symbol", "source");
            
        _dataSourceErrorsCounter = factory.CreateCounter(
            "pricefeed_data_source_errors_total",
            "Total number of data source errors",
            "source", "type");
            
        _dataSourceReliabilityGauge = factory.CreateGauge(
            "pricefeed_data_source_reliability",
            "Reliability score of data sources",
            "source");
            
        _dataRetrievalDuration = factory.CreateHistogram(
            "pricefeed_data_retrieval_duration_seconds",
            "Time taken to retrieve data from sources",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 },
                LabelNames = new[] { "source" }
            });
            
        _gasBalanceGauge = factory.CreateGauge(
            "pricefeed_gas_balance",
            "Current gas balance for transactions",
            "account");
    }
    
    public void RecordPriceUpdate(string symbol, string source, decimal price)
    {
        _priceUpdatesCounter.WithLabels(symbol, source).Inc();
    }
    
    public void RecordDataSourceError(string source, string errorType)
    {
        _dataSourceErrorsCounter.WithLabels(source, errorType).Inc();
    }
    
    public void SetDataSourceReliability(string source, decimal reliability)
    {
        _dataSourceReliabilityGauge.WithLabels(source).Set((double)reliability);
    }
    
    public IDisposable MeasureDataRetrieval(string source)
    {
        return _dataRetrievalDuration.WithLabels(source).NewTimer();
    }
    
    public void SetGasBalance(string account, decimal balance)
    {
        _gasBalanceGauge.WithLabels(account).Set((double)balance);
    }
}
```

### Structured Logging Configuration

```csharp
public static class LoggingConfiguration
{
    public static ILoggerFactory CreateLoggerFactory(IConfiguration configuration)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            
            // Console logging for development
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();
                builder.AddConsole(options => options.FormatterName = nameof(CustomConsoleFormatter));
            }
            else
            {
                // JSON logging for production
                builder.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                    options.UseUtcTimestamp = true;
                    options.JsonWriterOptions = new JsonWriterOptions
                    {
                        Indented = false
                    };
                });
            }
            
            // Application Insights for Azure
            var appInsightsKey = configuration["ApplicationInsights:InstrumentationKey"];
            if (!string.IsNullOrEmpty(appInsightsKey))
            {
                builder.AddApplicationInsights(
                    configureTelemetryConfiguration: config => config.ConnectionString = appInsightsKey,
                    configureApplicationInsightsLoggerOptions: options => {});
            }
            
            // File logging for persistence
            builder.AddFile("logs/pricefeed-{Date}.log", options =>
            {
                options.FileSizeLimitBytes = 10 * 1024 * 1024; // 10MB
                options.RetainedFileCountLimit = 31; // Keep 31 days
                options.FormatLogFileName = fName => string.Format(fName, DateTime.UtcNow);
                options.OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";
            });
            
            // Set minimum log level from configuration
            var minLogLevel = configuration.GetValue<LogLevel>("Logging:LogLevel:Default");
            builder.SetMinimumLevel(minLogLevel);
        });
    }
}

public class CustomConsoleFormatter : ConsoleFormatter
{
    public CustomConsoleFormatter(IOptions<CustomConsoleFormatterOptions> options) 
        : base(nameof(CustomConsoleFormatter))
    {
    }
    
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (message == null) return;
        
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = GetLogLevelString(logEntry.LogLevel);
        
        textWriter.Write($"{timestamp} {level} ");
        
        if (logEntry.Exception != null)
        {
            textWriter.Write($"[{logEntry.Exception.GetType().Name}] ");
        }
        
        textWriter.WriteLine(message);
        
        if (logEntry.Exception != null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }
    }
    
    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "FAIL",
            LogLevel.Critical => "CRIT",
            _ => "UNKN"
        };
    }
}
```

## Security Hardening

### Secure Secret Management

```csharp
public class SecureSecretManager : ISecretManager
{
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<SecureSecretManager> _logger;
    private readonly Dictionary<string, Lazy<Task<string>>> _secretCache = new Dictionary<string, Lazy<Task<string>>>();
    
    public SecureSecretManager(IDataProtectionProvider dataProtectionProvider, ILogger<SecureSecretManager> logger)
    {
        _dataProtector = dataProtectionProvider.CreateProtector("EpicChain.PriceFeed.Secrets");
        _logger = logger;
    }
    
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_secretCache.TryGetValue(secretName, out var lazySecret))
        {
            return await lazySecret.Value;
        }
        
        var newLazy = new Lazy<Task<string>>(async () =>
        {
            try
            {
                // Try environment variables first
                var envValue = Environment.GetEnvironmentVariable(secretName);
                if (!string.IsNullOrEmpty(envValue))
                {
                    _logger.LogDebug("Retrieved secret {SecretName} from environment", secretName);
                    return envValue;
                }
                
                // Try Azure Key Vault
                if (IsAzureKeyVaultConfigured())
                {
                    var keyVaultValue = await GetSecretFromAzureKeyVaultAsync(secretName, cancellationToken);
                    if (!string.IsNullOrEmpty(keyVaultValue))
                    {
                        _logger.LogDebug("Retrieved secret {SecretName} from Azure Key Vault", secretName);
                        return keyVaultValue;
                    }
                }
                
                // Try AWS Secrets Manager
                if (IsAwsSecretsManagerConfigured())
                {
                    var awsValue = await GetSecretFromAwsSecretsManagerAsync(secretName, cancellationToken);
                    if (!string.IsNullOrEmpty(awsValue))
                    {
                        _logger.LogDebug("Retrieved secret {SecretName} from AWS Secrets Manager", secretName);
                        return awsValue;
                    }
                }
                
                // Try local secure storage
                var localValue = await GetSecretFromLocalStorageAsync(secretName);
                if (!string.IsNullOrEmpty(localValue))
                {
                    _logger.LogDebug("Retrieved secret {SecretName} from local secure storage", secretName);
                    return localValue;
                }
                
                throw new SecretNotFoundException($"Secret {secretName} not found in any configured storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret {SecretName}", secretName);
                throw;
            }
        });
        
        _secretCache[secretName] = newLazy;
        return await newLazy.Value;
    }
    
    public async Task StoreSecretAsync(string secretName, string secretValue, StoragePreference preference = StoragePreference.Environment)
    {
        try
        {
            // Encrypt the secret before storage
            var encryptedValue = _dataProtector.Protect(secretValue);
            
            switch (preference)
            {
                case StoragePreference.Environment:
                    Environment.SetEnvironmentVariable(secretName, encryptedValue);
                    break;
                    
                case StoragePreference.AzureKeyVault:
                    await StoreSecretInAzureKeyVaultAsync(secretName, encryptedValue);
                    break;
                    
                case StoragePreference.AwsSecretsManager:
                    await StoreSecretInAwsSecretsManagerAsync(secretName, encryptedValue);
                    break;
                    
                case StoragePreference.LocalSecureStorage:
                    await StoreSecretInLocalStorageAsync(secretName, encryptedValue);
                    break;
            }
            
            _logger.LogInformation("Stored secret {SecretName} in {Storage}", secretName, preference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret {SecretName}", secretName);
            throw;
        }
    }
    
    public void InvalidateSecret(string secretName)
    {
        _secretCache.Remove(secretName);
        _logger.LogDebug("Invalidated cached secret {SecretName}", secretName);
    }
}
```

### Comprehensive Security Audit Logging

```csharp
public class SecurityAuditLogger : IAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;
    private readonly List<AuditEvent> _recentEvents = new List<AuditEvent>();
    private readonly object _lock = new object();
    
    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger)
    {
        _logger = logger;
    }
    
    public void LogSecurityEvent(SecurityEventType eventType, string message, 
        string userId = null, string resourceId = null, Dictionary<string, object> details = null)
    {
        var auditEvent = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Message = message,
            UserId = userId,
            ResourceId = resourceId,
            Details = details ?? new Dictionary<string, object>(),
            SourceAddress = GetSourceAddress(),
            SessionId = GetSessionId()
        };
        
        lock (_lock)
        {
            _recentEvents.Add(auditEvent);
            // Keep only the last 1000 events
            if (_recentEvents.Count > 1000)
            {
                _recentEvents.RemoveAt(0);
            }
        }
        
        // Log to structured logging
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["AuditEventType"] = eventType.ToString(),
            ["UserId"] = userId,
            ["ResourceId"] = resourceId,
            ["SourceAddress"] = auditEvent.SourceAddress,
            ["SessionId"] = auditEvent.SessionId
        }))
        {
            _logger.LogInformation("Security audit: {Message} - {Details}", 
                message, 
                details != null ? JsonSerializer.Serialize(details) : "No details");
        }
    }
    
    public IReadOnlyList<AuditEvent> GetRecentEvents(TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        
        lock (_lock)
        {
            return _recentEvents
                .Where(e => e.Timestamp >= cutoff)
                .OrderByDescending(e => e.Timestamp)
                .ToList()
                .AsReadOnly();
        }
    }
    
    public async Task ExportAuditLogAsync(Stream outputStream, DateTime start, DateTime end)
    {
        List<AuditEvent> eventsToExport;
        
        lock (_lock)
        {
            eventsToExport = _recentEvents
                .Where(e => e.Timestamp >= start && e.Timestamp <= end)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
        
        await using var writer = new StreamWriter(outputStream);
        await writer.WriteLineAsync("Timestamp,EventType,UserId,ResourceId,Message,Details");
        
        foreach (var event in eventsToExport)
        {
            var detailsJson = event.Details != null ? JsonSerializer.Serialize(event.Details) : "";
            await writer.WriteLineAsync(
                $"{event.Timestamp:O}," +
                $"{event.EventType}," +
                $"{EscapeCsv(event.UserId)}," +
                $"{EscapeCsv(event.ResourceId)}," +
                $"{EscapeCsv(event.Message)}," +
                $"{EscapeCsv(detailsJson)}");
        }
    }
    
    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
```

## Conclusion

The EpicChain Price Feed Service with Trusted Execution Environment represents a state-of-the-art blockchain oracle solution that combines security, reliability, and performance. Through its sophisticated architecture, comprehensive security model, and robust implementation, it provides a foundation for decentralized applications that require trustworthy price data.

The system's key strengths include:

1. **Security First**: TEE integration and dual-signature transactions ensure data authenticity
2. **High Availability**: Multiple data sources with fallback mechanisms guarantee continuous operation
3. **Scalability**: Modular design supports easy expansion to new data sources and cryptocurrencies
4. **Resilience**: Comprehensive error handling and circuit breakers maintain service stability
5. **Transparency**: Detailed logging and monitoring provide full operational visibility
6. **Compliance**: Security audit logging and secret management meet enterprise requirements

