using System;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using R3E.SmartContract.Testing;
using R3E.SmartContract.Testing.Native;
using R3E.SmartContract.Testing.Extensions;
using PriceFeed.R3E.Contract;
using Microsoft.Extensions.Logging;

namespace PriceFeed.R3E.Tests
{
    /// <summary>
    /// Integration tests for TEE (Trusted Execution Environment) functionality
    /// These tests simulate the GitHub Actions TEE environment
    /// </summary>
    public class TeeIntegrationTests : SmartContractTest
    {
        private TestEngine Engine { get; set; }
        private UInt160 Owner { get; set; }
        private UInt160 TeeAccount { get; set; }
        private UInt160 MasterAccount { get; set; }
        private UInt160 UnauthorizedAccount { get; set; }
        private UInt160 ContractHash { get; set; }

        public TeeIntegrationTests()
        {
            // Initialize test engine with GitHub Actions simulation
            Engine = new TestEngine();
            
            // Create test accounts that simulate real deployment scenario
            Owner = Engine.CreateAccount("contract-owner", 1000_00000000);
            TeeAccount = Engine.CreateAccount("github-actions-tee", 50_00000000);
            MasterAccount = Engine.CreateAccount("gas-provider-master", 500_00000000);
            UnauthorizedAccount = Engine.CreateAccount("unauthorized", 10_00000000);
            
            // Deploy and initialize contract
            ContractHash = Engine.Deploy<PriceOracleContract>(Owner);
            
            var initResult = Engine.ExecuteContract(ContractHash, "initialize", Owner, TeeAccount);
            Assert.True(initResult.State == VMState.HALT);
            Assert.True((bool)initResult.Stack[0]);
        }

        [Fact]
        public void TestTeeAccountDualSignatureRequirement()
        {
            // Arrange
            var symbol = "BTCUSDT";
            var price = new BigInteger(45000_00000000);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var confidence = new BigInteger(95);

            // Act & Assert - Should succeed with dual signatures
            var successResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { TeeAccount, MasterAccount }, // Both TEE and Master signatures
                symbol,
                price,
                timestamp,
                confidence
            );
            
            Assert.True(successResult.State == VMState.HALT);
            Assert.True((bool)successResult.Stack[0]);

            // Act & Assert - Should fail with only TEE signature
            var teeOnlyResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { TeeAccount }, // Only TEE signature
                symbol,
                price,
                timestamp + 1000,
                confidence
            );
            
            Assert.True(teeOnlyResult.State == VMState.FAULT);

