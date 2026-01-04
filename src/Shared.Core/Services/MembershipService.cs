using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Shared.Core.Services;

public class MembershipService : IMembershipService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MembershipService> _logger;

    // Membership tier thresholds and discount percentages
    private readonly Dictionary<MembershipTier, decimal> _tierThresholds = new()
    {
        { MembershipTier.Bronze, 0 },
        { MembershipTier.Silver, 1000 },
        { MembershipTier.Gold, 5000 },
        { MembershipTier.Platinum, 15000 }
    };

    private readonly Dictionary<MembershipTier, decimal> _tierDiscountPercentages = new()
    {
        { MembershipTier.None, 0 },
        { MembershipTier.Bronze, 2 },
        { MembershipTier.Silver, 5 },
        { MembershipTier.Gold, 8 },
        { MembershipTier.Platinum, 12 }
    };

    public MembershipService(
        ICustomerRepository customerRepository,
        ISaleRepository saleRepository,
        ICurrentUserService currentUserService,
        ILogger<MembershipService> logger)
    {
        _customerRepository = customerRepository;
        _saleRepository = saleRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Customer?> GetCustomerByMembershipNumberAsync(string membershipNumber)
    {
        if (string.IsNullOrWhiteSpace(membershipNumber))
        {
            return null;
        }

        return await _customerRepository.GetByMembershipNumberAsync(membershipNumber);
    }

    public async Task<Customer> RegisterCustomerAsync(CustomerRegistrationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Customer name is required", nameof(request));
        }

        var membershipNumber = await GenerateUniqueMembershipNumberAsync();
        var deviceId = _currentUserService.CurrentUser?.DeviceId ?? Guid.NewGuid(); // Fallback to new GUID if no user

        var customer = new Customer
        {
            MembershipNumber = membershipNumber,
            Name = request.Name.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Tier = request.InitialTier,
            DeviceId = deviceId
        };

        if (!await ValidateCustomerAsync(customer))
        {
            throw new InvalidOperationException("Customer validation failed");
        }

        await _customerRepository.AddAsync(customer);
        await _customerRepository.SaveChangesAsync();

        _logger.LogInformation("Customer registered: {MembershipNumber} - {Name}", 
            customer.MembershipNumber, customer.Name);

        return customer;
    }

    public async Task<MembershipDiscount> CalculateMembershipDiscountAsync(Customer customer, Sale sale)
    {
        if (customer == null || sale == null)
        {
            return new MembershipDiscount
            {
                DiscountAmount = 0,
                DiscountPercentage = 0,
                Tier = MembershipTier.None,
                Reason = "No customer or sale provided"
            };
        }

        if (!customer.IsActive)
        {
            return new MembershipDiscount
            {
                DiscountAmount = 0,
                DiscountPercentage = 0,
                Tier = customer.Tier,
                Reason = "Customer is not active"
            };
        }

        var discountPercentage = _tierDiscountPercentages.GetValueOrDefault(customer.Tier, 0);
        var discountAmount = Math.Round(sale.TotalAmount * discountPercentage / 100, 2);

        return new MembershipDiscount
        {
            DiscountAmount = discountAmount,
            DiscountPercentage = discountPercentage,
            Tier = customer.Tier,
            Reason = $"{customer.Tier} membership discount ({discountPercentage}%)"
        };
    }

    public async Task UpdateCustomerPurchaseHistoryAsync(Customer customer, Sale sale)
    {
        if (customer == null || sale == null)
        {
            return;
        }

        customer.TotalSpent += sale.TotalAmount;
        customer.VisitCount++;
        customer.LastVisit = DateTime.UtcNow;

        // Check if tier should be upgraded
        var newTier = await CalculateMembershipTierAsync(customer);
        if (newTier != customer.Tier)
        {
            _logger.LogInformation("Customer {MembershipNumber} tier upgraded from {OldTier} to {NewTier}",
                customer.MembershipNumber, customer.Tier, newTier);
            customer.Tier = newTier;
        }

        await _customerRepository.UpdateAsync(customer);
        await _customerRepository.SaveChangesAsync();
    }

    public async Task<MembershipTier> CalculateMembershipTierAsync(Customer customer)
    {
        if (customer == null)
        {
            return MembershipTier.None;
        }

        var totalSpent = customer.TotalSpent;

        // Find the highest tier the customer qualifies for
        var qualifyingTier = MembershipTier.None;
        foreach (var tier in _tierThresholds.OrderByDescending(t => t.Value))
        {
            if (totalSpent >= tier.Value)
            {
                qualifyingTier = tier.Key;
                break;
            }
        }

        return qualifyingTier;
    }

    public async Task<CustomerAnalytics> GetCustomerAnalyticsAsync()
    {
        var allCustomers = await _customerRepository.GetAllAsync();
        var activeCustomers = allCustomers.Where(c => c.IsActive).ToList();

        var analytics = new CustomerAnalytics
        {
            TotalCustomers = allCustomers.Count(),
            ActiveCustomers = activeCustomers.Count,
            TotalRevenue = activeCustomers.Sum(c => c.TotalSpent),
            AverageSpendingPerCustomer = activeCustomers.Any() 
                ? activeCustomers.Average(c => c.TotalSpent) 
                : 0
        };

        // Group customers by tier
        analytics.CustomersByTier = activeCustomers
            .GroupBy(c => c.Tier)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get top customers
        analytics.TopCustomers = activeCustomers
            .OrderByDescending(c => c.TotalSpent)
            .Take(10)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                MembershipNumber = c.MembershipNumber,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                JoinDate = c.JoinDate,
                Tier = c.Tier,
                TotalSpent = c.TotalSpent,
                VisitCount = c.VisitCount,
                LastVisit = c.LastVisit,
                IsActive = c.IsActive
            })
            .ToList();

        return analytics;
    }

    public async Task<IEnumerable<Customer>> GetTopCustomersAsync(int count)
    {
        return await _customerRepository.GetTopCustomersBySpendingAsync(count);
    }

    public async Task<bool> ValidateCustomerAsync(Customer customer)
    {
        if (customer == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(customer.MembershipNumber))
        {
            return false;
        }

        // Check if membership number is unique
        if (!await _customerRepository.IsMembershipNumberUniqueAsync(customer.MembershipNumber, customer.Id))
        {
            return false;
        }

        // Validate email format if provided
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(customer.Email);
                if (addr.Address != customer.Email)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public async Task<string> GenerateUniqueMembershipNumberAsync()
    {
        string membershipNumber;
        bool isUnique;
        int attempts = 0;
        const int maxAttempts = 100;

        do
        {
            // Generate membership number in format: MEM-YYYYMMDD-XXXX
            var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomSuffix = Random.Shared.Next(1000, 9999);
            membershipNumber = $"MEM-{datePrefix}-{randomSuffix}";

            isUnique = await _customerRepository.IsMembershipNumberUniqueAsync(membershipNumber);
            attempts++;

            if (attempts >= maxAttempts)
            {
                throw new InvalidOperationException("Unable to generate unique membership number after maximum attempts");
            }
        }
        while (!isUnique);

        return membershipNumber;
    }
}