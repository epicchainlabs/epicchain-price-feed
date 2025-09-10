using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EpicChain;
using EpicChain.Cryptography.ECC;
using EpicChain.SmartContract;
using EpicChain.Wallets;
using Newtonsoft.Json;
using PriceFeed.Core.Configuration;
using PriceFeed.Core.Interfaces;
using PriceFeed.Core.Models;
using PriceFeed.Core.Options;
using PriceFeed.Console;
using PriceFeed.Infrastructure.DataSources;
using PriceFeed.Infrastructure.Services;
using PriceFeed.Infrastructure.HealthChecks;
using PriceFeed.Core.Validation;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// Configure Serilog with structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PriceFeed")
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

try
{
    // Check command line arguments
    var commandLineArgs = Environment.GetCommandLineArgs();

    // Account generation mode
    if (commandLineArgs.Contains("--generate-account"))
    {
        // Check if secure output is requested
        string? secureOutputPath = null;
        for (int i = 0; i < commandLineArgs.Length - 1; i++)
        {
            if (commandLineArgs[i] == "--secure-output")
            {
                secureOutputPath = commandLineArgs[i + 1];
                break;
            }
        }

        GenerateNeoAccount(secureOutputPath!);
        return 0;
    }

    // Create account attestation mode
    if (commandLineArgs.Contains("--create-account-attestation"))
    {
        // Get the account address from command line
        string? accountAddress = null;
        for (int i = 0; i < commandLineArgs.Length - 1; i++)
        {
            if (commandLineArgs[i] == "--account-address")
            {
                accountAddress = commandLineArgs[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(accountAddress))
        {
            Log.Error("Account address is required for attestation");
            return 1;
        }

        await CreateAccountAttestationAsync(accountAddress);
        return 0;
    }

    // Verify account attestation mode
    if (commandLineArgs.Contains("--verify-account-attestation"))
    {
        bool isValid = await VerifyAccountAttestationAsync();
        return isValid ? 0 : 1;
    }

    // Test symbol mappings mode
    if (commandLineArgs.Contains("--test-symbol-mappings"))
    {
        SymbolMappingTest.RunTest();
        return 0;
    }

    // Test mock price feed mode
    if (commandLineArgs.Contains("--test-mock-price-feed"))
    {
        return await MockPriceFeedTest.RunTest();
    }

    // Test contract interaction mode
    if (commandLineArgs.Contains("--test-contract-interaction"))
    {
        return await ContractInteractionTest.RunFromCommandLine(commandLineArgs);
    }

    // Check if we should skip health checks
    bool skipHealthChecks = commandLineArgs.Contains("--skip-health-checks");

    // Check for continuous execution mode
    bool continuousMode = commandLineArgs.Contains("--continuous");
    int executionDurationMinutes = 5; // Default 5 minutes
    int updateIntervalSeconds = 15; // Default 15 seconds

    // Parse execution duration
    for (int i = 0; i < commandLineArgs.Length - 1; i++)
    {
        if (commandLineArgs[i] == "--duration" && int.TryParse(commandLineArgs[i + 1], out int duration))
        {
            executionDurationMinutes = duration;
        }
        else if (commandLineArgs[i] == "--interval" && int.TryParse(commandLineArgs[i + 1], out int interval))
        {
            updateIntervalSeconds = interval;
        }
    }

    // Validate required environment variables unless in development mode
    if (!EnvironmentConfiguration.IsDevelopmentMode())
    {
        try
        {
            EnvironmentConfiguration.ValidateRequiredEnvironmentVariables();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "Missing required environment variables");
            return 1;
        }
    }

    Log.Information("Starting PriceFeed job");

    // Check for sensitive environment variables
    CheckSensitiveEnvironmentVariables();

    // Create and configure the host
    using var host = Host.CreateDefaultBuilder(commandLineArgs)
        .UseSerilog()
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            config.AddEnvironmentVariableOverrides();
            config.AddCommandLine(commandLineArgs);
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Configure options with validation
            services.Configure<PriceFeedOptions>(options =>
            {
                hostContext.Configuration.GetSection("PriceFeed").Bind(options);
                options.ApplyEnvironmentOverrides(); // Apply environment variable overrides after configuration binding
            });
            services.Configure<BinanceOptions>(hostContext.Configuration.GetSection("Binance"));
            services.Configure<CoinMarketCapOptions>(options =>
            {
                hostContext.Configuration.GetSection("CoinMarketCap").Bind(options);
                options.ApplyEnvironmentOverrides(); // Apply environment variable overrides after configuration binding
            });
            services.Configure<CoinbaseOptions>(hostContext.Configuration.GetSection("Coinbase"));
            services.Configure<OKExOptions>(hostContext.Configuration.GetSection("OKEx"));
            services.Configure<CoinGeckoOptions>(hostContext.Configuration.GetSection("CoinGecko"));
            services.Configure<KrakenOptions>(hostContext.Configuration.GetSection("Kraken"));
            services.Configure<BatchProcessingOptions>(options =>
            {
                hostContext.Configuration.GetSection("BatchProcessing").Bind(options);
                options.ApplyEnvironmentOverrides(); // Apply environment variable overrides after configuration binding
            });

            // Add options validation (relaxed for testnet mode)
            var isTestnetMode = hostContext.Configuration.GetSection("BatchProcessing:RpcEndpoint").Value?.Contains("seed1t5.neo.org") == true;
            services.AddSingleton<IValidateOptions<BatchProcessingOptions>, BatchProcessingOptionsValidator>();
            services.AddSingleton<IValidateOptions<BinanceOptions>, BinanceOptionsValidator>();
            if (!isTestnetMode)
            {
                services.AddSingleton<IValidateOptions<CoinMarketCapOptions>, CoinMarketCapOptionsValidator>();
            }
            services.AddSingleton<IValidateOptions<CoinbaseOptions>, CoinbaseOptionsValidator>();
            services.AddSingleton<IValidateOptions<OKExOptions>, OKExOptionsValidator>();

            // Add memory cache
            services.AddMemoryCache();

            // Register HTTP clients with resilience policies
            services.AddHttpClient();

            // Configure Binance HTTP client with Polly
            services.AddHttpClient("Binance", (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<BinanceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "Binance");
            });

            // Configure CoinMarketCap HTTP client with Polly
            services.AddHttpClient("CoinMarketCap", (serviceProvider, client) =>
            {
                try
                {
                    var options = serviceProvider.GetRequiredService<IOptions<CoinMarketCapOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    if (!string.IsNullOrEmpty(options.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", options.ApiKey);
                    }
                }
                catch (OptionsValidationException)
                {
                    // In testnet mode, CoinMarketCap validation might fail
                    // Use default values if validation fails
                    client.BaseAddress = new Uri("https://pro-api.coinmarketcap.com");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.Timeout = TimeSpan.FromSeconds(30);
                }
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "CoinMarketCap");
            });

            // Configure Coinbase HTTP client with Polly
            services.AddHttpClient("Coinbase", (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<CoinbaseOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "Coinbase");
            });

            // Configure OKEx HTTP client with Polly
            services.AddHttpClient("OKEx", (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OKExOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                if (!string.IsNullOrEmpty(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("OK-ACCESS-KEY", options.ApiKey);
                }
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "OKEx");
            });

            // Configure Neo HTTP client with Polly
            services.AddHttpClient("Neo", client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "Neo", 30);
            });

            // Configure CoinGecko HTTP client with Polly
            services.AddHttpClient("CoinGecko", client =>
            {
                client.BaseAddress = new Uri("https://api.coingecko.com");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "CoinGecko");
            });

            // Configure Kraken HTTP client with Polly
            services.AddHttpClient("Kraken", client =>
            {
                client.BaseAddress = new Uri("https://api.kraken.com");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                return ResiliencePolicies.GetCombinedPolicy(logger, "Kraken");
            });

            // Register data source adapters
            services.AddTransient<IDataSourceAdapter, BinanceDataSourceAdapter>();
            services.AddTransient<IDataSourceAdapter, CoinMarketCapDataSourceAdapter>();
            services.AddTransient<IDataSourceAdapter, CoinbaseDataSourceAdapter>();
            services.AddTransient<IDataSourceAdapter, OKExDataSourceAdapter>();
            services.AddTransient<IDataSourceAdapter, CoinGeckoDataSourceAdapter>();
            services.AddTransient<IDataSourceAdapter, KrakenDataSourceAdapter>();

            // Register symbol mapping service
            services.AddSingleton<SymbolMappingOptions>(sp =>
                sp.GetRequiredService<IOptions<PriceFeedOptions>>().Value.SymbolMappings);

            // Register services
            services.AddTransient<IPriceAggregationService, PriceAggregationService>();

            // Register batch processing service
            services.AddTransient<IBatchProcessingService, BatchProcessingService>();

            services.AddSingleton<RateLimiter>();
            services.AddSingleton<AttestationService>();
            services.AddSingleton<IAttestationService>(provider => provider.GetRequiredService<AttestationService>());

            // Configure rate limiter
            var serviceProvider = services.BuildServiceProvider();
            var rateLimiter = serviceProvider.GetRequiredService<RateLimiter>();
            rateLimiter.Configure("Binance", 10); // 10 requests per second
            rateLimiter.Configure("CoinMarketCap", 5); // 5 requests per second
            rateLimiter.Configure("Coinbase", 5); // 5 requests per second
            rateLimiter.Configure("OKEx", 5); // 5 requests per second
            rateLimiter.Configure("CoinGecko", 10); // 10 requests per second (free tier)
            rateLimiter.Configure("Kraken", 1); // 1 request per second (public API)

            // Add health checks
            services.AddHealthChecks()
                .AddCheck<DataSourceHealthCheck>("data_sources", tags: new[] { "ready" })
                .AddCheck<NeoRpcHealthCheck>("neo_rpc", tags: new[] { "ready" });

            // Add OpenTelemetry
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService("PriceFeed")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        { "environment", hostContext.HostingEnvironment.EnvironmentName },
                        { "version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown" }
                    }))
                .WithTracing(tracing => tracing
                    .AddSource("PriceFeed")
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter())
                .WithMetrics(metrics => metrics
                    .AddMeter("PriceFeed")
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter());

            // Register the main job
            services.AddTransient<PriceFeedJob>();
        })
        .Build();

    // Run the job
    var job = host.Services.GetRequiredService<PriceFeedJob>();

    if (continuousMode)
    {
        Log.Information("Starting continuous execution for {Duration} minutes with {Interval} second intervals",
            executionDurationMinutes, updateIntervalSeconds);

        var endTime = DateTime.UtcNow.AddMinutes(executionDurationMinutes);
        var iterationCount = 0;

        while (DateTime.UtcNow < endTime)
        {
            iterationCount++;
            var iterationStart = DateTime.UtcNow;

            Log.Information("Starting iteration {Iteration} at {Time}", iterationCount, iterationStart);

            try
            {
                await job.RunAsync(skipHealthChecks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in iteration {Iteration}, continuing...", iterationCount);
            }

            var processingTime = DateTime.UtcNow - iterationStart;
            var remainingDelay = TimeSpan.FromSeconds(updateIntervalSeconds) - processingTime;

            if (remainingDelay > TimeSpan.Zero && DateTime.UtcNow < endTime)
            {
                Log.Information("Iteration {Iteration} completed in {Time}s, waiting {Delay}s for next iteration",
                    iterationCount, processingTime.TotalSeconds, remainingDelay.TotalSeconds);
                await Task.Delay(remainingDelay);
            }
            else if (processingTime > TimeSpan.FromSeconds(updateIntervalSeconds))
            {
                Log.Warning("Iteration {Iteration} took {Time}s, which exceeds the interval of {Interval}s",
                    iterationCount, processingTime.TotalSeconds, updateIntervalSeconds);
            }
        }

        Log.Information("Continuous execution completed after {Count} iterations", iterationCount);
    }
    else
    {
        // Single run mode (existing behavior)
        await job.RunAsync(skipHealthChecks);
    }

    Log.Information("PriceFeed job completed successfully");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "PriceFeed job terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Checks for sensitive environment variables and warns if they're not set
/// For testnet mode, allows configuration via appsettings.json
/// </summary>
static void CheckSensitiveEnvironmentVariables()
{
    // For testnet deployment, check if we have testnet configuration
    // Skip strict environment variable checks if running with testnet config
    bool isTestnetMode = false;

    try
    {
        // Check if appsettings.json has testnet RPC endpoint configured
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("src/PriceFeed.Console/appsettings.json", optional: true)
            .Build();

        var rpcEndpoint = config.GetSection("BatchProcessing:RpcEndpoint").Value;
        isTestnetMode = !string.IsNullOrEmpty(rpcEndpoint) && rpcEndpoint.Contains("seed1t5.neo.org");

        if (isTestnetMode)
        {
            Log.Information("Testnet mode detected. Relaxing environment variable requirements for local development.");
        }
    }
    catch
    {
        // If we can't read config, fall back to strict mode
    }

    // List of required environment variables (relaxed for testnet mode)
    var requiredVariables = new List<string>();

    if (!isTestnetMode)
    {
        requiredVariables.AddRange(new[]
        {
            "NEO_RPC_ENDPOINT",
            "NEO_CONTRACT_HASH",
            "NEO_ACCOUNT_ADDRESS",
            "NEO_ACCOUNT_PRIVATE_KEY"
        });
    }

    // List of optional but recommended environment variables
    var recommendedVariables = new[]
    {
        "BINANCE_API_KEY",
        "BINANCE_API_SECRET",
        "COINMARKETCAP_API_KEY",
        "COINBASE_API_KEY",
        "COINBASE_API_SECRET",
        "OKEX_API_KEY",
        "OKEX_API_SECRET",
        "OKEX_PASSPHRASE"
    };

    // Check required variables
    var missingRequired = requiredVariables
        .Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v)))
        .ToList();

    if (missingRequired.Any())
    {
        Log.Error("Missing required environment variables: {Variables}", string.Join(", ", missingRequired));
        throw new InvalidOperationException($"Missing required environment variables: {string.Join(", ", missingRequired)}");
    }

    // Check recommended variables
    var missingRecommended = recommendedVariables
        .Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v)))
        .ToList();

    if (missingRecommended.Any())
    {
        Log.Warning("Missing recommended environment variables: {Variables}", string.Join(", ", missingRecommended));
    }

    // Inform about configuration mode
    if (isTestnetMode)
    {
        Log.Information("Using testnet configuration with hardcoded accounts for development/testing.");
    }
    else
    {
        Log.Information("Sensitive environment variables are set. Make sure they are securely stored in GitHub Secrets.");
    }
}

