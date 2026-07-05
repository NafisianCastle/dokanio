using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for the RealTimeCalculationEngine service
/// </summary>
public class RealTimeCalculationEngineTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IRealTimeCalculationEngine _calculationEngine;
    private readonly ITestOutputHelper _output;

    public RealTimeCalculationEngineTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        
        _calculationEngine = _serviceProvider.GetRequiredService<IRealTimeCalculationEngine>();
    }

    [Fact]
    public async Task CalculateLineItemAsync_WithRegularProduct_ShouldCalculateCorrectly()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            UnitPrice = 10.00m,
            IsWeightBased = false
        };

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Quantity = 2,
            UnitPrice = 10.00m,
            TotalPrice = 20.00m
        };

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.08m,
            PricingRules = new PricingRules
            {
                AllowPriceOverride = true,
                MaxDiscountPercentage = 0.20m
            }
        };

        // Act
        var result = await _calculationEngine.CalculateLineItemAsync(saleItem, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(saleItem.Id, result.SaleItemId);
        Assert.Equal(10.00m, result.BasePrice);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(10.00m, result.UnitPrice);
        Assert.Equal(20.00m, result.LineSubtotal);
        Assert.Equal(20.00m, result.LineTotal);
        Assert.Equal(0, result.DiscountAmount);
        Assert.Equal(0, result.TaxAmount);

        _output.WriteLine($"Line item calculation: {result.LineSubtotal:C} subtotal, {result.LineTotal:C} total");
    }

    [Fact]
    public async Task CalculateLineItemAsync_WithWeightBasedProduct_ShouldCalculateCorrectly()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Weight-Based Product",
            IsWeightBased = true,
            RatePerKilogram = 5.00m,
            WeightPrecision = 3
        };

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Quantity = 1,
            Weight = 2.5m,
            UnitPrice = 5.00m,
            TotalPrice = 12.50m
        };

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.08m
        };

        // Act
        var result = await _calculationEngine.CalculateLineItemAsync(saleItem, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(saleItem.Id, result.SaleItemId);
        Assert.Equal(2.5m, result.Weight);
        Assert.Equal(5.00m, result.UnitPrice);
        Assert.True(result.LineSubtotal > 0);

        _output.WriteLine($"Weight-based calculation: {result.Weight}kg × {result.UnitPrice:C}/kg = {result.LineSubtotal:C}");
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WithMultipleItems_ShouldCalculateCorrectly()
    {
        // Arrange
        var product1 = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Product 1",
            UnitPrice = 10.00m,
            IsWeightBased = false
        };

        var product2 = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Product 2",
            UnitPrice = 15.00m,
            IsWeightBased = false
        };

        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = product1.Id,
                Product = product1,
                Quantity = 2,
                UnitPrice = 10.00m,
                TotalPrice = 20.00m
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = product2.Id,
                Product = product2,
                Quantity = 1,
                UnitPrice = 15.00m,
                TotalPrice = 15.00m
            }
        };

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.08m,
            PricingRules = new PricingRules
            {
                MaxDiscountPercentage = 0.20m
            }
        };

        // Act
        var result = await _calculationEngine.CalculateOrderTotalsAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(3, result.TotalQuantity);
        Assert.Equal(35.00m, result.Subtotal);
        Assert.True(result.FinalTotal > 0);
        Assert.Equal(2, result.LineItems.Count);
        Assert.True(result.IsValid);

        _output.WriteLine($"Order calculation: Subtotal {result.Subtotal:C}, Tax {result.TotalTaxAmount:C}, Total {result.FinalTotal:C}");
    }

    [Fact]
    public async Task CalculateTaxesAsync_WithTaxableItems_ShouldCalculateCorrectly()
    {
        // Arrange
        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 2,
                UnitPrice = 10.00m,
                TotalPrice = 20.00m
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 15.00m,
                TotalPrice = 15.00m
            }
        };

        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.08m // 8% tax
        };

        // Act
        var result = await _calculationEngine.CalculateTaxesAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2.80m, result.TotalTaxAmount); // 35.00 * 0.08 = 2.80
        Assert.Single(result.AppliedTaxes);
        Assert.Equal("Sales Tax", result.AppliedTaxes[0].TaxName);
        Assert.Equal(0.08m, result.AppliedTaxes[0].TaxRate);
        Assert.Equal(35.00m, result.AppliedTaxes[0].TaxableAmount);

        _output.WriteLine($"Tax calculation: {result.AppliedTaxes[0].TaxableAmount:C} × {result.AppliedTaxes[0].TaxRate:P2} = {result.TotalTaxAmount:C}");
    }

    [Fact]
    public async Task ValidateCalculationAsync_WithValidCalculation_ShouldReturnValid()
    {
        // Arrange
        var calculation = new OrderTotalCalculation
        {
            Subtotal = 100.00m,
            TotalDiscountAmount = 10.00m,
            TotalTaxAmount = 8.00m,
            FinalTotal = 98.00m // 100 - 10 + 8 = 98
        };

        var shopConfig = new ShopConfiguration
        {
            PricingRules = new PricingRules
            {
                MaxDiscountPercentage = 0.20m // 20% max discount
            }
        };

        // Act
        var result = await _calculationEngine.ValidateCalculationAsync(calculation, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);

        _output.WriteLine($"Validation result: Valid = {result.IsValid}, Errors = {result.Errors.Count}");
    }

    [Fact]
    public async Task ValidateCalculationAsync_WithExcessiveDiscount_ShouldReturnInvalid()
    {
        // Arrange
        var calculation = new OrderTotalCalculation
        {
            Subtotal = 100.00m,
            TotalDiscountAmount = 150.00m, // More than subtotal
            TotalTaxAmount = 8.00m,
            FinalTotal = -42.00m // Invalid negative total
        };

        var shopConfig = new ShopConfiguration
        {
            PricingRules = new PricingRules
            {
                MaxDiscountPercentage = 0.20m
            }
        };

        // Act
        var result = await _calculationEngine.ValidateCalculationAsync(calculation, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Code == "NEGATIVE_TOTAL");
        Assert.Contains(result.Errors, e => e.Code == "EXCESSIVE_DISCOUNT");

        _output.WriteLine($"Validation result: Valid = {result.IsValid}, Errors = {string.Join(", ", result.Errors.Select(e => e.Code))}");
    }

    [Fact]
    public async Task RecalculateOnItemChangeAsync_WhenItemModified_ShouldRecalculateOrder()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            UnitPrice = 10.00m,
            IsWeightBased = false
        };

        var modifiedItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Quantity = 3, // Changed from 2 to 3
            UnitPrice = 10.00m,
            TotalPrice = 30.00m
        };

        var allItems = new List<SaleItem>
        {
            modifiedItem,
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 15.00m,
                TotalPrice = 15.00m
            }
        };

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.08m
        };

        // Act
        var result = await _calculationEngine.RecalculateOnItemChangeAsync(modifiedItem, allItems, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(4, result.TotalQuantity); // 3 + 1
        Assert.Equal(45.00m, result.Subtotal); // 30 + 15
        Assert.True(result.IsValid);

        _output.WriteLine($"Recalculation result: Subtotal {result.Subtotal:C}, Total {result.FinalTotal:C}");
    }

    [Fact]
    public async Task CalculateTaxesAsync_WithCategoryBasedTaxRates_ShouldApplyCorrectRates()
    {
        // Arrange - two items in different categories with different tax rates
        var foodItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 10.00m,
            TotalPrice = 10.00m,
            Product = new Product { Name = "Apple", Category = "Food", UnitPrice = 10.00m }
        };

        var electronicsItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 100.00m,
            TotalPrice = 100.00m,
            Product = new Product { Name = "Headphones", Category = "Electronics", UnitPrice = 100.00m }
        };

        var items = new List<SaleItem> { foodItem, electronicsItem };

        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.05m, // Default 5% tax
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Food", 0.00m },         // Food is tax-exempt
                { "Electronics", 0.10m }   // Electronics taxed at 10%
            }
        };

        // Act
        var result = await _calculationEngine.CalculateTaxesAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        // Food: 0% tax = $0.00
        // Electronics: 10% of $100 = $10.00
        Assert.Equal(10.00m, result.TotalTaxAmount);
        // Only one tax group (Electronics) since Food is 0%
        Assert.Single(result.AppliedTaxes);
        Assert.Equal("Electronics Tax", result.AppliedTaxes[0].TaxName);
        Assert.Equal(0.10m, result.AppliedTaxes[0].TaxRate);
        Assert.Equal(100.00m, result.AppliedTaxes[0].TaxableAmount);
        Assert.Equal(10.00m, result.AppliedTaxes[0].TaxAmount);

        _output.WriteLine($"Category tax: Food=$0.00 (exempt), Electronics=$10.00 (10%), Total={result.TotalTaxAmount:C}");
    }

    [Fact]
    public async Task CalculateTaxesAsync_WithDefaultRateForUncategorizedItems_ShouldApplyDefaultRate()
    {
        // Arrange - item with no category should use default tax rate
        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 50.00m,
                TotalPrice = 50.00m,
                Product = new Product { Name = "Misc Item", Category = null, UnitPrice = 50.00m }
            }
        };

        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.08m, // Default 8% tax
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Food", 0.00m } // Only Food is configured; other categories use default
            }
        };

        // Act
        var result = await _calculationEngine.CalculateTaxesAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        // Uncategorized item: 8% of $50 = $4.00
        Assert.Equal(4.00m, result.TotalTaxAmount);
        Assert.Single(result.AppliedTaxes);
        Assert.Equal("Sales Tax", result.AppliedTaxes[0].TaxName);
        Assert.Equal(0.08m, result.AppliedTaxes[0].TaxRate);

        _output.WriteLine($"Default rate tax: {result.TotalTaxAmount:C} on uncategorized item");
    }

    [Fact]
    public async Task CalculateTaxesAsync_WithTaxIncludedInPrice_ShouldReturnZeroTax()
    {
        // Arrange - tax-inclusive pricing means no additional tax
        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 2,
                UnitPrice = 10.00m,
                TotalPrice = 20.00m
            }
        };

        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.10m,
            TaxIncludedInPrice = true // Tax already in price
        };

        // Act
        var result = await _calculationEngine.CalculateTaxesAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result.TotalTaxAmount);
        Assert.Empty(result.AppliedTaxes);
        Assert.True(result.TaxIncludedInPrice);

        _output.WriteLine("Tax-inclusive pricing: no additional tax calculated");
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WithCategoryTaxRates_ShouldProduceCorrectBreakdown()
    {
        // Arrange - mixed categories with different tax rates
        var foodProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Bread",
            Category = "Food",
            UnitPrice = 3.00m,
            IsWeightBased = false
        };

        var beverageProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Soda",
            Category = "Beverages",
            UnitPrice = 2.00m,
            IsWeightBased = false
        };

        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = foodProduct.Id,
                Product = foodProduct,
                Quantity = 2,
                UnitPrice = 3.00m,
                TotalPrice = 6.00m
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                ProductId = beverageProduct.Id,
                Product = beverageProduct,
                Quantity = 3,
                UnitPrice = 2.00m,
                TotalPrice = 6.00m
            }
        };

        var shopConfig = new ShopConfiguration
        {
            Currency = "USD",
            TaxRate = 0.05m, // Default 5%
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Food", 0.00m },      // Food exempt
                { "Beverages", 0.08m }  // Beverages at 8%
            }
        };

        // Act
        var result = await _calculationEngine.CalculateOrderTotalsAsync(items, shopConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12.00m, result.Subtotal); // 6 + 6
        // Food: 0% tax = $0.00
        // Beverages: 8% of $6 = $0.48
        Assert.Equal(0.48m, result.TotalTaxAmount);
        Assert.Equal(12.48m, result.FinalTotal); // 12 + 0.48
        Assert.True(result.IsValid);

        // Verify breakdown is populated
        Assert.NotNull(result.Breakdown);
        Assert.NotEmpty(result.Breakdown.Items);
        Assert.NotEmpty(result.Breakdown.CalculationSteps);
        Assert.True(result.Breakdown.Totals.ContainsKey("Subtotal"));
        Assert.True(result.Breakdown.Totals.ContainsKey("Tax"));
        Assert.True(result.Breakdown.Totals.ContainsKey("Final"));

        _output.WriteLine($"Mixed category order: Subtotal={result.Subtotal:C}, Tax={result.TotalTaxAmount:C}, Total={result.FinalTotal:C}");
        _output.WriteLine($"Breakdown steps: {string.Join(" | ", result.Breakdown.CalculationSteps)}");
    }

    [Fact]
    public async Task CalculateOrderTotalsAsync_WithMultipleTaxCategories_ShouldGroupByCategory()
    {
        // Arrange - three different categories
        var items = new List<SaleItem>
        {
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 100.00m,
                TotalPrice = 100.00m,
                Product = new Product { Name = "Laptop", Category = "Electronics", UnitPrice = 100.00m }
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 50.00m,
                TotalPrice = 50.00m,
                Product = new Product { Name = "Shirt", Category = "Clothing", UnitPrice = 50.00m }
            },
            new SaleItem
            {
                Id = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 20.00m,
                TotalPrice = 20.00m,
                Product = new Product { Name = "Apple", Category = "Food", UnitPrice = 20.00m }
            }
        };

        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.05m,
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Electronics", 0.10m }, // 10%
                { "Clothing", 0.07m },    // 7%
                { "Food", 0.00m }         // Exempt
            }
        };

        // Act
        var taxResult = await _calculationEngine.CalculateTaxesAsync(items, shopConfig);

        // Assert
        // Electronics: 10% of $100 = $10.00
        // Clothing: 7% of $50 = $3.50
        // Food: 0% = $0.00
        Assert.Equal(13.50m, taxResult.TotalTaxAmount);
        Assert.Equal(2, taxResult.AppliedTaxes.Count); // Electronics + Clothing (Food is 0%)
        Assert.Equal(2, taxResult.TaxBreakdowns.Count);

        _output.WriteLine($"Multi-category tax: Electronics=$10.00, Clothing=$3.50, Food=$0.00, Total={taxResult.TotalTaxAmount:C}");
    }

    [Fact]
    public async Task ShopConfiguration_GetTaxRateForCategory_ShouldReturnCorrectRate()
    {
        // Arrange
        var shopConfig = new ShopConfiguration
        {
            TaxRate = 0.05m, // Default
            CategoryTaxRates = new Dictionary<string, decimal>
            {
                { "Food", 0.00m },
                { "Electronics", 0.10m }
            }
        };

        // Act & Assert
        Assert.Equal(0.00m, shopConfig.GetTaxRateForCategory("Food"));
        Assert.Equal(0.10m, shopConfig.GetTaxRateForCategory("Electronics"));
        Assert.Equal(0.05m, shopConfig.GetTaxRateForCategory("Clothing")); // Falls back to default
        Assert.Equal(0.05m, shopConfig.GetTaxRateForCategory(null));       // Null falls back to default
        Assert.Equal(0.05m, shopConfig.GetTaxRateForCategory(""));         // Empty falls back to default

        _output.WriteLine("Category tax rate lookup verified");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}