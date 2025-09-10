# R3E Smart Contract Development Environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Install R3E tools
RUN dotnet tool install --global r3e-compiler --version 1.0.0-*
ENV PATH="${PATH}:/root/.dotnet/tools"

# Set working directory
WORKDIR /app

# Copy solution file
COPY PriceFeed.R3E.sln ./

# Copy project files
COPY PriceFeed.R3E.Contract/*.csproj ./PriceFeed.R3E.Contract/
COPY PriceFeed.R3E.Tests/*.csproj ./PriceFeed.R3E.Tests/
COPY PriceFeed.R3E.Deploy/*.csproj ./PriceFeed.R3E.Deploy/
COPY PriceFeed.R3E.Benchmarks/*.csproj ./PriceFeed.R3E.Benchmarks/

# Restore packages
RUN dotnet restore

# Copy source code
COPY . ./

# Build contract
RUN dotnet build PriceFeed.R3E.Contract/PriceFeed.R3E.Contract.csproj -c Release

# Build tests
RUN dotnet build PriceFeed.R3E.Tests/PriceFeed.R3E.Tests.csproj -c Release

# Build deployment tool
RUN dotnet build PriceFeed.R3E.Deploy/PriceFeed.R3E.Deploy.csproj -c Release

# Run tests
RUN dotnet test PriceFeed.R3E.Tests/PriceFeed.R3E.Tests.csproj -c Release --no-build

# Create runtime image for deployment
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app

# Copy deployment tool
COPY --from=build-env /app/PriceFeed.R3E.Deploy/bin/Release/net8.0/ ./deploy/

# Copy contract artifacts
COPY --from=build-env /app/PriceFeed.R3E.Contract/bin/sc/ ./contracts/

# Set entrypoint
ENTRYPOINT ["dotnet", "/app/deploy/PriceFeed.R3E.Deploy.dll"]