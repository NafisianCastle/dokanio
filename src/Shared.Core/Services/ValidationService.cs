using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shared.Core.Services;

/// <summary>
/// Comprehensive validation service implementation
/// </summary>
public class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly Dictionary<string, ValidationMessageTemplate> _messageTemplates;
    private readonly Dictionary<string, ValidationRuleDefinition> _validationRules;

    public ValidationService(
        ILogger<ValidationService> logger,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ICustomerRepository customerRepository,
        ISaleRepository saleRepository)
    {
        _logger = logger;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _customerRepository = customerRepository;
        _saleRepository = saleRepository;
        _messageTemplates = InitializeMessageTemplates();
        _validationRules = InitializeValidationRules();
    }

    #region Field-Level Validation

    public async Task<FieldValidationResult> ValidateFieldAsync(string fieldName, object? value, FieldValidationRules validationRules)
    {
        var result = new FieldValidationResult
        {
            FieldName = fieldName,
            ValidatedValue = value
        };

        try
        {
            // Required field validation
            if (validationRules.IsRequired && IsNullOrEmpty(value))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "FIELD_REQUIRED",
                    Message = await GetLocalizedValidationMessageAsync("FIELD_REQUIRED", new Dictionary<string, object> { { "fieldName", fieldName } }),
                    Field = fieldName,
                    Value = value,
                    Severity = ValidationSeverity.Error
                });
                return result;
            }

            // Skip other validations if field is not required and is empty
            if (!validationRules.IsRequired && IsNullOrEmpty(value))
            {
                return result;
            }

            var stringValue = value?.ToString() ?? string.Empty;

            // Length validation
            if (validationRules.MinLength.HasValue && stringValue.Length < validationRules.MinLength.Value)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "MIN_LENGTH",
                    Message = await GetLocalizedValidationMessageAsync("MIN_LENGTH", new Dictionary<string, object> 
                    { 
                        { "fieldName", fieldName }, 
                        { "minLength", validationRules.MinLength.Value },
                        { "actualLength", stringValue.Length }
                    }),
                    Field = fieldName,
                    Value = value,
                    Severity = ValidationSeverity.Error
                });
            }

            if (validationRules.MaxLength.HasValue && stringValue.Length > validationRules.MaxLength.Value)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "MAX_LENGTH",
                    Message = await GetLocalizedValidationMessageAsync("MAX_LENGTH", new Dictionary<string, object> 
                    { 
                        { "fieldName", fieldName }, 
                        { "maxLength", validationRules.MaxLength.Value },
                        { "actualLength", stringValue.Length }
                    }),
                    Field = fieldName,
                    Value = value,
                    Severity = ValidationSeverity.Error
                });
            }

            // Numeric validation
            if (validationRules.MinValue.HasValue || validationRules.MaxValue.HasValue)
            {
                if (decimal.TryParse(stringValue, out var numericValue))
                {
                    if (validationRules.MinValue.HasValue && numericValue < validationRules.MinValue.Value)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Code = "MIN_VALUE",
                            Message = await GetLocalizedValidationMessageAsync("MIN_VALUE", new Dictionary<string, object> 
                            { 
                                { "fieldName", fieldName }, 
                                { "minValue", validationRules.MinValue.Value },
                                { "actualValue", numericValue }
                            }),
                            Field = fieldName,
                            Value = value,
                            Severity = ValidationSeverity.Error
                        });
                    }

                    if (validationRules.MaxValue.HasValue && numericValue > validationRules.MaxValue.Value)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Code = "MAX_VALUE",
                            Message = await GetLocalizedValidationMessageAsync("MAX_VALUE", new Dictionary<string, object> 
                            { 
                                { "fieldName", fieldName }, 
                                { "maxValue", validationRules.MaxValue.Value },
                                { "actualValue", numericValue }
                            }),
                            Field = fieldName,
                            Value = value,
                            Severity = ValidationSeverity.Error
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(stringValue))
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Code = "INVALID_NUMBER",
                        Message = await GetLocalizedValidationMessageAsync("INVALID_NUMBER", new Dictionary<string, object> { { "fieldName", fieldName } }),
                        Field = fieldName,
                        Value = value,
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Regex pattern validation
            if (!string.IsNullOrEmpty(validationRules.RegexPattern) && !string.IsNullOrEmpty(stringValue))
            {
                if (!Regex.IsMatch(stringValue, validationRules.RegexPattern))
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Code = "INVALID_FORMAT",
                        Message = await GetLocalizedValidationMessageAsync("INVALID_FORMAT", new Dictionary<string, object> { { "fieldName", fieldName } }),
                        Field = fieldName,
                        Value = value,
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Allowed values validation
            if (validationRules.AllowedValues?.Any() == true && !string.IsNullOrEmpty(stringValue))
            {
                if (!validationRules.AllowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Code = "INVALID_VALUE",
                        Message = await GetLocalizedValidationMessageAsync("INVALID_VALUE", new Dictionary<string, object> 
                        { 
                            { "fieldName", fieldName },
                            { "allowedValues", string.Join(", ", validationRules.AllowedValues) }
                        }),
                        Field = fieldName,
                        Value = value,
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Custom rules validation
            foreach (var customRule in validationRules.CustomRules)
            {
                var customResult = await ValidateCustomRuleAsync(fieldName, value, customRule, validationRules.RuleParameters);
                if (!customResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(customResult.Errors);
                    result.Warnings.AddRange(customResult.Warnings);
                }
                result.AppliedRules.Add(customRule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating field {FieldName}", fieldName);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during validation",
                Field = fieldName,
                Value = value,
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<MultiFieldValidationResult> ValidateFieldsAsync(Dictionary<string, object?> fieldValues, Dictionary<string, FieldValidationRules> validationRules)
    {
        var result = new MultiFieldValidationResult
        {
            TotalFieldCount = fieldValues.Count
        };

        foreach (var fieldValue in fieldValues)
        {
            if (validationRules.TryGetValue(fieldValue.Key, out var rules))
            {
                var fieldResult = await ValidateFieldAsync(fieldValue.Key, fieldValue.Value, rules);
                result.FieldResults[fieldValue.Key] = fieldResult;

                if (fieldResult.IsValid)
                {
                    result.ValidFieldCount++;
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.AddRange(fieldResult.Errors);
                    result.Warnings.AddRange(fieldResult.Warnings);
                }
            }
        }

        return result;
    }

    #endregion

    #region Entity Validation

    public async Task<EntityValidationResult> ValidateProductAsync(Product product, Guid shopId)
    {
        var result = new EntityValidationResult
        {
            EntityType = nameof(Product),
            EntityId = product.Id
        };

        try
        {
            // Basic field validation
            var fieldRules = new Dictionary<string, FieldValidationRules>
            {
                { nameof(Product.Name), new FieldValidationRules { IsRequired = true, MinLength = 1, MaxLength = 200 } },
                { nameof(Product.UnitPrice), new FieldValidationRules { IsRequired = true, MinValue = 0 } },
                { nameof(Product.Barcode), new FieldValidationRules { MaxLength = 50 } },
                { nameof(Product.Category), new FieldValidationRules { MaxLength = 100 } }
            };

            var fieldValues = new Dictionary<string, object?>
            {
                { nameof(Product.Name), product.Name },
                { nameof(Product.UnitPrice), product.UnitPrice },
                { nameof(Product.Barcode), product.Barcode },
                { nameof(Product.Category), product.Category }
            };

            var fieldValidation = await ValidateFieldsAsync(fieldValues, fieldRules);
            result.PropertyResults = fieldValidation.FieldResults;
            result.IsValid = fieldValidation.IsValid;
            result.Errors.AddRange(fieldValidation.Errors);
            result.Warnings.AddRange(fieldValidation.Warnings);

            // Business rule validation
            if (product.ShopId != shopId)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Product does not belong to the specified shop");
                result.Errors.Add(new ValidationError
                {
                    Code = "INVALID_SHOP",
                    Message = "Product does not belong to the specified shop",
                    Field = nameof(Product.ShopId),
                    Value = product.ShopId,
                    Severity = ValidationSeverity.Error
                });
            }

            // Weight-based product validation
            if (product.IsWeightBased)
            {
                if (!product.RatePerKilogram.HasValue || product.RatePerKilogram <= 0)
                {
                    result.IsValid = false;
                    result.BusinessRuleViolations.Add("Weight-based products must have a valid rate per kilogram");
                    result.Errors.Add(new ValidationError
                    {
                        Code = "INVALID_WEIGHT_RATE",
                        Message = "Weight-based products must have a valid rate per kilogram",
                        Field = nameof(Product.RatePerKilogram),
                        Value = product.RatePerKilogram,
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Expiry date validation for products with expiry
            if (product.ExpiryDate.HasValue && product.ExpiryDate <= DateTime.UtcNow)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "EXPIRED_PRODUCT",
                    Message = "Product has expired",
                    Field = nameof(Product.ExpiryDate),
                    Value = product.ExpiryDate,
                    Suggestion = "Consider removing expired products from active inventory"
                });
            }

            // Barcode uniqueness validation (if barcode is provided)
            if (!string.IsNullOrEmpty(product.Barcode))
            {
                var existingProduct = await _productRepository.GetByBarcodeAsync(product.Barcode);
                if (existingProduct != null && existingProduct.Id != product.Id && existingProduct.ShopId == shopId)
                {
                    result.IsValid = false;
                    result.BusinessRuleViolations.Add("Barcode must be unique within the shop");
                    result.Errors.Add(new ValidationError
                    {
                        Code = "DUPLICATE_BARCODE",
                        Message = "Barcode already exists for another product in this shop",
                        Field = nameof(Product.Barcode),
                        Value = product.Barcode,
                        Severity = ValidationSeverity.Error
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating product {ProductId}", product.Id);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during product validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<EntityValidationResult> ValidateSaleAsync(Sale sale)
    {
        var result = new EntityValidationResult
        {
            EntityType = nameof(Sale),
            EntityId = sale.Id
        };

        try
        {
            // Basic field validation
            var fieldRules = new Dictionary<string, FieldValidationRules>
            {
                { nameof(Sale.InvoiceNumber), new FieldValidationRules { IsRequired = true, MinLength = 1, MaxLength = 50 } },
                { nameof(Sale.TotalAmount), new FieldValidationRules { IsRequired = true, MinValue = 0 } },
                { nameof(Sale.DiscountAmount), new FieldValidationRules { MinValue = 0 } },
                { nameof(Sale.TaxAmount), new FieldValidationRules { MinValue = 0 } }
            };

            var fieldValues = new Dictionary<string, object?>
            {
                { nameof(Sale.InvoiceNumber), sale.InvoiceNumber },
                { nameof(Sale.TotalAmount), sale.TotalAmount },
                { nameof(Sale.DiscountAmount), sale.DiscountAmount },
                { nameof(Sale.TaxAmount), sale.TaxAmount }
            };

            var fieldValidation = await ValidateFieldsAsync(fieldValues, fieldRules);
            result.PropertyResults = fieldValidation.FieldResults;
            result.IsValid = fieldValidation.IsValid;
            result.Errors.AddRange(fieldValidation.Errors);
            result.Warnings.AddRange(fieldValidation.Warnings);

            // Business rule validation
            if (sale.Items?.Any() != true)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Sale must have at least one item");
                result.Errors.Add(new ValidationError
                {
                    Code = "NO_SALE_ITEMS",
                    Message = "Sale must have at least one item",
                    Field = "Items",
                    Severity = ValidationSeverity.Error
                });
            }

            // Discount validation
            if (sale.DiscountAmount > sale.TotalAmount)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Discount amount cannot exceed total amount");
                result.Errors.Add(new ValidationError
                {
                    Code = "EXCESSIVE_DISCOUNT",
                    Message = "Discount amount cannot exceed total amount",
                    Field = nameof(Sale.DiscountAmount),
                    Value = sale.DiscountAmount,
                    Severity = ValidationSeverity.Error
                });
            }

            // Invoice number uniqueness validation
            var existingSale = await _saleRepository.GetByInvoiceNumberAsync(sale.InvoiceNumber);
            if (existingSale != null && existingSale.Id != sale.Id && existingSale.ShopId == sale.ShopId)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Invoice number must be unique within the shop");
                result.Errors.Add(new ValidationError
                {
                    Code = "DUPLICATE_INVOICE",
                    Message = "Invoice number already exists for another sale in this shop",
                    Field = nameof(Sale.InvoiceNumber),
                    Value = sale.InvoiceNumber,
                    Severity = ValidationSeverity.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating sale {SaleId}", sale.Id);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during sale validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<EntityValidationResult> ValidateSaleItemAsync(SaleItem saleItem, Guid shopId)
    {
        var result = new EntityValidationResult
        {
            EntityType = nameof(SaleItem),
            EntityId = saleItem.Id
        };

        try
        {
            // Basic field validation
            var fieldRules = new Dictionary<string, FieldValidationRules>
            {
                { nameof(SaleItem.Quantity), new FieldValidationRules { IsRequired = true, MinValue = 1 } },
                { nameof(SaleItem.UnitPrice), new FieldValidationRules { IsRequired = true, MinValue = 0 } },
                { nameof(SaleItem.TotalPrice), new FieldValidationRules { IsRequired = true, MinValue = 0 } }
            };

            var fieldValues = new Dictionary<string, object?>
            {
                { nameof(SaleItem.Quantity), saleItem.Quantity },
                { nameof(SaleItem.UnitPrice), saleItem.UnitPrice },
                { nameof(SaleItem.TotalPrice), saleItem.TotalPrice }
            };

            var fieldValidation = await ValidateFieldsAsync(fieldValues, fieldRules);
            result.PropertyResults = fieldValidation.FieldResults;
            result.IsValid = fieldValidation.IsValid;
            result.Errors.AddRange(fieldValidation.Errors);
            result.Warnings.AddRange(fieldValidation.Warnings);

            // Stock validation
            var stockValidation = await ValidateStockLevelsAsync(saleItem.ProductId, saleItem.Quantity, shopId);
            if (!stockValidation.IsValid)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Insufficient stock for the requested quantity");
                result.Errors.AddRange(stockValidation.Errors);
                result.Warnings.AddRange(stockValidation.Warnings);
            }

            // Weight-based item validation
            if (saleItem.Weight.HasValue && saleItem.RatePerKilogram.HasValue)
            {
                var expectedTotal = saleItem.Weight.Value * saleItem.RatePerKilogram.Value;
                if (Math.Abs(saleItem.TotalPrice - expectedTotal) > 0.01m) // Allow for rounding differences
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Code = "PRICE_CALCULATION_MISMATCH",
                        Message = $"Total price ({saleItem.TotalPrice:C}) does not match weight calculation ({expectedTotal:C})",
                        Field = nameof(SaleItem.TotalPrice),
                        Value = saleItem.TotalPrice,
                        Suggestion = $"Expected total: {expectedTotal:C}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating sale item {SaleItemId}", saleItem.Id);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during sale item validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<EntityValidationResult> ValidateCustomerAsync(Customer customer)
    {
        var result = new EntityValidationResult
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id
        };

        try
        {
            // Basic field validation
            var fieldRules = new Dictionary<string, FieldValidationRules>
            {
                { nameof(Customer.Name), new FieldValidationRules { IsRequired = true, MinLength = 1, MaxLength = 200 } },
                { nameof(Customer.MembershipNumber), new FieldValidationRules { IsRequired = true, MinLength = 1, MaxLength = 50 } },
                { nameof(Customer.Email), new FieldValidationRules { MaxLength = 255, RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$" } },
                { nameof(Customer.Phone), new FieldValidationRules { MaxLength = 20, RegexPattern = @"^\+?[\d\s\-\(\)]+$" } }
            };

            var fieldValues = new Dictionary<string, object?>
            {
                { nameof(Customer.Name), customer.Name },
                { nameof(Customer.MembershipNumber), customer.MembershipNumber },
                { nameof(Customer.Email), customer.Email },
                { nameof(Customer.Phone), customer.Phone }
            };

            var fieldValidation = await ValidateFieldsAsync(fieldValues, fieldRules);
            result.PropertyResults = fieldValidation.FieldResults;
            result.IsValid = fieldValidation.IsValid;
            result.Errors.AddRange(fieldValidation.Errors);
            result.Warnings.AddRange(fieldValidation.Warnings);

            // Membership number uniqueness validation
            var existingCustomer = await _customerRepository.GetByMembershipNumberAsync(customer.MembershipNumber);
            if (existingCustomer != null && existingCustomer.Id != customer.Id)
            {
                result.IsValid = false;
                result.BusinessRuleViolations.Add("Membership number must be unique");
                result.Errors.Add(new ValidationError
                {
                    Code = "DUPLICATE_MEMBERSHIP",
                    Message = "Membership number already exists for another customer",
                    Field = nameof(Customer.MembershipNumber),
                    Value = customer.MembershipNumber,
                    Severity = ValidationSeverity.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating customer {CustomerId}", customer.Id);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during customer validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    #endregion

    #region Business Rule Validation

    public async Task<StockValidationResult> ValidateStockLevelsAsync(Guid productId, decimal requestedQuantity, Guid shopId)
    {
        var result = new StockValidationResult
        {
            ProductId = productId,
            RequestedQuantity = requestedQuantity
        };

        try
        {
            var stockEntry = await _stockRepository.GetByProductIdAsync(productId);
            var totalAvailable = stockEntry?.Quantity ?? 0;

            result.AvailableQuantity = totalAvailable;
            result.ReservedQuantity = 0; // No reserved quantity tracking in current Stock entity
            result.HasSufficientStock = result.AvailableQuantity >= requestedQuantity;

            if (!result.HasSufficientStock)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "INSUFFICIENT_STOCK",
                    Message = $"Insufficient stock. Available: {result.AvailableQuantity}, Requested: {requestedQuantity}",
                    Field = "Quantity",
                    Value = requestedQuantity,
                    Severity = ValidationSeverity.Error
                });
                result.RecommendedAction = $"Reduce quantity to {result.AvailableQuantity} or restock the product";
            }
            else if (result.AvailableQuantity - requestedQuantity < 5) // Low stock warning
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "LOW_STOCK",
                    Message = $"Low stock warning. Only {result.AvailableQuantity - requestedQuantity} units will remain after this sale",
                    Field = "Quantity",
                    Value = requestedQuantity,
                    Suggestion = "Consider restocking this product soon"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating stock levels for product {ProductId}", productId);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "STOCK_VALIDATION_ERROR",
                Message = "An error occurred during stock validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<ExpiryValidationResult> ValidateProductExpiryAsync(Guid productId, string? batchNumber = null)
    {
        var result = new ExpiryValidationResult
        {
            ProductId = productId,
            BatchNumber = batchNumber
        };

        try
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product?.ExpiryDate.HasValue == true)
            {
                result.ExpiryDate = product.ExpiryDate.Value;
                var daysUntilExpiry = (product.ExpiryDate.Value - DateTime.UtcNow).Days;
                result.DaysUntilExpiry = daysUntilExpiry;

                if (daysUntilExpiry < 0)
                {
                    result.IsExpired = true;
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Code = "PRODUCT_EXPIRED",
                        Message = $"Product expired {Math.Abs(daysUntilExpiry)} days ago",
                        Field = "ExpiryDate",
                        Value = product.ExpiryDate,
                        Severity = ValidationSeverity.Error
                    });
                    result.ExpiryWarningMessage = "This product has expired and should not be sold";
                }
                else if (daysUntilExpiry <= 7)
                {
                    result.IsNearExpiry = true;
                    result.Warnings.Add(new ValidationWarning
                    {
                        Code = "NEAR_EXPIRY",
                        Message = $"Product expires in {daysUntilExpiry} days",
                        Field = "ExpiryDate",
                        Value = product.ExpiryDate,
                        Suggestion = "Consider offering a discount or prioritizing this product for sale"
                    });
                    result.ExpiryWarningMessage = $"Product expires in {daysUntilExpiry} days";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating product expiry for product {ProductId}", productId);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "EXPIRY_VALIDATION_ERROR",
                Message = "An error occurred during expiry validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<PricingValidationResult> ValidatePricingRulesAsync(List<SaleItem> saleItems, Guid shopId)
    {
        var result = new PricingValidationResult();

        try
        {
            result.TotalAmount = saleItems.Sum(item => item.TotalPrice);

            foreach (var item in saleItems)
            {
                // Validate reasonable pricing (not too high or too low)
                if (item.UnitPrice > 10000) // Arbitrary high limit
                {
                    result.HasUnreasonablePricing = true;
                    result.RuleViolations.Add(new PricingRuleViolation
                    {
                        RuleName = "Maximum Price Limit",
                        ViolationType = "Excessive Price",
                        Description = $"Unit price {item.UnitPrice:C} exceeds reasonable limit",
                        ExpectedValue = 10000,
                        ActualValue = item.UnitPrice,
                        Severity = ValidationSeverity.Warning
                    });
                }

                if (item.UnitPrice < 0.01m) // Minimum price
                {
                    result.IsValid = false;
                    result.RuleViolations.Add(new PricingRuleViolation
                    {
                        RuleName = "Minimum Price Limit",
                        ViolationType = "Price Too Low",
                        Description = $"Unit price {item.UnitPrice:C} is below minimum allowed",
                        ExpectedValue = 0.01m,
                        ActualValue = item.UnitPrice,
                        Severity = ValidationSeverity.Error
                    });
                }

                // Validate weight-based pricing calculations
                if (item.Weight.HasValue && item.RatePerKilogram.HasValue)
                {
                    var expectedPrice = item.Weight.Value * item.RatePerKilogram.Value;
                    var priceDifference = Math.Abs(item.TotalPrice - expectedPrice);
                    
                    if (priceDifference > 0.05m) // Allow 5 cent tolerance
                    {
                        result.SuggestedAdjustments.Add(new PricingAdjustment
                        {
                            AdjustmentType = "Weight-based Calculation",
                            AdjustmentAmount = expectedPrice - item.TotalPrice,
                            Reason = "Price does not match weight calculation",
                            OriginalValue = item.TotalPrice,
                            AdjustedValue = expectedPrice
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating pricing rules for shop {ShopId}", shopId);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "PRICING_VALIDATION_ERROR",
                Message = "An error occurred during pricing validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    public async Task<DiscountValidationResult> ValidateDiscountApplicationAsync(List<Discount> discounts, decimal saleTotal, Guid? customerId = null)
    {
        var result = new DiscountValidationResult
        {
            IsValid = true,
            ValidationErrors = new List<string>()
        };

        try
        {
            decimal totalDiscountAmount = 0;

            foreach (var discount in discounts)
            {
                // Validate discount is active and within date range
                if (!discount.IsActive || discount.StartDate > DateTime.UtcNow || discount.EndDate < DateTime.UtcNow)
                {
                    result.IsValid = false;
                    result.ValidationErrors.Add($"Discount '{discount.Name}' is not active or outside valid date range");
                    continue;
                }

                // Calculate discount amount
                decimal discountAmount = discount.Type == DiscountType.Percentage 
                    ? saleTotal * (discount.Value / 100) 
                    : discount.Value;

                totalDiscountAmount += discountAmount;

                // Validate minimum purchase amount
                if (discount.MinimumAmount.HasValue && saleTotal < discount.MinimumAmount.Value)
                {
                    result.IsValid = false;
                    result.ValidationErrors.Add($"Minimum purchase amount of {discount.MinimumAmount.Value:C} not met for discount '{discount.Name}'");
                }
            }

            // Validate total discount doesn't exceed sale total
            if (totalDiscountAmount > saleTotal)
            {
                result.IsValid = false;
                result.ValidationErrors.Add($"Total discount amount ({totalDiscountAmount:C}) exceeds sale total ({saleTotal:C})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating discount application");
            result.IsValid = false;
            result.ValidationErrors.Add("An error occurred during discount validation");
        }

        return result;
    }

    #endregion

    #region Real-Time Validation

    public async Task<RealTimeValidationResult> ValidateRealTimeAsync(string fieldName, object? currentValue, ValidationContext context)
    {
        var result = new RealTimeValidationResult
        {
            FieldName = fieldName
        };

        try
        {
            // Get validation rules for the field based on context
            var rules = GetValidationRulesForField(fieldName, context);
            if (rules == null)
            {
                result.ShowFeedback = false;
                return result;
            }

            var fieldResult = await ValidateFieldAsync(fieldName, currentValue, rules);
            result.IsValid = fieldResult.IsValid;
            result.Errors.AddRange(fieldResult.Errors);
            result.Warnings.AddRange(fieldResult.Warnings);

            // Determine severity and feedback
            if (fieldResult.Errors.Any())
            {
                result.Severity = ValidationSeverity.Error;
                result.InstantFeedback = fieldResult.Errors.First().Message;
            }
            else if (fieldResult.Warnings.Any())
            {
                result.Severity = ValidationSeverity.Warning;
                result.InstantFeedback = fieldResult.Warnings.First().Message;
            }
            else
            {
                result.Severity = ValidationSeverity.Info;
                result.InstantFeedback = "✓ Valid";
            }

            // Provide suggestions for common corrections
            if (!result.IsValid && !string.IsNullOrEmpty(currentValue?.ToString()))
            {
                result.SuggestedCorrection = GenerateSuggestion(fieldName, currentValue, rules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in real-time validation for field {FieldName}", fieldName);
            result.IsValid = false;
            result.Severity = ValidationSeverity.Error;
            result.InstantFeedback = "Validation error occurred";
        }

        return result;
    }

    public async Task<FormValidationResult> ValidateFormCompletionAsync(Dictionary<string, object?> formData, List<string> requiredFields)
    {
        var result = new FormValidationResult
        {
            FormName = "Generic Form"
        };

        try
        {
            var missingFields = requiredFields.Where(field => 
                !formData.ContainsKey(field) || IsNullOrEmpty(formData[field])).ToList();

            result.MissingRequiredFields = missingFields;
            result.CanSubmit = !missingFields.Any();

            if (missingFields.Any())
            {
                result.IsValid = false;
                foreach (var field in missingFields)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "REQUIRED_FIELD_MISSING",
                        Message = $"Required field '{field}' is missing",
                        Field = field,
                        Severity = ValidationSeverity.Error
                    });
                }
            }

            // Calculate completion percentage
            var completedFields = formData.Count(kvp => !IsNullOrEmpty(kvp.Value));
            result.CompletionPercentage = formData.Count > 0 ? (decimal)completedFields / formData.Count * 100 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating form completion");
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "FORM_VALIDATION_ERROR",
                Message = "An error occurred during form validation",
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    #endregion

    #region Localization Support

    public async Task<string> GetLocalizedValidationMessageAsync(string errorCode, Dictionary<string, object>? parameters = null, string? languageCode = null)
    {
        try
        {
            languageCode ??= CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            
            var templateKey = $"{errorCode}_{languageCode}";
            if (!_messageTemplates.TryGetValue(templateKey, out var template))
            {
                // Fallback to English
                templateKey = $"{errorCode}_en";
                if (!_messageTemplates.TryGetValue(templateKey, out template))
                {
                    return $"Validation error: {errorCode}";
                }
            }

            var message = template.MessageTemplate;
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    message = message.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? "");
                }
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting localized validation message for {ErrorCode}", errorCode);
            return $"Validation error: {errorCode}";
        }
    }

    public async Task<Dictionary<string, string>> GetValidationMessagesAsync(string languageCode)
    {
        try
        {
            return _messageTemplates
                .Where(kvp => kvp.Key.EndsWith($"_{languageCode}"))
                .ToDictionary(
                    kvp => kvp.Key.Substring(0, kvp.Key.LastIndexOf('_')),
                    kvp => kvp.Value.MessageTemplate
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting validation messages for language {LanguageCode}", languageCode);
            return new Dictionary<string, string>();
        }
    }

    #endregion

    #region Private Helper Methods

    private static bool IsNullOrEmpty(object? value)
    {
        return value == null || 
               (value is string str && string.IsNullOrWhiteSpace(str)) ||
               (value is Guid guid && guid == Guid.Empty);
    }

    private async Task<FieldValidationResult> ValidateCustomRuleAsync(string fieldName, object? value, string ruleName, Dictionary<string, object> parameters)
    {
        var result = new FieldValidationResult
        {
            FieldName = fieldName,
            ValidatedValue = value
        };

        try
        {
            switch (ruleName.ToUpperInvariant())
            {
                case "MOBILE_NUMBER":
                    if (value is string mobileNumber && !string.IsNullOrEmpty(mobileNumber))
                    {
                        // Basic mobile number validation
                        var cleanNumber = Regex.Replace(mobileNumber, @"[\s\-\(\)]", "");
                        if (!Regex.IsMatch(cleanNumber, @"^\+?[\d]{10,15}$"))
                        {
                            result.IsValid = false;
                            result.Errors.Add(new ValidationError
                            {
                                Code = "INVALID_MOBILE_NUMBER",
                                Message = "Invalid mobile number format",
                                Field = fieldName,
                                Value = value,
                                Severity = ValidationSeverity.Error
                            });
                        }
                    }
                    break;

                case "BARCODE":
                    if (value is string barcode && !string.IsNullOrEmpty(barcode))
                    {
                        // Basic barcode validation (alphanumeric, specific length ranges)
                        if (!Regex.IsMatch(barcode, @"^[A-Za-z0-9]{8,20}$"))
                        {
                            result.IsValid = false;
                            result.Errors.Add(new ValidationError
                            {
                                Code = "INVALID_BARCODE",
                                Message = "Barcode must be 8-20 alphanumeric characters",
                                Field = fieldName,
                                Value = value,
                                Severity = ValidationSeverity.Error
                            });
                        }
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown custom validation rule: {RuleName}", ruleName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing custom validation rule {RuleName}", ruleName);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Code = "CUSTOM_RULE_ERROR",
                Message = $"Error executing custom validation rule: {ruleName}",
                Field = fieldName,
                Value = value,
                Severity = ValidationSeverity.Error
            });
        }

        return result;
    }

    private FieldValidationRules? GetValidationRulesForField(string fieldName, ValidationContext context)
    {
        // This would typically be configured based on entity type and field
        // For now, return basic rules based on common field names
        return fieldName.ToLowerInvariant() switch
        {
            "name" => new FieldValidationRules { IsRequired = true, MinLength = 1, MaxLength = 200 },
            "email" => new FieldValidationRules { RegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", MaxLength = 255 },
            "phone" or "mobilenumber" => new FieldValidationRules { CustomRules = new List<string> { "MOBILE_NUMBER" }, MaxLength = 20 },
            "barcode" => new FieldValidationRules { CustomRules = new List<string> { "BARCODE" }, MaxLength = 50 },
            "unitprice" or "price" => new FieldValidationRules { IsRequired = true, MinValue = 0 },
            "quantity" => new FieldValidationRules { IsRequired = true, MinValue = 1 },
            _ => null
        };
    }

    private string? GenerateSuggestion(string fieldName, object? currentValue, FieldValidationRules rules)
    {
        var stringValue = currentValue?.ToString() ?? "";

        if (rules.MinLength.HasValue && stringValue.Length < rules.MinLength.Value)
        {
            return $"Add {rules.MinLength.Value - stringValue.Length} more characters";
        }

        if (rules.MaxLength.HasValue && stringValue.Length > rules.MaxLength.Value)
        {
            return $"Remove {stringValue.Length - rules.MaxLength.Value} characters";
        }

        if (rules.AllowedValues?.Any() == true)
        {
            var closest = rules.AllowedValues
                .OrderBy(v => LevenshteinDistance(stringValue, v))
                .FirstOrDefault();
            if (closest != null)
            {
                return $"Did you mean '{closest}'?";
            }
        }

        return null;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(
                    matrix[i - 1, j] + 1,
                    matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private Dictionary<string, ValidationMessageTemplate> InitializeMessageTemplates()
    {
        return new Dictionary<string, ValidationMessageTemplate>
        {
            // English messages
            { "FIELD_REQUIRED_en", new ValidationMessageTemplate { ErrorCode = "FIELD_REQUIRED", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' is required" } },
            { "MIN_LENGTH_en", new ValidationMessageTemplate { ErrorCode = "MIN_LENGTH", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must be at least {minLength} characters (current: {actualLength})" } },
            { "MAX_LENGTH_en", new ValidationMessageTemplate { ErrorCode = "MAX_LENGTH", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must not exceed {maxLength} characters (current: {actualLength})" } },
            { "MIN_VALUE_en", new ValidationMessageTemplate { ErrorCode = "MIN_VALUE", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must be at least {minValue} (current: {actualValue})" } },
            { "MAX_VALUE_en", new ValidationMessageTemplate { ErrorCode = "MAX_VALUE", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must not exceed {maxValue} (current: {actualValue})" } },
            { "INVALID_NUMBER_en", new ValidationMessageTemplate { ErrorCode = "INVALID_NUMBER", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must be a valid number" } },
            { "INVALID_FORMAT_en", new ValidationMessageTemplate { ErrorCode = "INVALID_FORMAT", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' has an invalid format" } },
            { "INVALID_VALUE_en", new ValidationMessageTemplate { ErrorCode = "INVALID_VALUE", LanguageCode = "en", MessageTemplate = "Field '{fieldName}' must be one of: {allowedValues}" } },
            { "INVALID_MOBILE_NUMBER_en", new ValidationMessageTemplate { ErrorCode = "INVALID_MOBILE_NUMBER", LanguageCode = "en", MessageTemplate = "Invalid mobile number format. Use format: +1234567890" } },
            { "INVALID_BARCODE_en", new ValidationMessageTemplate { ErrorCode = "INVALID_BARCODE", LanguageCode = "en", MessageTemplate = "Barcode must be 8-20 alphanumeric characters" } }
        };
    }

    private Dictionary<string, ValidationRuleDefinition> InitializeValidationRules()
    {
        return new Dictionary<string, ValidationRuleDefinition>
        {
            { "REQUIRED", new ValidationRuleDefinition { RuleName = "REQUIRED", Description = "Field is required", RuleType = "Basic", Severity = ValidationSeverity.Error } },
            { "MIN_LENGTH", new ValidationRuleDefinition { RuleName = "MIN_LENGTH", Description = "Minimum length validation", RuleType = "Length", Severity = ValidationSeverity.Error } },
            { "MAX_LENGTH", new ValidationRuleDefinition { RuleName = "MAX_LENGTH", Description = "Maximum length validation", RuleType = "Length", Severity = ValidationSeverity.Error } },
            { "NUMERIC_RANGE", new ValidationRuleDefinition { RuleName = "NUMERIC_RANGE", Description = "Numeric range validation", RuleType = "Range", Severity = ValidationSeverity.Error } },
            { "REGEX_PATTERN", new ValidationRuleDefinition { RuleName = "REGEX_PATTERN", Description = "Regular expression pattern validation", RuleType = "Pattern", Severity = ValidationSeverity.Error } },
            { "MOBILE_NUMBER", new ValidationRuleDefinition { RuleName = "MOBILE_NUMBER", Description = "Mobile number format validation", RuleType = "Custom", Severity = ValidationSeverity.Error } },
            { "BARCODE", new ValidationRuleDefinition { RuleName = "BARCODE", Description = "Barcode format validation", RuleType = "Custom", Severity = ValidationSeverity.Error } }
        };
    }

    #endregion
}