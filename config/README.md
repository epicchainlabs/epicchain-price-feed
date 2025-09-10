# Configuration Templates

This directory contains configuration templates and examples for the Neo Price Feed service.

## Files

- **`appsettings.production.json`** - Production configuration template
- **`deploy-config.json`** - Deployment configuration example

## Configuration Overview

The application supports configuration through:

1. **Environment Variables** (recommended for production)
2. **appsettings.json files** (for development)
3. **Command-line arguments** (for specific operations)

## Environment Variables

### Required for Production

```bash
# Neo Blockchain Configuration
NEO_RPC_ENDPOINT=https://mainnet1.neo.coz.io:443
CONTRACT_SCRIPT_HASH=<your-deployed-contract-hash>

# Account Configuration
TEE_ACCOUNT_ADDRESS=<tee-account-address>
TEE_ACCOUNT_PRIVATE_KEY=<tee-account-wif>
MASTER_ACCOUNT_ADDRESS=<master-account-address>
MASTER_ACCOUNT_PRIVATE_KEY=<master-account-wif>

# Symbol Configuration
SYMBOLS=NEOBTC,NEOUSDT,BTCUSDT,ETHUSDT
```

### Optional Data Source API Keys

```bash
# Binance
BINANCE_API_KEY=<your-api-key>
BINANCE_API_SECRET=<your-api-secret>

# CoinMarketCap
COINMARKETCAP_API_KEY=<your-api-key>

# Coinbase
COINBASE_API_KEY=<your-api-key>
COINBASE_API_SECRET=<your-api-secret>

# OKEx
OKEX_API_KEY=<your-api-key>
OKEX_API_SECRET=<your-api-secret>
OKEX_PASSPHRASE=<your-passphrase>
```

## Security Notes

- **Never commit sensitive configuration** to version control
- Use **GitHub Secrets** for production deployment
- Store **private keys securely** using proper key management
- Regularly **rotate API keys** for data sources

## Local Development

For local development, copy `src/PriceFeed.Console/appsettings.json` and modify as needed:

```bash
cp src/PriceFeed.Console/appsettings.json src/PriceFeed.Console/appsettings.Development.json
```

The Development configuration file is gitignored and safe for local credentials.

## Production Deployment

1. **Deploy smart contract** to Neo N3 network
2. **Configure environment variables** in your deployment environment
3. **Set up monitoring** and alerting
4. **Test with small batches** before full deployment

For detailed deployment instructions, see [docs/deployment.md](../docs/deployment.md).