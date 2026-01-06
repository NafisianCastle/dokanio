# Shared Services for Cross-Platform Functionality

This document outlines the shared services implemented in the POS system that provide cross-platform functionality across Desktop, Mobile, Server, and WebDashboard applications.

## Overview

The following shared services are implemented in the `Shared.Core` project and are available for use across all platform-specific applications:

## 1. CustomerLookupService

**Interface:** `ICustomerLookupService`  
**Implementation:** `CustomerLookupService`  
**Purpose:** Fast customer lookup with mobile number validation and auto-fill functionality

### Key Features:
- Mobile number validation and formatting
- Fast customer lookup by mobile number with caching
- Customer membership details retrieval
- New customer creation workflow
- Customer preferences management
- Purchase history tracking and tier upgrades
- Customer search functionality

### Usage:
```csharp
// Lookup customer by mobile number
var customer = await _customerLookupService.LookupByMobileNumberAsync("1234567890");

// Validate mobile number format
var validation = await _customerLookupService.ValidateMobileNumberAsync("+1-234-567-8900");

// Create new customer
var newCustomer = await _customerLookupService.CreateNewCustomerAsync(request);
```

## 2. BarcodeIntegrationService

**Interface:** `IBarcodeIntegrationService`  
**Implementation:** `BarcodeIntegrationService`  
**Purpose:** Comprehensive barcode scanning integration with product lookup

### Key Features:
- Multi-format barcode scanning (EAN-13, EAN-8, Code 128, Code 39, UPC-A)
- Product lookup by barcode in shop inventory
- Visual and audio feedback for scan results
- Automatic product addition to sales grid
- Stock availability validation
- Error handling and recovery

### Usage:
```csharp
// Initialize barcode service
await _barcodeService.InitializeAsync();

// Scan barcode
var result = await _barcodeService.ScanBarcodeAsync(scanOptions);

// Process scanned barcode
var processResult = await _barcodeService.ProcessScannedBarcodeAsync(barcode, sessionId);
```

## 3. RealTimeCalculationEngine

**Interface:** `IRealTimeCalculationEngine`  
**Implementation:** `RealTimeCalculationEngine`  
**Purpose:** Real-time calculation engine for immediate sales calculations

### Key Features:
- Line item calculations with pricing rules
- Order total calculations with discounts and taxes
- Weight-based pricing calculations
- Membership-based discount applications
- Complex pricing rules (bulk discounts, tiered pricing)
- Calculation validation and error checking
- Real-time recalculation on item changes

### Usage:
```csharp
// Calculate line item
var lineResult = await _calculationEngine.CalculateLineItemAsync(saleItem, shopConfig);

// Calculate order totals
var orderTotal = await _calculationEngine.CalculateOrderTotalsAsync(items, shopConfig, customer);

// Recalculate on item change
var updatedTotal = await _calculationEngine.RecalculateOnItemChangeAsync(modifiedItem, allItems, shopConfig);
```

## 4. ValidationService

**Interface:** `IValidationService`  
**Implementation:** `ValidationService`  
**Purpose:** Comprehensive validation service for all POS entities and operations

### Key Features:
- Field-level validation with custom rules
- Entity validation (Products, Sales, Customers, etc.)
- Business rule validation (stock levels, expiry dates, pricing)
- Real-time validation feedback
- Localized validation messages
- Form completion validation
- Stock and expiry validation

### Usage:
```csharp
// Validate field
var fieldResult = await _validationService.ValidateFieldAsync("email", email, rules);

// Validate entity
var productResult = await _validationService.ValidateProductAsync(product, shopId);

// Validate stock levels
var stockResult = await _validationService.ValidateStockLevelsAsync(productId, quantity, shopId);
```

## Cross-Platform Registration

All shared services are registered in the dependency injection container via `ServiceCollectionExtensions`:

```csharp
// In AddSharedCore method
services.AddScoped<ICustomerLookupService, CustomerLookupService>();
services.AddScoped<IBarcodeIntegrationService, BarcodeIntegrationService>();
services.AddScoped<IRealTimeCalculationEngine, RealTimeCalculationEngine>();
services.AddScoped<IValidationService, ValidationService>();
```

## Platform Usage

### Desktop Application
These services are automatically available through dependency injection in ViewModels and other services.

### Mobile Application  
Services are shared across mobile ViewModels and can be used in platform-specific implementations.

### Server Application
Services are available in controllers and can be used for API endpoints and business logic.

### WebDashboard
Services can be used in Blazor components and server-side logic.

## Dependencies

The shared services depend on:
- Repository interfaces for data access
- Caching services for performance optimization
- Configuration services for business rules
- Logging services for diagnostics

## Testing

All shared services include comprehensive unit tests and are configured for in-memory testing via `AddSharedCoreInMemory()`.

## Benefits

1. **Code Reuse**: Single implementation shared across all platforms
2. **Consistency**: Same business logic and validation rules everywhere
3. **Maintainability**: Changes in one place affect all platforms
4. **Testing**: Centralized testing of core business logic
5. **Performance**: Optimized implementations with caching and validation