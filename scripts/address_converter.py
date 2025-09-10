#!/usr/bin/env python3
"""
Convert Neo addresses to script hashes for neo-cli
"""

def address_to_scripthash(address):
    """Convert Neo address to script hash (without base58 dependency)"""
    # Manual conversion for known addresses
    address_map = {
        "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX": "285f1b5a0ae39889635da4df5a58dc4e2f1f163f",
        "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB": "936b3e5e7c0e9fc87f8cc2e1698ce62d6cb951b8"
    }
    
    return address_map.get(address)

def show_commands():
    """Show corrected neo-cli commands"""
    master_addr = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
    tee_addr = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"
    
    master_hash = address_to_scripthash(master_addr)
    tee_hash = address_to_scripthash(tee_addr)
    
    contract = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
    
    print("🔧 CORRECTED NEO-CLI COMMANDS")
    print("=" * 50)
    print()
    
    print("Address to Script Hash conversion:")
    print(f"Master: {master_addr} → 0x{master_hash}")
    print(f"TEE:    {tee_addr} → 0x{tee_hash}")
    print()
    
    print("1️⃣ Initialize (Method 1 - with 0x prefix):")
    print(f"invoke {contract} initialize [0x{master_hash}, 0x{tee_hash}]")
    print()
    
    print("2️⃣ Initialize (Method 2 - without 0x prefix):")
    print(f"invoke {contract} initialize [{master_hash}, {tee_hash}]")
    print()
    
    print("3️⃣ Initialize (Method 3 - using address strings):")
    print(f'invoke {contract} initialize ["{master_addr}", "{tee_addr}"]')
    print()
    
    print("4️⃣ Alternative: Use invoke with JSON file:")
    print("invoke initialize.neo-invoke.json")
    print()
    
    print("Try each method until one works. Neo-CLI version 3.8.2 may have different syntax requirements.")

if __name__ == "__main__":
    show_commands()