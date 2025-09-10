#!/usr/bin/env python3
"""
Verify Neo Price Feed Oracle System
This script checks the complete system to ensure everything is working correctly.
"""

import json
import requests
import sys
from datetime import datetime

# Configuration
RPC_ENDPOINT = "http://seed1t5.neo.org:20332"
CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
TEE_ADDRESS = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"
OWNER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"

def invoke_contract_method(method, params=[]):
    """Invoke a contract method and return the result."""
    payload = {
        "jsonrpc": "2.0",
        "method": "invokefunction",
        "params": [CONTRACT_HASH, method, params],
        "id": 1
    }
    
    try:
        response = requests.post(RPC_ENDPOINT, json=payload, timeout=10)
        result = response.json()
        
        if "error" in result:
            return None, result["error"]["message"]
        
        if result["result"]["state"] == "HALT":
            return result["result"]["stack"], None
        else:
            return None, f"Execution failed: {result['result']['exception']}"
    except Exception as e:
        return None, str(e)

def check_contract_deployment():
    """Check if the contract is deployed."""
    print("1. Checking contract deployment...")
    
    payload = {
        "jsonrpc": "2.0",
        "method": "getcontractstate",
        "params": [CONTRACT_HASH],
        "id": 1
    }
    
    try:
        response = requests.post(RPC_ENDPOINT, json=payload, timeout=10)
        result = response.json()
        
        if "error" in result:
            print("   ❌ Contract not found on TestNet")
            return False
        
        print(f"   ✅ Contract deployed: {result['result']['hash']}")
        print(f"      Name: {result['result']['manifest']['name']}")
        return True
    except Exception as e:
        print(f"   ❌ Error checking contract: {e}")
        return False

def check_contract_initialization():
    """Check if the contract is initialized."""
    print("\n2. Checking contract initialization...")
    
    # Check owner
    stack, error = invoke_contract_method("getOwner")
    if error:
        print(f"   ❌ Error getting owner: {error}")
        return False
    
    if not stack or (isinstance(stack[0], dict) and stack[0].get("type") == "Any" and not stack[0].get("value")):
        print("   ❌ Contract not initialized (no owner set)")
        return False
    
    try:
        # Handle different stack response formats
        if isinstance(stack[0], dict) and "value" in stack[0]:
            owner_value = stack[0]["value"]
        else:
            owner_value = stack[0]
        
        if not owner_value:
            print("   ❌ Contract not initialized (no owner set)")
            return False
            
        owner = bytes.fromhex(owner_value).decode('utf-8')
    except:
        print("   ❌ Contract not initialized (no owner set)")
        return False
    print(f"   ✅ Owner: {owner}")
    
    # Check TEE accounts
    stack, error = invoke_contract_method("getTeeAccounts")
    if error:
        print(f"   ❌ Error getting TEE accounts: {error}")
        return False
    
    if not stack or not stack[0]["value"]:
        print("   ❌ No TEE accounts configured")
        return False
    
    tee_accounts = [bytes.fromhex(item["value"]).decode('utf-8') for item in stack[0]["value"]]
    print(f"   ✅ TEE accounts: {tee_accounts}")
    
    # Check oracles
    stack, error = invoke_contract_method("getOracles")
    if error:
        print(f"   ❌ Error getting oracles: {error}")
        return False
    
    if not stack or not stack[0]["value"]:
        print("   ❌ No oracles configured")
        return False
    
    oracles = [bytes.fromhex(item["value"]).decode('utf-8') for item in stack[0]["value"]]
    print(f"   ✅ Oracles: {oracles}")
    
    # Check min oracles
    stack, error = invoke_contract_method("getMinOracles")
    if error:
        print(f"   ❌ Error getting min oracles: {error}")
        return False
    
    min_oracles = int(stack[0]["value"]) if stack and stack[0]["value"] else 0
    print(f"   ✅ Minimum oracles: {min_oracles}")
    
    return TEE_ADDRESS in tee_accounts and TEE_ADDRESS in oracles and min_oracles > 0

