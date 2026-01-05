using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for the complete multi-business POS system
/// </summary>
public class SystemIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ISystemIntegrationService _integrationService;

    public SystemIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedCoreInMemory();
        
        _serviceProvider = services.BuildServiceProvider();
        _integrationService = _serviceProvider.GetRequiredService<ISystemIntegrationService>();
    }

    [Fact]
    public async Task ValidateSystemIntegration_ShouldSucceed()
    {
        // Act
        var result = await _integrationService.ValidateSystemIntegrationAsync();

        // Assert
        Assert.True(result.IsSuccess, $"System integration validation failed. Failed components: {string.Join(", ", result.FailedComponents)}");
        Assert.NotEmpty(result.ValidatedComponents);
    }

    [Fact]
    public async Task TestBusinessCreationWorkflow_ShouldCreateCompleteBusinessStructure()
    {
        // Arrange
        var request = new BusinessCreationTestRequest
        {
            BusinessName = "Test Grocery Store",
            BusinessType = BusinessType.Grocery,
            OwnerUsername = "grocery_owner",
            NumberOfShops = 3,
            TestWithCustomAttributes = true
        };

        // Act
        var result = await _integrationService.TestBusinessCreationWorkflowAsync(request);

        // Assert
        Assert.True(result.IsSuccess, $"Business creation workflow failed. Errors: {string.Join(", ", result.Errors)}");
        Assert.Contains("Creating business owner user", result.CompletedSteps);
        Assert.Contains("Creating business", result.CompletedSteps);
        Assert.Contains("Creating 3 shops", result.CompletedSteps);
        
        var businessResult = result as BusinessCreationWorkflowResult;
        Assert.NotNull(businessResult);
        Assert.NotEqual(Guid.Empty, businessResult.CreatedBusinessId);
        Assert.Equal(3, businessResult.CreatedShopIds.Count);
    }

    [Fact]
    public async Task PerformSystemHealthCheck_ShouldReportHealthySystem()
    {
        // Act
        var result = await _integrationService.PerformSystemHealthCheckAsync();

        // Assert
        Assert.True(result.IsHealthy, $"System health check failed. Errors: {string.Join(", ", result.Errors)}");
        Assert.NotEmpty(result.ComponentHealths);
    }

    [Fact]
    public async Task ValidateCrossPlatformCompatibility_ShouldSupportAllPlatforms()
    {
        // Act
        var result = await _integrationService.ValidateCrossPlatformCompatibilityAsync();

        // Assert
        Assert.True(result.IsSuccess, $"Cross-platform validation failed. Issues: {string.Join(", ", result.PlatformSpecificIssues.SelectMany(kvp => kvp.Value))}");
        Assert.NotEmpty(result.SupportedPlatforms);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}