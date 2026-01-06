using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Core.Services;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Mobile.ViewModels;

/// <summary>
/// Mobile-optimized ViewModel for customer lookup functionality
/// Features touch-optimized controls and mobile-specific UX patterns
/// </summary>
public partial class MobileCustomerLookupViewModel : BaseViewModel
{
    private readonly ICustomerLookupService _customerLookupService;
    private readonly ILogger<MobileCustomerLookupViewModel> _logger;

    [ObservableProperty]
    private string mobileNumber = string.Empty;

    [ObservableProperty]
    private CustomerLookupResult? selectedCustomer;

    [ObservableProperty]
    private bool isLookupInProgress;

    [ObservableProperty]
    private string lookupStatus = string.Empty;

    [ObservableProperty]
    private bool showCustomerDetails;

    [ObservableProperty]
    private bool showCreateCustomerForm;

    [ObservableProperty]
    private string newCustomerName = string.Empty;

    [ObservableProperty]
    private string newCustomerEmail = string.Empty;

    [ObservableProperty]
    private MembershipTier selectedMembershipTier = MembershipTier.Bronze;

    [ObservableProperty]
    private ObservableCollection<CustomerSearchResult> searchResults = new();

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private bool isSearchMode;

    [ObservableProperty]
    private bool enableHapticFeedback = true;

    [ObservableProperty]
    private bool showMembershipBenefits;

    [ObservableProperty]
    private List<MembershipBenefit> membershipBenefits = new();

    [ObservableProperty]
    private List<MembershipDiscount> availableDiscounts = new();

    // Events for communication with parent ViewModels
    public event EventHandler<CustomerSelectedEventArgs>? CustomerSelected;
    public event EventHandler<CustomerCreatedEventArgs>? CustomerCreated;

    public MobileCustomerLookupViewModel(
        ICustomerLookupService customerLookupService,
        ILogger<MobileCustomerLookupViewModel> logger)
    {
        _customerLookupService = customerLookupService;
        _logger = logger;
        
        Title = "Customer Lookup";
    }

