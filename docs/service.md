# Price Feed Service with TEE and Dual-Signature Transactions

A production-ready price feed service for Neo N3 blockchain that uses a Trusted Execution Environment (TEE). It fetches data from multiple sources, aggregates it, and submits it to a smart contract on-chain using a dual-signature transaction system.

## Overview

The Price Feed Service is a .NET application that runs in a Trusted Execution Environment (TEE). It collects price data from multiple cryptocurrency exchanges and data providers, aggregates it, and submits it to the Neo blockchain through the Price Oracle Contract using a dual-signature transaction system with two Neo accounts:

1. **TEE Account**: Generated within the TEE, used to authenticate that transactions are truly generated in the secure environment
2. **Master Account**: Set by you as a secret, contains GAS for transaction fees

### Trusted Execution Environment Benefits

The service leverages a Trusted Execution Environment (TEE) for several security benefits:

- **Isolated Execution**: Each execution runs in a clean, isolated environment
- **Dual-Signature System**: Two Neo accounts are used for each transaction:
  - TEE Account: Authenticates that transactions are generated in the secure environment
  - Master Account: Provides GAS for transaction fees
- **Secret Management**: Sensitive information is securely stored and never exposed
- **Audit Trail**: All actions are logged and can be audited
- **Reproducible Builds**: The entire environment is defined as code, ensuring consistency
- **Attestation Mechanism**: Cryptographic attestations prove that actions were performed within the TEE
- **Asset Transfer**: Any assets received by the TEE account are automatically transferred to the Master account

This approach ensures that only the TEE can send price feed transactions to the blockchain, as the TEE account's private key is only accessible within the secure environment. The Master account provides the GAS for transaction fees, eliminating the need to fund the TEE account. This is similar to how NeoBurger uses a TEE for its operations, but specifically focused on providing price feed data to the Neo blockchain.

### Account Generation and Management

The service uses two Neo accounts with different roles:

#### TEE Account

1. A dedicated key generation process creates the TEE account using cryptographically secure random number generation
2. The account's address and private key are securely stored within the TEE
3. The private key is never exposed outside of the secure environment
4. The same account is used for all price feed transactions, providing a consistent identity
5. Only the TEE can access the private key, ensuring that only the secure environment can send price feed transactions
6. This account is used to authenticate that transactions were generated in the TEE

#### Master Account

1. You create this account using a Neo wallet of your choice
2. You fund this account with GAS for transaction fees
3. You securely provide the account's address and private key to the TEE
4. This account is used to pay for transaction fees
5. Any assets received by the TEE account are automatically transferred to this account

### Attestation Mechanism

The service implements a comprehensive attestation mechanism to prove that actions were performed within the secure TEE:

#### Account Attestation

When the TEE account is generated, an attestation is created that includes:
- The TEE account address
- The execution environment identifier
- A timestamp
- A cryptographic signature that can only be generated within the TEE

This attestation is securely stored with a 90-day retention period, providing long-term proof that the TEE account was generated within the secure environment.

#### Price Feed Attestations

For each price feed update, an attestation is created that includes:
- The batch ID and transaction hash
- A summary of the price data
- The execution environment identifier
- A timestamp
- A cryptographic signature that can only be generated within the TEE

These attestations are stored with a 7-day retention period, providing short-term proof that price feed updates were performed within the secure environment while keeping storage requirements minimal.

#### Verification

The attestations can be verified by:
1. Checking the cryptographic signature to ensure it was generated within the TEE
2. Verifying that the execution environment identifier and other metadata match the expected values
3. Confirming that the TEE account address matches the one used for price feed transactions
4. Verifying that transactions are signed by both the TEE and Master accounts

This verification process, combined with the dual-signature transaction system, provides strong evidence that the price feed updates are legitimate and were performed within the secure TEE.

## Features

- **Multi-Source Data Collection**: Fetches price data from multiple sources:
  - Binance
  - CoinMarketCap
  - Coinbase
  - OKEx
- **Advanced Aggregation**: Aggregates price data using volume-weighted average or simple average
- **Confidence Scoring**: Calculates confidence scores based on standard deviation and number of sources
- **Batch Processing**: Sends batched price updates to a Neo smart contract
- **TEE Integration**: Runs periodically in a secure Trusted Execution Environment
- **Production-Ready**:
  - Comprehensive error handling and logging
  - Environment variables for configuration
  - Secure secrets management
  - Detailed execution logs
  - Fallback mechanisms when data sources are unavailable

## Architecture

The solution is structured into the following projects:

- **PriceFeed.Core**: Contains domain models and interfaces
- **PriceFeed.Infrastructure**: Implements data source adapters and services
- **PriceFeed.Console**: Console application that runs in the TEE
- **PriceFeed.Contracts**: Contains the Neo smart contract
- **PriceFeed.Tests**: Contains unit tests

### System Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│                 │     │                 │     │                 │
│  Data Sources   │────▶│  Price Feed     │────▶│  Neo Blockchain │
│  (Binance, etc) │     │  Console App    │     │  Smart Contract │
│                 │     │  (Secure TEE)   │     │                 │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

## Configuration

The application can be configured using environment variables in the TEE:

**Data Source API Keys:**
- `BINANCE_API_KEY`: API key for Binance
- `BINANCE_API_SECRET`: API secret for Binance
- `COINMARKETCAP_API_KEY`: API key for CoinMarketCap
- `COINBASE_API_KEY`: API key for Coinbase
- `COINBASE_API_SECRET`: API secret for Coinbase
- `OKEX_API_KEY`: API key for OKEx
- `OKEX_API_SECRET`: API secret for OKEx
- `OKEX_PASSPHRASE`: Passphrase for OKEx API

