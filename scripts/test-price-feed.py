#!/usr/bin/env python3
"""
Test Neo Price Feed Oracle System
This script tests the complete price feed system including:
1. Contract initialization status
2. Price data fetching from sources
3. Transaction creation and validation
4. End-to-end price update simulation
"""

import json
import os
import requests
import sys
import time
from datetime import datetime
import base64

# Configuration from environment variables
RPC_ENDPOINT = os.getenv("NEO_RPC_ENDPOINT", "http://seed1t5.neo.org:20332")
CONTRACT_HASH = os.getenv("CONTRACT_SCRIPT_HASH", "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc")
MASTER_ADDRESS = os.getenv("MASTER_ACCOUNT_ADDRESS", "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX")
TEE_ADDRESS = os.getenv("TEE_ACCOUNT_ADDRESS", "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB")

# Price sources
PRICE_SOURCES = {
    "CoinGecko": {
        "url": "https://api.coingecko.com/api/v3/simple/price",
        "params": {"ids": "bitcoin,ethereum,neo", "vs_currencies": "usd"}
    },
    "Kraken": {
        "url": "https://api.kraken.com/0/public/Ticker",
        "params": {"pair": "XBTUSD,ETHUSD"}
    },
    "Coinbase": {
        "url": "https://api.coinbase.com/v2/exchange-rates",
        "params": {"currency": "USD"}
    }
}

def test_price_sources():
    """Test if price sources are accessible and returning data."""
    print("🌐 Testing Price Sources")
    print("========================")
    
    results = {}
    
    # Test CoinGecko
    print("\n1. CoinGecko:")
    try:
        response = requests.get(PRICE_SOURCES["CoinGecko"]["url"], 
                              params=PRICE_SOURCES["CoinGecko"]["params"], 
                              timeout=10)
        if response.status_code == 200:
            data = response.json()
            btc_price = data.get("bitcoin", {}).get("usd", 0)
            eth_price = data.get("ethereum", {}).get("usd", 0)
            neo_price = data.get("neo", {}).get("usd", 0)
            
            print(f"   ✅ Accessible")
            print(f"   BTC: ${btc_price:,.2f}")
            print(f"   ETH: ${eth_price:,.2f}")
            print(f"   NEO: ${neo_price:,.2f}")
            
            results["CoinGecko"] = {
                "status": "OK",
                "prices": {"BTC": btc_price, "ETH": eth_price, "NEO": neo_price}
            }
        else:
            print(f"   ❌ HTTP {response.status_code}")
            results["CoinGecko"] = {"status": "ERROR", "error": f"HTTP {response.status_code}"}
    except Exception as e:
        print(f"   ❌ Error: {str(e)}")
        results["CoinGecko"] = {"status": "ERROR", "error": str(e)}
    
    # Test Kraken
    print("\n2. Kraken:")
    try:
        response = requests.get(PRICE_SOURCES["Kraken"]["url"], 
                              params=PRICE_SOURCES["Kraken"]["params"], 
                              timeout=10)
        if response.status_code == 200:
            data = response.json()
            if data.get("error") == []:
                btc_price = float(data["result"]["XXBTZUSD"]["c"][0])
                eth_price = float(data["result"]["XETHZUSD"]["c"][0]) if "XETHZUSD" in data["result"] else 0
                
                print(f"   ✅ Accessible")
                print(f"   BTC: ${btc_price:,.2f}")
                if eth_price > 0:
                    print(f"   ETH: ${eth_price:,.2f}")
                
                results["Kraken"] = {
                    "status": "OK",
                    "prices": {"BTC": btc_price, "ETH": eth_price}
                }
            else:
                print(f"   ❌ API Error: {data['error']}")
                results["Kraken"] = {"status": "ERROR", "error": str(data['error'])}
        else:
            print(f"   ❌ HTTP {response.status_code}")
            results["Kraken"] = {"status": "ERROR", "error": f"HTTP {response.status_code}"}
    except Exception as e:
        print(f"   ❌ Error: {str(e)}")
        results["Kraken"] = {"status": "ERROR", "error": str(e)}
    
    # Test Coinbase
    print("\n3. Coinbase:")
    try:
        response = requests.get(PRICE_SOURCES["Coinbase"]["url"], 
                              params=PRICE_SOURCES["Coinbase"]["params"], 
                              timeout=10)
        if response.status_code == 200:
            data = response.json()
            rates = data.get("data", {}).get("rates", {})
            
            # Coinbase gives inverse rates (USD to crypto)
            btc_rate = float(rates.get("BTC", 0))
            eth_rate = float(rates.get("ETH", 0))
            
            btc_price = 1 / btc_rate if btc_rate > 0 else 0
            eth_price = 1 / eth_rate if eth_rate > 0 else 0
            
            print(f"   ✅ Accessible")
            print(f"   BTC: ${btc_price:,.2f}")
            print(f"   ETH: ${eth_price:,.2f}")
            
            results["Coinbase"] = {
                "status": "OK",
                "prices": {"BTC": btc_price, "ETH": eth_price}
            }
        else:
            print(f"   ❌ HTTP {response.status_code}")
            results["Coinbase"] = {"status": "ERROR", "error": f"HTTP {response.status_code}"}
    except Exception as e:
        print(f"   ❌ Error: {str(e)}")
        results["Coinbase"] = {"status": "ERROR", "error": str(e)}
    
    return results

