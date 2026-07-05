using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;

namespace Desktop.ViewModels;

public class SupplierViewModel : BaseViewModel
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly ILogger<SupplierViewModel> _logger;

    [Reactive] public string SearchText { get; set; } = string.Empty;
    [Reactive] public Supplier? SelectedSupplier { get; set; }
    [Reactive] public bool IsAddingSupplier { get; set; }
    [Reactive] public string SupplierName { get; set; } = string.Empty;
    [Reactive] public string ContactPerson { get; set; } = string.Empty;
    [Reactive] public string Phone { get; set; } = string.Empty;
    [Reactive] public string Email { get; set; } = string.Empty;
    [Reactive] public string Address { get; set; } = string.Empty;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadSuppliersCommand { get; }
    public ReactiveCommand<Unit, Unit> AddNewSupplierCommand { get; }
    public ReactiveCommand<Supplier, Unit> EditSupplierCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSupplierCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelEditCommand { get; }
    public ReactiveCommand<Supplier, Unit> DeleteSupplierCommand { get; }

    // Design-time constructor
    public SupplierViewModel()
    {
        Title = "Supplier Management";
        LoadSuppliersCommand  = ReactiveCommand.CreateFromTask(LoadSuppliersAsync);
        AddNewSupplierCommand = ReactiveCommand.Create(AddNewSupplier);
        EditSupplierCommand   = ReactiveCommand.Create<Supplier>(EditSupplier);
        SaveSupplierCommand   = ReactiveCommand.CreateFromTask(SaveSupplierAsync);
        CancelEditCommand     = ReactiveCommand.Create(CancelEdit);
        DeleteSupplierCommand = ReactiveCommand.CreateFromTask<Supplier>(DeleteSupplierAsync);
    }

    public SupplierViewModel(
        ISupplierRepository supplierRepository,
        ILogger<SupplierViewModel> logger) : this()
    {
        _supplierRepository = supplierRepository;
        _logger             = logger;

        this.WhenAnyValue(x => x.SearchText).Subscribe(_ => RefreshFilteredSuppliers());

        _ = Task.Run(LoadSuppliersAsync);
    }

    private async Task LoadSuppliersAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var suppliers = await _supplierRepository.GetActiveSuppliersAsync();
            Suppliers.Clear();
            foreach (var s in suppliers) Suppliers.Add(s);
            RefreshFilteredSuppliers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading suppliers");
            SetError($"Error loading suppliers: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void AddNewSupplier()
    {
        IsAddingSupplier = true;
        ClearForm();
    }

    private void EditSupplier(Supplier supplier)
    {
        SelectedSupplier = supplier;
        IsAddingSupplier = true;
        SupplierName  = supplier.Name;
        ContactPerson = supplier.ContactPerson ?? string.Empty;
        Phone         = supplier.Phone ?? string.Empty;
        Email         = supplier.Email ?? string.Empty;
        Address       = supplier.Address ?? string.Empty;
    }

    private async Task SaveSupplierAsync()
    {
        if (string.IsNullOrWhiteSpace(SupplierName)) { SetError("Supplier name is required"); return; }

        IsBusy = true;
        ClearError();
        try
        {
            if (SelectedSupplier != null)
            {
                SelectedSupplier.Name          = SupplierName;
                SelectedSupplier.ContactPerson = ContactPerson;
                SelectedSupplier.Phone         = Phone;
                SelectedSupplier.Email         = Email;
                SelectedSupplier.Address       = Address;
                SelectedSupplier.UpdatedAt     = DateTime.UtcNow;
                SelectedSupplier.SyncStatus    = SyncStatus.NotSynced;
                await _supplierRepository.UpdateAsync(SelectedSupplier);
                await _supplierRepository.SaveChangesAsync();
                _logger.LogInformation("Updated supplier {SupplierName}", SelectedSupplier.Name);
            }
            else
            {
                var s = new Supplier
                {
                    Id            = Guid.NewGuid(),
                    Name          = SupplierName,
                    ContactPerson = ContactPerson,
                    Phone         = Phone,
                    Email         = Email,
                    Address       = Address,
                    IsActive      = true,
                    CreatedAt     = DateTime.UtcNow,
                    UpdatedAt     = DateTime.UtcNow,
                    SyncStatus    = SyncStatus.NotSynced
                };
                await _supplierRepository.AddAsync(s);
                await _supplierRepository.SaveChangesAsync();
                Suppliers.Add(s);
                _logger.LogInformation("Created supplier {SupplierName}", s.Name);
            }
            RefreshFilteredSuppliers();
            CancelEdit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving supplier");
            SetError($"Error saving supplier: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void CancelEdit()
    {
        IsAddingSupplier = false;
        SelectedSupplier = null;
        ClearForm();
    }

    private async Task DeleteSupplierAsync(Supplier supplier)
    {
        IsBusy = true;
        ClearError();
        try
        {
            supplier.IsActive   = false;
            supplier.IsDeleted  = true;
            supplier.DeletedAt  = DateTime.UtcNow;
            supplier.UpdatedAt  = DateTime.UtcNow;
            supplier.SyncStatus = SyncStatus.NotSynced;
            await _supplierRepository.UpdateAsync(supplier);
            await _supplierRepository.SaveChangesAsync();
            RefreshFilteredSuppliers();
            _logger.LogInformation("Soft-deleted supplier {SupplierName}", supplier.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier");
            SetError($"Error deleting supplier: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ClearForm()
    {
        SupplierName  = string.Empty;
        ContactPerson = string.Empty;
        Phone         = string.Empty;
        Email         = string.Empty;
        Address       = string.Empty;
        ClearError();
    }

    private void RefreshFilteredSuppliers()
    {
        FilteredSuppliers.Clear();
        var filtered = Suppliers.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.ToLowerInvariant();
            filtered = filtered.Where(s =>
                s.Name.ToLowerInvariant().Contains(q) ||
                (s.ContactPerson?.ToLowerInvariant().Contains(q) == true) ||
                (s.Phone?.Contains(SearchText) == true));
        }
        foreach (var s in filtered.OrderBy(s => s.Name)) FilteredSuppliers.Add(s);
    }
}
