using Mobile.ViewModels;

namespace Mobile.Views;

/// <summary>
/// Configuration management page for mobile
/// </summary>
public partial class ConfigurationPage : ContentPage
{
    public ConfigurationPage()
    {
        InitializeComponent();
    }

    public ConfigurationPage(ConfigurationViewModel viewModel) : this()
    {
        BindingContext = viewModel;
    }
}