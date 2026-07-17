using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HylianHeroCompanion;

public sealed class TeleportTargetWindow : Window
{
    public TeleportTargetWindow()
    {
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = true;
        Focusable = true;
        Topmost = true;
        Cursor = Cursors.Cross;
        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseRightButtonDown += OnMouseRightButtonDown;
        PreviewKeyDown += OnKeyDown;
    }

    public event Action<Point>? TargetSelected;

    public event Action? Cancelled;

    public void Arm()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);
        if (!IsVisible)
        {
            Show();
        }
        Activate();
        Focus();
    }

    public void Disarm()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var controlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (controlPressed && e.ClickCount == 2)
        {
            var local = e.GetPosition(this);
            var target = new Point(Left + local.X, Top + local.Y);
            Disarm();
            TargetSelected?.Invoke(target);
        }

        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelSelection();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelSelection();
            e.Handled = true;
        }
    }

    private void CancelSelection()
    {
        Disarm();
        Cancelled?.Invoke();
    }
}
