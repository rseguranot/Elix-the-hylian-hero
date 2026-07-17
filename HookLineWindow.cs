using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HylianHeroCompanion;

public sealed class HookLineWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Canvas _canvas = new() { Background = Brushes.Transparent, IsHitTestVisible = false };
    private readonly Line _chainOutline = Segment(Color.FromRgb(36, 29, 21), 8);
    private readonly Line _chain = Segment(Color.FromRgb(207, 164, 77), 4, dashed: true);
    private readonly Line _prongOneOutline = Segment(Color.FromRgb(36, 29, 21), 8);
    private readonly Line _prongTwoOutline = Segment(Color.FromRgb(36, 29, 21), 8);
    private readonly Line _prongOne = Segment(Color.FromRgb(222, 184, 94), 4);
    private readonly Line _prongTwo = Segment(Color.FromRgb(222, 184, 94), 4);
    private readonly Ellipse _tipOutline = Tip(Color.FromRgb(36, 29, 21), 15);
    private readonly Ellipse _tip = Tip(Color.FromRgb(232, 197, 112), 8);

    public HookLineWindow()
    {
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = _canvas;

        _canvas.Children.Add(_chainOutline);
        _canvas.Children.Add(_chain);
        _canvas.Children.Add(_prongOneOutline);
        _canvas.Children.Add(_prongTwoOutline);
        _canvas.Children.Add(_prongOne);
        _canvas.Children.Add(_prongTwo);
        _canvas.Children.Add(_tipOutline);
        _canvas.Children.Add(_tip);

        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void ShowBetween(Point startScreen, Point endScreen)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);

        if (!IsVisible)
        {
            Show();
        }

        var start = new Point(startScreen.X - Left, startScreen.Y - Top);
        var end = new Point(endScreen.X - Left, endScreen.Y - Top);
        var direction = end - start;
        if (direction.Length < 1)
        {
            direction = new Vector(1, 0);
        }
        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        var prongBase = end - direction * 19;
        var prongOne = prongBase + perpendicular * 10;
        var prongTwo = prongBase - perpendicular * 10;

        SetSegment(_chainOutline, start, end);
        SetSegment(_chain, start, end);
        SetSegment(_prongOneOutline, end, prongOne);
        SetSegment(_prongTwoOutline, end, prongTwo);
        SetSegment(_prongOne, end, prongOne);
        SetSegment(_prongTwo, end, prongTwo);
        SetTip(_tipOutline, end);
        SetTip(_tip, end);
    }

    public void HideHook()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private static Line Segment(Color color, double thickness, bool dashed = false)
    {
        return new Line
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeDashCap = PenLineCap.Round,
            StrokeDashArray = dashed ? new DoubleCollection([2.2, 1.25]) : null,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };
    }

    private static Ellipse Tip(Color color, double size)
    {
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
    }

    private static void SetSegment(Line line, Point start, Point end)
    {
        line.X1 = start.X;
        line.Y1 = start.Y;
        line.X2 = end.X;
        line.Y2 = end.Y;
    }

    private static void SetTip(FrameworkElement tip, Point point)
    {
        Canvas.SetLeft(tip, point.X - tip.Width / 2);
        Canvas.SetTop(tip, point.Y - tip.Height / 2);
    }

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr handle, int index, int newStyle);
}
