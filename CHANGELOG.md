# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Professional project structure with src/, test/, docs/, scripts/, and config/ directories
- Comprehensive .editorconfig for consistent code formatting
- CONTRIBUTING.md with development guidelines
- CHANGELOG.md for tracking changes

### Changed
- Reorganized project structure for better maintainability
- Updated solution file to reflect new directory structure
- Moved configuration files to dedicated config/ directory
- Moved build scripts to scripts/ directory

## [1.0.0] - 2024-06-21

### Added
- Production-ready Neo N3 price feed service with TEE integration
- Multi-source data collection from Binance, CoinMarketCap, Coinbase, and OKEx
- Advanced price aggregation with confidence scoring
- Batch processing with dual-signature transaction system
- Comprehensive error handling and logging
- Smart contract for on-chain price storage
- GitHub Actions workflow for TEE execution
- Cryptographic attestations for security verification
- Account persistence across code updates
- Automated asset transfer from TEE to Master account

### Features
- **Security**: Dual-signature system with TEE and Master accounts
- **Reliability**: Fallback mechanisms for data source failures
- **Performance**: Batch processing and rate limiting
- **Monitoring**: Detailed logging and execution tracking
- **Flexibility**: Configurable symbol mappings and intervals

### Technical
- Built on .NET 9.0
- Neo N3 blockchain integration
- Clean architecture with separation of concerns
- Comprehensive unit and integration tests (56 tests passing)
- Production-ready configuration management
- Professional documentation

### Security
- Environment variable configuration for sensitive data
- Secure private key handling
- TEE-based execution environment
- Cryptographic transaction signing
- Access control and authorization

## [0.1.0] - Initial Development

### Added
- Basic project structure
- Initial data source adapters
- Core domain models
- Basic price aggregation logic
- Initial smart contract implementation

---

## Types of Changes

- **Added** for new features
- **Changed** for changes in existing functionality
- **Deprecated** for soon-to-be removed features
- **Removed** for now removed features
- **Fixed** for any bug fixes
- **Security** for security-related changes