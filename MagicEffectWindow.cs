using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HylianHeroCompanion;

public sealed class MagicEffectWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Canvas _canvas = new() { Background = Brushes.Transparent, IsHitTestVisible = false };
    private readonly DispatcherTimer _timer;
    private readonly List<MusicNote> _notes = [];
    private readonly List<Spark> _sparks = [];
    private readonly Ellipse _naviGlow = Circle(28, Color.FromArgb(75, 70, 220, 255));
    private readonly Ellipse _naviWingLeft = Wing(-28);
    private readonly Ellipse _naviWingRight = Wing(28);
    private readonly Ellipse _naviHalo = Circle(15, Color.FromArgb(150, 115, 235, 255));
    private readonly Ellipse _naviCore = Circle(7, Colors.White);
    private Func<Point>? _musicAnchorProvider;
    private DateTime _lastNoteSpawn;
    private bool _teleporting;
    private DateTime _teleportStarted;
    private Point _teleportStart;
    private Point _teleportEnd;
    private Action<double>? _teleportProgress;
    private Action? _teleportCompleted;

    public MagicEffectWindow()
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

        _canvas.Children.Add(_naviGlow);
        _canvas.Children.Add(_naviWingLeft);
        _canvas.Children.Add(_naviWingRight);
        _canvas.Children.Add(_naviHalo);
        _canvas.Children.Add(_naviCore);
        SetNaviVisible(false);

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void StartMusic(Func<Point> anchorProvider)
    {
        _musicAnchorProvider = anchorProvider;
        _lastNoteSpawn = DateTime.MinValue;
        EnsureVisible();
        _timer.Start();
    }

    public void StopMusic()
    {
        _musicAnchorProvider = null;
    }

    public void PlayTeleport(
        Point startScreen,
        Point endScreen,
        Action<double> progress,
        Action completed)
    {
        _musicAnchorProvider = null;
        _teleporting = true;
        _teleportStarted = DateTime.UtcNow;
        _teleportStart = startScreen;
        _teleportEnd = endScreen;
        _teleportProgress = progress;
        _teleportCompleted = completed;
        SetNaviVisible(true);
        EnsureVisible();
        _timer.Start();
    }

    public void StopAll()
    {
        _musicAnchorProvider = null;
        _teleporting = false;
        _teleportProgress = null;
        _teleportCompleted = null;
        _timer.Stop();
        ClearParticles();
        SetNaviVisible(false);
        if (IsVisible)
        {
            Hide();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        if (_musicAnchorProvider is not null
            && (now - _lastNoteSpawn).TotalMilliseconds >= 245)
        {
            SpawnNote(_musicAnchorProvider());
            _lastNoteSpawn = now;
        }

        UpdateNotes(now);
        UpdateSparks(now);

        if (_teleporting)
        {
            UpdateTeleport(now);
        }

        if (_musicAnchorProvider is null && !_teleporting && _notes.Count == 0 && _sparks.Count == 0)
        {
            _timer.Stop();
            if (IsVisible)
            {
                Hide();
            }
        }
    }

    private void SpawnNote(Point anchorScreen)
    {
        var origin = ToCanvas(anchorScreen + new Vector(Random.Shared.Next(-30, 31), -24));
        var note = new TextBlock
        {
            Text = Random.Shared.Next(2) == 0 ? "♪" : "♫",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = Random.Shared.Next(17, 24),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Random.Shared.Next(2) == 0
                ? Color.FromRgb(116, 232, 255)
                : Color.FromRgb(255, 222, 104)),
            IsHitTestVisible = false,
            Opacity = 0
        };

        _canvas.Children.Add(note);
        _notes.Add(new MusicNote(
            note,
            origin,
            DateTime.UtcNow,
            Random.Shared.NextDouble() * Math.PI * 2,
            Random.Shared.Next(1050, 1450)));
    }

    private void UpdateNotes(DateTime now)
    {
        for (var index = _notes.Count - 1; index >= 0; index--)
        {
            var note = _notes[index];
            var elapsed = (now - note.Created).TotalMilliseconds;
            var progress = elapsed / note.LifetimeMilliseconds;
            if (progress >= 1)
            {
                _canvas.Children.Remove(note.Element);
                _notes.RemoveAt(index);
                continue;
            }

            var x = note.Origin.X + Math.Sin(note.Phase + progress * Math.PI * 2) * 14;
            var y = note.Origin.Y - progress * 78;
            Canvas.SetLeft(note.Element, x);
            Canvas.SetTop(note.Element, y);
            note.Element.Opacity = Math.Min(1, progress * 5) * Math.Min(1, (1 - progress) * 2.3);
        }
    }

    private void UpdateTeleport(DateTime now)
    {
        var raw = (now - _teleportStarted).TotalMilliseconds
            / InteractionPolicy.TeleportDurationMilliseconds;
        var progress = Math.Clamp(raw, 0, 1);
        Point position;

        if (progress < 0.28)
        {
            var orbit = progress / 0.28 * Math.PI * 3.2;
            var radius = 42 - progress * 55;
            position = _teleportStart + new Vector(Math.Cos(orbit) * radius, Math.Sin(orbit) * radius * 0.55);
            SpawnSparks(position, 3, outward: false);
        }
        else if (progress < 0.76)
        {
            var travel = (progress - 0.28) / 0.48;
            var eased = travel * travel * (3 - 2 * travel);
            var line = _teleportEnd - _teleportStart;
            position = _teleportStart + line * eased;
            position.Y -= Math.Sin(travel * Math.PI) * Math.Min(120, Math.Max(55, line.Length * 0.10));
            SpawnSparks(position, 2, outward: false);
        }
        else
        {
            var settle = (progress - 0.76) / 0.24;
            var orbit = settle * Math.PI * 3.5;
            var radius = 10 + (1 - settle) * 32;
            position = _teleportEnd + new Vector(Math.Cos(orbit) * radius, Math.Sin(orbit) * radius * 0.58);
            SpawnSparks(_teleportEnd, 4, outward: true);
        }

        SetNaviPosition(position);
        _teleportProgress?.Invoke(progress);

        if (progress >= 1)
        {
            _teleporting = false;
            SetNaviVisible(false);
            var completed = _teleportCompleted;
            _teleportProgress = null;
            _teleportCompleted = null;
            completed?.Invoke();
        }
    }

    private void SpawnSparks(Point screenPoint, int count, bool outward)
    {
        if (_sparks.Count > 150)
        {
            return;
        }

        var origin = ToCanvas(screenPoint);
        for (var i = 0; i < count; i++)
        {
            var angle = Random.Shared.NextDouble() * Math.PI * 2;
            var speed = outward ? Random.Shared.Next(36, 92) : Random.Shared.Next(12, 42);
            var size = Random.Shared.Next(3, 8);
            var element = Circle(size, Random.Shared.Next(3) switch
            {
                0 => Color.FromArgb(230, 255, 255, 255),
                1 => Color.FromArgb(220, 93, 225, 255),
                _ => Color.FromArgb(220, 255, 222, 104)
            });
            _canvas.Children.Add(element);
            _sparks.Add(new Spark(
                element,
                origin,
                new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                DateTime.UtcNow,
                Random.Shared.Next(420, 880)));
        }
    }

    private void UpdateSparks(DateTime now)
    {
        for (var index = _sparks.Count - 1; index >= 0; index--)
        {
            var spark = _sparks[index];
            var elapsedMilliseconds = (now - spark.Created).TotalMilliseconds;
            var progress = elapsedMilliseconds / spark.LifetimeMilliseconds;
            if (progress >= 1)
            {
                _canvas.Children.Remove(spark.Element);
                _sparks.RemoveAt(index);
                continue;
            }

            var seconds = elapsedMilliseconds / 1000;
            var position = spark.Origin + spark.Velocity * seconds;
            Canvas.SetLeft(spark.Element, position.X - spark.Element.Width / 2);
            Canvas.SetTop(spark.Element, position.Y - spark.Element.Height / 2);
            spark.Element.Opacity = Math.Min(1, progress * 6) * (1 - progress);
        }
    }

    private void SetNaviPosition(Point screenPoint)
    {
        var point = ToCanvas(screenPoint);
        PositionCentered(_naviGlow, point);
        PositionCentered(_naviHalo, point);
        PositionCentered(_naviCore, point);
        PositionCentered(_naviWingLeft, point + new Vector(-10, -1));
        PositionCentered(_naviWingRight, point + new Vector(10, -1));
    }

    private void EnsureVisible()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);
        if (!IsVisible)
        {
            Show();
        }
    }

    private Point ToCanvas(Point screenPoint)
        => new(screenPoint.X - Left, screenPoint.Y - Top);

    private void ClearParticles()
    {
        foreach (var note in _notes)
        {
            _canvas.Children.Remove(note.Element);
        }
        _notes.Clear();

        foreach (var spark in _sparks)
        {
            _canvas.Children.Remove(spark.Element);
        }
        _sparks.Clear();
    }

    private void SetNaviVisible(bool visible)
    {
        var value = visible ? Visibility.Visible : Visibility.Collapsed;
        _naviGlow.Visibility = value;
        _naviWingLeft.Visibility = value;
        _naviWingRight.Visibility = value;
        _naviHalo.Visibility = value;
        _naviCore.Visibility = value;
    }

    private static Ellipse Circle(double size, Color color)
        => new()
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false
        };

    private static Ellipse Wing(double angle)
        => new()
        {
            Width = 18,
            Height = 8,
            Fill = new SolidColorBrush(Color.FromArgb(155, 180, 245, 255)),
            RenderTransform = new RotateTransform(angle),
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false
        };

    private static void PositionCentered(FrameworkElement element, Point point)
    {
        Canvas.SetLeft(element, point.X - element.Width / 2);
        Canvas.SetTop(element, point.Y - element.Height / 2);
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

    private sealed record MusicNote(
        TextBlock Element,
        Point Origin,
        DateTime Created,
        double Phase,
        double LifetimeMilliseconds);

    private sealed record Spark(
        Ellipse Element,
        Point Origin,
        Vector Velocity,
        DateTime Created,
        double LifetimeMilliseconds);
}
