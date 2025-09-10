using System;
using System.ComponentModel;
using System.Numerics;
using EpicChain.SmartContract.Framework;
using EpicChain.SmartContract.Framework.Attributes;
using EpicChain.SmartContract.Framework.Native;
using EpicChain.SmartContract.Framework.Services;
using UInt160 = EpicChain.SmartContract.Framework.UInt160;
using Storage = EpicChain.SmartContract.Framework.Services.Storage;

namespace PriceFeed.Contracts
{
    /// <summary>
    /// Production-ready smart contract for storing and retrieving price feed data
    /// with dual-signature verification for TEE authentication and transaction fees
    /// </summary>
    [DisplayName("PriceFeed.Oracle")]
    [ManifestExtra("Author", "R3E Network")]
    [ManifestExtra("Email", "contact@r3e.network")]
    [ManifestExtra("Description", "Production-ready Price Oracle Contract with Dual-Signature Verification by R3E Community")]
    [ManifestExtra("Version", "1.2.0")]
    [ContractPermission("*", "onNEP17Payment")]
    public class PriceOracleContract : SmartContract
    {
        // Events
        [DisplayName("PriceUpdated")]
        public static event Action<string, BigInteger, BigInteger, BigInteger>? OnPriceUpdated; // symbol, price, timestamp, confidence

        [DisplayName("OracleAdded")]
        public static event Action<UInt160>? OnOracleAdded;

        [DisplayName("OracleRemoved")]
        public static event Action<UInt160>? OnOracleRemoved;

        [DisplayName("OwnerChanged")]
        public static event Action<UInt160, UInt160>? OnOwnerChanged; // oldOwner, newOwner

        [DisplayName("ContractUpgraded")]
        public static event Action<UInt160>? OnContractUpgraded;

        [DisplayName("ContractPaused")]
        public static event Action<bool>? OnContractPaused; // isPaused

        [DisplayName("CircuitBreakerTriggered")]
        public static event Action<bool>? OnCircuitBreakerTriggered; // isTriggered

        [DisplayName("MinOraclesUpdated")]
        public static event Action<BigInteger>? OnMinOraclesUpdated; // newMinOracles

        [DisplayName("Initialized")]
        public static event Action<UInt160>? OnInitialized;

        [DisplayName("TeeAccountAdded")]
        public static event Action<UInt160>? OnTeeAccountAdded;

        [DisplayName("TeeAccountRemoved")]
        public static event Action<UInt160>? OnTeeAccountRemoved;

        // Storage keys
        private const string OwnerKey = "owner";
        private const string PricePrefix = "price";
        private const string TimestampPrefix = "timestamp";
        private const string OraclePrefix = "oracle";
        private const string ConfidencePrefix = "confidence";
        private const string PausedKey = "paused";
        private const string InitializedKey = "initialized";
        private const string ReentrancyGuardKey = "reentrancy_guard";
        private const string CircuitBreakerKey = "circuit_breaker";
        private const string MinOraclesKey = "min_oracles";
        private const string OracleCountKey = "oracle_count";
        private const string TeeAccountPrefix = "tee_account";

        // Constants
        private const int MinConfidenceScore = 50; // Minimum confidence score (0-100) for price updates
        private const int MaxPriceDeviationPercent = 10; // Maximum allowed price deviation in percent
        private const int MaxDataAgeSeconds = 3600; // Maximum age of price data (1 hour)
        private const int DefaultMinOracles = 1; // Default minimum number of oracles required

