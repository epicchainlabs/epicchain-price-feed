#!/usr/bin/env python3
"""
Quick initialization script for the deployed contract
"""

import requests
import json
import sys

# Configuration
RPC_ENDPOINT = "http://seed1t5.neo.org:20332"
CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
MASTER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
TEE_ADDRESS = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"

def check_contract_state():
    """Check current contract state"""
    print("🔍 Checking contract state...")
    
    # Check if contract exists
    payload = {
        "jsonrpc": "2.0",
        "method": "getcontractstate",
        "params": [CONTRACT_HASH],
        "id": 1
    }
    
    response = requests.post(RPC_ENDPOINT, json=payload)
    result = response.json()
    
    if 'result' in result:
        print(f"✅ Contract found at: {CONTRACT_HASH}")
        return True
    else:
        print(f"❌ Contract not found at: {CONTRACT_HASH}")
        return False

def check_initialization():
    """Check if contract is initialized"""
    print("\n🔍 Checking initialization status...")
    
    # Invoke getOwner
    payload = {
        "jsonrpc": "2.0",
        "method": "invokefunction",
        "params": [CONTRACT_HASH, "getOwner", []],
        "id": 1
    }
    
    response = requests.post(RPC_ENDPOINT, json=payload)
    result = response.json()
    
    if result.get('result', {}).get('stack', []):
        stack_value = result['result']['stack'][0]
        if stack_value.get('type') != 'Any' or stack_value.get('value'):
            print("✅ Contract is already initialized")
            return True
    
    print("⚠️ Contract is NOT initialized")
    return False

def main():
    print("🚀 Neo Price Feed Contract Initialization")
    print("=" * 50)
    
    # Check contract exists
    if not check_contract_state():
        sys.exit(1)
    
    # Check initialization
    if check_initialization():
        print("\n✅ Contract is ready to use!")
        return
    
    print("\n📋 INITIALIZATION REQUIRED")
    print("=" * 30)
    print("\nThe contract needs to be initialized with these commands:")
    
    print("\n🥇 Option 1: Using neo-mamba (RECOMMENDED)")
    print("```bash")
    print("# Install neo-mamba")
    print("pip install neo-mamba")
    print()
    print("# Initialize contract")
    print(f'neo-mamba contract invoke {CONTRACT_HASH} initialize "{MASTER_ADDRESS}" "{TEE_ADDRESS}" \\')
    print(f'  --wallet-wif YOUR_MASTER_WIF --rpc {RPC_ENDPOINT}')
    print()
    print("# Add TEE as oracle")
    print(f'neo-mamba contract invoke {CONTRACT_HASH} addOracle "{TEE_ADDRESS}" \\')
    print(f'  --wallet-wif YOUR_MASTER_WIF --rpc {RPC_ENDPOINT}')
    print()
    print("# Set minimum oracles to 1")
    print(f'neo-mamba contract invoke {CONTRACT_HASH} setMinOracles 1 \\')
    print(f'  --wallet-wif YOUR_MASTER_WIF --rpc {RPC_ENDPOINT}')
    print("```")
    
    print("\n🥈 Option 2: Using NeoLine Chrome Extension")
    print("1. Install NeoLine: https://neoline.io/")
    print("2. Import your master account")
    print("3. Switch to TestNet")
    print("4. Visit: https://dapp.neoline.io/")
    print("5. Execute the initialization methods")
    
    print("\n⚠️ IMPORTANT:")
    print(f"- Master Account: {MASTER_ADDRESS}")
    print(f"- TEE Account: {TEE_ADDRESS}")
    print("- You need the Master Account private key (WIF) to initialize")
    print("- Ensure the Master Account has sufficient GAS (>5 GAS)")

if __name__ == "__main__":
    main()