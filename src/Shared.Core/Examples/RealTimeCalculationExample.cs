using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Shared.Core.Examples;

/// <summary>
/// Example demonstrating how to use the RealTimeCalculationEngine for immediate sales calculations
/// </summary>
public class RealTimeCalculationExample
{
    private readonly IRealTimeCalculationEngine _calculationEngine;
    private readonly ILogger<RealTimeCalculationExample> _logger;

    public RealTimeCalculationExample(IRealTimeCalculationEngine calculationEngine, ILogger<RealTimeCalculationExample> logger)
    {
        _calculationEngine = calculationEngine;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates a complete real-time calculation workflow for a multi-item sale
    /// </summary>
    public async Task<OrderTotalCalculation> DemoCompleteCalculationWorkflowAsync()
    {
        _logger.LogInformation("Starting real-time calculation demo");

        // Setup shop configuration
        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.08m, // 8% sales tax
            PricingRules = new PricingRules
            {
                AllowPriceOverride = true,
                MaxDiscountPercentage = 0.20m, // 20% max discount
                EnableTieredPricing = true,
                EnableDynamicPricing = false
            }
        };

        // Setup customer with membership
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            MembershipNumber = "GOLD001",
            Tier = MembershipTier.Gold,
            TotalSpent = 1500.00m
        };

