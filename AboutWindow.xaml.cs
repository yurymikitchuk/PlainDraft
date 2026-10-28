using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using PlainDraft.Services;

namespace PlainDraft;

public partial class AboutWindow : Window
{
    private const string ReleasesUrl =
        "https://github.com/yurymikitchuk/PlainDraft/releases/latest";

    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.6";
        VersionText.Text = $"Номер версии: {version}";
        UpdateLink.NavigateUri = new Uri(ReleasesUrl);
        ApplyLogoMask();
    }

    private void ApplyLogoMask()
    {
        if (!File.Exists(AppPaths.Logo256)) return;
        LogoMark.OpacityMask = new ImageBrush(new BitmapImage(new Uri(AppPaths.Logo256)))
        {
            Stretch = Stretch.Uniform
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        WindowChromeUtil.ApplyDialogChrome(this);

    private void OnDragMove(object sender, MouseButtonEventArgs e) =>
        WindowChromeUtil.TryDrag(this, e);

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        UriLauncher.Open(e.Uri);
        e.Handled = true;
    }

    private void OnOk(object sender, RoutedEventArgs e) => Close();
}
