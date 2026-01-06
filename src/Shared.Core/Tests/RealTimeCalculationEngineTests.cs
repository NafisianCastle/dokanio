using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
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

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}