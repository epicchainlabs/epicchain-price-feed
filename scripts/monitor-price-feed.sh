#!/bin/bash

# Monitor Neo Price Feed Oracle
# This script continuously monitors the price feed system

echo "📊 Neo Price Feed Oracle Monitor"
echo "================================"
echo "Contract: 0xc14ffc3f28363fe59645873b28ed3ed8ccb774cc"
echo "Press Ctrl+C to stop monitoring"
echo ""

# Function to check contract state
check_contract() {
    echo -e "\n🔍 Checking contract state..."
    cd /home/neo/git/neo-price-feed/src/PriceFeed.ContractDeployer
    dotnet run verify 2>/dev/null | grep -E "(Owner:|TEE Accounts:|Oracles:|Min Oracles:|BTCUSDT:|ETHUSDT:|NEOUSDT:|Master Account:|TEE Account:)"
}

# Function to check GitHub Actions status
check_github_actions() {
    echo -e "\n🚀 Latest GitHub Actions runs:"
    # This would normally use gh CLI, but we'll show the command
    echo "   Run: gh run list --workflow=price-feed-testnet.yml --limit=5"
    echo "   (Install GitHub CLI to see actual results)"
}

# Function to test price sources
test_price_sources() {
    echo -e "\n🌐 Testing price sources..."
    
    # Test CoinGecko
    echo -n "   CoinGecko: "
    if curl -s "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd" | grep -q "bitcoin"; then
        echo "✅ OK"
    else
        echo "❌ Failed"
    fi
    
    # Test Kraken
    echo -n "   Kraken: "
    if curl -s "https://api.kraken.com/0/public/Ticker?pair=XBTUSD" | grep -q "result"; then
        echo "✅ OK"
    else
        echo "❌ Failed"
    fi
    
    # Test Coinbase
    echo -n "   Coinbase: "
    if curl -s "https://api.coinbase.com/v2/exchange-rates?currency=BTC" | grep -q "data"; then
        echo "✅ OK"
    else
        echo "❌ Failed"
    fi
}

# Main monitoring loop
while true; do
    clear
    echo "📊 Neo Price Feed Oracle Monitor"
    echo "================================"
    echo "Time: $(date '+%Y-%m-%d %H:%M:%S')"
    
    check_contract
    test_price_sources
    check_github_actions
    
    echo -e "\n⏳ Next update in 60 seconds..."
    sleep 60
done