    [RelayCommand]
    private async Task LookupCustomer()
    {
        if (string.IsNullOrWhiteSpace(MobileNumber))
        {
            SetError("Please enter a mobile number");
            TriggerErrorHapticFeedback();
            return;
        }

        try
        {
            IsLookupInProgress = true;
            LookupStatus = "Looking up customer...";
            ClearError();
            TriggerHapticFeedback();

            // Validate mobile number format
            var validationResult = await _customerLookupService.ValidateMobileNumberAsync(MobileNumber);
            if (!validationResult.IsValid)
            {
                SetError(validationResult.ErrorMessage ?? "Invalid mobile number format");
                LookupStatus = "Invalid number format";
                TriggerErrorHapticFeedback();
                return;
            }

            // Perform lookup
            var customer = await _customerLookupService.LookupByMobileNumberAsync(MobileNumber);
            
            if (customer != null)
            {
                await SelectCustomer(customer);
                LookupStatus = $"Found: {customer.Name}";
                TriggerHapticFeedback();
            }
            else
            {
                SelectedCustomer = null;
                ShowCustomerDetails = false;
                LookupStatus = "Customer not found";
                
                // Show option to create new customer
                var shouldCreate = await Shell.Current.DisplayAlert(
                    "Customer Not Found", 
                    $"No customer found with mobile number {MobileNumber}. Would you like to create a new customer?", 
                    "Create New", 
                    "Cancel");
                
                if (shouldCreate)
                {
                    await ShowCreateCustomerForm();
                }
                else
                {
                    TriggerHapticFeedback();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during customer lookup for mobile number: {MobileNumber}", MobileNumber);
            SetError($"Lookup failed: {ex.Message}");
            LookupStatus = "Lookup failed";
            TriggerErrorHapticFeedback();
        }
        finally
        {
            IsLookupInProgress = false;
            
            // Auto-clear status after delay
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (LookupStatus != "Looking up customer...")
                    {
                        LookupStatus = string.Empty;
                    }
                });
            });
        }
    }

    [RelayCommand]
    private async Task SearchCustomers()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            SearchResults.Clear();
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            TriggerHapticFeedback();

            var results = await _customerLookupService.SearchCustomersAsync(SearchTerm, 10);
            
            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            if (!results.Any())
            {
                SetError("No customers found matching your search");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", SearchTerm);
            SetError($"Search failed: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectSearchResult(CustomerSearchResult searchResult)
    {
        if (searchResult == null) return;

        try
        {
            TriggerHapticFeedback();
            
            // Convert search result to lookup result
            var customer = new CustomerLookupResult
            {
                Id = searchResult.Id,
                Name = searchResult.Name,
                Phone = searchResult.Phone,
                Tier = searchResult.Tier,
                TotalSpent = searchResult.TotalSpent,
                LastVisit = searchResult.LastVisit,
                IsActive = searchResult.IsActive
            };

            await SelectCustomer(customer);
            
            // Clear search
            IsSearchMode = false;
            SearchTerm = string.Empty;
            SearchResults.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting search result for customer: {CustomerId}", searchResult.Id);
            SetError($"Failed to select customer: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task ShowCreateCustomerForm()
    {
        ShowCreateCustomerForm = true;
        NewCustomerName = string.Empty;
        NewCustomerEmail = string.Empty;
        SelectedMembershipTier = MembershipTier.Bronze;
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task CreateNewCustomer()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName))
        {
            SetError("Customer name is required");
            TriggerErrorHapticFeedback();
            return;
        }

        try
        {
            IsBusy = true;
            ClearError();
            TriggerHapticFeedback();

            var request = new CustomerCreationRequest
            {
                Name = NewCustomerName.Trim(),
                MobileNumber = MobileNumber,
                Email = string.IsNullOrWhiteSpace(NewCustomerEmail) ? null : NewCustomerEmail.Trim(),
                InitialTier = SelectedMembershipTier,
                ShopId = Guid.Empty // Would be set from context in real implementation
            };

            var result = await _customerLookupService.CreateNewCustomerAsync(request);
            
            if (result.Success && result.Customer != null)
            {
                await SelectCustomer(result.Customer);
                ShowCreateCustomerForm = false;
                TriggerHapticFeedback();
                
                // Notify listeners
                CustomerCreated?.Invoke(this, new CustomerCreatedEventArgs
                {
                    Customer = result.Customer,
                    CreatedAt = DateTime.UtcNow
                });

                await Shell.Current.DisplayAlert("Success", "New customer created successfully!", "OK");
                
                _logger.LogInformation("Successfully created new customer: {CustomerName} (Id: {CustomerId})",
                    result.Customer.Name, result.Customer.Id);
            }
            else
            {
                SetError(result.ErrorMessage ?? "Failed to create customer");
                TriggerErrorHapticFeedback();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new customer: {CustomerName}", NewCustomerName);
            SetError($"Failed to create customer: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelCreateCustomer()
    {
        ShowCreateCustomerForm = false;
        NewCustomerName = string.Empty;
        NewCustomerEmail = string.Empty;
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task ClearCustomer()
    {
        SelectedCustomer = null;
        MobileNumber = string.Empty;
        ShowCustomerDetails = false;
        ShowMembershipBenefits = false;
        MembershipBenefits.Clear();
        AvailableDiscounts.Clear();
        LookupStatus = string.Empty;
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task ToggleSearchMode()
    {
        IsSearchMode = !IsSearchMode;
        
        if (!IsSearchMode)
        {
            SearchTerm = string.Empty;
            SearchResults.Clear();
        }
        
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task ShowMembershipDetails()
    {
        if (SelectedCustomer == null) return;

        try
        {
            TriggerHapticFeedback();
            
            var membershipDetails = await _customerLookupService.GetMembershipDetailsAsync(SelectedCustomer.Id);
            if (membershipDetails != null)
            {
                MembershipBenefits = membershipDetails.Benefits;
                AvailableDiscounts = membershipDetails.AvailableDiscounts;
                ShowMembershipBenefits = true;
            }
            else
            {
                SetError("Failed to load membership details");
                TriggerErrorHapticFeedback();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading membership details for customer: {CustomerId}", SelectedCustomer.Id);
            SetError($"Failed to load membership details: {ex.Message}");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task HideMembershipDetails()
    {
        ShowMembershipBenefits = false;
        MembershipBenefits.Clear();
        AvailableDiscounts.Clear();
        TriggerHapticFeedback();
    }

    [RelayCommand]
    private async Task QuickDialCustomer()
    {
        if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.Phone))
        {
            SetError("No phone number available");
            TriggerErrorHapticFeedback();
            return;
        }

        try
        {
            TriggerHapticFeedback();
            PhoneDialer.Default.Open(SelectedCustomer.Phone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening phone dialer for customer: {CustomerId}", SelectedCustomer.Id);
            SetError("Failed to open phone dialer");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task SendSmsToCustomer()
    {
        if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.Phone))
        {
            SetError("No phone number available");
            TriggerErrorHapticFeedback();
            return;
        }

        try
        {
            TriggerHapticFeedback();
            
            var message = await Shell.Current.DisplayPromptAsync(
                "Send SMS", 
                "Enter message to send:", 
                "Send", 
                "Cancel", 
                placeholder: "Thank you for your purchase!");

            if (!string.IsNullOrWhiteSpace(message))
            {
                var smsMessage = new SmsMessage(message, new[] { SelectedCustomer.Phone });
                await Sms.Default.ComposeAsync(smsMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to customer: {CustomerId}", SelectedCustomer.Id);
            SetError("Failed to send SMS");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task ShareCustomerInfo()
    {
        if (SelectedCustomer == null) return;

        try
        {
            TriggerHapticFeedback();
            
            var customerInfo = $"Customer: {SelectedCustomer.Name}\n" +
                              $"Phone: {SelectedCustomer.Phone}\n" +
                              $"Membership: {SelectedCustomer.Tier}\n" +
                              $"Total Spent: {SelectedCustomer.TotalSpent:C}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = customerInfo,
                Title = "Customer Information"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing customer info: {CustomerId}", SelectedCustomer.Id);
            SetError("Failed to share customer information");
            TriggerErrorHapticFeedback();
        }
    }

    [RelayCommand]
    private async Task ScanCustomerQrCode()
    {
        try
        {
            TriggerHapticFeedback();
            
            // This would integrate with a QR code scanner
            // For now, show a placeholder message
            await Shell.Current.DisplayAlert(
                "QR Code Scanner", 
                "QR code scanning for customer lookup will be available soon", 
                "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning customer QR code");
            SetError("QR code scan failed");
            TriggerErrorHapticFeedback();
        }
    }

    private async Task SelectCustomer(CustomerLookupResult customer)
    {
        try
        {
            SelectedCustomer = customer;
            ShowCustomerDetails = true;
            MobileNumber = customer.Phone ?? string.Empty;
            
            // Load membership details
            if (customer.Tier != MembershipTier.None)
            {
                await ShowMembershipDetails();
            }
            
            // Notify listeners
            CustomerSelected?.Invoke(this, new CustomerSelectedEventArgs
            {
                Customer = customer,
                SelectedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Selected customer: {CustomerName} (ID: {CustomerId})", 
                customer.Name, customer.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting customer: {CustomerId}", customer.Id);
            SetError($"Failed to select customer: {ex.Message}");
        }
    }

    private void TriggerHapticFeedback()
    {
        if (!EnableHapticFeedback) return;

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptic feedback not available
        }
    }

    private void TriggerErrorHapticFeedback()
    {
        if (!EnableHapticFeedback) return;

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        }
        catch
        {
            // Haptic feedback not available
        }
    }

    // Property change handlers for mobile-specific behavior
    partial void OnMobileNumberChanged(string value)
    {
        // Auto-format mobile number as user types
        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 10)
        {
            // Trigger lookup after a short delay to avoid excessive API calls
            _ = Task.Delay(1000).ContinueWith(async _ =>
            {
                if (MobileNumber == value && !IsLookupInProgress)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () => await LookupCustomer());
                }
            });
        }
    }

    partial void OnSearchTermChanged(string value)
    {
        // Auto-search as user types
        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 2)
        {
            _ = Task.Delay(500).ContinueWith(async _ =>
            {
                if (SearchTerm == value && IsSearchMode)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () => await SearchCustomers());
                }
            });
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
        }
    }
}

/// <summary>
/// Event arguments for customer selected events
/// </summary>
public class CustomerSelectedEventArgs : EventArgs
{
    public CustomerLookupResult Customer { get; set; } = null!;
    public DateTime SelectedAt { get; set; }
}

/// <summary>
/// Event arguments for customer created events
/// </summary>
public class CustomerCreatedEventArgs : EventArgs
{
    public CustomerLookupResult Customer { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}