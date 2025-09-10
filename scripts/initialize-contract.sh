#!/bin/bash

# Initialize Neo Price Feed Oracle Contract
# This script initializes the deployed contract with proper configuration

set -e

# Contract details
CONTRACT_HASH="0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
OWNER_ADDRESS="NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
TEE_ADDRESS="NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"
RPC_ENDPOINT="http://seed1t5.neo.org:20332"

echo "=== Neo Price Feed Oracle Contract Initialization ==="
echo "Contract Hash: $CONTRACT_HASH"
echo "Owner Address: $OWNER_ADDRESS"
echo "TEE Address: $TEE_ADDRESS"
echo "RPC Endpoint: $RPC_ENDPOINT"
echo ""

# Check if neo-cli is available
if ! command -v neo-cli &> /dev/null; then
    echo "Error: neo-cli is not installed or not in PATH"
    echo "Please install neo-cli from: https://github.com/neo-project/neo-cli"
    exit 1
fi

echo "Please execute the following commands in neo-cli:"
echo ""
echo "1. Connect to TestNet:"
echo "   connect $RPC_ENDPOINT"
echo ""
echo "2. Initialize the contract (set owner and TEE account):"
echo "   invoke $CONTRACT_HASH initialize [\"$OWNER_ADDRESS\",\"$TEE_ADDRESS\"]"
echo ""
echo "3. Add TEE as oracle:"
echo "   invoke $CONTRACT_HASH addOracle [\"$TEE_ADDRESS\"]"
echo ""
echo "4. Set minimum oracles to 1:"
echo "   invoke $CONTRACT_HASH setMinOracles [1]"
echo ""
echo "5. (Optional) Verify the contract state:"
echo "   invokefunction $CONTRACT_HASH getOwner"
echo "   invokefunction $CONTRACT_HASH getTeeAccounts"
echo "   invokefunction $CONTRACT_HASH getMinOracles"
echo ""
echo "Note: You'll need to sign these transactions with the owner's private key."
echo "Make sure the owner account has enough GAS for the transactions."