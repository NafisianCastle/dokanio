using Avalonia.Controls;
using Desktop.ViewModels;

namespace Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    public LoginView(LoginViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}