/// <summary>
/// Generates a new Neo TEE account and outputs the address and WIF
/// </summary>
/// <param name="secureOutputPath">Optional path to write the account information to a file instead of stdout</param>
static void GenerateNeoAccount(string? secureOutputPath = null)
{
    try
    {
        Log.Information("Generating new Neo TEE account...");

        // Generate a new Neo account using the official Neo SDK
        // This creates a valid Neo N3 account with proper cryptographic keys

        // Generate a new private key
        byte[] privateKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(privateKey);
        }

        // Create a Neo KeyPair from the private key
        var neoPrivateKey = new EpicChain.Wallets.KeyPair(privateKey);

        // Get the WIF (Wallet Import Format) - this is the standard format for Neo private keys
        string wif = neoPrivateKey.Export();

        // Get the Neo address - this is derived from the public key using proper Neo address generation
        var contract = EpicChain.SmartContract.Contract.CreateSignatureContract(neoPrivateKey.PublicKey);
        var scriptHash = contract.ScriptHash;
        string address = scriptHash.ToAddress(Neo.ProtocolSettings.Default.AddressVersion);

        // Output the account information
        if (!string.IsNullOrEmpty(secureOutputPath))
        {
            // Write to secure file instead of stdout
            var accountInfo = $"Address: {address}\nWIF: {wif}";

            // Ensure directory exists
            var directory = Path.GetDirectoryName(secureOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to file with restricted permissions
            File.WriteAllText(secureOutputPath, accountInfo);

            // Set restrictive permissions on the file (Unix-like systems)
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                try
                {
                    // Use chmod to set permissions to owner-only (600)
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"600 {secureOutputPath}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to set restrictive permissions on the secure output file");
                }
            }

            Log.Information("Neo TEE account information written to secure file: {FilePath}", secureOutputPath);
        }
        else
        {
            // Output to stdout (this will be captured by the GitHub Actions workflow)
            // Note: This is less secure as it will appear in logs
            Console.WriteLine($"Address: {address}");
            Console.WriteLine($"WIF: {wif}");
        }

        Log.Information("Neo TEE account generated successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating Neo account");
        throw;
    }
}

