using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.DependencyInjection;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Shared.Core.Services;
using Shared.Core.DTOs;

namespace Shared.Core.Tests;

public class EnhancedSalesGridEngineTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEnhancedSalesGridEngine _gridEngine;
    private readonly ISaleSessionRepository _saleSessionRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;

    public EnhancedSalesGridEngineTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();

        _gridEngine = _serviceProvider.GetRequiredService<IEnhancedSalesGridEngine>();
        _saleSessionRepository = _serviceProvider.GetRequiredService<ISaleSessionRepository>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
        _stockRepository = _serviceProvider.GetRequiredService<IStockRepository>();
    }

    public async Task<bool> TestAddProductToGrid()
    {
        try
        {
            // Arrange
            var saleSession = new SaleSession
            {
                Id = Guid.NewGuid(),
                SaleId = Guid.NewGuid(),
                TabName = "Test Tab",
                ShopId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                State = SessionState.Active,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsActive = true
            };

            await _saleSessionRepository.AddAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Barcode = "TEST001",
                UnitPrice = 10.50m,
                IsWeightBased = false,
                IsActive = true
            };

            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

            // Add stock for the product
            var stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 100,
                ShopId = saleSession.ShopId
            };

            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            // Act
            var result = await _gridEngine.AddProductToGridAsync(saleSession.Id, product, 2);

            // Assert
            if (!result.Success)
            {
                Console.WriteLine($"Test failed: {string.Join(", ", result.Errors)}");
                return false;
            }

            if (result.UpdatedGridState == null)
            {
                Console.WriteLine("Test failed: UpdatedGridState is null");
                return false;
            }

            if (result.UpdatedGridState.Items.Count != 1)
            {
                Console.WriteLine($"Test failed: Expected 1 item, got {result.UpdatedGridState.Items.Count}");
                return false;
            }

            var gridItem = result.UpdatedGridState.Items.First();
            if (gridItem.Quantity != 2)
            {
                Console.WriteLine($"Test failed: Expected quantity 2, got {gridItem.Quantity}");
                return false;
            }

            if (gridItem.LineTotal != 21.00m) // 2 * 10.50
            {
                Console.WriteLine($"Test failed: Expected line total 21.00, got {gridItem.LineTotal}");
                return false;
            }

            Console.WriteLine("✓ TestAddProductToGrid passed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestUpdateQuantity()
    {
        try
        {
            // Arrange - First add a product
            var saleSession = new SaleSession
            {
                Id = Guid.NewGuid(),
                SaleId = Guid.NewGuid(),
                TabName = "Test Tab",
                ShopId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                State = SessionState.Active,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsActive = true
            };

            await _saleSessionRepository.AddAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product 2",
                Barcode = "TEST002",
                UnitPrice = 5.00m,
                IsWeightBased = false,
                IsActive = true
            };

            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

            // Add stock
            var stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 100,
                ShopId = saleSession.ShopId
            };

            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            // Add product to grid first
            var addResult = await _gridEngine.AddProductToGridAsync(saleSession.Id, product, 1);
            if (!addResult.Success)
            {
                Console.WriteLine($"Setup failed: {string.Join(", ", addResult.Errors)}");
                return false;
            }

            var saleItemId = addResult.UpdatedGridState!.Items.First().Id;

            // Act - Update quantity
            var updateResult = await _gridEngine.UpdateQuantityAsync(saleSession.Id, saleItemId, 5);

            // Assert
            if (!updateResult.Success)
            {
                Console.WriteLine($"Test failed: {string.Join(", ", updateResult.Errors)}");
                return false;
            }

            var gridItem = updateResult.UpdatedGridState!.Items.First();
            if (gridItem.Quantity != 5)
            {
                Console.WriteLine($"Test failed: Expected quantity 5, got {gridItem.Quantity}");
                return false;
            }

            if (gridItem.LineTotal != 25.00m) // 5 * 5.00
            {
                Console.WriteLine($"Test failed: Expected line total 25.00, got {gridItem.LineTotal}");
                return false;
            }

            Console.WriteLine("✓ TestUpdateQuantity passed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestGridValidation()
    {
        try
        {
            // Arrange
            var saleSession = new SaleSession
            {
                Id = Guid.NewGuid(),
                SaleId = Guid.NewGuid(),
                TabName = "Test Tab",
                ShopId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                State = SessionState.Active,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsActive = true
            };

            await _saleSessionRepository.AddAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            // Act - Validate empty grid
            var validationResult = await _gridEngine.ValidateGridDataAsync(saleSession.Id);

            // Assert
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"Test failed: Empty grid should be valid, but got errors: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
                return false;
            }

            if (validationResult.Warnings.Count == 0)
            {
                Console.WriteLine("Test failed: Expected warning for empty grid");
                return false;
            }

            Console.WriteLine("✓ TestGridValidation passed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestRecalculateAllTotals()
    {
        try
        {
            // Arrange
            var saleSession = new SaleSession
            {
                Id = Guid.NewGuid(),
                SaleId = Guid.NewGuid(),
                TabName = "Test Tab",
                ShopId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                State = SessionState.Active,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsActive = true
            };

            await _saleSessionRepository.AddAsync(saleSession);
            await _saleSessionRepository.SaveChangesAsync();

            var product1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 1",
                Barcode = "PROD001",
                UnitPrice = 10.00m,
                IsWeightBased = false,
                IsActive = true
            };

            var product2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 2",
                Barcode = "PROD002",
                UnitPrice = 15.00m,
                IsWeightBased = false,
                IsActive = true
            };

            await _productRepository.AddAsync(product1);
            await _productRepository.AddAsync(product2);
            await _productRepository.SaveChangesAsync();

            // Add stock
            await _stockRepository.AddAsync(new Stock { Id = Guid.NewGuid(), ProductId = product1.Id, Quantity = 100, ShopId = saleSession.ShopId });
            await _stockRepository.AddAsync(new Stock { Id = Guid.NewGuid(), ProductId = product2.Id, Quantity = 100, ShopId = saleSession.ShopId });
            await _stockRepository.SaveChangesAsync();

            // Add products to grid
            await _gridEngine.AddProductToGridAsync(saleSession.Id, product1, 2); // 2 * 10 = 20
            await _gridEngine.AddProductToGridAsync(saleSession.Id, product2, 3); // 3 * 15 = 45

            // Act
            var calculationResult = await _gridEngine.RecalculateAllTotalsAsync(saleSession.Id);

            // Assert
            if (calculationResult.Subtotal != 65.00m) // 20 + 45
            {
                Console.WriteLine($"Test failed: Expected subtotal 65.00, got {calculationResult.Subtotal}");
                return false;
            }

            if (calculationResult.LineItemCalculations.Count != 2)
            {
                Console.WriteLine($"Test failed: Expected 2 line items, got {calculationResult.LineItemCalculations.Count}");
                return false;
            }

            Console.WriteLine("✓ TestRecalculateAllTotals passed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RunAllTests()
    {
        Console.WriteLine("Running Enhanced Sales Grid Engine Tests...");
        
        var tests = new List<(string Name, Func<Task<bool>> Test)>
        {
            ("Add Product to Grid", TestAddProductToGrid),
            ("Update Quantity", TestUpdateQuantity),
            ("Grid Validation", TestGridValidation),
            ("Recalculate All Totals", TestRecalculateAllTotals)
        };

        var passedTests = 0;
        var totalTests = tests.Count;

        foreach (var (name, test) in tests)
        {
            Console.WriteLine($"\nRunning: {name}");
            try
            {
                var passed = await test();
                if (passed)
                {
                    passedTests++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ {name} failed with exception: {ex.Message}");
            }
        }

        Console.WriteLine($"\n=== Test Results ===");
        Console.WriteLine($"Passed: {passedTests}/{totalTests}");
        Console.WriteLine($"Success Rate: {(passedTests * 100.0 / totalTests):F1}%");

        return passedTests == totalTests;
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}