**Neo Blockchain Configuration:**
- `NEO_RPC_ENDPOINT`: Neo RPC endpoint URL
- `NEO_CONTRACT_HASH`: Script hash of the deployed contract
- `NEO_WALLET_WIF`: WIF of the wallet to use for signing transactions

**General Configuration:**
- `SYMBOLS`: Comma-separated list of symbols to collect price data for

These environment variables are securely stored as GitHub Secrets.

Alternatively, you can use the `appsettings.json` file for local development:

```json
{
  "PriceFeed": {
    "Symbols": ["NEOBTC", "NEOUSDT", "BTCUSDT", "ETHUSDT"]
  },
  "Binance": {
    "BaseUrl": "https://api.binance.com",
    "TickerPriceEndpoint": "/api/v3/ticker/price",
    "Ticker24hEndpoint": "/api/v3/ticker/24hr",
    "ExchangeInfoEndpoint": "/api/v3/exchangeInfo",
    "TimeoutSeconds": 30
  },
  "CoinMarketCap": {
    "BaseUrl": "https://pro-api.coinmarketcap.com",
    "LatestQuotesEndpoint": "/v1/cryptocurrency/quotes/latest",
    "TimeoutSeconds": 30
  },
  "Coinbase": {
    "BaseUrl": "https://api.coinbase.com",
    "ExchangeRatesEndpoint": "/v2/exchange-rates",
    "SpotPriceEndpoint": "/v2/prices",
    "TimeoutSeconds": 30
  },
  "OKEx": {
    "BaseUrl": "https://www.okex.com",
    "TickerEndpoint": "/api/v5/market/ticker",
    "TimeoutSeconds": 30
  },
  "BatchProcessing": {
    "RpcEndpoint": "http://localhost:10332",
    "ContractScriptHash": "0xd2a4cff31913016155e38e474a2c06d08be276cf",
    "WalletWif": "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr",
    "MaxBatchSize": 50
  }
}
```

## TEE Execution Schedule

The TEE execution is defined in a configuration file:

```yaml
name: Scheduled Price Feed

schedule:
  # Run once per week on Monday at 00:00 UTC
  cron: '0 0 * * 1'

  # Allow manual triggering
  manual: true

execution:
  environment: secure-tee

  steps:
  - name: Build Application
    command: dotnet build --configuration Release

  - name: Run Price Feed
    command: dotnet run --project PriceFeed.Console/PriceFeed.Console.csproj --configuration Release
    environment:
      # Binance API credentials
      BINANCE_API_KEY: ${BINANCE_API_KEY}
      BINANCE_API_SECRET: ${BINANCE_API_SECRET}

      # CoinMarketCap API credentials
      COINMARKETCAP_API_KEY: ${COINMARKETCAP_API_KEY}

      # Coinbase API credentials
      COINBASE_API_KEY: ${COINBASE_API_KEY}
      COINBASE_API_SECRET: ${COINBASE_API_SECRET}

      # OKEx API credentials
      OKEX_API_KEY: ${OKEX_API_KEY}
      OKEX_API_SECRET: ${OKEX_API_SECRET}
      OKEX_PASSPHRASE: ${OKEX_PASSPHRASE}

      # Neo blockchain configuration
      NEO_RPC_ENDPOINT: ${NEO_RPC_ENDPOINT}
      NEO_CONTRACT_HASH: ${NEO_CONTRACT_HASH}

      # TEE Account credentials
      NEO_TEE_ACCOUNT_ADDRESS: ${NEO_TEE_ACCOUNT_ADDRESS}
      NEO_TEE_ACCOUNT_PRIVATE_KEY: ${NEO_TEE_ACCOUNT_PRIVATE_KEY}

      # Master Account credentials
      NEO_MASTER_ACCOUNT_ADDRESS: ${NEO_MASTER_ACCOUNT_ADDRESS}
      NEO_MASTER_ACCOUNT_PRIVATE_KEY: ${NEO_MASTER_ACCOUNT_PRIVATE_KEY}

      # Asset transfer configuration
      CHECK_AND_TRANSFER_TEE_ASSETS: "true"

      # Symbols to fetch
      SYMBOLS: "BTCUSDT,ETHUSDT,BNBUSDT,XRPUSDT,ADAUSDT,SOLUSDT,DOGEUSDT,DOTUSDT,MATICUSDT,LTCUSDT,AVAXUSDT,LINKUSDT,UNIUSDT,ATOMUSDT,NEOUSDT,GASUSDT,FLMUSDT"
```

## Running Locally

To run the Price Feed Service locally:

1. Clone the repository:
   ```bash
   git clone https://github.com/r3e-network/neo-price-feed.git
   cd neo-price-feed
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Configure the application by editing `PriceFeed.Console/appsettings.json` or setting environment variables.

4. Run the console application:
   ```bash
   cd PriceFeed.Console
   dotnet run
   ```

## Logging

The application outputs detailed logs to the console when running in the TEE. These logs include:

- Price data collection from each source
- Aggregation results
- Batch processing status
- Detailed price information for each symbol

The application uses Serilog for structured logging. When running locally, logs are written to the console with a clear, readable format.

## Source Code

The full source code for the Price Feed Service is available in the [repository](https://github.com/r3e-network/neo-price-feed).
