using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Shared.Core.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Integration tests for enhanced POS system workflows including multi-tab sales,
/// customer lookup auto-fill, barcode scanning, and exception handling
/// Requirements: 5.1, 6.1, 8.1, 3.1
/// </summary>
public class EnhancedWorkflowIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;
    
    // Service dependencies - these would be injected from the service provider
    private readonly IMultiTabSalesManager? _multiTabSalesManager;
    private readonly IEnhancedSalesGridEngine? _salesGridEngine;
    private readonly ICustomerRepository? _customerRepository;
    private readonly ICustomerLookupService? _customerLookupService;
    private readonly IRealTimeCalculationEngine? _calculationEngine;
    private readonly IBarcodeIntegrationService? _barcodeIntegrationService;
    private readonly IGlobalExceptionHandler? _globalExceptionHandler;
    private readonly IProductRepository? _productRepository;

    public EnhancedWorkflowIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        
        // Add minimal services for testing
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
        
        try
        {
            services.AddSharedCoreInMemory();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not add full shared core services: {ex.Message}");
            // Add minimal services manually if needed
        }
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Initialize service dependencies
        _multiTabSalesManager = _serviceProvider.GetService<IMultiTabSalesManager>();
        _salesGridEngine = _serviceProvider.GetService<IEnhancedSalesGridEngine>();
        _customerRepository = _serviceProvider.GetService<ICustomerRepository>();
        _customerLookupService = _serviceProvider.GetService<ICustomerLookupService>();
        _calculationEngine = _serviceProvider.GetService<IRealTimeCalculationEngine>();
        _barcodeIntegrationService = _serviceProvider.GetService<IBarcodeIntegrationService>();
        _globalExceptionHandler = _serviceProvider.GetService<IGlobalExceptionHandler>();
        _productRepository = _serviceProvider.GetService<IProductRepository>();
    }

    #region Multi-Tab Sales Workflow Tests (Requirements 5.1, 5.2)

    [Fact]
    public async Task CompleteMultiTabSalesWorkflow_ShouldHandleMultipleConcurrentSales()
    {
        // Arrange
        _output.WriteLine("Testing complete multi-tab sales workflow...");
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        
        // Create test products
        var product1 = await CreateAndSaveTestProduct("Product 1", "BARCODE001", 10.99m);
        var product2 = await CreateAndSaveTestProduct("Product 2", "BARCODE002", 15.50m);
        var product3 = await CreateAndSaveTestProduct("Product 3", "BARCODE003", 8.75m);

        // Act & Assert - Create multiple sale sessions
        var sessionRequest1 = new CreateSaleSessionRequest { UserId = userId, DeviceId = deviceId, ShopId = shopId, TabName = "Tab 1" };
        var sessionRequest2 = new CreateSaleSessionRequest { UserId = userId, DeviceId = deviceId, ShopId = shopId, TabName = "Tab 2" };
        var sessionRequest3 = new CreateSaleSessionRequest { UserId = userId, DeviceId = deviceId, ShopId = shopId, TabName = "Tab 3" };
        
        var sessionResult1 = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest1);
        var sessionResult2 = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest2);
        var sessionResult3 = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest3);
        
        Assert.True(sessionResult1.Success);
        Assert.True(sessionResult2.Success);
        Assert.True(sessionResult3.Success);
        Assert.NotEqual(sessionResult1.Session.Id, sessionResult2.Session.Id);
        Assert.NotEqual(sessionResult2.Session.Id, sessionResult3.Session.Id);
        _output.WriteLine($"Created 3 sale sessions: {sessionResult1.Session.Id}, {sessionResult2.Session.Id}, {sessionResult3.Session.Id}");

        // Test session isolation - add different products to each session
        var gridResult1 = await _salesGridEngine.AddProductToGridAsync(sessionResult1.Session.Id, product1, 2);
        var gridResult2 = await _salesGridEngine.AddProductToGridAsync(sessionResult2.Session.Id, product2, 3);
        var gridResult3 = await _salesGridEngine.AddProductToGridAsync(sessionResult3.Session.Id, product3, 1);
        
        Assert.True(gridResult1.Success);
        Assert.True(gridResult2.Success);
        Assert.True(gridResult3.Success);

        // Verify session isolation - each session should have only its own items
        var session1State = await _salesGridEngine.GetGridStateAsync(sessionResult1.Session.Id);
        var session2State = await _salesGridEngine.GetGridStateAsync(sessionResult2.Session.Id);
        var session3State = await _salesGridEngine.GetGridStateAsync(sessionResult3.Session.Id);
        
        Assert.Single(session1State.Items);
        Assert.Single(session2State.Items);
        Assert.Single(session3State.Items);
        
        Assert.Equal(product1.Id, session1State.Items[0].ProductId);
        Assert.Equal(product2.Id, session2State.Items[0].ProductId);
        Assert.Equal(product3.Id, session3State.Items[0].ProductId);
        
        _output.WriteLine("Session isolation verified - each session maintains independent state");

        // Test session switching and state persistence
        await _multiTabSalesManager.SwitchToSessionAsync(sessionResult1.Session.Id);
        await _salesGridEngine.UpdateQuantityAsync(sessionResult1.Session.Id, session1State.Items[0].Id, 5);
        
        await _multiTabSalesManager.SwitchToSessionAsync(sessionResult2.Session.Id);
        await _salesGridEngine.UpdateQuantityAsync(sessionResult2.Session.Id, session2State.Items[0].Id, 7);
        
        // Verify quantities were updated correctly in each session
        var updatedSession1 = await _salesGridEngine.GetGridStateAsync(sessionResult1.Session.Id);
        var updatedSession2 = await _salesGridEngine.GetGridStateAsync(sessionResult2.Session.Id);
        
        Assert.Equal(5, updatedSession1.Items[0].Quantity);
        Assert.Equal(7, updatedSession2.Items[0].Quantity);
        
        _output.WriteLine("Session switching and state persistence verified");

        // Test session completion
        var completionResult = await _multiTabSalesManager.CompleteSessionAsync(sessionResult1.Session.Id, PaymentMethod.Cash);
        Assert.True(completionResult.Success);
        
        // Verify session is no longer active
        var activeSessions = await _multiTabSalesManager.GetActiveSessionsAsync(userId, deviceId);
        Assert.Equal(2, activeSessions.Count); // Should have 2 remaining active sessions
        Assert.DoesNotContain(activeSessions, s => s.Id == sessionResult1.Session.Id);
        
        _output.WriteLine("Multi-tab sales workflow completed successfully");
    }

    [Fact]
    public async Task MultiTabSalesWorkflow_ShouldEnforceSessionLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var maxSessions = await _multiTabSalesManager.GetMaxConcurrentSessionsAsync();
        
        // Act - Create sessions up to the limit
        var sessions = new List<SessionOperationResult>();
        for (int i = 0; i < maxSessions; i++)
        {
            var request = new CreateSaleSessionRequest 
            { 
                UserId = userId, 
                DeviceId = deviceId, 
                ShopId = shopId, 
                TabName = $"Tab {i + 1}" 
            };
            var result = await _multiTabSalesManager.CreateNewSaleSessionAsync(request);
            Assert.True(result.Success);
            sessions.Add(result);
        }
        
        // Try to create one more session beyond the limit
        var extraRequest = new CreateSaleSessionRequest 
        { 
            UserId = userId, 
            DeviceId = deviceId, 
            ShopId = shopId, 
            TabName = "Extra Tab" 
        };
        var extraResult = await _multiTabSalesManager.CreateNewSaleSessionAsync(extraRequest);
        
        // Assert
        Assert.False(extraResult.Success);
        Assert.Contains("maximum", extraResult.Message?.ToLower() ?? "");
        
        _output.WriteLine($"Session limit enforcement verified - max sessions: {maxSessions}");
    }

    #endregion

    #region Customer Lookup and Auto-Fill Integration Tests (Requirements 6.1, 6.2)

    [Fact]
    public async Task CustomerLookupAutoFillWorkflow_ShouldPopulateCustomerDataAutomatically()
    {
        // Arrange
        _output.WriteLine("Testing customer lookup and auto-fill integration...");
        
        var testCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-20250106-TEST",
            Name = "John Doe",
            Phone = "5551234567",
            Email = "john.doe@example.com",
            Tier = MembershipTier.Gold,
            TotalSpent = 1500.00m,
            VisitCount = 15,
            IsActive = true,
            DeviceId = Guid.NewGuid()
        };
        
        await _customerRepository.AddAsync(testCustomer);
        await _customerRepository.SaveChangesAsync();
        
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        
        // Create a sale session
        var sessionRequest = new CreateSaleSessionRequest 
        { 
            UserId = userId, 
            DeviceId = deviceId, 
            ShopId = shopId, 
            TabName = "Customer Test Tab" 
        };
        var sessionResult = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest);
        Assert.True(sessionResult.Success);

        // Act - Lookup customer by mobile number
        var lookupResult = await _customerLookupService.LookupByMobileNumberAsync("5551234567");
        
        // Assert - Customer should be found and data populated
        Assert.NotNull(lookupResult);
        Assert.Equal(testCustomer.Id, lookupResult.Id);
        Assert.Equal(testCustomer.Name, lookupResult.Name);
        Assert.Equal(testCustomer.Phone, lookupResult.Phone);
        Assert.Equal(testCustomer.Tier, lookupResult.Tier);
        Assert.Equal(testCustomer.TotalSpent, lookupResult.TotalSpent);
        
        _output.WriteLine($"Customer found: {lookupResult.Name}, Tier: {lookupResult.Tier}, Total Spent: ${lookupResult.TotalSpent}");

        // Test membership benefits application
        var product = await CreateAndSaveTestProduct("Premium Product", "PREMIUM001", 100.00m);
        var gridResult = await _salesGridEngine.AddProductToGridAsync(sessionResult.Session.Id, product, 1);
        Assert.True(gridResult.Success);
        
        // Verify that membership discounts are applied (Gold tier should get discounts)
        var gridState = await _salesGridEngine!.GetGridStateAsync(sessionResult.Session.Id);
        // Convert SalesGridItems to SaleItems for calculation
        var saleItems = gridState.Items.Select(item => new SaleItem
        {
            Id = item.Id,
            ProductId = item.ProductId,
            Quantity = (int)item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.LineTotal
        }).ToList();
        
        // Convert CustomerLookupResult to Customer for calculation
        var customer = lookupResult != null ? new Customer
        {
            Id = lookupResult.Id,
            Name = lookupResult.Name,
            Phone = lookupResult.Phone,
            Tier = lookupResult.Tier,
            TotalSpent = lookupResult.TotalSpent
        } : null;
        
        var calculation = await _calculationEngine!.CalculateOrderTotalsAsync(saleItems, new ShopConfiguration(), customer);
        
        // Gold tier customers should receive automatic discounts
        Assert.True(calculation.TotalDiscountAmount > 0, "Gold tier customer should receive automatic discounts");
        
        _output.WriteLine($"Membership discount applied: ${calculation.TotalDiscountAmount}");
        _output.WriteLine("Customer lookup and auto-fill workflow completed successfully");
    }

    [Fact]
    public async Task CustomerLookupWorkflow_ShouldHandleNewCustomerCreation()
    {
        // Arrange
        var unknownMobileNumber = "5559999999";
        
        // Act - Try to lookup non-existent customer
        var lookupResult = await _customerLookupService.LookupByMobileNumberAsync(unknownMobileNumber);
        
        // Assert - Should return null for non-existent customer
        Assert.Null(lookupResult);
        
        // Act - Create new customer
        var createRequest = new CustomerCreationRequest
        {
            Name = "Jane Smith",
            MobileNumber = unknownMobileNumber,
            Email = "jane.smith@example.com",
            InitialTier = MembershipTier.Bronze,
            ShopId = Guid.NewGuid()
        };
        
        var createResult = await _customerLookupService.CreateNewCustomerAsync(createRequest);
        
        // Assert - Customer should be created successfully
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Customer);
        Assert.Equal(createRequest.Name, createResult.Customer.Name);
        Assert.Equal(unknownMobileNumber, createResult.Customer.Phone);
        Assert.Equal(createRequest.InitialTier, createResult.Customer.Tier);
        
        // Verify customer can now be found
        var secondLookup = await _customerLookupService.LookupByMobileNumberAsync(unknownMobileNumber);
        Assert.NotNull(secondLookup);
        Assert.Equal(createResult.Customer.Id, secondLookup.Id);
        
        _output.WriteLine("New customer creation workflow completed successfully");
    }

    #endregion

    #region Barcode Scanning End-to-End Tests (Requirements 8.1, 8.2)

    [Fact]
    public async Task BarcodeScanning_EndToEndWorkflow_ShouldAddProductToSale()
    {
        // Arrange
        _output.WriteLine("Testing barcode scanning end-to-end workflow...");
        
        var shopId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        
        // Create test product with barcode
        var product = await CreateAndSaveTestProduct("Scanned Product", "1234567890123", 25.99m);
        
        // Create sale session
        var sessionRequest = new CreateSaleSessionRequest 
        { 
            UserId = userId, 
            DeviceId = deviceId, 
            ShopId = shopId, 
            TabName = "Barcode Test Tab" 
        };
        var sessionResult = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest);
        Assert.True(sessionResult.Success);

        // Act - Simulate barcode scanning workflow
        
        // 1. Validate barcode format
        var isValidFormat = await _barcodeIntegrationService.ValidateBarcodeFormatAsync("1234567890123");
        Assert.True(isValidFormat);
        
        // 2. Lookup product by barcode
        var scannedProduct = await _barcodeIntegrationService.LookupProductByBarcodeAsync("1234567890123", shopId);
        Assert.NotNull(scannedProduct);
        Assert.Equal(product.Id, scannedProduct.Id);
        Assert.Equal(product.Name, scannedProduct.Name);
        
        // 3. Add scanned product to sales grid
        var gridResult = await _salesGridEngine.AddProductToGridAsync(sessionResult.Session.Id, scannedProduct, 1);
        Assert.True(gridResult.Success);
        
        // 4. Verify product was added correctly
        var gridState = await _salesGridEngine.GetGridStateAsync(sessionResult.Session.Id);
        Assert.Single(gridState.Items);
        Assert.Equal(scannedProduct.Id, gridState.Items[0].ProductId);
        Assert.Equal(scannedProduct.Name, gridState.Items[0].ProductName);
        Assert.Equal(1, gridState.Items[0].Quantity);
        Assert.Equal(25.99m, gridState.Items[0].UnitPrice);
        
        // 5. Test scan feedback
        var scanResult = new BarcodeResult
        {
            IsSuccess = true,
            IsProductFound = true,
            IsInStock = true,
            Product = scannedProduct
        };
        
        var feedback = await _barcodeIntegrationService.ProvideScanFeedbackAsync(scanResult);
        Assert.Equal(FeedbackType.Success, feedback.Type);
        Assert.True(feedback.ShouldPlayBeep);
        Assert.Contains("Product found", feedback.VisualMessage);
        
        _output.WriteLine($"Product '{scannedProduct.Name}' successfully added via barcode scan");
        _output.WriteLine("Barcode scanning end-to-end workflow completed successfully");
    }

    [Fact]
    public async Task BarcodeScanning_ShouldHandleMultipleFormats()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        
        // Create products with different barcode formats
        var ean13Product = await CreateAndSaveTestProduct("EAN-13 Product", "1234567890123", 10.00m);
        var ean8Product = await CreateAndSaveTestProduct("EAN-8 Product", "12345678", 15.00m);
        var code128Product = await CreateAndSaveTestProduct("Code 128 Product", "CODE128BARCODE", 20.00m);
        
        // Act & Assert - Test different barcode formats
        var supportedFormats = await _barcodeIntegrationService.GetSupportedFormatsAsync();
        Assert.Contains(supportedFormats, f => f.Name == "EAN-13");
        Assert.Contains(supportedFormats, f => f.Name == "EAN-8");
        Assert.Contains(supportedFormats, f => f.Name == "Code 128");
        
        // Test EAN-13
        var ean13Valid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync("1234567890123");
        Assert.True(ean13Valid);
        var ean13Lookup = await _barcodeIntegrationService.LookupProductByBarcodeAsync("1234567890123", shopId);
        Assert.NotNull(ean13Lookup);
        
        // Test EAN-8
        var ean8Valid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync("12345678");
        Assert.True(ean8Valid);
        var ean8Lookup = await _barcodeIntegrationService.LookupProductByBarcodeAsync("12345678", shopId);
        Assert.NotNull(ean8Lookup);
        
        // Test Code 128
        var code128Valid = await _barcodeIntegrationService.ValidateBarcodeFormatAsync("CODE128BARCODE");
        Assert.True(code128Valid);
        var code128Lookup = await _barcodeIntegrationService.LookupProductByBarcodeAsync("CODE128BARCODE", shopId);
        Assert.NotNull(code128Lookup);
        
        _output.WriteLine("Multiple barcode format support verified");
    }

    [Fact]
    public async Task BarcodeScanning_ShouldHandleProductNotFound()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var nonExistentBarcode = "9999999999999";
        
        // Act
        var product = await _barcodeIntegrationService.LookupProductByBarcodeAsync(nonExistentBarcode, shopId);
        
        // Assert
        Assert.Null(product);
        
        // Test feedback for product not found
        var scanResult = new BarcodeResult
        {
            IsSuccess = true,
            IsProductFound = false
        };
        
        var feedback = await _barcodeIntegrationService.ProvideScanFeedbackAsync(scanResult);
        Assert.Equal(FeedbackType.Warning, feedback.Type);
        Assert.False(feedback.ShouldPlayBeep);
        Assert.Contains("Product not found", feedback.VisualMessage);
        
        _output.WriteLine("Product not found scenario handled correctly");
    }

    #endregion

    #region Exception Handling Integration Tests (Requirements 3.1, 3.2)

    [Fact]
    public async Task ExceptionHandling_ShouldHandleServiceExceptionsGracefully()
    {
        // Arrange
        _output.WriteLine("Testing exception handling across all components...");
        var deviceId = Guid.NewGuid();
        
        // Test database exception handling
        var dbException = new Microsoft.EntityFrameworkCore.DbUpdateException("Database connection failed");
        var dbResult = await _globalExceptionHandler.HandleExceptionAsync(dbException, "Database Operation", deviceId);
        
        Assert.NotNull(dbResult);
        Assert.Equal(500, dbResult.StatusCode);
        Assert.Contains("Unable to save", dbResult.Message);
        Assert.NotNull(dbResult.RecoveryAction);
        
        // Test validation exception handling
        var validationException = new ArgumentException("Invalid input parameter");
        var validationResult = await _globalExceptionHandler.HandleExceptionAsync(validationException, "Input Validation", deviceId);
        
        Assert.NotNull(validationResult);
        Assert.Equal(400, validationResult.StatusCode);
        Assert.Contains("Invalid", validationResult.Message);
        
        // Test network exception handling
        var networkException = new HttpRequestException("Network timeout");
        var networkResult = await _globalExceptionHandler.HandleExceptionAsync(networkException, "API Call", deviceId);
        
        Assert.NotNull(networkResult);
        Assert.Contains("network", networkResult.Message.ToLower());
        Assert.NotNull(networkResult.RecoveryAction);
        
        _output.WriteLine("Exception handling verified for database, validation, and network errors");
    }

    [Fact]
    public async Task ExceptionHandling_ShouldProvideRecoveryActions()
    {
        // Arrange
        var context = "Test Operation";
        
        // Test different exception types and their recovery actions
        var exceptions = new List<Exception>
        {
            new Microsoft.EntityFrameworkCore.DbUpdateException("DB Error"),
            new HttpRequestException("Network Error"),
            new TimeoutException("Operation Timeout"),
            new ArgumentNullException("paramName", "Null Parameter")
        };
        
        foreach (var exception in exceptions)
        {
            // Act
            var recoveryAction = await _globalExceptionHandler.SuggestRecoveryActionAsync(exception, context);
            
            // Assert
            Assert.NotNull(recoveryAction);
            Assert.NotEmpty(recoveryAction.ActionType);
            Assert.NotEmpty(recoveryAction.Steps);
            
            _output.WriteLine($"Recovery action for {exception.GetType().Name}: {recoveryAction.ActionType}");
        }
        
        _output.WriteLine("Recovery action suggestions verified for all exception types");
    }

    [Fact]
    public async Task ExceptionHandling_ShouldLogExceptionsWithContext()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception for logging");
        var context = "Integration Test";
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            ["TestKey"] = "TestValue",
            ["SessionId"] = Guid.NewGuid()
        };
        
        // Act & Assert - Should not throw
        await _globalExceptionHandler.LogExceptionAsync(exception, context, deviceId, userId, metadata);
        
        _output.WriteLine("Exception logging with context and metadata completed successfully");
    }

    #endregion

    #region Integrated Workflow Tests

    [Fact]
    public async Task CompleteIntegratedWorkflow_ShouldCombineAllFeatures()
    {
        // Arrange
        _output.WriteLine("Testing complete integrated workflow combining all features...");
        
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        
        // Setup test data
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            MembershipNumber = "CUST-INTEGRATED-TEST",
            Name = "Integrated Test Customer",
            Phone = "5551111111",
            Tier = MembershipTier.Silver,
            TotalSpent = 800.00m,
            IsActive = true,
            DeviceId = deviceId
        };
        await _customerRepository.AddAsync(customer);
        await _customerRepository.SaveChangesAsync();
        
        var product1 = await CreateAndSaveTestProduct("Barcode Product 1", "1111111111111", 50.00m);
        var product2 = await CreateAndSaveTestProduct("Barcode Product 2", "2222222222222", 75.00m);
        
        // Act - Execute integrated workflow
        
        // 1. Create multi-tab session
        var sessionRequest = new CreateSaleSessionRequest 
        { 
            UserId = userId, 
            DeviceId = deviceId, 
            ShopId = shopId, 
            TabName = "Integrated Test Tab" 
        };
        var sessionResult = await _multiTabSalesManager.CreateNewSaleSessionAsync(sessionRequest);
        Assert.True(sessionResult.Success);
        
        // 2. Customer lookup and auto-fill
        var customerLookup = await _customerLookupService.LookupByMobileNumberAsync("5551111111");
        Assert.NotNull(customerLookup);
        Assert.Equal(customer.Name, customerLookup.Name);
        
        // 3. Barcode scanning and product addition
        var scannedProduct1 = await _barcodeIntegrationService.LookupProductByBarcodeAsync("1111111111111", shopId);
        Assert.NotNull(scannedProduct1);
        
        var gridResult1 = await _salesGridEngine.AddProductToGridAsync(sessionResult.Session.Id, scannedProduct1, 2);
        Assert.True(gridResult1.Success);
        
        var scannedProduct2 = await _barcodeIntegrationService.LookupProductByBarcodeAsync("2222222222222", shopId);
        Assert.NotNull(scannedProduct2);
        
        var gridResult2 = await _salesGridEngine.AddProductToGridAsync(sessionResult.Session.Id, scannedProduct2, 1);
        Assert.True(gridResult2.Success);
        
        // 4. Real-time calculations with membership discounts
        var finalCalculation = await _salesGridEngine!.RecalculateAllTotalsAsync(sessionResult.Session.Id);
        
        // 5. Validate final state
        var finalGridState = await _salesGridEngine.GetGridStateAsync(sessionResult.Session.Id);
        Assert.Equal(2, finalGridState.Items.Count);
        
        var totalBeforeDiscount = (50.00m * 2) + (75.00m * 1); // $175.00
        Assert.True(finalCalculation.Subtotal <= totalBeforeDiscount);
        
        // Silver tier should get some discount
        Assert.True(finalCalculation.TotalDiscount > 0);
        
        // 6. Complete the sale
        var completionResult = await _multiTabSalesManager.CompleteSessionAsync(sessionResult.Session.Id, PaymentMethod.Card);
        Assert.True(completionResult.Success);
        
        _output.WriteLine($"Integrated workflow completed successfully:");
        _output.WriteLine($"- Customer: {customerLookup.Name} ({customerLookup.Tier})");
        _output.WriteLine($"- Products: {finalGridState.Items.Count}");
        _output.WriteLine($"- Subtotal: ${finalCalculation.Subtotal:F2}");
        _output.WriteLine($"- Discount: ${finalCalculation.TotalDiscount:F2}");
        _output.WriteLine($"- Final Total: ${finalCalculation.FinalTotal:F2}");
    }

    #endregion

    #region Helper Methods

    private async Task<Product> CreateAndSaveTestProduct(string name, string barcode, decimal price)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Barcode = barcode,
            UnitPrice = price,
            IsActive = true,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();
        return product;
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}