using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Architecture;
using Shared.Core.Events;
using Shared.Core.Integration;
using Shared.Core.Plugins;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for the extensible architecture components
/// </summary>
public class ExtensibleArchitectureTests
{
    [Fact]
    public void AddExtensibleArchitecture_WithDefaultConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddExtensibleArchitecture();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify event bus is registered
        var eventBus = serviceProvider.GetService<IEventBus>();
        Assert.NotNull(eventBus);
        
        // Verify configuration is registered
        var config = serviceProvider.GetService<MultiTenantServiceConfiguration>();
        Assert.NotNull(config);
        Assert.True(config.EnableEventBus);
        Assert.True(config.EnablePluginSystem);
    }

    [Fact]
    public void AddExtensibleArchitecture_WithCustomConfiguration_UsesCustomSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var customConfig = new MultiTenantServiceConfiguration
        {
            EnablePluginSystem = false,
            EnableEventBus = true,
            EnableAnalyticsIntegration = true,
            EnableAIIntegration = true,
            PluginDirectory = "custom-plugins",
            AnalyticsApi = new AnalyticsApiConfiguration
            {
                BaseUrl = "https://custom-analytics.com",
                ApiKey = "test-key"
            },
            AIService = new AIServiceConfiguration
            {
                BaseUrl = "https://custom-ai.com",
                ApiKey = "ai-key"
            }
        };

        // Act
        services.AddExtensibleArchitecture(customConfig);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<MultiTenantServiceConfiguration>();
        
        Assert.False(config.EnablePluginSystem);
        Assert.True(config.EnableEventBus);
        Assert.True(config.EnableAnalyticsIntegration);
        Assert.True(config.EnableAIIntegration);
        Assert.Equal("custom-plugins", config.PluginDirectory);
        Assert.Equal("https://custom-analytics.com", config.AnalyticsApi.BaseUrl);
        Assert.Equal("https://custom-ai.com", config.AIService.BaseUrl);
    }

    [Fact]
    public async Task EventBus_PublishAndSubscribe_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExtensibleArchitecture();
        
        var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        
        var receivedEvent = false;
        var testEvent = new TestEvent { Message = "Test Message" };

        // Act
        eventBus.Subscribe<TestEvent>((evt, ct) =>
        {
            receivedEvent = true;
            Assert.Equal("Test Message", evt.Message);
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(testEvent);

        // Assert
        Assert.True(receivedEvent);
    }

    [Fact]
    public void PluginManager_RegisterAndGetPlugin_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPluginManager, PluginManager>();
        
        var serviceProvider = services.BuildServiceProvider();
        var pluginManager = serviceProvider.GetRequiredService<IPluginManager>();
        var testPlugin = new TestBusinessTypePlugin();

        // Act
        pluginManager.RegisterPluginAsync(testPlugin).Wait();
        var retrievedPlugin = pluginManager.GetPlugin(testPlugin.BusinessType);

        // Assert
        Assert.NotNull(retrievedPlugin);
        Assert.Equal(testPlugin.BusinessType, retrievedPlugin.BusinessType);
        Assert.Equal(testPlugin.DisplayName, retrievedPlugin.DisplayName);
    }

    [Fact]
    public void PluginManager_GetActiveSubscriptions_ReturnsCorrectCount()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExtensibleArchitecture();
        
        var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();

        // Act
        eventBus.Subscribe<TestEvent>((evt, ct) => Task.CompletedTask);
        eventBus.Subscribe<TestEvent>((evt, ct) => Task.CompletedTask);
        
        var subscriptions = eventBus.GetActiveSubscriptions();

        // Assert
        Assert.True(subscriptions.ContainsKey(typeof(TestEvent)));
        Assert.Equal(2, subscriptions[typeof(TestEvent)]);
    }
}

/// <summary>
/// Test event for unit testing
/// </summary>
public class TestEvent : BaseEvent
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Test plugin for unit testing
/// </summary>
public class TestBusinessTypePlugin : IBusinessTypePlugin
{
    public Shared.Core.Enums.BusinessType BusinessType => Shared.Core.Enums.BusinessType.Custom;
    public string DisplayName => "Test Business Type";
    public string Description => "Test plugin for unit testing";
    public Version Version => new(1, 0, 0);

    public Task<ValidationResult> ValidateProductAsync(Shared.Core.Entities.Product product)
    {
        return Task.FromResult(new ValidationResult { IsValid = true });
    }

    public Task<ValidationResult> ValidateSaleAsync(Shared.Core.Entities.Sale sale)
    {
        return Task.FromResult(new ValidationResult { IsValid = true });
    }

    public IEnumerable<CustomAttributeDefinition> GetCustomProductAttributes()
    {
        return new List<CustomAttributeDefinition>();
    }

    public BusinessConfigurationSchema GetConfigurationSchema()
    {
        return new BusinessConfigurationSchema();
    }

    public Task InitializeAsync(Dictionary<string, object> configuration)
    {
        return Task.CompletedTask;
    }
}