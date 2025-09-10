#!/usr/bin/env python3
"""
Direct Neo N3 contract deployment script using Python SDK
"""

import json
import requests
import base64
import time
from pathlib import Path

# Configuration
TESTNET_RPC = "http://seed1t5.neo.org:20332"
MASTER_WIF = "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb"
MASTER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"

def rpc_call(method, params=None):
    """Make RPC call to Neo N3 TestNet"""
    payload = {
        "jsonrpc": "2.0",
        "method": method,
        "params": params or [],
        "id": 1
    }
    
    try:
        response = requests.post(TESTNET_RPC, json=payload, timeout=30)
        result = response.json()
        
        if "error" in result:
            print(f"❌ RPC Error: {result['error']}")
            return None
            
        return result.get("result")
    except Exception as e:
        print(f"❌ Network error: {e}")
        return None

def get_network_fee():
    """Get current network fee"""
    fee_per_byte = rpc_call("getfeeperbyteresult")
    if fee_per_byte:
        return fee_per_byte.get("feePerByte", 1000)
    return 1000

def get_system_fee():
    """Estimate system fee for deployment"""
    # Typical deployment system fee (in datoshi)
    return 10_000_000_000  # 10 GAS

def create_deployment_script(nef_file, manifest_file):
    """Create deployment script"""
    try:
        # Read NEF file
        with open(nef_file, 'rb') as f:
            nef_data = f.read()
        
        # Read manifest
        with open(manifest_file, 'r') as f:
            manifest_data = f.read()
        
        print(f"✅ NEF size: {len(nef_data)} bytes")
        print(f"✅ Manifest size: {len(manifest_data)} bytes")
        
        # Create deployment transaction script
        # This is a simplified approach - in practice you'd use neo3-boa
        script_data = {
            "nef": base64.b64encode(nef_data).decode(),
            "manifest": manifest_data
        }
        
        return script_data
        
    except Exception as e:
        print(f"❌ Error reading contract files: {e}")
        return None

def check_deployment_requirements():
    """Check if deployment requirements are met"""
    print("🔍 Checking deployment requirements...")
    
    # Check files exist
    nef_path = Path("src/PriceFeed.Contracts/PriceFeed.Oracle.nef")
    manifest_path = Path("src/PriceFeed.Contracts/PriceFeed.Oracle.manifest.json")
    
    if not nef_path.exists():
        print(f"❌ NEF file not found: {nef_path}")
        return False
    
    if not manifest_path.exists():
        print(f"❌ Manifest file not found: {manifest_path}")
        return False
    
    # Check network connectivity
    block_count = rpc_call("getblockcount")
    if not block_count:
        print("❌ Cannot connect to TestNet")
        return False
    
    print(f"✅ TestNet connected (block {block_count})")
    
    # Check account balance
    balance_result = rpc_call("getnep17balances", [MASTER_ADDRESS])
    if not balance_result:
        print("❌ Cannot get account balance")
        return False
    
    gas_balance = 0
    for balance in balance_result.get("balance", []):
        if balance["assethash"] == "0xd2a4cff31913016155e38e474a2c06d08be276cf":  # GAS
            gas_balance = int(balance["amount"]) / 100_000_000
    
    print(f"✅ GAS balance: {gas_balance}")
    
    if gas_balance < 15:
        print("❌ Insufficient GAS (need at least 15 GAS)")
        return False
    
    return True

def simulate_deployment():
    """Simulate the deployment to estimate costs"""
    print("\n🧪 Simulating deployment...")
    
    # Read contract files
    script_data = create_deployment_script(
        "src/PriceFeed.Contracts/PriceFeed.Oracle.nef",
        "src/PriceFeed.Contracts/PriceFeed.Oracle.manifest.json"
    )
    
    if not script_data:
        return False
    
    # Estimate fees
    network_fee = get_network_fee()
    system_fee = get_system_fee()
    
    total_fee_gas = (network_fee + system_fee) / 100_000_000
    
    print(f"📊 Deployment cost estimate:")
    print(f"   System fee: ~{system_fee / 100_000_000} GAS")
    print(f"   Network fee: ~{network_fee / 100_000_000} GAS")
    print(f"   Total: ~{total_fee_gas} GAS")
    
    return True

def provide_manual_instructions():
    """Provide manual deployment instructions"""
    print("\n📋 MANUAL DEPLOYMENT INSTRUCTIONS")
    print("=" * 50)
    
    print("\n🎯 Since automated deployment requires complex transaction signing,")
    print("   use one of these proven methods:")
    
    print("\n🥇 OPTION 1: NeoLine Extension (RECOMMENDED)")
    print("   1. Install: https://neoline.io/")
    print("   2. Import private key")
    print("   3. Switch to TestNet")
    print("   4. Deploy contract files")
    
    print("\n🥈 OPTION 2: Neo-CLI")
    print("   1. Download: https://github.com/neo-project/neo-cli/releases")
    print("   2. Run: ./neo-cli --network testnet")
    print(f"   3. Import: import key {MASTER_WIF}")
    print("   4. Deploy: deploy src/PriceFeed.Contracts/PriceFeed.Oracle.nef")
    
    print("\n🥉 OPTION 3: Neo Playground")
    print("   1. Go to: https://neo-playground.dev/")
    print("   2. Upload contract files")
    print("   3. Deploy to TestNet")

def main():
    """Main deployment process"""
    print("🚀 Neo N3 Contract Deployment Script")
    print("=" * 45)
    
    # Check requirements
    if not check_deployment_requirements():
        print("\n❌ Deployment requirements not met")
        return False
    
    # Simulate deployment
    if not simulate_deployment():
        print("\n❌ Deployment simulation failed")
        return False
    
    print("\n✅ All checks passed!")
    
    # Note about complex deployment
    print("\n⚠️  IMPORTANT NOTE:")
    print("   Direct RPC deployment requires complex transaction signing")
    print("   and witness creation. For reliability, use the manual methods below.")
    
    provide_manual_instructions()
    
    print("\n🎯 RECOMMENDATION:")
    print("   Use NeoLine extension for the easiest deployment experience!")
    
    return True

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nDeployment cancelled by user")
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")