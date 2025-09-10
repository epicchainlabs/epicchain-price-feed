using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using EpicChain;
using EpicChain.IO;
using EpicChain.Json;
using EpicChain.Network.P2P.Payloads;
using EpicChain.Network.RPC;
using EpicChain.Network.RPC.Models;
using EpicChain.SmartContract;
using EpicChain.SmartContract.Manifest;
using EpicChain.SmartContract.Native;
using EpicChain.VM;
using EpicChain.VM.Types;
using EpicChain.Wallets;
using System.Linq;

namespace PriceFeed.ContractDeployer
{
    class Program
    {
        private const string RPC_ENDPOINT = "http://seed1t5.neo.org:20332";
        private const string MASTER_WIF = "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb";
        private const string MASTER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX";
        private const string TEE_ADDRESS = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB";
        private const string KNOWN_CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc";

        static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "deploy":
                        await DeployContract();
                        break;
                    case "init":
                        await InitializeContract();
                        break;
                    case "init-execute":
                        await InitializeContractWithTransactions();
                        break;
                    case "verify":
                        await VerifyContract();
                        break;
                    case "full":
                        await FullDeploymentAndSetup();
                        break;
                    default:
                        ShowHelp();
                        break;
                }
            }
            else
            {
                ShowHelp();
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("🚀 Neo N3 Smart Contract Deployer");
            Console.WriteLine("==================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run deploy        - Deploy the contract");
            Console.WriteLine("  dotnet run init          - Show initialization commands");
            Console.WriteLine("  dotnet run init-execute  - Execute initialization transactions");
            Console.WriteLine("  dotnet run verify        - Verify contract status");
            Console.WriteLine("  dotnet run full          - Full deployment and setup");
        }

        static Task<string> DeployContract()
        {
            try
            {
                Console.WriteLine("🚀 Deploying Price Feed Oracle Contract");
                Console.WriteLine("=======================================");

                // Note: Contract deployment requires manual process with neo-cli
                // This method prepares the deployment information

                Console.WriteLine("📋 Contract Deployment Information:");
                Console.WriteLine($"   Contract Path: deploy/PriceFeed.Oracle.nef");
                Console.WriteLine($"   Manifest Path: deploy/PriceFeed.Oracle.manifest.json");
                Console.WriteLine($"   Deployer Address: {MASTER_ADDRESS}");
                Console.WriteLine($"   Expected Contract Hash: {KNOWN_CONTRACT_HASH}");

                Console.WriteLine("\n⚠️  Contract deployment requires neo-cli or Neo wallet");
                Console.WriteLine("   The contract is already deployed at: " + KNOWN_CONTRACT_HASH);

                return Task.FromResult(KNOWN_CONTRACT_HASH);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Deployment preparation failed: {ex.Message}");
                throw;
            }
        }

        static async Task InitializeContract()
        {
            try
            {
                Console.WriteLine("📝 Initializing Price Feed Oracle Contract");
                Console.WriteLine("==========================================");

                var contractHash = UInt160.Parse(KNOWN_CONTRACT_HASH);
                var rpcClient = new RpcClient(new Uri(RPC_ENDPOINT));

                Console.WriteLine($"📍 Contract Hash: {KNOWN_CONTRACT_HASH}");
                Console.WriteLine($"📍 Master Account: {MASTER_ADDRESS}");
                Console.WriteLine($"📍 TEE Account: {TEE_ADDRESS}");
                Console.WriteLine($"🌐 Connected to TestNet: {RPC_ENDPOINT}");

                // Check if already initialized
                var ownerResult = await InvokeContractMethod(rpcClient, contractHash, "getOwner");
                if (ownerResult != null && ownerResult.State == VMState.HALT && ownerResult.Stack.Length > 0)
                {
                    var ownerStack = ownerResult.Stack[0];
                    if (ownerStack.Type != EpicChain.VM.Types.StackItemType.Any)
                    {
                        Console.WriteLine("\n⚠️  Contract appears to be already initialized!");
                        Console.WriteLine("   Use 'dotnet run verify' to check the current state.");
                        return;
                    }
                }

                Console.WriteLine("\n🔧 Initialization Steps:");
                Console.WriteLine("The contract needs to be initialized with the following transactions:");

                // Generate initialization script
                Console.WriteLine("\n1️⃣ Initialize contract (set owner and TEE account):");
                var initParams = new[]
                {
                    new RpcStack { Type = "String", Value = MASTER_ADDRESS },
                    new RpcStack { Type = "String", Value = TEE_ADDRESS }
                };

                var initResult = await rpcClient.InvokeFunctionAsync(contractHash.ToString(), "initialize", initParams);
                if (initResult.State == VMState.HALT)
                {
                    Console.WriteLine($"   ✅ Script validated successfully");
                    Console.WriteLine($"   GAS required: {decimal.Parse(initResult.GasConsumed.ToString()) / 100000000M:F8} GAS");
                    Console.WriteLine($"   Script: {initResult.Script}");
                }
                else
                {
                    Console.WriteLine($"   ❌ Script validation failed: {initResult.Exception}");
                }

                // Add oracle script
                Console.WriteLine("\n2️⃣ Add TEE account as oracle:");
                var oracleParams = new[]
                {
                    new RpcStack { Type = "String", Value = TEE_ADDRESS }
                };

                var oracleResult = await rpcClient.InvokeFunctionAsync(contractHash.ToString(), "addOracle", oracleParams);
                if (oracleResult.State == VMState.HALT)
                {
                    Console.WriteLine($"   ✅ Script validated successfully");
                    Console.WriteLine($"   GAS required: {decimal.Parse(oracleResult.GasConsumed.ToString()) / 100000000M:F8} GAS");
                }

                // Set min oracles script
                Console.WriteLine("\n3️⃣ Set minimum oracles to 1:");
                var minParams = new[]
                {
                    new RpcStack { Type = "Integer", Value = "1" }
                };

                var minResult = await rpcClient.InvokeFunctionAsync(contractHash.ToString(), "setMinOracles", minParams);
                if (minResult.State == VMState.HALT)
                {
                    Console.WriteLine($"   ✅ Script validated successfully");
                    Console.WriteLine($"   GAS required: {decimal.Parse(minResult.GasConsumed.ToString()) / 100000000M:F8} GAS");
                }

                // Provide neo-cli commands
                Console.WriteLine("\n📋 Neo-CLI Commands to Execute:");
                Console.WriteLine("================================");
                Console.WriteLine($"# Connect to TestNet");
                Console.WriteLine($"connect {RPC_ENDPOINT}");
                Console.WriteLine();
                Console.WriteLine($"# Import key if needed");
                Console.WriteLine($"import key {MASTER_WIF}");
                Console.WriteLine();
                Console.WriteLine($"# Initialize contract");
                Console.WriteLine($"invoke {KNOWN_CONTRACT_HASH} initialize [\"{MASTER_ADDRESS}\",\"{TEE_ADDRESS}\"] {MASTER_ADDRESS}");
                Console.WriteLine();
                Console.WriteLine($"# Add oracle");
                Console.WriteLine($"invoke {KNOWN_CONTRACT_HASH} addOracle [\"{TEE_ADDRESS}\"] {MASTER_ADDRESS}");
                Console.WriteLine();
                Console.WriteLine($"# Set min oracles");
                Console.WriteLine($"invoke {KNOWN_CONTRACT_HASH} setMinOracles [1] {MASTER_ADDRESS}");

                Console.WriteLine("\n✅ Initialization commands prepared!");
                Console.WriteLine("Execute the commands above in neo-cli to complete initialization.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Initialization failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        static async Task VerifyContract()
        {
            try
            {
                Console.WriteLine("🔍 Verifying Price Feed Oracle Contract");
                Console.WriteLine("=======================================");

                var contractHash = UInt160.Parse(KNOWN_CONTRACT_HASH);
                var rpcClient = new RpcClient(new Uri(RPC_ENDPOINT));

                // Check contract state
                var contractState = await rpcClient.GetContractStateAsync(contractHash.ToString());
                if (contractState == null)
                {
                    Console.WriteLine("❌ Contract not found on TestNet");
                    return;
                }

                Console.WriteLine("✅ Contract deployed");
                Console.WriteLine($"   Name: {contractState.Manifest.Name}");
                Console.WriteLine($"   Hash: {KNOWN_CONTRACT_HASH}");
                Console.WriteLine($"   Update Counter: {contractState.UpdateCounter}");

                // Check initialization
                Console.WriteLine("\n📊 Contract State:");

                // Get owner
                var ownerResult = await InvokeContractMethod(rpcClient, contractHash, "getOwner");
                if (ownerResult != null && ownerResult.State == VMState.HALT && ownerResult.Stack.Length > 0)
                {
                    var ownerStack = ownerResult.Stack[0];
                    if (ownerStack.Type != EpicChain.VM.Types.StackItemType.Any && ownerStack.GetSpan().Length > 0)
                    {
                        var owner = System.Text.Encoding.UTF8.GetString(ownerStack.GetSpan().ToArray());
                        Console.WriteLine($"   Owner: {owner}");
                    }
                    else
                    {
                        Console.WriteLine("   Owner: Not set (contract not initialized)");
                    }
                }

                // Get TEE accounts
                var teeResult = await InvokeContractMethod(rpcClient, contractHash, "getTeeAccounts");
                if (teeResult != null && teeResult.State == VMState.HALT && teeResult.Stack.Length > 0)
                {
                    if (teeResult.Stack[0].Type == EpicChain.VM.Types.StackItemType.Array)
                    {
                        var teeArray = teeResult.Stack[0] as EpicChain.VM.Types.Array;
                        Console.WriteLine($"   TEE Accounts: {teeArray?.Count ?? 0} configured");
                    }
                    else
                    {
                        Console.WriteLine("   TEE Accounts: None");
                    }
                }

                // Get oracles
                var oraclesResult = await InvokeContractMethod(rpcClient, contractHash, "getOracles");
                if (oraclesResult != null && oraclesResult.State == VMState.HALT && oraclesResult.Stack.Length > 0)
                {
                    if (oraclesResult.Stack[0].Type == EpicChain.VM.Types.StackItemType.Array)
                    {
                        var oracleArray = oraclesResult.Stack[0] as EpicChain.VM.Types.Array;
                        Console.WriteLine($"   Oracles: {oracleArray?.Count ?? 0} configured");
                    }
                    else
                    {
                        Console.WriteLine("   Oracles: None");
                    }
                }

                // Get min oracles
                var minOraclesResult = await InvokeContractMethod(rpcClient, contractHash, "getMinOracles");
                if (minOraclesResult != null && minOraclesResult.State == VMState.HALT && minOraclesResult.Stack.Length > 0)
                {
                    if (minOraclesResult.Stack[0].Type == EpicChain.VM.Types.StackItemType.Integer)
                    {
                        Console.WriteLine($"   Min Oracles: {minOraclesResult.Stack[0].GetInteger()}");
                    }
                }

                // Check for price data
                Console.WriteLine("\n📈 Sample Price Data:");
                var symbols = new[] { "BTCUSDT", "ETHUSDT", "NEOUSDT" };
                foreach (var symbol in symbols)
                {
                    var priceParams = new[] { new RpcStack { Type = "String", Value = symbol } };
                    var priceResult = await rpcClient.InvokeFunctionAsync(contractHash.ToString(), "getPriceData", priceParams);

                    if (priceResult.State == VMState.HALT && priceResult.Stack.Length > 0)
                    {
                        if (priceResult.Stack[0].Type == EpicChain.VM.Types.StackItemType.Struct)
                        {
                            var priceStruct = priceResult.Stack[0] as EpicChain.VM.Types.Struct;
                            if (priceStruct != null && priceStruct.Count >= 4)
                            {
                                var price = priceStruct[1].GetInteger();
                                var timestamp = priceStruct[2].GetInteger();
                                var confidence = priceStruct[3].GetInteger();

                                var priceDecimal = (decimal)price / 100000000M;
                                var updateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp).DateTime;
                                var ageSeconds = (DateTime.UtcNow - updateTime).TotalSeconds;

                                Console.WriteLine($"   {symbol}: ${priceDecimal:F2} (Updated: {(int)ageSeconds}s ago, Confidence: {confidence}%)");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"   {symbol}: No data");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"   {symbol}: No data");
                    }
                }

                // Check account balances
                Console.WriteLine("\n💰 Account Balances:");
                var masterBalance = await GetAccountBalance(rpcClient, MASTER_ADDRESS);
                var teeBalance = await GetAccountBalance(rpcClient, TEE_ADDRESS);

                Console.WriteLine($"   Master Account: {masterBalance:F8} GAS");
                Console.WriteLine($"   TEE Account: {teeBalance:F8} GAS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Verification failed: {ex.Message}");
            }
        }

        static async Task FullDeploymentAndSetup()
        {
            try
            {
                Console.WriteLine("🚀 Full Deployment and Setup");
                Console.WriteLine("=============================\n");

                // Deploy is informational only
                await DeployContract();

                Console.WriteLine("\n⏳ Proceeding to initialization setup...");
                await Task.Delay(2000);

                // Initialize the contract
                await InitializeContract();

                Console.WriteLine("\n⏳ Waiting before verification...");
                await Task.Delay(2000);

                // Verify the setup
                await VerifyContract();

                Console.WriteLine("\n📌 Next Steps:");
                Console.WriteLine("1. Execute the neo-cli commands shown above to complete initialization");
                Console.WriteLine("2. Run 'dotnet run verify' after initialization to confirm");
                Console.WriteLine("3. The price feed will start automatically via GitHub Actions");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Setup failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static async Task<RpcInvokeResult?> InvokeContractMethod(RpcClient client, UInt160 contractHash, string method, params RpcStack[] parameters)
        {
            try
            {
                return await client.InvokeFunctionAsync(contractHash.ToString(), method, parameters);
            }
            catch
            {
                return null;
            }
        }

        static async Task<decimal> GetAccountBalance(RpcClient client, string address)
        {
            try
            {
                var balances = await client.GetNep17BalancesAsync(address);
                if (balances?.Balances != null)
                {
                    var gasBalance = balances.Balances.FirstOrDefault(b => b.AssetHash == NativeContract.GAS.Hash.ToString());
                    if (gasBalance != null)
                    {
                        // Amount is a BigInteger representing the balance in datoshi
                        // Convert to decimal by dividing by 10^8
                        var amountDecimal = (decimal)gasBalance.Amount;
                        return amountDecimal / 100000000M;
                    }
                }
            }
            catch { }

            return 0;
        }

        static async Task InitializeContractWithTransactions()
        {
            try
            {
                Console.WriteLine("🚀 Executing Contract Initialization");
                Console.WriteLine("====================================");

                var contractHash = UInt160.Parse(KNOWN_CONTRACT_HASH);
                var rpcClient = new RpcClient(new Uri(RPC_ENDPOINT));

                Console.WriteLine($"📍 Contract Hash: {KNOWN_CONTRACT_HASH}");
                Console.WriteLine($"📍 Master Account: {MASTER_ADDRESS}");
                Console.WriteLine($"📍 TEE Account: {TEE_ADDRESS}");
                Console.WriteLine($"🌐 Connected to TestNet: {RPC_ENDPOINT}");

                // Check if already initialized
                var ownerResult = await InvokeContractMethod(rpcClient, contractHash, "getOwner");
                if (ownerResult != null && ownerResult.State == VMState.HALT && ownerResult.Stack.Length > 0)
                {
                    var ownerStack = ownerResult.Stack[0];
                    if (ownerStack.Type != EpicChain.VM.Types.StackItemType.Any)
                    {
                        Console.WriteLine("\n⚠️  Contract appears to be already initialized!");
                        Console.WriteLine("   Use 'dotnet run verify' to check the current state.");
                        return;
                    }
                }

                Console.WriteLine("\n🔑 Starting initialization process...");

                // Step 1: Initialize contract
                Console.WriteLine("\n1️⃣ Initializing contract with owner and TEE account...");
                var initParams = new[]
                {
                    new ContractParameter { Type = ContractParameterType.String, Value = MASTER_ADDRESS },
                    new ContractParameter { Type = ContractParameterType.String, Value = TEE_ADDRESS }
                };

                try
                {
                    var initTxHash = await TransactionSender.SendInitializeTransaction(
                        rpcClient, KNOWN_CONTRACT_HASH, MASTER_ADDRESS, TEE_ADDRESS, MASTER_WIF);

                    Console.WriteLine($"   ✅ Transaction sent: {initTxHash}");
                    Console.WriteLine("   ⏳ Waiting for confirmation...");
                    await WaitForTransaction(rpcClient, initTxHash);
                    Console.WriteLine("   ✅ Contract initialized!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed to initialize: {ex.Message}");
                    return;
                }

                // Wait a bit for the blockchain to process
                await Task.Delay(5000);

                // Step 2: Add TEE as oracle
                Console.WriteLine("\n2️⃣ Adding TEE account as oracle...");
                var oracleParams = new[]
                {
                    new ContractParameter { Type = ContractParameterType.String, Value = TEE_ADDRESS }
                };

                try
                {
                    var oracleTxHash = await TransactionSender.SendAddOracleTransaction(
                        rpcClient, KNOWN_CONTRACT_HASH, TEE_ADDRESS, MASTER_WIF);

                    Console.WriteLine($"   ✅ Transaction sent: {oracleTxHash}");
                    Console.WriteLine("   ⏳ Waiting for confirmation...");
                    await WaitForTransaction(rpcClient, oracleTxHash);
                    Console.WriteLine("   ✅ TEE account added as oracle!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed to add oracle: {ex.Message}");
                    return;
                }

                // Wait a bit
                await Task.Delay(5000);

                // Step 3: Set minimum oracles to 1
                Console.WriteLine("\n3️⃣ Setting minimum oracles to 1...");
                var minParams = new[]
                {
                    new ContractParameter { Type = ContractParameterType.Integer, Value = 1 }
                };

                try
                {
                    var minTxHash = await TransactionSender.SendSetMinOraclesTransaction(
                        rpcClient, KNOWN_CONTRACT_HASH, 1, MASTER_WIF);

                    Console.WriteLine($"   ✅ Transaction sent: {minTxHash}");
                    Console.WriteLine("   ⏳ Waiting for confirmation...");
                    await WaitForTransaction(rpcClient, minTxHash);
                    Console.WriteLine("   ✅ Minimum oracles set to 1!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed to set min oracles: {ex.Message}");
                    return;
                }

                Console.WriteLine("\n✅ Contract initialization complete!");
                Console.WriteLine("\n📊 Verifying final state...");
                await Task.Delay(5000);
                await VerifyContract();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Initialization failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static async Task WaitForTransaction(RpcClient rpcClient, string txHash, int maxAttempts = 30)
        {
            Console.Write("   Waiting for confirmation");
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // Check if transaction is in a block
                    var appLog = await rpcClient.GetApplicationLogAsync(txHash);
                    if (appLog != null)
                    {
                        Console.WriteLine(" ✅");
                        if (appLog.Executions[0].VMState != VMState.HALT)
                        {
                            throw new Exception($"Transaction failed with state: {appLog.Executions[0].VMState}");
                        }
                        return;
                    }
                }
                catch { }

                Console.Write(".");
                await Task.Delay(2000); // Wait 2 seconds between attempts
            }

            Console.WriteLine(" ⚠️");
            Console.WriteLine("   Transaction not confirmed yet. It may still be processing.");
        }
    }
}
