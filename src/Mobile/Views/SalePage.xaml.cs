using Mobile.ViewModels;

namespace Mobile.Views;

public partial class SalePage : ContentPage
{
    private readonly SaleViewModel _viewModel;

    public SalePage(SaleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.Initialize();
    }
}