/// <summary>
/// Creates an attestation for the TEE account
/// </summary>
/// <param name="accountAddress">The TEE account address</param>
static async Task CreateAccountAttestationAsync(string accountAddress)
{
    try
    {
        Log.Information("Creating attestation for TEE account {AccountAddress}", accountAddress);

        // Build a host to get the AttestationService
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Register the AttestationService
                services.AddLogging(builder => builder.AddSerilog());
                services.AddSingleton<AttestationService>();
            })
            .Build();

        // Get the AttestationService
        var attestationService = host.Services.GetRequiredService<AttestationService>();

        // Get GitHub Actions environment variables
        var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "unknown";
        var runNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "unknown";
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "unknown";
        var workflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "unknown";

        // Split repository into owner and name
        var repoSplit = repository.Split('/');
        var repoOwner = repoSplit.Length > 0 ? repoSplit[0] : "unknown";
        var repoName = repoSplit.Length > 1 ? repoSplit[1] : "unknown";

        // Create attestation
        var attestation = await attestationService.CreateAccountAttestationAsync(
            accountAddress,
            runId,
            runNumber,
            repoOwner,
            repoName,
            workflow);

        Log.Information("Account attestation created successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating account attestation");
        throw;
    }
}

/// <summary>
/// Verifies the TEE account attestation and checks for Master account
/// </summary>
/// <returns>True if the attestation is valid and accounts are properly configured</returns>
static async Task<bool> VerifyAccountAttestationAsync()
{
    try
    {
        Log.Information("Verifying TEE account attestation and checking for Master account");

        // Build a host to get the AttestationService
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Register the AttestationService
                services.AddLogging(builder => builder.AddSerilog());
                services.AddSingleton<AttestationService>();
            })
            .Build();

        // Get the AttestationService
        var attestationService = host.Services.GetRequiredService<AttestationService>();

        // Read the attestation file
        var attestationPath = Path.Combine("attestations", "account_attestation.json");
        if (!File.Exists(attestationPath))
        {
            Log.Error("Account attestation file not found at {Path}", attestationPath);
            return false;
        }

        var attestationJson = await File.ReadAllTextAsync(attestationPath);
        var attestation = JsonConvert.DeserializeObject<AccountAttestationData>(attestationJson);

        if (attestation == null)
        {
            Log.Error("Failed to parse account attestation");
            return false;
        }

        // Verify the attestation
        bool isValid = attestationService.VerifyAccountAttestation(attestation);

        if (isValid)
        {
            Log.Information("TEE account attestation is valid for address {AccountAddress}", attestation.AccountAddress);

            // Verify that the account address matches the TEE account in GitHub Secrets
            var teeAccountAddress = Environment.GetEnvironmentVariable("NEO_TEE_ACCOUNT_ADDRESS");

            // For backward compatibility, also check the old environment variable
            if (string.IsNullOrEmpty(teeAccountAddress))
            {
                teeAccountAddress = Environment.GetEnvironmentVariable("NEO_ACCOUNT_ADDRESS");
            }

            if (string.IsNullOrEmpty(teeAccountAddress))
            {
                Log.Warning("Neither NEO_TEE_ACCOUNT_ADDRESS nor NEO_ACCOUNT_ADDRESS environment variables are set");
            }
            else if (teeAccountAddress != attestation.AccountAddress)
            {
                Log.Error("Account address in attestation ({AttestationAddress}) does not match TEE account address ({EnvAddress})",
                    attestation.AccountAddress, teeAccountAddress);
                return false;
            }

            // Also verify that the Master account is set
            var masterAccountAddress = Environment.GetEnvironmentVariable("NEO_MASTER_ACCOUNT_ADDRESS");
            if (string.IsNullOrEmpty(masterAccountAddress))
            {
                Log.Warning("NEO_MASTER_ACCOUNT_ADDRESS environment variable is not set. " +
                           "This is required for the dual-signature transaction system.");
            }

            return true;
        }
        else
        {
            Log.Error("Account attestation is invalid");
            return false;
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error verifying account attestation");
        return false;
    }
}

