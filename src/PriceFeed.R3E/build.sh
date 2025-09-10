#!/bin/bash

# R3E PriceFeed Contract Build Script

set -e

echo "🔨 Building R3E PriceFeed Contract Solution"
echo "=========================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print success
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

# Function to print error
print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf */bin */obj
print_success "Clean completed"

# Restore packages
echo "📦 Restoring NuGet packages..."
dotnet restore PriceFeed.R3E.sln
print_success "Package restore completed"

# Build contract
echo "🏗️  Building contract project..."
dotnet build PriceFeed.R3E.Contract/PriceFeed.R3E.Contract.csproj -c Release
if [ $? -eq 0 ]; then
    print_success "Contract build completed"
    
    # Check if R3E compiler generated files
    if [ -f "PriceFeed.R3E.Contract/bin/sc/PriceFeed.Oracle.nef" ]; then
        print_success "NEF file generated"
    else
        print_error "NEF file not found - R3E compiler may have failed"
    fi
    
    if [ -f "PriceFeed.R3E.Contract/bin/sc/PriceFeed.Oracle.manifest.json" ]; then
        print_success "Manifest file generated"
    else
        print_error "Manifest file not found - R3E compiler may have failed"
    fi
else
    print_error "Contract build failed"
    exit 1
fi

# Build tests
echo "🧪 Building test project..."
dotnet build PriceFeed.R3E.Tests/PriceFeed.R3E.Tests.csproj -c Release
if [ $? -eq 0 ]; then
    print_success "Test project build completed"
else
    print_error "Test project build failed"
    exit 1
fi

# Build deployment tool
echo "🚀 Building deployment project..."
dotnet build PriceFeed.R3E.Deploy/PriceFeed.R3E.Deploy.csproj -c Release
if [ $? -eq 0 ]; then
    print_success "Deployment project build completed"
else
    print_error "Deployment project build failed"
    exit 1
fi

# Run tests (optional)
read -p "Run tests? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🧪 Running tests..."
    dotnet test PriceFeed.R3E.Tests/PriceFeed.R3E.Tests.csproj --no-build -c Release
    if [ $? -eq 0 ]; then
        print_success "All tests passed"
    else
        print_error "Some tests failed"
    fi
fi

echo
echo "✅ Build process completed!"
echo
echo "Next steps:"
echo "1. Configure deployment settings in PriceFeed.R3E.Deploy/appsettings.json"
echo "2. Deploy contract: cd PriceFeed.R3E.Deploy && dotnet run -- deploy"
echo "3. Initialize contract: cd PriceFeed.R3E.Deploy && dotnet run -- initialize"
echo "4. Verify deployment: cd PriceFeed.R3E.Deploy && dotnet run -- verify"