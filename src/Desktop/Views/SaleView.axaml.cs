using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Desktop.Models;
using Desktop.ViewModels;

namespace Desktop.Views;

public partial class SaleView : UserControl
{
    public SaleView()
    {
        InitializeComponent();
        
        // Set up keyboard shortcuts
        KeyDown += OnGlobalKeyDown;
        
        // Set initial focus
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Set focus to search box when view loads
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox?.Focus();
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SaleViewModel viewModel) return;

        switch (e.Key)
        {
            case Key.F1:
                // Focus search box
                var searchBox = this.FindControl<TextBox>("SearchBox");
                searchBox?.Focus();
                e.Handled = true;
                break;
                
            case Key.F2:
                // Start barcode scan
                if (viewModel.StartBarcodeScanCommand.CanExecute(null))
                {
                    viewModel.StartBarcodeScanCommand.Execute(null);
                }
                e.Handled = true;
                break;
                
            case Key.F9:
                // Complete sale
                if (viewModel.CompleteSaleCommand.CanExecute(null))
                {
                    viewModel.CompleteSaleCommand.Execute(null);
                }
                e.Handled = true;
                break;
                
            case Key.Escape:
                // Reset sale (with confirmation)
                if (viewModel.ResetSaleCommand.CanExecute(null))
                {
                    viewModel.ResetSaleCommand.Execute(null);
                }
                e.Handled = true;
                break;
        }
    }

    private void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && 
            border.DataContext is Product product &&
            DataContext is SaleViewModel viewModel)
        {
            viewModel.AddProductCommand.Execute(product);
        }
    }

    private void OnProductKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (sender is Border border && 
                border.DataContext is Product product &&
                DataContext is SaleViewModel viewModel)
            {
                viewModel.AddProductCommand.Execute(product);
                e.Handled = true;
            }
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SaleViewModel viewModel)
        {
            if (viewModel.SearchProductsCommand.CanExecute(null))
            {
                viewModel.SearchProductsCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void OnQuantityChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is SaleViewModel viewModel)
        {
            // Trigger recalculation when quantity changes
            viewModel.RecalculateTotalsCommand?.Execute(null);
        }
    }

    private void OnPhoneNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && 
            !string.IsNullOrWhiteSpace(textBox.Text) &&
            DataContext is SaleViewModel viewModel)
        {
            // Auto-trigger customer lookup when phone number is entered
            if (viewModel.LookupCustomerCommand.CanExecute(null))
            {
                viewModel.LookupCustomerCommand.Execute(null);
            }
        }
    }
}