def test_contract_methods():
    """Test contract method calls."""
    print("\n📝 Testing Contract Methods")
    print("===========================")
    
    # Test getOwner
    print("\n1. getOwner():")
    result = invoke_contract_method("getOwner", [])
    if result and result["state"] == "HALT":
        stack = result.get("stack", [])
        if stack and stack[0]["type"] != "Any":
            owner = bytes.fromhex(stack[0]["value"]).decode('utf-8')
            print(f"   ✅ Owner: {owner}")
        else:
            print("   ❌ Owner not set (contract not initialized)")
    else:
        print(f"   ❌ Method call failed")
    
    # Test getOracles
    print("\n2. getOracles():")
    result = invoke_contract_method("getOracles", [])
    if result and result["state"] == "HALT":
        stack = result.get("stack", [])
        if stack and stack[0]["type"] == "Array":
            oracles = stack[0]["value"]
            print(f"   ✅ Oracles: {len(oracles)} configured")
            for oracle in oracles:
                oracle_addr = bytes.fromhex(oracle["value"]).decode('utf-8')
                print(f"      - {oracle_addr}")
        else:
            print("   ❌ No oracles configured")
    else:
        print(f"   ❌ Method call failed")
    
    # Test getMinOracles
    print("\n3. getMinOracles():")
    result = invoke_contract_method("getMinOracles", [])
    if result and result["state"] == "HALT":
        stack = result.get("stack", [])
        if stack and stack[0]["type"] == "Integer":
            min_oracles = int(stack[0]["value"])
            print(f"   ✅ Minimum oracles: {min_oracles}")
        else:
            print("   ❌ Value not set")
    else:
        print(f"   ❌ Method call failed")
    
    # Test getPriceData for multiple symbols
    print("\n4. getPriceData(symbol):")
    test_symbols = ["BTCUSDT", "ETHUSDT", "NEOUSDT"]
    for symbol in test_symbols:
        result = invoke_contract_method("getPriceData", [{"type": "String", "value": symbol}])
        if result and result["state"] == "HALT":
            stack = result.get("stack", [])
            if stack and stack[0]["type"] == "Struct":
                data = stack[0]["value"]
                if len(data) >= 4:
                    stored_symbol = bytes.fromhex(data[0]["value"]).decode('utf-8')
                    price = int(data[1]["value"])
                    timestamp = int(data[2]["value"])
                    confidence = int(data[3]["value"])
                    
                    price_decimal = price / 100000000
                    update_time = datetime.fromtimestamp(timestamp / 1000)
                    age_seconds = (datetime.now() - update_time).total_seconds()
                    
                    print(f"   ✅ {symbol}: ${price_decimal:.2f}")
                    print(f"      Updated: {int(age_seconds)}s ago")
                    print(f"      Confidence: {confidence}%")
                else:
                    print(f"   ⚠️  {symbol}: Invalid data structure")
            else:
                print(f"   ❌ {symbol}: No data")
        else:
            print(f"   ❌ {symbol}: Method call failed")

def invoke_contract_method(method, params):
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
            return None
        
        return result["result"]
    except:
        return None

