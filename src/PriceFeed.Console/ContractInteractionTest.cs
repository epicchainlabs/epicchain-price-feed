using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EpicChain;
using EpicChain.SmartContract;
using EpicChain.Wallets;
using EpicChain.Wallets.NEP6;
using EpicChain.VM;
using EpicChain.Cryptography;
using Serilog;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace PriceFeed.Console
{
    /// <summary>
    /// Test class that implements the README examples to test the deployed contract
    /// </summary>
    public static class ContractInteractionTest
    {
        // Your deployed contract hash
        private static readonly string ContractHash = "0xab8e532653f79d60e6a7d2ce9d8be7a80b5f4cad";

        // Neo Express RPC endpoint
        private static readonly string RpcEndpoint = "http://localhost:20332";

        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Runs the contract interaction tests
        /// </summary>
        /// <param name="oracleAccountWif">WIF (Wallet Import Format) of the oracle account</param>
        /// <param name="readerAccountWif">WIF of an account to read prices (optional)</param>
        /// <returns>0 if successful, 1 if failed</returns>
        public static async Task<int> RunTest(string? oracleAccountWif = null, string? readerAccountWif = null)
        {
            Log.Information("Starting contract interaction test...");

            try
            {
                // Test 1: Read price data (Consumer functionality)
                await TestReadPrice();

                // Test 2: Read complete price data
                await TestReadPriceData();

                // Test 3: Check contract status
                await TestContractStatus();

                // Test 4: Oracle-specific tests (if WIF provided)
                if (!string.IsNullOrEmpty(oracleAccountWif))
                {
                    await TestOracleStatus(oracleAccountWif);
                }
                else
                {
                    Log.Warning("No oracle account WIF provided. Skipping oracle-specific tests.");
                    Log.Information("To test oracle functionality, provide --oracle-wif parameter");
                }

                Log.Information("Contract interaction test completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Contract interaction test failed");
                return 1;
            }
        }

        /// <summary>
        /// Test reading a price (implementing README example)
        /// </summary>
        private static async Task TestReadPrice()
        {
            Log.Information("Testing GetPrice method...");

            try
            {
                var symbol = "BTCUSDT";

                // Create RPC request to invoke the GetPrice method
                var rpcRequest = new
                {
                    jsonrpc = "2.0",
                    method = "invokefunction",
                    @params = new object[]
                    {
                        ContractHash,
                        "GetPrice",
                        new object[]
                        {
                            new { type = "String", value = symbol }
                        }
                    },
                    id = 1
                };

                var result = await MakeRpcCall(rpcRequest);

                if (result != null && result.ContainsKey("result"))
                {
                    var invokeResult = result["result"] as Newtonsoft.Json.Linq.JObject;
                    if (invokeResult != null && invokeResult["state"]?.ToString() == "HALT")
                    {
                        var stack = invokeResult["stack"] as Newtonsoft.Json.Linq.JArray;
                        if (stack != null && stack.Count > 0)
                        {
                            var priceResult = stack[0] as Newtonsoft.Json.Linq.JObject;
                            if (priceResult != null && priceResult["type"]?.ToString() == "Integer")
                            {
                                var priceValue = priceResult["value"]?.ToString();
                                if (!string.IsNullOrEmpty(priceValue) && BigInteger.TryParse(priceValue, out var price))
                                {
                                    // Convert the price to a decimal (assuming 8 decimal places)
                                    var actualPrice = (decimal)price / 100000000;

                                    if (price == 0)
                                    {
                                        Log.Information("Price for {Symbol}: No data available (price is 0)", symbol);
                                    }
                                    else
                                    {
                                        Log.Information("Price for {Symbol}: {Price} (raw: {RawPrice})",
                                            symbol, actualPrice, price);
                                    }
                                }
                                else
                                {
                                    Log.Warning("Price for {Symbol}: Could not parse price value: {Value}", symbol, priceValue);
                                }
                            }
                            else
                            {
                                Log.Warning("Price for {Symbol}: Invalid result type", symbol);
                            }
                        }
                        else
                        {
                            Log.Warning("Price for {Symbol}: Empty stack result", symbol);
                        }
                    }
                    else
                    {
                        var state = invokeResult?["state"]?.ToString();
                        Log.Warning("Failed to get price for {Symbol}. State: {State}", symbol, state);
                    }
                }
                else
                {
                    Log.Warning("Failed to get price for {Symbol}. Invalid RPC response", symbol);
                }

                Log.Information("GetPrice test completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetPrice test failed");
                throw;
            }
        }

        /// <summary>
        /// Test reading complete price data
        /// </summary>
        private static async Task TestReadPriceData()
        {
            Log.Information("Testing GetPriceData method...");

            try
            {
                var symbol = "BTCUSDT";

                // Create RPC request to invoke the GetPriceData method
                var rpcRequest = new
                {
                    jsonrpc = "2.0",
                    method = "invokefunction",
                    @params = new object[]
                    {
                        ContractHash,
                        "GetPriceData",
                        new object[]
                        {
                            new { type = "String", value = symbol }
                        }
                    },
                    id = 1
                };

                var result = await MakeRpcCall(rpcRequest);

                if (result != null && result.ContainsKey("result"))
                {
                    var invokeResult = result["result"] as Newtonsoft.Json.Linq.JObject;
                    if (invokeResult != null && invokeResult["state"]?.ToString() == "HALT")
                    {
                        var stack = invokeResult["stack"] as Newtonsoft.Json.Linq.JArray;
                        if (stack != null && stack.Count > 0)
                        {
                            var priceDataResult = stack[0] as Newtonsoft.Json.Linq.JObject;
                            if (priceDataResult != null && priceDataResult["type"]?.ToString() == "Array")
                            {
                                var priceDataArray = priceDataResult["value"] as Newtonsoft.Json.Linq.JArray;
                                if (priceDataArray != null && priceDataArray.Count >= 3)
                                {
                                    var priceObj = priceDataArray[0] as Newtonsoft.Json.Linq.JObject;
                                    var timestampObj = priceDataArray[1] as Newtonsoft.Json.Linq.JObject;
                                    var confidenceObj = priceDataArray[2] as Newtonsoft.Json.Linq.JObject;

                                    if (priceObj != null && timestampObj != null && confidenceObj != null)
                                    {
                                        var priceValue = priceObj["value"]?.ToString();
                                        var timestampValue = timestampObj["value"]?.ToString();
                                        var confidenceValue = confidenceObj["value"]?.ToString();

                                        if (BigInteger.TryParse(priceValue, out var price) &&
                                            BigInteger.TryParse(timestampValue, out var timestamp) &&
                                            BigInteger.TryParse(confidenceValue, out var confidence))
                                        {
                                            // Convert values
                                            var actualPrice = (decimal)price / 100000000;
                                            var dateTime = timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)timestamp) : DateTimeOffset.MinValue;

                                            Log.Information("Complete price data for {Symbol}:", symbol);
                                            if (price == 0)
                                            {
                                                Log.Information("  Price: No data available (price is 0)");
                                            }
                                            else
                                            {
                                                Log.Information("  Price: {Price} (raw: {RawPrice})", actualPrice, price);
                                            }

                                            if (timestamp == 0)
                                            {
                                                Log.Information("  Timestamp: No data available (timestamp is 0)");
                                            }
                                            else
                                            {
                                                Log.Information("  Timestamp: {Timestamp} ({DateTime})", timestamp, dateTime);
                                            }

                                            Log.Information("  Confidence: {Confidence}%", confidence);
                                        }
                                        else
                                        {
                                            Log.Warning("Price data for {Symbol}: Could not parse values", symbol);
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("Price data for {Symbol}: Invalid array structure", symbol);
                                    }
                                }
                                else
                                {
                                    Log.Warning("Price data for {Symbol}: Incomplete data array", symbol);
                                }
                            }
                            else
                            {
                                Log.Warning("Price data for {Symbol}: Invalid result type", symbol);
                            }
                        }
                        else
                        {
                            Log.Warning("Price data for {Symbol}: Empty stack result", symbol);
                        }
                    }
                    else
                    {
                        var state = invokeResult?["state"]?.ToString();
                        Log.Warning("Failed to get price data for {Symbol}. State: {State}", symbol, state);
                    }
                }
                else
                {
                    Log.Warning("Failed to get price data for {Symbol}. Invalid RPC response", symbol);
                }

                Log.Information("GetPriceData test completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetPriceData test failed");
                throw;
            }
        }

        /// <summary>
        /// Test contract status methods
        /// </summary>
        private static async Task TestContractStatus()
        {
            Log.Information("Testing contract status methods...");

            try
            {
                // Test IsPaused
                var pausedRequest = new
                {
                    jsonrpc = "2.0",
                    method = "invokefunction",
                    @params = new object[]
                    {
                        ContractHash,
                        "IsPaused",
                        new object[] { }
                    },
                    id = 1
                };

                var pausedResult = await MakeRpcCall(pausedRequest);
                if (pausedResult != null && pausedResult.ContainsKey("result"))
                {
                    var invokeResult = pausedResult["result"] as Newtonsoft.Json.Linq.JObject;
                    if (invokeResult != null && invokeResult["state"]?.ToString() == "HALT")
                    {
                        var stack = invokeResult["stack"] as Newtonsoft.Json.Linq.JArray;
                        if (stack != null && stack.Count > 0)
                        {
                            var pausedObj = stack[0] as Newtonsoft.Json.Linq.JObject;
                            if (pausedObj != null && pausedObj["type"]?.ToString() == "Boolean")
                            {
                                var isPaused = pausedObj["value"]?.ToString() == "true";
                                Log.Information("Contract is paused: {IsPaused}", isPaused);
                            }
                        }
                    }
                }

                // Test GetOwner
                var ownerRequest = new
                {
                    jsonrpc = "2.0",
                    method = "invokefunction",
                    @params = new object[]
                    {
                        ContractHash,
                        "GetOwner",
                        new object[] { }
                    },
                    id = 1
                };

                var ownerResult = await MakeRpcCall(ownerRequest);
                if (ownerResult != null && ownerResult.ContainsKey("result"))
                {
                    var invokeResult = ownerResult["result"] as Newtonsoft.Json.Linq.JObject;
                    if (invokeResult != null && invokeResult["state"]?.ToString() == "HALT")
                    {
                        var stack = invokeResult["stack"] as Newtonsoft.Json.Linq.JArray;
                        if (stack != null && stack.Count > 0)
                        {
                            var ownerObj = stack[0] as Newtonsoft.Json.Linq.JObject;
                            if (ownerObj != null && ownerObj["type"]?.ToString() == "ByteString")
                            {
                                var ownerValue = ownerObj["value"]?.ToString();
                                if (!string.IsNullOrEmpty(ownerValue))
                                {
                                    // Convert hex string to UInt160 and then to address
                                    try
                                    {
                                        var ownerBytes = Convert.FromHexString(ownerValue);
                                        if (ownerBytes.Length == 20)
                                        {
                                            var ownerHash = new UInt160(ownerBytes);
                                            var ownerAddress = ownerHash.ToAddress(Neo.ProtocolSettings.Default.AddressVersion);
                                            Log.Information("Contract owner: {Owner}", ownerAddress);
                                        }
                                        else
                                        {
                                            Log.Information("Contract owner (hex): {Owner}", ownerValue);
                                        }
                                    }
                                    catch
                                    {
                                        Log.Information("Contract owner (hex): {Owner}", ownerValue);
                                    }
                                }
                            }
                        }
                    }
                }

                Log.Information("Contract status test completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Contract status test failed");
                throw;
            }
        }

        /// <summary>
        /// Test oracle-specific functionality
        /// </summary>
        private static async Task TestOracleStatus(string oracleAccountWif)
        {
            Log.Information("Testing oracle status...");

            try
            {
                // Create key pair from WIF
                var keyPair = CreateKeyPairFromWIF(oracleAccountWif);
                var scriptHash = Contract.CreateSignatureContract(keyPair.PublicKey).ScriptHash;
                var oracleAddress = scriptHash.ToAddress(Neo.ProtocolSettings.Default.AddressVersion);

                Log.Information("Oracle account address: {Address}", oracleAddress);

                // Test IsOracle
                var isOracleRequest = new
                {
                    jsonrpc = "2.0",
                    method = "invokefunction",
                    @params = new object[]
                    {
                        ContractHash,
                        "IsOracle",
                        new object[]
                        {
                            new { type = "Hash160", value = scriptHash.ToString() }
                        }
                    },
                    id = 1
                };

                var isOracleResult = await MakeRpcCall(isOracleRequest);
                if (isOracleResult != null && isOracleResult.ContainsKey("result"))
                {
                    var invokeResult = isOracleResult["result"] as Newtonsoft.Json.Linq.JObject;
                    if (invokeResult != null && invokeResult["state"]?.ToString() == "HALT")
                    {
                        var stack = invokeResult["stack"] as Newtonsoft.Json.Linq.JArray;
                        if (stack != null && stack.Count > 0)
                        {
                            var oracleObj = stack[0] as Newtonsoft.Json.Linq.JObject;
                            if (oracleObj != null && oracleObj["type"]?.ToString() == "Boolean")
                            {
                                var isOracle = oracleObj["value"]?.ToString() == "true";
                                Log.Information("Address {Address} is oracle: {IsOracle}", oracleAddress, isOracle);

                                if (!isOracle)
                                {
                                    Log.Warning("This account is not authorized as an oracle.");
                                    Log.Information("To authorize this account as an oracle, run:");
                                    Log.Information("neoxp contract invoke {ContractHash} AddOracle [\"{Address}\"] --account node1",
                                        ContractHash, oracleAddress);
                                }
                            }
                        }
                    }
                }

                Log.Information("Oracle status test completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Oracle status test failed");
                throw;
            }
        }

        /// <summary>
        /// Make an RPC call to the Neo node
        /// </summary>
        private static async Task<Dictionary<string, object>?> MakeRpcCall(object request)
        {
            try
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(RpcEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseContent);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RPC call failed");
                return null;
            }
        }

        /// <summary>
        /// Create a KeyPair from a WIF (Wallet Import Format) string
        /// </summary>
        private static KeyPair CreateKeyPairFromWIF(string wif)
        {
            try
            {
                // Create a NEP-6 wallet in memory and import the WIF
                var wallet = new NEP6Wallet(null, null, Neo.ProtocolSettings.Default);
                var account = wallet.Import(wif);
                return account.GetKey();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create KeyPair from WIF: {WIF}", wif);
                throw new ArgumentException($"Invalid WIF format: {wif}", ex);
            }
        }

        /// <summary>
        /// Helper method to run the test from command line
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static async Task<int> RunFromCommandLine(string[] args)
        {
            string? oracleWif = null;
            string? readerWif = null;

            // Parse command line arguments
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--oracle-wif")
                {
                    oracleWif = args[i + 1];
                }
                else if (args[i] == "--reader-wif")
                {
                    readerWif = args[i + 1];
                }
            }

            return await RunTest(oracleWif, readerWif);
        }
    }
}
