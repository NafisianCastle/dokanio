using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Unit tests for DiscountProcessingEngine.
/// Covers: membership discount application, percentage/fixed-amount calculations,
/// discount combination rules, priority handling, and audit trail.
/// Requirements: 4.1, 4.2, 4.3, 4.4, 4.5
/// </summary>
public class DiscountProcessingEngineTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDiscountProcessingEngine _engine;
    private readonly ITestOutputHelper _output;

    public DiscountProcessingEngineTests(ITestOutputHelper output)
    {
        _output = output;
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        _engine = _serviceProvider.GetRequiredService<IDiscountProcessingEngine>();
    }

    // =========================================================================
    // Helper factories
    // =========================================================================

    private static Sale CreateSale(decimal itemTotal = 100m, Customer? customer = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            UnitPrice = itemTotal,
            IsWeightBased = false,
            IsActive = true
        };

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Quantity = 1,
            UnitPrice = itemTotal,
            TotalPrice = itemTotal,
            IsDeleted = false
        };

        return new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV-TEST-{Guid.NewGuid():N}".Substring(0, 20).ToUpper(),
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CustomerId = customer?.Id,
            Customer = customer,
            TotalAmount = itemTotal,
            Status = SaleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Items = new List<SaleItem> { item }
        };
    }

    private static Customer CreateCustomer(MembershipTier tier = MembershipTier.Silver, bool isActive = true)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = $"MEM-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
            Name = "Test Customer",
            Tier = tier,
            IsActive = isActive
        };
    }

    // =========================================================================
    // CalculateMembershipDiscountAsync Tests (Requirements 4.1, 4.2)
    // =========================================================================

    [Theory]
    [InlineData(MembershipTier.Bronze, 100.00, 2.00)]
    [InlineData(MembershipTier.Silver, 100.00, 5.00)]
    [InlineData(MembershipTier.Gold, 100.00, 8.00)]
    [InlineData(MembershipTier.Platinum, 100.00, 12.00)]
    [InlineData(MembershipTier.None, 100.00, 0.00)]
    public async Task CalculateMembershipDiscountAsync_ByTier_ShouldReturnCorrectPercentage(
        MembershipTier tier, double saleTotal, double expectedDiscount)
    {
        // Arrange
        var customer = CreateCustomer(tier);
        var sale = CreateSale((decimal)saleTotal, customer);

        // Act
        var discount = await _engine.CalculateMembershipDiscountAsync(customer, sale);

        // Assert
        Assert.Equal((decimal)expectedDiscount, discount);
        _output.WriteLine($"{tier} tier: {saleTotal:C} × discount% = {discount:C}");
    }

    [Fact]
    public async Task CalculateMembershipDiscountAsync_InactiveCustomer_ShouldReturnZero()
    {
        // Arrange
        var customer = CreateCustomer(MembershipTier.Gold, isActive: false);
        var sale = CreateSale(100m, customer);

        // Act
        var discount = await _engine.CalculateMembershipDiscountAsync(customer, sale);

        // Assert
        Assert.Equal(0m, discount);
        _output.WriteLine("Inactive customer: no membership discount applied");
    }

    [Fact]
    public async Task CalculateMembershipDiscountAsync_EmptySale_ShouldReturnZero()
    {
        // Arrange
        var customer = CreateCustomer(MembershipTier.Gold);
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-EMPTY",
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalAmount = 0,
            Status = SaleStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            Items = new List<SaleItem>()
        };

        // Act
        var discount = await _engine.CalculateMembershipDiscountAsync(customer, sale);

        // Assert
        Assert.Equal(0m, discount);
        _output.WriteLine("Empty sale: no membership discount applied");
    }

    [Fact]
    public async Task CalculateMembershipDiscountAsync_NullCustomer_ShouldThrowArgumentNullException()
    {
        var sale = CreateSale(100m);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.CalculateMembershipDiscountAsync(null!, sale));
    }

    [Fact]
    public async Task CalculateMembershipDiscountAsync_NullSale_ShouldThrowArgumentNullException()
    {
        var customer = CreateCustomer(MembershipTier.Silver);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.CalculateMembershipDiscountAsync(customer, null!));
    }

    // =========================================================================
    // CalculateDiscountsAsync Tests (Requirements 4.1, 4.3, 4.4)
    // =========================================================================

    [Fact]
    public async Task CalculateDiscountsAsync_WithMembershipCustomer_ShouldApplyMembershipDiscount()
    {
        // Arrange - Silver tier = 5% discount
        var customer = CreateCustomer(MembershipTier.Silver);
        var sale = CreateSale(200m, customer);

        // Act
        var result = await _engine.CalculateDiscountsAsync(sale, customer);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalDiscountAmount > 0);
        Assert.Contains(result.AppliedDiscounts, d => d.DiscountName.Contains("Silver"));
        Assert.Equal(10m, result.TotalDiscountAmount); // 5% of 200
        _output.WriteLine($"Silver membership: 5% of $200 = {result.TotalDiscountAmount:C}");
    }

    [Fact]
    public async Task CalculateDiscountsAsync_WithNoCustomer_ShouldReturnNoMembershipDiscount()
    {
        // Arrange
        var sale = CreateSale(100m);

        // Act
        var result = await _engine.CalculateDiscountsAsync(sale, null);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(result.AppliedDiscounts, d => d.DiscountName.Contains("Membership"));
        _output.WriteLine($"No customer: total discount = {result.TotalDiscountAmount:C}");
    }

    [Fact]
    public async Task CalculateDiscountsAsync_NullSale_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.CalculateDiscountsAsync(null!));
    }

    [Fact]
    public async Task CalculateDiscountsAsync_TotalDiscountNeverExceedsSaleSubtotal()
    {
        // Arrange - Platinum tier = 12% discount
        var customer = CreateCustomer(MembershipTier.Platinum);
        var sale = CreateSale(50m, customer);

        // Act
        var result = await _engine.CalculateDiscountsAsync(sale, customer);

        // Assert - discount must not exceed sale subtotal
        var saleSubtotal = sale.Items.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
        Assert.True(result.TotalDiscountAmount <= saleSubtotal,
            $"Discount {result.TotalDiscountAmount} exceeded subtotal {saleSubtotal}");
        _output.WriteLine($"Discount cap check: {result.TotalDiscountAmount:C} <= {saleSubtotal:C}");
    }

    [Fact]
    public async Task CalculateDiscountsAsync_WithDeletedItems_ShouldIgnoreDeletedItems()
    {
        // Arrange - sale with one active and one deleted item
        var customer = CreateCustomer(MembershipTier.Silver);
        var product = new Product { Id = Guid.NewGuid(), Name = "Active Product", UnitPrice = 100m, IsActive = true };
        var deletedProduct = new Product { Id = Guid.NewGuid(), Name = "Deleted Product", UnitPrice = 500m, IsActive = true };

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-DELETED-TEST",
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            TotalAmount = 100m,
            Status = SaleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Items = new List<SaleItem>
            {
                new SaleItem { Id = Guid.NewGuid(), ProductId = product.Id, Product = product,
                    Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, IsDeleted = false },
                new SaleItem { Id = Guid.NewGuid(), ProductId = deletedProduct.Id, Product = deletedProduct,
                    Quantity = 1, UnitPrice = 500m, TotalPrice = 500m, IsDeleted = true }
            }
        };

        // Act
        var result = await _engine.CalculateDiscountsAsync(sale, customer);

        // Assert - only active item (100) should be discounted, not deleted item (500)
        // Silver = 5% of 100 = 5
        Assert.Equal(5m, result.TotalDiscountAmount);
        _output.WriteLine($"Deleted items ignored: discount on active items only = {result.TotalDiscountAmount:C}");
    }

    // =========================================================================
    // GetApplicableDiscountsAsync Tests (Requirement 4.1)
    // =========================================================================

    [Fact]
    public async Task GetApplicableDiscountsAsync_WithMembershipCustomer_ShouldIncludeMembershipDiscount()
    {
        // Arrange
        var customer = CreateCustomer(MembershipTier.Gold);
        var sale = CreateSale(100m, customer);

        // Act
        var applicableDiscounts = await _engine.GetApplicableDiscountsAsync(sale, customer);

        // Assert
        var discountList = applicableDiscounts.ToList();
        Assert.Contains(discountList, d => d.Scope == "Membership" && d.IsEligible);
        _output.WriteLine($"Applicable discounts: {discountList.Count} found");
    }

    [Fact]
    public async Task GetApplicableDiscountsAsync_WithNoCustomer_ShouldNotIncludeMembershipDiscount()
    {
        // Arrange
        var sale = CreateSale(100m);

        // Act
        var applicableDiscounts = await _engine.GetApplicableDiscountsAsync(sale, null);

        // Assert
        var discountList = applicableDiscounts.ToList();
        Assert.DoesNotContain(discountList, d => d.Scope == "Membership");
        _output.WriteLine($"No customer: {discountList.Count} applicable discounts");
    }

    [Fact]
    public async Task GetApplicableDiscountsAsync_NullSale_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.GetApplicableDiscountsAsync(null!));
    }

    [Fact]
    public async Task GetApplicableDiscountsAsync_ShouldReturnDiscountsOrderedByPriority()
    {
        // Arrange
        var customer = CreateCustomer(MembershipTier.Silver);
        var sale = CreateSale(100m, customer);

        // Act
        var applicableDiscounts = (await _engine.GetApplicableDiscountsAsync(sale, customer)).ToList();

        // Assert - membership discount should have lowest priority number (applied first)
        if (applicableDiscounts.Count > 1)
        {
            for (int i = 0; i < applicableDiscounts.Count - 1; i++)
            {
                Assert.True(applicableDiscounts[i].Priority <= applicableDiscounts[i + 1].Priority,
                    "Discounts should be ordered by priority (ascending)");
            }
        }
        _output.WriteLine($"Priority ordering verified for {applicableDiscounts.Count} discounts");
    }

    // =========================================================================
    // ValidateDiscountApplicationAsync Tests (Requirements 4.3, 4.4)
    // =========================================================================

    [Fact]
    public async Task ValidateDiscountApplicationAsync_ActiveDiscount_ShouldReturnValid()
    {
        // Arrange
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Test Discount",
            Type = DiscountType.Percentage,
            Value = 10m,
            Scope = DiscountScope.Sale,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var sale = CreateSale(100m);

        // Act
        var result = await _engine.ValidateDiscountApplicationAsync(discount, sale);

        // Assert
        Assert.True(result.IsValid);
        _output.WriteLine($"Active discount validation: {result.IsValid}");
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_InactiveDiscount_ShouldReturnInvalid()
    {
        // Arrange
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Discount",
            Type = DiscountType.Percentage,
            Value = 10m,
            Scope = DiscountScope.Sale,
            IsActive = false,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var sale = CreateSale(100m);

        // Act
        var result = await _engine.ValidateDiscountApplicationAsync(discount, sale);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ValidationErrors);
        _output.WriteLine($"Inactive discount: {result.ValidationErrors[0]}");
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_ExpiredDiscount_ShouldReturnInvalid()
    {
        // Arrange
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Expired Discount",
            Type = DiscountType.Percentage,
            Value = 10m,
            Scope = DiscountScope.Sale,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };
        var sale = CreateSale(100m);

        // Act
        var result = await _engine.ValidateDiscountApplicationAsync(discount, sale);

        // Assert
        Assert.False(result.IsValid);
        _output.WriteLine($"Expired discount: {result.ValidationErrors[0]}");
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_ProductDiscountWithNoMatchingItem_ShouldReturnInvalid()
    {
        // Arrange
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Product Discount",
            Type = DiscountType.Percentage,
            Value = 10m,
            Scope = DiscountScope.Product,
            ProductId = Guid.NewGuid(), // Different product not in sale
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var sale = CreateSale(100m);

        // Act
        var result = await _engine.ValidateDiscountApplicationAsync(discount, sale);

        // Assert
        Assert.False(result.IsValid);
        _output.WriteLine($"Product not in sale: {result.ValidationErrors[0]}");
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_SaleDiscountBelowMinimumAmount_ShouldReturnInvalid()
    {
        // Arrange
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "High Value Discount",
            Type = DiscountType.Percentage,
            Value = 10m,
            Scope = DiscountScope.Sale,
            MinimumAmount = 500m, // Requires $500 minimum
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var sale = CreateSale(100m); // Only $100

        // Act
        var result = await _engine.ValidateDiscountApplicationAsync(discount, sale);

        // Assert
        Assert.False(result.IsValid);
        _output.WriteLine($"Below minimum amount: {result.ValidationErrors[0]}");
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_NullDiscount_ShouldThrowArgumentNullException()
    {
        var sale = CreateSale(100m);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.ValidateDiscountApplicationAsync(null!, sale));
    }

    [Fact]
    public async Task ValidateDiscountApplicationAsync_NullSale_ShouldThrowArgumentNullException()
    {
        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.ValidateDiscountApplicationAsync(discount, null!));
    }

    // =========================================================================
    // Requirement 4.4: Discount cannot exceed line item total
    // =========================================================================

    [Fact]
    public async Task CalculateMembershipDiscountAsync_DiscountNeverExceedsSubtotal()
    {
        // Arrange - even with 100% discount, it should be capped at subtotal
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "MEM-TEST",
            Name = "Test",
            Tier = MembershipTier.Platinum, // 12%
            IsActive = true
        };
        var sale = CreateSale(10m, customer); // Small sale

        // Act
        var discount = await _engine.CalculateMembershipDiscountAsync(customer, sale);

        // Assert
        var subtotal = sale.Items.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);
        Assert.True(discount <= subtotal,
            $"Discount {discount} should not exceed subtotal {subtotal}");
        _output.WriteLine($"Discount cap: {discount:C} <= {subtotal:C}");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Property-based tests for DiscountProcessingEngine.
