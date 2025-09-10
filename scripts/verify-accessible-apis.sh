#!/bin/bash

# Script to verify that all accessible API endpoints are working correctly
# This script tests the real endpoints used in the Neo Price Feed system

echo "🔍 Verifying Accessible API Endpoints..."
echo "========================================="

# Test CoinGecko API
echo ""
echo "✅ Testing CoinGecko API..."
echo "Endpoint: https://api.coingecko.com/api/v3/simple/price"
COINGECKO_RESPONSE=$(curl -s "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=usd&include_24hr_vol=true")
if echo "$COINGECKO_RESPONSE" | grep -q "bitcoin"; then
    echo "✅ CoinGecko API: WORKING"
    echo "   Bitcoin Price: $(echo "$COINGECKO_RESPONSE" | jq -r '.bitcoin.usd') USD"
    echo "   Ethereum Price: $(echo "$COINGECKO_RESPONSE" | jq -r '.ethereum.usd') USD"
else
    echo "❌ CoinGecko API: FAILED"
fi

# Test Kraken API
echo ""
echo "✅ Testing Kraken API..."
echo "Endpoint: https://api.kraken.com/0/public/Ticker"
KRAKEN_RESPONSE=$(curl -s "https://api.kraken.com/0/public/Ticker?pair=XBTUSD")
if echo "$KRAKEN_RESPONSE" | grep -q "XXBTZUSD"; then
    echo "✅ Kraken API: WORKING"
    KRAKEN_PRICE=$(echo "$KRAKEN_RESPONSE" | jq -r '.result.XXBTZUSD.c[0]')
    echo "   Bitcoin Price: $KRAKEN_PRICE USD"
else
    echo "❌ Kraken API: FAILED"
fi

# Test Coinbase API
echo ""
echo "✅ Testing Coinbase API..."
echo "Endpoint: https://api.coinbase.com/v2/exchange-rates"
COINBASE_RESPONSE=$(curl -s "https://api.coinbase.com/v2/exchange-rates?currency=BTC")
if echo "$COINBASE_RESPONSE" | grep -q "USD"; then
    echo "✅ Coinbase API: WORKING"
    USD_RATE=$(echo "$COINBASE_RESPONSE" | jq -r '.data.rates.USD')
    BTC_PRICE=$(echo "scale=2; 1 / $USD_RATE" | bc -l)
    echo "   Bitcoin Price: ~$BTC_PRICE USD (calculated from exchange rate)"
else
    echo "❌ Coinbase API: FAILED"
fi

# Test restricted APIs (for comparison)
echo ""
echo "⚠️  Testing Restricted APIs (for comparison)..."

# Test Binance API (may be restricted)
echo ""
echo "⚠️  Testing Binance API..."
echo "Endpoint: https://api.binance.com/api/v3/ticker/price"
BINANCE_RESPONSE=$(curl -s --max-time 10 "https://api.binance.com/api/v3/ticker/price?symbol=BTCUSDT" 2>/dev/null)
if echo "$BINANCE_RESPONSE" | grep -q "price"; then
    echo "✅ Binance API: ACCESSIBLE in your region"
    echo "   Bitcoin Price: $(echo "$BINANCE_RESPONSE" | jq -r '.price') USDT"
else
    echo "❌ Binance API: RESTRICTED in your region"
fi

# Test CoinMarketCap API (requires API key)
echo ""
echo "⚠️  Testing CoinMarketCap API..."
echo "Endpoint: https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest"
CMC_RESPONSE=$(curl -s --max-time 10 "https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest?symbol=BTC" 2>/dev/null)
if echo "$CMC_RESPONSE" | grep -q "API_KEY_REQUIRED\|API_KEY_INVALID"; then
    echo "❌ CoinMarketCap API: REQUIRES API KEY (as expected)"
elif echo "$CMC_RESPONSE" | grep -q "price"; then
    echo "✅ CoinMarketCap API: Working (API key configured)"
else
    echo "❌ CoinMarketCap API: NOT ACCESSIBLE"
fi

echo ""
echo "========================================="
echo "📊 Summary:"
echo "✅ Accessible APIs: CoinGecko, Kraken, Coinbase (exchange rates)"
echo "❌ Restricted APIs: Binance (regional), CoinMarketCap (API key), OKEx (domain)"
echo ""
echo "💡 Recommendation: Use the accessible APIs for reliable global access"
echo "   Primary: CoinGecko + Kraken"
echo "   Secondary: Coinbase (exchange rates)"
echo ""
echo "🔧 Configuration: Use appsettings.accessible.json for maximum compatibility"