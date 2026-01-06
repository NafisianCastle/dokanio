using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Shared.Core.DTOs;
using Shared.Core.Enums;
using Shared.Core.Services;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace Desktop.Controls;

public partial class ValidationTextBox : UserControl
{
    private readonly IValidationService? _validationService;
    private readonly Timer _validationTimer;
    private string _currentValue = string.Empty;

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(Placeholder), string.Empty);

    public static readonly StyledProperty<string> FieldNameProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(FieldName), string.Empty);

    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<ValidationTextBox, bool>(nameof(IsRequired), false);

    public static readonly StyledProperty<int?> MinLengthProperty =
        AvaloniaProperty.Register<ValidationTextBox, int?>(nameof(MinLength));

    public static readonly StyledProperty<int?> MaxLengthProperty =
        AvaloniaProperty.Register<ValidationTextBox, int?>(nameof(MaxLength));

    public static readonly StyledProperty<decimal?> MinValueProperty =
        AvaloniaProperty.Register<ValidationTextBox, decimal?>(nameof(MinValue));

    public static readonly StyledProperty<decimal?> MaxValueProperty =
        AvaloniaProperty.Register<ValidationTextBox, decimal?>(nameof(MaxValue));

    public static readonly StyledProperty<string> RegexPatternProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(RegexPattern), string.Empty);

    public static readonly StyledProperty<string> ValidationMessageProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(ValidationMessage), string.Empty);

    public static readonly StyledProperty<IBrush> ValidationMessageColorProperty =
        AvaloniaProperty.Register<ValidationTextBox, IBrush>(nameof(ValidationMessageColor), Brushes.Red);

    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        AvaloniaProperty.Register<ValidationTextBox, IBrush>(nameof(BorderBrush), Brushes.Gray);

    public static readonly StyledProperty<bool> ShowValidationIconProperty =
        AvaloniaProperty.Register<ValidationTextBox, bool>(nameof(ShowValidationIcon), false);

    public static readonly StyledProperty<IBrush> ValidationIconColorProperty =
        AvaloniaProperty.Register<ValidationTextBox, IBrush>(nameof(ValidationIconColor), Brushes.Green);

    public static readonly StyledProperty<string> ValidationIconPathProperty =
        AvaloniaProperty.Register<ValidationTextBox, string>(nameof(ValidationIconPath), string.Empty);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string FieldName
    {
        get => GetValue(FieldNameProperty);
        set => SetValue(FieldNameProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    public int? MinLength
    {
        get => GetValue(MinLengthProperty);
        set => SetValue(MinLengthProperty, value);
    }

    public int? MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public decimal? MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public decimal? MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string RegexPattern
    {
        get => GetValue(RegexPatternProperty);
        set => SetValue(RegexPatternProperty, value);
    }

    public string ValidationMessage
    {
        get => GetValue(ValidationMessageProperty);
        set => SetValue(ValidationMessageProperty, value);
    }

    public IBrush ValidationMessageColor
    {
        get => GetValue(ValidationMessageColorProperty);
        set => SetValue(ValidationMessageColorProperty, value);
    }

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public bool ShowValidationIcon
    {
        get => GetValue(ShowValidationIconProperty);
        set => SetValue(ShowValidationIconProperty, value);
    }

    public IBrush ValidationIconColor
    {
        get => GetValue(ValidationIconColorProperty);
        set => SetValue(ValidationIconColorProperty, value);
    }

    public string ValidationIconPath
    {
        get => GetValue(ValidationIconPathProperty);
        set => SetValue(ValidationIconPathProperty, value);
    }

    public ValidationTextBox()
    {
        InitializeComponent();
        
        // Try to get validation service from DI container
        if (App.Current?.Services != null)
        {
            _validationService = App.Current.Services.GetService(typeof(IValidationService)) as IValidationService;
        }

        // Setup validation timer for real-time validation
        _validationTimer = new Timer(500); // 500ms delay
        _validationTimer.Elapsed += OnValidationTimerElapsed;
        _validationTimer.AutoReset = false;

        // Subscribe to value changes
        ValueProperty.Changed.Subscribe(OnValueChanged);
    }

    private void OnValueChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is string newValue && newValue != _currentValue)
        {
            _currentValue = newValue;
            
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
                    { "ControlType", "TextBox" },
                    { "Label", Label ?? string.Empty }
                }
            };

            var result = await _validationService!.ValidateRealTimeAsync(FieldName, Value, context);

            // Update UI on main thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateValidationUI(result);
            });
        }
        catch (Exception ex)
        {
            // Log error and show generic validation message
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ValidationMessage = "Validation error occurred";
                ValidationMessageColor = Brushes.Red;
                BorderBrush = Brushes.Red;
                ShowValidationIcon = true;
                ValidationIconColor = Brushes.Red;
                ValidationIconPath = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,7A1.5,1.5 0 0,1 13.5,8.5A1.5,1.5 0 0,1 12,10A1.5,1.5 0 0,1 10.5,8.5A1.5,1.5 0 0,1 12,7M10.5,12H13.5V17H10.5V12Z"; // Error icon
            });
        }
    }

    private void UpdateValidationUI(RealTimeValidationResult result)
    {
        if (result.IsValid)
        {
            // Valid state
            ValidationMessage = result.InstantFeedback ?? string.Empty;
            ValidationMessageColor = Brushes.Green;
            BorderBrush = Brushes.Green;
            ShowValidationIcon = true;
            ValidationIconColor = Brushes.Green;
            ValidationIconPath = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,16.5L6.5,12L7.91,10.59L11,13.67L16.59,8.09L18,9.5L11,16.5Z"; // Check icon
        }
        else
        {
            // Invalid state
            ValidationMessage = result.InstantFeedback ?? "Invalid input";
            
            switch (result.Severity)
            {
                case ValidationSeverity.Error:
                    ValidationMessageColor = Brushes.Red;
                    BorderBrush = Brushes.Red;
                    ValidationIconColor = Brushes.Red;
                    ValidationIconPath = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,7A1.5,1.5 0 0,1 13.5,8.5A1.5,1.5 0 0,1 12,10A1.5,1.5 0 0,1 10.5,8.5A1.5,1.5 0 0,1 12,7M10.5,12H13.5V17H10.5V12Z"; // Error icon
                    break;
                case ValidationSeverity.Warning:
                    ValidationMessageColor = Brushes.Orange;
                    BorderBrush = Brushes.Orange;
                    ValidationIconColor = Brushes.Orange;
                    ValidationIconPath = "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z"; // Warning icon
                    break;
                default:
                    ValidationMessageColor = Brushes.Gray;
                    BorderBrush = Brushes.Gray;
                    ValidationIconColor = Brushes.Gray;
                    ValidationIconPath = "M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"; // Info icon
                    break;
            }
            
            ShowValidationIcon = true;
        }

        // Show suggestion if available
        if (!string.IsNullOrEmpty(result.SuggestedCorrection))
        {
            ValidationMessage += $" {result.SuggestedCorrection}";
        }
    }

    public async Task<bool> ValidateAsync()
    {
        if (_validationService != null && !string.IsNullOrEmpty(FieldName))
        {
            await ValidateFieldAsync();
            return string.IsNullOrEmpty(ValidationMessage) || ValidationMessageColor == Brushes.Green;
        }
        return true;
    }
}