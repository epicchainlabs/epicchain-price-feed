#!/bin/bash

# Build and deploy the Neo smart contract

echo "Building Neo Price Oracle Contract..."

# Navigate to the contracts directory
cd PriceFeed.Contracts

# Install Neo.Compiler.CSharp if not already installed
if ! dotnet tool list -g | grep -q "Neo.Compiler.CSharp"; then
    echo "Installing Neo.Compiler.CSharp..."
    dotnet tool install -g Neo.Compiler.CSharp
fi

# Compile the smart contract
echo "Compiling PriceOracleContract..."
nccs PriceOracleContract.cs

# Check if compilation was successful
if [ -f "PriceOracleContract.nef" ] && [ -f "PriceOracleContract.manifest.json" ]; then
    echo "Contract compiled successfully!"
    echo "Generated files:"
    echo "  - PriceOracleContract.nef"
    echo "  - PriceOracleContract.manifest.json"
    
    # Display contract hash
    echo ""
    echo "Contract deployment info:"
    echo "========================"
    echo "NEF file: PriceOracleContract.nef"
    echo "Manifest: PriceOracleContract.manifest.json"
    echo ""
    echo "To deploy to testnet/mainnet:"
    echo "1. Use neo-cli or neo-express to deploy the contract"
    echo "2. Update the ContractScriptHash in your configuration with the deployed contract hash"
    echo "3. Initialize the contract by calling the Initialize method with owner and TEE account addresses"
else
    echo "Contract compilation failed!"
    exit 1
fi

cd ..