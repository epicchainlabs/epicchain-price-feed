using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using R3E.SmartContract.Deploy;
using R3E.SmartContract.Deploy.Models;
using R3E.SmartContract.Deploy.Services;

namespace PriceFeed.R3E.Deploy
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                logger.LogInformation("🚀 R3E Price Feed Contract Deployment Tool");
                logger.LogInformation("=========================================");

                // Parse command
                if (args.Length == 0)
                {
                    ShowHelp();
                    return 0;
                }

                var command = args[0].ToLower();

                switch (command)
                {
                    case "deploy":
                        return await DeployContract(configuration, logger);
                    
                    case "initialize":
                        return await InitializeContract(configuration, logger);
                    
                    case "verify":
                        return await VerifyContract(configuration, logger);
                    
                    case "upgrade":
                        return await UpgradeContract(configuration, logger);
                    
                    default:
                        logger.LogError($"Unknown command: {command}");
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deployment failed");
                return 1;
            }
        }

        static async Task<int> DeployContract(IConfiguration configuration, ILogger logger)
        {
            var deployConfig = configuration.GetSection("Deployment").Get<DeploymentConfig>();
            if (deployConfig == null)
            {
                logger.LogError("Deployment configuration not found");
                return 1;
            }

            // Create deployment service
            var deployService = new R3EDeploymentService(deployConfig, logger);

            // Load contract files
            var nefPath = Path.Combine("bin", "sc", "PriceFeed.Oracle.nef");
            var manifestPath = Path.Combine("bin", "sc", "PriceFeed.Oracle.manifest.json");

            if (!File.Exists(nefPath) || !File.Exists(manifestPath))
            {
                logger.LogError("Contract files not found. Please build the contract first.");
                return 1;
            }

            // Deploy contract
            logger.LogInformation("📤 Deploying contract to {Network}...", deployConfig.Network);
            
            var deployResult = await deployService.DeployContractAsync(
                nefPath,
                manifestPath,
                deployConfig.DeployerWif
            );

            if (deployResult.Success)
            {
                logger.LogInformation("✅ Contract deployed successfully!");
                logger.LogInformation("📋 Contract Hash: {ContractHash}", deployResult.ContractHash);
                logger.LogInformation("📋 Transaction: {TransactionHash}", deployResult.TransactionHash);
                
                // Save deployment info
                var deploymentInfo = new
                {
                    ContractHash = deployResult.ContractHash,
                    TransactionHash = deployResult.TransactionHash,
                    Network = deployConfig.Network,
                    Timestamp = DateTime.UtcNow,
                    DeployerAddress = deployResult.DeployerAddress
                };

                var deploymentInfoPath = "deployment-info.json";
                await File.WriteAllTextAsync(deploymentInfoPath, 
                    System.Text.Json.JsonSerializer.Serialize(deploymentInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                
                logger.LogInformation("💾 Deployment info saved to: {Path}", deploymentInfoPath);
                
                return 0;
            }
            else
            {
                logger.LogError("❌ Deployment failed: {Error}", deployResult.Error);
                return 1;
            }
        }

        static async Task<int> InitializeContract(IConfiguration configuration, ILogger logger)
        {
            var deployConfig = configuration.GetSection("Deployment").Get<DeploymentConfig>();
            if (deployConfig == null)
            {
                logger.LogError("Deployment configuration not found");
                return 1;
            }

            var initConfig = configuration.GetSection("Initialization").Get<InitializationConfig>();
            if (initConfig == null)
            {
                logger.LogError("Initialization configuration not found");
                return 1;
            }

            // Create deployment service
            var deployService = new R3EDeploymentService(deployConfig, logger);

            logger.LogInformation("🔧 Initializing contract...");
            logger.LogInformation("📍 Contract Hash: {ContractHash}", deployConfig.ContractHash);
            logger.LogInformation("👤 Owner: {Owner}", initConfig.OwnerAddress);
            logger.LogInformation("🔐 TEE Account: {TeeAccount}", initConfig.TeeAccountAddress ?? "None");

            // Call initialize method
            var initResult = await deployService.InvokeContractAsync(
                deployConfig.ContractHash,
                "initialize",
                deployConfig.DeployerWif,
                initConfig.OwnerAddress,
                initConfig.TeeAccountAddress
            );

            if (initResult.Success)
            {
                logger.LogInformation("✅ Contract initialized successfully!");
                logger.LogInformation("📋 Transaction: {TransactionHash}", initResult.TransactionHash);
                return 0;
            }
            else
            {
                logger.LogError("❌ Initialization failed: {Error}", initResult.Error);
                return 1;
            }
        }

        static async Task<int> VerifyContract(IConfiguration configuration, ILogger logger)
        {
            var deployConfig = configuration.GetSection("Deployment").Get<DeploymentConfig>();
            if (deployConfig == null)
            {
                logger.LogError("Deployment configuration not found");
                return 1;
            }

            // Create deployment service
            var deployService = new R3EDeploymentService(deployConfig, logger);

            logger.LogInformation("🔍 Verifying contract...");
            logger.LogInformation("📍 Contract Hash: {ContractHash}", deployConfig.ContractHash);

            // Verify contract exists and is initialized
            var verifyResult = await deployService.VerifyContractAsync(deployConfig.ContractHash);

            if (verifyResult.Success)
            {
                logger.LogInformation("✅ Contract verification successful!");
                logger.LogInformation("📋 Contract Name: {Name}", verifyResult.ContractName);
                logger.LogInformation("📋 Version: {Version}", verifyResult.Version);
                logger.LogInformation("📋 Initialized: {Initialized}", verifyResult.IsInitialized);
                logger.LogInformation("📋 Owner: {Owner}", verifyResult.Owner);
                return 0;
            }
            else
            {
                logger.LogError("❌ Verification failed: {Error}", verifyResult.Error);
                return 1;
            }
        }

        static async Task<int> UpgradeContract(IConfiguration configuration, ILogger logger)
        {
            logger.LogWarning("Contract upgrade functionality not yet implemented");
            return 1;
        }

        static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("R3E Price Feed Contract Deployment Tool");
            Console.WriteLine("======================================");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run -- <command>");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  deploy      Deploy the contract to the blockchain");
            Console.WriteLine("  initialize  Initialize the deployed contract");
            Console.WriteLine("  verify      Verify the deployed contract");
            Console.WriteLine("  upgrade     Upgrade the contract (not implemented)");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  Edit appsettings.json to configure deployment settings");
            Console.WriteLine("  Use environment variables to override settings");
            Console.WriteLine();
        }
    }

    // Configuration models
    public class DeploymentConfig
    {
        public string Network { get; set; } = "TestNet";
        public string RpcEndpoint { get; set; } = "http://seed1t5.neo.org:20332";
        public string ContractHash { get; set; } = "";
        public string DeployerWif { get; set; } = "";
    }

    public class InitializationConfig
    {
        public string OwnerAddress { get; set; } = "";
        public string? TeeAccountAddress { get; set; }
    }
}