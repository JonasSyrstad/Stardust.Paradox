using System.Windows;
using System.Windows.Media.Imaging;

namespace Stardust.Paradox.GremlinStudio;

/// <summary>
/// Splash screen window shown during application startup.
/// </summary>
public partial class SplashScreen : Window
{
    private static readonly string[] SplashImages =
    [
        "/Resources/splash.png",
        "/Resources/splash2.png",
        "/Resources/splash3.png",
        "/Resources/splash4.png",
        "/Resources/splash5.png",
        "/Resources/splash6.png",
        "/Resources/splash7.png",
        "/Resources/splash8.png"
    ];

    public SplashScreen()
    {
        InitializeComponent();
        SetRandomSplashImage();
        SetVersionInfo();
    }

    /// <summary>
    /// Shows the app version and when the installed executable was last updated.
    /// </summary>
    private void SetVersionInfo()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3);

        var text = $"Version {version}";
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            text += $"  \u2022  Updated {System.IO.File.GetLastWriteTime(exePath):d}";
        VersionText.Text = text;
    }

    /// <summary>
    /// Sets a random splash image from the available splash images.
    /// </summary>
    private void SetRandomSplashImage()
    {
        var random = new Random();
        var selectedImage = SplashImages[random.Next(SplashImages.Length)];
        SplashImage.Source = new BitmapImage(new Uri(selectedImage, UriKind.Relative));
    }

    /// <summary>
    /// Updates the loading status text.
    /// </summary>
    /// <param name="status">The status message to display.</param>
    public void UpdateStatus(string status)
    {
        LoadingText.Text = status;
    }
}
