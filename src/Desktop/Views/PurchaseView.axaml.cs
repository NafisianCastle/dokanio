using Avalonia.Controls;
using Avalonia.Input;
using Desktop.Models;
using Desktop.ViewModels;

namespace Desktop.Views;

public partial class PurchaseView : UserControl
{
    public PurchaseView()
    {
        InitializeComponent();
    }

    private void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && 
            border.DataContext is Product product &&
            DataContext is PurchaseViewModel viewModel)
        {
            viewModel.AddProductCommand.Execute(product);
        }
    }
}