using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Tests for data layer enhancements including new entities and repositories
/// </summary>
public class DataLayerEnhancementsTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public DataLayerEnhancementsTest()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CustomerMembership_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var customerRepo = _serviceProvider.GetRequiredService<ICustomerRepository>();
        var membershipRepo = _serviceProvider.GetRequiredService<ICustomerMembershipRepository>();

        var customer = new Customer
        {
            Name = "Test Customer",
            Phone = "1234567890",
            Email = "test@example.com",
            MembershipNumber = "MEM001",
            Tier = MembershipTier.Bronze,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        var membership = new CustomerMembership
        {
            CustomerId = customer.Id,
            Tier = MembershipTier.Silver,
            DiscountPercentage = 5.0m,
            Points = 100,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        // Act
        await membershipRepo.AddAsync(membership);
        await membershipRepo.SaveChangesAsync();

        var retrievedMembership = await membershipRepo.GetByCustomerIdAsync(customer.Id);

        // Assert
        Assert.NotNull(retrievedMembership);
        Assert.Equal(customer.Id, retrievedMembership.CustomerId);
        Assert.Equal(MembershipTier.Silver, retrievedMembership.Tier);
        Assert.Equal(5.0m, retrievedMembership.DiscountPercentage);
        Assert.Equal(100, retrievedMembership.Points);
    }

    [Fact]
    public async Task MembershipBenefit_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var customerRepo = _serviceProvider.GetRequiredService<ICustomerRepository>();
        var membershipRepo = _serviceProvider.GetRequiredService<ICustomerMembershipRepository>();
        var benefitRepo = _serviceProvider.GetRequiredService<IMembershipBenefitRepository>();

        var customer = new Customer
        {
            Name = "Test Customer",
            Phone = "1234567890",
            MembershipNumber = "MEM002",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        var membership = new CustomerMembership
        {
            CustomerId = customer.Id,
            Tier = MembershipTier.Gold,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await membershipRepo.AddAsync(membership);
        await membershipRepo.SaveChangesAsync();

        var benefit = new MembershipBenefit
        {
            CustomerMembershipId = membership.Id,
            Name = "Gold Discount",
            Description = "10% discount on all purchases",
            Type = Entities.BenefitType.PercentageDiscount,
            Value = 10.0m,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        // Act
        await benefitRepo.AddAsync(benefit);
        await benefitRepo.SaveChangesAsync();

        var retrievedBenefits = await benefitRepo.GetByCustomerMembershipIdAsync(membership.Id);

        // Assert
        Assert.NotEmpty(retrievedBenefits);
        var retrievedBenefit = retrievedBenefits.First();
        Assert.Equal("Gold Discount", retrievedBenefit.Name);
        Assert.Equal(Entities.BenefitType.PercentageDiscount, retrievedBenefit.Type);
        Assert.Equal(10.0m, retrievedBenefit.Value);
    }

    [Fact]
    public async Task CustomerPreference_CanBeSetAndRetrieved()
    {
        // Arrange
        var customerRepo = _serviceProvider.GetRequiredService<ICustomerRepository>();
        var preferenceRepo = _serviceProvider.GetRequiredService<ICustomerPreferenceRepository>();

        var customer = new Customer
        {
            Name = "Test Customer",
            Phone = "1234567890",
            MembershipNumber = "MEM003",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        // Act
        await preferenceRepo.SetPreferenceAsync(customer.Id, "preferred_payment", "credit_card", "payment");
        await preferenceRepo.SetPreferenceAsync(customer.Id, "newsletter", "true", "communication");

        var preferences = await preferenceRepo.GetByCustomerIdAsync(customer.Id);
        var preferencesDict = await preferenceRepo.GetPreferencesDictionaryAsync(customer.Id);

        // Assert
        Assert.Equal(2, preferences.Count);
        Assert.Equal("credit_card", preferencesDict["preferred_payment"]);
        Assert.Equal("true", preferencesDict["newsletter"]);
    }

    [Fact]
    public async Task Customer_MobileNumberLookup_ReturnsCustomerWithMembershipAndPreferences()
    {
        // Arrange
        var customerRepo = _serviceProvider.GetRequiredService<ICustomerRepository>();
        var membershipRepo = _serviceProvider.GetRequiredService<ICustomerMembershipRepository>();
        var preferenceRepo = _serviceProvider.GetRequiredService<ICustomerPreferenceRepository>();

        var customer = new Customer
        {
            Name = "Test Customer",
            Phone = "9876543210",
            MembershipNumber = "MEM004",
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await customerRepo.AddAsync(customer);
        await customerRepo.SaveChangesAsync();

        var membership = new CustomerMembership
        {
            CustomerId = customer.Id,
            Tier = MembershipTier.Platinum,
            DiscountPercentage = 15.0m,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };

        await membershipRepo.AddAsync(membership);
        await membershipRepo.SaveChangesAsync();

        await preferenceRepo.SetPreferenceAsync(customer.Id, "language", "en", "ui");

        // Act
        var retrievedCustomer = await customerRepo.GetByMobileNumberAsync("9876543210");

        // Assert
        Assert.NotNull(retrievedCustomer);
        Assert.Equal("Test Customer", retrievedCustomer.Name);
        Assert.NotNull(retrievedCustomer.Membership);
        Assert.Equal(MembershipTier.Platinum, retrievedCustomer.Membership.Tier);
        Assert.Equal(15.0m, retrievedCustomer.Membership.DiscountPercentage);
        Assert.NotEmpty(retrievedCustomer.Preferences);
        Assert.Equal("en", retrievedCustomer.Preferences.First().Value);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}