        // Create sample products
        var regularProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Premium Coffee Beans",
            UnitPrice = 12.99m,
            Category = "Beverages",
            IsWeightBased = false
        };

        var weightBasedProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Fresh Salmon",
            Category = "Seafood",
            IsWeightBased = true,
            RatePerKilogram = 24.99m,
            WeightPrecision = 3
        };

        // Create sale items
        var saleItems = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = regularProduct.Id,
                Product = regularProduct,
                Quantity = 2,
                UnitPrice = regularProduct.UnitPrice,
                TotalPrice = regularProduct.UnitPrice * 2
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = weightBasedProduct.Id,
                Product = weightBasedProduct,
                Quantity = 1,
                Weight = 1.250m, // 1.25 kg
                RatePerKilogram = weightBasedProduct.RatePerKilogram,
                TotalPrice = 1.250m * weightBasedProduct.RatePerKilogram.Value
            }
        };

        _logger.LogInformation("Calculating individual line items");

        // Calculate each line item
        foreach (var item in saleItems)
        {
            var lineResult = await _calculationEngine.CalculateLineItemAsync(item, shopConfig);
            _logger.LogInformation("Line item {ProductName}: {Quantity} × {UnitPrice:C} = {LineTotal:C}",
                item.Product?.Name, item.Quantity, lineResult.UnitPrice, lineResult.LineTotal);
        }

        _logger.LogInformation("Calculating complete order totals");

        // Calculate complete order
        var orderCalculation = await _calculationEngine.CalculateOrderTotalsAsync(saleItems, shopConfig, customer);

        // Log detailed results
        _logger.LogInformation("Order Summary:");
        _logger.LogInformation("  Items: {ItemCount}", orderCalculation.TotalItems);
        _logger.LogInformation("  Quantity: {TotalQuantity}", orderCalculation.TotalQuantity);
        _logger.LogInformation("  Subtotal: {Subtotal:C}", orderCalculation.Subtotal);
        _logger.LogInformation("  Discounts: -{DiscountAmount:C}", orderCalculation.TotalDiscountAmount);
        _logger.LogInformation("  Tax: +{TaxAmount:C}", orderCalculation.TotalTaxAmount);
        _logger.LogInformation("  Final Total: {FinalTotal:C}", orderCalculation.FinalTotal);

        if (orderCalculation.AppliedPricingRules.Any())
        {
            _logger.LogInformation("Applied Pricing Rules:");
            foreach (var rule in orderCalculation.AppliedPricingRules)
            {
                _logger.LogInformation("  - {RuleName}: {AdjustmentAmount:C} ({Description})",
                    rule.RuleName, rule.AdjustmentAmount, rule.Description);
            }
        }

        // Validate the calculation
        var validation = await _calculationEngine.ValidateCalculationAsync(orderCalculation, shopConfig);
        if (validation.IsValid)
        {
            _logger.LogInformation("✓ Calculation validation passed");
        }
        else
        {
            _logger.LogWarning("⚠ Calculation validation failed:");
            foreach (var error in validation.Errors)
            {
                _logger.LogWarning("  - {ErrorCode}: {ErrorMessage}", error.Code, error.Message);
            }
        }

        return orderCalculation;
    }

    /// <summary>
    /// Demonstrates real-time recalculation when an item is modified
    /// </summary>
    public async Task<OrderTotalCalculation> DemoItemModificationRecalculationAsync()
    {
        _logger.LogInformation("Starting item modification recalculation demo");

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.10m
        };

        // Initial items
        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 10.00m,
                TotalPrice = 10.00m,
                Product = new Product { Name = "Item A", UnitPrice = 10.00m }
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 2,
                UnitPrice = 15.00m,
                TotalPrice = 30.00m,
                Product = new Product { Name = "Item B", UnitPrice = 15.00m }
            }
        };

        // Calculate initial order
        var initialCalculation = await _calculationEngine.CalculateOrderTotalsAsync(items, shopConfig);
        _logger.LogInformation("Initial order total: {InitialTotal:C}", initialCalculation.FinalTotal);

        // Modify the first item (increase quantity)
        var modifiedItem = items[0];
        modifiedItem.Quantity = 3; // Changed from 1 to 3
        modifiedItem.TotalPrice = modifiedItem.Quantity * modifiedItem.UnitPrice;

        _logger.LogInformation("Modified {ProductName} quantity from 1 to {NewQuantity}",
            modifiedItem.Product?.Name, modifiedItem.Quantity);

        // Recalculate with the modified item
        var recalculatedOrder = await _calculationEngine.RecalculateOnItemChangeAsync(modifiedItem, items, shopConfig);

        _logger.LogInformation("Recalculated order total: {RecalculatedTotal:C}", recalculatedOrder.FinalTotal);
        _logger.LogInformation("Total change: {TotalChange:C}",
            recalculatedOrder.FinalTotal - initialCalculation.FinalTotal);

        return recalculatedOrder;
    }

    /// <summary>
    /// Demonstrates weight-based pricing calculations
    /// </summary>
    public async Task DemoWeightBasedPricingAsync()
    {
        _logger.LogInformation("Starting weight-based pricing demo");

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            PricingRules = new PricingRules
            {
                EnableDynamicPricing = false
            }
        };

        var weightBasedProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Premium Beef",
            IsWeightBased = true,
            RatePerKilogram = 32.99m,
            WeightPrecision = 3
        };

        // Test different weights
        var weights = new[] { 0.5m, 1.0m, 1.25m, 2.75m };

        foreach (var weight in weights)
        {
            var pricingResult = await _calculationEngine.CalculateWeightBasedPricingAsync(
                weightBasedProduct, weight, shopConfig);

            _logger.LogInformation("Weight: {Weight}kg × Rate: {Rate:C}/kg = Price: {Price:C}",
                pricingResult.Weight, pricingResult.RatePerKilogram, pricingResult.AdjustedPrice);
        }
    }

    /// <summary>
    /// Static method to run the complete demo
    /// </summary>
    public static async Task RunDemoAsync()
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));

        using var serviceProvider = services.BuildServiceProvider();
        var example = new RealTimeCalculationExample(
            serviceProvider.GetRequiredService<IRealTimeCalculationEngine>(),
            serviceProvider.GetRequiredService<ILogger<RealTimeCalculationExample>>());

        Console.WriteLine("=== Real-Time Calculation Engine Demo ===\n");

        // Run complete workflow demo
        Console.WriteLine("1. Complete Calculation Workflow:");
        await example.DemoCompleteCalculationWorkflowAsync();

        Console.WriteLine("\n2. Item Modification Recalculation:");
        await example.DemoItemModificationRecalculationAsync();

        Console.WriteLine("\n3. Weight-Based Pricing:");
        await example.DemoWeightBasedPricingAsync();

        Console.WriteLine("\n=== Demo Complete ===");
    }
}