#!/usr/bin/env python3
"""
Auto-initialization script for GitHub Actions
Checks if contract is initialized and initializes if needed
"""

import requests
import json
import subprocess
import sys
import os

# Configuration
RPC_ENDPOINT = "http://seed1t5.neo.org:20332"
CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
MASTER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
TEE_ADDRESS = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"

def rpc_call(method, params=None):
    """Make RPC call"""
    payload = {
        "jsonrpc": "2.0",
        "method": method,
        "params": params or [],
        "id": 1
    }
    
    try:
        response = requests.post(RPC_ENDPOINT, json=payload, timeout=10)
        return response.json()
    except Exception as e:
        print(f"❌ RPC error: {e}")
        return None

def check_initialization():
    """Check if contract is initialized"""
    result = rpc_call("invokefunction", [CONTRACT_HASH, "getOwner", []])
    if not result or 'result' not in result:
        return False
    
    # Check if getOwner returns a valid address (not null)
    stack = result['result'].get('stack', [])
    if not stack or stack[0].get('type') == 'Any':
        return False
    
    return True

def run_neo_mamba_command(command):
    """Run neo-mamba command"""
    try:
        result = subprocess.run(command, shell=True, capture_output=True, text=True, timeout=60)
        if result.returncode == 0:
            print(f"✅ Success: {command}")
            return True
        else:
            print(f"❌ Failed: {command}")
            print(f"   Error: {result.stderr}")
            return False
    except subprocess.TimeoutExpired:
        print(f"⏱️ Timeout: {command}")
        return False
    except Exception as e:
        print(f"❌ Exception: {e}")
        return False

def install_neo_mamba():
    """Install neo-mamba if not available"""
    try:
        subprocess.run(["neo-mamba", "--version"], capture_output=True, timeout=5)
        return True
    except:
        print("📦 Installing neo-mamba...")
        result = subprocess.run(["pip", "install", "neo-mamba"], capture_output=True, timeout=60)
        return result.returncode == 0

def initialize_contract():
    """Initialize the contract"""
    master_wif = os.environ.get('MASTER_ACCOUNT_PRIVATE_KEY')
    if not master_wif:
        print("❌ MASTER_ACCOUNT_PRIVATE_KEY not set")
        return False
    
    # Install neo-mamba if needed
    if not install_neo_mamba():
        print("❌ Failed to install neo-mamba")
        return False
    
    print("🔧 Initializing contract...")
    
    # Step 1: Initialize
    cmd1 = f'neo-mamba contract invoke {CONTRACT_HASH} initialize "\\"{MASTER_ADDRESS}\\"" "\\"{TEE_ADDRESS}\\"" --wallet-wif {master_wif} --rpc {RPC_ENDPOINT} --force'
    if not run_neo_mamba_command(cmd1):
        return False
    
    # Step 2: Add oracle
    cmd2 = f'neo-mamba contract invoke {CONTRACT_HASH} addOracle "\\"{TEE_ADDRESS}\\"" --wallet-wif {master_wif} --rpc {RPC_ENDPOINT} --force'
    if not run_neo_mamba_command(cmd2):
        return False
    
    # Step 3: Set min oracles
    cmd3 = f'neo-mamba contract invoke {CONTRACT_HASH} setMinOracles 1 --wallet-wif {master_wif} --rpc {RPC_ENDPOINT} --force'
    if not run_neo_mamba_command(cmd3):
        return False
    
    return True

def main():
    """Main function"""
    print("🔍 Checking contract initialization...")
    
    if check_initialization():
        print("✅ Contract is already initialized")
        return True
    
    print("⚠️ Contract needs initialization")
    
    if initialize_contract():
        print("🎉 Contract initialization completed")
        return True
    else:
        print("❌ Contract initialization failed")
        return False

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)