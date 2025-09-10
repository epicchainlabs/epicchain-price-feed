using System;
using System.Numerics;
using Xunit;
using R3E.SmartContract.Testing;
using R3E.SmartContract.Testing.Native;
using R3E.SmartContract.Testing.Extensions;
using PriceFeed.R3E.Contract;

namespace PriceFeed.R3E.Tests
{
    public class PriceOracleContractTests : SmartContractTest
    {
        private TestEngine Engine { get; set; }
        private UInt160 Owner { get; set; }
        private UInt160 TeeAccount { get; set; }
        private UInt160 MasterAccount { get; set; }
        private UInt160 ContractHash { get; set; }

        public PriceOracleContractTests()
        {
            // Initialize test engine
            Engine = new TestEngine();
            
            // Create test accounts
            Owner = Engine.CreateAccount("owner", 1000_00000000); // 1000 GAS
            TeeAccount = Engine.CreateAccount("tee", 10_00000000); // 10 GAS
            MasterAccount = Engine.CreateAccount("master", 100_00000000); // 100 GAS
            
            // Deploy contract
            ContractHash = Engine.Deploy<PriceOracleContract>(Owner);
        }

        [Fact]
        public void TestInitialize()
        {
            // Act
            var result = Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.True((bool)result.Stack[0]);
            
            // Verify events
            var events = result.Notifications;
            Assert.Contains(events, e => e.EventName == "TeeAccountAdded");
            Assert.Contains(events, e => e.EventName == "Initialized");
        }

        [Fact]
        public void TestInitialize_AlreadyInitialized_ShouldFail()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            // Act
            var result = Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.False((bool)result.Stack[0]);
        }

        [Fact]
        public void TestUpdatePrice_WithValidDualSignature()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000); // $45,000 with 8 decimals
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);
            
            // Act - Execute with dual signatures
            var result = Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount }, // Signers
                symbol, 
                price, 
                timestamp, 
                confidence
            );
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.True((bool)result.Stack[0]);
            
            // Verify event
            var priceUpdateEvent = result.Notifications.First(e => e.EventName == "PriceUpdated");
            Assert.Equal(symbol, priceUpdateEvent.State[0]);
            Assert.Equal(price, priceUpdateEvent.State[1]);
        }

        [Fact]
        public void TestUpdatePrice_WithoutTeeSignature_ShouldFail()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);
            
            // Act - Execute with only master signature
            var result = Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { MasterAccount }, // Only master signer
                symbol, 
                price, 
                timestamp, 
                confidence
            );
            
            // Assert
            Assert.True(result.State == VMState.FAULT);
            Assert.Contains("Dual signature verification failed", result.FaultException.Message);
        }

        [Fact]
        public void TestUpdatePriceBatch()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var updates = new[]
            {
                new PriceOracleContract.PriceUpdate
                {
                    Symbol = "BTCUSDT",
                    Price = new BigInteger(45000_00000000),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Confidence = new BigInteger(95)
                },
                new PriceOracleContract.PriceUpdate
                {
                    Symbol = "ETHUSDT",
                    Price = new BigInteger(3000_00000000),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Confidence = new BigInteger(92)
                },
                new PriceOracleContract.PriceUpdate
                {
                    Symbol = "NEOUSDT",
                    Price = new BigInteger(15_00000000),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Confidence = new BigInteger(88)
                }
            };
            
            // Act
            var result = Engine.ExecuteContract(
                ContractHash, 
                "updatePriceBatch", 
                new[] { TeeAccount, MasterAccount },
                updates
            );
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.True((bool)result.Stack[0]);
            
            // Verify all events were fired
            var priceUpdateEvents = result.Notifications.Where(e => e.EventName == "PriceUpdated").ToList();
            Assert.Equal(3, priceUpdateEvents.Count);
        }

        [Fact]
        public void TestGetPrice()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);
            
            Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount },
                symbol, 
                price, 
                timestamp, 
                confidence
            );
            
            // Act
            var result = Engine.ExecuteContract(ContractHash, "getPrice", symbol);
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.Equal(price, result.Stack[0]);
        }

        [Fact]
        public void TestGetPriceData()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);
            
            Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount },
                symbol, 
                price, 
                timestamp, 
                confidence
            );
            
            // Act
            var result = Engine.ExecuteContract(ContractHash, "getPriceData", symbol);
            
            // Assert
            Assert.True(result.State == VMState.HALT);
            var priceData = result.Stack[0].ToStruct();
            Assert.Equal(symbol, priceData[0].GetString());
            Assert.Equal(price, priceData[1].GetInteger());
            Assert.Equal(timestamp, priceData[2].GetInteger());
            Assert.Equal(confidence, priceData[3].GetInteger());
        }

        [Fact]
        public void TestCircuitBreaker_PriceDeviationTooHigh()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var initialPrice = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);
            
            // Set initial price
            Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount },
                symbol, 
                initialPrice, 
                timestamp, 
                confidence
            );
            
            // Try to update with >10% deviation
            var newPrice = new BigInteger(50000_00000000); // ~11% increase
            
            // Act
            var result = Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount },
                symbol, 
                newPrice, 
                timestamp + 1000, 
                confidence
            );
            
            // Assert
            Assert.True(result.State == VMState.FAULT);
            Assert.Contains("Circuit breaker triggered", result.FaultException.Message);
        }

        [Fact]
        public void TestAddRemoveTeeAccount()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, null);
            var newTeeAccount = Engine.CreateAccount("new-tee", 10_00000000);
            
            // Act - Add TEE account
            var addResult = Engine.ExecuteContract(
                ContractHash, 
                "addTeeAccount", 
                Owner,
                newTeeAccount
            );
            
            // Assert
            Assert.True(addResult.State == VMState.HALT);
            Assert.True((bool)addResult.Stack[0]);
            
            // Act - Remove TEE account
            var removeResult = Engine.ExecuteContract(
                ContractHash, 
                "removeTeeAccount", 
                Owner,
                newTeeAccount
            );
            
            // Assert
            Assert.True(removeResult.State == VMState.HALT);
            Assert.True((bool)removeResult.Stack[0]);
        }

        [Fact]
        public void TestConfidenceScoreValidation()
        {
            // Arrange
            Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var lowConfidence = new BigInteger(40); // Below minimum of 50
            
            // Act
            var result = Engine.ExecuteContract(
                ContractHash, 
                "updatePrice", 
                new[] { TeeAccount, MasterAccount },
                symbol, 
                price, 
                timestamp, 
                lowConfidence
            );
            
            // Assert
            Assert.True(result.State == VMState.FAULT);
            Assert.Contains("Confidence score must be at least 50", result.FaultException.Message);
        }
    }
}