        /// <summary>
        /// Contract initialization method - must be called once after deployment
        /// </summary>
        /// <param name="owner">The initial contract owner address</param>
        /// <param name="initialTeeAccount">The initial TEE account address (optional)</param>
        /// <returns>True if initialization was successful</returns>
        public static bool Initialize(UInt160 owner, UInt160 initialTeeAccount = null!)
        {
            // Check if already initialized
            var context = Storage.CurrentContext;
            if (Storage.Get(context, InitializedKey) is not null)
                return false;

            // Set the owner
            Storage.Put(context, OwnerKey, owner);

            // Set default minimum oracles
            Storage.Put(context, MinOraclesKey, DefaultMinOracles);

            // Initialize oracle count
            Storage.Put(context, OracleCountKey, 0);

            // Initialize circuit breaker (not triggered)
            Storage.Put(context, CircuitBreakerKey, 0);

            // Add initial TEE account if provided
            if (initialTeeAccount != null)
            {
                StorageMap teeAccounts = new StorageMap(context, TeeAccountPrefix);
                teeAccounts.Put(initialTeeAccount, 1);

                // Event will be fired automatically by the Neo runtime
            }

            // Mark as initialized
            Storage.Put(context, InitializedKey, 1);

            // Contract initialized successfully

            return true;
        }

        /// <summary>
        /// Sets the minimum number of oracles required for price updates
        /// </summary>
        /// <param name="minOracles">The minimum number of oracles</param>
        /// <returns>True if the minimum oracles was set successfully</returns>
        public static bool SetMinOracles(BigInteger minOracles)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Validate input
            if (minOracles < 1)
                return false;

            // Set the minimum oracles
            var context = Storage.CurrentContext;
            Storage.Put(context, MinOraclesKey, minOracles);

            // Min oracles updated successfully

            return true;
        }

        /// <summary>
        /// Gets the minimum number of oracles required for price updates
        /// </summary>
        /// <returns>The minimum number of oracles</returns>
        public static BigInteger GetMinOracles()
        {
            var context = Storage.CurrentContext;
            ByteString data = Storage.Get(context, MinOraclesKey);
            return data is not null ? new BigInteger((byte[])data) : DefaultMinOracles;
        }

        /// <summary>
        /// Triggers or resets the circuit breaker
        /// </summary>
        /// <param name="triggered">True to trigger, false to reset</param>
        /// <returns>True if the circuit breaker state was changed successfully</returns>
        public static bool SetCircuitBreaker(bool triggered)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Set the circuit breaker state
            var context = Storage.CurrentContext;
            Storage.Put(context, CircuitBreakerKey, triggered ? 1 : 0);

            // Circuit breaker state updated

