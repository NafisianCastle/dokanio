using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;

namespace Shared.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for discount system functionality
/// Feature: offline-first-pos, Property 24: Discount Rule Application
/// Validates: Requirements 15.1, 15.2, 15.4
/// </summary>
public class DiscountPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IDiscountService _discountService;
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;

    public DiscountPropertyTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _discountService = _serviceProvider.GetRequiredService<IDiscountService>();
        _productService = _serviceProvider.GetRequiredService<IProductService>();
        _saleService = _serviceProvider.GetRequiredService<ISaleService>();
        
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 24: Discount Rule Application
    /// For any active discount rule and qualifying sale conditions (time, quantity, amount, membership), 
    /// the discount should be applied correctly according to the rule type (percentage or fixed amount)
    /// Validates: Requirements 15.1, 15.2, 15.4
    /// </summary>
    [Property]
    public bool DiscountRuleApplicationProperty(PositiveInt discountValueCents, PositiveInt saleAmountCents, PositiveInt quantityValue)
    {
        // Feature: offline-first-pos, Property 24: Discount Rule Application
        // **Validates: Requirements 15.1, 15.2, 15.4**
        
        var discountValue = Math.Round(discountValueCents.Get / 100.0m, 2);
        var saleAmount = Math.Round(saleAmountCents.Get / 100.0m, 2);
        var quantity = Math.Max(1, quantityValue.Get % 10); // Limit quantity to reasonable range
        
        // Ensure percentage discounts don't exceed 100%
        if (discountValue > 100) discountValue = 100;
        
        // Create a test product
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = $"TEST{Guid.NewGuid():N}",
            UnitPrice = Math.Round(saleAmount / quantity, 2),
            Category = "TestCategory",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        _context.Products.Add(product);
        _context.SaveChanges();
        
        // Test both percentage and fixed amount discounts
        var percentageDiscount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Test Percentage Discount",
            Type = DiscountType.Percentage,
            Value = discountValue,
            Scope = DiscountScope.Product,
            ProductId = product.Id,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        var fixedDiscount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Test Fixed Discount",
            Type = DiscountType.FixedAmount,
            Value = Math.Min(discountValue, saleAmount), // Don't exceed sale amount
            Scope = DiscountScope.Product,
            ProductId = product.Id,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        _context.Discounts.Add(percentageDiscount);
        _context.Discounts.Add(fixedDiscount);
        _context.SaveChanges();
        
        // Test percentage discount calculation
        var percentageDiscountAmount = _discountService.CalculateDiscountAmountAsync(percentageDiscount, saleAmount, quantity).Result;
        var expectedPercentageAmount = Math.Round(saleAmount * discountValue / 100, 2);
        var actualPercentageAmount = Math.Min(expectedPercentageAmount, saleAmount);
        
        // Test fixed amount discount calculation
        var fixedDiscountAmount = _discountService.CalculateDiscountAmountAsync(fixedDiscount, saleAmount, quantity).Result;
        var expectedFixedAmount = Math.Min(fixedDiscount.Value * quantity, saleAmount);
        
        // Clean up
        _context.Products.Remove(product);
        _context.Discounts.Remove(percentageDiscount);
        _context.Discounts.Remove(fixedDiscount);
        _context.SaveChanges();
        
        // Verify discount calculations
        return Math.Abs(percentageDiscountAmount - actualPercentageAmount) < 0.01m &&
               Math.Abs(fixedDiscountAmount - expectedFixedAmount) < 0.01m &&
               percentageDiscountAmount >= 0 &&
               fixedDiscountAmount >= 0 &&
               percentageDiscountAmount <= saleAmount &&
               fixedDiscountAmount <= saleAmount;
    }

    /// <summary>
    /// Property: Time-based discount activation
    /// For any discount with time restrictions, the discount should only be active during the specified time range
    /// </summary>
    [Property]
    public bool TimeBasedDiscountActivationProperty(PositiveInt hourValue, PositiveInt minuteValue)
    {
        var hour = hourValue.Get % 24;
        var minute = minuteValue.Get % 60;
        var testTime = new TimeSpan(hour, minute, 0);
        
        // Create a discount active only during specific hours (e.g., 9 AM to 5 PM)
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Time-based Discount",
            Type = DiscountType.Percentage,
            Value = 10,
            Scope = DiscountScope.Sale,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            StartTime = new TimeSpan(9, 0, 0), // 9 AM
            EndTime = new TimeSpan(17, 0, 0),  // 5 PM
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        _context.Discounts.Add(discount);
        _context.SaveChanges();
        
        var isActive = _discountService.IsDiscountActiveAsync(discount, DateTime.UtcNow.Date.Add(testTime)).Result;
        var shouldBeActive = testTime >= discount.StartTime && testTime <= discount.EndTime;
        
        // Clean up
        _context.Discounts.Remove(discount);
        _context.SaveChanges();
        
        return isActive == shouldBeActive;
    }

    /// <summary>
    /// Property: Quantity-based discount qualification
    /// For any discount with minimum quantity requirements, the discount should only apply when quantity meets the requirement
    /// </summary>
    [Property]
    public bool QuantityBasedDiscountQualificationProperty(PositiveInt quantityValue, PositiveInt minQuantityValue)
    {
        var quantity = Math.Max(1, quantityValue.Get % 20); // Limit to reasonable range
        var minQuantity = Math.Max(1, minQuantityValue.Get % 10);
        
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = $"TEST{Guid.NewGuid():N}",
            UnitPrice = 10.00m,
            Category = "TestCategory",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        _context.Products.Add(product);
        _context.SaveChanges();
        
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Quantity-based Discount",
            Type = DiscountType.Percentage,
            Value = 15,
            Scope = DiscountScope.Product,
            ProductId = product.Id,
            MinimumQuantity = minQuantity,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        _context.Discounts.Add(discount);
        _context.SaveChanges();
        
        // Create a sale with the test quantity
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV{Guid.NewGuid():N}",
            TotalAmount = product.UnitPrice * quantity,
            PaymentMethod = PaymentMethod.Cash,
            DeviceId = Guid.NewGuid(),
            Items = new List<SaleItem>
            {
                new SaleItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    Quantity = quantity,
                    UnitPrice = product.UnitPrice,
                    TotalPrice = product.UnitPrice * quantity
                }
            }
        };
        
        var discountResult = _discountService.CalculateDiscountsAsync(sale, null).Result;
        var shouldHaveDiscount = quantity >= minQuantity;
        var hasDiscount = discountResult.TotalDiscountAmount > 0;
        
        // Clean up
        _context.Products.Remove(product);
        _context.Discounts.Remove(discount);
        _context.SaveChanges();
        
        return hasDiscount == shouldHaveDiscount;
    }

    /// <summary>
    /// Property: Discount amount never exceeds sale amount
    /// For any discount calculation, the total discount amount should never exceed the original sale amount
    /// </summary>
    [Property]
    public bool DiscountNeverExceedsSaleAmountProperty(PositiveInt saleAmountCents, PositiveInt discountValueCents)
    {
        var saleAmount = Math.Round(saleAmountCents.Get / 100.0m, 2);
        var discountValue = Math.Round(discountValueCents.Get / 100.0m, 2);
        
        // Test with a very high percentage discount (should be capped at sale amount)
        var highPercentageDiscount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "High Percentage Discount",
            Type = DiscountType.Percentage,
            Value = Math.Max(discountValue, 150), // Ensure it's high
            Scope = DiscountScope.Sale,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        // Test with a very high fixed amount discount (should be capped at sale amount)
        var highFixedDiscount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "High Fixed Discount",
            Type = DiscountType.FixedAmount,
            Value = saleAmount + discountValue, // Ensure it exceeds sale amount
            Scope = DiscountScope.Sale,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        var percentageDiscountAmount = _discountService.CalculateDiscountAmountAsync(highPercentageDiscount, saleAmount).Result;
        var fixedDiscountAmount = _discountService.CalculateDiscountAmountAsync(highFixedDiscount, saleAmount).Result;
        
        return percentageDiscountAmount <= saleAmount &&
               fixedDiscountAmount <= saleAmount &&
               percentageDiscountAmount >= 0 &&
               fixedDiscountAmount >= 0;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}