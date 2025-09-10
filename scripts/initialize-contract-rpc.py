#!/usr/bin/env python3
"""
Initialize Neo Price Feed Oracle Contract via RPC
This script initializes the deployed contract with proper configuration using RPC calls.
"""

import json
import os
import requests
import sys
import time
import binascii

# Configuration from environment variables
RPC_ENDPOINT = os.getenv("NEO_RPC_ENDPOINT", "http://seed1t5.neo.org:20332")
CONTRACT_HASH = os.getenv("CONTRACT_SCRIPT_HASH", "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc")
OWNER_ADDRESS = os.getenv("MASTER_ACCOUNT_ADDRESS", "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX")
OWNER_WIF = os.getenv("MASTER_ACCOUNT_PRIVATE_KEY", "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb")
TEE_ADDRESS = os.getenv("TEE_ACCOUNT_ADDRESS", "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB")

def address_to_script_hash(address):
    """Convert Neo address to script hash (simplified version)."""
    # For known addresses, return the known script hashes
    known_addresses = {
        "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX": "0x88ac1bdac4c5b67b1b63c95820f088b350df3d91",
        "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB": "0xe19de267a37a71734478f6ddaac9c7b77c0c5f8f"
    }
    
    if address in known_addresses:
        return known_addresses[address]
    else:
        # For this script, we only need the known addresses
        raise ValueError(f"Unknown address: {address}")

def invoke_contract_function(contract_hash, method, params, signers):
    """Invoke a contract function."""
    payload = {
        "jsonrpc": "2.0",
        "method": "invokefunction",
        "params": [contract_hash, method, params, signers],
        "id": 1
    }
    
    response = requests.post(RPC_ENDPOINT, json=payload, timeout=30)
    result = response.json()
    
    if "error" in result:
        raise Exception(f"RPC Error: {result['error']['message']}")
    
    return result["result"]

def send_transaction(script, signers, attributes=None):
    """Create and send a transaction."""
    # First, get the transaction
    payload = {
        "jsonrpc": "2.0",
        "method": "invokescript",
        "params": [script, signers],
        "id": 1
    }
    
    response = requests.post(RPC_ENDPOINT, json=payload, timeout=30)
    result = response.json()
    
    if "error" in result:
        raise Exception(f"RPC Error: {result['error']['message']}")
    
    if result["result"]["state"] != "HALT":
        raise Exception(f"Script execution failed: {result['result']['exception']}")
    
    # Note: In a real implementation, you would need to:
    # 1. Create the transaction from the script
    # 2. Sign it with the private key
    # 3. Send it via sendrawtransaction
    # This requires complex cryptographic operations
    
    print("   ⚠️  Transaction script created successfully")
    print("   ℹ️  To complete the transaction, use neo-cli or a wallet to sign and send")
    return result["result"]["script"]

def check_contract_state():
    """Check if the contract is initialized."""
    print("🔍 Checking contract state...")
    
    # Check owner
    result = invoke_contract_function(CONTRACT_HASH, "getOwner", [], [])
    if result["state"] == "HALT" and result["stack"]:
        stack_value = result["stack"][0]
        if stack_value["type"] != "Any":
            owner_hex = stack_value["value"]
            owner_address = binascii.unhexlify(owner_hex).decode('utf-8')
            print(f"   ✅ Owner: {owner_address}")
            return True
        else:
            print("   ❌ Owner: Not set (contract not initialized)")
            return False
    else:
        print("   ❌ Failed to get owner")
        return False

def generate_initialization_script():
    """Generate the initialization script."""
    print("\n📝 Generating initialization commands...")
    
    owner_signer = {
        "account": address_to_script_hash(OWNER_ADDRESS),
        "scopes": "CalledByEntry"
    }
    
    # Initialize contract
    print("\n1️⃣ Initialize contract command:")
    init_params = [
        {"type": "String", "value": OWNER_ADDRESS},
        {"type": "String", "value": TEE_ADDRESS}
    ]
    init_result = invoke_contract_function(CONTRACT_HASH, "initialize", init_params, [owner_signer])
    
    if init_result["state"] == "HALT":
        print(f"   Script: {init_result['script']}")
        print(f"   GAS consumed: {init_result['gasconsumed']}")
    
    # Add oracle
    print("\n2️⃣ Add oracle command:")
    oracle_params = [{"type": "String", "value": TEE_ADDRESS}]
    oracle_result = invoke_contract_function(CONTRACT_HASH, "addOracle", oracle_params, [owner_signer])
    
    if oracle_result["state"] == "HALT":
        print(f"   Script: {oracle_result['script']}")
        print(f"   GAS consumed: {oracle_result['gasconsumed']}")
    
    # Set min oracles
    print("\n3️⃣ Set minimum oracles command:")
    min_params = [{"type": "Integer", "value": "1"}]
    min_result = invoke_contract_function(CONTRACT_HASH, "setMinOracles", min_params, [owner_signer])
    
    if min_result["state"] == "HALT":
        print(f"   Script: {min_result['script']}")
        print(f"   GAS consumed: {min_result['gasconsumed']}")

def generate_neo_cli_commands():
    """Generate neo-cli commands for manual execution."""
    print("\n📋 Neo-CLI Commands:")
    print("====================")
    print("Execute these commands in neo-cli to initialize the contract:\n")
    
    print(f"# Connect to TestNet")
    print(f"connect {RPC_ENDPOINT}")
    print()
    
    print(f"# Import the owner's private key (if not already imported)")
    print(f"import key {OWNER_WIF}")
    print()
    
    print(f"# Initialize the contract")
    print(f'invoke {CONTRACT_HASH} initialize ["{OWNER_ADDRESS}","{TEE_ADDRESS}"] {OWNER_ADDRESS}')
    print()
    
    print(f"# Add TEE as oracle")
    print(f'invoke {CONTRACT_HASH} addOracle ["{TEE_ADDRESS}"] {OWNER_ADDRESS}')
    print()
    
    print(f"# Set minimum oracles to 1")
    print(f'invoke {CONTRACT_HASH} setMinOracles [1] {OWNER_ADDRESS}')
    print()
    
    print(f"# Verify the setup")
    print(f"invokefunction {CONTRACT_HASH} getOwner")
    print(f"invokefunction {CONTRACT_HASH} getOracles")
    print(f"invokefunction {CONTRACT_HASH} getMinOracles")

def main():
    """Main function."""
    print("🚀 Neo Price Feed Oracle Contract Initialization")
    print("================================================")
    print(f"Contract: {CONTRACT_HASH}")
    print(f"Owner: {OWNER_ADDRESS}")
    print(f"TEE: {TEE_ADDRESS}")
    print()
    
    # Check current state
    is_initialized = check_contract_state()
    
    if is_initialized:
        print("\n✅ Contract appears to be already initialized!")
        response = input("Do you want to see the initialization commands anyway? (y/n): ")
        if response.lower() != 'y':
            return
    
    # Generate initialization script
    try:
        generate_initialization_script()
    except Exception as e:
        print(f"\n❌ Error generating scripts: {e}")
    
    # Generate neo-cli commands
    generate_neo_cli_commands()
    
    print("\n📌 Next Steps:")
    print("1. Use neo-cli with the commands above to initialize the contract")
    print("2. Or use a Neo wallet that supports contract invocation")
    print("3. After initialization, run the verification script to confirm")

if __name__ == "__main__":
    main()