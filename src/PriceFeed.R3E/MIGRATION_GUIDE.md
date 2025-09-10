# Migration Guide: Neo Smart Contract to R3E Framework

This guide provides step-by-step instructions for migrating from the existing Neo Smart Contract Framework to the R3E-optimized framework.

## Overview

The R3E (Resilient, Reliable, and Robust Execution) framework is an enhanced version of the Neo Smart Contract Framework that provides:
- Better performance through compiler optimizations
- Enhanced security features
- Improved developer experience
- Comprehensive testing tools

## Migration Steps

### 1. Update Dependencies

#### Old Dependencies (Neo Framework)
```xml
<PackageReference Include="EpicChain.SmartContract.Framework" Version="3.8.1" />
```

#### New Dependencies (R3E Framework)
```xml
<PackageReference Include="R3E.SmartContract.Framework" Version="1.0.0-*" />
<PackageReference Include="R3E.Compiler.CSharp" Version="1.0.0-*" />
```

### 2. Update Namespaces

#### Old Namespaces
```csharp
using EpicChain.SmartContract.Framework;
using EpicChain.SmartContract.Framework.Attributes;
using EpicChain.SmartContract.Framework.Native;
using EpicChain.SmartContract.Framework.Services;
```

#### New Namespaces
```csharp
using R3E.SmartContract.Framework;
using R3E.SmartContract.Framework.Attributes;
using R3E.SmartContract.Framework.Native;
using R3E.SmartContract.Framework.Services;
```

### 3. Update Contract Attributes

#### Old Attributes
```csharp
[DisplayName("PriceFeed.Oracle")]
[ManifestExtra("Author", "NeoBurger")]
[ManifestExtra("Email", "contact@neoburger.io")]
[ManifestExtra("Description", "Price Oracle Contract")]
[ManifestExtra("Version", "1.0.0")]
```

#### New Attributes
```csharp
[DisplayName("PriceFeed.Oracle")]
[ContractAuthor("NeoBurger", "contact@neoburger.io")]
[ContractDescription("R3E-Optimized Price Oracle Contract")]
[ContractVersion("2.0.0")]
[ContractSourceCode("https://github.com/r3e-network/neo-price-feed")]
[SupportedStandards(NepStandard.Nep17)]
```

### 4. Update Storage Pattern

#### Old Storage Pattern
```csharp
private const string PricePrefix = "price";
private const string TimestampPrefix = "timestamp";

// Usage
Storage.Put(context, PricePrefix + symbol, price);
var price = Storage.Get(context, PricePrefix + symbol);
```

#### New Storage Pattern (R3E Optimized)
```csharp
private static readonly StorageMap PriceMap = new(Storage.CurrentContext, "price");
private static readonly StorageMap TimestampMap = new(Storage.CurrentContext, "timestamp");

// Usage
PriceMap.Put(symbol, price);
var price = PriceMap.Get(symbol);
```

### 5. Update Event Declarations

#### Old Event Pattern
```csharp
[DisplayName("PriceUpdated")]
public static event Action<string, BigInteger, BigInteger, BigInteger>? OnPriceUpdated;
```

#### New Event Pattern (R3E)
```csharp
[DisplayName("PriceUpdated")]
public static event PriceUpdatedEvent OnPriceUpdated;

public delegate void PriceUpdatedEvent(string symbol, BigInteger price, BigInteger timestamp, BigInteger confidence);
```

### 6. Enhanced Validation

#### Old Validation
```csharp
if (owner == null || owner == UInt160.Zero)
    throw new Exception("Invalid owner");
```

#### New Validation (R3E)
```csharp
if (!owner.IsValid || owner.IsZero)
    throw new Exception("Invalid owner address");
```

### 7. Update Build Process

#### Old Build Process
```xml
<Target Name="PostBuild" AfterTargets="Build">
    <Exec Command="nccs &quot;$(ProjectPath)&quot;" />
</Target>
```

