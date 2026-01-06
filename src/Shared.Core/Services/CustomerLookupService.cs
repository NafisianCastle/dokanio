using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Text.RegularExpressions;

namespace Shared.Core.Services;

/// <summary>
/// Service for fast customer lookup with mobile number validation and auto-fill functionality
/// Implements caching for optimal performance and provides comprehensive customer management
/// </summary>
public class CustomerLookupService : ICustomerLookupService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICachingStrategyService _cachingService;
    private readonly ILogger<CustomerLookupService> _logger;

    // Cache configuration
    private readonly TimeSpan _customerCacheExpiration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _membershipCacheExpiration = TimeSpan.FromMinutes(30);

    // Mobile number validation patterns
    private static readonly Regex MobileNumberPattern = new(@"^(\+?1-?)?(\([0-9]{3}\)|[0-9]{3})-?[0-9]{3}-?[0-9]{4}$|^(\+?[1-9]{1,4})?[-.\s]?(\(?[0-9]{1,4}\)?[-.\s]?)?[0-9]{1,4}[-.\s]?[0-9]{1,9}$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyPattern = new(@"[^\d]", RegexOptions.Compiled);

    // Membership tier thresholds for upgrades
    private static readonly Dictionary<MembershipTier, decimal> TierThresholds = new()
    {
        { MembershipTier.Bronze, 0 },
        { MembershipTier.Silver, 500 },
        { MembershipTier.Gold, 2000 },
        { MembershipTier.Platinum, 5000 }
    };

    // Membership tier discount percentages
    private static readonly Dictionary<MembershipTier, decimal> TierDiscounts = new()
    {
        { MembershipTier.None, 0 },
        { MembershipTier.Bronze, 2 },
        { MembershipTier.Silver, 5 },
        { MembershipTier.Gold, 8 },
        { MembershipTier.Platinum, 12 }
    };

    public CustomerLookupService(
        ICustomerRepository customerRepository,
        ICachingStrategyService cachingService,
        ILogger<CustomerLookupService> logger)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomerLookupResult?> LookupByMobileNumberAsync(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            _logger.LogWarning("Mobile number lookup attempted with null or empty number");
            return null;
        }

        // Normalize mobile number for consistent lookup
        var normalizedNumber = NormalizeMobileNumber(mobileNumber);
        var cacheKey = $"customer_mobile_{normalizedNumber}";

        try
        {
            // Try to get from cache first
            var cachedResult = await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
            {
                var customer = await _customerRepository.GetByMobileNumberAsync(normalizedNumber);
                return customer != null ? await MapToLookupResultAsync(customer) : null;
            }, CacheStrategy.MemoryFirst);

            if (cachedResult != null)
            {
                _logger.LogDebug("Customer lookup successful for provided mobile number.");
            }
            else
            {
                _logger.LogDebug("No customer found for provided mobile number.");
            }

            return cachedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during customer lookup for provided mobile number.");
            return null;
        }
    }

    public async Task<MobileNumberValidationResult> ValidateMobileNumberAsync(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            return new MobileNumberValidationResult
            {
                IsValid = false,
                ErrorMessage = "Mobile number is required"
            };
        }

        var trimmedNumber = mobileNumber.Trim();
        
        // Check basic format
        if (!MobileNumberPattern.IsMatch(trimmedNumber))
        {
            return new MobileNumberValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid mobile number format. Please enter a valid phone number."
            };
        }

        // Check length constraints
        var digitsOnly = DigitsOnlyPattern.Replace(trimmedNumber, "");
        if (digitsOnly.Length < 10 || digitsOnly.Length > 15)
        {
            return new MobileNumberValidationResult
            {
                IsValid = false,
                ErrorMessage = "Mobile number must be between 10 and 15 digits."
            };
        }

        // Check for uniqueness if this is for a new customer
        var normalizedNumber = NormalizeMobileNumber(trimmedNumber);
        var existingCustomer = await _customerRepository.GetByMobileNumberAsync(normalizedNumber);
        
        return new MobileNumberValidationResult
        {
            IsValid = true,
            FormattedNumber = FormatMobileNumber(normalizedNumber),
            CountryCode = ExtractCountryCode(trimmedNumber)
        };
    }

    public async Task<CustomerMembershipDetails?> GetMembershipDetailsAsync(Guid customerId)
    {
        var cacheKey = $"membership_details_{customerId}";

        try
        {
            return await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
            {
                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null) return null;

                return new CustomerMembershipDetails
                {
                    Tier = customer.Tier,
                    JoinDate = customer.JoinDate,
                    TotalSpent = customer.TotalSpent,
                    VisitCount = customer.VisitCount,
                    LastVisit = customer.LastVisit,
                    AvailableDiscounts = GetAvailableDiscounts(customer.Tier),
                    Benefits = GetMembershipBenefits(customer.Tier),
                    DiscountPercentage = TierDiscounts.GetValueOrDefault(customer.Tier, 0),
                    IsEligibleForUpgrade = IsEligibleForTierUpgrade(customer),
                    NextTier = GetNextTier(customer.Tier),
                    AmountToNextTier = CalculateAmountToNextTier(customer)
                };
            }, CacheStrategy.MemoryFirst);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving membership details for customer: {CustomerId}", customerId);
            return null;
        }
    }

    public async Task<CustomerCreationResult> CreateNewCustomerAsync(CustomerCreationRequest request)
    {
        try
        {
            // Validate the request
            var validationResult = await ValidateCustomerCreationRequestAsync(request);
            if (!validationResult.IsValid)
            {
                return new CustomerCreationResult
                {
                    Success = false,
                    ErrorMessage = "Validation failed",
                    ValidationErrors = validationResult.Errors
                };
            }

            // Check if mobile number already exists
            var normalizedNumber = NormalizeMobileNumber(request.MobileNumber);
            var existingCustomer = await _customerRepository.GetByMobileNumberAsync(normalizedNumber);
            if (existingCustomer != null)
            {
                return new CustomerCreationResult
                {
                    Success = false,
                    ErrorMessage = "A customer with this mobile number already exists",
                    Customer = await MapToLookupResultAsync(existingCustomer)
                };
            }

            // Generate unique membership number
            var membershipNumber = await GenerateUniqueMembershipNumberAsync();

            // Create new customer
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                MembershipNumber = membershipNumber,
                Name = request.Name.Trim(),
                Phone = normalizedNumber,
                Email = request.Email?.Trim(),
                Tier = request.InitialTier,
                JoinDate = DateTime.UtcNow,
                IsActive = true,
                DeviceId = request.ShopId, // Using ShopId as DeviceId for now
                SyncStatus = SyncStatus.NotSynced
            };

            await _customerRepository.AddAsync(customer);

            // Invalidate relevant caches
            await InvalidateCustomerCacheAsync(customer.Id);

            var result = await MapToLookupResultAsync(customer);
            
            _logger.LogInformation("New customer created successfully: {CustomerId}", 
                customer.Id);

            return new CustomerCreationResult
            {
                Success = true,
                Customer = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new customer");
            
            return new CustomerCreationResult
            {
                Success = false,
                ErrorMessage = "An error occurred while creating the customer. Please try again."
            };
        }
    }

    public async Task<CustomerPreferences?> GetCustomerPreferencesAsync(Guid customerId)
    {
        var cacheKey = $"customer_preferences_{customerId}";

        try
        {
            return await _cachingService.GetWithFallbackAsync(cacheKey, async () =>
            {
                // For now, return default preferences as this would typically come from a separate preferences table
                return new CustomerPreferences
                {
                    CustomerId = customerId,
                    ReceivePromotions = true,
                    ReceiveSmsNotifications = true,
                    PreferredLanguage = "en",
                    FavoriteProducts = new List<string>(),
                    PreferredCategories = new List<string>(),
                    CustomPreferences = new Dictionary<string, string>()
                };
            }, CacheStrategy.MemoryFirst);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer preferences for: {CustomerId}", customerId);
            return null;
        }
    }

    public async Task<CustomerUpdateResult> UpdateCustomerAfterPurchaseAsync(Guid customerId, decimal purchaseAmount)
    {
        try
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
            {
                return new CustomerUpdateResult
                {
                    Success = false,
                    ErrorMessage = "Customer not found"
                };
            }

            var originalTier = customer.Tier;
            
            // Update customer statistics
            customer.TotalSpent += purchaseAmount;
            customer.VisitCount++;
            customer.LastVisit = DateTime.UtcNow;

            // Check for tier upgrade
            var newTier = CalculateNewTier(customer.TotalSpent);
            var tierUpgraded = newTier != originalTier;
            if (tierUpgraded)
            {
                customer.Tier = newTier;
                _logger.LogInformation("Customer {CustomerId} upgraded from {OldTier} to {NewTier}", 
                    customerId, originalTier, newTier);
            }

            await _customerRepository.UpdateAsync(customer);

            // Invalidate caches
            await InvalidateCustomerCacheAsync(customerId);

            var updatedCustomer = await MapToLookupResultAsync(customer);

            return new CustomerUpdateResult
            {
                Success = true,
                UpdatedCustomer = updatedCustomer,
                TierUpgraded = tierUpgraded,
                NewTier = tierUpgraded ? newTier : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer after purchase: {CustomerId}", customerId);
            return new CustomerUpdateResult
            {
                Success = false,
                ErrorMessage = "An error occurred while updating customer information"
            };
        }
    }

    public async Task InvalidateCustomerCacheAsync(Guid customerId)
    {
        try
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer != null)
        {
            var normalized = NormalizeMobileNumber(customer.Phone);
            await _cachingService.InvalidateCacheAsync($"customer_mobile_{normalized}");
        }

        await _cachingService.InvalidateCacheAsync($"membership_details_{customerId}");
            await _cachingService.InvalidateCacheAsync($"customer_preferences_{customerId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for customer: {CustomerId}", customerId);
        }
    }

    public async Task<List<CustomerSearchResult>> SearchCustomersAsync(string searchTerm, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<CustomerSearchResult>();
        }

        try
        {
            var customers = await _customerRepository.SearchByNameOrMembershipAsync(searchTerm.Trim(), maxResults);
            
            return customers.Select(c => new CustomerSearchResult
            {
                Id = c.Id,
                MembershipNumber = c.MembershipNumber,
                Name = c.Name,
                Phone = c.Phone,
                Tier = c.Tier,
                TotalSpent = c.TotalSpent,
                LastVisit = c.LastVisit,
                IsActive = c.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", searchTerm);
            return new List<CustomerSearchResult>();
        }
    }

    // Private helper methods

    private async Task<CustomerLookupResult> MapToLookupResultAsync(Customer customer)
    {
        return new CustomerLookupResult
        {
            Id = customer.Id,
            MembershipNumber = customer.MembershipNumber,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
            Tier = customer.Tier,
            TotalSpent = customer.TotalSpent,
            VisitCount = customer.VisitCount,
            LastVisit = customer.LastVisit,
            IsActive = customer.IsActive,
            AvailableDiscounts = GetAvailableDiscounts(customer.Tier),
            Preferences = await GetCustomerPreferencesAsync(customer.Id)
        };
    }

    private static string NormalizeMobileNumber(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return string.Empty;

        // Remove all non-digit characters
        var digitsOnly = DigitsOnlyPattern.Replace(mobileNumber, "");
        
        // Handle US numbers - remove leading 1 if present and number is 11 digits
        if (digitsOnly.Length == 11 && digitsOnly.StartsWith("1"))
        {
            digitsOnly = digitsOnly.Substring(1);
        }

        return digitsOnly;
    }

    private static string FormatMobileNumber(string normalizedNumber)
    {
        if (string.IsNullOrWhiteSpace(normalizedNumber))
            return string.Empty;

        // Format as (XXX) XXX-XXXX for 10-digit US numbers
        if (normalizedNumber.Length == 10)
        {
            return $"({normalizedNumber.Substring(0, 3)}) {normalizedNumber.Substring(3, 3)}-{normalizedNumber.Substring(6)}";
        }

        // For other lengths, just add dashes
        if (normalizedNumber.Length > 6)
        {
            return $"{normalizedNumber.Substring(0, 3)}-{normalizedNumber.Substring(3, 3)}-{normalizedNumber.Substring(6)}";
        }

        return normalizedNumber;
    }

    private static string ExtractCountryCode(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return "1"; // Default to US

        var trimmed = mobileNumber.Trim();
        if (trimmed.StartsWith("+"))
        {
            var digits = DigitsOnlyPattern.Replace(trimmed, "");
            if (digits.Length > 10)
            {
                return digits.Substring(0, digits.Length - 10);
            }
        }

        return "1"; // Default to US
    }

    private static string MaskMobileNumber(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"****{mobileNumber.Substring(mobileNumber.Length - 4)}";
    }

    private List<MembershipDiscount> GetAvailableDiscounts(MembershipTier tier)
    {
        var discounts = new List<MembershipDiscount>();
        
        if (TierDiscounts.TryGetValue(tier, out var discountPercentage) && discountPercentage > 0)
        {
            discounts.Add(new MembershipDiscount
            {
                DiscountPercentage = discountPercentage,
                Tier = tier,
                Reason = $"{tier} membership discount"
            });
        }

        return discounts;
    }

    private List<DTOs.MembershipBenefit> GetMembershipBenefits(MembershipTier tier)
    {
        var benefits = new List<DTOs.MembershipBenefit>();

        switch (tier)
        {
            case MembershipTier.Bronze:
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Welcome Discount",
                    Description = "2% discount on all purchases",
                    Type = DTOs.BenefitType.PercentageDiscount,
                    Value = 2
                });
                break;

            case MembershipTier.Silver:
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Silver Discount",
                    Description = "5% discount on all purchases",
                    Type = DTOs.BenefitType.PercentageDiscount,
                    Value = 5
                });
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Priority Support",
                    Description = "Priority customer support",
                    Type = DTOs.BenefitType.Other,
                    Value = 0
                });
                break;

            case MembershipTier.Gold:
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Gold Discount",
                    Description = "8% discount on all purchases",
                    Type = DTOs.BenefitType.PercentageDiscount,
                    Value = 8
                });
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Early Access",
                    Description = "Early access to new products and sales",
                    Type = DTOs.BenefitType.EarlyAccess,
                    Value = 0
                });
                break;

            case MembershipTier.Platinum:
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Platinum Discount",
                    Description = "12% discount on all purchases",
                    Type = DTOs.BenefitType.PercentageDiscount,
                    Value = 12
                });
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "VIP Treatment",
                    Description = "VIP customer treatment and exclusive offers",
                    Type = DTOs.BenefitType.Other,
                    Value = 0
                });
                benefits.Add(new DTOs.MembershipBenefit
                {
                    Name = "Free Shipping",
                    Description = "Free shipping on all orders",
                    Type = DTOs.BenefitType.FreeShipping,
                    Value = 0
                });
                break;
        }

        return benefits;
    }

    private bool IsEligibleForTierUpgrade(Customer customer)
    {
        var nextTier = GetNextTier(customer.Tier);
        if (nextTier == null) return false;

        return TierThresholds.TryGetValue(nextTier.Value, out var threshold) && 
               customer.TotalSpent >= threshold;
    }

    private MembershipTier? GetNextTier(MembershipTier currentTier)
    {
        return currentTier switch
        {
            MembershipTier.None => MembershipTier.Bronze,
            MembershipTier.Bronze => MembershipTier.Silver,
            MembershipTier.Silver => MembershipTier.Gold,
            MembershipTier.Gold => MembershipTier.Platinum,
            MembershipTier.Platinum => null,
            _ => null
        };
    }

    private decimal CalculateAmountToNextTier(Customer customer)
    {
        var nextTier = GetNextTier(customer.Tier);
        if (nextTier == null) return 0;

        if (TierThresholds.TryGetValue(nextTier.Value, out var threshold))
        {
            return Math.Max(0, threshold - customer.TotalSpent);
        }

        return 0;
    }

    private MembershipTier CalculateNewTier(decimal totalSpent)
    {
        if (totalSpent >= TierThresholds[MembershipTier.Platinum])
            return MembershipTier.Platinum;
        if (totalSpent >= TierThresholds[MembershipTier.Gold])
            return MembershipTier.Gold;
        if (totalSpent >= TierThresholds[MembershipTier.Silver])
            return MembershipTier.Silver;
        if (totalSpent >= TierThresholds[MembershipTier.Bronze])
            return MembershipTier.Bronze;
        
        return MembershipTier.None;
    }

    private async Task<string> GenerateUniqueMembershipNumberAsync()
    {
        string membershipNumber;
        bool isUnique;
        int attempts = 0;
        const int maxAttempts = 10;

        do
        {
            // Generate format: CUST-YYYYMMDD-XXXX where XXXX is random
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = Random.Shared.Next(1000, 9999);
            membershipNumber = $"CUST-{datePart}-{randomPart}";
            
            isUnique = await _customerRepository.IsMembershipNumberUniqueAsync(membershipNumber);
            attempts++;
            
        } while (!isUnique && attempts < maxAttempts);

        if (!isUnique)
        {
            // Fallback to GUID-based number if we can't generate a unique one
            membershipNumber = $"CUST-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        return membershipNumber;
    }

    private async Task<(bool IsValid, List<string> Errors)> ValidateCustomerCreationRequestAsync(CustomerCreationRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Customer name is required");
        }
        else if (request.Name.Length > 200)
        {
            errors.Add("Customer name cannot exceed 200 characters");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            errors.Add("Mobile number is required");
        }
        else
        {
            var mobileValidation = await ValidateMobileNumberAsync(request.MobileNumber);
            if (!mobileValidation.IsValid)
            {
                errors.Add(mobileValidation.ErrorMessage ?? "Invalid mobile number");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (request.Email.Length > 255)
            {
                errors.Add("Email cannot exceed 255 characters");
            }
            else if (!IsValidEmail(request.Email))
            {
                errors.Add("Invalid email format");
            }
        }

        return (errors.Count == 0, errors);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
