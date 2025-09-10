#!/bin/bash

# R3E PriceFeed Docker Quick Start Script

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo "🐳 R3E PriceFeed Docker Quick Start"
echo "=================================="
echo

# Function to print colored output
print_info() {
    echo -e "${GREEN}ℹ️  $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Check Docker installation
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Main menu
while true; do
    echo
    echo "Select an option:"
    echo "1) Build R3E development environment"
    echo "2) Compile smart contract"
    echo "3) Run tests"
    echo "4) Run benchmarks"
    echo "5) Start local Neo node"
    echo "6) Deploy to local node"
    echo "7) Deploy to TestNet"
    echo "8) Start contract monitor"
    echo "9) Clean up containers"
    echo "0) Exit"
    echo
    read -p "Enter your choice: " choice

    case $choice in
        1)
            print_info "Building R3E development environment..."
            docker-compose build r3e-dev
            print_info "Development environment ready!"
            print_info "Run: docker-compose run --rm r3e-dev"
            ;;
        
        2)
            print_info "Compiling smart contract..."
            mkdir -p build-output
            docker-compose run --rm r3e-compiler
            if [ -f "build-output/PriceFeed.Oracle.nef" ]; then
                print_info "Contract compiled successfully!"
                print_info "Output files in: ./build-output/"
            else
                print_error "Compilation failed"
            fi
            ;;
        
        3)
            print_info "Running tests..."
            docker-compose run --rm r3e-tests
            print_info "Test results saved in TestResults/"
            ;;
        
        4)
            print_info "Running benchmarks..."
            docker-compose run --rm r3e-benchmarks
            print_info "Benchmark results saved in BenchmarkDotNet.Artifacts/"
            ;;
        
        5)
            print_info "Starting local Neo node..."
            docker-compose up -d neo-node
            print_info "Neo node started on http://localhost:20332"
            print_info "Waiting for node to be ready..."
            sleep 10
            ;;
        
        6)
            print_info "Deploying to local node..."
            read -p "Enter deployer WIF: " DEPLOYER_WIF
            read -p "Enter owner address: " OWNER_ADDRESS
            read -p "Enter TEE account address: " TEE_ACCOUNT_ADDRESS
            
            export NETWORK=Local
            export RPC_ENDPOINT=http://neo-node:20332
            export DEPLOYER_WIF
            export OWNER_ADDRESS
            export TEE_ACCOUNT_ADDRESS
            export DEPLOY_COMMAND=deploy
            
            docker-compose run --rm r3e-deploy
            
            read -p "Enter contract hash from deployment: " CONTRACT_HASH
            export CONTRACT_HASH
            export DEPLOY_COMMAND=initialize
            
            docker-compose run --rm r3e-deploy
            print_info "Deployment complete!"
            ;;
        
        7)
            print_info "Deploying to TestNet..."
            print_warning "Make sure you have configured environment variables:"
            echo "  - DEPLOYER_WIF"
            echo "  - OWNER_ADDRESS"
            echo "  - TEE_ACCOUNT_ADDRESS"
            
            read -p "Continue? (y/n) " -n 1 -r
            echo
            if [[ $REPLY =~ ^[Yy]$ ]]; then
                export NETWORK=TestNet
                export DEPLOY_COMMAND=deploy
                docker-compose run --rm r3e-deploy
            fi
            ;;
        
        8)
            print_info "Starting contract monitor..."
            read -p "Enter contract hash to monitor: " CONTRACT_HASH
            export CONTRACT_HASH
            docker-compose up -d r3e-monitor
            print_info "Monitor started. View logs: docker-compose logs -f r3e-monitor"
            ;;
        
        9)
            print_warning "Cleaning up containers..."
            docker-compose down -v
            print_info "Cleanup complete!"
            ;;
        
        0)
            print_info "Exiting..."
            exit 0
            ;;
        
        *)
            print_error "Invalid choice"
            ;;
    esac
done