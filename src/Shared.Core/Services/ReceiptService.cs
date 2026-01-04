using Shared.Core.Entities;
using System.Text;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of IReceiptService for generating formatted receipts
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly ReceiptConfiguration _defaultConfiguration;

    public ReceiptService()
    {
        _defaultConfiguration = new ReceiptConfiguration();
    }

    public async Task<ReceiptContent> GenerateReceiptAsync(Sale sale, ReceiptConfiguration? configuration = null)
    {
        var config = configuration ?? _defaultConfiguration;
        var lines = new List<ReceiptLine>();
        var plainTextBuilder = new StringBuilder();

        // Header
        AddHeaderLines(lines, plainTextBuilder, config);
        
        // Sale information
        AddSaleInfoLines(lines, plainTextBuilder, sale);
        
        // Items
        await AddItemLines(lines, plainTextBuilder, sale);
        
        // Total
        AddTotalLines(lines, plainTextBuilder, sale);
        
        // Footer
        AddFooterLines(lines, plainTextBuilder, config, sale);

        return new ReceiptContent
        {
            PlainText = plainTextBuilder.ToString(),
            Lines = lines,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<ReceiptContent> GenerateTestReceiptAsync(ReceiptConfiguration? configuration = null)
    {
        var config = configuration ?? _defaultConfiguration;
        var lines = new List<ReceiptLine>();
        var plainTextBuilder = new StringBuilder();

        // Test header
        lines.Add(new ReceiptLine
        {
            Text = CenterText("TEST RECEIPT", config.PaperWidth),
            Type = ReceiptLineType.Header,
            Alignment = ReceiptAlignment.Center,
            Bold = true
        });
        plainTextBuilder.AppendLine(CenterText("TEST RECEIPT", config.PaperWidth));

        lines.Add(new ReceiptLine
        {
            Text = new string('=', config.PaperWidth),
            Type = ReceiptLineType.Separator,
            Alignment = ReceiptAlignment.Center
        });
        plainTextBuilder.AppendLine(new string('=', config.PaperWidth));

        // Test content
        lines.Add(new ReceiptLine
        {
            Text = "Printer Test Successful",
            Type = ReceiptLineType.Normal,
            Alignment = ReceiptAlignment.Center
        });
        plainTextBuilder.AppendLine("Printer Test Successful");

        lines.Add(new ReceiptLine
        {
            Text = $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            Type = ReceiptLineType.Normal,
            Alignment = ReceiptAlignment.Center
        });
        plainTextBuilder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        await Task.CompletedTask; // For async consistency

        return new ReceiptContent
        {
            PlainText = plainTextBuilder.ToString(),
            Lines = lines,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<ReceiptValidationResult> ValidateReceiptDataAsync(Sale sale)
    {
        var result = new ReceiptValidationResult { IsValid = true };

        // Validate sale basic information
        if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
        {
            result.Errors.Add("Invoice number is required");
            result.IsValid = false;
        }

        if (sale.TotalAmount <= 0)
        {
            result.Errors.Add("Total amount must be greater than zero");
            result.IsValid = false;
        }

        // Validate sale items
        if (sale.Items == null || !sale.Items.Any())
        {
            result.Errors.Add("Sale must have at least one item");
            result.IsValid = false;
        }
        else
        {
            foreach (var item in sale.Items)
            {
                if (item.Quantity <= 0)
                {
                    result.Errors.Add($"Item quantity must be greater than zero");
                    result.IsValid = false;
                }

                if (item.UnitPrice < 0)
                {
                    result.Errors.Add($"Item unit price cannot be negative");
                    result.IsValid = false;
                }

                if (item.Product == null)
                {
                    result.Warnings.Add("Some items may not display product names correctly");
                }
            }
        }

        await Task.CompletedTask; // For async consistency
        return result;
    }

    private void AddHeaderLines(List<ReceiptLine> lines, StringBuilder plainText, ReceiptConfiguration config)
    {
        // Shop name
        lines.Add(new ReceiptLine
        {
            Text = CenterText(config.ShopName, config.PaperWidth),
            Type = ReceiptLineType.Header,
            Alignment = ReceiptAlignment.Center,
            Bold = true
        });
        plainText.AppendLine(CenterText(config.ShopName, config.PaperWidth));

        // Shop address (if provided)
        if (!string.IsNullOrWhiteSpace(config.ShopAddress))
        {
            lines.Add(new ReceiptLine
            {
                Text = CenterText(config.ShopAddress, config.PaperWidth),
                Type = ReceiptLineType.Header,
                Alignment = ReceiptAlignment.Center
            });
            plainText.AppendLine(CenterText(config.ShopAddress, config.PaperWidth));
        }

        // Shop phone (if provided)
        if (!string.IsNullOrWhiteSpace(config.ShopPhone))
        {
            lines.Add(new ReceiptLine
            {
                Text = CenterText(config.ShopPhone, config.PaperWidth),
                Type = ReceiptLineType.Header,
                Alignment = ReceiptAlignment.Center
            });
            plainText.AppendLine(CenterText(config.ShopPhone, config.PaperWidth));
        }

        // Separator
        lines.Add(new ReceiptLine
        {
            Text = new string('=', config.PaperWidth),
            Type = ReceiptLineType.Separator,
            Alignment = ReceiptAlignment.Center
        });
        plainText.AppendLine(new string('=', config.PaperWidth));
    }

    private void AddSaleInfoLines(List<ReceiptLine> lines, StringBuilder plainText, Sale sale)
    {
        // Invoice number
        lines.Add(new ReceiptLine
        {
            Text = $"Invoice: {sale.InvoiceNumber}",
            Type = ReceiptLineType.Normal,
            Alignment = ReceiptAlignment.Left
        });
        plainText.AppendLine($"Invoice: {sale.InvoiceNumber}");

        // Date and time
        lines.Add(new ReceiptLine
        {
            Text = $"Date: {sale.CreatedAt:yyyy-MM-dd HH:mm:ss}",
            Type = ReceiptLineType.Normal,
            Alignment = ReceiptAlignment.Left
        });
        plainText.AppendLine($"Date: {sale.CreatedAt:yyyy-MM-dd HH:mm:ss}");

        // Payment method
        lines.Add(new ReceiptLine
        {
            Text = $"Payment: {sale.PaymentMethod}",
            Type = ReceiptLineType.Normal,
            Alignment = ReceiptAlignment.Left
        });
        plainText.AppendLine($"Payment: {sale.PaymentMethod}");

        // Separator
        lines.Add(new ReceiptLine
        {
            Text = new string('-', 48),
            Type = ReceiptLineType.Separator,
            Alignment = ReceiptAlignment.Center
        });
        plainText.AppendLine(new string('-', 48));
    }

    private async Task AddItemLines(List<ReceiptLine> lines, StringBuilder plainText, Sale sale)
    {
        // Items header
        lines.Add(new ReceiptLine
        {
            Text = FormatItemHeader(),
            Type = ReceiptLineType.Item,
            Alignment = ReceiptAlignment.Left,
            Bold = true
        });
        plainText.AppendLine(FormatItemHeader());

        // Individual items
        foreach (var item in sale.Items)
        {
            var productName = item.Product?.Name ?? "Unknown Product";
            
            // Check if this is a weight-based item
            if (item.Weight.HasValue && item.RatePerKilogram.HasValue)
            {
                var itemLine = FormatWeightBasedItemLine(productName, item.Weight.Value, item.RatePerKilogram.Value, item.TotalPrice);
                
                lines.Add(new ReceiptLine
                {
                    Text = itemLine,
                    Type = ReceiptLineType.Item,
                    Alignment = ReceiptAlignment.Left
                });
                plainText.AppendLine(itemLine);
            }
            else
            {
                var itemLine = FormatItemLine(productName, item.Quantity, item.UnitPrice);
                
                lines.Add(new ReceiptLine
                {
                    Text = itemLine,
                    Type = ReceiptLineType.Item,
                    Alignment = ReceiptAlignment.Left
                });
                plainText.AppendLine(itemLine);
            }

            // Add batch number if available (for medicines)
            if (!string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                var batchLine = $"  Batch: {item.BatchNumber}";
                lines.Add(new ReceiptLine
                {
                    Text = batchLine,
                    Type = ReceiptLineType.Item,
                    Alignment = ReceiptAlignment.Left
                });
                plainText.AppendLine(batchLine);
            }
        }

        await Task.CompletedTask; // For async consistency
    }

    private void AddTotalLines(List<ReceiptLine> lines, StringBuilder plainText, Sale sale)
    {
        // Separator
        lines.Add(new ReceiptLine
        {
            Text = new string('-', 48),
            Type = ReceiptLineType.Separator,
            Alignment = ReceiptAlignment.Center
        });
        plainText.AppendLine(new string('-', 48));

        // Total
        var totalLine = $"TOTAL: {sale.TotalAmount:C}".PadLeft(48);
        lines.Add(new ReceiptLine
        {
            Text = totalLine,
            Type = ReceiptLineType.Total,
            Alignment = ReceiptAlignment.Right,
            Bold = true
        });
        plainText.AppendLine(totalLine);
    }

    private void AddFooterLines(List<ReceiptLine> lines, StringBuilder plainText, ReceiptConfiguration config, Sale sale)
    {
        // Separator
        lines.Add(new ReceiptLine
        {
            Text = new string('=', config.PaperWidth),
            Type = ReceiptLineType.Separator,
            Alignment = ReceiptAlignment.Center
        });
        plainText.AppendLine(new string('=', config.PaperWidth));

        // Footer message
        if (!string.IsNullOrWhiteSpace(config.FooterMessage))
        {
            lines.Add(new ReceiptLine
            {
                Text = CenterText(config.FooterMessage, config.PaperWidth),
                Type = ReceiptLineType.Footer,
                Alignment = ReceiptAlignment.Center
            });
            plainText.AppendLine(CenterText(config.FooterMessage, config.PaperWidth));
        }

        // Barcode (if enabled)
        if (config.PrintBarcode)
        {
            lines.Add(new ReceiptLine
            {
                Text = CenterText($"*{sale.InvoiceNumber}*", config.PaperWidth),
                Type = ReceiptLineType.Barcode,
                Alignment = ReceiptAlignment.Center
            });
            plainText.AppendLine(CenterText($"*{sale.InvoiceNumber}*", config.PaperWidth));
        }
    }

    private string CenterText(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        
        var padding = (width - text.Length) / 2;
        return text.PadLeft(text.Length + padding).PadRight(width);
    }

    private string FormatItemHeader()
    {
        return "Item                    Qty   Price   Total";
    }

    private string FormatItemLine(string productName, int quantity, decimal unitPrice)
    {
        var total = quantity * unitPrice;
        
        // Truncate product name if too long
        var name = productName.Length > 20 ? productName.Substring(0, 17) + "..." : productName;
        
        return $"{name,-20} {quantity,3} {unitPrice,7:F2} {total,7:F2}";
    }

    private string FormatWeightBasedItemLine(string productName, decimal weight, decimal ratePerKg, decimal totalPrice)
    {
        // Truncate product name if too long
        var name = productName.Length > 20 ? productName.Substring(0, 17) + "..." : productName;
        
        // Format weight with appropriate precision (show up to 3 decimal places, remove trailing zeros)
        var weightStr = weight.ToString("0.###");
        
        return $"{name,-20} {weightStr}kg @{ratePerKg:F2} {totalPrice,7:F2}";
    }
}