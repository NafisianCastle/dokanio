using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Unit tests for ValidationService functionality
/// </summary>
public class ValidationServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IValidationService _validationService;
    private readonly ITestOutputHelper _output;

    public ValidationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
        
        _serviceProvider = services.BuildServiceProvider();
        _validationService = _serviceProvider.GetRequiredService<IValidationService>();
    }

    [Fact]
    public async Task ValidateFieldAsync_RequiredField_WithEmptyValue_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            IsRequired = true
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("TestField", "", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("FIELD_REQUIRED", result.Errors[0].Code);
        _output.WriteLine($"Validation result: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_RequiredField_WithValidValue_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            IsRequired = true,
            MinLength = 3,
            MaxLength = 50
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("TestField", "Valid Value", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Validation passed for valid required field");
    }

    [Fact]
    public async Task ValidateFieldAsync_MinLength_WithShortValue_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            MinLength = 5
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("TestField", "Hi", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("MIN_LENGTH", result.Errors[0].Code);
        _output.WriteLine($"Min length validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_MaxLength_WithLongValue_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            MaxLength = 10
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("TestField", "This is a very long string", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("MAX_LENGTH", result.Errors[0].Code);
        _output.WriteLine($"Max length validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_NumericRange_WithValidValue_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            MinValue = 10,
            MaxValue = 100
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("PriceField", "50", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Numeric range validation passed");
    }

    [Fact]
    public async Task ValidateFieldAsync_NumericRange_WithInvalidValue_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            MinValue = 10,
            MaxValue = 100
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("PriceField", "5", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("MIN_VALUE", result.Errors[0].Code);
        _output.WriteLine($"Numeric range validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_RegexPattern_WithValidEmail_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("EmailField", "test@example.com", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Email regex validation passed");
    }

    [Fact]
    public async Task ValidateFieldAsync_RegexPattern_WithInvalidEmail_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("EmailField", "invalid-email", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_FORMAT", result.Errors[0].Code);
        _output.WriteLine($"Email regex validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_AllowedValues_WithValidValue_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            AllowedValues = new List<string> { "Small", "Medium", "Large" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("SizeField", "Medium", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Allowed values validation passed");
    }

    [Fact]
    public async Task ValidateFieldAsync_AllowedValues_WithInvalidValue_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            AllowedValues = new List<string> { "Small", "Medium", "Large" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("SizeField", "ExtraLarge", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_VALUE", result.Errors[0].Code);
        _output.WriteLine($"Allowed values validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_CustomMobileNumberRule_WithValidNumber_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            CustomRules = new List<string> { "MOBILE_NUMBER" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("PhoneField", "+1234567890", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Mobile number validation passed");
    }

    [Fact]
    public async Task ValidateFieldAsync_CustomMobileNumberRule_WithInvalidNumber_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            CustomRules = new List<string> { "MOBILE_NUMBER" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("PhoneField", "invalid-phone", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_MOBILE_NUMBER", result.Errors[0].Code);
        _output.WriteLine($"Mobile number validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldAsync_CustomBarcodeRule_WithValidBarcode_ShouldReturnValid()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            CustomRules = new List<string> { "BARCODE" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("BarcodeField", "ABC123456789", rules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Barcode validation passed");
    }

    [Fact]
    public async Task ValidateFieldAsync_CustomBarcodeRule_WithInvalidBarcode_ShouldReturnError()
    {
        // Arrange
        var rules = new FieldValidationRules
        {
            CustomRules = new List<string> { "BARCODE" }
        };

        // Act
        var result = await _validationService.ValidateFieldAsync("BarcodeField", "invalid@barcode!", rules);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_BARCODE", result.Errors[0].Code);
        _output.WriteLine($"Barcode validation error: {result.Errors[0].Message}");
    }

    [Fact]
    public async Task ValidateFieldsAsync_MultipleFields_WithMixedValidation_ShouldReturnCorrectResults()
    {
        // Arrange
        var fieldValues = new Dictionary<string, object?>
        {
            { "Name", "John Doe" },
            { "Email", "john@example.com" },
            { "Age", "25" },
            { "Phone", "+1234567890" }
        };

        var validationRules = new Dictionary<string, FieldValidationRules>
        {
            { "Name", new FieldValidationRules { IsRequired = true, MinLength = 2, MaxLength = 100 } },
            { "Email", new FieldValidationRules { RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$" } },
            { "Age", new FieldValidationRules { MinValue = 18, MaxValue = 120 } },
            { "Phone", new FieldValidationRules { CustomRules = new List<string> { "MOBILE_NUMBER" } } }
        };

        // Act
        var result = await _validationService.ValidateFieldsAsync(fieldValues, validationRules);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(4, result.ValidFieldCount);
        Assert.Equal(4, result.TotalFieldCount);
        _output.WriteLine($"Multi-field validation: {result.ValidFieldCount}/{result.TotalFieldCount} fields valid");
    }

    [Fact]
    public async Task ValidateProductAsync_WithValidProduct_ShouldReturnValid()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Name = "Test Product",
            UnitPrice = 10.99m,
            Barcode = "ABC123456789",
            Category = "Electronics",
            IsActive = true
        };

        // Act
        var result = await _validationService.ValidateProductAsync(product, shopId);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Product validation passed");
    }

    [Fact]
    public async Task ValidateProductAsync_WithInvalidProduct_ShouldReturnErrors()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = Guid.NewGuid(), // Different shop ID
            Name = "", // Empty name
            UnitPrice = -5, // Negative price
            IsActive = true
        };

        // Act
        var result = await _validationService.ValidateProductAsync(product, shopId);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.NotEmpty(result.BusinessRuleViolations);
        _output.WriteLine($"Product validation failed with {result.Errors.Count} errors and {result.BusinessRuleViolations.Count} business rule violations");
    }

    [Fact]
    public async Task ValidateCustomerAsync_WithValidCustomer_ShouldReturnValid()
    {
        // Arrange
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            MembershipNumber = "MEMBER001",
            Email = "john@example.com",
            Phone = "+1234567890",
            IsActive = true
        };

        // Act
        var result = await _validationService.ValidateCustomerAsync(customer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        _output.WriteLine("Customer validation passed");
    }

    [Fact]
    public async Task ValidateCustomerAsync_WithInvalidCustomer_ShouldReturnErrors()
    {
        // Arrange
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "", // Empty name
            MembershipNumber = "", // Empty membership number
            Email = "invalid-email", // Invalid email format
            Phone = "invalid-phone", // Invalid phone format
            IsActive = true
        };

        // Act
        var result = await _validationService.ValidateCustomerAsync(customer);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        _output.WriteLine($"Customer validation failed with {result.Errors.Count} errors");
    }

    [Fact]
    public async Task ValidateRealTimeAsync_WithValidInput_ShouldReturnPositiveFeedback()
    {
        // Arrange
        var context = new ValidationContext
        {
            EntityType = "UI",
            ContextData = new Dictionary<string, object> { { "ControlType", "TextBox" } }
        };

        // Act
        var result = await _validationService.ValidateRealTimeAsync("name", "John Doe", context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ValidationSeverity.Info, result.Severity);
        Assert.Contains("Valid", result.InstantFeedback ?? "");
        _output.WriteLine($"Real-time validation feedback: {result.InstantFeedback}");
    }

    [Fact]
    public async Task ValidateRealTimeAsync_WithInvalidInput_ShouldReturnErrorFeedback()
    {
        // Arrange
        var context = new ValidationContext
        {
            EntityType = "UI",
            ContextData = new Dictionary<string, object> { { "ControlType", "TextBox" } }
        };

        // Act
        var result = await _validationService.ValidateRealTimeAsync("name", "", context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Error, result.Severity);
        Assert.NotNull(result.InstantFeedback);
        _output.WriteLine($"Real-time validation error: {result.InstantFeedback}");
    }

    [Fact]
    public async Task ValidateFormCompletionAsync_WithCompleteForm_ShouldReturnValid()
    {
        // Arrange
        var formData = new Dictionary<string, object?>
        {
            { "Name", "John Doe" },
            { "Email", "john@example.com" },
            { "Phone", "+1234567890" }
        };
        var requiredFields = new List<string> { "Name", "Email" };

        // Act
        var result = await _validationService.ValidateFormCompletionAsync(formData, requiredFields);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.CanSubmit);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Equal(100, result.CompletionPercentage);
        _output.WriteLine($"Form completion: {result.CompletionPercentage}% complete, can submit: {result.CanSubmit}");
    }

    [Fact]
    public async Task ValidateFormCompletionAsync_WithIncompleteForm_ShouldReturnInvalid()
    {
        // Arrange
        var formData = new Dictionary<string, object?>
        {
            { "Name", "John Doe" },
            { "Email", "" }, // Missing required field
            { "Phone", "+1234567890" }
        };
        var requiredFields = new List<string> { "Name", "Email" };

        // Act
        var result = await _validationService.ValidateFormCompletionAsync(formData, requiredFields);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.CanSubmit);
        Assert.Single(result.MissingRequiredFields);
        Assert.Contains("Email", result.MissingRequiredFields);
        _output.WriteLine($"Form completion: {result.CompletionPercentage}% complete, missing fields: {string.Join(", ", result.MissingRequiredFields)}");
    }

    [Fact]
    public async Task GetLocalizedValidationMessageAsync_WithValidErrorCode_ShouldReturnLocalizedMessage()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "fieldName", "TestField" }
        };

        // Act
        var message = await _validationService.GetLocalizedValidationMessageAsync("FIELD_REQUIRED", parameters, "en");

        // Assert
        Assert.NotNull(message);
        Assert.Contains("TestField", message);
        Assert.Contains("required", message.ToLowerInvariant());
        _output.WriteLine($"Localized message: {message}");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}