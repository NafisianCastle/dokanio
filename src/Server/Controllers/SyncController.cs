using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Shared.Core.Services;
using System.Security.Claims;

namespace Server.Controllers;

/// <summary>
/// Controller for data synchronization operations
/// Handles bulk upload and download of POS data
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ServerDbContext _context;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ServerDbContext context, ILogger<SyncController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint (no authentication required)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new { message = "POS Server API is running", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Uploads local changes from a device to the server
    /// </summary>
    /// <param name="request">Upload request containing local changes</param>
    /// <returns>Result of the upload operation</returns>
    [HttpPost("upload")]
    public async Task<ActionResult<SyncApiResult>> UploadChanges([FromBody] SyncUploadRequest request)
    {
        try
        {
            var deviceId = GetDeviceIdFromToken();
            if (deviceId == null)
            {
                return Unauthorized(new SyncApiResult
                {
                    Success = false,
                    Message = "Invalid device token",
                    StatusCode = 401,
                    Errors = new List<string> { "Device authentication failed" }
                });
            }

            if (request.DeviceId != deviceId)
            {
                return Forbid();
            }

            _logger.LogInformation("Processing upload request from device {DeviceId} with {SalesCount} sales and {StockUpdatesCount} stock updates",
                deviceId, request.Sales.Count, request.StockUpdates.Count);

            var result = new SyncApiResult
            {
                Success = true,
                Message = "Upload completed successfully",
                StatusCode = 200
            };

            var uploadedRecords = 0;

            // Process sales
            foreach (var saleDto in request.Sales)
            {
                await ProcessSaleUpload(saleDto, deviceId.Value);
                uploadedRecords++;
            }

            // Process stock updates
            foreach (var stockUpdateDto in request.StockUpdates)
            {
                await ProcessStockUpdate(stockUpdateDto, deviceId.Value);
                uploadedRecords++;
            }

            // Update device last sync timestamp
            await UpdateDeviceLastSync(deviceId.Value);

            await _context.SaveChangesAsync();

            result.Message = $"Successfully uploaded {uploadedRecords} records";
            _logger.LogInformation("Upload completed for device {DeviceId}: {RecordCount} records processed", 
                deviceId, uploadedRecords);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing upload request from device {DeviceId}", request.DeviceId);
            return StatusCode(500, new SyncApiResult
            {
                Success = false,
                Message = "Internal server error during upload",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Downloads changes from the server to a device
    /// </summary>
    /// <param name="lastSyncTimestamp">Timestamp of the last successful sync</param>
    /// <returns>Changes from the server since the last sync</returns>
    [HttpGet("download")]
    public async Task<ActionResult<SyncApiResult<SyncDownloadResponse>>> DownloadChanges([FromQuery] DateTime? lastSyncTimestamp)
    {
        try
        {
            var deviceId = GetDeviceIdFromToken();
            if (deviceId == null)
            {
                return Unauthorized(new SyncApiResult<SyncDownloadResponse>
                {
                    Success = false,
                    Message = "Invalid device token",
                    StatusCode = 401,
                    Errors = new List<string> { "Device authentication failed" }
                });
            }

            var syncTimestamp = lastSyncTimestamp ?? DateTime.MinValue;
            
            _logger.LogInformation("Processing download request from device {DeviceId} since {LastSyncTimestamp}",
                deviceId, syncTimestamp);

            // Get updated products since last sync
            var products = await _context.Products
                .Where(p => p.UpdatedAt > syncTimestamp && !p.IsDeleted)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Barcode = p.Barcode,
                    Category = p.Category,
                    UnitPrice = p.UnitPrice,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    DeviceId = p.DeviceId,
                    BatchNumber = p.BatchNumber,
                    ExpiryDate = p.ExpiryDate,
                    PurchasePrice = p.PurchasePrice,
                    SellingPrice = p.SellingPrice
                })
                .ToListAsync();

            // Get updated stock since last sync
            var stock = await _context.Stock
                .Where(s => s.LastUpdatedAt > syncTimestamp && !s.IsDeleted)
                .Select(s => new StockDto
                {
                    Id = s.Id,
                    ProductId = s.ProductId,
                    Quantity = s.Quantity,
                    LastUpdatedAt = s.LastUpdatedAt,
                    DeviceId = s.DeviceId
                })
                .ToListAsync();

            var response = new SyncDownloadResponse
            {
                ServerTimestamp = DateTime.UtcNow,
                Products = products,
                Stock = stock,
                HasMoreData = false // For now, we return all data in one batch
            };

            _logger.LogInformation("Download completed for device {DeviceId}: {ProductCount} products, {StockCount} stock entries",
                deviceId, products.Count, stock.Count);

            return Ok(new SyncApiResult<SyncDownloadResponse>
            {
                Success = true,
                Message = "Download completed successfully",
                StatusCode = 200,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing download request from device {DeviceId}", GetDeviceIdFromToken());
            return StatusCode(500, new SyncApiResult<SyncDownloadResponse>
            {
                Success = false,
                Message = "Internal server error during download",
                StatusCode = 500,
                Errors = new List<string> { ex.Message }
            });
        }
    }

    private Guid? GetDeviceIdFromToken()
    {
        var deviceIdClaim = User.FindFirst("device_id");
        if (deviceIdClaim != null && Guid.TryParse(deviceIdClaim.Value, out var deviceId))
        {
            return deviceId;
        }
        return null;
    }

    private async Task ProcessSaleUpload(SaleDto saleDto, Guid deviceId)
    {
        // Check if sale already exists (idempotency)
        var existingSale = await _context.Sales
            .FirstOrDefaultAsync(s => s.Id == saleDto.Id);

        if (existingSale != null)
        {
            _logger.LogDebug("Sale {SaleId} already exists, skipping", saleDto.Id);
            return;
        }

        // Create new sale entity
        var sale = new Shared.Core.Entities.Sale
        {
            Id = saleDto.Id,
            InvoiceNumber = saleDto.InvoiceNumber,
            TotalAmount = saleDto.TotalAmount,
            PaymentMethod = (Shared.Core.Enums.PaymentMethod)saleDto.PaymentMethod,
            CreatedAt = saleDto.CreatedAt,
            DeviceId = deviceId,
            ServerSyncedAt = DateTime.UtcNow,
            SyncStatus = Shared.Core.Enums.SyncStatus.Synced
        };

        // Add sale items
        foreach (var itemDto in saleDto.Items)
        {
            var saleItem = new Shared.Core.Entities.SaleItem
            {
                Id = itemDto.Id,
                SaleId = itemDto.SaleId,
                ProductId = itemDto.ProductId,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                BatchNumber = itemDto.BatchNumber
            };
            sale.Items.Add(saleItem);
        }

        _context.Sales.Add(sale);
    }

    private async Task ProcessStockUpdate(StockUpdateDto stockUpdateDto, Guid deviceId)
    {
        // Find existing stock entry or create new one
        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductId == stockUpdateDto.ProductId && s.DeviceId == deviceId);

        if (stock == null)
        {
            stock = new Shared.Core.Entities.Stock
            {
                Id = Guid.NewGuid(),
                ProductId = stockUpdateDto.ProductId,
                Quantity = stockUpdateDto.QuantityChange,
                LastUpdatedAt = stockUpdateDto.UpdatedAt,
                DeviceId = deviceId,
                ServerSyncedAt = DateTime.UtcNow,
                SyncStatus = Shared.Core.Enums.SyncStatus.Synced
            };
            _context.Stock.Add(stock);
        }
        else
        {
            // Apply quantity change
            stock.Quantity += stockUpdateDto.QuantityChange;
            stock.LastUpdatedAt = stockUpdateDto.UpdatedAt;
            stock.ServerSyncedAt = DateTime.UtcNow;
            stock.SyncStatus = Shared.Core.Enums.SyncStatus.Synced;
        }
    }

    private async Task UpdateDeviceLastSync(Guid deviceId)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device != null)
        {
            device.LastSyncAt = DateTime.UtcNow;
        }
    }
}