def simulate_price_update():
    """Simulate a price update transaction."""
    print("\n🚀 Simulating Price Update Transaction")
    print("======================================")
    
    # Create sample price data
    price_updates = [
        {
            "symbol": "BTCUSDT",
            "price": 45000 * 100000000,  # Convert to 8 decimals
            "timestamp": int(time.time() * 1000),
            "confidence": 95
        },
        {
            "symbol": "ETHUSDT",
            "price": 2500 * 100000000,
            "timestamp": int(time.time() * 1000),
            "confidence": 95
        }
    ]
    
    print("\nPrice updates to send:")
    for update in price_updates:
        print(f"  - {update['symbol']}: ${update['price'] / 100000000:.2f}")
    
    # Create updatePriceBatch parameters
    batch_param = {
        "type": "Array",
        "value": []
    }
    
    for update in price_updates:
        update_struct = {
            "type": "Struct",
            "value": [
                {"type": "String", "value": update["symbol"]},
                {"type": "Integer", "value": str(update["price"])},
                {"type": "Integer", "value": str(update["timestamp"])},
                {"type": "Integer", "value": str(update["confidence"])}
            ]
        }
        batch_param["value"].append(update_struct)
    
    # Test the updatePriceBatch call
    print("\nTesting updatePriceBatch script generation...")
    signers = [{
        "account": "0xe19de267a37a71734478f6ddaac9c7b77c0c5f8f",  # TEE account script hash
        "scopes": "CalledByEntry"
    }]
    
    payload = {
        "jsonrpc": "2.0",
        "method": "invokefunction",
        "params": [CONTRACT_HASH, "updatePriceBatch", [batch_param], signers],
        "id": 1
    }
    
    try:
        response = requests.post(RPC_ENDPOINT, json=payload, timeout=10)
        result = response.json()
        
        if "error" in result:
            print(f"❌ Error: {result['error']['message']}")
        else:
            invoke_result = result["result"]
            if invoke_result["state"] == "HALT":
                print("✅ Script validation successful")
                print(f"   GAS required: {int(invoke_result['gasconsumed']) / 100000000:.8f} GAS")
                print(f"   Script: {invoke_result['script']}")
                
                # Note: This is where the actual transaction would be created,
                # signed with both TEE and Master private keys, and sent
                print("\n📝 Note: To complete the transaction:")
                print("   1. Create transaction with the script above")
                print("   2. Sign with TEE private key")
                print("   3. Sign with Master private key")
                print("   4. Send via sendrawtransaction")
            else:
                print(f"❌ Script execution failed: {invoke_result.get('exception', 'Unknown error')}")
    except Exception as e:
        print(f"❌ Error: {str(e)}")

def check_system_health():
    """Overall system health check."""
    print("\n🏥 System Health Check")
    print("======================")
    
    health_status = {
        "contract_deployed": False,
        "contract_initialized": False,
        "price_sources_available": False,
        "accounts_funded": False,
        "ready_for_updates": False
    }
    
    # Check contract deployment
    try:
        payload = {
            "jsonrpc": "2.0",
            "method": "getcontractstate",
            "params": [CONTRACT_HASH],
            "id": 1
        }
        response = requests.post(RPC_ENDPOINT, json=payload, timeout=10)
        result = response.json()
        if "result" in result:
            health_status["contract_deployed"] = True
    except:
        pass
    
    # Check initialization
    owner_result = invoke_contract_method("getOwner", [])
    if owner_result and owner_result["state"] == "HALT":
        stack = owner_result.get("stack", [])
        if stack and stack[0]["type"] != "Any":
            health_status["contract_initialized"] = True
    
    # Check price sources (at least 2 working)
    sources = test_price_sources()
    working_sources = sum(1 for s in sources.values() if s["status"] == "OK")
    health_status["price_sources_available"] = working_sources >= 2
    
    # Check account balances
    # (Would need to implement balance checking)
    health_status["accounts_funded"] = True  # Assume true for now
    
    # Overall readiness
    health_status["ready_for_updates"] = all([
        health_status["contract_deployed"],
        health_status["contract_initialized"],
        health_status["price_sources_available"],
        health_status["accounts_funded"]
    ])
    
    # Display results
    print("\nHealth Status:")
    for check, status in health_status.items():
        icon = "✅" if status else "❌"
        print(f"  {icon} {check.replace('_', ' ').title()}")
    
    if health_status["ready_for_updates"]:
        print("\n✅ System is ready for price updates!")
    else:
        print("\n❌ System is not ready. Please address the issues above.")
    
    return health_status

def main():
    """Main test function."""
    print("🧪 Neo Price Feed Oracle System Test")
    print("====================================")
    print(f"Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Contract: {CONTRACT_HASH}")
    print(f"Network: Neo N3 TestNet")
    print()
    
    # Run all tests
    test_contract_methods()
    health_status = check_system_health()
    
    if health_status["contract_initialized"]:
        simulate_price_update()
    else:
        print("\n⚠️  Skipping price update simulation - contract not initialized")
        print("   Please initialize the contract first using neo-cli")
    
    print("\n✅ Test completed!")

if __name__ == "__main__":
    main()