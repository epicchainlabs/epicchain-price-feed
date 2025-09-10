using System;
using System.ComponentModel;
using System.Numerics;
using R3E.SmartContract.Framework;
using R3E.SmartContract.Framework.Attributes;
using R3E.SmartContract.Framework.Native;
using R3E.SmartContract.Framework.Services;

namespace PriceFeed.R3E.Contract
{
    /// <summary>
    /// R3E-optimized smart contract for storing and retrieving price feed data
    /// with dual-signature verification for TEE authentication and transaction fees
    /// </summary>
    [DisplayName("PriceFeed.Oracle")]
    [ContractAuthor("NeoBurger", "contact@neoburger.io")]
    [ContractDescription("R3E-Optimized Price Oracle Contract with Dual-Signature Verification")]
    [ContractVersion("2.0.0")]
    [ContractSourceCode("https://github.com/r3e-network/neo-price-feed")]
    [ContractPermission("*", "onNEP17Payment")]
    [SupportedStandards(NepStandard.Nep17)]
    public class PriceOracleContract : SmartContract
    {
        // Events
        [DisplayName("PriceUpdated")]
        public static event PriceUpdatedEvent OnPriceUpdated;

        [DisplayName("OracleAdded")]
        public static event OracleAddedEvent OnOracleAdded;

        [DisplayName("OracleRemoved")]
        public static event OracleRemovedEvent OnOracleRemoved;

        [DisplayName("OwnerChanged")]
        public static event OwnerChangedEvent OnOwnerChanged;

        [DisplayName("ContractUpgraded")]
        public static event ContractUpgradedEvent OnContractUpgraded;

        [DisplayName("ContractPaused")]
        public static event ContractPausedEvent OnContractPaused;

        [DisplayName("CircuitBreakerTriggered")]
        public static event CircuitBreakerTriggeredEvent OnCircuitBreakerTriggered;

        [DisplayName("MinOraclesUpdated")]
        public static event MinOraclesUpdatedEvent OnMinOraclesUpdated;

        [DisplayName("Initialized")]
        public static event InitializedEvent OnInitialized;

        [DisplayName("TeeAccountAdded")]
        public static event TeeAccountAddedEvent OnTeeAccountAdded;

        [DisplayName("TeeAccountRemoved")]
        public static event TeeAccountRemovedEvent OnTeeAccountRemoved;

        // Event delegates
        public delegate void PriceUpdatedEvent(string symbol, BigInteger price, BigInteger timestamp, BigInteger confidence);
        public delegate void OracleAddedEvent(UInt160 oracle);
        public delegate void OracleRemovedEvent(UInt160 oracle);
        public delegate void OwnerChangedEvent(UInt160 oldOwner, UInt160 newOwner);
        public delegate void ContractUpgradedEvent(UInt160 newContractHash);
        public delegate void ContractPausedEvent(bool isPaused);
        public delegate void CircuitBreakerTriggeredEvent(bool isTriggered);
        public delegate void MinOraclesUpdatedEvent(BigInteger newMinOracles);
        public delegate void InitializedEvent(UInt160 owner);
        public delegate void TeeAccountAddedEvent(UInt160 teeAccount);
        public delegate void TeeAccountRemovedEvent(UInt160 teeAccount);

        // Storage layout using R3E optimized storage
        private static readonly StorageMap PriceMap = new(Storage.CurrentContext, "price");
        private static readonly StorageMap TimestampMap = new(Storage.CurrentContext, "timestamp");
        private static readonly StorageMap OracleMap = new(Storage.CurrentContext, "oracle");
        private static readonly StorageMap ConfidenceMap = new(Storage.CurrentContext, "confidence");
        private static readonly StorageMap TeeAccountMap = new(Storage.CurrentContext, "tee_account");

        // Single-value storage keys
        private const byte OwnerKey = 0x01;
        private const byte PausedKey = 0x02;
        private const byte InitializedKey = 0x03;
        private const byte ReentrancyGuardKey = 0x04;
        private const byte CircuitBreakerKey = 0x05;
        private const byte MinOraclesKey = 0x06;
        private const byte OracleCountKey = 0x07;

        // Constants
        private const int MinConfidenceScore = 50;
        private const int MaxPriceDeviationPercent = 10;
        private const int MaxDataAgeSeconds = 3600;
        private const int DefaultMinOracles = 1;

