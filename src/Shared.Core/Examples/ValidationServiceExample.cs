using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.DependencyInjection;

namespace Shared.Core.Examples;

/// <summary>
/// Example demonstrating how to use the ValidationService for comprehensive input validation
/// </summary>
public class ValidationServiceExample
{
    private readonly IValidationService _validationService;

    public ValidationServiceExample(IValidationService validationService)
    {
        _validationService = validationService;
    }

    /// <summary>
    /// Example: Validating product creation form
    /// </summary>
    public async Task<bool> ValidateProductCreationFormAsync()
    {
        Console.WriteLine("=== Product Creation Form Validation Example ===");

        // Simulate form data from UI
        var formData = new Dictionary<string, object?>
        {
            { "Name", "Wireless Headphones" },
            { "UnitPrice", "99.99" },
            { "Barcode", "WH123456789" },
            { "Category", "Electronics" },
            { "Description", "High-quality wireless headphones with noise cancellation" }
        };

        // Define validation rules for each field
        var validationRules = new Dictionary<string, FieldValidationRules>
        {
            { "Name", new FieldValidationRules 
                { 
                    IsRequired = true, 
                    MinLength = 2, 
                    MaxLength = 200 
                } 
            },
            { "UnitPrice", new FieldValidationRules 
                { 
                    IsRequired = true, 
                    MinValue = 0.01m, 
                    MaxValue = 10000m 
                } 
            },
            { "Barcode", new FieldValidationRules 
                { 
                    CustomRules = new List<string> { "BARCODE" },
                    MaxLength = 50 
                } 
            },
            { "Category", new FieldValidationRules 
                { 
                    MaxLength = 100,
                    AllowedValues = new List<string> { "Electronics", "Clothing", "Books", "Home", "Sports" }
                } 
            },
            { "Description", new FieldValidationRules 
                { 
                    MaxLength = 500 
                } 
            }
        };

        // Validate all fields
        var result = await _validationService.ValidateFieldsAsync(formData, validationRules);

        Console.WriteLine($"Form validation result: {(result.IsValid ? "VALID" : "INVALID")}");
        Console.WriteLine($"Valid fields: {result.ValidFieldCount}/{result.TotalFieldCount}");

        if (!result.IsValid)
        {
            Console.WriteLine("Validation errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Field}: {error.Message}");
            }
        }

