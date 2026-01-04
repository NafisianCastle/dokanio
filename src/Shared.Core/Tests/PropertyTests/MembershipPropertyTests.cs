using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for membership system functionality
/// Feature: offline-first-pos, Property 23: Membership Discount Application
/// Validates: Requirements 14.3, 14.4
/// </summary>
public class MembershipPropertyTests : IDisposable
{
    private readonly PosDbContext _context;
    private readonly CustomerRepository _customerRepository;
    private readonly SaleRepository _saleRepository;
    private readonly MembershipService _membershipService;
    private readonly ILogger<CustomerRepository> _customerLogger;
    private readonly ILogger<SaleRepository> _saleLogger;
    private readonly ILogger<MembershipService> _membershipLogger;
    private readonly ICurrentUserService _currentUserService;

    public MembershipPropertyTests()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PosDbContext(options);
        
        // Create loggers
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _customerLogger = loggerFactory.CreateLogger<CustomerRepository>();
        _saleLogger = loggerFactory.CreateLogger<SaleRepository>();
        _membershipLogger = loggerFactory.CreateLogger<MembershipService>();

        // Create mock current user service
        _currentUserService = new MockCurrentUserService();

        _customerRepository = new CustomerRepository(_context, _customerLogger);
        _saleRepository = new SaleRepository(_context, _saleLogger);
        _membershipService = new MembershipService(_customerRepository, _saleRepository, _currentUserService, _membershipLogger);
    }

    /// <summary>
    /// Property 23: Membership Discount Application
    /// For any customer with a valid membership tier and applicable discount rules, 
    /// the membership discount should be calculated correctly based on the tier benefits 
    /// and applied to the sale total
    /// Validates: Requirements 14.3, 14.4
    /// </summary>
    [Property]
    public bool MembershipDiscountApplicationProperty(PositiveInt tierValue, PositiveInt totalAmountCents)
    {
        // Feature: offline-first-pos, Property 23: Membership Discount Application
        // **Validates: Requirements 14.3, 14.4**
        
        var tier = (MembershipTier)(tierValue.Get % 5); // 0-4 maps to None-Platinum
        var totalAmount = Math.Max(1m, totalAmountCents.Get / 100m); // Convert cents to dollars
        
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = $"MEM-{DateTime.UtcNow:yyyyMMdd}-{System.Random.Shared.Next(1000, 9999)}",
            Name = "Test Customer",
            Tier = tier,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV-{System.Random.Shared.Next(10000, 99999)}",
            TotalAmount = totalAmount,
            PaymentMethod = PaymentMethod.Cash,
            DeviceId = Guid.NewGuid()
        };
        
        // Calculate membership discount
        var discount = _membershipService.CalculateMembershipDiscountAsync(customer, sale).Result;
        
        // Expected discount percentages based on tier
        var expectedDiscountPercentage = tier switch
        {
            MembershipTier.None => 0m,
            MembershipTier.Bronze => 2m,
            MembershipTier.Silver => 5m,
            MembershipTier.Gold => 8m,
            MembershipTier.Platinum => 12m,
            _ => 0m
        };
        
        var expectedDiscountAmount = Math.Round(totalAmount * expectedDiscountPercentage / 100, 2);
        
        // Verify discount calculation
        return discount.DiscountPercentage == expectedDiscountPercentage &&
               discount.DiscountAmount == expectedDiscountAmount &&
               discount.Tier == tier &&
               discount.DiscountAmount >= 0;
    }

    /// <summary>
    /// Property: Customer tier calculation based on total spending
    /// For any customer, the membership tier should be correctly calculated based on total spent amount
    /// </summary>
    [Property]
    public bool CustomerTierCalculationProperty(PositiveInt totalSpentCents)
    {
        var totalSpent = Math.Max(0m, totalSpentCents.Get / 100m); // Convert cents to dollars
        
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = $"MEM-{DateTime.UtcNow:yyyyMMdd}-{System.Random.Shared.Next(1000, 9999)}",
            Name = "Test Customer",
            TotalSpent = totalSpent,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        var calculatedTier = _membershipService.CalculateMembershipTierAsync(customer).Result;
        
        var expectedTier = totalSpent switch
        {
            >= 15000 => MembershipTier.Platinum,
            >= 5000 => MembershipTier.Gold,
            >= 1000 => MembershipTier.Silver,
            >= 0 => MembershipTier.Bronze,
            _ => MembershipTier.None
        };
        
        return calculatedTier == expectedTier;
    }

    /// <summary>
    /// Property: Inactive customers should not receive discounts
    /// For any inactive customer, the membership discount should be zero regardless of tier
    /// </summary>
    [Property]
    public bool InactiveCustomerNoDiscountProperty(PositiveInt tierValue, PositiveInt totalAmountCents)
    {
        var tier = (MembershipTier)(tierValue.Get % 5); // 0-4 maps to None-Platinum
        var totalAmount = Math.Max(1m, totalAmountCents.Get / 100m); // Convert cents to dollars
        
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = $"MEM-{DateTime.UtcNow:yyyyMMdd}-{System.Random.Shared.Next(1000, 9999)}",
            Name = "Test Customer",
            Tier = tier,
            IsActive = false, // Inactive customer
            DeviceId = Guid.NewGuid()
        };
        
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = $"INV-{System.Random.Shared.Next(10000, 99999)}",
            TotalAmount = totalAmount,
            PaymentMethod = PaymentMethod.Cash,
            DeviceId = Guid.NewGuid()
        };
        
        var discount = _membershipService.CalculateMembershipDiscountAsync(customer, sale).Result;
        
        return discount.DiscountAmount == 0 &&
               discount.Reason.Contains("not active");
    }

    /// <summary>
    /// Property: Membership number uniqueness
    /// For any generated membership number, it should be unique across all customers
    /// </summary>
    [Property]
    public bool MembershipNumberUniquenessProperty()
    {
        var membershipNumber1 = _membershipService.GenerateUniqueMembershipNumberAsync().Result;
        var membershipNumber2 = _membershipService.GenerateUniqueMembershipNumberAsync().Result;
        
        return membershipNumber1 != membershipNumber2 &&
               membershipNumber1.StartsWith("MEM-") &&
               membershipNumber2.StartsWith("MEM-");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

/// <summary>
/// Mock implementation of ICurrentUserService for testing
/// </summary>
public class MockCurrentUserService : ICurrentUserService
{
    public User? CurrentUser { get; private set; }
    public UserSession? CurrentSession { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

    public void SetCurrentUser(User user, UserSession session)
    {
        CurrentUser = user;
        CurrentSession = session;
    }

    public void ClearCurrentUser()
    {
        CurrentUser = null;
        CurrentSession = null;
    }

    public Task UpdateActivityAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsSessionExpiredAsync(int inactivityTimeoutMinutes = 30)
    {
        return Task.FromResult(false);
    }
}