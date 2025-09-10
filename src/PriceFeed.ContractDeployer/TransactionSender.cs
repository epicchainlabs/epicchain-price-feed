using System;
using System.Threading.Tasks;
using EpicChain.Network.RPC;
using EpicChain.VM;
using EpicChain.Network.RPC.Models;

namespace PriceFeed.ContractDeployer
{
    public static class TransactionSender
    {
        public static async Task<string> SendInitializeTransaction(
            RpcClient rpcClient,
            string contractHash,
            string ownerAddress,
            string teeAddress,
            string wif)
        {
            try
            {
                Console.WriteLine($"   Validating initialize transaction...");

                // First validate the script will work
                var initParams = new[]
                {
                    new RpcStack { Type = "String", Value = ownerAddress },
                    new RpcStack { Type = "String", Value = teeAddress }
                };

                var testResult = await rpcClient.InvokeFunctionAsync(contractHash, "initialize", initParams);
                if (testResult.State != VMState.HALT)
                {
                    throw new Exception($"Initialize script validation failed: {testResult.Exception}");
                }

                Console.WriteLine($"   ✅ Script validated, gas required: {decimal.Parse(testResult.GasConsumed.ToString()) / 100000000M:F8} GAS");

                // Generate the transaction commands for external execution
                GenerateTransactionCommands("initialize", contractHash, initParams, ownerAddress);

                return "commands-generated";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create initialize transaction: {ex.Message}", ex);
            }
        }

        public static async Task<string> SendAddOracleTransaction(
            RpcClient rpcClient,
            string contractHash,
            string oracleAddress,
            string wif)
        {
            try
            {
                Console.WriteLine($"   Validating addOracle transaction...");

                var oracleParams = new[]
                {
                    new RpcStack { Type = "String", Value = oracleAddress }
                };

                var testResult = await rpcClient.InvokeFunctionAsync(contractHash, "addOracle", oracleParams);
                if (testResult.State != VMState.HALT)
                {
                    throw new Exception($"AddOracle script validation failed: {testResult.Exception}");
                }

                Console.WriteLine($"   ✅ Script validated, gas required: {decimal.Parse(testResult.GasConsumed.ToString()) / 100000000M:F8} GAS");

                // Generate the transaction commands for external execution
                GenerateTransactionCommands("addOracle", contractHash, oracleParams, oracleAddress);

                return "commands-generated";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create addOracle transaction: {ex.Message}", ex);
            }
        }

        public static async Task<string> SendSetMinOraclesTransaction(
            RpcClient rpcClient,
            string contractHash,
            int minOracles,
            string wif)
        {
            try
            {
                Console.WriteLine($"   Validating setMinOracles transaction...");

                var minParams = new[]
                {
                    new RpcStack { Type = "Integer", Value = minOracles.ToString() }
                };

                var testResult = await rpcClient.InvokeFunctionAsync(contractHash, "setMinOracles", minParams);
                if (testResult.State != VMState.HALT)
                {
                    throw new Exception($"SetMinOracles script validation failed: {testResult.Exception}");
                }

                Console.WriteLine($"   ✅ Script validated, gas required: {decimal.Parse(testResult.GasConsumed.ToString()) / 100000000M:F8} GAS");

                // Generate the transaction commands for external execution
                GenerateTransactionCommands("setMinOracles", contractHash, minParams, "");

                return "commands-generated";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create setMinOracles transaction: {ex.Message}", ex);
            }
        }

        private static void GenerateTransactionCommands(
            string method,
            string contractHash,
            RpcStack[] parameters,
            string signerAddress)
        {
            Console.WriteLine($"   📋 Transaction Commands for {method}:");
            Console.WriteLine($"   ================================");

            // Create parameter string for neo-cli
            var paramStrings = new System.Collections.Generic.List<string>();
            foreach (var param in parameters)
            {
                if (param.Type == "String")
                {
                    paramStrings.Add($"\"{param.Value}\"");
                }
                else
                {
                    paramStrings.Add(param.Value.ToString());
                }
            }
            var paramList = string.Join(",", paramStrings);

            Console.WriteLine($"   💡 Neo-CLI Command:");
            Console.WriteLine($"      invoke {contractHash} {method} [{paramList}] {signerAddress}");
            Console.WriteLine();

            Console.WriteLine($"   🐍 Python Alternative (neo-mamba):");
            Console.WriteLine($"      pip install neo-mamba");
            Console.WriteLine($"      neo-mamba contract invoke {contractHash} {method} {string.Join(" ", paramStrings)} --wallet-wif <WIF> --rpc http://seed1t5.neo.org:20332");
            Console.WriteLine();
        }
    }
}
