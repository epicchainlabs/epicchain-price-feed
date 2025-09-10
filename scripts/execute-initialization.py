#!/usr/bin/env python3
"""
Execute Neo Price Feed Oracle Contract Initialization
This script creates and sends the initialization transactions programmatically.
"""

import json
import requests
import sys
import time
import binascii
from neo3 import wallet as neo3_wallet
from neo3.core import types
from neo3.network.payloads import transaction as tx_module
from neo3.api.rpc import NeoRpcClient
from neo3.api import noderpc
from neo3.contracts import NeoToken, GasToken
from neo3.core.cryptography import ECPoint
from neo3.wallet import Account

# Configuration
RPC_ENDPOINT = "http://seed1t5.neo.org:20332"
CONTRACT_HASH = "0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
MASTER_ADDRESS = "NTmHjwiadq4g3VHpJ5FQigQcD4fF5m8TyX"
MASTER_WIF = "KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb"
TEE_ADDRESS = "NiNmXL8FjEUEs1nfX9uHFBNaenxDHJtmuB"

def create_and_send_transaction(rpc_client, script, account):
    """Create and send a transaction with the given script."""
    try:
        # Create transaction
        transaction = tx_module.Transaction()
        transaction.script = script
        transaction.signers = [tx_module.Signer(account.script_hash, tx_module.WitnessScope.CALLED_BY_ENTRY)]
        
        # Set network fee and system fee
        transaction.network_fee = 1000000  # 0.01 GAS
        transaction.system_fee = 5000000   # 0.05 GAS
        
        # Set valid until block
        current_height = rpc_client.get_block_count()
        transaction.valid_until_block = current_height + 100
        
        # Add witness
        transaction.witnesses = [tx_module.Witness(
            invocation_script=bytes(),  # Will be filled when signing
            verification_script=account.script_hash.to_array()
        )]
        
        # Sign transaction
        transaction.sign(account)
        
        # Send transaction
        result = rpc_client.send_raw_transaction(transaction)
        return result
        
    except Exception as e:
        print(f"Error: {e}")
        return None

def main():
    """Main function to execute initialization."""
    print("🚀 Executing Neo Price Feed Oracle Contract Initialization")
    print("=========================================================")
    
    try:
        # Import neo3 modules
        from neo3.api.rpc import NeoRpcClient
        from neo3.wallet import Account
        from neo3.contracts import Contract
        from neo3.core import types
        from neo3.network.payloads import transaction as tx_module
        from neo3.vm import VMState
        from neo3.core.cryptography import ECPoint, ECCCurve
        from neo3 import settings
        
        # Set network to TestNet
        settings.network.magic = 894710606  # TestNet magic number
        
    except ImportError:
        print("❌ Error: neo3 package not installed")
        print("Please install it with: pip3 install neo3-boa")
        return
    
    print(f"Contract: {CONTRACT_HASH}")
    print(f"Master Account: {MASTER_ADDRESS}")
    print(f"TEE Account: {TEE_ADDRESS}")
    print()
    
    try:
        # Create RPC client
        rpc_client = NeoRpcClient(RPC_ENDPOINT)
        
        # Create account from WIF
        account = Account.from_wif(MASTER_WIF)
        print(f"✅ Account loaded: {account.address}")
        
        # Check account balance
        balance = rpc_client.get_nep17_balances(account.address)
        gas_balance = 0
        for bal in balance:
            if bal['assethash'] == '0xd2a4cff31913016155e38e474a2c06d08be276cf':  # GAS
                gas_balance = int(bal['amount']) / 100000000
        
        print(f"💰 GAS Balance: {gas_balance:.8f} GAS")
        
        if gas_balance < 0.5:
            print("❌ Insufficient GAS balance. Need at least 0.5 GAS")
            return
        
        # Check if contract is already initialized
        print("\n🔍 Checking contract state...")
        contract_hash_bytes = types.UInt160.from_string(CONTRACT_HASH)
        
        result = rpc_client.invoke_function(
            contract_hash_bytes,
            "getOwner",
            []
        )
        
        if result.state == VMState.HALT and result.stack and result.stack[0].type != tx_module.StackItemType.ANY:
            print("⚠️  Contract appears to be already initialized!")
            return
        
        print("✅ Contract is not initialized. Proceeding...")
        
        # Step 1: Initialize contract
        print("\n1️⃣ Initializing contract...")
        
        # Build initialize script
        from neo3.contracts.contract import Contract
        contract = Contract(CONTRACT_HASH)
        
        init_script = contract.call_function(
            "initialize",
            [MASTER_ADDRESS, TEE_ADDRESS]
        )
        
        # Test the script first
        test_result = rpc_client.invoke_script(init_script)
        if test_result.state != VMState.HALT:
            print(f"❌ Script test failed: {test_result.exception}")
            return
        
        print(f"   Script test passed. Gas required: {test_result.gas_consumed / 100000000:.8f} GAS")
        
        # Send transaction
        tx_hash = create_and_send_transaction(rpc_client, init_script, account)
        if tx_hash:
            print(f"   ✅ Transaction sent: {tx_hash}")
            print("   ⏳ Waiting for confirmation...")
            time.sleep(20)
        else:
            print("   ❌ Failed to send transaction")
            return
        
        # Step 2: Add oracle
        print("\n2️⃣ Adding TEE as oracle...")
        
        oracle_script = contract.call_function(
            "addOracle",
            [TEE_ADDRESS]
        )
        
        tx_hash = create_and_send_transaction(rpc_client, oracle_script, account)
        if tx_hash:
            print(f"   ✅ Transaction sent: {tx_hash}")
            print("   ⏳ Waiting for confirmation...")
            time.sleep(20)
        else:
            print("   ❌ Failed to send transaction")
            return
        
        # Step 3: Set min oracles
        print("\n3️⃣ Setting minimum oracles to 1...")
        
        min_script = contract.call_function(
            "setMinOracles",
            [1]
        )
        
        tx_hash = create_and_send_transaction(rpc_client, min_script, account)
        if tx_hash:
            print(f"   ✅ Transaction sent: {tx_hash}")
            print("   ⏳ Waiting for confirmation...")
            time.sleep(20)
        else:
            print("   ❌ Failed to send transaction")
            return
        
        print("\n✅ Initialization complete!")
        
        # Verify the result
        print("\n🔍 Verifying contract state...")
        
        # Check owner
        result = rpc_client.invoke_function(contract_hash_bytes, "getOwner", [])
        if result.state == VMState.HALT and result.stack:
            owner = result.stack[0].to_bytes().decode('utf-8')
            print(f"   Owner: {owner}")
        
        # Check oracles
        result = rpc_client.invoke_function(contract_hash_bytes, "getOracles", [])
        if result.state == VMState.HALT and result.stack:
            print(f"   Oracles: {len(result.stack[0])} configured")
        
        # Check min oracles
        result = rpc_client.invoke_function(contract_hash_bytes, "getMinOracles", [])
        if result.state == VMState.HALT and result.stack:
            print(f"   Min Oracles: {result.stack[0].to_int()}")
        
        print("\n🎉 Contract is now ready to receive price updates!")
        
    except Exception as e:
        print(f"\n❌ Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()