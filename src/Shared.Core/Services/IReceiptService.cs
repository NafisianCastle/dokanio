using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for receipt generation service
/// Provides receipt formatting and generation capabilities
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Generates a formatted receipt for a sale
    /// </summary>
    /// <param name="sale">Sale to generate receipt for</param>
    /// <param name="configuration">Receipt formatting configuration</param>
    /// <returns>Formatted receipt content</returns>
    Task<ReceiptContent> GenerateReceiptAsync(Sale sale, ReceiptConfiguration? configuration = null);
    
    /// <summary>
    /// Generates a test receipt for printer testing
    /// </summary>
    /// <param name="configuration">Receipt formatting configuration</param>
    /// <returns>Test receipt content</returns>
    Task<ReceiptContent> GenerateTestReceiptAsync(ReceiptConfiguration? configuration = null);
    
    /// <summary>
    /// Validates that a sale has all required information for receipt generation
    /// </summary>
    /// <param name="sale">Sale to validate</param>
    /// <returns>Validation result</returns>
    Task<ReceiptValidationResult> ValidateReceiptDataAsync(Sale sale);
}

/// <summary>
/// Content of a generated receipt
/// </summary>
public class ReceiptContent
{
    public string PlainText { get; set; } = string.Empty;
    public byte[]? PrinterCommands { get; set; }
    public List<ReceiptLine> Lines { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A line in a receipt
/// </summary>
public class ReceiptLine
{
    public string Text { get; set; } = string.Empty;
    public ReceiptLineType Type { get; set; } = ReceiptLineType.Normal;
    public ReceiptAlignment Alignment { get; set; } = ReceiptAlignment.Left;
    public bool Bold { get; set; } = false;
    public bool Underline { get; set; } = false;
}

/// <summary>
/// Types of receipt lines
/// </summary>
public enum ReceiptLineType
{
    Normal = 0,
    Header = 1,
    Separator = 2,
    Item = 3,
    Total = 4,
    Footer = 5,
    Barcode = 6
}

/// <summary>
/// Text alignment for receipt lines
/// </summary>
public enum ReceiptAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Result of receipt data validation
/// </summary>
public class ReceiptValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}