using Microsoft.Extensions.Options;
using PriceFeed.Core.Options;
using PriceFeed.Core.Validation;
using Xunit;

namespace PriceFeed.Tests.Core.Validation;

public class BatchProcessingOptionsValidatorTests
{
    private readonly BatchProcessingOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidHttpsEndpoint_ReturnsSuccess()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithHttpEndpoint_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "http://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("RPC endpoint must use HTTPS for production", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithLocalhost_AllowsHttp()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "http://localhost:10332",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithEmptyRpcEndpoint_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("RPC endpoint must be configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithInvalidContractHash_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "invalid-hash",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("Contract script hash must start with '0x'", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithInvalidBatchSize_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 0
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("Max batch size must be greater than 0", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithBatchSizeOver100_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NQPBWiGruFDpRrGNMDuGHLa7HkSZAZDk9x",
            TeeAccountPrivateKey = "L1QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MasterAccountAddress = "NZMyYadiW93JrpUxj7758BDppnND4KUu6X",
            MasterAccountPrivateKey = "L2QqQJnpBwbsPGAuutuzPTac8piqvbR1HRjrY5qHup48TBCBFe4g",
            MaxBatchSize = 101
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("Max batch size should not exceed 100", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithMissingTeeAccount_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "",
            TeeAccountPrivateKey = "PrivateKey1",
            MasterAccountAddress = "NeoAddress2",
            MasterAccountPrivateKey = "PrivateKey2",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("TEE account address must be configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithMissingMasterAccount_ReturnsFail()
    {
        var options = new BatchProcessingOptions
        {
            RpcEndpoint = "https://mainnet.neo.org",
            ContractScriptHash = "0x1234567890123456789012345678901234567890",
            TeeAccountAddress = "NeoAddress1",
            TeeAccountPrivateKey = "PrivateKey1",
            MasterAccountAddress = "",
            MasterAccountPrivateKey = "PrivateKey2",
            MaxBatchSize = 50
        };

        var result = _validator.Validate("BatchProcessing", options);

        Assert.True(result.Failed);
        Assert.Contains("Master account address must be configured", result.FailureMessage);
    }
}
