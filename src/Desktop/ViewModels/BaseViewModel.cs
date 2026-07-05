using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Desktop.ViewModels;

public class BaseViewModel : ReactiveObject
{
    [Reactive] public bool IsBusy { get; set; }
    [Reactive] public string Title { get; set; } = string.Empty;
    [Reactive] public string ErrorMessage { get; set; } = string.Empty;
    [Reactive] public bool HasError { get; set; }
    [Reactive] public string? SuccessMessage { get; set; }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = !string.IsNullOrEmpty(message);
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
