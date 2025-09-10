# PriceFeed Contract Deployer

A comprehensive deployment tool for the Neo Price Feed Oracle smart contract that handles deployment, initialization, and verification via RPC.

## Features

- **Full Automation**: Deploy, initialize, and verify contracts without manual neo-cli commands
- **RPC-based**: All operations performed via Neo RPC API
- **Multiple Commands**: Modular commands for different deployment stages
- **Verification**: Built-in contract state verification

## Prerequisites

- .NET 8.0 SDK
- Master account with at least 10 GAS for deployment
- Access to Neo N3 TestNet RPC endpoint

## Usage

### Commands

```bash
# Show help
dotnet run

# Deploy the contract only
dotnet run deploy

# Initialize an already deployed contract
dotnet run init

# Verify contract status
dotnet run verify

# Full deployment and setup (recommended)
dotnet run full
```

### Full Deployment (Recommended)

The `full` command performs a complete deployment:

1. **Deploy Contract** - Deploys the smart contract to TestNet
2. **Initialize Contract** - Sets owner and TEE account
3. **Configure Oracle** - Adds TEE account as authorized oracle
4. **Set Parameters** - Sets minimum oracle count to 1
5. **Verify Setup** - Confirms everything is configured correctly

```bash
cd src/PriceFeed.ContractDeployer
dotnet run full
```

### Individual Commands

#### Deploy Only
```bash
dotnet run deploy
```
- Deploys the contract to TestNet
- Saves contract hash to `contract-hash.txt`
- Does NOT initialize the contract

#### Initialize Only
```bash
dotnet run init
```
- Initializes an already deployed contract
- Uses contract hash from `contract-hash.txt` or default
- Sets up owner, TEE accounts, and oracle configuration

#### Verify Status
```bash
dotnet run verify
```
- Checks if contract is deployed
- Shows initialization status
- Displays configured accounts and parameters
- Shows sample price data (if available)

## Configuration

The deployer uses hardcoded configuration for security:

- **RPC Endpoint**: `http://seed1t5.neo.org:20332`
- **Master Account**: `NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX`
- **TEE Account**: `NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB`
- **Contract Hash**: `0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc` (after deployment)

## Output

### Successful Full Deployment
```
🚀 Full Deployment and Setup
=============================

🚀 Deploying Price Feed Oracle Contract
=======================================
📍 Deploying with Master Account: NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX
📄 Contract files loaded successfully
🌐 Connected to TestNet: http://seed1t5.neo.org:20332
💰 GAS Balance: 100
📤 Deploying contract to TestNet...
🎯 Expected Contract Hash: 0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc
✅ Deployment transaction sent!
📋 Transaction Hash: 0x123...
⏳ Waiting for transaction confirmation...
✅ Contract deployed successfully!

📝 Initializing Price Feed Oracle Contract
==========================================
1️⃣ Initializing contract with owner and TEE account...
✅ Contract initialized
2️⃣ Adding TEE account as oracle...
✅ TEE account added as oracle
3️⃣ Setting minimum oracles to 1...
✅ Minimum oracles set to 1

🔍 Verifying Price Feed Oracle Contract
=======================================
✅ Contract deployed
   Owner: NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX
   TEE Accounts: Configured
   Oracles: Configured
   Min Oracles: 1

✅ Full deployment and setup completed successfully!
The price feed oracle is now ready to receive price updates.
```

## Troubleshooting

### Insufficient GAS
- Ensure the master account has at least 10 GAS
- Check balance with: `dotnet run verify`

### Contract Not Found
- Ensure contract files exist in `../../deploy/` directory
- Files needed: `PriceFeed.Oracle.nef` and `PriceFeed.Oracle.manifest.json`

### Initialization Fails
- Contract may already be initialized (can only initialize once)
- Check status with: `dotnet run verify`

### Transaction Timeout
- TestNet may be slow, wait a few minutes and retry
- Use `dotnet run verify` to check current state

## Security Notes

- Private keys are hardcoded for TestNet deployment only
- For MainNet deployment, use environment variables or secure key storage
- Always verify contract hash after deployment
- Keep `contract-hash.txt` file for reference

## Next Steps

After successful deployment:

1. Update `appsettings.json` with the deployed contract hash
2. GitHub Actions will automatically start sending price updates every 5 minutes
3. Monitor price updates using `dotnet run verify`
4. Check GitHub Actions logs for price feed status