#!/bin/bash

# One-command initialization script for the deployed contract
# This script initializes the contract with all required setup

set -e

echo "🚀 Neo Price Feed Contract - One-Command Initialization"
echo "========================================================"

# Contract details
CONTRACT_HASH="0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
MASTER_ADDRESS="NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
TEE_ADDRESS="NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"
RPC_URL="http://seed1t5.neo.org:20332"

# Check if neo-mamba is installed
if ! command -v neo-mamba &> /dev/null; then
    echo "📦 Installing neo-mamba..."
    pip install neo-mamba
fi

# Check if WIF is provided
if [ -z "$MASTER_WIF" ]; then
    echo "⚠️  Please set the MASTER_WIF environment variable"
    echo "    Example: export MASTER_WIF=your_master_account_wif"
    echo "    Then run: $0"
    exit 1
fi

echo "🔧 Initializing contract with:"
echo "   Contract: $CONTRACT_HASH"
echo "   Master: $MASTER_ADDRESS"
echo "   TEE: $TEE_ADDRESS"
echo ""

# Step 1: Initialize contract
echo "📝 Step 1: Initialize contract..."
neo-mamba contract invoke $CONTRACT_HASH initialize "\"$MASTER_ADDRESS\"" "\"$TEE_ADDRESS\"" \
    --wallet-wif "$MASTER_WIF" \
    --rpc $RPC_URL \
    --force

echo "✅ Contract initialized"

# Step 2: Add TEE account as oracle
echo "📝 Step 2: Add TEE account as oracle..."
neo-mamba contract invoke $CONTRACT_HASH addOracle "\"$TEE_ADDRESS\"" \
    --wallet-wif "$MASTER_WIF" \
    --rpc $RPC_URL \
    --force

echo "✅ TEE account added as oracle"

# Step 3: Set minimum oracles to 1
echo "📝 Step 3: Set minimum oracles to 1..."
neo-mamba contract invoke $CONTRACT_HASH setMinOracles 1 \
    --wallet-wif "$MASTER_WIF" \
    --rpc $RPC_URL \
    --force

echo "✅ Minimum oracles set to 1"

# Verify initialization
echo "📝 Step 4: Verify initialization..."
echo "Checking contract owner..."
neo-mamba contract test-invoke $CONTRACT_HASH getOwner --rpc $RPC_URL

echo "Checking oracle count..."
neo-mamba contract test-invoke $CONTRACT_HASH getOracleCount --rpc $RPC_URL

echo ""
echo "🎉 Contract initialization completed successfully!"
echo "   The price feed workflow can now submit price updates."
echo "   Contract hash: $CONTRACT_HASH"
echo "   Explorer: https://testnet.explorer.onegate.space/contract/${CONTRACT_HASH:2}"