def check_price_data():
    """Check if price data is available in the contract."""
    print("\n3. Checking price data...")
    
    test_symbols = ["BTCUSDT", "ETHUSDT", "NEOUSDT"]
    has_data = False
    
    for symbol in test_symbols:
        stack, error = invoke_contract_method("getPriceData", [{"type": "String", "value": symbol}])
        
        if error or not stack or not stack[0]["value"]:
            print(f"   ⚠️  No price data for {symbol}")
            continue
        
        try:
            # Parse the struct returned by getPriceData
            data = stack[0]["value"]
            if len(data) >= 4:
                stored_symbol = bytes.fromhex(data[0]["value"]).decode('utf-8')
                price = int(data[1]["value"]) / 100_000_000
                timestamp = int(data[2]["value"])
                confidence = int(data[3]["value"])
                
                # Convert timestamp to readable format
                update_time = datetime.fromtimestamp(timestamp / 1000)
                age_seconds = (datetime.now() - update_time).total_seconds()
                
                print(f"   ✅ {stored_symbol}: ${price:.2f}")
                print(f"      Updated: {update_time.strftime('%Y-%m-%d %H:%M:%S')} ({int(age_seconds)}s ago)")
                print(f"      Confidence: {confidence}%")
                has_data = True
        except Exception as e:
            print(f"   ⚠️  Error parsing data for {symbol}: {e}")
    
    return has_data

def check_github_actions():
    """Check if GitHub Actions is configured."""
    print("\n4. Checking GitHub Actions configuration...")
    
    try:
        with open(".github/workflows/price-feed-testnet.yml", "r") as f:
            workflow = f.read()
            
        if "schedule:" in workflow and "cron:" in workflow:
            print("   ✅ GitHub Actions workflow configured with schedule")
            
            # Extract cron schedule
            for line in workflow.split('\n'):
                if 'cron:' in line:
                    print(f"      Schedule: {line.strip()}")
            
            return True
        else:
            print("   ❌ GitHub Actions workflow missing schedule")
            return False
    except FileNotFoundError:
        print("   ❌ GitHub Actions workflow not found")
        return False

def check_price_sources():
    """Check if price data sources are accessible."""
    print("\n5. Checking price data sources...")
    
    sources = {
        "CoinGecko": "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd",
        "Kraken": "https://api.kraken.com/0/public/Ticker?pair=XBTUSD",
        "Coinbase": "https://api.coinbase.com/v2/exchange-rates?currency=BTC"
    }
    
    working_sources = 0
    
    for name, url in sources.items():
        try:
            response = requests.get(url, timeout=5)
            if response.status_code == 200:
                print(f"   ✅ {name}: Accessible")
                working_sources += 1
            else:
                print(f"   ❌ {name}: HTTP {response.status_code}")
        except Exception as e:
            print(f"   ❌ {name}: {str(e)[:50]}...")
    
    return working_sources >= 2  # At least 2 sources should work

def main():
    """Main verification function."""
    print("=== Neo Price Feed Oracle System Verification ===")
    print(f"Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Contract: {CONTRACT_HASH}")
    print(f"Network: Neo N3 TestNet")
    print("")
    
    # Run checks
    checks = {
        "Contract Deployed": check_contract_deployment(),
        "Contract Initialized": check_contract_initialization(),
        "Price Data Available": check_price_data(),
        "GitHub Actions": check_github_actions(),
        "Price Sources": check_price_sources()
    }
    
    # Summary
    print("\n=== Summary ===")
    all_passed = True
    
    for check, passed in checks.items():
        status = "✅ PASS" if passed else "❌ FAIL"
        print(f"{check}: {status}")
        if not passed:
            all_passed = False
    
    if all_passed:
        print("\n✅ All checks passed! The system is fully operational.")
        print("Price feeds will be automatically updated every 5 minutes via GitHub Actions.")
    else:
        print("\n❌ Some checks failed. Please address the issues above.")
        
        if not checks["Contract Initialized"]:
            print("\n📝 To initialize the contract, run:")
            print("   ./scripts/initialize-contract.sh")
    
    return 0 if all_passed else 1

if __name__ == "__main__":
    sys.exit(main())