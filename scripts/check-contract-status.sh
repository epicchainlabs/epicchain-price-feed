#!/bin/bash
# Check the status of the deployed Neo Price Feed Oracle contract on TestNet

CONTRACT_HASH="0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
RPC_ENDPOINT="http://seed1t5.neo.org:20332"

echo "Neo Price Feed Oracle - Contract Status Check"
echo "============================================"
echo "Contract Hash: $CONTRACT_HASH"
echo "Network: TestNet"
echo "RPC Endpoint: $RPC_ENDPOINT"
echo ""

# Function to make RPC calls
rpc_call() {
    local method=$1
    local params=$2
    
    curl -s -X POST "$RPC_ENDPOINT" \
        -H "Content-Type: application/json" \
        -d "{
            \"jsonrpc\": \"2.0\",
            \"method\": \"$method\",
            \"params\": $params,
            \"id\": 1
        }"
}

# Get contract state
echo "Checking contract state..."
CONTRACT_STATE=$(rpc_call "getcontractstate" "[\"$CONTRACT_HASH\"]")

if echo "$CONTRACT_STATE" | grep -q "error"; then
    echo "❌ Error: Contract not found or RPC error"
    echo "$CONTRACT_STATE" | jq -r '.error.message' 2>/dev/null || echo "$CONTRACT_STATE"
else
    echo "✅ Contract found on TestNet"
    
    # Extract contract info
    MANIFEST=$(echo "$CONTRACT_STATE" | jq -r '.result.manifest' 2>/dev/null)
    if [ ! -z "$MANIFEST" ]; then
        NAME=$(echo "$MANIFEST" | jq -r '.name' 2>/dev/null)
        echo "Contract Name: $NAME"
        
        # Show available methods
        echo ""
        echo "Available Methods:"
        echo "$MANIFEST" | jq -r '.abi.methods[].name' 2>/dev/null | sed 's/^/  - /'
    fi
fi

echo ""
echo "To check if the contract is initialized:"
echo "1. Use neo-cli to invoke the 'isInitialized' method"
echo "2. Or use the Neo TestNet explorer:"
echo "   https://testnet.explorer.onegate.space/contract/$CONTRACT_HASH"
echo ""
echo "For manual initialization, follow INITIALIZATION_GUIDE.md"