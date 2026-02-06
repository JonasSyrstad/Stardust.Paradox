using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stardust.Paradox.GremlinStudio.ViewModels;

/// <summary>
/// Represents a node in the graph visualization.
/// </summary>
public partial class GraphNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _type = "vertex";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _radius = 25;

    [ObservableProperty]
    private Brush _fill = Brushes.DodgerBlue;

    [ObservableProperty]
    private Brush _stroke = Brushes.White;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _displayText = string.Empty;

    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets a short display ID for the node.
    /// </summary>
    public string ShortId => Id.Length > 12 ? Id[..12] + "..." : Id;
}

/// <summary>
/// Represents an edge in the graph visualization.
/// </summary>
public partial class GraphEdgeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _fromId = string.Empty;

    [ObservableProperty]
    private string _toId = string.Empty;


    // Curve offset for parallel edges (positive = curve right, negative = curve left)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathData))]
    [NotifyPropertyChangedFor(nameof(CurvePathData))]
    [NotifyPropertyChangedFor(nameof(ArrowPathData))]
    [NotifyPropertyChangedFor(nameof(LabelX))]
    [NotifyPropertyChangedFor(nameof(LabelY))]
    private double _curveOffset;

    // Calculated line coordinates
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathData))]
    [NotifyPropertyChangedFor(nameof(CurvePathData))]
    [NotifyPropertyChangedFor(nameof(ArrowPathData))]
    [NotifyPropertyChangedFor(nameof(LabelX))]
    [NotifyPropertyChangedFor(nameof(LabelY))]
    private double _x1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathData))]
    [NotifyPropertyChangedFor(nameof(CurvePathData))]
    [NotifyPropertyChangedFor(nameof(ArrowPathData))]
    [NotifyPropertyChangedFor(nameof(LabelX))]
    [NotifyPropertyChangedFor(nameof(LabelY))]
    private double _y1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathData))]
    [NotifyPropertyChangedFor(nameof(CurvePathData))]
    [NotifyPropertyChangedFor(nameof(ArrowPathData))]
    [NotifyPropertyChangedFor(nameof(LabelX))]
    [NotifyPropertyChangedFor(nameof(LabelY))]
    private double _x2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathData))]
    [NotifyPropertyChangedFor(nameof(CurvePathData))]
    [NotifyPropertyChangedFor(nameof(ArrowPathData))]
    [NotifyPropertyChangedFor(nameof(LabelX))]
    [NotifyPropertyChangedFor(nameof(LabelY))]
    private double _y2;

    // Arrow head coordinates (legacy, kept for compatibility)
    [ObservableProperty]
    private string _arrowPoints = string.Empty;


    [ObservableProperty]
    private Brush _stroke = Brushes.Gray;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private double _strokeThickness = 1.5;

    /// <summary>
    /// Gets the control point for the quadratic Bezier curve.
    /// </summary>
    private (double cx, double cy) GetControlPoint()
    {
        double midX = (X1 + X2) / 2;
        double midY = (Y1 + Y2) / 2;
        
        if (Math.Abs(CurveOffset) < 0.1)
            return (midX, midY);

        // Calculate perpendicular offset
        double dx = X2 - X1;
        double dy = Y2 - Y1;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return (midX, midY);

        // Perpendicular direction (normalized)
        double perpX = -dy / dist;
        double perpY = dx / dist;

        return (midX + perpX * CurveOffset, midY + perpY * CurveOffset);
    }

    /// <summary>
    /// Gets the X coordinate for the label (at curve midpoint).
    /// </summary>
    public double LabelX
    {
        get
        {
            var (cx, cy) = GetControlPoint();
            // For quadratic Bezier, the point at t=0.5 is: 0.25*P0 + 0.5*Control + 0.25*P1
            return 0.25 * X1 + 0.5 * cx + 0.25 * X2 - 20;
        }
    }

    /// <summary>
    /// Gets the Y coordinate for the label (at curve midpoint).
    /// </summary>
    public double LabelY
    {
        get
        {
            var (cx, cy) = GetControlPoint();
            return 0.25 * Y1 + 0.5 * cy + 0.25 * Y2 - 12;
        }
    }

    /// <summary>
    /// Gets the path geometry data for just the curved edge line (no arrow).
    /// </summary>
    public string CurvePathData
    {
        get
        {
            if (X1 == 0 && Y1 == 0 && X2 == 0 && Y2 == 0)
                return string.Empty;

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var (cx, cy) = GetControlPoint();

            // Quadratic Bezier curve from start to end
            return $"M {X1.ToString(ic)},{Y1.ToString(ic)} Q {cx.ToString(ic)},{cy.ToString(ic)} {X2.ToString(ic)},{Y2.ToString(ic)}";
        }
    }

    /// <summary>
    /// Gets the path geometry data for just the arrow head.
    /// </summary>
    public string ArrowPathData
    {
        get
        {
            if (X1 == 0 && Y1 == 0 && X2 == 0 && Y2 == 0)
                return string.Empty;

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var (cx, cy) = GetControlPoint();

            // Calculate arrow direction at the end of the curve
            double tangentX = X2 - cx;
            double tangentY = Y2 - cy;
            double tangentLen = Math.Sqrt(tangentX * tangentX + tangentY * tangentY);
            if (tangentLen < 1) return string.Empty;

            double angle = Math.Atan2(tangentY, tangentX);
            double arrowSize = 8;

            double ax1 = X2 - arrowSize * Math.Cos(angle - Math.PI / 7);
            double ay1 = Y2 - arrowSize * Math.Sin(angle - Math.PI / 7);
            double ax2 = X2 - arrowSize * Math.Cos(angle + Math.PI / 7);
            double ay2 = Y2 - arrowSize * Math.Sin(angle + Math.PI / 7);

            return $"M {X2.ToString(ic)},{Y2.ToString(ic)} L {ax1.ToString(ic)},{ay1.ToString(ic)} L {ax2.ToString(ic)},{ay2.ToString(ic)} Z";
        }
    }

    /// <summary>
    /// Gets the path geometry data for the curved edge with arrow (combined).
    /// </summary>
    public string PathData
    {
        get
        {
            if (X1 == 0 && Y1 == 0 && X2 == 0 && Y2 == 0)
                return string.Empty;

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var (cx, cy) = GetControlPoint();

            // Quadratic Bezier curve from start to end
            string curve = $"M {X1.ToString(ic)},{Y1.ToString(ic)} Q {cx.ToString(ic)},{cy.ToString(ic)} {X2.ToString(ic)},{Y2.ToString(ic)}";

            // Calculate arrow direction at the end of the curve
            // For quadratic Bezier, tangent at t=1 is: P1 - Control
            double tangentX = X2 - cx;
            double tangentY = Y2 - cy;
            double tangentLen = Math.Sqrt(tangentX * tangentX + tangentY * tangentY);
            if (tangentLen < 1) return curve;

            double angle = Math.Atan2(tangentY, tangentX);
            double arrowSize = 10;

            double ax1 = X2 - arrowSize * Math.Cos(angle - Math.PI / 7);
            double ay1 = Y2 - arrowSize * Math.Sin(angle - Math.PI / 7);
            double ax2 = X2 - arrowSize * Math.Cos(angle + Math.PI / 7);
            double ay2 = Y2 - arrowSize * Math.Sin(angle + Math.PI / 7);

            string arrow = $" M {X2.ToString(ic)},{Y2.ToString(ic)} L {ax1.ToString(ic)},{ay1.ToString(ic)} L {ax2.ToString(ic)},{ay2.ToString(ic)} Z";

            return curve + arrow;
        }
    }

    /// <summary>
    /// Gets the source vertex ID for display.
    /// </summary>
    public string SourceId => FromId.Length > 15 ? FromId[..15] + "..." : FromId;

    /// <summary>
    /// Gets the target vertex ID for display.
    /// </summary>
    public string TargetId => ToId.Length > 15 ? ToId[..15] + "..." : ToId;

    public Dictionary<string, object> Properties { get; set; } = new();
}
