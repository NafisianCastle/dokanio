using Microsoft.Extensions.Logging;
using Shared.Core.Services;

namespace Shared.Core.Events;

/// <summary>
/// Event handler for sale completed events
/// </summary>
public class SaleCompletedEventHandler : IEventHandler<SaleCompletedEvent>
{
    private readonly ILogger<SaleCompletedEventHandler> _logger;
    private readonly IInventoryService _inventoryService;
    private readonly IEventBus _eventBus;

    public SaleCompletedEventHandler(
        ILogger<SaleCompletedEventHandler> logger,
        IInventoryService inventoryService,
        IEventBus eventBus)
    {
        _logger = logger;
        _inventoryService = inventoryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SaleCompletedEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing sale completed event for sale {SaleId} in shop {ShopId}", 
                eventData.SaleId, eventData.ShopId);

            // Check for low stock after sale
            var lowStockProducts = await _inventoryService.GetLowStockProductsAsync();
            
            foreach (var product in lowStockProducts)
            {
                await _eventBus.PublishAsync(new LowStockDetectedEvent
                {
                    ProductId = product.Id,
                    ShopId = eventData.ShopId,
                    BusinessId = eventData.BusinessId,
                    ProductName = product.Name,
                    CurrentStock = 0, // Will be updated by inventory service
                    MinimumStock = 10, // Default minimum stock
                    Source = nameof(SaleCompletedEventHandler)
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling sale completed event for sale {SaleId}", eventData.SaleId);
        }
    }
}

/// <summary>
/// Event handler for inventory updated events
/// </summary>
public class InventoryUpdatedEventHandler : IEventHandler<InventoryUpdatedEvent>
{
    private readonly ILogger<InventoryUpdatedEventHandler> _logger;
    private readonly IProductService _productService;
    private readonly IEventBus _eventBus;

    public InventoryUpdatedEventHandler(
        ILogger<InventoryUpdatedEventHandler> logger,
        IProductService productService,
        IEventBus eventBus)
    {
        _logger = logger;
        _productService = productService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(InventoryUpdatedEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing inventory updated event for product {ProductId}", eventData.ProductId);

            // Check if product is approaching expiry (for pharmacy business types)
            var product = await _productService.GetProductByIdAsync(eventData.ProductId);
            if (product?.BusinessTypeAttributesJson != null && 
                product.BusinessTypeAttributesJson.Contains("ExpiryDate"))
            {
                // Parse expiry date and check if it's within warning period
                if (product.ExpiryDate.HasValue)
                {
                    var daysUntilExpiry = (product.ExpiryDate.Value - DateTime.UtcNow).Days;
                    if (daysUntilExpiry <= 30 && daysUntilExpiry > 0) // 30 days warning
                    {
                        await _eventBus.PublishAsync(new ProductExpiryWarningEvent
                        {
                            ProductId = eventData.ProductId,
                            ShopId = eventData.ShopId,
                            BusinessId = eventData.BusinessId,
                            ProductName = product.Name,
                            ExpiryDate = product.ExpiryDate.Value,
                            DaysUntilExpiry = daysUntilExpiry,
                            Quantity = eventData.NewQuantity,
                            Source = nameof(InventoryUpdatedEventHandler)
                        }, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling inventory updated event for product {ProductId}", eventData.ProductId);
        }
    }
}

/// <summary>
/// Event handler for low stock detected events
/// </summary>
public class LowStockDetectedEventHandler : IEventHandler<LowStockDetectedEvent>
{
    private readonly ILogger<LowStockDetectedEventHandler> _logger;
    private readonly IAIAnalyticsEngine _aiAnalyticsEngine;

    public LowStockDetectedEventHandler(
        ILogger<LowStockDetectedEventHandler> logger,
        IAIAnalyticsEngine aiAnalyticsEngine)
    {
        _logger = logger;
        _aiAnalyticsEngine = aiAnalyticsEngine;
    }

    public async Task HandleAsync(LowStockDetectedEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Low stock detected for product {ProductName} in shop {ShopId}. Current: {CurrentStock}, Minimum: {MinimumStock}", 
                eventData.ProductName, eventData.ShopId, eventData.CurrentStock, eventData.MinimumStock);

            // Generate AI-powered reorder recommendations
            var recommendations = await _aiAnalyticsEngine.GenerateInventoryRecommendationsAsync(eventData.ShopId);
            
            if (recommendations.ReorderSuggestions.Any(r => r.ProductId == eventData.ProductId))
            {
                var recommendation = recommendations.ReorderSuggestions.First(r => r.ProductId == eventData.ProductId);
                _logger.LogInformation("AI recommends reordering {Quantity} units of {ProductName}", 
                    recommendation.RecommendedOrderQuantity, eventData.ProductName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling low stock detected event for product {ProductId}", eventData.ProductId);
        }
    }
}

/// <summary>
/// Event handler for product expiry warning events
/// </summary>
public class ProductExpiryWarningEventHandler : IEventHandler<ProductExpiryWarningEvent>
{
    private readonly ILogger<ProductExpiryWarningEventHandler> _logger;

    public ProductExpiryWarningEventHandler(ILogger<ProductExpiryWarningEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(ProductExpiryWarningEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Product expiry warning for {ProductName} in shop {ShopId}. Expires in {DaysUntilExpiry} days ({ExpiryDate})", 
                eventData.ProductName, eventData.ShopId, eventData.DaysUntilExpiry, eventData.ExpiryDate.ToString("yyyy-MM-dd"));

            // Here you could implement additional logic like:
            // - Send notifications to shop managers
            // - Create discount recommendations for near-expiry products
            // - Update product pricing for quick sale
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling product expiry warning event for product {ProductId}", eventData.ProductId);
        }
    }
}