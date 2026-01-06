using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DependencyInjection;
using Shared.Core.Services;
using Shared.Core.Entities;
using Shared.Core.Enums;
using Shared.Core.Repositories;
using Xunit;

namespace Shared.Core.Tests;

public class BarcodeIntegrationServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IBarcodeIntegrationService _barcodeIntegrationService;
    private readonly IProductRepository _productRepository;

    public BarcodeIntegrationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSharedCoreInMemory();
        _serviceProvider = services.BuildServiceProvider();
        
        _barcodeIntegrationService = _serviceProvider.GetRequiredService<IBarcodeIntegrationService>();
        _productRepository = _serviceProvider.GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task InitializeAsync_ShouldReturnTrue()
    {
        // Act
        var result = await _barcodeIntegrationService.InitializeAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateBarcodeFormatAsync_WithValidEAN13_ShouldReturnTrue()
    {
        // Arrange
        var validEAN13 = "1234567890123";

        // Act
        var result = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(validEAN13);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateBarcodeFormatAsync_WithInvalidBarcode_ShouldReturnFalse()
    {
        // Arrange
        var invalidBarcode = "!@#$%^&*()"; // Contains special characters not valid in any format

        // Act
        var result = await _barcodeIntegrationService.ValidateBarcodeFormatAsync(invalidBarcode);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task LookupProductByBarcodeAsync_WithExistingProduct_ShouldReturnProduct()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var barcode = "1234567890123";
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Barcode = barcode,
            UnitPrice = 10.50m,
            IsActive = true,
            DeviceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncStatus = SyncStatus.NotSynced
        };

        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();

        // Act
        var result = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, shopId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(product.Name, result.Name);
        Assert.Equal(product.Barcode, result.Barcode);
    }

    [Fact]
    public async Task LookupProductByBarcodeAsync_WithNonExistentProduct_ShouldReturnNull()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var barcode = "9999999999999";

        // Act
        var result = await _barcodeIntegrationService.LookupProductByBarcodeAsync(barcode, shopId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSupportedFormatsAsync_ShouldReturnSupportedFormats()
    {
        // Act
        var result = await _barcodeIntegrationService.GetSupportedFormatsAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, f => f.Name == "EAN-13");
        Assert.Contains(result, f => f.Name == "EAN-8");
        Assert.Contains(result, f => f.Name == "Code 128");
        Assert.Contains(result, f => f.Name == "Code 39");
        Assert.Contains(result, f => f.Name == "UPC-A");
    }

    [Fact]
    public async Task ProvideScanFeedbackAsync_WithSuccessfulScan_ShouldReturnSuccessFeedback()
    {
        // Arrange
        var scanResult = new BarcodeResult
        {
            IsSuccess = true,
            IsProductFound = true,
            IsInStock = true,
            Product = new Product { Name = "Test Product" }
        };

        // Act
        var feedback = await _barcodeIntegrationService.ProvideScanFeedbackAsync(scanResult);

        // Assert
        Assert.Equal(FeedbackType.Success, feedback.Type);
        Assert.True(feedback.ShouldPlayBeep);
        Assert.True(feedback.ShouldVibrate);
        Assert.Contains("Product found", feedback.VisualMessage);
    }

    [Fact]
    public async Task ProvideScanFeedbackAsync_WithProductNotFound_ShouldReturnWarningFeedback()
    {
        // Arrange
        var scanResult = new BarcodeResult
        {
            IsSuccess = true,
            IsProductFound = false
        };

        // Act
        var feedback = await _barcodeIntegrationService.ProvideScanFeedbackAsync(scanResult);

        // Assert
        Assert.Equal(FeedbackType.Warning, feedback.Type);
        Assert.False(feedback.ShouldPlayBeep);
        Assert.True(feedback.ShouldVibrate);
        Assert.Contains("Product not found", feedback.VisualMessage);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}