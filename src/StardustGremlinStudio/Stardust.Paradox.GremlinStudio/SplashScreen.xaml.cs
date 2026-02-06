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