        /// <summary>
        /// Contract initialization method - must be called once after deployment
        /// </summary>
        [DisplayName("initialize")]
        [Safe]
        public static bool Initialize(UInt160 owner, UInt160? initialTeeAccount = null)
        {
            var context = Storage.CurrentContext;
            
            // Check if already initialized
            if (Storage.Get(context, InitializedKey) != null)
            {
                return false;
            }

            // Validate owner
            if (!owner.IsValid || owner.IsZero)
            {
                throw new Exception("Invalid owner address");
            }

            // Set the owner
            Storage.Put(context, OwnerKey, owner);

            // Set default minimum oracles
            Storage.Put(context, MinOraclesKey, DefaultMinOracles);

            // Initialize oracle count
            Storage.Put(context, OracleCountKey, 0);

            // Add initial TEE account if provided
            if (initialTeeAccount != null && initialTeeAccount.IsValid && !initialTeeAccount.IsZero)
            {
                TeeAccountMap.Put(initialTeeAccount, true);
                OnTeeAccountAdded(initialTeeAccount);
            }

            // Mark as initialized
            Storage.Put(context, InitializedKey, true);

            // Fire initialization event
            OnInitialized(owner);

            return true;
        }

        /// <summary>
        /// Update price data with dual-signature verification
        /// </summary>
        [DisplayName("updatePrice")]
        public static bool UpdatePrice(string symbol, BigInteger price, BigInteger timestamp, BigInteger confidence)
        {
            // Check initialization
            EnsureInitialized();

            // Check if paused
            if (IsPaused())
            {
                throw new Exception("Contract is paused");
            }

            // Validate inputs
            if (string.IsNullOrEmpty(symbol) || symbol.Length > 32)
            {
                throw new Exception("Invalid symbol");
            }

            if (price <= 0)
            {
                throw new Exception("Price must be positive");
            }

            if (confidence < 0 || confidence > 100)
            {
                throw new Exception("Confidence must be between 0 and 100");
            }

            // Validate timestamp
            var currentTime = Runtime.Time;
            if (timestamp > currentTime + 60000 || timestamp < currentTime - MaxDataAgeSeconds * 1000)
            {
                throw new Exception("Invalid timestamp");
            }

            // Verify dual signatures
            if (!VerifyDualSignatures())
            {
                throw new Exception("Dual signature verification failed");
            }

            // Check minimum confidence
            if (confidence < MinConfidenceScore)
            {
                throw new Exception($"Confidence score must be at least {MinConfidenceScore}");
            }

            // Apply circuit breaker check
            if (!CheckCircuitBreaker(symbol, price))
            {
                throw new Exception("Circuit breaker triggered - price deviation too high");
            }

            // Store price data
            PriceMap.Put(symbol, price);
            TimestampMap.Put(symbol, timestamp);
            ConfidenceMap.Put(symbol, confidence);

            // Fire event
            OnPriceUpdated(symbol, price, timestamp, confidence);

            return true;
        }

        /// <summary>
        /// Update multiple prices in a single transaction
        /// </summary>
        [DisplayName("updatePriceBatch")]
        public static bool UpdatePriceBatch(PriceUpdate[] updates)
        {
            // Check initialization
            EnsureInitialized();

            // Check if paused
            if (IsPaused())
            {
                throw new Exception("Contract is paused");
            }

            // Verify dual signatures once for the batch
            if (!VerifyDualSignatures())
            {
                throw new Exception("Dual signature verification failed");
            }

            // Validate batch size
            if (updates == null || updates.Length == 0 || updates.Length > 50)
            {
                throw new Exception("Invalid batch size (1-50 updates allowed)");
            }

            // Process each update
            foreach (var update in updates)
            {
                // Validate inputs
                if (string.IsNullOrEmpty(update.Symbol) || update.Symbol.Length > 32)
                {
                    throw new Exception($"Invalid symbol: {update.Symbol}");
                }

                if (update.Price <= 0)
                {
                    throw new Exception($"Price must be positive for {update.Symbol}");
                }

                if (update.Confidence < 0 || update.Confidence > 100)
                {
                    throw new Exception($"Confidence must be between 0 and 100 for {update.Symbol}");
                }

                // Check minimum confidence
                if (update.Confidence < MinConfidenceScore)
                {
                    throw new Exception($"Confidence score must be at least {MinConfidenceScore} for {update.Symbol}");
                }

                // Apply circuit breaker check
                if (!CheckCircuitBreaker(update.Symbol, update.Price))
                {
                    throw new Exception($"Circuit breaker triggered for {update.Symbol}");
                }

                // Store price data
                PriceMap.Put(update.Symbol, update.Price);
                TimestampMap.Put(update.Symbol, update.Timestamp);
                ConfidenceMap.Put(update.Symbol, update.Confidence);

                // Fire event
                OnPriceUpdated(update.Symbol, update.Price, update.Timestamp, update.Confidence);
            }

            return true;
        }

