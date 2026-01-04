using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Models;
using System.Collections.ObjectModel;

namespace Desktop.ViewModels;

public partial class SupplierViewModel : BaseViewModel
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private Supplier? selectedSupplier;

    [ObservableProperty]
    private bool isAddingSupplier;

    [ObservableProperty]
    private string supplierName = string.Empty;

    [ObservableProperty]
    private string contactPerson = string.Empty;

    [ObservableProperty]
    private string phone = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string address = string.Empty;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();

    public SupplierViewModel()
    {
        Title = "Supplier Management";
        LoadSampleSuppliers();
        RefreshFilteredSuppliers();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredSuppliers();
    }

    [RelayCommand]
    private void AddNewSupplier()
    {
        IsAddingSupplier = true;
        ClearForm();
    }

    [RelayCommand]
    private void EditSupplier(Supplier supplier)
    {
        SelectedSupplier = supplier;
        IsAddingSupplier = true;
        
        SupplierName = supplier.Name;
        ContactPerson = supplier.ContactPerson ?? string.Empty;
        Phone = supplier.Phone ?? string.Empty;
        Email = supplier.Email ?? string.Empty;
        Address = supplier.Address ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveSupplier()
    {
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            ErrorMessage = "Supplier name is required";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(500); // Simulate saving

            if (SelectedSupplier != null)
            {
                // Update existing supplier
                SelectedSupplier.Name = SupplierName;
                SelectedSupplier.ContactPerson = ContactPerson;
                SelectedSupplier.Phone = Phone;
                SelectedSupplier.Email = Email;
                SelectedSupplier.Address = Address;
                SelectedSupplier.UpdatedAt = DateTime.Now;
            }
            else
            {
                // Add new supplier
                var newSupplier = new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = SupplierName,
                    ContactPerson = ContactPerson,
                    Phone = Phone,
                    Email = Email,
                    Address = Address,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                Suppliers.Add(newSupplier);
            }

            RefreshFilteredSuppliers();
            CancelEdit();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving supplier: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsAddingSupplier = false;
        SelectedSupplier = null;
        ClearForm();
    }

    [RelayCommand]
    private async Task DeleteSupplier(Supplier supplier)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await Task.Delay(300); // Simulate deletion

            // Soft delete
            supplier.IsActive = false;
            supplier.UpdatedAt = DateTime.Now;

            RefreshFilteredSuppliers();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting supplier: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        SupplierName = string.Empty;
        ContactPerson = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        ErrorMessage = string.Empty;
    }

    private void RefreshFilteredSuppliers()
    {
        FilteredSuppliers.Clear();

        var filtered = Suppliers.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(s => 
                s.Name.ToLowerInvariant().Contains(searchLower) ||
                (s.ContactPerson?.ToLowerInvariant().Contains(searchLower) == true) ||
                (s.Phone?.Contains(SearchText) == true));
        }

        foreach (var supplier in filtered.OrderBy(s => s.Name))
        {
            FilteredSuppliers.Add(supplier);
        }
    }

    private void LoadSampleSuppliers()
    {
        var sampleSuppliers = new List<Supplier>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "MediCorp Pharmaceuticals", 
                ContactPerson = "John Smith", 
                Phone = "+91-9876543210", 
                Email = "john@medicorp.com",
                Address = "123 Medical Street, Mumbai, Maharashtra",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-30),
                UpdatedAt = DateTime.Now.AddDays(-5)
            },
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "HealthPlus Distributors", 
                ContactPerson = "Sarah Johnson", 
                Phone = "+91-8765432109", 
                Email = "sarah@healthplus.com",
                Address = "456 Wellness Avenue, Delhi, Delhi",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-25),
                UpdatedAt = DateTime.Now.AddDays(-3)
            },
            new() 
            { 
                Id = Guid.NewGuid(), 
                Name = "Global Medical Supplies", 
                ContactPerson = "Michael Brown", 
                Phone = "+91-7654321098", 
                Email = "michael@globalmed.com",
                Address = "789 Healthcare Road, Bangalore, Karnataka",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-20),
                UpdatedAt = DateTime.Now.AddDays(-1)
            }
        };

        foreach (var supplier in sampleSuppliers)
        {
            Suppliers.Add(supplier);
        }
    }
}