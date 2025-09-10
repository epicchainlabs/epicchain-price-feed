# Price Oracle Contract with Dual-Signature Verification

A production-ready Neo N3 smart contract for storing and retrieving price feed data from multiple sources, with dual-signature verification for TEE authentication and transaction fees.

## Overview

The Price Oracle Contract is a decentralized price oracle for the Neo blockchain. It allows authorized oracles to submit price data for various cryptocurrency pairs, which can then be accessed by other smart contracts or applications. The contract implements a dual-signature verification system:

1. **TEE Account Signature**: Verifies that the transaction was generated in the GitHub Actions environment
2. **Master Account Signature**: Provides the GAS for transaction fees

This dual-signature approach ensures that price feed updates are both authentic (generated in the TEE) and properly funded (transaction fees paid by the Master account).

## Features

- **Dual-Signature Verification**: Requires signatures from both TEE and Master accounts
- **TEE Account Management**: Authorized TEE accounts can be added and removed
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
| `Initialize` | Initializes the contract | `owner`: UInt160, `initialTeeAccount`: UInt160 (optional) | Anyone (once) |
| `ChangeOwner` | Changes the contract owner | `newOwner`: UInt160 | Owner only |
| `SetPaused` | Pauses or unpauses the contract | `paused`: bool | Owner only |
| `Update` | Upgrades the contract | `nefFile`: ByteString, `manifest`: string, `data`: object | Owner only |
| `AddOracle` | Adds an oracle to the authorized list | `oracleAddress`: UInt160 | Owner only |
| `RemoveOracle` | Removes an oracle from the authorized list | `oracleAddress`: UInt160 | Owner only |
| `AddTeeAccount` | Adds a TEE account to the authorized list | `teeAccountAddress`: UInt160 | Owner only |
| `RemoveTeeAccount` | Removes a TEE account from the authorized list | `teeAccountAddress`: UInt160 | Owner only |

### Oracle Methods

| Method | Description | Parameters | Access |
|--------|-------------|------------|--------|
| `UpdatePrice` | Updates the price for a single symbol | `symbol`: string, `price`: BigInteger, `timestamp`: BigInteger, `confidenceScore`: BigInteger | Oracles with TEE signature |
| `UpdatePriceBatch` | Updates prices for multiple symbols | `symbols`: string[], `prices`: BigInteger[], `timestamps`: BigInteger[], `confidenceScores`: BigInteger[] | Oracles with TEE signature |

### Query Methods

| Method | Description | Parameters | Access |
|--------|-------------|------------|--------|
| `GetPrice` | Gets the current price for a symbol | `symbol`: string | Anyone |
| `GetTimestamp` | Gets the timestamp of the current price | `symbol`: string | Anyone |
| `GetConfidenceScore` | Gets the confidence score of the current price | `symbol`: string | Anyone |
| `GetPriceData` | Gets the complete price data for a symbol | `symbol`: string | Anyone |
| `IsOracle` | Checks if an address is an authorized oracle | `address`: UInt160 | Anyone |
| `IsTeeAccount` | Checks if an address is an authorized TEE account | `address`: UInt160 | Anyone |
| `GetOwner` | Gets the current contract owner | None | Anyone |
| `IsPaused` | Checks if the contract is paused | None | Anyone |

## Events

| Event | Description | Parameters |
|-------|-------------|------------|
| `PriceUpdated` | Emitted when a price is updated | `symbol`: string, `price`: BigInteger, `timestamp`: BigInteger, `confidenceScore`: BigInteger |
| `OracleAdded` | Emitted when an oracle is added | `oracleAddress`: UInt160 |
| `OracleRemoved` | Emitted when an oracle is removed | `oracleAddress`: UInt160 |
| `TeeAccountAdded` | Emitted when a TEE account is added | `teeAccountAddress`: UInt160 |
| `TeeAccountRemoved` | Emitted when a TEE account is removed | `teeAccountAddress`: UInt160 |
| `OwnerChanged` | Emitted when the owner is changed | `oldOwner`: UInt160, `newOwner`: UInt160 |
| `ContractUpgraded` | Emitted when the contract is upgraded | `contractHash`: UInt160 |
| `ContractPaused` | Emitted when the contract is paused or unpaused | `isPaused`: bool |
| `Initialized` | Emitted when the contract is initialized | `owner`: UInt160 |

## Security Considerations

- **Dual-Signature Verification**: Transactions must be signed by both an authorized oracle and an authorized TEE account
- **TEE Authentication**: Only transactions generated in the GitHub Actions environment (with TEE account signature) are accepted
- **Transaction Fee Management**: The Master account pays for transaction fees, eliminating the need to fund the TEE account
- **Minimum Confidence Score**: The contract includes a minimum confidence score requirement to prevent low-quality data
- **Price Deviation Checks**: Prevent extreme price fluctuations
- **Authorized Access**: Only authorized oracles and TEE accounts can update prices
- **Emergency Controls**: The contract can be paused in case of emergencies
- **Owner Management**: The owner can be changed if the current owner's keys are compromised

## Usage Examples

### Updating a Price (for Oracles with Dual Signatures)

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

// Get the TEE account and Oracle account
UInt160 teeAccount = UInt160.Parse("<tee-account-address>");
UInt160 oracleAccount = UInt160.Parse("<oracle-account-address>");

// Create signers array with both accounts
Signer[] signers = new Signer[]
{
    new Signer { Account = teeAccount, Scopes = WitnessScope.CalledByEntry },
    new Signer { Account = oracleAccount, Scopes = WitnessScope.CalledByEntry }
};

// Call the UpdatePrice method with both signatures
object[] args = new object[] { "BTCUSDT", 5000000000000, 1625097600, 95 };
Contract.Call(contractHash, "UpdatePrice", CallFlags.All, args, signers);
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

## Source Code

The full source code for the Price Oracle Contract is available in the [GitHub repository](https://github.com/yourusername/pricefeed/tree/main/PriceFeed.Contracts).