            // Act & Assert - Should fail with only Master signature
            var masterOnlyResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { MasterAccount }, // Only Master signature
                symbol,
                price,
                timestamp + 2000,
                confidence
            );
            
            Assert.True(masterOnlyResult.State == VMState.FAULT);

            // Act & Assert - Should fail with unauthorized account
            var unauthorizedResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { UnauthorizedAccount, MasterAccount }, // Unauthorized + Master
                symbol,
                price,
                timestamp + 3000,
                confidence
            );
            
            Assert.True(unauthorizedResult.State == VMState.FAULT);
        }

        [Fact]
        public void TestTeeAccountManagement()
        {
            // Create a new TEE account for testing
            var newTeeAccount = Engine.CreateAccount("new-tee-account", 10_00000000);

            // Act - Add new TEE account (owner only)
            var addResult = Engine.ExecuteContract(
                ContractHash,
                "addTeeAccount",
                Owner,
                newTeeAccount
            );

            // Assert
            Assert.True(addResult.State == VMState.HALT);
            Assert.True((bool)addResult.Stack[0]);

            // Verify event was fired
            var addEvent = addResult.Notifications.First(e => e.EventName == "TeeAccountAdded");
            Assert.Equal(newTeeAccount.ToString(), addEvent.State[0].GetString());

            // Test that new TEE account can now be used for dual signatures
            var priceUpdateResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { newTeeAccount, MasterAccount },
                "ETHUSDT",
                new BigInteger(3000_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(90)
            );

            Assert.True(priceUpdateResult.State == VMState.HALT);

            // Act - Remove TEE account (owner only)
            var removeResult = Engine.ExecuteContract(
                ContractHash,
                "removeTeeAccount",
                Owner,
                newTeeAccount
            );

            // Assert
            Assert.True(removeResult.State == VMState.HALT);
            Assert.True((bool)removeResult.Stack[0]);

            // Verify that removed TEE account can no longer be used
            var failedUpdateResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { newTeeAccount, MasterAccount },
                "ETHUSDT",
                new BigInteger(3100_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(88)
            );

            Assert.True(failedUpdateResult.State == VMState.FAULT);
        }

        [Fact]
        public void TestTeeAccountUnauthorizedAccess()
        {
            var newTeeAccount = Engine.CreateAccount("unauthorized-tee", 10_00000000);

            // Act - Try to add TEE account with unauthorized account
            var unauthorizedAddResult = Engine.ExecuteContract(
                ContractHash,
                "addTeeAccount",
                UnauthorizedAccount, // Not the owner
                newTeeAccount
            );

            // Assert - Should fail
            Assert.True(unauthorizedAddResult.State == VMState.FAULT);
            Assert.Contains("Only owner can call this method", unauthorizedAddResult.FaultException.Message);

            // Act - Try to remove existing TEE account with unauthorized account
            var unauthorizedRemoveResult = Engine.ExecuteContract(
                ContractHash,
                "removeTeeAccount",
                UnauthorizedAccount, // Not the owner
                TeeAccount
            );

            // Assert - Should fail
            Assert.True(unauthorizedRemoveResult.State == VMState.FAULT);
            Assert.Contains("Only owner can call this method", unauthorizedRemoveResult.FaultException.Message);
        }

        [Fact]
        public void TestBatchOperationsWithTeeSignatures()
        {
            // Arrange - Create batch of price updates
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

            // Act - Execute batch with dual signatures
            var result = Engine.ExecuteContract(
                ContractHash,
                "updatePriceBatch",
                new[] { TeeAccount, MasterAccount },
                updates
            );

            // Assert
            Assert.True(result.State == VMState.HALT);
            Assert.True((bool)result.Stack[0]);

            // Verify all price update events were fired
            var priceUpdateEvents = result.Notifications.Where(e => e.EventName == "PriceUpdated").ToList();
            Assert.Equal(3, priceUpdateEvents.Count);

            // Verify each price was stored correctly
            foreach (var update in updates)
            {
                var priceResult = Engine.ExecuteContract(ContractHash, "getPrice", update.Symbol);
                Assert.True(priceResult.State == VMState.HALT);
                Assert.Equal(update.Price, priceResult.Stack[0].GetInteger());
            }
        }

        [Fact]
        public void TestTeeAccountAssetTransfer()
        {
            // This test simulates the automatic transfer of assets from TEE to Master account
            // In real deployment, this ensures TEE account doesn't accumulate assets

            // Arrange - Transfer some GAS to TEE account (simulating received assets)
            var transferAmount = new BigInteger(5_00000000); // 5 GAS
            
            // Simulate GAS transfer to TEE account
            var transferResult = Engine.ExecuteContract(
                NativeContract.GAS.Hash,
                "transfer",
                MasterAccount,
                TeeAccount,
                transferAmount,
                "Test transfer to TEE"
            );

            Assert.True(transferResult.State == VMState.HALT);

            // Verify TEE account has the GAS
            var teeBalance = Engine.GetBalance(TeeAccount, NativeContract.GAS.Hash);
            Assert.True(teeBalance >= transferAmount);

            // Act - Execute price update (which should trigger asset check)
            var updateResult = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { TeeAccount, MasterAccount },
                "TESTUSDT",
                new BigInteger(100_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(95)
            );

            Assert.True(updateResult.State == VMState.HALT);

            // In real implementation with R3EBatchProcessingService,
            // assets would be automatically transferred back to Master account
            // This test verifies the dual-signature mechanism works correctly
        }

        [Fact]
        public void TestTeeEnvironmentSimulation()
        {
            // This test simulates the GitHub Actions TEE environment
            // by testing various scenarios that would occur in production

            var testScenarios = new[]
            {
                new { Symbol = "BTCUSDT", Price = 45000m, Confidence = 95 },
                new { Symbol = "ETHUSDT", Price = 3000m, Confidence = 92 },
                new { Symbol = "NEOUSDT", Price = 15m, Confidence = 88 },
                new { Symbol = "GASUSDT", Price = 5m, Confidence = 85 }
            };

            foreach (var scenario in testScenarios)
            {
                // Simulate price update from TEE environment
                var result = Engine.ExecuteContract(
                    ContractHash,
                    "updatePrice",
                    new[] { TeeAccount, MasterAccount },
                    scenario.Symbol,
                    new BigInteger(scenario.Price * 100_000_000m), // Convert to 8 decimals
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new BigInteger(scenario.Confidence)
                );

                Assert.True(result.State == VMState.HALT, 
                    $"Failed to update price for {scenario.Symbol}");
                Assert.True((bool)result.Stack[0], 
                    $"Price update returned false for {scenario.Symbol}");

                // Verify price was stored
                var priceResult = Engine.ExecuteContract(ContractHash, "getPrice", scenario.Symbol);
                Assert.True(priceResult.State == VMState.HALT);
                
                var storedPrice = priceResult.Stack[0].GetInteger();
                var expectedPrice = new BigInteger(scenario.Price * 100_000_000m);
                Assert.Equal(expectedPrice, storedPrice);
            }
        }

        [Fact]
        public void TestTeeAccountRotation()
        {
            // Test the ability to rotate TEE accounts for security
            var oldTeeAccount = TeeAccount;
            var newTeeAccount = Engine.CreateAccount("rotated-tee-account", 20_00000000);

            // Add new TEE account
            var addResult = Engine.ExecuteContract(
                ContractHash,
                "addTeeAccount",
                Owner,
                newTeeAccount
            );
            Assert.True(addResult.State == VMState.HALT);

            // Test that both old and new TEE accounts work
            var oldAccountTest = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { oldTeeAccount, MasterAccount },
                "ROTATION_TEST_OLD",
                new BigInteger(100_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(95)
            );
            Assert.True(oldAccountTest.State == VMState.HALT);

            var newAccountTest = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { newTeeAccount, MasterAccount },
                "ROTATION_TEST_NEW",
                new BigInteger(200_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(93)
            );
            Assert.True(newAccountTest.State == VMState.HALT);

            // Remove old TEE account
            var removeResult = Engine.ExecuteContract(
                ContractHash,
                "removeTeeAccount",
                Owner,
                oldTeeAccount
            );
            Assert.True(removeResult.State == VMState.HALT);

            // Verify old account no longer works
            var failedOldAccountTest = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { oldTeeAccount, MasterAccount },
                "ROTATION_TEST_FAIL",
                new BigInteger(300_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(90)
            );
            Assert.True(failedOldAccountTest.State == VMState.FAULT);

            // Verify new account still works
            var successNewAccountTest = Engine.ExecuteContract(
                ContractHash,
                "updatePrice",
                new[] { newTeeAccount, MasterAccount },
                "ROTATION_TEST_SUCCESS",
                new BigInteger(400_00000000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new BigInteger(92)
            );
            Assert.True(successNewAccountTest.State == VMState.HALT);
        }
    }
}