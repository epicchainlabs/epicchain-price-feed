#!/usr/bin/env python3
"""
Initialize deployed contract with admin accounts and settings
"""

import json
import sys
from pathlib import Path

def load_config():
    """Load configuration to get contract hash"""
    config_path = Path("src/PriceFeed.Console/appsettings.json")
    
    if not config_path.exists():
        print(f"❌ Configuration file not found: {config_path}")
        return None
    
    try:
        with open(config_path, 'r') as f:
            config = json.load(f)
        
        contract_hash = config.get("BatchProcessing", {}).get("ContractScriptHash", "")
        
        if not contract_hash or contract_hash == "0x245f20c5932eb9c5db16b66b9d074b40ee12be50":
            print("❌ Contract hash not updated in configuration")
            print("   Please run: python3 scripts/update-contract-hash.py YOUR_CONTRACT_HASH")
            return None
        
        return config
        
    except Exception as e:
        print(f"❌ Error reading configuration: {e}")
        return None

def show_initialization_commands(contract_hash):
    """Show initialization commands to run"""
    print(f"🔧 CONTRACT INITIALIZATION COMMANDS")
    print("=" * 50)
    print(f"Contract Hash: {contract_hash}")
    print()
    
    print("📝 Run these commands in your deployment tool (NeoLine, Neo-CLI, etc.):")
    print()
    
    print("1️⃣ Initialize contract with admin accounts:")
    print(f'   invoke {contract_hash} initialize ["NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX","NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"]')
    print()
    
    print("2️⃣ Add TEE account as oracle:")
    print(f'   invoke {contract_hash} addOracle ["NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"]')
    print()
    
    print("3️⃣ Set minimum oracles to 1:")
    print(f'   invoke {contract_hash} setMinOracles [1]')
    print()
    
    print("🔍 Verification commands:")
    print(f'   invoke {contract_hash} getOracleCount []')
    print(f'   invoke {contract_hash} isOracle ["NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"]')
    print(f'   invoke {contract_hash} getMinOracles []')

def show_test_commands():
    """Show test commands"""
    print(f"\n🧪 TEST YOUR ORACLE")
    print("=" * 30)
    print()
    print("After initialization, test the complete workflow:")
    print()
    print("dotnet run --project src/PriceFeed.Console --skip-health-checks")
    print()
    print("Expected output:")
    print("✅ Successfully processed batch with 3 prices")
    print("✅ Price for BTCUSDT: $XXX,XXX (Confidence: 60%)")
    print("✅ Price for ETHUSDT: $X,XXX (Confidence: 60%)")
    print("✅ Price for NEOUSDT: $X.XX (Confidence: 60%)")
    print("✅ Transaction sent: 0x[hash]")

def show_explorer_links(contract_hash):
    """Show explorer links"""
    print(f"\n🔍 VERIFY ON TESTNET EXPLORER")
    print("=" * 35)
    print()
    print(f"Contract: https://testnet.explorer.onegate.space/contractinfo/{contract_hash}")
    print(f"Account:  https://testnet.explorer.onegate.space/address/NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX")

def main():
    """Main initialization guide"""
    print("🚀 Neo N3 Contract Initialization Guide")
    print("=" * 45)
    print()
    
    # Load configuration
    config = load_config()
    if not config:
        return False
    
    contract_hash = config["BatchProcessing"]["ContractScriptHash"]
    
    print(f"✅ Configuration loaded successfully")
    print(f"   Contract Hash: {contract_hash}")
    print()
    
    # Show initialization commands
    show_initialization_commands(contract_hash)
    
    # Show test commands
    show_test_commands()
    
    # Show explorer links
    show_explorer_links(contract_hash)
    
    print(f"\n🎯 NEXT STEPS:")
    print("1. Run the initialization commands above")
    print("2. Test your oracle with the dotnet command")
    print("3. Verify on the TestNet explorer")
    print("4. Your Neo N3 Price Feed Oracle is LIVE! 🎉")
    
    return True

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nInitialization guide cancelled by user")
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")