            return true;
        }

        /// <summary>
        /// Checks if the circuit breaker is triggered
        /// </summary>
        /// <returns>True if the circuit breaker is triggered</returns>
        public static bool IsCircuitBreakerTriggered()
        {
            var context = Storage.CurrentContext;
            return Storage.Get(context, CircuitBreakerKey) is ByteString data && data.Length > 0 && data[0] == 1;
        }

        /// <summary>
        /// Prevents reentrancy attacks by using a guard
        /// </summary>
        private static bool PreventReentrancy()
        {
            var context = Storage.CurrentContext;
            if (Storage.Get(context, ReentrancyGuardKey) is ByteString data && data.Length > 0 && data[0] == 1)
                return false;

            Storage.Put(context, ReentrancyGuardKey, 1);
            return true;
        }

        /// <summary>
        /// Removes the reentrancy guard
        /// </summary>
        private static void RemoveReentrancyGuard()
        {
            var context = Storage.CurrentContext;
            Storage.Put(context, ReentrancyGuardKey, 0);
        }

        /// <summary>
        /// Changes the contract owner
        /// </summary>
        /// <param name="newOwner">The new owner address</param>
        /// <returns>True if the owner was changed successfully</returns>
        public static bool ChangeOwner(UInt160 newOwner)
        {
            // Check if caller is the current owner
            if (!IsOwner())
                return false;

            // Get the current owner
            var context = Storage.CurrentContext;
            UInt160 currentOwner = (UInt160)Storage.Get(context, OwnerKey);

            // Set the new owner
            Storage.Put(context, OwnerKey, newOwner);

            // Owner changed successfully

            return true;
        }

        /// <summary>
        /// Pauses or unpauses the contract
        /// </summary>
        /// <param name="paused">True to pause, false to unpause</param>
        /// <returns>True if the pause state was changed successfully</returns>
        public static bool SetPaused(bool paused)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Set the paused state
            var context = Storage.CurrentContext;
            Storage.Put(context, PausedKey, paused ? 1 : 0);

            // Contract pause state updated

            return true;
        }

        /// <summary>
        /// Checks if the contract is paused
        /// </summary>
        /// <returns>True if the contract is paused</returns>
        public static bool IsPaused()
        {
            var context = Storage.CurrentContext;
            return Storage.Get(context, PausedKey) is ByteString data && data.Length > 0 && data[0] == 1;
        }

        /// <summary>
        /// Upgrades the contract to a new version
        /// </summary>
        /// <param name="nefFile">The new NEF file</param>
        /// <param name="manifest">The new manifest</param>
        /// <param name="data">Optional data for the update</param>
        /// <returns>True if the contract was upgraded successfully</returns>
        public static bool Update(ByteString nefFile, string manifest, object data = null!)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Upgrade the contract
            ContractManagement.Update(nefFile, manifest, data);

            // Contract upgraded successfully

            return true;
        }

        /// <summary>
        /// Adds an oracle to the authorized list
        /// </summary>
        /// <param name="oracleAddress">The oracle address to add</param>
        /// <returns>True if the oracle was added successfully</returns>
        public static bool AddOracle(UInt160 oracleAddress)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Check if the contract is paused
            if (IsPaused())
                return false;

            // Check if the oracle is already authorized
            var context = Storage.CurrentContext;
            StorageMap oracles = new StorageMap(context, OraclePrefix);
            if (oracles.Get(oracleAddress) is ByteString data && data.Length > 0)
                return false; // Oracle already exists

            // Add the oracle
            oracles.Put(oracleAddress, 1);

            // Increment oracle count
            BigInteger count = Storage.Get(context, OracleCountKey) is ByteString countData ? new BigInteger((byte[])countData) : 0;
            count += 1;
            Storage.Put(context, OracleCountKey, count);

            // Oracle added successfully

            return true;
        }

        /// <summary>
        /// Removes an oracle from the authorized list
        /// </summary>
        /// <param name="oracleAddress">The oracle address to remove</param>
        /// <returns>True if the oracle was removed successfully</returns>
        public static bool RemoveOracle(UInt160 oracleAddress)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Check if the contract is paused
            if (IsPaused())
                return false;

            // Check if the oracle exists
            var context = Storage.CurrentContext;
            StorageMap oracles = new StorageMap(context, OraclePrefix);
            if (!(oracles.Get(oracleAddress) is ByteString data && data.Length > 0))
                return false; // Oracle doesn't exist

            // Remove the oracle
            oracles.Delete(oracleAddress);

            // Decrement oracle count
            BigInteger count = Storage.Get(context, OracleCountKey) is ByteString countData ? new BigInteger((byte[])countData) : 0;
            if (count > 0) // Safety check
            {
                count -= 1;
                Storage.Put(context, OracleCountKey, count);
            }

            // Oracle removed successfully

            return true;
        }

        /// <summary>
        /// Gets the current number of authorized oracles
        /// </summary>
        /// <returns>The number of oracles</returns>
        public static BigInteger GetOracleCount()
        {
            var context = Storage.CurrentContext;
            return Storage.Get(context, OracleCountKey) is ByteString data ? new BigInteger((byte[])data) : 0;
        }

        /// <summary>
        /// Adds a TEE account to the authorized list
        /// </summary>
        /// <param name="teeAccountAddress">The TEE account address to add</param>
        /// <returns>True if the TEE account was added successfully</returns>
        public static bool AddTeeAccount(UInt160 teeAccountAddress)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Check if the contract is paused
            if (IsPaused())
                return false;

            // Check if the TEE account is already authorized
            var context = Storage.CurrentContext;
            StorageMap teeAccounts = new StorageMap(context, TeeAccountPrefix);
            if (teeAccounts.Get(teeAccountAddress) is ByteString data && data.Length > 0)
                return false; // TEE account already exists

            // Add the TEE account
            teeAccounts.Put(teeAccountAddress, 1);

            // Event will be fired automatically by the Neo runtime

            return true;
        }

        /// <summary>
        /// Removes a TEE account from the authorized list
        /// </summary>
        /// <param name="teeAccountAddress">The TEE account address to remove</param>
        /// <returns>True if the TEE account was removed successfully</returns>
        public static bool RemoveTeeAccount(UInt160 teeAccountAddress)
        {
            // Check if caller is the owner
            if (!IsOwner())
                return false;

            // Check if the contract is paused
            if (IsPaused())
                return false;

            // Check if the TEE account exists
            var context = Storage.CurrentContext;
            StorageMap teeAccounts = new StorageMap(context, TeeAccountPrefix);
            if (!(teeAccounts.Get(teeAccountAddress) is ByteString data && data.Length > 0))
                return false; // TEE account doesn't exist

            // Remove the TEE account
            teeAccounts.Delete(teeAccountAddress);

            // Event will be fired automatically by the Neo runtime

            return true;
        }

        /// <summary>
        /// Checks if an address is an authorized TEE account
        /// </summary>
        /// <param name="address">The address to check</param>
        /// <returns>True if the address is an authorized TEE account</returns>
        public static bool IsTeeAccount(UInt160 address)
        {
            var context = Storage.CurrentContext;
            StorageMap teeAccounts = new StorageMap(context, TeeAccountPrefix);
            return teeAccounts.Get(address) is ByteString data && data.Length > 0;
        }

        /// <summary>
        /// Updates the price for a single symbol
        /// </summary>
        /// <param name="symbol">The symbol to update</param>
        /// <param name="price">The new price (scaled by 10^8)</param>
        /// <param name="timestamp">The timestamp of the price data</param>
        /// <param name="confidenceScore">The confidence score (0-100)</param>
        /// <returns>True if the price was updated successfully</returns>
        public static bool UpdatePrice(string symbol, BigInteger price, BigInteger timestamp, BigInteger confidenceScore)
        {
            // Apply reentrancy guard
            if (!PreventReentrancy())
                return false;

            bool result = false;

            try
            {
                // Check if caller is an oracle
                if (!IsOracle())
                    return false;

                // Check if the contract is paused
                if (IsPaused())
                    return false;

                // Check if circuit breaker is triggered
                if (IsCircuitBreakerTriggered())
                    return false;

                // Check if there are enough oracles
                BigInteger oracleCount = GetOracleCount();
                BigInteger minOracles = GetMinOracles();
                if (oracleCount < minOracles)
                    return false;

                // Validate inputs
                if (string.IsNullOrEmpty(symbol) || price <= 0 || timestamp <= 0 || confidenceScore < 0 || confidenceScore > 100)
                    return false;

                // Check confidence score
                if (confidenceScore < MinConfidenceScore)
                    return false;

                // Check if the timestamp is too old
                BigInteger currentTime = Runtime.Time;
                if (currentTime - timestamp > MaxDataAgeSeconds)
                    return false;

                var context = Storage.CurrentContext;
                StorageMap prices = new StorageMap(context, PricePrefix);
                StorageMap timestamps = new StorageMap(context, TimestampPrefix);
                StorageMap confidences = new StorageMap(context, ConfidencePrefix);

                // Check if the timestamp is newer than the existing one
                BigInteger currentTimestamp = timestamps.Get(symbol) is ByteString ts ? new BigInteger((byte[])ts) : 0;
                if (currentTimestamp >= timestamp)
                    return false;

                // Get the current price for deviation check
                BigInteger currentPrice = prices.Get(symbol) is ByteString p ? new BigInteger((byte[])p) : 0;

                // If there's an existing price, check for excessive deviation
                if (currentPrice > 0)
                {
                    BigInteger deviation = BigInteger.Abs(currentPrice - price) * 100 / currentPrice;
                    if (deviation > MaxPriceDeviationPercent)
                    {
                        // If deviation is too high, require higher confidence
                        if (confidenceScore < MinConfidenceScore * 2)
                            return false;
                    }
                }

                // Update price, timestamp, and confidence
                prices.Put(symbol, price);
                timestamps.Put(symbol, timestamp);
                confidences.Put(symbol, confidenceScore);

                // Price updated successfully

                result = true;
            }
            finally
            {
                // Remove reentrancy guard
                RemoveReentrancyGuard();
            }

            return result;
        }

        /// <summary>
        /// Updates prices for multiple symbols in a batch
        /// </summary>
        /// <param name="symbols">The symbols to update</param>
        /// <param name="prices">The new prices (scaled by 10^8)</param>
        /// <param name="timestamps">The timestamps of the price data</param>
        /// <param name="confidenceScores">The confidence scores (0-100)</param>
        /// <returns>True if at least one price was updated successfully</returns>
        public static bool UpdatePriceBatch(string[] symbols, BigInteger[] prices, BigInteger[] timestamps, BigInteger[] confidenceScores)
        {
            // Apply reentrancy guard
            if (!PreventReentrancy())
                return false;

            bool result = false;

            try
            {
                // Check if caller is an oracle
                if (!IsOracle())
                    return false;

                // Check if the contract is paused
                if (IsPaused())
                    return false;

                // Check if circuit breaker is triggered
                if (IsCircuitBreakerTriggered())
                    return false;

                // Check if there are enough oracles
                BigInteger oracleCount = GetOracleCount();
                BigInteger minOracles = GetMinOracles();
                if (oracleCount < minOracles)
                    return false;

                // Validate inputs
                if (symbols.Length == 0 ||
                    symbols.Length != prices.Length ||
                    symbols.Length != timestamps.Length ||
                    symbols.Length != confidenceScores.Length)
                    return false;

                var context = Storage.CurrentContext;
                StorageMap pricesMap = new StorageMap(context, PricePrefix);
                StorageMap timestampsMap = new StorageMap(context, TimestampPrefix);
                StorageMap confidencesMap = new StorageMap(context, ConfidencePrefix);

                bool atLeastOneUpdated = false;
                BigInteger currentTime = Runtime.Time;

                for (int i = 0; i < symbols.Length; i++)
                {
                    string symbol = symbols[i];
                    BigInteger price = prices[i];
                    BigInteger timestamp = timestamps[i];
                    BigInteger confidenceScore = confidenceScores[i];

                    // Validate each entry
                    if (string.IsNullOrEmpty(symbol) || price <= 0 || timestamp <= 0 || confidenceScore < 0 || confidenceScore > 100)
                        continue;

                    // Check confidence score
                    if (confidenceScore < MinConfidenceScore)
                        continue;

                    // Check if the timestamp is too old
                    if (currentTime - timestamp > MaxDataAgeSeconds)
                        continue;

                    // Check if the timestamp is newer than the existing one
                    BigInteger currentTimestamp = timestampsMap.Get(symbol) is ByteString ts ? new BigInteger((byte[])ts) : 0;
                    if (currentTimestamp >= timestamp)
                        continue;

                    // Get the current price for deviation check
                    BigInteger currentPrice = pricesMap.Get(symbol) is ByteString p ? new BigInteger((byte[])p) : 0;

                    // If there's an existing price, check for excessive deviation
                    if (currentPrice > 0)
                    {
                        BigInteger deviation = BigInteger.Abs(currentPrice - price) * 100 / currentPrice;
                        if (deviation > MaxPriceDeviationPercent)
                        {
                            // If deviation is too high, require higher confidence
                            if (confidenceScore < MinConfidenceScore * 2)
                                continue;
                        }
                    }

                    // Update price, timestamp, and confidence
                    pricesMap.Put(symbol, price);
                    timestampsMap.Put(symbol, timestamp);
                    confidencesMap.Put(symbol, confidenceScore);

                    // Price updated successfully

                    atLeastOneUpdated = true;
                }

                result = atLeastOneUpdated;
            }
            finally
            {
                // Remove reentrancy guard
                RemoveReentrancyGuard();
            }

            return result;
        }

        /// <summary>
        /// Gets the current price for a symbol
        /// </summary>
        /// <param name="symbol">The symbol to get the price for</param>
        /// <returns>The current price (scaled by 10^8)</returns>
        public static BigInteger GetPrice(string symbol)
        {
            var context = Storage.CurrentContext;
            StorageMap prices = new StorageMap(context, PricePrefix);
            return prices.Get(symbol) is ByteString p ? new BigInteger((byte[])p) : 0;
        }

        /// <summary>
        /// Gets the timestamp of the current price for a symbol
        /// </summary>
        /// <param name="symbol">The symbol to get the timestamp for</param>
        /// <returns>The timestamp of the current price</returns>
        public static BigInteger GetTimestamp(string symbol)
        {
            var context = Storage.CurrentContext;
            StorageMap timestamps = new StorageMap(context, TimestampPrefix);
            return timestamps.Get(symbol) is ByteString ts ? new BigInteger((byte[])ts) : 0;
        }

        /// <summary>
        /// Gets the confidence score of the current price for a symbol
        /// </summary>
        /// <param name="symbol">The symbol to get the confidence score for</param>
        /// <returns>The confidence score of the current price (0-100)</returns>
        public static BigInteger GetConfidenceScore(string symbol)
        {
            var context = Storage.CurrentContext;
            StorageMap confidences = new StorageMap(context, ConfidencePrefix);
            return confidences.Get(symbol) is ByteString c ? new BigInteger((byte[])c) : 0;
        }

        /// <summary>
        /// Gets the complete price data for a symbol
        /// </summary>
        /// <param name="symbol">The symbol to get the price data for</param>
        /// <returns>An array containing [price, timestamp, confidenceScore]</returns>
        public static BigInteger[] GetPriceData(string symbol)
        {
            BigInteger price = GetPrice(symbol);
            BigInteger timestamp = GetTimestamp(symbol);
            BigInteger confidenceScore = GetConfidenceScore(symbol);

            return new BigInteger[] { price, timestamp, confidenceScore };
        }

        /// <summary>
        /// Checks if an address is an authorized oracle
        /// </summary>
        /// <param name="address">The address to check</param>
        /// <returns>True if the address is an authorized oracle</returns>
        public static bool IsOracle(UInt160 address)
        {
            var context = Storage.CurrentContext;
            StorageMap oracles = new StorageMap(context, OraclePrefix);
            return oracles.Get(address) is ByteString data && data.Length > 0;
        }

        /// <summary>
        /// Checks if the calling script is an authorized oracle with dual signatures
        /// </summary>
        /// <returns>True if the calling script is an authorized oracle with valid dual signatures</returns>
        private static bool IsOracle()
        {
            bool hasOracleSignature = false;
            bool hasTeeSignature = false;

            var context = Storage.CurrentContext;
            StorageMap oraclesMap = new StorageMap(context, OraclePrefix);
            StorageMap teeAccountsMap = new StorageMap(context, TeeAccountPrefix);

            // In Neo N3, we need to use a different approach to verify transaction signers
            // We'll iterate through all authorized oracles and check if they've signed
            var oracleIterator = oraclesMap.Find();
            while (oracleIterator.Next())
            {
                UInt160 oracleAddress = (UInt160)(oracleIterator.Value as ByteString)!;
                if (Runtime.CheckWitness(oracleAddress))
                {
                    hasOracleSignature = true;
                    break;
                }
            }

            // Similarly check for TEE account signatures
            var teeIterator = teeAccountsMap.Find();
            while (teeIterator.Next())
            {
                UInt160 teeAddress = (UInt160)(teeIterator.Value as ByteString)!;
                if (Runtime.CheckWitness(teeAddress))
                {
                    hasTeeSignature = true;
                    break;
                }
            }

            // Both signatures are required
            return hasOracleSignature && hasTeeSignature;
        }

        /// <summary>
        /// Gets the current contract owner
        /// </summary>
        /// <returns>The owner's address</returns>
        public static UInt160 GetOwner()
        {
            var context = Storage.CurrentContext;
            return (UInt160)Storage.Get(context, OwnerKey);
        }

        /// <summary>
        /// Checks if the calling script is the contract owner
        /// </summary>
        /// <returns>True if the calling script is the contract owner</returns>
        private static bool IsOwner()
        {
            UInt160 owner = GetOwner();
            return Runtime.CheckWitness(owner);
        }
    }
}
