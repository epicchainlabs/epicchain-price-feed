# Neo Account Persistence in TEE

This document explains how the Neo accounts (TEE and Master) are generated, stored, and persisted across code updates in the Price Feed Service running in a Trusted Execution Environment (TEE).

## Account Generation and Storage

The Price Feed Service uses two Neo accounts:

1. **TEE Account**: Generated within the TEE, used to authenticate that transactions are truly generated in the secure environment
2. **Master Account**: Set by you and securely provided to the TEE, contains GAS for transaction fees

### TEE Account Generation

The TEE account is generated **only once** through a secure key generation process within the TEE. This account is then stored securely in the TEE's protected storage, which is independent of the codebase. This ensures that the account remains the same even when the code is updated.

### Master Account Setup

The Master account is created and controlled by you. You need to:
1. Create a Neo account using a wallet of your choice
2. Fund this account with GAS for transaction fees
3. Securely provide the account address and private key to the TEE

### Key Points

1. **Dual-Signature System**: Both accounts are used to sign transactions, providing authentication and fee payment
2. **One-Time Generation**: The TEE account is generated only once and then reused for all subsequent price feed updates
3. **Persistent Storage**: Both account credentials are stored securely
   - TEE Account: Stored in the TEE's protected storage
   - Master Account: Provided securely to the TEE
4. **Attestation**: An attestation is created to prove that the TEE account was generated within the secure environment
5. **Exclusive Access**: Only the TEE has access to the private keys - they are never exposed in logs or artifacts
6. **No External Backup**: For security reasons, there is no backup of the TEE account private key outside of the secure environment

### Security Measures

The following security measures ensure that only the TEE has access to the account credentials:

1. **Secure Generation**: The account is generated using a secure process that prevents the private key from appearing in logs.
2. **Protected Storage**: The account credentials are stored in the TEE's protected storage, which is:
   - Encrypted at rest using secure encryption
   - Only accessible within the TEE
   - Never exposed in logs or outputs
   - Not accessible to anyone outside the TEE
3. **No External Storage**: The private key is not stored anywhere outside of the TEE's protected storage.
4. **Masked Values**: Any reference to the private key or address in logs is automatically masked.
5. **Restricted Access**: Access to the TEE is strictly controlled and limited.

## Safeguards Against Accidental Overwriting

The key generation process includes safeguards to prevent accidentally overwriting the existing TEE account:

1. **Existence Check**: Before generating a new TEE account, the system checks if an account already exists in the TEE's protected storage.
2. **Process Termination**: If a TEE account already exists, the key generation process terminates with an error message.
3. **Manual Override**: To generate a new TEE account and replace the existing one, you must first manually delete the existing account from the TEE's protected storage.

For the Master account, since it's set by you, there are no automatic safeguards. Be careful not to overwrite it accidentally.

## Recovery Procedure

In case the account credentials in the TEE are accidentally deleted, you have the following options:

### TEE Account Recovery

#### Option 1: Generate a New TEE Account (Recommended)

1. Delete the existing TEE account credentials from the TEE's protected storage (if they exist)
2. Run the key generation process to generate a new TEE account
3. Update the smart contract to authorize the new TEE account
4. Update any other services that depend on the TEE account address

#### Option 2: Contact TEE Support

In some cases, the TEE support team may be able to help recover recently deleted credentials.

> **Important Security Note**: For security reasons, there is no backup of the TEE account private key outside of the TEE's protected storage. This ensures that only the TEE has access to the private key.

### Master Account Recovery

Since you created and control the Master account, you should have a backup of its private key. If the credential is deleted from the TEE:

1. Re-add the Master account address and private key to the TEE's protected storage

If you've lost the Master account private key, you'll need to:

1. Create a new Master account and fund it with GAS
2. Add the new account credentials to the TEE
3. Update the smart contract to recognize the new account (if necessary)

## Verification

The TEE verifies both accounts before using them:

1. **TEE Account Verification**:
   - **Attestation Verification**: If available, the system verifies the TEE account attestation.
   - **Credential Verification**: The system checks that the TEE account credentials exist in the TEE's protected storage.

2. **Master Account Verification**:
   - **Credential Verification**: The system checks that the Master account credentials exist in the TEE's protected storage.

## Important Security Notes

- **DO NOT** run the key generation process again unless you intend to replace the existing TEE account.
- **DO NOT** delete the account credentials from the TEE's protected storage:
  - TEE Account credentials
  - Master Account credentials
- **DO NOT** attempt to extract or view the private keys - they should remain exclusively within the TEE.
- **DO** keep a secure backup of your Master account private key.
- **DO** restrict access to the TEE to prevent unauthorized access to credentials.
- **DO** use code integrity verification to prevent unauthorized changes to the application.
- **DO** ensure that the TEE has the necessary permissions to manage the accounts.

## Access Control Recommendations

To further enhance security and ensure that only the TEE has access to the credentials:

1. **Restrict TEE Access**: Limit the number of people who have access to the TEE.
2. **Enable Code Signing**: Require code signing and verification before execution in the TEE.
3. **Audit Access Regularly**: Regularly review who has access to the TEE and revoke unnecessary access.
4. **Use Secure Boot**: Set up secure boot to ensure only authorized code runs in the TEE.
5. **Enable Attestation**: Require attestation for all TEE operations to verify the environment's integrity.
6. **Set Up Alerts**: Configure alerts for unauthorized access attempts and integrity violations.

## Technical Implementation

The account persistence is implemented through several mechanisms:

1. **Protected Storage**: Both account credentials are stored in the TEE's protected storage, which is encrypted and only accessible within the TEE.
2. **Attestation**: An attestation is created to prove that the TEE account was generated within the secure environment.
3. **Secure Generation**: The TEE account is generated using a secure process that prevents the private key from appearing in logs.
4. **Generation Safeguards**: The key generation process includes safeguards to prevent accidentally overwriting the existing TEE account.
5. **Value Masking**: The system automatically masks sensitive values in logs.
6. **Dual-Signature Verification**: The smart contract verifies that transactions are signed by both the TEE and Master accounts.
7. **Asset Transfer**: Any assets received by the TEE account are automatically transferred to the Master account.

This multi-layered approach ensures that:
1. Both accounts remain the same even when the code is updated
2. Only the TEE has access to the private keys
3. The private keys are never exposed in logs or artifacts
4. The accounts cannot be accidentally overwritten
5. Transactions are provably generated in the secure TEE
6. Transaction fees are paid by the Master account

## TEE's Security Features

The Trusted Execution Environment provides a secure way to store sensitive information:

1. **Hardware-Based Encryption**: Credentials are encrypted using hardware-based cryptography, with keys that only the TEE can access.
2. **Access Control**: Credentials are only accessible to code running within the TEE and cannot be viewed by users, even system administrators.
3. **Runtime Security**: During execution, sensitive data is:
   - Protected in memory
   - Automatically redacted from logs if printed
   - Not accessible to code running outside the TEE
4. **Isolation**: The TEE provides hardware-level isolation from the rest of the system, including the operating system.

For more information, see the documentation on Trusted Execution Environments and secure enclaves.
