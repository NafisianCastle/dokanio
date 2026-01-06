using Avalonia.Controls;
using Avalonia.Input;
using Desktop.ViewModels;

namespace Desktop.Views;

public partial class BarcodeScannerWindow : Window
{
    public BarcodeScannerWindow()
    {
        InitializeComponent();
    }

    public BarcodeScannerWindow(BarcodeScannerWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        // Subscribe to close event
        viewModel.CloseRequested += (sender, e) => Close(e);
    }

    private void OnManualBarcodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is BarcodeScannerWindowViewModel viewModel)
        {
            if (viewModel.ProcessManualBarcodeCommand.CanExecute(null))
            {
                viewModel.ProcessManualBarcodeCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not BarcodeScannerWindowViewModel viewModel)
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                viewModel.CloseCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.F2:
                if (viewModel.StartScanningCommand.CanExecute(null))
                {
                    viewModel.StartScanningCommand.Execute(null);
                }
                e.Handled = true;
                break;
                
            case Key.Enter when viewModel.HasValidProduct:
                if (viewModel.AddToSaleCommand.CanExecute(null))
                {
                    viewModel.AddToSaleCommand.Execute(null);
                }
                e.Handled = true;
                break;
                
            default:
                base.OnKeyDown(e);
                break;
        }
    }
}