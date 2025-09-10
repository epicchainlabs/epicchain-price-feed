# PriceFeed R3E Smart Contract

This is the R3E-optimized version of the PriceFeed Oracle smart contract for Neo N3 blockchain.

## Overview

The R3E (Resilient, Reliable, and Robust Execution) framework provides optimized tools and libraries for Neo smart contract development. This migration brings several benefits:

- **Performance Optimizations**: R3E compiler optimizations for better gas efficiency
- **Enhanced Security**: Built-in security patterns and validations
- **Better Testing**: Comprehensive testing framework with test engine
- **Simplified Deployment**: Streamlined deployment and management tools
- **Type Safety**: Improved type safety and null handling

## Project Structure

```
PriceFeed.R3E/
├── PriceFeed.R3E.Contract/     # Smart contract implementation
├── PriceFeed.R3E.Tests/        # Unit tests
├── PriceFeed.R3E.Deploy/       # Deployment tool
└── PriceFeed.R3E.sln           # Solution file
```

## Key Features

### Smart Contract (PriceFeed.R3E.Contract)

- **Dual-Signature Verification**: Requires both TEE and Master account signatures
- **Price Oracle Functionality**: Store and retrieve price data with confidence scores
- **Circuit Breaker**: Prevents extreme price deviations (>10%)
- **Batch Updates**: Update multiple prices in a single transaction
- **Access Control**: Owner-based permission system
- **R3E Optimizations**:
  - Optimized storage layout using StorageMap
  - Efficient event handling
  - Gas-optimized operations

### Testing (PriceFeed.R3E.Tests)

- Comprehensive unit test coverage
- Test engine for contract simulation
- Tests for:
  - Initialization
  - Price updates (single and batch)
  - Dual signature verification
  - Circuit breaker functionality
  - Access control
  - Edge cases and error handling

### Deployment (PriceFeed.R3E.Deploy)

- Command-line deployment tool
- Supports:
  - Contract deployment
  - Contract initialization
  - Contract verification
  - Future: Contract upgrades

## Prerequisites

1. .NET 8.0 SDK or later
2. R3E.Compiler.CSharp NuGet package
3. Neo N3 node access (TestNet or MainNet)

## Building

```bash
# Build the entire solution
dotnet build PriceFeed.R3E.sln

# Build individual projects
dotnet build PriceFeed.R3E.Contract/PriceFeed.R3E.Contract.csproj
dotnet build PriceFeed.R3E.Tests/PriceFeed.R3E.Tests.csproj
dotnet build PriceFeed.R3E.Deploy/PriceFeed.R3E.Deploy.csproj
```

The R3E compiler will automatically compile the smart contract after building the Contract project.

## Testing

```bash
# Run all tests
dotnet test PriceFeed.R3E.Tests/

# Run with coverage
dotnet test PriceFeed.R3E.Tests/ --collect:"XPlat Code Coverage"
```

## Deployment

### 1. Configure Deployment Settings

Edit `PriceFeed.R3E.Deploy/appsettings.json`:

```json
{
  "Deployment": {
    "Network": "TestNet",
    "RpcEndpoint": "http://seed1t5.neo.org:20332",
    "DeployerWif": "YOUR_DEPLOYER_WIF"
  },
  "Initialization": {
    "OwnerAddress": "YOUR_OWNER_ADDRESS",
    "TeeAccountAddress": "YOUR_TEE_ACCOUNT_ADDRESS"
  }
}
```

### 2. Deploy Contract

```bash
cd PriceFeed.R3E.Deploy
dotnet run -- deploy
```

### 3. Initialize Contract

```bash
dotnet run -- initialize
```

### 4. Verify Deployment

```bash
dotnet run -- verify
```

## Usage Example

```csharp
// Update price with dual signatures
var symbol = "BTCUSDT";
var price = 45000_00000000; // $45,000 with 8 decimals
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var confidence = 95; // 95% confidence

// Transaction must be signed by both TEE and Master accounts
var result = contract.UpdatePrice(symbol, price, timestamp, confidence);
```

## R3E Framework Benefits

1. **Gas Optimization**: R3E compiler optimizations reduce gas consumption
2. **Storage Efficiency**: Optimized storage layout using StorageMap
3. **Security Enhancements**: Built-in validation and security patterns
4. **Testing Framework**: Comprehensive testing tools for contract validation
5. **Deployment Tools**: Simplified deployment and management

## Migration Notes

Key changes from the original Neo contract:

1. **Namespace**: Changed to `R3E.SmartContract.Framework`
2. **Attributes**: Updated to R3E-specific attributes
3. **Storage**: Optimized using StorageMap pattern
4. **Events**: Strongly-typed event delegates
5. **Validation**: Enhanced input validation and error handling
6. **Testing**: Comprehensive test suite using R3E test engine

## Security Considerations

- Dual-signature requirement prevents unauthorized updates
- Circuit breaker prevents extreme price manipulations
- Owner-only administrative functions
- Minimum confidence score requirement (50%)
- Timestamp validation to prevent stale data

## Future Enhancements

- Contract upgrade mechanism
- Additional price feed sources
- Historical price data storage
- Advanced analytics and aggregation
- Multi-asset batch operations

## License

MIT License - See LICENSE file for details