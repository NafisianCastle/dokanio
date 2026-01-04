using Avalonia.Controls;
using Desktop.ViewModels;

namespace Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}