/// <summary>
/// Main job class that runs the price feed process
/// </summary>
public class PriceFeedJob
{
    private readonly ILogger<PriceFeedJob> _logger;
    private readonly PriceFeedOptions _options;
    private readonly SymbolMappingOptions _symbolMappings;
    private readonly IEnumerable<IDataSourceAdapter> _dataSourceAdapters;
    private readonly IPriceAggregationService _aggregationService;
    private readonly IBatchProcessingService _batchProcessingService;
    private readonly AttestationService _attestationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceFeedJob"/> class
    /// </summary>
    public PriceFeedJob(
        ILogger<PriceFeedJob> logger,
        IOptions<PriceFeedOptions> options,
        SymbolMappingOptions symbolMappings,
        IEnumerable<IDataSourceAdapter> dataSourceAdapters,
        IPriceAggregationService aggregationService,
        IBatchProcessingService batchProcessingService,
        AttestationService attestationService)
    {
        _logger = logger;
        _options = options.Value;
        _symbolMappings = symbolMappings;
        _dataSourceAdapters = dataSourceAdapters;
        _aggregationService = aggregationService;
        _batchProcessingService = batchProcessingService;
        _attestationService = attestationService;
    }

    /// <summary>
    /// Runs the price feed job
    /// </summary>
    /// <param name="skipHealthChecks">Parameter kept for backward compatibility but no longer used</param>
    public async Task RunAsync(bool skipHealthChecks = false)
    {
        _logger.LogInformation("Starting price feed job with {Count} symbols: {Symbols}",
            _options.Symbols.Count, string.Join(", ", _options.Symbols));

        // Log symbol mappings
        _logger.LogInformation("Symbol mappings configured for {Count} symbols", _symbolMappings.Mappings.Count);

        try
        {
            // 1. Collect price data from all enabled sources
            var priceDataBySymbol = new ConcurrentDictionary<string, List<PriceData>>();

            // Filter out disabled data sources
            var enabledAdapters = _dataSourceAdapters.Where(adapter => adapter.IsEnabled()).ToList();

            if (!enabledAdapters.Any())
            {
                _logger.LogWarning("No enabled data sources found. Please configure at least one data source API key.");
                throw new InvalidOperationException("No enabled data sources found. Please configure at least one data source API key.");
            }

            _logger.LogInformation("Using {Count} enabled data sources: {Sources}",
                enabledAdapters.Count,
                string.Join(", ", enabledAdapters.Select(a => a.SourceName)));

            // Parallelize data collection from all sources
            var dataCollectionTasks = enabledAdapters.Select(async adapter =>
            {
                try
                {
                    _logger.LogInformation("Collecting price data from {Source}", adapter.SourceName);
                    var priceData = await adapter.GetPriceDataBatchAsync(_options.Symbols);
                    _logger.LogInformation("Collected {Count} price data points from {Source}",
                        priceData.Count(), adapter.SourceName);
                    return (adapter.SourceName, priceData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting price data from {Source}", adapter.SourceName);
                    return (adapter.SourceName, Enumerable.Empty<PriceData>());
                }
            });

            // Wait for all data sources to complete
            var results = await Task.WhenAll(dataCollectionTasks);

            // Process results thread-safely using ConcurrentDictionary
            foreach (var (sourceName, priceDataList) in results)
            {
                foreach (var data in priceDataList)
                {
                    priceDataBySymbol.AddOrUpdate(
                        data.Symbol,
                        new List<PriceData> { data },
                        (key, existingList) =>
                        {
                            lock (existingList) // Thread-safe list update
                            {
                                existingList.Add(data);
                                return existingList;
                            }
                        });
                }
            }

            if (!priceDataBySymbol.Any())
            {
                _logger.LogWarning("No price data collected from any source");
                // Instead of just returning, we'll throw a specific exception that can be caught and handled
                throw new InvalidOperationException("Failed to collect price data from any source. Check API keys and network connectivity.");
            }

            // 2. Aggregate price data
            _logger.LogInformation("Aggregating price data for {Count} symbols", priceDataBySymbol.Count);

            var aggregatedPrices = await _aggregationService.AggregateBatchAsync(
                priceDataBySymbol.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value as IEnumerable<PriceData>));

            if (!aggregatedPrices.Any())
            {
                _logger.LogWarning("No aggregated prices to send");
                throw new InvalidOperationException("Failed to aggregate price data. Check if the aggregation service is working correctly.");
            }

            // 3. Create and process batch
            _logger.LogInformation("Creating batch with {Count} aggregated prices", aggregatedPrices.Count());

            var batch = new PriceBatch
            {
                Prices = aggregatedPrices.ToList()
            };

            // 4. Send batch to smart contract with exponential backoff retry logic
            _logger.LogInformation("Sending batch {BatchId} to smart contract", batch.BatchId);

            const int maxRetries = 3;
            const int baseDelayMs = 1000; // 1 second base delay
            const double backoffMultiplier = 2.0; // Exponential backoff factor
            const int maxJitterMs = 500; // Random jitter up to 500ms
            bool success = false;
            var random = new Random();

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    success = await _batchProcessingService.ProcessBatchAsync(batch);

                    if (success)
                    {
                        _logger.LogInformation("Successfully processed batch {BatchId} with {Count} prices",
                            batch.BatchId, batch.Prices.Count);
                        break;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to process batch {BatchId} (Attempt {Attempt}/{MaxAttempts})",
                            batch.BatchId, retry + 1, maxRetries);

                        if (retry < maxRetries - 1)
                        {
                            // Calculate exponential backoff delay with jitter
                            var delayMs = (int)(baseDelayMs * Math.Pow(backoffMultiplier, retry)) + random.Next(0, maxJitterMs);
                            _logger.LogInformation("Retrying in {Delay}ms (exponential backoff)...", delayMs);
                            await Task.Delay(delayMs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch {BatchId} (Attempt {Attempt}/{MaxAttempts})",
                        batch.BatchId, retry + 1, maxRetries);

                    if (retry < maxRetries - 1)
                    {
                        // Calculate exponential backoff delay with jitter
                        var delayMs = (int)(baseDelayMs * Math.Pow(backoffMultiplier, retry)) + random.Next(0, maxJitterMs);
                        _logger.LogInformation("Retrying in {Delay}ms (exponential backoff)...", delayMs);
                        await Task.Delay(delayMs);
                    }
                }
            }

            if (!success)
            {
                _logger.LogError("Failed to process batch {BatchId} after {MaxAttempts} attempts",
                    batch.BatchId, maxRetries);
            }

            // 5. Log detailed price information
            foreach (var price in batch.Prices)
            {
                _logger.LogInformation("Price for {Symbol}: {Price} (Confidence: {Confidence}%)",
                    price.Symbol, price.Price, price.ConfidenceScore);
            }

            // 6. Clean up old attestations
            try
            {
                // Keep attestations for 7 days
                const int retentionDays = 7;

                _logger.LogInformation("Cleaning up attestations older than {RetentionDays} days", retentionDays);
                int removedCount = await _attestationService.CleanupOldAttestationsAsync(retentionDays);

                if (removedCount > 0)
                {
                    _logger.LogInformation("Removed {Count} old attestation files", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up old attestations");
                // Continue processing - cleanup failure shouldn't stop the price feed
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running price feed job");
            throw;
        }
    }
}