        return result.IsValid;
    }

    /// <summary>
    /// Example: Real-time validation as user types
    /// </summary>
    public async Task DemonstrateRealTimeValidationAsync()
    {
        Console.WriteLine("\n=== Real-Time Validation Example ===");

        var context = new ValidationContext
        {
            EntityType = "Product",
            ContextData = new Dictionary<string, object>
            {
                { "FormType", "ProductCreation" },
                { "UserRole", "Manager" }
            }
        };

        // Simulate user typing different values
        var testInputs = new[]
        {
            ("email", "john@"),           // Invalid email (incomplete)
            ("email", "john@example"),    // Invalid email (no TLD)
            ("email", "john@example.com"), // Valid email
            ("phone", "123"),             // Invalid phone (too short)
            ("phone", "+1234567890"),     // Valid phone
            ("price", "-5"),              // Invalid price (negative)
            ("price", "25.99")            // Valid price
        };

        foreach (var (fieldName, value) in testInputs)
        {
            var result = await _validationService.ValidateRealTimeAsync(fieldName, value, context);
            
            var status = result.IsValid ? "✓" : "✗";
            var severity = result.Severity.ToString().ToUpper();
            
            Console.WriteLine($"{status} {fieldName}: '{value}' - {severity} - {result.InstantFeedback}");
            
            if (!string.IsNullOrEmpty(result.SuggestedCorrection))
            {
                Console.WriteLine($"    Suggestion: {result.SuggestedCorrection}");
            }
        }
    }

    /// <summary>
    /// Example: Validating a complete product entity with business rules
    /// </summary>
    public async Task<bool> ValidateProductEntityAsync()
    {
        Console.WriteLine("\n=== Product Entity Validation Example ===");

        var shopId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Name = "Smart Watch",
            UnitPrice = 299.99m,
            Barcode = "SW987654321",
            Category = "Electronics",
            IsWeightBased = false,
            ExpiryDate = DateTime.UtcNow.AddYears(2), // Future date
            IsActive = true
        };

        var result = await _validationService.ValidateProductAsync(product, shopId);

        Console.WriteLine($"Product validation result: {(result.IsValid ? "VALID" : "INVALID")}");
        Console.WriteLine($"Entity: {result.EntityType} (ID: {result.EntityId})");

        if (!result.IsValid)
        {
            Console.WriteLine("Validation errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Field}: {error.Message}");
            }

            if (result.BusinessRuleViolations.Any())
            {
                Console.WriteLine("Business rule violations:");
                foreach (var violation in result.BusinessRuleViolations)
                {
                    Console.WriteLine($"  - {violation}");
                }
            }
        }

        if (result.Warnings.Any())
        {
            Console.WriteLine("Warnings:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  - {warning.Field}: {warning.Message}");
            }
        }

        return result.IsValid;
    }

    /// <summary>
    /// Example: Validating stock levels before sale
    /// </summary>
    public async Task<bool> ValidateStockForSaleAsync()
    {
        Console.WriteLine("\n=== Stock Validation Example ===");

        var productId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var requestedQuantity = 5;

        var result = await _validationService.ValidateStockLevelsAsync(productId, requestedQuantity, shopId);

        Console.WriteLine($"Stock validation result: {(result.IsValid ? "SUFFICIENT" : "INSUFFICIENT")}");
        Console.WriteLine($"Product ID: {result.ProductId}");
        Console.WriteLine($"Requested: {result.RequestedQuantity}");
        Console.WriteLine($"Available: {result.AvailableQuantity}");
        Console.WriteLine($"Reserved: {result.ReservedQuantity}");

        if (!result.IsValid)
        {
            Console.WriteLine("Stock issues:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Message}");
            }

            if (!string.IsNullOrEmpty(result.RecommendedAction))
            {
                Console.WriteLine($"Recommended action: {result.RecommendedAction}");
            }
        }

        if (result.Warnings.Any())
        {
            Console.WriteLine("Stock warnings:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  - {warning.Message}");
            }
        }

        return result.IsValid;
    }

    /// <summary>
    /// Example: Form completion tracking
    /// </summary>
    public async Task DemonstrateFormCompletionTrackingAsync()
    {
        Console.WriteLine("\n=== Form Completion Tracking Example ===");

        var formData = new Dictionary<string, object?>
        {
            { "CustomerName", "John Doe" },
            { "Email", "john@example.com" },
            { "Phone", "" },              // Missing optional field
            { "Address", "" },            // Missing required field
            { "PaymentMethod", "Credit Card" }
        };

        var requiredFields = new List<string> { "CustomerName", "Email", "Address" };

        var result = await _validationService.ValidateFormCompletionAsync(formData, requiredFields);

        Console.WriteLine($"Form completion: {result.CompletionPercentage:F1}%");
        Console.WriteLine($"Can submit: {result.CanSubmit}");

        if (result.MissingRequiredFields.Any())
        {
            Console.WriteLine($"Missing required fields: {string.Join(", ", result.MissingRequiredFields)}");
        }

        if (!result.IsValid)
        {
            Console.WriteLine("Form errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Message}");
            }
        }
    }

    /// <summary>
    /// Example: Localized validation messages
    /// </summary>
    public async Task DemonstrateLocalizedMessagesAsync()
    {
        Console.WriteLine("\n=== Localized Validation Messages Example ===");

        var errorCodes = new[] { "FIELD_REQUIRED", "MIN_LENGTH", "INVALID_EMAIL", "INVALID_MOBILE_NUMBER" };
        var languages = new[] { "en" }; // Only English is implemented in this example

        foreach (var language in languages)
        {
            Console.WriteLine($"\nMessages in {language.ToUpper()}:");
            
            foreach (var errorCode in errorCodes)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "fieldName", "TestField" },
                    { "minLength", "5" },
                    { "actualLength", "2" }
                };

                var message = await _validationService.GetLocalizedValidationMessageAsync(errorCode, parameters, language);
                Console.WriteLine($"  {errorCode}: {message}");
            }
        }
    }

    /// <summary>
    /// Run all validation examples
    /// </summary>
    public async Task RunAllExamplesAsync()
    {
        Console.WriteLine("ValidationService Examples");
        Console.WriteLine("==========================");

        try
        {
            await ValidateProductCreationFormAsync();
            await DemonstrateRealTimeValidationAsync();
            await ValidateProductEntityAsync();
            await ValidateStockForSaleAsync();
            await DemonstrateFormCompletionTrackingAsync();
            await DemonstrateLocalizedMessagesAsync();

            Console.WriteLine("\n=== All Examples Completed Successfully ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError running examples: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}

/// <summary>
/// Console application to run validation examples
/// </summary>
public class ValidationExampleProgram
{
    public static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var validationService = serviceProvider.GetRequiredService<IValidationService>();

        // Run examples
        var example = new ValidationServiceExample(validationService);
        await example.RunAllExamplesAsync();

        serviceProvider.Dispose();
    }
}