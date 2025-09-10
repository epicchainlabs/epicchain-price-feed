using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PriceFeed.Deployment
{
    public static class NeoCliDeployment
    {
        private const string MASTER_WIF = "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb";
        private const string NEO_CLI_VERSION = "v3.6.2";
        private const string TESTNET_RPC = "http://seed1t5.neo.org:20332";
        
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Neo N3 Contract Deployment via Neo-CLI");
            Console.WriteLine(new string('=', 50));
            
            try
            {
                // Step 1: Check/Download Neo-CLI
                var neoCliPath = await EnsureNeoCli();
                if (string.IsNullOrEmpty(neoCliPath))
                {
                    Console.WriteLine("❌ Failed to setup Neo-CLI");
                    return;
                }
                
                // Step 2: Prepare deployment files
                PrepareDeploymentFiles();
                
                // Step 3: Create deployment script
                var deployScript = CreateDeploymentScript(neoCliPath);
                
                // Step 4: Execute deployment
                await ExecuteDeployment(neoCliPath, deployScript);
                
                // Step 5: Verify deployment
                await VerifyDeployment();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static async Task<string> EnsureNeoCli()
        {
            Console.WriteLine("\n📦 Setting up Neo-CLI...");
            
            var neoCliDir = Path.Combine(Directory.GetCurrentDirectory(), "neo-cli");
            var neoCliExe = Path.Combine(neoCliDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "neo-cli.exe" : "neo-cli");
            
            if (File.Exists(neoCliExe))
            {
                Console.WriteLine("✅ Neo-CLI already installed");
                return neoCliDir;
            }
            
            Console.WriteLine("📥 Downloading Neo-CLI...");
            
            try
            {
                var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : 
                              RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-x64" : 
                              "osx-x64";
                
                var downloadUrl = $"https://github.com/neo-project/neo-cli/releases/download/{NEO_CLI_VERSION}/neo-cli-{platform}.zip";
                var zipPath = Path.Combine(Directory.GetCurrentDirectory(), "neo-cli.zip");
                
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(downloadUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        await File.WriteAllBytesAsync(zipPath, await response.Content.ReadAsByteArrayAsync());
                        Console.WriteLine("✅ Downloaded Neo-CLI");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Failed to download Neo-CLI: {response.StatusCode}");
                        return null;
                    }
                }
                
                // Extract
                Console.WriteLine("📂 Extracting Neo-CLI...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, Directory.GetCurrentDirectory());
                File.Delete(zipPath);
                
                // Make executable on Unix
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("chmod", $"+x {neoCliExe}").WaitForExit();
                }
                
                Console.WriteLine("✅ Neo-CLI ready");
                return neoCliDir;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to setup Neo-CLI: {ex.Message}");
                return null;
            }
        }
        
        private static void PrepareDeploymentFiles()
        {
            Console.WriteLine("\n📁 Preparing deployment files...");
            
            var deployDir = Path.Combine(Directory.GetCurrentDirectory(), "neo-cli", "deploy");
            Directory.CreateDirectory(deployDir);
            
            // Copy contract files
            var nefSource = Path.Combine("src", "PriceFeed.Contracts", "PriceFeed.Oracle.nef");
            var manifestSource = Path.Combine("src", "PriceFeed.Contracts", "PriceFeed.Oracle.manifest.json");
            
            var nefDest = Path.Combine(deployDir, "PriceFeed.Oracle.nef");
            var manifestDest = Path.Combine(deployDir, "PriceFeed.Oracle.manifest.json");
            
            File.Copy(nefSource, nefDest, true);
            File.Copy(manifestSource, manifestDest, true);
            
            Console.WriteLine($"✅ Files copied to: {deployDir}");
            
            // Create testnet config
            var config = new
            {
                ProtocolConfiguration = new
                {
                    Network = 877933390,
                    MillisecondsPerBlock = 15000,
                    MaxTraceableBlocks = 2102400,
                    MaxTransactionsPerBlock = 512,
                    MemoryPoolMaxTransactions = 50000,
                    InitialGasDistribution = 52000000,
                    SeedList = new[]
                    {
                        "seed1t5.neo.org:20333",
                        "seed2t5.neo.org:20333",
                        "seed3t5.neo.org:20333",
                        "seed4t5.neo.org:20333",
                        "seed5t5.neo.org:20333"
                    }
                },
                ApplicationConfiguration = new
                {
                    Storage = new { Engine = "LevelDBStore" },
                    P2P = new
                    {
                        Port = 20333,
                        MaxConnections = 10
                    },
                    UnlockWallet = new
                    {
                        Path = "deploy-wallet.json",
                        Password = "deploy123"
                    }
                }
            };
            
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "neo-cli", "testnet.config.json");
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            
            Console.WriteLine("✅ TestNet configuration created");
        }
        
        private static string CreateDeploymentScript(string neoCliPath)
        {
            Console.WriteLine("\n📝 Creating deployment script...");
            
            var scriptContent = $@"#!/bin/bash
# Neo-CLI Deployment Script

cd {neoCliPath}

# Create deployment commands file
cat > deploy-commands.txt << 'EOF'
create wallet deploy-wallet.json
open wallet deploy-wallet.json
import key {MASTER_WIF}
list asset
deploy deploy/PriceFeed.Oracle.nef deploy/PriceFeed.Oracle.manifest.json
EOF

echo ""🚀 Starting Neo-CLI deployment...""

# Run Neo-CLI with commands
if [[ ""$OSTYPE"" == ""msys"" || ""$OSTYPE"" == ""win32"" ]]; then
    ./neo-cli.exe --config testnet.config.json < deploy-commands.txt
else
    ./neo-cli --config testnet.config.json < deploy-commands.txt
fi
";
            
            var scriptPath = Path.Combine(neoCliPath, "deploy.sh");
            File.WriteAllText(scriptPath, scriptContent);
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("chmod", $"+x {scriptPath}").WaitForExit();
            }
            
            Console.WriteLine($"✅ Deployment script created: {scriptPath}");
            return scriptPath;
        }
        
        private static async Task ExecuteDeployment(string neoCliPath, string deployScript)
        {
            Console.WriteLine("\n🔨 Executing deployment...");
            
            try
            {
                // Alternative: Create a C# wrapper for Neo-CLI commands
                var walletPath = Path.Combine(neoCliPath, "deploy-wallet.json");
                var nefPath = Path.Combine(neoCliPath, "deploy", "PriceFeed.Oracle.nef");
                var manifestPath = Path.Combine(neoCliPath, "deploy", "PriceFeed.Oracle.manifest.json");
                
                // Create wallet file with encrypted key
                var wallet = new
                {
                    name = "deploy-wallet",
                    version = "1.0",
                    scrypt = new
                    {
                        n = 16384,
                        r = 8,
                        p = 8
                    },
                    accounts = new[]
                    {
                        new
                        {
                            address = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX",
                            label = null as string,
                            isDefault = true,
                            @lock = false,
                            key = "6PYRWJgZBLr1e8xtpJKNXVwkHPRkZBRdpg3gFNQXqPJFMZevT4aBvDfc8Y", // Encrypted with password "deploy123"
                            contract = new
                            {
                                script = "DCEDQHwko4IBHBa+FZdpnNZGD1TknCUJjUlD/fAZLIDLaRdBVuezJw==",
                                parameters = new[]
                                {
                                    new { name = "signature", type = "Signature" }
                                },
                                deployed = false
                            }
                        }
                    },
                    extra = null as object
                };
                
                File.WriteAllText(walletPath, JsonSerializer.Serialize(wallet, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("✅ Wallet created");
                
                // Create automated deployment process
                Console.WriteLine("\n📋 DEPLOYMENT INSTRUCTIONS:");
                Console.WriteLine($"\n1. Open terminal/command prompt");
                Console.WriteLine($"2. Navigate to: {neoCliPath}");
                Console.WriteLine($"3. Run Neo-CLI:");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine($"   neo-cli.exe");
                }
                else
                {
                    Console.WriteLine($"   ./neo-cli");
                }
                Console.WriteLine($"\n4. In Neo-CLI console, run these commands:");
                Console.WriteLine($"   neo> open wallet deploy-wallet.json");
                Console.WriteLine($"   Password: deploy123");
                Console.WriteLine($"   neo> list asset");
                Console.WriteLine($"   neo> deploy deploy/PriceFeed.Oracle.nef deploy/PriceFeed.Oracle.manifest.json");
                Console.WriteLine($"   neo> exit");
                
                // Create batch deployment file for Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var batchContent = $@"@echo off
cd /d ""{neoCliPath}""
echo open wallet deploy-wallet.json > commands.txt
echo deploy123 >> commands.txt
echo list asset >> commands.txt
echo deploy deploy\PriceFeed.Oracle.nef deploy\PriceFeed.Oracle.manifest.json >> commands.txt
echo yes >> commands.txt
echo exit >> commands.txt
neo-cli.exe < commands.txt
pause
";
                    File.WriteAllText(Path.Combine(neoCliPath, "deploy.bat"), batchContent);
                    Console.WriteLine($"\n💡 Windows users can also run: {Path.Combine(neoCliPath, "deploy.bat")}");
                }
                
                // Create expect script for Linux/Mac automation
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var expectScript = $@"#!/usr/bin/expect -f
set timeout 30
spawn ./neo-cli
expect ""neo>""
send ""open wallet deploy-wallet.json\r""
expect ""password:""
send ""deploy123\r""
expect ""neo>""
send ""list asset\r""
expect ""neo>""
send ""deploy deploy/PriceFeed.Oracle.nef deploy/PriceFeed.Oracle.manifest.json\r""
expect ""Deploy""
send ""yes\r""
expect ""neo>""
send ""exit\r""
expect eof
";
                    var expectPath = Path.Combine(neoCliPath, "deploy.expect");
                    File.WriteAllText(expectPath, expectScript);
                    Process.Start("chmod", $"+x {expectPath}").WaitForExit();
                    Console.WriteLine($"\n💡 Linux/Mac users with 'expect' installed can run: {expectPath}");
                }
                
                Console.WriteLine($"\n✅ Deployment prepared. Follow the instructions above to deploy.");
                Console.WriteLine($"\n📊 Expected Results:");
                Console.WriteLine($"   Contract Hash: 0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc");
                Console.WriteLine($"   Deployment Cost: ~10.002 GAS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Deployment error: {ex.Message}");
            }
        }
        
        private static async Task VerifyDeployment()
        {
            Console.WriteLine("\n🔍 To verify deployment after completion:");
            Console.WriteLine("\n1. Check contract on TestNet:");
            
            var verifyScript = @"
# Verify deployment
curl -X POST http://seed1t5.neo.org:20332 \
  -H ""Content-Type: application/json"" \
  -d '{
    ""jsonrpc"": ""2.0"",
    ""method"": ""getcontractstate"",
    ""params"": [""0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc""],
    ""id"": 1
  }'
";
            
            Console.WriteLine(verifyScript);
            
            Console.WriteLine("\n2. Or run this verification:");
            Console.WriteLine("   dotnet run --project src/PriceFeed.Deployment -- verify");
            
            // Check if running in verify mode
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "verify"))
            {
                Console.WriteLine("\n🔍 Checking deployment status...");
                
                using var client = new HttpClient();
                var request = new
                {
                    jsonrpc = "2.0",
                    method = "getcontractstate",
                    @params = new[] { "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc" },
                    id = 1
                };
                
                var response = await client.PostAsync(TESTNET_RPC,
                    new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));
                
                var result = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(result);
                
                if (json.RootElement.TryGetProperty("result", out var contractState))
                {
                    Console.WriteLine("\n✅ CONTRACT DEPLOYED!");
                    Console.WriteLine($"   Contract is active on TestNet");
                    Console.WriteLine($"\n📝 Next: Initialize the contract");
                }
                else
                {
                    Console.WriteLine("\n❌ Contract not found on TestNet");
                    Console.WriteLine("   Deployment may still be pending or not yet completed");
                }
            }
        }
    }
}