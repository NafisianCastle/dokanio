using Mobile.ViewModels;

namespace Mobile.Views;

public partial class BarcodeScannerPage : ContentPage
{
    private readonly BarcodeScannerViewModel _viewModel;

    public BarcodeScannerPage(BarcodeScannerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Initialize();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_viewModel.IsDetecting)
        {
            await _viewModel.ToggleDetectionCommand.ExecuteAsync(null);
        }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddToSaleClicked(object sender, EventArgs e)
    {
        if (_viewModel.AddToSaleCommand.CanExecute(null))
        {
            await _viewModel.AddToSaleCommand.ExecuteAsync(null);
        }
    }
}