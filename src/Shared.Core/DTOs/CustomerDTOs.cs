using Shared.Core.Enums;

namespace Shared.Core.DTOs;

public class CustomerRegistrationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public MembershipTier InitialTier { get; set; } = MembershipTier.Bronze;
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime JoinDate { get; set; }
    public MembershipTier Tier { get; set; }
    public decimal TotalSpent { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public bool IsActive { get; set; }
}

public class MembershipDiscount
{
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public MembershipTier Tier { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CustomerAnalytics
{
    public int TotalCustomers { get; set; }
    public int ActiveCustomers { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageSpendingPerCustomer { get; set; }
    public Dictionary<MembershipTier, int> CustomersByTier { get; set; } = new();
    public List<CustomerDto> TopCustomers { get; set; } = new();
}

// Customer Lookup Service DTOs

public class CustomerLookupResult
{
    public Guid Id { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public MembershipTier Tier { get; set; }
    public decimal TotalSpent { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public bool IsActive { get; set; }
    public List<MembershipDiscount> AvailableDiscounts { get; set; } = new();
    public CustomerPreferences? Preferences { get; set; }
}

public class MobileNumberValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FormattedNumber { get; set; }
    public string? CountryCode { get; set; }
}

public class CustomerMembershipDetails
{
    public MembershipTier Tier { get; set; }
    public DateTime JoinDate { get; set; }
    public decimal TotalSpent { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public List<MembershipDiscount> AvailableDiscounts { get; set; } = new();
    public List<MembershipBenefit> Benefits { get; set; } = new();
    public decimal DiscountPercentage { get; set; }
    public bool IsEligibleForUpgrade { get; set; }
    public MembershipTier? NextTier { get; set; }
    public decimal AmountToNextTier { get; set; }
}

public class MembershipBenefit
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BenefitType Type { get; set; }
    public decimal Value { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum BenefitType
{
    PercentageDiscount,
    FixedAmountDiscount,
    FreeShipping,
    EarlyAccess,
    BonusPoints,
    Other
}

public class CustomerCreationRequest
{
    public string Name { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public MembershipTier InitialTier { get; set; } = MembershipTier.Bronze;
    public Guid ShopId { get; set; }
    public string? Notes { get; set; }
}

public class CustomerCreationResult
{
    public bool Success { get; set; }
    public CustomerLookupResult? Customer { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

public class CustomerPreferences
{
    public Guid CustomerId { get; set; }
    public string? PreferredPaymentMethod { get; set; }
    public List<string> FavoriteProducts { get; set; } = new();
    public List<string> PreferredCategories { get; set; } = new();
    public bool ReceivePromotions { get; set; } = true;
    public bool ReceiveSmsNotifications { get; set; } = true;
    public string? PreferredLanguage { get; set; }
    public Dictionary<string, string> CustomPreferences { get; set; } = new();
}

public class CustomerUpdateResult
{
    public bool Success { get; set; }
    public CustomerLookupResult? UpdatedCustomer { get; set; }
    public string? ErrorMessage { get; set; }
    public bool TierUpgraded { get; set; }
    public MembershipTier? NewTier { get; set; }
}

public class CustomerSearchResult
{
    public Guid Id { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public MembershipTier Tier { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisit { get; set; }
    public bool IsActive { get; set; }
}