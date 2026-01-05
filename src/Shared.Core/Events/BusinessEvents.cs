using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Events;

/// <summary>
/// Base class for all business events
/// </summary>
public abstract class BaseEvent : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
}

/// <summary>
/// Event raised when a new business is created
/// </summary>
public class BusinessCreatedEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public Guid OwnerId { get; set; }
}

/// <summary>
/// Event raised when a business is updated
/// </summary>
public class BusinessUpdatedEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public Dictionary<string, object> ChangedFields { get; set; } = new();
}

/// <summary>
/// Event raised when a new shop is created
/// </summary>
public class ShopCreatedEvent : BaseEvent
{
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// Event raised when a shop is updated
/// </summary>
public class ShopUpdatedEvent : BaseEvent
{
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public Dictionary<string, object> ChangedFields { get; set; } = new();
}

/// <summary>
/// Event raised when a sale is completed
/// </summary>
public class SaleCompletedEvent : BaseEvent
{
    public Guid SaleId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}

/// <summary>
/// Event raised when inventory is updated
/// </summary>
public class InventoryUpdatedEvent : BaseEvent
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string UpdateReason { get; set; } = string.Empty;
}

/// <summary>
/// Event raised when a product is created
/// </summary>
public class ProductCreatedEvent : BaseEvent
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Event raised when a product is updated
/// </summary>
public class ProductUpdatedEvent : BaseEvent
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public Dictionary<string, object> ChangedFields { get; set; } = new();
}

/// <summary>
/// Event raised when a user is created
/// </summary>
public class UserCreatedEvent : BaseEvent
{
    public Guid UserId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string Username { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

/// <summary>
/// Event raised when a user logs in
/// </summary>
public class UserLoggedInEvent : BaseEvent
{
    public Guid UserId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

/// <summary>
/// Event raised when a user logs out
/// </summary>
public class UserLoggedOutEvent : BaseEvent
{
    public Guid UserId { get; set; }
    public Guid BusinessId { get; set; }
    public string Username { get; set; } = string.Empty;
    public TimeSpan SessionDuration { get; set; }
}

/// <summary>
/// Event raised when data synchronization starts
/// </summary>
public class SyncStartedEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public int RecordCount { get; set; }
}

/// <summary>
/// Event raised when data synchronization completes
/// </summary>
public class SyncCompletedEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public int SyncedRecords { get; set; }
    public int FailedRecords { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Event raised when a sync conflict occurs
/// </summary>
public class SyncConflictEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
}

/// <summary>
/// Event raised when low stock is detected
/// </summary>
public class LowStockDetectedEvent : BaseEvent
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
}

/// <summary>
/// Event raised when a product is about to expire
/// </summary>
public class ProductExpiryWarningEvent : BaseEvent
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public Guid BusinessId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Event raised when AI recommendations are generated
/// </summary>
public class AIRecommendationsGeneratedEvent : BaseEvent
{
    public Guid BusinessId { get; set; }
    public Guid? ShopId { get; set; }
    public string RecommendationType { get; set; } = string.Empty;
    public int RecommendationCount { get; set; }
    public decimal ConfidenceScore { get; set; }
}

/// <summary>
/// Event raised when system performance issues are detected
/// </summary>
public class PerformanceIssueDetectedEvent : BaseEvent
{
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();
    public string Severity { get; set; } = string.Empty;
}