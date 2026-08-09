using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WinForms = System.Windows.Forms;

namespace ClipboardManager;

internal static class UiLanguage
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    private static readonly ConditionalWeakTable<DependencyObject, ElementState> States = new();
    private static readonly DependencyProperty[] FrameworkElementProperties =
    [
        Window.TitleProperty,
        TextBlock.TextProperty,
        ContentControl.ContentProperty,
        HeaderedContentControl.HeaderProperty,
        HeaderedItemsControl.HeaderProperty,
        ToolTipService.ToolTipProperty
    ];
    private static readonly DependencyProperty[] FrameworkContentElementProperties =
    [
        Run.TextProperty,
        ToolTipService.ToolTipProperty
    ];

    private static bool _registered;
    private static string _current = Chinese;

    public static string Current => _current;
    public static bool IsEnglish => string.Equals(_current, English, StringComparison.OrdinalIgnoreCase);
    public static event Action? Changed;

    public static string Normalize(string? language) =>
        string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;

    public static void Initialize(string? language)
    {
        if (!_registered)
        {
            _registered = true;
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded));
            EventManager.RegisterClassHandler(
                typeof(FrameworkContentElement),
                FrameworkContentElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded));
        }

        Set(language);
    }

    public static void Set(string? language)
    {
        _current = Normalize(language);
        if (System.Windows.Application.Current == null) return;

        foreach (Window window in System.Windows.Application.Current.Windows)
            ApplyTree(window);
        Changed?.Invoke();
    }

    public static string T(string? source) => EnglishTranslations.Translate(source, IsEnglish);

    public static void ApplyTo(WinForms.ContextMenuStrip? menu)
    {
        if (menu == null) return;
        foreach (WinForms.ToolStripItem item in menu.Items)
            ApplyTo(item);
    }

    private static void ApplyTo(WinForms.ToolStripItem item)
    {
        if (item.Tag is not TrayTextState state)
        {
            state = new TrayTextState(item.Text ?? string.Empty, item.ToolTipText ?? string.Empty);
            item.Tag = state;
        }

        item.Text = IsEnglish ? T(state.Text) : state.Text;
        item.ToolTipText = IsEnglish ? T(state.ToolTipText) : state.ToolTipText;
        if (item is WinForms.ToolStripDropDownItem dropdown)
        {
            foreach (WinForms.ToolStripItem child in dropdown.DropDownItems)
                ApplyTo(child);
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject element)
            AttachAndApply(element);
    }

    private static void ApplyTree(DependencyObject root)
    {
        AttachAndApply(root);
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dependencyObject)
                ApplyTree(dependencyObject);
        }
    }

    private static void AttachAndApply(DependencyObject element)
    {
        var properties = element is FrameworkContentElement
            ? FrameworkContentElementProperties
            : FrameworkElementProperties;
        var state = States.GetOrCreateValue(element);

        foreach (var property in properties)
        {
            if (!IsPropertyValidForElement(element, property)) continue;
            if (state.Properties.TryGetValue(property, out var propertyState))
            {
                ApplyProperty(element, property, propertyState);
                continue;
            }

            var value = element.GetValue(property) as string;
            if (string.IsNullOrEmpty(value)) continue;

            propertyState = new PropertyState(value);
            state.Properties[property] = propertyState;
            var descriptor = DependencyPropertyDescriptor.FromProperty(property, element.GetType());
            if (descriptor != null)
            {
                EventHandler handler = (_, _) => OnPropertyChanged(element, property, propertyState);
                propertyState.Handler = handler;
                descriptor.AddValueChanged(element, handler);
            }

            ApplyProperty(element, property, propertyState);
        }
    }

    private static bool IsPropertyValidForElement(DependencyObject element, DependencyProperty property)
    {
        if (property == Window.TitleProperty) return element is Window;
        if (property == TextBlock.TextProperty) return element is TextBlock;
        if (property == Run.TextProperty) return element is Run;
        if (property == ContentControl.ContentProperty) return element is ContentControl;
        if (property == HeaderedContentControl.HeaderProperty) return element is HeaderedContentControl;
        if (property == HeaderedItemsControl.HeaderProperty) return element is HeaderedItemsControl;
        return property == ToolTipService.ToolTipProperty && element is FrameworkElement or FrameworkContentElement;
    }

    private static void OnPropertyChanged(
        DependencyObject element,
        DependencyProperty property,
        PropertyState state)
    {
        if (state.Applying) return;
        var current = element.GetValue(property) as string;
        if (string.IsNullOrEmpty(current)) return;

        if (EnglishTranslations.ContainsHan(current))
            state.ChineseSource = current;
        ApplyProperty(element, property, state);
    }

    private static void ApplyProperty(
        DependencyObject element,
        DependencyProperty property,
        PropertyState state)
    {
        var desired = IsEnglish ? T(state.ChineseSource) : state.ChineseSource;
        if (Equals(element.GetValue(property), desired)) return;

        state.Applying = true;
        try
        {
            element.SetCurrentValue(property, desired);
        }
        finally
        {
            state.Applying = false;
        }
    }

    private sealed class ElementState
    {
        public Dictionary<DependencyProperty, PropertyState> Properties { get; } = new();
    }

    private sealed class PropertyState(string source)
    {
        public string ChineseSource { get; set; } = source;
        public EventHandler? Handler { get; set; }
        public bool Applying { get; set; }
    }

    private sealed record TrayTextState(string Text, string ToolTipText);
}

internal static class LocalizedMessageBox
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon) =>
        System.Windows.MessageBox.Show(UiLanguage.T(messageBoxText), UiLanguage.T(caption), button, icon);

    public static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon) =>
        System.Windows.MessageBox.Show(owner, UiLanguage.T(messageBoxText), UiLanguage.T(caption), button, icon);
}
