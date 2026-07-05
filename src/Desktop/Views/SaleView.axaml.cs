using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Desktop.Models;
using Desktop.ViewModels;
using System.Reactive;

namespace Desktop.Views;

public partial class SaleView : UserControl
{
    public SaleView()
    {
        InitializeComponent();
        KeyDown += OnGlobalKeyDown;
        Loaded  += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        this.FindControl<TextBox>("SearchBox")?.Focus();
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SaleViewModel vm) return;

        switch (e.Key)
        {
            case Key.F1:
                this.FindControl<TextBox>("SearchBox")?.Focus();
                e.Handled = true;
                break;

            case Key.F2:
                vm.StartBarcodeScanCommand.Execute(Unit.Default);
                e.Handled = true;
                break;

            case Key.F9:
                vm.CompleteSaleCommand.Execute(Unit.Default);
                e.Handled = true;
                break;

            case Key.Escape:
                vm.ResetSaleCommand.Execute(Unit.Default);
                e.Handled = true;
                break;
        }
    }

    private void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { DataContext: Product product } &&
            DataContext is SaleViewModel vm)
            vm.AddProductCommand.Execute(product);
    }

    private void OnProductKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Enter || e.Key == Key.Space) &&
            sender is Border { DataContext: Product product } &&
            DataContext is SaleViewModel vm)
        {
            vm.AddProductCommand.Execute(product);
            e.Handled = true;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SaleViewModel vm)
        {
            vm.SearchProductsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnQuantityChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is SaleViewModel vm)
            vm.RecalculateTotalsCommand.Execute(Unit.Default);
    }

    private void OnPhoneNumberLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Text: { Length: > 0 } } &&
            DataContext is SaleViewModel vm)
            vm.LookupCustomerCommand.Execute(Unit.Default);
    }
}
