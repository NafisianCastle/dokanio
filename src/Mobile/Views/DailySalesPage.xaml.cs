using Mobile.ViewModels;

namespace Mobile.Views;

public partial class DailySalesPage : ContentPage
{
    private readonly DailySalesViewModel _viewModel;

    public DailySalesPage(DailySalesViewModel viewModel)
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