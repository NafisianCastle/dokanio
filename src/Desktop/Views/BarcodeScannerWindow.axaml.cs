using Avalonia.Controls;
using Avalonia.Input;
using Desktop.ViewModels;
using System.Reactive;

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
        viewModel.CloseRequested += (_, e) => Close(e);
    }

    private void OnManualBarcodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is BarcodeScannerWindowViewModel vm)
        {
            vm.ProcessManualBarcodeCommand.Execute(Unit.Default);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not BarcodeScannerWindowViewModel vm)
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                vm.CloseCommand.Execute(Unit.Default);
                e.Handled = true;
                break;

            case Key.F2:
                vm.StartScanningCommand.Execute(Unit.Default);
                e.Handled = true;
                break;

            case Key.Enter when vm.HasValidProduct:
                vm.AddToSaleCommand.Execute(Unit.Default);
                e.Handled = true;
                break;

            default:
                base.OnKeyDown(e);
                break;
        }
    }
}
