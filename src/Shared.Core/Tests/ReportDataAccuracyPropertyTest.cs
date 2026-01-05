using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Data;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests;

/// <summary>
/// Property-based test for report data accuracy
/// Feature: multi-business-pos, Property 9: Report Data Accuracy
/// </summary>
public class ReportDataAccuracyPropertyTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;

    public ReportDataAccuracyPropertyTest()
    {
        var services = new ServiceCollection();
        
        // Add in-memory database
        services.AddDbContext<PosDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        
        // Add services
        services.AddScoped<IBusinessManagementService, BusinessManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IEnhancedSalesService, EnhancedSalesService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IWeightBasedPricingService, WeightBasedPricingService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        // Add Report Service
        services.AddScoped<IReportService, ReportService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 9: Report Data Accuracy
    /// For any generated report, the data should accurately reflect the transactions and inventory for the specified time period and scope.
    /// Validates: Requirements 12.1, 12.4
    /// Feature: multi-business-pos, Property 9: Report Data Accuracy
    /// </summary>
    [Fact]
    public async Task ReportDataAccuracy_GeneratedReportsShouldAccuratelyReflectTransactionsAndInventoryForSpecifiedPeriodAndScope()
    {
        var reportService = _serviceProvider.GetRequiredService<IReportService>();
        
        // Test with multiple random scenarios
        for (int iteration = 0; iteration < 3; iteration++)
        {
            try
            {
                // Setup: Create test data with known values
                var testData = GenerateRandomReportTestData();
                await SetupReportTestDataAsync(testData);

                // Test 1: Sales Report Accuracy
                await ValidateSalesReportAccuracy(reportService, testData, iteration);

                // Test 2: Inventory Report Accuracy
                await ValidateInventoryReportAccuracy(reportService, testData, iteration);

                // Test 3: Financial Report Accuracy
                await ValidateFinancialReportAccuracy(reportService, testData, iteration);

                // Test 4: Cross-Report Consistency
                await ValidateCrossReportConsistency(reportService, testData, iteration);

                // Test 5: Time Period Filtering Accuracy
                await ValidateTimePeriodFilteringAccuracy(reportService, testData, iteration);

                // Test 6: Shop-Level Scoping Accuracy
                await ValidateShopLevelScopingAccuracy(reportService, testData, iteration);
            }
            finally
            {
                CleanupTestData();
            }
        }
    }

    private async Task ValidateSalesReportAccuracy(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            var dateRange = new DateRange
            {
                StartDate = testData.TestPeriodStart,
                EndDate = testData.TestPeriodEnd
            };

            var salesRequest = new SalesReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = dateRange,
                Format = ReportFormat.JSON,
                ReportType = SalesReportType.Summary,
                IncludeRefunds = true
            };

            var salesReport = await reportService.GenerateSalesReportAsync(salesRequest);

            // Verify report metadata
            Assert.Equal(businessData.BusinessId, salesReport.BusinessId);
            Assert.Equal(dateRange.StartDate, salesReport.DateRange.StartDate);
            Assert.Equal(dateRange.EndDate, salesReport.DateRange.EndDate);

            // Calculate expected values from test data
            var expectedSales = businessData.Sales
                .Where(s => s.CreatedAt >= dateRange.StartDate && s.CreatedAt <= dateRange.EndDate)
                .ToList();

            var expectedTotalSales = expectedSales.Where(s => s.TotalAmount >= 0).Sum(s => s.TotalAmount);
            var expectedTotalRefunds = Math.Abs(expectedSales.Where(s => s.TotalAmount < 0).Sum(s => s.TotalAmount));
            var expectedNetSales = expectedSales.Sum(s => s.TotalAmount);
            var expectedTransactionCount = expectedSales.Count(s => s.TotalAmount >= 0);
            var expectedRefundCount = expectedSales.Count(s => s.TotalAmount < 0);

            // Verify summary accuracy
            Assert.Equal(expectedTotalSales, salesReport.Summary.TotalSales);
            Assert.Equal(expectedTotalRefunds, salesReport.Summary.TotalRefunds);
            Assert.Equal(expectedNetSales, salesReport.Summary.NetSales);
            Assert.Equal(expectedTransactionCount, salesReport.Summary.TotalTransactions);
            Assert.Equal(expectedRefundCount, salesReport.Summary.TotalRefundTransactions);

            // Verify individual items match expected data
            Assert.Equal(expectedSales.Count, salesReport.Items.Count);

            // Verify each sale item accuracy
            foreach (var expectedSale in expectedSales)
            {
                var reportItem = salesReport.Items.FirstOrDefault(i => i.InvoiceNumber == expectedSale.InvoiceNumber);
                Assert.NotNull(reportItem);
                Assert.Equal(expectedSale.TotalAmount, reportItem.TotalAmount);
                Assert.Equal(expectedSale.CreatedAt.Date, reportItem.Date.Date);
            }

            // Validate report data accuracy using the service's validation method
            var validationResult = await reportService.ValidateReportDataAccuracyAsync(salesReport);
            Assert.True(validationResult.IsValid);
            Assert.True(validationResult.AccuracyScore >= 0.95);
        }
    }

    private async Task ValidateInventoryReportAccuracy(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            var inventoryRequest = new InventoryReportRequest
            {
                BusinessId = businessData.BusinessId,
                Format = ReportFormat.JSON,
                ReportType = InventoryReportType.StockLevels,
                IncludeLowStock = true,
                IncludeExpiring = true,
                LowStockThreshold = 10,
                ExpiringDays = 30
            };

            var inventoryReport = await reportService.GenerateInventoryReportAsync(inventoryRequest);

            // Verify report metadata
            Assert.Equal(businessData.BusinessId, inventoryReport.BusinessId);

            // Calculate expected values from test data
            var expectedProducts = businessData.Products.Count;
            var expectedLowStockProducts = businessData.Stock.Count(s => s.Quantity <= 10);
            var expectedExpiringProducts = businessData.Products.Count(p => 
                p.ExpiryDate.HasValue && p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30));
            var expectedOutOfStockProducts = businessData.Stock.Count(s => s.Quantity == 0);
            var expectedTotalValue = businessData.Stock.Sum(s => s.Quantity * (s.Product?.UnitPrice ?? 0));

            // Verify summary accuracy
            Assert.Equal(expectedProducts, inventoryReport.Summary.TotalProducts);
            Assert.Equal(expectedLowStockProducts, inventoryReport.Summary.LowStockProducts);
            Assert.Equal(expectedOutOfStockProducts, inventoryReport.Summary.OutOfStockProducts);

            // Verify individual items match expected data
            Assert.Equal(expectedProducts, inventoryReport.Items.Count);

            // Verify each inventory item accuracy
            foreach (var expectedStock in businessData.Stock)
            {
                var reportItem = inventoryReport.Items.FirstOrDefault(i => i.ProductId == expectedStock.ProductId);
                Assert.NotNull(reportItem);
                Assert.Equal(expectedStock.Quantity, reportItem.CurrentStock);
                Assert.Equal(expectedStock.Product?.UnitPrice ?? 0, reportItem.UnitPrice);
            }

            // Validate report data accuracy using the service's validation method
            var validationResult = await reportService.ValidateReportDataAccuracyAsync(inventoryReport);
            Assert.True(validationResult.IsValid);
            Assert.True(validationResult.AccuracyScore >= 0.95);
        }
    }

    private async Task ValidateFinancialReportAccuracy(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            var dateRange = new DateRange
            {
                StartDate = testData.TestPeriodStart,
                EndDate = testData.TestPeriodEnd
            };

            var financialRequest = new FinancialReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = dateRange,
                Format = ReportFormat.JSON,
                ReportType = FinancialReportType.ProfitLoss,
                IncludeTaxBreakdown = true,
                IncludeCostAnalysis = true
            };

            var financialReport = await reportService.GenerateFinancialReportAsync(financialRequest);

            // Verify report metadata
            Assert.Equal(businessData.BusinessId, financialReport.BusinessId);
            Assert.Equal(dateRange.StartDate, financialReport.DateRange.StartDate);
            Assert.Equal(dateRange.EndDate, financialReport.DateRange.EndDate);

            // Calculate expected values from test data
            var expectedSales = businessData.Sales
                .Where(s => s.CreatedAt >= dateRange.StartDate && s.CreatedAt <= dateRange.EndDate)
                .ToList();

            var expectedRevenue = expectedSales.Sum(s => s.TotalAmount);
            var expectedCosts = expectedSales.Sum(s => s.Items?.Sum(si => si.Quantity * (si.Product?.PurchasePrice ?? 0)) ?? 0);
            var expectedGrossProfit = expectedRevenue - expectedCosts;

            // Verify summary accuracy
            Assert.Equal(expectedRevenue, financialReport.Summary.TotalRevenue);
            Assert.Equal(expectedCosts, financialReport.Summary.TotalCosts);
            Assert.Equal(expectedGrossProfit, financialReport.Summary.GrossProfit);

            // Verify profit margin calculation accuracy
            var expectedGrossProfitMargin = expectedRevenue > 0 ? (expectedGrossProfit / expectedRevenue) * 100 : 0;
            Assert.Equal(expectedGrossProfitMargin, financialReport.Summary.GrossProfitMargin, 2);

            // Validate report data accuracy using the service's validation method
            var validationResult = await reportService.ValidateReportDataAccuracyAsync(financialReport);
            Assert.True(validationResult.IsValid);
            Assert.True(validationResult.AccuracyScore >= 0.95);
        }
    }

    private async Task ValidateCrossReportConsistency(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            var dateRange = new DateRange
            {
                StartDate = testData.TestPeriodStart,
                EndDate = testData.TestPeriodEnd
            };

            // Generate both sales and financial reports
            var salesRequest = new SalesReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = dateRange,
                Format = ReportFormat.JSON,
                ReportType = SalesReportType.Summary
            };

            var financialRequest = new FinancialReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = dateRange,
                Format = ReportFormat.JSON,
                ReportType = FinancialReportType.Revenue
            };

            var salesReport = await reportService.GenerateSalesReportAsync(salesRequest);
            var financialReport = await reportService.GenerateFinancialReportAsync(financialRequest);

            // Verify revenue consistency between sales and financial reports
            Assert.Equal(salesReport.Summary.NetSales, financialReport.Summary.TotalRevenue);

            // Verify transaction count consistency
            var expectedTransactionCount = businessData.Sales
                .Where(s => s.CreatedAt >= dateRange.StartDate && s.CreatedAt <= dateRange.EndDate)
                .Count();

            Assert.Equal(expectedTransactionCount, salesReport.Items.Count);
        }
    }

    private async Task ValidateTimePeriodFilteringAccuracy(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            // Test with different time periods
            var fullPeriod = new DateRange
            {
                StartDate = testData.TestPeriodStart,
                EndDate = testData.TestPeriodEnd
            };

            var halfPeriod = new DateRange
            {
                StartDate = testData.TestPeriodStart,
                EndDate = testData.TestPeriodStart.AddDays((testData.TestPeriodEnd - testData.TestPeriodStart).Days / 2)
            };

            // Generate reports for both periods
            var fullPeriodRequest = new SalesReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = fullPeriod,
                Format = ReportFormat.JSON,
                ReportType = SalesReportType.Summary
            };

            var halfPeriodRequest = new SalesReportRequest
            {
                BusinessId = businessData.BusinessId,
                DateRange = halfPeriod,
                Format = ReportFormat.JSON,
                ReportType = SalesReportType.Summary
            };

            var fullPeriodReport = await reportService.GenerateSalesReportAsync(fullPeriodRequest);
            var halfPeriodReport = await reportService.GenerateSalesReportAsync(halfPeriodRequest);

            // Verify that half period has fewer or equal transactions
            Assert.True(halfPeriodReport.Summary.TotalTransactions <= fullPeriodReport.Summary.TotalTransactions);

            // Verify that half period sales are less than or equal to full period
            Assert.True(halfPeriodReport.Summary.TotalSales <= fullPeriodReport.Summary.TotalSales);

            // Verify exact filtering by checking individual items
            var expectedHalfPeriodSales = businessData.Sales
                .Where(s => s.CreatedAt >= halfPeriod.StartDate && s.CreatedAt <= halfPeriod.EndDate)
                .Count();

            Assert.Equal(expectedHalfPeriodSales, halfPeriodReport.Items.Count);
        }
    }

    private async Task ValidateShopLevelScopingAccuracy(IReportService reportService, ReportTestData testData, int iteration)
    {
        foreach (var businessData in testData.BusinessData)
        {
            foreach (var shopData in businessData.Shops)
            {
                var dateRange = new DateRange
                {
                    StartDate = testData.TestPeriodStart,
                    EndDate = testData.TestPeriodEnd
                };

                // Generate shop-specific report
                var shopRequest = new SalesReportRequest
                {
                    BusinessId = businessData.BusinessId,
                    ShopId = shopData.ShopId,
                    DateRange = dateRange,
                    Format = ReportFormat.JSON,
                    ReportType = SalesReportType.Summary
                };

                var shopReport = await reportService.GenerateSalesReportAsync(shopRequest);

                // Verify shop scoping
                Assert.Equal(shopData.ShopId, shopReport.ShopId);

                // Calculate expected values for this specific shop
                var expectedShopSales = businessData.Sales
                    .Where(s => s.ShopId == shopData.ShopId && 
                               s.CreatedAt >= dateRange.StartDate && 
                               s.CreatedAt <= dateRange.EndDate)
                    .ToList();

                var expectedShopTotal = expectedShopSales.Sum(s => s.TotalAmount);
                var expectedShopTransactions = expectedShopSales.Count;

                // Verify shop-specific accuracy
                Assert.Equal(expectedShopTotal, shopReport.Summary.NetSales);
                Assert.Equal(expectedShopTransactions, shopReport.Items.Count);

                // Verify all items belong to the correct shop
                foreach (var item in shopReport.Items)
                {
                    var originalSale = businessData.Sales.FirstOrDefault(s => s.InvoiceNumber == item.InvoiceNumber);
                    Assert.NotNull(originalSale);
                    Assert.Equal(shopData.ShopId, originalSale.ShopId);
                }
            }
        }
    }

    private ReportTestData GenerateRandomReportTestData()
    {
        var random = new Random();
        var testData = new ReportTestData
        {
            TestPeriodStart = DateTime.UtcNow.AddDays(-30),
            TestPeriodEnd = DateTime.UtcNow
        };

        // Create 1-2 businesses
        var businessCount = random.Next(1, 3);
        for (int i = 0; i < businessCount; i++)
        {
            var businessId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var businessType = (BusinessType)random.Next(0, Enum.GetValues<BusinessType>().Length);

            var businessData = new BusinessReportTestData
            {
                BusinessId = businessId,
                OwnerId = ownerId,
                BusinessType = businessType
            };

            // Create 1-2 shops per business
            var shopCount = random.Next(1, 3);
            for (int j = 0; j < shopCount; j++)
            {
                var shopId = Guid.NewGuid();
                var shopData = new ShopReportTestData
                {
                    ShopId = shopId,
                    BusinessId = businessId
                };
                businessData.Shops.Add(shopData);

                // Create 2-4 products per shop
                var productCount = random.Next(2, 5);
                for (int k = 0; k < productCount; k++)
                {
                    var productId = Guid.NewGuid();
                    var product = new Product
                    {
                        Id = productId,
                        ShopId = shopId,
                        Name = $"Product {k + 1} - {businessType}",
                        Barcode = $"BC{productId:N}",
                        Category = GetCategoryForBusinessType(businessType, k),
                        UnitPrice = 10.00m + k,
                        PurchasePrice = 5.00m + k, // Using PurchasePrice instead of CostPrice
                        DeviceId = Guid.NewGuid()
                    };

                    if (businessType == BusinessType.Pharmacy)
                    {
                        product.ExpiryDate = DateTime.UtcNow.AddDays(30 + k * 10);
                        product.BatchNumber = $"BATCH{k:D3}";
                    }

                    businessData.Products.Add(product);

                    // Create stock for each product
                    var stock = new Stock
                    {
                        Id = Guid.NewGuid(),
                        ShopId = shopId,
                        ProductId = productId,
                        Product = product,
                        Quantity = random.Next(0, 100),
                        LastUpdatedAt = DateTime.UtcNow.AddMinutes(-k * 10),
                        DeviceId = Guid.NewGuid()
                    };
                    businessData.Stock.Add(stock);
                }

                // Create 3-8 sales per shop
                var salesCount = random.Next(3, 9);
                for (int s = 0; s < salesCount; s++)
                {
                    var saleId = Guid.NewGuid();
                    var saleDate = testData.TestPeriodStart.AddDays(random.Next(0, (testData.TestPeriodEnd - testData.TestPeriodStart).Days));
                    
                    var sale = new Sale
                    {
                        Id = saleId,
                        ShopId = shopId,
                        UserId = ownerId,
                        InvoiceNumber = $"INV{saleId:N}",
                        DiscountAmount = random.Next(0, 10),
                        TaxAmount = random.Next(2, 20),
                        PaymentMethod = PaymentMethod.Cash,
                        CreatedAt = saleDate,
                        DeviceId = Guid.NewGuid()
                    };
                    
                    var subTotal = random.Next(20, 200);
                    sale.TotalAmount = subTotal + sale.TaxAmount - sale.DiscountAmount;

                    // Add some refunds (negative amounts)
                    if (random.Next(0, 10) == 0) // 10% chance of refund
                    {
                        sale.TotalAmount = -Math.Abs(sale.TotalAmount);
                    }

                    // Create sale items
                    var itemCount = random.Next(1, 4);
                    var shopProducts = businessData.Products.Where(p => p.ShopId == shopId).ToList();
                    for (int si = 0; si < itemCount && si < shopProducts.Count; si++)
                    {
                        var product = shopProducts[si];
                        var quantity = random.Next(1, 5);
                        var unitPrice = product.UnitPrice;

                        var saleItem = new SaleItem
                        {
                            Id = Guid.NewGuid(),
                            SaleId = saleId,
                            ProductId = product.Id,
                            Product = product,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = unitPrice * quantity
                        };

                        if (sale.Items == null) sale.Items = new List<SaleItem>();
                        sale.Items.Add(saleItem);
                    }

                    businessData.Sales.Add(sale);
                }
            }

            testData.BusinessData.Add(businessData);
        }

        return testData;
    }

    private async Task SetupReportTestDataAsync(ReportTestData testData)
    {
        // Create businesses and owners
        foreach (var businessData in testData.BusinessData)
        {
            var owner = new User
            {
                Id = businessData.OwnerId,
                BusinessId = businessData.BusinessId,
                Username = $"owner_{businessData.OwnerId:N}",
                FullName = $"Owner {businessData.OwnerId:N}",
                Email = $"owner_{businessData.OwnerId:N}@test.com",
                PasswordHash = "hash",
                Salt = "salt",
                Role = UserRole.BusinessOwner,
                DeviceId = Guid.NewGuid()
            };

            var business = new Business
            {
                Id = businessData.BusinessId,
                Name = $"Business {businessData.BusinessId:N}",
                Type = businessData.BusinessType,
                OwnerId = businessData.OwnerId,
                DeviceId = Guid.NewGuid()
            };

            _context.Users.Add(owner);
            _context.Businesses.Add(business);

            // Create shops
            foreach (var shopData in businessData.Shops)
            {
                var shop = new Shop
                {
                    Id = shopData.ShopId,
                    BusinessId = businessData.BusinessId,
                    Name = $"Shop {shopData.ShopId:N}",
                    DeviceId = Guid.NewGuid()
                };
                _context.Shops.Add(shop);
            }

            // Add products and stock
            _context.Products.AddRange(businessData.Products);
            _context.Stock.AddRange(businessData.Stock);

            // Add sales and sale items
            _context.Sales.AddRange(businessData.Sales);
            foreach (var sale in businessData.Sales)
            {
                if (sale.Items != null)
                {
                    _context.SaleItems.AddRange(sale.Items);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private string GetCategoryForBusinessType(BusinessType businessType, int index)
    {
        return businessType switch
        {
            BusinessType.Pharmacy => (index % 3) switch
            {
                0 => "Medicine",
                1 => "Vitamins",
                _ => "Health Supplements"
            },
            BusinessType.Grocery => (index % 3) switch
            {
                0 => "Fresh Produce",
                1 => "Dairy",
                _ => "Packaged Foods"
            },
            BusinessType.SuperShop => (index % 4) switch
            {
                0 => "Electronics",
                1 => "Clothing",
                2 => "Home & Garden",
                _ => "Food & Beverages"
            },
            _ => "General"
        };
    }

    private void CleanupTestData()
    {
        try
        {
            // Remove all test data in correct order (respecting foreign keys)
            _context.SaleItems.ExecuteDelete();
            _context.Sales.ExecuteDelete();
            _context.Stock.ExecuteDelete();
            _context.Products.ExecuteDelete();
            _context.Shops.ExecuteDelete();
            _context.Businesses.ExecuteDelete();
            _context.Users.ExecuteDelete();
            // SaveChanges is not needed with ExecuteDelete
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    #region Test Data Classes

    private class ReportTestData
    {
        public DateTime TestPeriodStart { get; set; }
        public DateTime TestPeriodEnd { get; set; }
        public List<BusinessReportTestData> BusinessData { get; set; } = new();
    }

    private class BusinessReportTestData
    {
        public Guid BusinessId { get; set; }
        public Guid OwnerId { get; set; }
        public BusinessType BusinessType { get; set; }
        public List<ShopReportTestData> Shops { get; set; } = new();
        public List<Product> Products { get; set; } = new();
        public List<Stock> Stock { get; set; } = new();
        public List<Sale> Sales { get; set; } = new();
    }

    private class ShopReportTestData
    {
        public Guid ShopId { get; set; }
        public Guid BusinessId { get; set; }
    }

    #endregion
}