        /// <summary>
        /// Get current price for a symbol
        /// </summary>
        [DisplayName("getPrice")]
        [Safe]
        public static BigInteger GetPrice(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new Exception("Invalid symbol");
            }

            var price = PriceMap.Get(symbol);
            if (price == null)
            {
                throw new Exception("Price not found");
            }

            return (BigInteger)price;
        }

        /// <summary>
        /// Get price data including timestamp and confidence
        /// </summary>
        [DisplayName("getPriceData")]
        [Safe]
        public static PriceData GetPriceData(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new Exception("Invalid symbol");
            }

            var price = PriceMap.Get(symbol);
            if (price == null)
            {
                throw new Exception("Price not found");
            }

            return new PriceData
            {
                Symbol = symbol,
                Price = (BigInteger)price,
                Timestamp = (BigInteger)TimestampMap.Get(symbol),
                Confidence = (BigInteger)ConfidenceMap.Get(symbol)
            };
        }

        /// <summary>
        /// Add a TEE account
        /// </summary>
        [DisplayName("addTeeAccount")]
        public static bool AddTeeAccount(UInt160 teeAccount)
        {
            // Check owner
            EnsureOwner();

            // Validate account
            if (!teeAccount.IsValid || teeAccount.IsZero)
            {
                throw new Exception("Invalid TEE account");
            }

            // Check if already exists
            if (TeeAccountMap.Get(teeAccount) != null)
            {
                return false;
            }

            // Add TEE account
            TeeAccountMap.Put(teeAccount, true);

            // Fire event
            OnTeeAccountAdded(teeAccount);

            return true;
        }

        /// <summary>
        /// Remove a TEE account
        /// </summary>
        [DisplayName("removeTeeAccount")]
        public static bool RemoveTeeAccount(UInt160 teeAccount)
        {
            // Check owner
            EnsureOwner();

            // Check if exists
            if (TeeAccountMap.Get(teeAccount) == null)
            {
                return false;
            }

            // Remove TEE account
            TeeAccountMap.Delete(teeAccount);

            // Fire event
            OnTeeAccountRemoved(teeAccount);

            return true;
        }

        // Helper methods
        private static void EnsureInitialized()
        {
            if (Storage.Get(Storage.CurrentContext, InitializedKey) == null)
            {
                throw new Exception("Contract not initialized");
            }
        }

        private static void EnsureOwner()
        {
            if (!Runtime.CheckWitness(GetOwner()))
            {
                throw new Exception("Only owner can call this method");
            }
        }

        private static UInt160 GetOwner()
        {
            return (UInt160)Storage.Get(Storage.CurrentContext, OwnerKey);
        }

        private static bool IsPaused()
        {
            var paused = Storage.Get(Storage.CurrentContext, PausedKey);
            return paused != null && (bool)paused;
        }

        private static bool VerifyDualSignatures()
        {
            // Get the transaction
            var tx = (Transaction)Runtime.ScriptContainer;
            
            // Must have at least 2 signers
            if (tx.Signers.Length < 2)
            {
                return false;
            }

            // Check if first signer is a valid TEE account
            var teeAccount = tx.Signers[0].Account;
            if (TeeAccountMap.Get(teeAccount) == null)
            {
                return false;
            }

            // Both signers must have signed
            return Runtime.CheckWitness(tx.Signers[0].Account) && 
                   Runtime.CheckWitness(tx.Signers[1].Account);
        }

        private static bool CheckCircuitBreaker(string symbol, BigInteger newPrice)
        {
            // Get current price
            var currentPriceObj = PriceMap.Get(symbol);
            if (currentPriceObj == null)
            {
                // No existing price, allow update
                return true;
            }

            var currentPrice = (BigInteger)currentPriceObj;
            
            // Calculate percentage change
            var difference = newPrice > currentPrice ? newPrice - currentPrice : currentPrice - newPrice;
            var percentChange = difference * 100 / currentPrice;

            // Check if within allowed deviation
            return percentChange <= MaxPriceDeviationPercent;
        }

        /// <summary>
        /// Upgrade the contract to a new version
        /// </summary>
        [DisplayName("upgrade")]
        public static bool Upgrade(ByteString nef, string manifest, object data)
        {
            // Check owner
            EnsureOwner();

            // Validate inputs
            if (nef == null || nef.Length == 0)
            {
                throw new Exception("Invalid NEF data");
            }

            if (string.IsNullOrEmpty(manifest))
            {
                throw new Exception("Invalid manifest data");
            }

            // Store upgrade data before upgrade
            var upgradeData = new UpgradeData
            {
                PreviousVersion = "2.0.0",
                UpgradeTime = Runtime.Time,
                Upgrader = GetOwner()
            };

            Storage.Put(Storage.CurrentContext, "upgrade_history", upgradeData);

            // Perform the upgrade
            ContractManagement.Update(nef, manifest, data);

            // Fire upgrade event
            OnContractUpgraded(Runtime.ExecutingScriptHash);

            return true;
        }

        /// <summary>
        /// Pause/unpause the contract
        /// </summary>
        [DisplayName("setPaused")]
        public static bool SetPaused(bool paused)
        {
            // Check owner
            EnsureOwner();

            // Update paused state
            Storage.Put(Storage.CurrentContext, PausedKey, paused);

            // Fire event
            OnContractPaused(paused);

            return true;
        }

        /// <summary>
        /// Set the contract owner
        /// </summary>
        [DisplayName("setOwner")]
        public static bool SetOwner(UInt160 newOwner)
        {
            // Check current owner
            EnsureOwner();

            // Validate new owner
            if (!newOwner.IsValid || newOwner.IsZero)
            {
                throw new Exception("Invalid new owner address");
            }

            var oldOwner = GetOwner();
            
            // Update owner
            Storage.Put(Storage.CurrentContext, OwnerKey, newOwner);

            // Fire event
            OnOwnerChanged(oldOwner, newOwner);

            return true;
        }

        /// <summary>
        /// Emergency destroy the contract (owner only)
        /// </summary>
        [DisplayName("destroy")]
        public static void Destroy()
        {
            // Check owner
            EnsureOwner();

            // Transfer any remaining assets to owner
            var owner = GetOwner();
            
            // Get contract balance
            var gasBalance = GAS.BalanceOf(Runtime.ExecutingScriptHash);
            if (gasBalance > 0)
            {
                GAS.Transfer(Runtime.ExecutingScriptHash, owner, gasBalance, "Contract destruction");
            }

            // Destroy the contract
            ContractManagement.Destroy();
        }

        /// <summary>
        /// Get contract version and info
        /// </summary>
        [DisplayName("getVersion")]
        [Safe]
        public static ContractInfo GetVersion()
        {
            return new ContractInfo
            {
                Version = "2.0.0",
                Framework = "R3E",
                Compiler = "R3E.Compiler.CSharp",
                IsInitialized = Storage.Get(Storage.CurrentContext, InitializedKey) != null,
                Owner = GetOwner(),
                IsPaused = IsPaused()
            };
        }

        // Prevent direct transfers
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            throw new Exception("Direct transfers not supported");
        }

        // Data structures
        public struct PriceData
        {
            public string Symbol;
            public BigInteger Price;
            public BigInteger Timestamp;
            public BigInteger Confidence;
        }

        public struct PriceUpdate
        {
            public string Symbol;
            public BigInteger Price;
            public BigInteger Timestamp;
            public BigInteger Confidence;
        }

        public struct ContractInfo
        {
            public string Version;
            public string Framework;
            public string Compiler;
            public bool IsInitialized;
            public UInt160 Owner;
            public bool IsPaused;
        }

        public struct UpgradeData
        {
            public string PreviousVersion;
            public BigInteger UpgradeTime;
            public UInt160 Upgrader;
        }
    }
}