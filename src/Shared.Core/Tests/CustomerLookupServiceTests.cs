using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Shared.Core.DependencyInjection;
using Shared.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for CustomerLookupService functionality
/// </summary>
public class CustomerLookupServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ICustomerLookupService _customerLookupService;
    private readonly ICustomerRepository _customerRepository;
    private readonly PosDbContext _dbContext;

    public CustomerLookupServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _customerLookupService = _serviceProvider.GetRequiredService<ICustomerLookupService>();
        _customerRepository = _serviceProvider.GetRequiredService<ICustomerRepository>();
        _dbContext = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task LookupByMobileNumberAsync_WithValidNumber_ReturnsCustomer()
    {
        // Arrange
        var testCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-1234",
            Name = "John Doe",
            Phone = "5551234567",
            Email = "john.doe@example.com",
            Tier = MembershipTier.Silver,
            TotalSpent = 750.00m,
            VisitCount = 5,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await _customerRepository.AddAsync(testCustomer);
        await _customerRepository.SaveChangesAsync(); // Save changes to persist

        // Act
        var result = await _customerLookupService.LookupByMobileNumberAsync("5551234567");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testCustomer.Id, result.Id);
        Assert.Equal(testCustomer.Name, result.Name);
        Assert.Equal(testCustomer.Phone, result.Phone);
        Assert.Equal(testCustomer.Tier, result.Tier);
    }

    [Fact]
    public async Task LookupByMobileNumberAsync_WithNonExistentNumber_ReturnsNull()
    {
        // Act
        var result = await _customerLookupService.LookupByMobileNumberAsync("9999999999");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateMobileNumberAsync_WithValidNumber_ReturnsValid()
    {
        // Act
        var result = await _customerLookupService.ValidateMobileNumberAsync("(555) 123-4567");

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.FormattedNumber);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateMobileNumberAsync_WithInvalidNumber_ReturnsInvalid()
    {
        // Act
        var result = await _customerLookupService.ValidateMobileNumberAsync("123");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateNewCustomerAsync_WithValidRequest_CreatesCustomer()
    {
        // Arrange
        var request = new CustomerCreationRequest
        {
            Name = "Jane Smith",
            MobileNumber = "5559876543",
            Email = "jane.smith@example.com",
            InitialTier = MembershipTier.Bronze,
            ShopId = Guid.NewGuid()
        };

        // Act
        var result = await _customerLookupService.CreateNewCustomerAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Customer);
        Assert.Equal(request.Name, result.Customer.Name);
        Assert.Equal("5559876543", result.Customer.Phone); // Normalized
        Assert.Equal(request.InitialTier, result.Customer.Tier);
    }

    [Fact]
    public async Task CreateNewCustomerAsync_WithDuplicateMobileNumber_ReturnsFalse()
    {
        // Arrange
        var existingCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-5678",
            Name = "Existing Customer",
            Phone = "5551111111",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await _customerRepository.AddAsync(existingCustomer);
        await _customerRepository.SaveChangesAsync(); // Save changes to persist

        var request = new CustomerCreationRequest
        {
            Name = "New Customer",
            MobileNumber = "5551111111", // Same number
            ShopId = Guid.NewGuid()
        };

        // Act
        var result = await _customerLookupService.CreateNewCustomerAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotNull(result.Customer); // Should return existing customer
        Assert.Equal(existingCustomer.Id, result.Customer.Id);
    }

    [Fact]
    public async Task UpdateCustomerAfterPurchaseAsync_WithValidPurchase_UpdatesCustomer()
    {
        // Arrange
        var testCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-9999",
            Name = "Test Customer",
            Phone = "5552222222",
            Tier = MembershipTier.Bronze,
            TotalSpent = 400.00m,
            VisitCount = 2,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await _customerRepository.AddAsync(testCustomer);
        await _customerRepository.SaveChangesAsync(); // Save changes to persist

        // Act - Purchase that should upgrade to Silver tier (threshold is 500)
        var result = await _customerLookupService.UpdateCustomerAfterPurchaseAsync(testCustomer.Id, 150.00m);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.UpdatedCustomer);
        Assert.Equal(550.00m, result.UpdatedCustomer.TotalSpent);
        Assert.Equal(3, result.UpdatedCustomer.VisitCount);
        Assert.True(result.TierUpgraded);
        Assert.Equal(MembershipTier.Silver, result.NewTier);
    }

    [Fact]
    public async Task SearchCustomersAsync_WithValidTerm_ReturnsMatchingCustomers()
    {
        // Arrange
        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-1111",
            Name = "Alice Johnson",
            Phone = "5553333333",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-2222",
            Name = "Bob Johnson",
            Phone = "5554444444",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await _customerRepository.AddAsync(customer1);
        await _customerRepository.AddAsync(customer2);
        await _customerRepository.SaveChangesAsync(); // Save changes to persist

        // Act
        var results = await _customerLookupService.SearchCustomersAsync("Johnson");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "Alice Johnson");
        Assert.Contains(results, r => r.Name == "Bob Johnson");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}