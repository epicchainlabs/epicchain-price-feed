using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PriceFeed.Deployment
{
    /// <summary>
    /// Simple deployment runner that uses Neo Express or calls deployment scripts
    /// </summary>
    public class RunDeployment
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Neo N3 Contract Deployment");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine();
            
            // Check if contract files exist
            var nefPath = "src/PriceFeed.Contracts/PriceFeed.Oracle.nef";
            var manifestPath = "src/PriceFeed.Contracts/PriceFeed.Oracle.manifest.json";
            
            if (!File.Exists(nefPath) || !File.Exists(manifestPath))
            {
                Console.WriteLine("❌ Contract files not found!");
                Console.WriteLine($"   NEF: {nefPath}");
                Console.WriteLine($"   Manifest: {manifestPath}");
                return;
            }
            
            Console.WriteLine("✅ Contract files found:");
            Console.WriteLine($"   NEF: {new FileInfo(nefPath).Length} bytes");
            Console.WriteLine($"   Manifest: {new FileInfo(manifestPath).Length} bytes");
            Console.WriteLine();
            
            // Show deployment options
            Console.WriteLine("📋 DEPLOYMENT OPTIONS:");
            Console.WriteLine();
            Console.WriteLine("1️⃣ Use Python RPC script (Recommended)");
            Console.WriteLine("   Run: python3 scripts/simple_deploy_rpc.py");
            Console.WriteLine();
            Console.WriteLine("2️⃣ Use NeoLine Browser Extension");
            Console.WriteLine("   1. Install: https://neoline.io/");
            Console.WriteLine("   2. Import key: KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb");
            Console.WriteLine("   3. Switch to TestNet");
            Console.WriteLine("   4. Deploy contract files");
            Console.WriteLine();
            Console.WriteLine("3️⃣ Use Neo-CLI");
            Console.WriteLine("   1. Download: https://github.com/neo-project/neo-cli/releases");
            Console.WriteLine("   2. Run: ./neo-cli --network testnet");
            Console.WriteLine("   3. Import wallet and deploy");
            Console.WriteLine();
            
            // Try to run Python deployment
            Console.WriteLine("🔧 Attempting to run Python deployment script...");
            Console.WriteLine();
            
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = "scripts/simple_deploy_rpc.py",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };
                
                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Read output
                    while (!process.StandardOutput.EndOfStream)
                    {
                        Console.WriteLine(await process.StandardOutput.ReadLineAsync());
                    }
                    
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("✅ Python deployment script completed!");
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("⚠️  Python script exited with errors");
                        Console.WriteLine("   Try using NeoLine extension instead");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Could not run Python script: {ex.Message}");
                Console.WriteLine("   Please run manually: python3 scripts/simple_deploy_rpc.py");
            }
            
            Console.WriteLine();
            Console.WriteLine("📝 After deployment:");
            Console.WriteLine("   1. Update configuration with contract hash");
            Console.WriteLine("   2. Initialize contract with admin accounts");
            Console.WriteLine("   3. Test oracle: dotnet run --project src/PriceFeed.Console --skip-health-checks");
        }
    }
}