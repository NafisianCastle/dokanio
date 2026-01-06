using Microsoft.Extensions.DependencyInjection;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Services;
using System.Timers;

namespace Mobile.Controls;

public partial class ValidationEntry : ContentView
{
    private readonly IValidationService? _validationService;
    private readonly System.Timers.Timer _validationTimer;
    private string _currentValue = string.Empty;

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(ValidationEntry), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(ValidationEntry), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ValidationEntry), string.Empty);

    public static readonly BindableProperty FieldNameProperty =
        BindableProperty.Create(nameof(FieldName), typeof(string), typeof(ValidationEntry), string.Empty);

    public static readonly BindableProperty IsRequiredProperty =
        BindableProperty.Create(nameof(IsRequired), typeof(bool), typeof(ValidationEntry), false);

    public static readonly BindableProperty MinLengthProperty =
        BindableProperty.Create(nameof(MinLength), typeof(int?), typeof(ValidationEntry), null);

    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(nameof(MaxLength), typeof(int?), typeof(ValidationEntry), null);

    public static readonly BindableProperty MinValueProperty =
        BindableProperty.Create(nameof(MinValue), typeof(decimal?), typeof(ValidationEntry), null);

    public static readonly BindableProperty MaxValueProperty =
        BindableProperty.Create(nameof(MaxValue), typeof(decimal?), typeof(ValidationEntry), null);

    public static readonly BindableProperty RegexPatternProperty =
        BindableProperty.Create(nameof(RegexPattern), typeof(string), typeof(ValidationEntry), string.Empty);

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(ValidationEntry), Keyboard.Default);

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(ValidationEntry), false);

    public static readonly BindableProperty ValidationMessageProperty =
        BindableProperty.Create(nameof(ValidationMessage), typeof(string), typeof(ValidationEntry), string.Empty);

    public static readonly BindableProperty ValidationMessageColorProperty =
        BindableProperty.Create(nameof(ValidationMessageColor), typeof(Color), typeof(ValidationEntry), Colors.Red);

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(ValidationEntry), Colors.Gray);

    public static readonly BindableProperty ShowValidationIconProperty =
        BindableProperty.Create(nameof(ShowValidationIcon), typeof(bool), typeof(ValidationEntry), false);

    public static readonly BindableProperty ValidationIconProperty =
        BindableProperty.Create(nameof(ValidationIcon), typeof(string), typeof(ValidationEntry), string.Empty);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string FieldName
    {
        get => (string)GetValue(FieldNameProperty);
        set => SetValue(FieldNameProperty, value);
    }

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    public int? MinLength
    {
        get => (int?)GetValue(MinLengthProperty);
        set => SetValue(MinLengthProperty, value);
    }

    public int? MaxLength
    {
        get => (int?)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public decimal? MinValue
    {
        get => (decimal?)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public decimal? MaxValue
    {
        get => (decimal?)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string RegexPattern
    {
        get => (string)GetValue(RegexPatternProperty);
        set => SetValue(RegexPatternProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public string ValidationMessage
    {
        get => (string)GetValue(ValidationMessageProperty);
        set => SetValue(ValidationMessageProperty, value);
    }

    public Color ValidationMessageColor
    {
        get => (Color)GetValue(ValidationMessageColorProperty);
        set => SetValue(ValidationMessageColorProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public bool ShowValidationIcon
    {
        get => (bool)GetValue(ShowValidationIconProperty);
        set => SetValue(ShowValidationIconProperty, value);
    }

    public string ValidationIcon
    {
        get => (string)GetValue(ValidationIconProperty);
        set => SetValue(ValidationIconProperty, value);
    }

    public ValidationEntry()
    {
        InitializeComponent();
        
        // Try to get validation service from DI container
        if (Application.Current?.Handler?.MauiContext?.Services != null)
        {
            _validationService = Application.Current.Handler.MauiContext.Services.GetService<IValidationService>();
        }

        // Setup validation timer for real-time validation
        _validationTimer = new System.Timers.Timer(500); // 500ms delay
        _validationTimer.Elapsed += OnValidationTimerElapsed;
        _validationTimer.AutoReset = false;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue != _currentValue)
        {
            _currentValue = e.NewTextValue ?? string.Empty;
            
            // Reset timer for real-time validation
            _validationTimer.Stop();
            _validationTimer.Start();
        }
    }

    private async void OnValidationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_validationService != null && !string.IsNullOrEmpty(FieldName))
        {
            await ValidateFieldAsync();
        }
    }

    private async Task ValidateFieldAsync()
    {
        try
        {
            var validationRules = new FieldValidationRules
            {
                IsRequired = IsRequired,
                MinLength = MinLength,
                MaxLength = MaxLength,
                MinValue = MinValue,
                MaxValue = MaxValue,
                RegexPattern = !string.IsNullOrEmpty(RegexPattern) ? RegexPattern : null
            };

            var context = new ValidationContext
            {
                EntityType = "UI",
                ContextData = new Dictionary<string, object>
                {
                    { "ControlType", "Entry" },
                    { "Label", Label ?? string.Empty }
                }
            };

            var result = await _validationService!.ValidateRealTimeAsync(FieldName, Value, context);

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateValidationUI(result);
            });
        }
        catch (Exception ex)
        {
            // Log error and show generic validation message
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ValidationMessage = "Validation error occurred";
                ValidationMessageColor = Colors.Red;
                BorderColor = Colors.Red;
                ShowValidationIcon = true;
                ValidationIcon = "error_icon.png";
            });
        }
    }

    private void UpdateValidationUI(RealTimeValidationResult result)
    {
        if (result.IsValid)
        {
            // Valid state
            ValidationMessage = result.InstantFeedback ?? string.Empty;
            ValidationMessageColor = Colors.Green;
            BorderColor = Colors.Green;
            ShowValidationIcon = true;
            ValidationIcon = "check_icon.png";
        }
        else
        {
            // Invalid state
            ValidationMessage = result.InstantFeedback ?? "Invalid input";
            
            switch (result.Severity)
            {
                case ValidationSeverity.Error:
                    ValidationMessageColor = Colors.Red;
                    BorderColor = Colors.Red;
                    ValidationIcon = "error_icon.png";
                    break;
                case ValidationSeverity.Warning:
                    ValidationMessageColor = Colors.Orange;
                    BorderColor = Colors.Orange;
                    ValidationIcon = "warning_icon.png";
                    break;
                default:
                    ValidationMessageColor = Colors.Gray;
                    BorderColor = Colors.Gray;
                    ValidationIcon = "info_icon.png";
                    break;
            }
            
            ShowValidationIcon = true;
        }

        // Show suggestion if available
        if (!string.IsNullOrEmpty(result.SuggestedCorrection))
        {
            ValidationMessage += $" {result.SuggestedCorrection}";
        }

        // Provide haptic feedback for mobile
        if (!result.IsValid && result.Severity == ValidationSeverity.Error)
        {
#if ANDROID || IOS
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            }
            catch
            {
                // Haptic feedback not available
            }
#endif
        }
    }

    public async Task<bool> ValidateAsync()
    {
        if (_validationService != null && !string.IsNullOrEmpty(FieldName))
        {
            await ValidateFieldAsync();
            return string.IsNullOrEmpty(ValidationMessage) || ValidationMessageColor == Colors.Green;
        }
        return true;
    }
}