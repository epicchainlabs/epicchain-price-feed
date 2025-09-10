# Price Oracle Smart Contract

A production-ready Neo N3 smart contract for storing and retrieving price feed data from multiple sources.

## Overview

This smart contract serves as a decentralized price oracle for the Neo blockchain. It allows authorized oracles to submit price data for various cryptocurrency pairs, which can then be accessed by other smart contracts or applications.

## Features

- **Multi-Oracle Support**: Multiple authorized oracles can submit price data
- **Batch Processing**: Efficient batch updates for multiple price feeds
- **Confidence Scoring**: Each price update includes a confidence score
- **Price Deviation Protection**: Prevents extreme price fluctuations
- **Owner Management**: Secure owner management with transfer capability
- **Pause Mechanism**: Contract can be paused in case of emergencies
- **Upgrade Path**: Contract can be upgraded to new versions
- **Comprehensive Events**: Detailed events for all important actions

## Contract Methods

### Administrative Methods

| Method | Description | Parameters | Access |
|--------|-------------|------------|--------|
| `Initialize` | Initializes the contract | `owner`: UInt160 | Anyone (once) |
| `ChangeOwner` | Changes the contract owner | `newOwner`: UInt160 | Owner only |
| `SetPaused` | Pauses or unpauses the contract | `paused`: bool | Owner only |
| `Update` | Upgrades the contract | `nefFile`: ByteString, `manifest`: string, `data`: object | Owner only |
| `AddOracle` | Adds an oracle to the authorized list | `oracleAddress`: UInt160 | Owner only |
| `RemoveOracle` | Removes an oracle from the authorized list | `oracleAddress`: UInt160 | Owner only |

### Oracle Methods

| Method | Description | Parameters | Access |
|--------|-------------|------------|--------|
| `UpdatePrice` | Updates the price for a single symbol | `symbol`: string, `price`: BigInteger, `timestamp`: BigInteger, `confidenceScore`: BigInteger | Oracles only |
| `UpdatePriceBatch` | Updates prices for multiple symbols | `symbols`: string[], `prices`: BigInteger[], `timestamps`: BigInteger[], `confidenceScores`: BigInteger[] | Oracles only |

### Query Methods

| Method | Description | Parameters | Access |
|--------|-------------|------------|--------|
| `GetPrice` | Gets the current price for a symbol | `symbol`: string | Anyone |
| `GetTimestamp` | Gets the timestamp of the current price | `symbol`: string | Anyone |
| `GetConfidenceScore` | Gets the confidence score of the current price | `symbol`: string | Anyone |
| `GetPriceData` | Gets the complete price data for a symbol | `symbol`: string | Anyone |
| `IsOracle` | Checks if an address is an authorized oracle | `address`: UInt160 | Anyone |
| `GetOwner` | Gets the current contract owner | None | Anyone |
| `IsPaused` | Checks if the contract is paused | None | Anyone |

## Events

| Event | Description | Parameters |
|-------|-------------|------------|
| `PriceUpdated` | Emitted when a price is updated | `symbol`: string, `price`: BigInteger, `timestamp`: BigInteger, `confidenceScore`: BigInteger |
| `OracleAdded` | Emitted when an oracle is added | `oracleAddress`: UInt160 |
| `OracleRemoved` | Emitted when an oracle is removed | `oracleAddress`: UInt160 |
| `OwnerChanged` | Emitted when the owner is changed | `oldOwner`: UInt160, `newOwner`: UInt160 |
| `ContractUpgraded` | Emitted when the contract is upgraded | `contractHash`: UInt160 |
| `ContractPaused` | Emitted when the contract is paused or unpaused | `isPaused`: bool |
| `Initialized` | Emitted when the contract is initialized | `owner`: UInt160 |

## Deployment Instructions

### Prerequisites

- [Neo N3 CLI](https://github.com/neo-project/neo-node)
- [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit)

### Deployment Steps

1. Compile the contract using the Neo Compiler:

```bash
nccs
```

2. Deploy the contract using Neo Express:

```bash
neoxp contract deploy bin\sc\PriceFeed.Oracle.nef account1 --force
```

3. Initialize the contract:

```bash
neoxp contract run <contract-hash> Initialize <owner-address> --acount account1
```

4. Add oracles:

```bash
neoxp contract invoke <contract-hash> AddOracle <oracle-address> account1
```

## Usage Examples

### Updating a Price (for Oracles)

```csharp
// Example parameters:
// symbol: "BTCUSDT"
// price: 50000_00000000 (scaled by 10^8)
// timestamp: 1625097600 (Unix timestamp)
// confidenceScore: 95 (0-100)

using EpicChain;
using EpicChain.SmartContract.Framework;
using EpicChain.SmartContract.Framework.Services;

// Get the contract hash
UInt160 contractHash = UInt160.Parse("<contract-hash>");

// Call the UpdatePrice method
object[] args = new object[] { "BTCUSDT", 5000000000000, 1625097600, 95 };
Contract.Call(contractHash, "UpdatePrice", CallFlags.All, args);
```

### Reading a Price (for Consumers)

```csharp
using EpicChain;
using EpicChain.SmartContract.Framework;
using EpicChain.SmartContract.Framework.Services;
using System.Numerics;

// Get the contract hash
UInt160 contractHash = UInt160.Parse("<contract-hash>");

// Call the GetPrice method
BigInteger price = (BigInteger)Contract.Call(contractHash, "GetPrice", CallFlags.ReadOnly, new object[] { "BTCUSDT" });

// Convert the price to a decimal (assuming 8 decimal places)
decimal actualPrice = (decimal)price / 100000000;
```

## Security Considerations

- The contract includes a minimum confidence score requirement to prevent low-quality data
- Price deviation checks prevent extreme price fluctuations
- Only authorized oracles can update prices
- The contract can be paused in case of emergencies
- The owner can be changed if the current owner's keys are compromised

## License

This project is licensed under the MIT License - see the LICENSE file for details.
