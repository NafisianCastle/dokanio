using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Data;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Services;
using Xunit;

namespace Shared.Core.Tests.PropertyTests;

public class HardwareIntegrationPropertyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PosDbContext _context;
    private readonly IReceiptService _receiptService;
    private readonly IPrinterService _printerService;
    private readonly IBarcodeScanner _barcodeScanner;
    private readonly ICashDrawerService _cashDrawerService;

    public HardwareIntegrationPropertyTests()
    {
        var services = new ServiceCollection();
        
        // Use SQLite in-memory database
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
            });
            options.EnableSensitiveDataLogging(true);
        });
        
        // Register hardware services
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IPrinterService, PrinterService>();
        services.AddScoped<IBarcodeScanner, BarcodeScanner>();
        services.AddScoped<ICashDrawerService, CashDrawerService>();
        
        // Register configurations
        services.AddSingleton(new ReceiptConfiguration
        {
            ShopName = "Test Shop",
            PaperWidth = 48,
            PrintBarcode = true,
            FooterMessage = "Thank you!"
        });
        
        services.AddSingleton(new ScannerConfiguration());
        services.AddSingleton(new CashDrawerConfiguration());
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<PosDbContext>();
        _receiptService = _serviceProvider.GetRequiredService<IReceiptService>();
        _printerService = _serviceProvider.GetRequiredService<IPrinterService>();
        _barcodeScanner = _serviceProvider.GetRequiredService<IBarcodeScanner>();
        _cashDrawerService = _serviceProvider.GetRequiredService<ICashDrawerService>();
        
        // Ensure database is created
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON");
    }

    [Property]
    public bool ReceiptGeneration_ContainsAllSaleInformation(NonEmptyString invoiceNumber, PositiveInt totalAmountCents)
    {
        // Feature: offline-first-pos, Property 17: For any completed sale, a receipt should be generated containing all sale items, quantities, prices, and total amount
        // **Validates: Requirements 1.6**
        
        ClearDatabase();
        
        // Create a product for the sale
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = $"TEST{DateTime.Now.Ticks}",
            UnitPrice = 10.00m,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        _context.Products.Add(product);
        _context.SaveChanges();
        
        // Create a sale with items
        var totalAmount = totalAmountCents.Get / 100.0m; // Convert cents to dollars
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber.Get,
            TotalAmount = totalAmount,
            PaymentMethod = PaymentMethod.Cash,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };
        
        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = product.Id,
            Quantity = 2,
            UnitPrice = product.UnitPrice,
            Product = product
        };
        
        sale.Items.Add(saleItem);
        _context.Sales.Add(sale);
        _context.SaleItems.Add(saleItem);
        _context.SaveChanges();
        
        // Generate receipt
        var receiptTask = _receiptService.GenerateReceiptAsync(sale);
        receiptTask.Wait();
        var receipt = receiptTask.Result;
        
        // Verify receipt contains all required information
        if (receipt == null || string.IsNullOrEmpty(receipt.PlainText))
        {
            return false;
        }
        
        var receiptText = receipt.PlainText;
        
        // Check that receipt contains invoice number
        if (!receiptText.Contains(sale.InvoiceNumber))
        {
            return false;
        }
        
        // Check that receipt contains total amount
        if (!receiptText.Contains(sale.TotalAmount.ToString("C")) && !receiptText.Contains(sale.TotalAmount.ToString("F2")))
        {
            return false;
        }
        
        // Check that receipt contains product information
        if (!receiptText.Contains(product.Name))
        {
            return false;
        }
        
        // Check that receipt contains quantity
        if (!receiptText.Contains(saleItem.Quantity.ToString()))
        {
            return false;
        }
        
        // Check that receipt contains unit price
        if (!receiptText.Contains(saleItem.UnitPrice.ToString("F2")))
        {
            return false;
        }
        
        // Check that receipt contains payment method
        if (!receiptText.Contains(sale.PaymentMethod.ToString()))
        {
            return false;
        }
        
        return true;
    }

    [Property]
    public Property HardwareFailureGracefulHandling_PrinterNotConnected()
    {
        // Feature: offline-first-pos, Property 18: For any hardware connection failure (printer, scanner, cash drawer), the system should continue operating and provide appropriate error feedback without crashing
        // **Validates: Requirements 10.6**
        
        return Prop.ForAll<NonEmptyString>(invoiceNumberGen =>
        {
            ClearDatabase();
            
            // Ensure we have a valid invoice number (not just whitespace)
            var invoiceNumber = string.IsNullOrWhiteSpace(invoiceNumberGen.Get) ? "TEST-001" : invoiceNumberGen.Get.Trim();
            if (string.IsNullOrEmpty(invoiceNumber))
            {
                invoiceNumber = "TEST-001";
            }
            
            // Create a product for the sale
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Barcode = $"TEST{DateTime.Now.Ticks}",
                UnitPrice = 10.00m,
                DeviceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            _context.Products.Add(product);
            _context.SaveChanges();
            
            // Create a sale with items (required for valid receipt)
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = invoiceNumber,
                TotalAmount = 10.00m,
                PaymentMethod = PaymentMethod.Cash,
                DeviceId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                SyncStatus = SyncStatus.NotSynced
            };
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                ProductId = product.Id,
                Quantity = 1,
                UnitPrice = product.UnitPrice,
                Product = product
            };
            
            sale.Items.Add(saleItem);
            _context.Sales.Add(sale);
            _context.SaleItems.Add(saleItem);
            _context.SaveChanges();
            
            // Ensure printer is not connected
            var isConnectedTask = _printerService.IsConnectedAsync();
            isConnectedTask.Wait();
            var isConnected = isConnectedTask.Result;
            
            if (isConnected)
            {
                // Disconnect if connected
                var disconnectTask = _printerService.DisconnectAsync();
                disconnectTask.Wait();
            }
            
            // Try to print receipt with disconnected printer
            var printTask = _printerService.PrintReceiptAsync(sale);
            printTask.Wait();
            var printResult = printTask.Result;
            
            // Verify that the operation doesn't crash and provides appropriate error feedback
            if (printResult == null)
            {
                return false; // Should return a result, not null
            }
            
            // Should indicate failure
            if (printResult.Success)
            {
                return false; // Should fail when printer not connected
            }
            
            // Should provide error information
            if (string.IsNullOrEmpty(printResult.Message))
            {
                return false; // Should provide error message
            }
            
            // Should indicate the specific error type
            if (printResult.Error != PrintError.NotConnected)
            {
                return false; // Should indicate printer not connected error
            }
            
            return true; // Graceful error handling confirmed
        });
    }

    [Property]
    public Property HardwareFailureGracefulHandling_ScannerNotInitialized()
    {
        // Feature: offline-first-pos, Property 18: For any hardware connection failure (printer, scanner, cash drawer), the system should continue operating and provide appropriate error feedback without crashing
        // **Validates: Requirements 10.6**
        
        return Prop.ForAll<bool>(dummyValue =>
        {
            // Create a new scanner instance that's not initialized
            var scanner = new BarcodeScanner();
            
            // Try to scan without initialization
            var scanTask = scanner.ScanAsync();
            scanTask.Wait();
            var result = scanTask.Result;
            
            // Should return null (no crash) when not initialized
            if (result != null)
            {
                return false; // Should return null when not initialized/connected
            }
            
            // Try to start continuous scanning without initialization
            var continuousScanTask = scanner.StartContinuousScanAsync();
            continuousScanTask.Wait();
            var continuousResult = continuousScanTask.Result;
            
            // Should return false (failure) when not initialized
            if (continuousResult)
            {
                return false; // Should fail when not initialized
            }
            
            return true; // Graceful error handling confirmed
        });
    }

    [Property]
    public Property HardwareFailureGracefulHandling_CashDrawerNotConnected()
    {
        // Feature: offline-first-pos, Property 18: For any hardware connection failure (printer, scanner, cash drawer), the system should continue operating and provide appropriate error feedback without crashing
        // **Validates: Requirements 10.6**
        
        return Prop.ForAll<bool>(dummyValue =>
        {
            // Ensure cash drawer is not connected
            var isConnectedTask = _cashDrawerService.IsConnectedAsync();
            isConnectedTask.Wait();
            var isConnected = isConnectedTask.Result;
            
            if (isConnected)
            {
                // Disconnect if connected
                var disconnectTask = _cashDrawerService.DisconnectAsync();
                disconnectTask.Wait();
            }
            
            // Try to open drawer when not connected
            var openTask = _cashDrawerService.OpenDrawerAsync();
            openTask.Wait();
            var openResult = openTask.Result;
            
            // Verify that the operation doesn't crash and provides appropriate error feedback
            if (openResult == null)
            {
                return false; // Should return a result, not null
            }
            
            // Should indicate failure
            if (openResult.Success)
            {
                return false; // Should fail when not connected
            }
            
            // Should provide error information
            if (string.IsNullOrEmpty(openResult.Message))
            {
                return false; // Should provide error message
            }
            
            // Should indicate the specific error type
            if (openResult.Error != CashDrawerError.NotConnected)
            {
                return false; // Should indicate not connected error
            }
            
            return true; // Graceful error handling confirmed
        });
    }

    private void ClearDatabase()
    {
        _context.SaleItems.IgnoreQueryFilters().ExecuteDelete();
        _context.Sales.IgnoreQueryFilters().ExecuteDelete();
        _context.Stock.IgnoreQueryFilters().ExecuteDelete();
        _context.Products.IgnoreQueryFilters().ExecuteDelete();
    }

    public void Dispose()
    {
        _context?.Database.CloseConnection();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}