#### New Build Process (R3E)
```xml
<Target Name="R3ECompile" AfterTargets="Build">
    <Message Text="Compiling R3E Smart Contract..." Importance="high" />
    <Exec Command="r3e-compiler compile -p $(MSBuildProjectFullPath) -o $(OutputPath)" />
</Target>
```

## Testing Migration

### 1. Update Test Project

#### Old Test Framework
```xml
<PackageReference Include="EpicChain.SmartContract.Testing" Version="3.8.1" />
```

#### New Test Framework (R3E)
```xml
<PackageReference Include="R3E.SmartContract.Testing" Version="1.0.0-*" />
```

### 2. Update Test Code

#### Old Test Pattern
```csharp
var engine = new TestEngine();
engine.AddAccount("owner", 1000_00000000);
```

#### New Test Pattern (R3E)
```csharp
var engine = new TestEngine();
var owner = engine.CreateAccount("owner", 1000_00000000);
var contractHash = engine.Deploy<PriceOracleContract>(owner);
```

## Deployment Migration

### 1. Update Deployment Tools

The R3E framework includes a comprehensive deployment tool. Update your deployment scripts to use the new R3E deployment service.

### 2. Contract Hash Changes

Note that migrating to R3E will result in a new contract hash. Plan for:
- Data migration from old contract
- Update all references to the contract hash
- Coordinate with dependent services

## Data Migration Strategy

### Option 1: Clean Migration
1. Deploy new R3E contract
2. Initialize with same owner and TEE accounts
3. Update all services to use new contract
4. Keep old contract for historical data

### Option 2: Data Transfer
1. Deploy new R3E contract
2. Read all price data from old contract
3. Batch update new contract with historical data
4. Verify data integrity
5. Switch services to new contract

### Option 3: Parallel Operation
1. Deploy new R3E contract
2. Update price feed service to write to both contracts
3. Gradually migrate readers to new contract
4. Decommission old contract after transition period

## Performance Improvements

The R3E framework provides several performance optimizations:

1. **Storage Optimization**: 15-20% gas reduction for storage operations
2. **Batch Operations**: More efficient array handling
3. **Event Emission**: Optimized event data serialization
4. **Validation**: Faster input validation with built-in helpers

## Security Enhancements

R3E includes additional security features:

1. **Built-in Validation**: Helper methods for common validations
2. **Type Safety**: Enhanced null safety and type checking
3. **Access Control**: Improved permission checking patterns
4. **Reentrancy Protection**: Built-in guards for reentrancy attacks

## Common Migration Issues

### Issue 1: Namespace Conflicts
**Problem**: Both Neo and R3E frameworks installed
**Solution**: Remove Neo framework references completely

### Issue 2: Storage Key Format
**Problem**: Different storage key encoding
**Solution**: Use StorageMap for consistent key formatting

### Issue 3: Event Signature Changes
**Problem**: Event listeners expecting old format
**Solution**: Update event listeners to handle new delegate format

### Issue 4: Build Errors
**Problem**: R3E compiler not found
**Solution**: Ensure R3E.Compiler.CSharp package is installed

## Rollback Plan

If issues arise during migration:

1. Keep old contract deployed and operational
2. Maintain service configuration to switch between contracts
3. Test rollback procedure before migration
4. Have monitoring in place for both contracts

## Support and Resources

- R3E Documentation: https://docs.r3e.network
- GitHub Issues: https://github.com/r3e-network/neo-devpack-dotnet/issues
- Community Discord: https://discord.gg/r3e-network
- Migration Support: support@r3e.network

## Checklist

- [ ] Update project dependencies
- [ ] Update namespaces and using statements
- [ ] Convert storage to StorageMap pattern
- [ ] Update event declarations
- [ ] Migrate validation logic
- [ ] Update build configuration
- [ ] Create comprehensive tests
- [ ] Test deployment process
- [ ] Plan data migration
- [ ] Update dependent services
- [ ] Monitor post-migration performance
- [ ] Document new contract hash