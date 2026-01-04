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