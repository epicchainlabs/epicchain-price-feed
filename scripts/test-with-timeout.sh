#!/bin/bash
# Run tests with strict timeout to prevent hanging

set -e

echo "Running tests with timeout protection..."

# Set maximum test execution time (5 minutes)
timeout 300 dotnet test PriceFeed.CI.sln \
  --configuration Release \
  --no-build \
  --verbosity normal \
  --logger "console;verbosity=minimal" \
  --blame-hang-timeout 4m \
  -- RunConfiguration.TestSessionTimeout=240000 \
     xunit.parallelizeAssembly=false \
     xunit.parallelizeTestCollections=false

exit_code=$?

if [ $exit_code -eq 124 ]; then
    echo "❌ Tests timed out after 5 minutes - there may be deadlocks or infinite loops"
    exit 1
elif [ $exit_code -ne 0 ]; then
    echo "❌ Tests failed with exit code $exit_code"
    exit $exit_code
else
    echo "✅ Tests completed successfully"
fi