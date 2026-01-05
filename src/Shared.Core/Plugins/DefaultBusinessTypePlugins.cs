using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Plugins;

/// <summary>
/// Default plugin for grocery business type
/// </summary>
public class GroceryBusinessTypePlugin : IBusinessTypePlugin
{
    public BusinessType BusinessType => BusinessType.Grocery;
    public string DisplayName => "Grocery Store";
    public string Description => "Grocery store with weight-based products and perishable items";
    public Version Version => new(1, 0, 0);

    public async Task<ValidationResult> ValidateProductAsync(Product product)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate weight-based products
        if (product.IsWeightBased && product.RatePerKilogram.HasValue)
        {
            if (product.RatePerKilogram <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("Rate per kilogram must be a positive number");
            }
        }

        // For now, use legacy properties until BusinessTypeAttributes are fully implemented
        // TODO: Migrate to BusinessTypeAttributesJson when ready

        return await Task.FromResult(result);
    }

    public async Task<ValidationResult> ValidateSaleAsync(Sale sale)
    {
        var result = new ValidationResult { IsValid = true };

        // Basic validation for grocery sales
        if (sale.TotalAmount <= 0)
        {
            result.IsValid = false;
            result.Errors.Add("Sale total must be positive");
        }

        return await Task.FromResult(result);
    }

    public IEnumerable<CustomAttributeDefinition> GetCustomProductAttributes()
    {
        return new[]
        {
            new CustomAttributeDefinition
            {
                Name = "Weight",
                DisplayName = "Weight (kg)",
                DataType = typeof(decimal),
                IsRequired = false,
                Description = "Product weight in kilograms"
            },
            new CustomAttributeDefinition
            {
                Name = "Volume",
                DisplayName = "Volume",
                DataType = typeof(string),
                IsRequired = false,
                Description = "Product volume (e.g., 500ml, 1L)"
            },
            new CustomAttributeDefinition
            {
                Name = "IsPerishable",
                DisplayName = "Perishable",
                DataType = typeof(bool),
                IsRequired = false,
                DefaultValue = false,
                Description = "Whether the product is perishable"
            }
        };
    }

    public BusinessConfigurationSchema GetConfigurationSchema()
    {
        return new BusinessConfigurationSchema
        {
            Fields = new List<ConfigurationField>
            {
                new()
                {
                    Name = "WeightUnit",
                    DisplayName = "Weight Unit",
                    DataType = typeof(string),
                    IsRequired = true,
                    DefaultValue = "kg",
                    AllowedValues = new List<object> { "kg", "g", "lb", "oz" },
                    Description = "Default unit for weight measurements"
                },
                new()
                {
                    Name = "EnableWeightBasedPricing",
                    DisplayName = "Enable Weight-Based Pricing",
                    DataType = typeof(bool),
                    IsRequired = false,
                    DefaultValue = true,
                    Description = "Allow pricing based on product weight"
                }
            }
        };
    }

    public async Task InitializeAsync(Dictionary<string, object> configuration)
    {
        // Initialize plugin with configuration
        await Task.CompletedTask;
    }
}

/// <summary>
/// Default plugin for pharmacy business type
/// </summary>
public class PharmacyBusinessTypePlugin : IBusinessTypePlugin
{
    public BusinessType BusinessType => BusinessType.Pharmacy;
    public string DisplayName => "Pharmacy/Medicine Shop";
    public string Description => "Pharmacy with medicine tracking and expiry date management";
    public Version Version => new(1, 0, 0);

    public async Task<ValidationResult> ValidateProductAsync(Product product)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate expiry date for medicines
        if (product.ExpiryDate.HasValue)
        {
            if (product.ExpiryDate.Value <= DateTime.UtcNow)
            {
                result.IsValid = false;
                result.Errors.Add("Expiry date must be in the future");
            }
        }

        // Validate batch number
        if (!string.IsNullOrWhiteSpace(product.BatchNumber))
        {
            // Batch number validation logic can be added here
        }

        // For now, use legacy properties until BusinessTypeAttributes are fully implemented
        // TODO: Migrate to BusinessTypeAttributesJson when ready

        return await Task.FromResult(result);
    }

    public async Task<ValidationResult> ValidateSaleAsync(Sale sale)
    {
        var result = new ValidationResult { IsValid = true };

        // Check for expired products in sale
        foreach (var item in sale.Items)
        {
            if (item.Product?.ExpiryDate.HasValue == true)
            {
                if (item.Product.ExpiryDate.Value <= DateTime.UtcNow)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Cannot sell expired medicine: {item.Product.Name}");
                }
            }
        }

        return await Task.FromResult(result);
    }

    public IEnumerable<CustomAttributeDefinition> GetCustomProductAttributes()
    {
        return new[]
        {
            new CustomAttributeDefinition
            {
                Name = "ExpiryDate",
                DisplayName = "Expiry Date",
                DataType = typeof(DateTime),
                IsRequired = true,
                Description = "Medicine expiry date"
            },
            new CustomAttributeDefinition
            {
                Name = "Manufacturer",
                DisplayName = "Manufacturer",
                DataType = typeof(string),
                IsRequired = true,
                Description = "Medicine manufacturer name"
            },
            new CustomAttributeDefinition
            {
                Name = "BatchNumber",
                DisplayName = "Batch Number",
                DataType = typeof(string),
                IsRequired = false,
                Description = "Manufacturing batch number"
            },
            new CustomAttributeDefinition
            {
                Name = "Dosage",
                DisplayName = "Dosage",
                DataType = typeof(string),
                IsRequired = false,
                Description = "Medicine dosage (e.g., 500mg, 10ml)"
            },
            new CustomAttributeDefinition
            {
                Name = "PrescriptionRequired",
                DisplayName = "Prescription Required",
                DataType = typeof(bool),
                IsRequired = false,
                DefaultValue = false,
                Description = "Whether prescription is required for this medicine"
            }
        };
    }

    public BusinessConfigurationSchema GetConfigurationSchema()
    {
        return new BusinessConfigurationSchema
        {
            Fields = new List<ConfigurationField>
            {
                new()
                {
                    Name = "ExpiryWarningDays",
                    DisplayName = "Expiry Warning Days",
                    DataType = typeof(int),
                    IsRequired = false,
                    DefaultValue = 30,
                    Description = "Number of days before expiry to show warnings"
                },
                new()
                {
                    Name = "RequirePrescriptionValidation",
                    DisplayName = "Require Prescription Validation",
                    DataType = typeof(bool),
                    IsRequired = false,
                    DefaultValue = true,
                    Description = "Validate prescriptions for controlled medicines"
                }
            }
        };
    }

    public async Task InitializeAsync(Dictionary<string, object> configuration)
    {
        // Initialize plugin with configuration
        await Task.CompletedTask;
    }
}