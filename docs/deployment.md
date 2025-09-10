# Deployment Guide

This guide provides step-by-step instructions for deploying both the Price Oracle Contract and the Price Feed Service.

## Deploying the Price Oracle Contract

### Prerequisites

- [Neo N3 CLI](https://github.com/neo-project/neo-node)
- [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)

### Step 1: Compile the Contract

1. Open the solution in Visual Studio or Visual Studio Code.

2. Build the contract in Release mode:
   ```bash
   dotnet publish PriceFeed.Contracts/PriceFeed.Contracts.csproj -c Release
   ```

3. The compiled contract files (`.nef` and `.manifest.json`) will be located in the `PriceFeed.Contracts/bin/Release/net6.0/publish` directory.

### Step 2: Deploy the Contract to a Private Network (for Testing)

1. Start Neo Express:
   ```bash
   neoxp start -c 1
   ```

2. Create a wallet:
   ```bash
   neoxp wallet create owner
   ```

3. Deploy the contract:
   ```bash
   neoxp contract deploy PriceFeed.Contracts/bin/Release/net6.0/publish/PriceFeed.Contracts.nef owner
   ```

4. Initialize the contract:
   ```bash
   neoxp contract invoke <contract-hash> Initialize [<owner-address>] owner
   ```

### Step 3: Deploy the Contract to the Neo N3 MainNet or TestNet

1. Open Neo GUI or use Neo CLI.

2. Import your wallet.

3. Deploy the contract using the `.nef` and `.manifest.json` files.

4. Initialize the contract by calling the `Initialize` method with your wallet address as the owner.

5. Add oracles by calling the `AddOracle` method with the oracle addresses.

## Deploying the Price Feed Service

### Prerequisites

- [GitHub](https://github.com/) account
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- API keys for the data sources (Binance, CoinMarketCap, Coinbase, OKEx)

### Step 1: Fork or Clone the Repository

1. Fork the repository on GitHub or clone it to your local machine:
   ```bash
   git clone https://github.com/yourusername/pricefeed.git
   cd pricefeed
   ```

### Step 2: Configure GitHub Secrets

1. Go to your repository on GitHub.

2. Navigate to Settings > Secrets and variables > Actions.

3. Add the following secrets:
   - `BINANCE_API_KEY`: Your Binance API key
   - `BINANCE_API_SECRET`: Your Binance API secret
   - `COINMARKETCAP_API_KEY`: Your CoinMarketCap API key
   - `COINBASE_API_KEY`: Your Coinbase API key
   - `COINBASE_API_SECRET`: Your Coinbase API secret
   - `OKEX_API_KEY`: Your OKEx API key
   - `OKEX_API_SECRET`: Your OKEx API secret
   - `OKEX_PASSPHRASE`: Your OKEx passphrase
   - `NEO_RPC_ENDPOINT`: Your Neo RPC endpoint URL
   - `NEO_CONTRACT_HASH`: The script hash of your deployed contract
   - `REPO_ACCESS_TOKEN`: A GitHub personal access token with repo scope (needed for the key generation workflow)

### Step 2.1: Generate a Neo Account for Price Feed

Instead of manually creating a Neo account, we'll use the secure key generation workflow:

1. Go to your repository on GitHub.

2. Navigate to Actions > GenerateKey workflow.

3. Click "Run workflow" to generate a new Neo account.

4. The workflow will:
   - Generate a new Neo account using secure random number generation
   - Store the account address and private key as GitHub Secrets:
     - `NEO_ACCOUNT_ADDRESS`: The public address of the account
     - `NEO_ACCOUNT_PRIVATE_KEY`: The private key of the account

> **Important Security Note**: The `NEO_ACCOUNT_PRIVATE_KEY` is only accessible within the GitHub Actions environment and is used to sign transactions. This ensures that only the GitHub Actions workflow can send price feed transactions to the blockchain, as the account's private key is never exposed outside of GitHub's secure environment.

### Step 3: Configure the GitHub Actions Workflow

1. Edit the `.github/workflows/scheduled-price-feed.yml` file to adjust the schedule if needed:
   ```yaml
   on:
     schedule:
       # Run once per week on Monday at 00:00 UTC
       - cron: '0 0 * * 1'
     workflow_dispatch:  # Allow manual triggering
   ```

2. Commit and push the changes:
   ```bash
   git add .
   git commit -m "Configure GitHub Actions workflow"
   git push
   ```

### Step 4: Enable GitHub Actions

1. Go to your repository on GitHub.

2. Navigate to Actions.

3. Click "I understand my workflows, go ahead and enable them".

### Step 5: Trigger the Workflow Manually (Optional)

1. Go to your repository on GitHub.

2. Navigate to Actions.

3. Select the "Scheduled Price Feed" workflow.

4. Click "Run workflow".

## Setting Up GitHub Pages

### Step 1: Configure GitHub Pages

1. Go to your repository on GitHub.

2. Navigate to Settings > Pages.

3. Under "Source", select "Deploy from a branch".

4. Under "Branch", select "main" and "/docs" folder, then click "Save".

### Step 2: Customize the Documentation

1. Edit the files in the `docs` directory to customize the documentation for your deployment.

2. Commit and push the changes:
   ```bash
   git add docs
   git commit -m "Update documentation"
   git push
   ```

3. Wait a few minutes for GitHub Pages to build and deploy your site.

4. Visit `https://yourusername.github.io/pricefeed` to view your documentation.

## Monitoring and Maintenance

### Monitoring the Price Feed Service

1. Go to your repository on GitHub.

2. Navigate to Actions.

3. Select the "Scheduled Price Feed" workflow to view the execution history and logs.

### Monitoring the Price Oracle Contract

1. Use a Neo blockchain explorer to view the contract's storage and transactions.

2. Monitor the contract's events using a Neo node or blockchain explorer.

### Updating the Price Feed Service

1. Make changes to the code.

2. Commit and push the changes:
   ```bash
   git add .
   git commit -m "Update Price Feed Service"
   git push
   ```

3. The changes will be automatically deployed the next time the GitHub Actions workflow runs.

### Upgrading the Price Oracle Contract

1. Make changes to the contract code.

2. Compile the new version of the contract.

3. Call the `Update` method on the deployed contract with the new NEF file and manifest.

## Troubleshooting

### Price Feed Service Issues

- Check the GitHub Actions logs for error messages.
- Verify that all required secrets are correctly configured.
- Ensure that the Neo RPC endpoint is accessible from GitHub Actions.

### Price Oracle Contract Issues

- Use the Neo debugger to step through the contract execution.
- Check the contract's storage to verify that data is being stored correctly.
- Monitor the contract's events to track state changes.
