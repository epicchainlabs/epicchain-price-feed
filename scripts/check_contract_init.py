#!/usr/bin/env python3
"""
Check contract initialization status via RPC
"""

import json
import requests
import base58

# Configuration
RPC_ENDPOINT = "http://seed1t5.neo.org:20332"
CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
MASTER_ACCOUNT = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
TEE_ACCOUNT = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"

def address_to_script_hash(address):
    """Convert Neo address to script hash"""
    decoded = base58.b58decode(address)
    script_hash = decoded[1:21]
    return script_hash.hex()

def invoke_function(method, params=[]):
    """Invoke a contract function via RPC"""
    payload = {
        "jsonrpc": "2.0",
        "method": "invokefunction",
        "params": [
            CONTRACT_HASH,
            method,
            params
        ],
        "id": 1
    }
    
    response = requests.post(RPC_ENDPOINT, json=payload)
    return response.json()

def check_initialization():
    """Check if contract is initialized"""
    print("🔍 Checking contract initialization status...")
    
    # Check isInitialized
    result = invoke_function("isInitialized")
    
    if result.get("result", {}).get("state") == "HALT":
        stack = result.get("result", {}).get("stack", [])
        if stack and len(stack) > 0:
            value = stack[0].get("value")
            if value == "1" or value == True:
                print("✅ Contract is initialized")
                return True
            else:
                print("❌ Contract is NOT initialized")
                return False
    
    print("❌ Failed to check initialization status")
    print(f"Response: {json.dumps(result, indent=2)}")
    return False

def check_oracle_setup():
    """Check oracle configuration"""
    print("\n🔍 Checking oracle configuration...")
    
    # Check oracle count
    result = invoke_function("getOracleCount")
    if result.get("result", {}).get("state") == "HALT":
        stack = result.get("result", {}).get("stack", [])
        if stack:
            count = int(stack[0].get("value", "0"))
            print(f"📊 Oracle count: {count}")
    
    # Check if TEE is oracle
    tee_script_hash = address_to_script_hash(TEE_ACCOUNT)
    params = [{"type": "Hash160", "value": tee_script_hash}]
    
    result = invoke_function("isOracle", params)
    if result.get("result", {}).get("state") == "HALT":
        stack = result.get("result", {}).get("stack", [])
        if stack:
            is_oracle = stack[0].get("value") == "1" or stack[0].get("value") == True
            print(f"🔑 TEE account ({TEE_ACCOUNT}) is oracle: {is_oracle}")
    
    # Check min oracles
    result = invoke_function("getMinOracles")
    if result.get("result", {}).get("state") == "HALT":
        stack = result.get("result", {}).get("stack", [])
        if stack:
            min_oracles = int(stack[0].get("value", "0"))
            print(f"⚙️  Minimum oracles: {min_oracles}")

def test_price_query():
    """Test price query functionality"""
    print("\n🔍 Testing price query...")
    
    params = [{"type": "String", "value": "BTCUSDT"}]
    result = invoke_function("getPrice", params)
    
    if result.get("result", {}).get("state") == "HALT":
        stack = result.get("result", {}).get("stack", [])
        if stack and len(stack) > 0:
            if stack[0].get("type") == "Integer":
                price = int(stack[0].get("value", "0"))
                if price > 0:
                    print(f"💰 Current BTCUSDT price: {price}")
                else:
                    print("📝 No price set for BTCUSDT yet")
            else:
                print("📝 No price data available")
    else:
        exception = result.get("result", {}).get("exception", "")
        print(f"⚠️  Price query returned: {exception}")

def show_initialization_guide():
    """Show initialization guide"""
    print("\n📋 INITIALIZATION COMMANDS FOR NEO-CLI")
    print("=" * 60)
    print("\nOpen neo-cli and run these commands in sequence:\n")
    
    print("1️⃣  Initialize the contract:")
    print(f'invoke {CONTRACT_HASH} initialize ["{MASTER_ACCOUNT}","{TEE_ACCOUNT}"]')
    print("   Then type 'yes' to relay the transaction\n")
    
    print("2️⃣  Add TEE account as oracle:")
    print(f'invoke {CONTRACT_HASH} addOracle ["{TEE_ACCOUNT}"]')
    print("   Then type 'yes' to relay the transaction\n")
    
    print("3️⃣  Set minimum oracles to 1:")
    print(f'invoke {CONTRACT_HASH} setMinOracles [1]')
    print("   Then type 'yes' to relay the transaction\n")
    
    print("🔍 Verification commands:")
    print(f'invoke {CONTRACT_HASH} isInitialized []')
    print(f'invoke {CONTRACT_HASH} getOracleCount []')
    print(f'invoke {CONTRACT_HASH} isOracle ["{TEE_ACCOUNT}"]')

def main():
    """Main function"""
    print("🚀 NEO PRICE FEED CONTRACT STATUS CHECK")
    print("=" * 50)
    print(f"Contract: {CONTRACT_HASH}")
    print(f"Network: Neo N3 TestNet")
    print(f"RPC: {RPC_ENDPOINT}")
    
    try:
        # Check initialization
        is_initialized = check_initialization()
        
        if is_initialized:
            # Check oracle setup
            check_oracle_setup()
            
            # Test price query
            test_price_query()
            
            print("\n✅ Contract is ready for use!")
            print("\n🎯 Next step: Run the price feed service")
            print("   dotnet run --project src/PriceFeed.Console")
        else:
            # Show initialization guide
            show_initialization_guide()
            
            print("\n⚠️  Contract needs initialization!")
            print("Please run the commands above in neo-cli")
        
    except Exception as e:
        print(f"\n❌ Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()