using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlainDraft.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PlainDraft;

public partial class LinkWindow : Window
{
    public string? DisplayText { get; private set; }
    public string? Url { get; private set; }

    public LinkWindow(string? initialDisplay = null, Window? owner = null)
    {
        Owner = owner;
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(initialDisplay))
        {
            DisplayBox.Text = initialDisplay;
            DisplayPlaceholder.Visibility = Visibility.Collapsed;
        }

        Loaded += (_, _) =>
        {
            UrlBox.Focus();
            UpdateInsertEnabled();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        WindowChromeUtil.ApplyDialogChrome(this);

    private void OnDragMove(object sender, MouseButtonEventArgs e) =>
        WindowChromeUtil.TryDrag(this, e);

    private void OnDisplayTextChanged(object sender, TextChangedEventArgs e)
    {
        DisplayPlaceholder.Visibility = string.IsNullOrEmpty(DisplayBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnUrlTextChanged(object sender, TextChangedEventArgs e)
    {
        UrlPlaceholder.Visibility = string.IsNullOrEmpty(UrlBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateInsertEnabled();
    }

    private void OnDisplayGotFocus(object sender, RoutedEventArgs e) =>
        WindowChromeUtil.SetDialogFieldFocus(this, DisplayFieldBorder, focused: true);

    private void OnUrlGotFocus(object sender, RoutedEventArgs e) =>
        WindowChromeUtil.SetDialogFieldFocus(this, UrlFieldBorder, focused: true);

    private void OnFieldLostFocus(object sender, RoutedEventArgs e)
    {
        WindowChromeUtil.SetDialogFieldFocus(this, DisplayFieldBorder, DisplayBox.IsKeyboardFocusWithin);
        WindowChromeUtil.SetDialogFieldFocus(this, UrlFieldBorder, UrlBox.IsKeyboardFocusWithin);
    }

    private void OnUrlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && InsertButton.IsEnabled)
        {
            OnInsert(sender, e);
            e.Handled = true;
        }
    }

    private void UpdateInsertEnabled() =>
        InsertButton.IsEnabled = !string.IsNullOrWhiteSpace(UrlBox.Text);

    private void OnInsert(object sender, RoutedEventArgs e)
    {
        if (!LinkTarget.TryNormalize(UrlBox.Text, out var absoluteUri))
        {
            System.Windows.MessageBox.Show(
                this,
                "Введите URL или путь к файлу.",
                "Ссылка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            UrlBox.Focus();
            return;
        }

        Url = absoluteUri;
        DisplayText = string.IsNullOrWhiteSpace(DisplayBox.Text) ? null : DisplayBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
