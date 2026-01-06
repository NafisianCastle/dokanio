using Avalonia.Controls;
using Desktop.ViewModels;

namespace Desktop.Views;

/// <summary>
/// Configuration management view
/// </summary>
public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
    }

    public ConfigurationView(ConfigurationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}