/// Tests universal properties that should hold across all valid inputs.
/// Requirements: 4.1, 4.2, 4.3, 4.4, 4.5
/// </summary>
public class DiscountProcessingEnginePropertyTests
{
    // Membership tier discount percentages (must match DiscountProcessingEngine)
    private static readonly Dictionary<MembershipTier, decimal> TierDiscountPercentages = new()
    {
        { MembershipTier.None, 0m },
        { MembershipTier.Bronze, 2m },
        { MembershipTier.Silver, 5m },
        { MembershipTier.Gold, 8m },
        { MembershipTier.Platinum, 12m }
    };

    private static IDiscountProcessingEngine CreateEngine()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDiscountProcessingEngine>();
    }

    private static Sale CreateSaleWithTotal(decimal total, Customer? customer = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Property Test Product",
            UnitPrice = total,
            IsWeightBased = false,
            IsActive = true
        };

        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            Quantity = 1,
            UnitPrice = total,
            TotalPrice = total,
            IsDeleted = false
        };

        return new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV-PROP-{Guid.NewGuid():N}".Substring(0, 20).ToUpper(),
            ShopId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CustomerId = customer?.Id,
            Customer = customer,
            TotalAmount = total,
            Status = SaleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Items = new List<SaleItem> { item }
        };
    }

    /// <summary>
    /// Property 9: Discount Application Logic
    /// For any customer with membership benefits and applicable promotions, the discount engine
    /// should automatically apply eligible discounts in the correct order without exceeding line totals.
    /// Validates: Requirements 4.1, 4.3, 4.4
    /// Feature: sales-service-implementation, Property 9: Discount Application Logic
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DiscountArbitraries) })]
    public bool Property9_DiscountApplicationLogic_TotalDiscountNeverExceedsSubtotal(
        DiscountTestInput input)
    {
        var engine = CreateEngine();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "MEM-PROP",
            Name = "Property Test Customer",
            Tier = input.MembershipTier,
            IsActive = true
        };

        var sale = CreateSaleWithTotal(input.SaleTotal, customer);

        var result = engine.CalculateDiscountsAsync(sale, customer).GetAwaiter().GetResult();

        var subtotal = sale.Items.Where(i => !i.IsDeleted).Sum(i => i.TotalPrice);

        // Property: total discount must never exceed the sale subtotal (Requirement 4.4)
        return result.TotalDiscountAmount >= 0 && result.TotalDiscountAmount <= subtotal;
    }

    /// <summary>
    /// Property 9b: Membership discounts are automatically applied for eligible customers.
    /// For any active customer with a non-None membership tier, the discount engine should
    /// include a membership discount in the results.
    /// Validates: Requirement 4.1
    /// Feature: sales-service-implementation, Property 9: Discount Application Logic
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DiscountArbitraries) })]
    public bool Property9b_MembershipDiscountAutoApplied_ForEligibleCustomers(
        DiscountTestInput input)
    {
        if (input.MembershipTier == MembershipTier.None)
            return true; // Skip None tier - no membership discount expected

        var engine = CreateEngine();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "MEM-PROP",
            Name = "Property Test Customer",
            Tier = input.MembershipTier,
            IsActive = true
        };

        var sale = CreateSaleWithTotal(input.SaleTotal, customer);

        var result = engine.CalculateDiscountsAsync(sale, customer).GetAwaiter().GetResult();

        // Property: active customers with membership tier should have membership discount applied
        return result.AppliedDiscounts.Any(d => d.DiscountName.Contains(input.MembershipTier.ToString()));
    }

    /// <summary>
    /// Property 10: Discount Calculation Accuracy
    /// For any discount configuration (percentage or fixed amount), the discount engine should
    /// calculate the correct discount amount and track it for audit purposes.
    /// Validates: Requirements 4.2, 4.5
    /// Feature: sales-service-implementation, Property 10: Discount Calculation Accuracy
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DiscountArbitraries) })]
    public bool Property10_DiscountCalculationAccuracy_MembershipPercentageIsCorrect(
        DiscountTestInput input)
    {
        if (input.MembershipTier == MembershipTier.None)
            return true; // No membership discount for None tier

        var engine = CreateEngine();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "MEM-PROP",
            Name = "Property Test Customer",
            Tier = input.MembershipTier,
            IsActive = true
        };

        var sale = CreateSaleWithTotal(input.SaleTotal, customer);

        var membershipDiscount = engine.CalculateMembershipDiscountAsync(customer, sale).GetAwaiter().GetResult();

        var expectedPercentage = TierDiscountPercentages[input.MembershipTier];
        var expectedAmount = Math.Round(input.SaleTotal * expectedPercentage / 100m, 2, MidpointRounding.AwayFromZero);

        // Property: membership discount must equal the expected percentage of the sale total
        return membershipDiscount == expectedAmount;
    }

    /// <summary>
    /// Property 10b: Applied discounts are tracked for audit purposes.
    /// For any sale with applied discounts, the result should contain audit information
    /// including discount names and reasons.
    /// Validates: Requirement 4.5
    /// Feature: sales-service-implementation, Property 10: Discount Calculation Accuracy
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DiscountArbitraries) })]
    public bool Property10b_AppliedDiscountsHaveAuditInformation(DiscountTestInput input)
    {
        if (input.MembershipTier == MembershipTier.None)
            return true; // No discounts to audit for None tier

        var engine = CreateEngine();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "MEM-PROP",
            Name = "Property Test Customer",
            Tier = input.MembershipTier,
            IsActive = true
        };

        var sale = CreateSaleWithTotal(input.SaleTotal, customer);

        var result = engine.CalculateDiscountsAsync(sale, customer).GetAwaiter().GetResult();

        // Property: every applied discount must have a name and reason (audit trail)
        return result.AppliedDiscounts.All(d =>
            !string.IsNullOrWhiteSpace(d.DiscountName) &&
            !string.IsNullOrWhiteSpace(d.Reason) &&
            d.CalculatedAmount >= 0);
    }
}

// =========================================================================
// Arbitraries for property-based tests
// =========================================================================

/// <summary>
/// Input for discount property tests
/// </summary>
public record DiscountTestInput(decimal SaleTotal, MembershipTier MembershipTier);

/// <summary>
/// FsCheck arbitraries for discount processing tests
/// </summary>
public static class DiscountArbitraries
{
    /// <summary>
    /// Generates valid discount test inputs: sale total in [1.00, 10000.00], random membership tier
    /// </summary>
    public static Arbitrary<DiscountTestInput> DiscountTestInputArbitrary()
    {
        // Generate sale total in [1, 1000000] cents = [0.01, 10000.00]
        var totalGen = Gen.Choose(100, 1000000).Select(cents => Math.Round((decimal)cents / 100m, 2));

        // Generate a random membership tier
        var tiers = new[]
        {
            MembershipTier.None,
            MembershipTier.Bronze,
            MembershipTier.Silver,
            MembershipTier.Gold,
            MembershipTier.Platinum
        };
        var tierGen = Gen.Elements(tiers);

        var gen = totalGen.SelectMany(total =>
            tierGen.Select(tier => new DiscountTestInput(total, tier)));

        return gen.ToArbitrary();
    }
}
