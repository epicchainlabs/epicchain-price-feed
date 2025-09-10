# Dual-Signature Transaction System in TEE

This document explains the dual-signature transaction system used in the Price Feed Service running in a Trusted Execution Environment (TEE).

## Overview

The Price Feed Service uses a dual-signature transaction system where two Neo accounts are used for each transaction:

1. **TEE Account**: Generated within the TEE, used to authenticate that transactions are truly generated in the secure environment
2. **Master Account**: Set by you and securely provided to the TEE, contains GAS for transaction fees

This approach provides several benefits:
- **Authentication**: The TEE account signature proves that the transaction was generated in the secure TEE environment
- **Fee Payment**: The Master account pays for transaction fees, eliminating the need to fund the TEE account
- **Security**: The TEE account private key never leaves the secure environment
- **Flexibility**: You can control the Master account and fund it as needed

## Account Roles

### TEE Account

The TEE account is generated within the Trusted Execution Environment and is used to authenticate that transactions are truly generated in the TEE. This account:

- Is generated once through a secure key generation process
- Has its private key stored securely in the TEE's protected storage
- Is used to sign all price feed transactions
- Does not need to contain any assets (NEO or GAS)
- Provides a consistent identity for the Price Feed Service

### Master Account

The Master account is set by you and securely provided to the TEE, and is used to pay for transaction fees. This account:

- Is created and controlled by you
- Has its private key securely stored in the TEE's protected storage
- Contains GAS for transaction fees
- Is used to sign all price feed transactions
- Provides the funds needed for the Price Feed Service to operate

## Transaction Flow

When the Price Feed Service sends a transaction to update price data:

1. The transaction is created with both the TEE and Master accounts as signers
2. The transaction is first signed with the TEE account to prove it was generated in the secure environment
3. The transaction is then signed with the Master account to pay for transaction fees
4. The dual-signed transaction is sent to the Neo blockchain
5. The transaction fees are paid from the Master account's GAS balance

## Asset Transfer

If the TEE account receives any assets (NEO or GAS), the Price Feed Service will automatically transfer them to the Master account. This ensures that:

1. The TEE account remains empty, reinforcing that it's only used for authentication
2. Any assets sent to the TEE account are not lost
3. The Master account receives all assets, consolidating them in one place

The asset transfer process:
1. Checks if the TEE account has any NEO or GAS
2. If assets are found, creates a transaction to transfer them to the Master account
3. Signs the transaction with the TEE account's private key
4. Sends the transaction to the Neo blockchain
5. Logs the transfer details

## Setup Instructions

To set up the dual-signature transaction system:

1. **Generate the TEE Account**:
   - Run the key generation process in the TEE to generate the TEE account
   - This will securely store the TEE account credentials in the TEE's protected storage

2. **Create the Master Account**:
   - Create a Neo account using a wallet of your choice
   - Fund this account with GAS for transaction fees
   - Securely provide the account address and private key to the TEE:
     - `MASTER_ACCOUNT_ADDRESS`: The public address of the Master account
     - `MASTER_ACCOUNT_PRIVATE_KEY`: The private key of the Master account

3. **Configure Asset Transfer** (Optional):
   - By default, any assets in the TEE account will be transferred to the Master account
   - To disable this behavior, set the `CHECK_AND_TRANSFER_TEE_ASSETS` environment variable to `false`

## Security Considerations

- The Master account private key is stored in the TEE's protected storage, ensuring it's encrypted and only accessible within the TEE
- The TEE account private key is also stored in the TEE's protected storage and never leaves the secure environment
- Both accounts are required to sign transactions, providing an additional layer of security
- If either account's private key is compromised, the attacker cannot send transactions without the other account's private key

## Technical Implementation

The dual-signature transaction system is implemented through several components:

1. **BatchProcessingOptions**: Stores the credentials for both accounts
2. **BatchProcessingService**: Creates and signs transactions with both accounts
3. **CheckAndTransferTeeAssetsAsync**: Transfers assets from the TEE account to the Master account
4. **TEE Execution**: Configure and use both accounts for price feed updates
