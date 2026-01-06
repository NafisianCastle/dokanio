using Avalonia.Controls;
using Desktop.ViewModels;

namespace Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    public LoginWindow(LoginViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}