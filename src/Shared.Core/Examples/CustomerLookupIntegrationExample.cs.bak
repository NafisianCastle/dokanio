using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.DTOs;
using Shared.Core.Services;
using System.Collections.ObjectModel;

namespace Shared.Core.Examples;

/// <summary>
/// Example showing how to integrate CustomerLookupService into a ViewModel
/// This demonstrates the customer membership auto-fill functionality
/// </summary>
public partial class CustomerLookupIntegrationExample : ObservableObject
{
    private readonly ICustomerLookupService _customerLookupService;

    [ObservableProperty]
    private string mobileNumber = string.Empty;

    [ObservableProperty]
    private string customerName = string.Empty;

    [ObservableProperty]
    private string customerEmail = string.Empty;

    [ObservableProperty]
    private string membershipTier = string.Empty;

    [ObservableProperty]
    private string membershipNumber = string.Empty;

    [ObservableProperty]
    private decimal availableDiscount;

    [ObservableProperty]
    private bool isCustomerFound;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    [ObservableProperty]
    private bool showCreateCustomerOption;

    public ObservableCollection<MembershipDiscount> AvailableDiscounts { get; } = new();

    public CustomerLookupIntegrationExample(ICustomerLookupService customerLookupService)
    {
        _customerLookupService = customerLookupService ?? throw new ArgumentNullException(nameof(customerLookupService));
    }

    /// <summary>
    /// Automatically triggered when mobile number changes
    /// Demonstrates real-time customer lookup and auto-fill
    /// </summary>
    partial void OnMobileNumberChanged(string value)
    {
        // Clear previous data
        ClearCustomerData();
        
        // Validate and lookup customer if mobile number is complete
        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 10)
        {
            _ = LookupCustomerAsync(value);
        }
    }

    /// <summary>
    /// Performs customer lookup and auto-fills customer information
    /// </summary>
    [RelayCommand]
    private async Task LookupCustomerAsync(string? phoneNumber = null)
    {
        phoneNumber ??= MobileNumber;
        
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            ValidationMessage = "Please enter a mobile number";
            return;
        }

        IsLoading = true;
        ValidationMessage = string.Empty;

        try
        {
            // Validate mobile number format first
            var validationResult = await _customerLookupService.ValidateMobileNumberAsync(phoneNumber);
            if (!validationResult.IsValid)
            {
                ValidationMessage = validationResult.ErrorMessage ?? "Invalid mobile number format";
                ShowCreateCustomerOption = false;
                return;
            }

            // Lookup customer by mobile number
            var customer = await _customerLookupService.LookupByMobileNumberAsync(phoneNumber);
            
            if (customer != null)
            {
                // Auto-fill customer information
                CustomerName = customer.Name;
                CustomerEmail = customer.Email ?? string.Empty;
                MembershipTier = customer.Tier.ToString();
                MembershipNumber = customer.MembershipNumber;
                IsCustomerFound = true;
                ShowCreateCustomerOption = false;

                // Load available discounts
                AvailableDiscounts.Clear();
                foreach (var discount in customer.AvailableDiscounts)
                {
                    AvailableDiscounts.Add(discount);
                }

                // Calculate total available discount
                AvailableDiscount = customer.AvailableDiscounts.Sum(d => d.DiscountPercentage);

                ValidationMessage = $"Welcome back, {customer.Name}! ({customer.Tier} member)";
            }
            else
            {
                // Customer not found - offer to create new customer
                IsCustomerFound = false;
                ShowCreateCustomerOption = true;
                ValidationMessage = "Customer not found. Would you like to create a new customer account?";
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = "Error looking up customer. Please try again.";
            // In a real application, you would log this error
            System.Diagnostics.Debug.WriteLine($"Customer lookup error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates a new customer account
    /// </summary>
    [RelayCommand]
    private async Task CreateNewCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerName) || string.IsNullOrWhiteSpace(MobileNumber))
        {
            ValidationMessage = "Please enter customer name and mobile number";
            return;
        }

        IsLoading = true;

        try
        {
            var request = new CustomerCreationRequest
            {
                Name = CustomerName.Trim(),
                MobileNumber = MobileNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail.Trim(),
                InitialTier = Shared.Core.Enums.MembershipTier.Bronze,
                ShopId = Guid.NewGuid() // In real app, this would come from current shop context
            };

            var result = await _customerLookupService.CreateNewCustomerAsync(request);
            
            if (result.Success && result.Customer != null)
            {
                // Auto-fill with new customer information
                MembershipTier = result.Customer.Tier.ToString();
                MembershipNumber = result.Customer.MembershipNumber;
                IsCustomerFound = true;
                ShowCreateCustomerOption = false;

                // Load available discounts for new customer
                AvailableDiscounts.Clear();
                foreach (var discount in result.Customer.AvailableDiscounts)
                {
                    AvailableDiscounts.Add(discount);
                }

                AvailableDiscount = result.Customer.AvailableDiscounts.Sum(d => d.DiscountPercentage);
                ValidationMessage = $"New customer account created successfully! Welcome, {result.Customer.Name}!";
            }
            else
            {
                ValidationMessage = result.ErrorMessage ?? "Failed to create customer account";
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = "Error creating customer account. Please try again.";
            System.Diagnostics.Debug.WriteLine($"Customer creation error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Searches for customers by name or membership number
    /// </summary>
    [RelayCommand]
    private async Task SearchCustomersAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return;

        IsLoading = true;

        try
        {
            var results = await _customerLookupService.SearchCustomersAsync(searchTerm);
            
            // In a real application, you would display these results in a list
            // and allow the user to select one
            if (results.Any())
            {
                ValidationMessage = $"Found {results.Count} customer(s) matching '{searchTerm}'";
            }
            else
            {
                ValidationMessage = $"No customers found matching '{searchTerm}'";
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = "Error searching customers. Please try again.";
            System.Diagnostics.Debug.WriteLine($"Customer search error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears all customer data fields
    /// </summary>
    private void ClearCustomerData()
    {
        CustomerName = string.Empty;
        CustomerEmail = string.Empty;
        MembershipTier = string.Empty;
        MembershipNumber = string.Empty;
        AvailableDiscount = 0;
        IsCustomerFound = false;
        ShowCreateCustomerOption = false;
        ValidationMessage = string.Empty;
        AvailableDiscounts.Clear();
    }

    /// <summary>
    /// Applies customer discount to a sale amount
    /// </summary>
    public decimal ApplyCustomerDiscount(decimal saleAmount)
    {
        if (!IsCustomerFound || AvailableDiscount <= 0)
            return saleAmount;

        var discountAmount = saleAmount * (AvailableDiscount / 100);
        return saleAmount - discountAmount;
    }

    /// <summary>
    /// Gets formatted display text for customer information
    /// </summary>
    public string GetCustomerDisplayText()
    {
        if (!IsCustomerFound)
            return "No customer selected";

        var discountText = AvailableDiscount > 0 ? $" ({AvailableDiscount}% discount)" : "";
        return $"{CustomerName} - {MembershipTier} Member{discountText}";
    }
}