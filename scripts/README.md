# Build Scripts

This directory contains build and deployment scripts for the Neo Price Feed project.

## Available Scripts

### `build-contract.sh`

Builds the Neo N3 smart contract for deployment.

**Usage:**
```bash
./scripts/build-contract.sh
```

**Requirements:**
- Neo SDK for .NET
- .NET 9.0 SDK

**Output:**
- Compiled contract files in `src/PriceFeed.Contracts/bin/`
- Contract manifest and NEF files ready for deployment

### Future Scripts

Additional scripts planned for future releases:

- **`deploy-contract.sh`** - Automated contract deployment
- **`run-tests.sh`** - Comprehensive test execution
- **`build-release.sh`** - Release build automation
- **`setup-dev-environment.sh`** - Development environment setup

## Script Guidelines

When creating new scripts:

1. **Use proper error handling** with `set -e`
2. **Include help documentation** with `--help` flag
3. **Validate prerequisites** before execution
4. **Provide clear output** and status messages
5. **Make scripts idempotent** when possible

## Platform Compatibility

- **Primary target**: Linux (GitHub Actions)
- **Secondary support**: macOS
- **Windows**: Use WSL or Git Bash

For Windows users, consider using PowerShell equivalents in the `.github/workflows/` directory.

## Contributing

When adding new scripts:

1. Follow existing patterns and naming conventions
2. Include proper documentation
3. Test on multiple platforms when possible
4. Update this README with new script descriptions