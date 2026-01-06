using Microsoft.Extensions.Logging;
using System.Text.Json;
using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Repositories;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service implementation for managing multi-tab sales functionality
/// </summary>
public class MultiTabSalesManager : IMultiTabSalesManager
{
    private readonly ISaleSessionRepository _sessionRepository;
    private readonly ISaleService _saleService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<MultiTabSalesManager> _logger;
    
    // Configuration constants
    private const int DefaultMaxConcurrentSessions = 5;
    private const int DefaultSessionExpiryHours = 24;

    public MultiTabSalesManager(
        ISaleSessionRepository sessionRepository,
        ISaleService saleService,
        IConfigurationService configurationService,
        ILogger<MultiTabSalesManager> logger)
    {
        _sessionRepository = sessionRepository;
        _saleService = saleService;
        _configurationService = configurationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> CreateNewSaleSessionAsync(CreateSaleSessionRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.TabName))
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Tab name is required",
                    Errors = new List<string> { "Tab name cannot be empty" }
                };
            }

            // Check if user can create a new session
            if (!await CanCreateNewSessionAsync(request.UserId, request.DeviceId))
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Maximum number of concurrent sessions reached",
                    Errors = new List<string> { "Cannot create more sessions. Please close existing sessions first." }
                };
            }

            // Check if tab name already exists for this user/device
            var existingSession = await _sessionRepository.GetSessionByTabNameAsync(
                request.TabName, request.UserId, request.DeviceId);
            
            if (existingSession != null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Tab name already exists",
                    Errors = new List<string> { $"A session with tab name '{request.TabName}' already exists" }
                };
            }

            // Create new session
            var session = new SaleSession
            {
                Id = Guid.NewGuid(),
                TabName = request.TabName,
                ShopId = request.ShopId,
                UserId = request.UserId,
                DeviceId = request.DeviceId,
                CustomerId = request.CustomerId,
                State = SessionState.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Initialize empty session data
            var sessionData = new SaleSessionDto
            {
                Id = session.Id,
                TabName = session.TabName,
                ShopId = session.ShopId,
                UserId = session.UserId,
                DeviceId = session.DeviceId,
                CustomerId = session.CustomerId,
                State = session.State,
                CreatedAt = session.CreatedAt,
                LastModified = session.LastModified,
                IsActive = session.IsActive,
                Items = new List<SaleSessionItemDto>(),
                Calculation = new SaleSessionCalculationDto()
            };

            session.SessionData = JsonSerializer.Serialize(sessionData);

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            _logger.LogInformation("Created new sale session {SessionId} with tab name '{TabName}' for user {UserId}",
                session.Id, session.TabName, session.UserId);

            return new SessionOperationResult
            {
                Success = true,
                Message = "Session created successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new sale session for user {UserId}", request.UserId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to create session",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<SaleSessionDto?> GetSaleSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
                return null;

            return DeserializeSessionData(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sale session {SessionId}", sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<SaleSessionDto>> GetActiveSessionsAsync(Guid userId, Guid deviceId)
    {
        try
        {
            var sessions = await _sessionRepository.GetActiveSessionsAsync(userId, deviceId);
            var result = new List<SaleSessionDto>();

            foreach (var session in sessions)
            {
                var sessionDto = DeserializeSessionData(session);
                if (sessionDto != null)
                {
                    result.Add(sessionDto);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions for user {UserId}", userId);
            return new List<SaleSessionDto>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SwitchToSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null || !session.IsActive)
                return false;

            // Update last modified to indicate activity
            await _sessionRepository.UpdateLastModifiedAsync(sessionId);

            _logger.LogInformation("Switched to session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<SaveSessionStateResult> SaveSessionStateAsync(Guid sessionId, SaleSessionDto sessionData)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new SaveSessionStateResult
                {
                    Success = false,
                    Message = "Session not found",
                    Errors = new List<string> { "Session does not exist" }
                };
            }

            // Update session data
            session.SessionData = JsonSerializer.Serialize(sessionData);
            session.LastModified = DateTime.UtcNow;
            session.CustomerId = sessionData.CustomerId;
            session.PaymentMethod = sessionData.PaymentMethod;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            _logger.LogDebug("Saved session state for session {SessionId}", sessionId);

            return new SaveSessionStateResult
            {
                Success = true,
                Message = "Session state saved successfully",
                SavedAt = session.LastModified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving session state for session {SessionId}", sessionId);
            return new SaveSessionStateResult
            {
                Success = false,
                Message = "Failed to save session state",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> CloseSessionAsync(Guid sessionId, bool saveState = true)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
                return false;

            if (saveState)
            {
                // Save current state before closing
                session.LastModified = DateTime.UtcNow;
            }

            session.IsActive = false;
            session.State = SessionState.Cancelled;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            _logger.LogInformation("Closed session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> UpdateSessionAsync(UpdateSaleSessionRequest request)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(request.SessionId);
            if (session == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session not found"
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to load session data"
                };
            }

            // Update session properties
            if (!string.IsNullOrWhiteSpace(request.TabName))
            {
                session.TabName = request.TabName;
                sessionData.TabName = request.TabName;
            }

            if (request.PaymentMethod.HasValue)
            {
                session.PaymentMethod = request.PaymentMethod.Value;
                sessionData.PaymentMethod = request.PaymentMethod.Value;
            }

            if (request.CustomerId.HasValue)
            {
                session.CustomerId = request.CustomerId.Value;
                sessionData.CustomerId = request.CustomerId.Value;
            }

            if (request.Items != null)
            {
                sessionData.Items = request.Items;
                // Recalculate totals
                sessionData.Calculation = await CalculateSessionTotals(sessionData.Items);
            }

            // Save updated session
            session.SessionData = JsonSerializer.Serialize(sessionData);
            session.LastModified = DateTime.UtcNow;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return new SessionOperationResult
            {
                Success = true,
                Message = "Session updated successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session {SessionId}", request.SessionId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to update session",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> AddItemToSessionAsync(Guid sessionId, SaleSessionItemDto item)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session not found"
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to load session data"
                };
            }

            // Check if item already exists
            var existingItem = sessionData.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem != null)
            {
                // Update quantity
                existingItem.Quantity += item.Quantity;
                existingItem.LineTotal = existingItem.Quantity * existingItem.UnitPrice - existingItem.DiscountAmount;
            }
            else
            {
                // Add new item
                item.Id = Guid.NewGuid();
                item.LineTotal = item.Quantity * item.UnitPrice - item.DiscountAmount;
                sessionData.Items.Add(item);
            }

            // Recalculate totals
            sessionData.Calculation = await CalculateSessionTotals(sessionData.Items);

            // Save session
            var saveResult = await SaveSessionStateAsync(sessionId, sessionData);
            if (!saveResult.Success)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to save session after adding item",
                    Errors = saveResult.Errors
                };
            }

            return new SessionOperationResult
            {
                Success = true,
                Message = "Item added successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to session {SessionId}", sessionId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to add item",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> RemoveItemFromSessionAsync(Guid sessionId, Guid itemId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session not found"
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to load session data"
                };
            }

            // Remove item
            var itemToRemove = sessionData.Items.FirstOrDefault(i => i.Id == itemId);
            if (itemToRemove == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Item not found in session"
                };
            }

            sessionData.Items.Remove(itemToRemove);

            // Recalculate totals
            sessionData.Calculation = await CalculateSessionTotals(sessionData.Items);

            // Save session
            var saveResult = await SaveSessionStateAsync(sessionId, sessionData);
            if (!saveResult.Success)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to save session after removing item",
                    Errors = saveResult.Errors
                };
            }

            return new SessionOperationResult
            {
                Success = true,
                Message = "Item removed successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from session {SessionId}", sessionId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to remove item",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> UpdateItemInSessionAsync(Guid sessionId, SaleSessionItemDto item)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session not found"
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to load session data"
                };
            }

            // Find and update item
            var existingItem = sessionData.Items.FirstOrDefault(i => i.Id == item.Id);
            if (existingItem == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Item not found in session"
                };
            }

            // Update item properties
            existingItem.Quantity = item.Quantity;
            existingItem.UnitPrice = item.UnitPrice;
            existingItem.DiscountAmount = item.DiscountAmount;
            existingItem.Weight = item.Weight;
            existingItem.LineTotal = item.Quantity * item.UnitPrice - item.DiscountAmount;

            // Recalculate totals
            sessionData.Calculation = await CalculateSessionTotals(sessionData.Items);

            // Save session
            var saveResult = await SaveSessionStateAsync(sessionId, sessionData);
            if (!saveResult.Success)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Failed to save session after updating item",
                    Errors = saveResult.Errors
                };
            }

            return new SessionOperationResult
            {
                Success = true,
                Message = "Item updated successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item in session {SessionId}", sessionId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to update item",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<SaleSessionCalculationDto> RecalculateSessionTotalsAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
                return new SaleSessionCalculationDto();

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
                return new SaleSessionCalculationDto();

            var calculation = await CalculateSessionTotals(sessionData.Items);
            
            // Update session with new calculation
            sessionData.Calculation = calculation;
            await SaveSessionStateAsync(sessionId, sessionData);

            return calculation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating session totals for session {SessionId}", sessionId);
            return new SaleSessionCalculationDto();
        }
    }

    /// <inheritdoc />
    public async Task<SessionOperationResult> CompleteSessionAsync(Guid sessionId, PaymentMethod paymentMethod)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session not found"
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null || !sessionData.Items.Any())
            {
                return new SessionOperationResult
                {
                    Success = false,
                    Message = "Session has no items to complete"
                };
            }

            // Create sale from session
            var invoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 10000:D4}";
            var sale = await _saleService.CreateSaleAsync(invoiceNumber, session.DeviceId);

            // Add items to sale
            foreach (var item in sessionData.Items)
            {
                await _saleService.AddItemToSaleAsync(
                    sale.Id, 
                    item.ProductId, 
                    (int)item.Quantity, 
                    item.UnitPrice, 
                    item.BatchNumber);
            }

            // Complete the sale
            var completedSale = await _saleService.CompleteSaleAsync(sale.Id, paymentMethod);

            // Update session
            session.SaleId = completedSale.Id;
            session.State = SessionState.Completed;
            session.IsActive = false;
            session.LastModified = DateTime.UtcNow;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            _logger.LogInformation("Completed session {SessionId} and created sale {SaleId}", sessionId, completedSale.Id);

            return new SessionOperationResult
            {
                Success = true,
                Message = "Session completed successfully",
                Session = sessionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session {SessionId}", sessionId);
            return new SessionOperationResult
            {
                Success = false,
                Message = "Failed to complete session",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> SuspendSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
                return false;

            session.State = SessionState.Suspended;
            session.LastModified = DateTime.UtcNow;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ResumeSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
                return false;

            session.State = SessionState.Active;
            session.LastModified = DateTime.UtcNow;

            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Session not found" }
                };
            }

            var sessionData = DeserializeSessionData(session);
            if (sessionData == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid session data" }
                };
            }

            var errors = new List<string>();

            // Validate session state
            if (!session.IsActive && session.State == SessionState.Active)
            {
                errors.Add("Session marked as active but is not active");
            }

            // Validate items
            foreach (var item in sessionData.Items)
            {
                if (item.Quantity <= 0)
                {
                    errors.Add($"Item {item.ProductName} has invalid quantity");
                }

                if (item.UnitPrice < 0)
                {
                    errors.Add($"Item {item.ProductName} has negative unit price");
                }
            }

            return new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session {SessionId}", sessionId);
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredSessionsAsync(DateTime expiryThreshold)
    {
        try
        {
            var expiredSessions = await _sessionRepository.GetExpiredSessionsAsync(expiryThreshold);
            int cleanedCount = 0;

            foreach (var session in expiredSessions)
            {
                session.State = SessionState.Expired;
                session.IsActive = false;
                session.LastModified = DateTime.UtcNow;
                cleanedCount++;
            }

            if (cleanedCount > 0)
            {
                await _sessionRepository.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {Count} expired sessions", cleanedCount);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sessions");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetMaxConcurrentSessionsAsync()
    {
        try
        {
            // Try to get from configuration service
            var config = await _configurationService.GetConfigurationAsync("MaxConcurrentSessions", DefaultMaxConcurrentSessions);
            return config;
        }
        catch
        {
            return DefaultMaxConcurrentSessions;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanCreateNewSessionAsync(Guid userId, Guid deviceId)
    {
        try
        {
            var activeSessions = await _sessionRepository.GetActiveSessionsAsync(userId, deviceId);
            var maxSessions = await GetMaxConcurrentSessionsAsync();
            
            return activeSessions.Count() < maxSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} can create new session", userId);
            return false;
        }
    }

    #region Private Methods

    private SaleSessionDto? DeserializeSessionData(SaleSession session)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(session.SessionData))
            {
                // Return default session data
                return new SaleSessionDto
                {
                    Id = session.Id,
                    TabName = session.TabName,
                    ShopId = session.ShopId,
                    UserId = session.UserId,
                    DeviceId = session.DeviceId,
                    CustomerId = session.CustomerId,
                    PaymentMethod = session.PaymentMethod,
                    State = session.State,
                    CreatedAt = session.CreatedAt,
                    LastModified = session.LastModified,
                    IsActive = session.IsActive,
                    SaleId = session.SaleId,
                    Items = new List<SaleSessionItemDto>(),
                    Calculation = new SaleSessionCalculationDto()
                };
            }

            var sessionData = JsonSerializer.Deserialize<SaleSessionDto>(session.SessionData);
            if (sessionData != null)
            {
                // Ensure basic properties are synchronized
                sessionData.Id = session.Id;
                sessionData.State = session.State;
                sessionData.IsActive = session.IsActive;
                sessionData.LastModified = session.LastModified;
                sessionData.SaleId = session.SaleId;
            }

            return sessionData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing session data for session {SessionId}", session.Id);
            return null;
        }
    }

    private async Task<SaleSessionCalculationDto> CalculateSessionTotals(List<SaleSessionItemDto> items)
    {
        try
        {
            var subtotal = items.Sum(i => i.LineTotal);
            var totalDiscount = items.Sum(i => i.DiscountAmount);
            var totalTax = items.Sum(i => i.TaxAmount);
            var finalTotal = subtotal - totalDiscount + totalTax;

            var breakdown = new List<CalculationBreakdownDto>
            {
                new() { Description = "Subtotal", Amount = subtotal, Type = CalculationType.Subtotal },
                new() { Description = "Total Discount", Amount = totalDiscount, Type = CalculationType.Discount },
                new() { Description = "Total Tax", Amount = totalTax, Type = CalculationType.Tax },
                new() { Description = "Final Total", Amount = finalTotal, Type = CalculationType.Total }
            };

            return new SaleSessionCalculationDto
            {
                Subtotal = subtotal,
                TotalDiscount = totalDiscount,
                TotalTax = totalTax,
                FinalTotal = finalTotal,
                Breakdown = breakdown,
                CalculatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating session totals");
            return new SaleSessionCalculationDto();
        }
    }

    #endregion
}