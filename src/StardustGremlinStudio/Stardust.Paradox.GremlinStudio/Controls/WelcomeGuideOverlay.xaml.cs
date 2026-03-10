using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;
using Stardust.Paradox.GremlinStudio.ViewModels;

namespace Stardust.Paradox.GremlinStudio.Controls;

/// <summary>
/// Overlay control that displays a positioned chat bubble pointing to a target UI element.
/// </summary>
public partial class WelcomeGuideOverlay : UserControl
{
    private const double ArrowSize = 10;

    public WelcomeGuideOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WelcomeGuideViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WelcomeGuideViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WelcomeGuideViewModel.BubbleLeft)
            or nameof(WelcomeGuideViewModel.BubbleTop)
            or nameof(WelcomeGuideViewModel.ArrowSide)
            or nameof(WelcomeGuideViewModel.TargetCenterX)
            or nameof(WelcomeGuideViewModel.TargetCenterY))
        {
            Dispatcher.InvokeAsync(UpdateArrowPosition, System.Windows.Threading.DispatcherPriority.Render);
        }

        if (e.PropertyName is nameof(WelcomeGuideViewModel.IsLastStep))
        {
            UpdateNextButtonContent();
        }
    }

    /// <summary>
    /// Switches the Next button between "Next ?" and "? Done" based on IsLastStep.
    /// </summary>
    private void UpdateNextButtonContent()
    {
        if (DataContext is not WelcomeGuideViewModel vm) return;

        if (vm.IsLastStep)
        {
            NextIcon.Text = "\uE73E";
            NextIcon.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
            NextIcon.FontSize = 10;
            NextIcon.Margin = new Thickness(0, 0, 4, 0);
            NextArrow.Text = "Done";
            NextArrow.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            NextArrow.FontSize = 11;
        }
        else
        {
            NextIcon.Text = "Next";
            NextIcon.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            NextIcon.FontSize = 11;
            NextIcon.Margin = new Thickness(0, 0, 4, 0);
            NextArrow.Text = "\uE76C";
            NextArrow.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
            NextArrow.FontSize = 10;
        }
    }

    /// <summary>
    /// Positions the arrow polygon to point from the bubble edge toward the actual target element.
    /// Uses TargetCenterX/Y to aim the arrow correctly even when the bubble is clamped.
    /// </summary>
    internal void UpdateArrowPosition()
    {
        if (DataContext is not WelcomeGuideViewModel vm) return;
        if (BubbleBorder.ActualWidth == 0) return;

        var bubbleLeft = vm.BubbleLeft;
        var bubbleTop = vm.BubbleTop;
        var bubbleWidth = BubbleBorder.ActualWidth;
        var bubbleHeight = BubbleBorder.ActualHeight;
        var targetX = vm.TargetCenterX;
        var targetY = vm.TargetCenterY;

        // Clamp arrow offset so it stays within the bubble edge (with margins)
        const double edgeMargin = 24;

        switch (vm.ArrowSide)
        {
            case BubbleArrowSide.Top:
            {
                // Arrow on top edge pointing up toward target
                var arrowX = Math.Clamp(targetX, bubbleLeft + edgeMargin, bubbleLeft + bubbleWidth - edgeMargin);
                BubbleArrow.Points = new PointCollection
                {
                    new Point(0, ArrowSize),
                    new Point(ArrowSize, 0),
                    new Point(ArrowSize * 2, ArrowSize)
                };
                Canvas.SetLeft(BubbleArrow, arrowX - ArrowSize);
                Canvas.SetTop(BubbleArrow, bubbleTop - ArrowSize);
                break;
            }

            case BubbleArrowSide.Bottom:
            {
                // Arrow on bottom edge pointing down toward target
                var arrowX = Math.Clamp(targetX, bubbleLeft + edgeMargin, bubbleLeft + bubbleWidth - edgeMargin);
                BubbleArrow.Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(ArrowSize * 2, 0),
                    new Point(ArrowSize, ArrowSize)
                };
                Canvas.SetLeft(BubbleArrow, arrowX - ArrowSize);
                Canvas.SetTop(BubbleArrow, bubbleTop + bubbleHeight);
                break;
            }

            case BubbleArrowSide.Left:
            {
                // Arrow on left edge pointing left toward target
                var arrowY = Math.Clamp(targetY, bubbleTop + edgeMargin, bubbleTop + bubbleHeight - edgeMargin);
                BubbleArrow.Points = new PointCollection
                {
                    new Point(ArrowSize, 0),
                    new Point(0, ArrowSize),
                    new Point(ArrowSize, ArrowSize * 2)
                };
                Canvas.SetLeft(BubbleArrow, bubbleLeft - ArrowSize);
                Canvas.SetTop(BubbleArrow, arrowY - ArrowSize);
                break;
            }

            case BubbleArrowSide.Right:
            {
                // Arrow on right edge pointing right toward target
                var arrowY = Math.Clamp(targetY, bubbleTop + edgeMargin, bubbleTop + bubbleHeight - edgeMargin);
                BubbleArrow.Points = new PointCollection
                {
                    new Point(0, 0),
                    new Point(ArrowSize, ArrowSize),
                    new Point(0, ArrowSize * 2)
                };
                Canvas.SetLeft(BubbleArrow, bubbleLeft + bubbleWidth);
                Canvas.SetTop(BubbleArrow, arrowY - ArrowSize);
                break;
            }
        }
    }

    /// <summary>
    /// Clicking the backdrop does not dismiss - prevents accidental closure.
    /// </summary>
    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
