using System;
using Xunit;

namespace PriceFeed.Tests.TestUtilities;

/// <summary>
/// Helper class for managing tests in CI environment
/// </summary>
public static class CITestHelper
{
    /// <summary>
    /// Checks if tests are running in CI environment
    /// </summary>
    public static bool IsRunningInCI =>
        Environment.GetEnvironmentVariable("CI") == "true" ||
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
        Environment.GetEnvironmentVariable("TF_BUILD") == "True";

    /// <summary>
    /// Fact attribute that skips integration tests in CI
    /// </summary>
    public class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (IsRunningInCI)
            {
                Skip = "Integration tests are skipped in CI environment";
            }

            // Also set the timeout
            Timeout = 30000;
        }
    }
}
