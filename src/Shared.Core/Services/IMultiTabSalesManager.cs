using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Service interface for managing multi-tab sales functionality
/// Provides session management, state isolation, and persistence
/// </summary>
public interface IMultiTabSalesManager
{
    /// <summary>
    /// Creates a new sale session
    /// </summary>
    /// <param name="request">Session creation request</param>
    /// <returns>Created session result</returns>
    Task<SessionOperationResult> CreateNewSaleSessionAsync(CreateSaleSessionRequest request);

    /// <summary>
    /// Gets a sale session by ID
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Session if found</returns>
    Task<SaleSessionDto?> GetSaleSessionAsync(Guid sessionId);

    /// <summary>
    /// Gets all active sessions for a user and device
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>List of active sessions</returns>
    Task<List<SaleSessionDto>> GetActiveSessionsAsync(Guid userId, Guid deviceId);

    /// <summary>
    /// Switches to a specific session (makes it the active session)
    /// </summary>
    /// <param name="sessionId">Session ID to switch to</param>
    /// <returns>True if switch was successful</returns>
    Task<bool> SwitchToSessionAsync(Guid sessionId);

    /// <summary>
    /// Saves the current state of a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="sessionData">Session data to save</param>
    /// <returns>Save result</returns>
    Task<SaveSessionStateResult> SaveSessionStateAsync(Guid sessionId, SaleSessionDto sessionData);

    /// <summary>
    /// Closes a session with optional state saving
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="saveState">Whether to save state before closing</param>
    /// <returns>True if closed successfully</returns>
    Task<bool> CloseSessionAsync(Guid sessionId, bool saveState = true);

    /// <summary>
    /// Updates session information
    /// </summary>
    /// <param name="request">Update request</param>
    /// <returns>Update result</returns>
    Task<SessionOperationResult> UpdateSessionAsync(UpdateSaleSessionRequest request);

    /// <summary>
    /// Adds an item to a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="item">Item to add</param>
    /// <returns>Updated session</returns>
    Task<SessionOperationResult> AddItemToSessionAsync(Guid sessionId, SaleSessionItemDto item);

    /// <summary>
    /// Removes an item from a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="itemId">Item ID to remove</param>
    /// <returns>Updated session</returns>
    Task<SessionOperationResult> RemoveItemFromSessionAsync(Guid sessionId, Guid itemId);

    /// <summary>
    /// Updates an item in a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="item">Updated item</param>
    /// <returns>Updated session</returns>
    Task<SessionOperationResult> UpdateItemInSessionAsync(Guid sessionId, SaleSessionItemDto item);

    /// <summary>
    /// Recalculates totals for a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Updated calculation</returns>
    Task<SaleSessionCalculationDto> RecalculateSessionTotalsAsync(Guid sessionId);

    /// <summary>
    /// Completes a session by converting it to a sale
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="paymentMethod">Payment method</param>
    /// <returns>Completed sale</returns>
    Task<SessionOperationResult> CompleteSessionAsync(Guid sessionId, PaymentMethod paymentMethod);

    /// <summary>
    /// Suspends a session (temporarily pauses it)
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if suspended successfully</returns>
    Task<bool> SuspendSessionAsync(Guid sessionId);

    /// <summary>
    /// Resumes a suspended session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>True if resumed successfully</returns>
    Task<bool> ResumeSessionAsync(Guid sessionId);

    /// <summary>
    /// Validates session state and data integrity
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateSessionAsync(Guid sessionId);

    /// <summary>
    /// Cleans up expired sessions
    /// </summary>
    /// <param name="expiryThreshold">Sessions older than this will be cleaned up</param>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupExpiredSessionsAsync(DateTime expiryThreshold);

    /// <summary>
    /// Gets the maximum number of concurrent sessions allowed
    /// </summary>
    /// <returns>Maximum session limit</returns>
    Task<int> GetMaxConcurrentSessionsAsync();

    /// <summary>
    /// Checks if a user can create a new session
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="deviceId">Device ID</param>
    /// <returns>True if user can create a new session</returns>
    Task<bool> CanCreateNewSessionAsync(Guid userId, Guid deviceId);
}