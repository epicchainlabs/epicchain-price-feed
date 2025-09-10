# Contributing to Neo Price Feed

Thank you for your interest in contributing to the Neo Price Feed project! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)

## Code of Conduct

This project adheres to a code of conduct that promotes a welcoming and inclusive environment. Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Git
- Your preferred IDE (Visual Studio, VS Code, or Rider)

### Getting Started

1. **Fork the repository**
   ```bash
   # Click the "Fork" button on GitHub, then clone your fork
   git clone https://github.com/YOUR_USERNAME/neo-price-feed.git
   cd neo-price-feed
   ```

2. **Set up upstream remote**
   ```bash
   git remote add upstream https://github.com/r3e-network/neo-price-feed.git
   ```

3. **Install dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the solution**
   ```bash
   dotnet build
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

## Project Structure

```
neo-price-feed/
├── src/                          # Source code
│   ├── PriceFeed.Core/          # Domain models and interfaces
│   ├── PriceFeed.Infrastructure/ # External integrations and services
│   ├── PriceFeed.Console/       # Console application entry point
│   └── PriceFeed.Contracts/     # Smart contracts
├── test/                        # Test projects
│   └── PriceFeed.Tests/         # Unit and integration tests
├── docs/                        # Documentation
├── scripts/                     # Build and deployment scripts
├── config/                      # Configuration templates
└── .github/                     # GitHub workflows and templates
```

## Coding Standards

### C# Style Guidelines

We follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

#### Key Points:

- **Naming**: Use PascalCase for public members, camelCase for private fields
- **Indentation**: Use 4 spaces (no tabs)
- **Line endings**: Use CRLF for Windows compatibility
- **File encoding**: UTF-8 with BOM

#### Example:

```csharp
namespace PriceFeed.Core.Models
{
    /// <summary>
    /// Represents aggregated price data from multiple sources
    /// </summary>
    public class AggregatedPriceData
    {
        private readonly string _symbol;

        public string Symbol => _symbol;
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public int ConfidenceScore { get; set; }

        public AggregatedPriceData(string symbol)
        {
            _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        }
    }
}
```

### Documentation Standards

- All public APIs must have XML documentation
- Include examples in documentation when helpful
- Keep README files up to date
- Use clear, concise language

### Error Handling

- Use exceptions for exceptional conditions
- Provide meaningful error messages
- Log errors appropriately using the configured logger
- Don't suppress exceptions without good reason

## Testing Guidelines

### Test Organization

- Unit tests should test individual components in isolation
- Integration tests should test interactions between components
- Use descriptive test method names following the pattern: `MethodName_Scenario_ExpectedResult`

### Example Test:

```csharp
[Fact]
public async Task ProcessBatchAsync_WithValidBatch_ShouldReturnTrue()
{
    // Arrange
    var batch = new PriceBatch
    {
        Prices = new List<AggregatedPriceData>
        {
            new AggregatedPriceData("BTCUSDT") { Price = 50000, ConfidenceScore = 95 }
        }
    };

    // Act
    var result = await _service.ProcessBatchAsync(batch);

    // Assert
    Assert.True(result);
}
```

### Test Coverage

- Aim for high test coverage (>80%)
- Focus on testing critical business logic
- Test error conditions and edge cases
- Include integration tests for external dependencies

## Pull Request Process

### Before Submitting

1. **Sync with upstream**
   ```bash
   git fetch upstream
   git checkout main
   git merge upstream/main
   ```

2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes**
   - Follow coding standards
   - Add/update tests as needed
   - Update documentation if applicable

4. **Test your changes**
   ```bash
   dotnet test
   ```

5. **Commit your changes**
   ```bash
   git commit -m "Add feature: brief description"
   ```

### Submitting the Pull Request

1. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create pull request**
   - Use the provided pull request template
   - Provide a clear description of changes
   - Link any related issues

3. **Address feedback**
   - Respond to code review comments
   - Make requested changes
   - Push updates to the same branch

### Pull Request Requirements

- [ ] All tests pass
- [ ] Code follows project standards
- [ ] Documentation is updated if needed
- [ ] Changes are backward compatible (unless breaking change is justified)
- [ ] Commit messages are clear and descriptive

## Issue Reporting

### Bug Reports

When reporting bugs, please include:

- **Environment**: OS, .NET version, etc.
- **Steps to reproduce**: Clear, step-by-step instructions
- **Expected behavior**: What should happen
- **Actual behavior**: What actually happened
- **Error messages**: Any error messages or logs
- **Additional context**: Screenshots, configuration details, etc.

### Feature Requests

When requesting features, please include:

- **Problem**: What problem does this solve?
- **Proposed solution**: How would you like it to work?
- **Alternatives**: Have you considered other approaches?
- **Use case**: How would you use this feature?

### Issue Labels

We use labels to categorize issues:

- `bug`: Something isn't working
- `enhancement`: New feature or improvement
- `documentation`: Documentation changes
- `good first issue`: Good for newcomers
- `help wanted`: Extra attention is needed

## Getting Help

If you need help:

1. Check the [documentation](docs/)
2. Search existing [issues](https://github.com/r3e-network/neo-price-feed/issues)
3. Create a new issue with the `question` label
4. Join our community discussions

## Recognition

Contributors will be recognized in:

- The project's contributor list
- Release notes for significant contributions
- Special recognition for outstanding contributions

Thank you for contributing to Neo Price Feed!