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
        
        // Subscribe to haptic feedback events
        _viewModel.HapticFeedbackRequested += OnHapticFeedbackRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe from events to prevent memory leaks
        _viewModel.HapticFeedbackRequested -= OnHapticFeedbackRequested;
    }

    private void OnHapticFeedbackRequested(object? sender, HapticFeedbackEventArgs e)
    {
        try
        {
            // Provide haptic feedback based on the action type
            switch (e.FeedbackType)
            {
                case Microsoft.Maui.Devices.HapticFeedbackType.Click:
#if ANDROID || IOS
                    HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click);
#endif
                    break;
                case Microsoft.Maui.Devices.HapticFeedbackType.LongPress:
#if ANDROID || IOS
                    HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
#endif
                    break;
            }
        }
        catch (Exception ex)
        {
            // Haptic feedback is not critical, so we just log the error
            System.Diagnostics.Debug.WriteLine($"Haptic feedback error: {ex.Message}");
        }
    }

    // Handle device back button for better UX
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.SaleItems?.Any() == true)
        {
            // Show confirmation dialog if there are items in the cart
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var result = await DisplayAlert(
                    "Unsaved Sale", 
                    "You have items in your cart. Are you sure you want to go back?", 
                    "Yes", 
                    "No");
                
                if (result)
                {
                    await Shell.Current.GoToAsync("..");
                }
            });
            return true; // Prevent default back behavior
        }
        
        return base.OnBackButtonPressed();
    }
}