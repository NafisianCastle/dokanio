namespace Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Register routes for navigation
        Routing.RegisterRoute("login", typeof(Views.LoginPage));
        Routing.RegisterRoute("scanner", typeof(Views.BarcodeScannerPage));
        Routing.RegisterRoute("businessselection", typeof(Views.LoginPage)); // Reuse login page for business selection
    }
}