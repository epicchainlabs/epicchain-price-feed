# Neo N3 Price Feed Service with TEE

A production-ready price feed service for Neo N3 blockchain that uses a Trusted Execution Environment (TEE). The service fetches data from multiple sources, aggregates it, and submits it to a smart contract on-chain using a securely generated account.

## Overview

This Neo N3 Price Feed Service is a comprehensive solution for providing reliable price data to the Neo blockchain. It consists of two main components:

1. **Price Feed Service with TEE**: A .NET application that runs in a Trusted Execution Environment (TEE). It collects price data from multiple sources, aggregates it, and submits it to the blockchain using a securely generated Neo account.
2. **Price Oracle Contract**: A Neo N3 smart contract that stores the price data and makes it available to other contracts and applications.

### Trusted Execution Environment (TEE)

The service uses a Trusted Execution Environment (TEE) for several security benefits:

- **Isolated Execution**: Each run executes in a clean, isolated environment
- **Secure Account Generation**: Neo accounts are generated within the TEE and not persisted
- **Secret Management**: Sensitive information is stored securely and never exposed
- **Audit Trail**: All actions are logged and can be audited
- **Reproducible Builds**: The entire environment is defined as code, ensuring consistency

## Features

### Price Feed Service

- **Multi-Source Data Collection**: Fetches price data from multiple sources:
  - Binance
  - CoinMarketCap
  - Coinbase
  - OKEx
- **Advanced Aggregation**: Aggregates price data using volume-weighted average or simple average
- **Confidence Scoring**: Calculates confidence scores based on standard deviation and number of sources
- **Batch Processing**: Sends batched price updates to a Neo smart contract
- **TEE Integration**: Runs periodically in a secure Trusted Execution Environment

### Price Oracle Contract

- **Multi-Oracle Support**: Multiple authorized oracles can submit price data
- **Batch Processing**: Efficient batch updates for multiple price feeds
- **Confidence Scoring**: Each price update includes a confidence score
- **Price Deviation Protection**: Prevents extreme price fluctuations
- **Owner Management**: Secure owner management with transfer capability
- **Pause Mechanism**: Contract can be paused in case of emergencies
- **Upgrade Path**: Contract can be upgraded to new versions

## Documentation

- [Price Feed Service Documentation](service.html)
- [Price Oracle Contract Documentation](contract.html)
- [API Reference](api.html)
- [Deployment Guide](deployment.html)

## Getting Started

To get started with the Neo Price Feed, follow these steps:

1. Clone the repository:
   ```bash
   git clone https://github.com/r3e-network/neo-price-feed.git
   cd neo-price-feed
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Deploy the smart contract (see [Deployment Guide](deployment.html))

4. Configure the price feed service (see [Price Feed Service Documentation](service.html))

5. Run the price feed service:
   ```bash
   cd PriceFeed.Console
   dotnet run
   ```

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/r3e-network/neo-price-feed/blob/main/LICENSE) file for details.
