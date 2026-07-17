using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HylianHeroCompanion;

public partial class MainWindow : Window
{
    private enum InteractiveMode
    {
        None,
        Combat,
        Ocarina,
        TeleportReady,
        Teleporting
    }

    private readonly CodexActivityMonitor _monitor = new();
    private readonly AnimationEngine _animations;
    private readonly DispatcherTimer _fallbackTimer;
    private readonly DispatcherTimer? _demoTimer;
    private readonly DispatcherTimer _hookPullTimer;
    private readonly DispatcherTimer _ctrlClickTimer;
    private readonly MenuItem _statusItem;
    private readonly MenuItem _pauseItem;
    private readonly MenuItem _startupItem;
    private readonly MenuItem _sizeMenu;
    private readonly MenuItem _sizeSmallItem;
    private readonly MenuItem _sizeMediumItem;
    private readonly MenuItem _sizeMaxItem;
    private readonly ContextMenu _contextMenu;
    private ActivityState _activity = ActivityState.Idle;
    private DateTime _lockedUntil = DateTime.MinValue;
    private int _currentPriority;
    private bool _dragCandidate;
    private bool _dragging;
    private bool _rightDragCandidate;
    private bool _rightDragging;
    private Point _dragStartScreen;
    private Point _dragStartWindow;
    private double _dragDirectionAnchorX;
    private string? _dragClip;
    private HookLineWindow? _hookLineWindow;
    private MagicEffectWindow? _magicEffectWindow;
    private TeleportTargetWindow? _teleportTargetWindow;
    private bool _hookMode;
    private bool _pulling;
    private Point _hookTargetScreen;
    private Point _hookPullFrom;
    private Point _hookPullTo;
    private DateTime _hookPullStarted;
    private string _animationPhase = "holding";
    private double _scale = SizePolicy.MaxScale;
    private InteractiveMode _interactiveMode;
    private string? _lastCombatClip;
    private Point _teleportWindowTarget;
    private bool _teleportPositionMoved;

    public MainWindow(bool demoMode)
    {
        InitializeComponent();

        var assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", "animations");
        _animations = new AnimationEngine(PetImage, assetRoot);
        _animations.ClipChanged += OnClipChanged;
        _animations.ClipCompleted += OnClipCompleted;

        _statusItem = new MenuItem { Header = "Estado: Inactivo", IsEnabled = false };
        _pauseItem = new MenuItem { Header = "Pausar actividad", IsCheckable = true };
        _startupItem = new MenuItem { Header = "Iniciar con Windows", IsCheckable = true, IsChecked = StartupManager.IsEnabled };
        _sizeMenu = new MenuItem { Header = "Tamaño (100%)" };
        _sizeSmallItem = new MenuItem { Header = "Pequeño (60%)", IsCheckable = true };
        _sizeMediumItem = new MenuItem { Header = "Mediano (80%)", IsCheckable = true };
        _sizeMaxItem = new MenuItem { Header = "Máximo (100%)", IsCheckable = true, IsChecked = true };
        _sizeMenu.Items.Add(_sizeSmallItem);
        _sizeMenu.Items.Add(_sizeMediumItem);
        _sizeMenu.Items.Add(_sizeMaxItem);

        _contextMenu = new ContextMenu { Placement = PlacementMode.MousePoint };
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_pauseItem);
        _contextMenu.Items.Add(_startupItem);
        _contextMenu.Items.Add(_sizeMenu);
        _contextMenu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = $"Cerrar {BrandIdentity.ShortName}" };
        _contextMenu.Items.Add(exitItem);

        _pauseItem.Click += OnPauseClicked;
        _startupItem.Click += OnStartupClicked;
        _sizeSmallItem.Click += (_, _) => SetScale(SizePolicy.SmallScale);
        _sizeMediumItem.Click += (_, _) => SetScale(SizePolicy.MediumScale);
        _sizeMaxItem.Click += (_, _) => SetScale(SizePolicy.MaxScale);
        exitItem.Click += (_, _) => Application.Current.Shutdown();

        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        PreviewMouseRightButtonDown += OnMouseRightButtonDown;
        PreviewMouseRightButtonUp += OnMouseRightButtonUp;
        PreviewMouseWheel += OnMouseWheel;
        Loaded += OnLoaded;
        Closing += OnClosing;

        _monitor.ActivityDetected += OnActivityDetected;
        _fallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _fallbackTimer.Tick += OnFallbackTick;
        _hookPullTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _hookPullTimer.Tick += OnHookPullTick;
        _ctrlClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InteractionPolicy.CtrlSingleClickDelayMilliseconds)
        };
        _ctrlClickTimer.Tick += OnCtrlClickTimer;

        if (demoMode)
        {
            var states = Enum.GetValues<ActivityState>();
            var index = 0;
            _demoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _demoTimer.Tick += (_, _) => ApplySignal(new ActivitySignal(states[index++ % states.Length], TimeSpan.FromSeconds(3.8), 100, "demo"));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStore.Load();
        SetScale(settings?.Scale, preserveCenter: false, save: false);
        RestorePosition(settings);
        _hookLineWindow = new HookLineWindow { Owner = this };
        _magicEffectWindow = new MagicEffectWindow { Owner = this };
        _teleportTargetWindow = new TeleportTargetWindow { Owner = this };
        _teleportTargetWindow.TargetSelected += OnTeleportTargetSelected;
        _teleportTargetWindow.Cancelled += OnTeleportCancelled;
        _monitor.IsPaused = settings?.MonitorPaused ?? false;
        _pauseItem.IsChecked = _monitor.IsPaused;
        _pauseItem.Header = _monitor.IsPaused ? "Reanudar actividad" : "Pausar actividad";
        if (settings?.Scale is null)
        {
            SaveSettings();
        }

        _animations.Start(ActivityState.Idle);
        _fallbackTimer.Start();
        _monitor.Start();
        _demoTimer?.Start();
        UpdateStatus(ActivityState.Idle);
    }

    private void OnActivityDetected(ActivitySignal signal)
    {
        Dispatcher.BeginInvoke(() => ApplySignal(signal));
    }

    private void ApplySignal(ActivitySignal signal)
    {
        if (_monitor.IsPaused || _dragging || _rightDragging || _pulling || _interactiveMode != InteractiveMode.None)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < _lockedUntil && signal.Priority <= _currentPriority)
        {
            return;
        }

        _activity = signal.State;
        _currentPriority = signal.Priority;
        _lockedUntil = now + signal.Hold;
        _animations.SetActivity(signal.State);
        UpdateStatus(signal.State);
    }

    private void OnFallbackTick(object? sender, EventArgs e)
    {
        if (_monitor.IsPaused
            || _dragging
            || _rightDragging
            || _pulling
            || _interactiveMode != InteractiveMode.None
            || DateTime.UtcNow < _lockedUntil)
        {
            return;
        }

        var fallback = _monitor.IsTaskActive ? ActivityState.Thinking : ActivityState.Idle;
        if (_activity != fallback)
        {
            _activity = fallback;
            _currentPriority = fallback == ActivityState.Thinking ? 30 : 10;
            _animations.SetActivity(fallback);
            UpdateStatus(fallback);
        }
    }

    private void OnClipChanged(string clip)
    {
        _animationPhase = _interactiveMode switch
        {
            InteractiveMode.Combat => "interactive-combat",
            InteractiveMode.Ocarina => "ocarina-playing",
            InteractiveMode.TeleportReady => "teleport-ready",
            InteractiveMode.Teleporting => "teleporting",
            _ => _pulling ? "hook-pulling" : _hookMode ? "hook-extended" : "playing"
        };
        var suffix = InteractiveStatusSuffix()
            ?? (_pulling ? "desplazándose con el gancho" : _hookMode ? "gancho extendido" : null);
        UpdateStatus(_activity, suffix);
    }

    private void OnClipCompleted(string clip)
    {
        if (_interactiveMode == InteractiveMode.Combat
            && AnimationCatalog.InteractiveCombatClips.Contains(clip, StringComparer.OrdinalIgnoreCase))
        {
            _animationPhase = "holding";
            Dispatcher.BeginInvoke(EndCombatInteraction);
            return;
        }

        if (_interactiveMode == InteractiveMode.Ocarina
            && string.Equals(clip, "ocarina", StringComparison.OrdinalIgnoreCase))
        {
            _interactiveMode = InteractiveMode.TeleportReady;
            _animationPhase = "teleport-ready";
            _teleportTargetWindow?.Arm();
            UpdateStatus(_activity, "ocarina lista; Ctrl + doble clic para elegir destino");
            return;
        }

        _animationPhase = _pulling ? "hook-pulling" : _hookMode ? "hook-extended" : "holding";
        var suffix = InteractiveStatusSuffix()
            ?? (_pulling ? "desplazándose con el gancho" : _hookMode ? "gancho extendido" : null);
        UpdateStatus(_activity, suffix);
    }

    private void OnPauseClicked(object sender, RoutedEventArgs e)
    {
        _monitor.IsPaused = _pauseItem.IsChecked;
        _pauseItem.Header = _monitor.IsPaused ? "Reanudar actividad" : "Pausar actividad";
        if (_monitor.IsPaused)
        {
            _activity = ActivityState.Idle;
            _animations.SetActivity(ActivityState.Idle);
            UpdateStatus(ActivityState.Idle, "pausado");
        }
        else
        {
            _lockedUntil = DateTime.MinValue;
            UpdateStatus(_activity);
        }
        SaveSettings();
    }

    private void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var executable = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("No se encontró el ejecutable.");
            StartupManager.SetEnabled(_startupItem.IsChecked, executable);
        }
        catch
        {
            _startupItem.IsChecked = StartupManager.IsEnabled;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var intent = InteractionPolicy.ClassifyCtrlLeftClick(
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            e.ClickCount);

        if (intent != CtrlLeftClickIntent.None)
        {
            e.Handled = true;
            if (_pulling
                || _interactiveMode is InteractiveMode.Ocarina
                    or InteractiveMode.TeleportReady
                    or InteractiveMode.Teleporting)
            {
                return;
            }

            if (intent == CtrlLeftClickIntent.PendingCombat)
            {
                _ctrlClickTimer.Stop();
                _ctrlClickTimer.Interval = TimeSpan.FromMilliseconds(
                    InteractionPolicy.CtrlSingleClickDelayMilliseconds);
                _ctrlClickTimer.Start();
            }
            else
            {
                _ctrlClickTimer.Stop();
                StartOcarinaSequence();
            }
            return;
        }

        if (e.ClickCount != 1 || _pulling || _interactiveMode != InteractiveMode.None)
        {
            return;
        }

        _dragCandidate = true;
        _dragStartScreen = GetMouseScreenDip(e);
        _dragStartWindow = new Point(Left, Top);
        _dragDirectionAnchorX = _dragStartScreen.X;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnCtrlClickTimer(object? sender, EventArgs e)
    {
        _ctrlClickTimer.Stop();
        if (_interactiveMode == InteractiveMode.None && !_dragging && !_pulling)
        {
            PlayRandomCombatAction();
        }
    }

    private void PlayRandomCombatAction()
    {
        var available = AnimationCatalog.InteractiveCombatClips
            .Where(_animations.HasClip)
            .Where(name => !string.Equals(name, _lastCombatClip, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (available.Count == 0)
        {
            available = AnimationCatalog.InteractiveCombatClips
                .Where(_animations.HasClip)
                .ToList();
        }
        if (available.Count == 0)
        {
            return;
        }

        var clip = available[Random.Shared.Next(available.Count)];
        _lastCombatClip = clip;
        _interactiveMode = InteractiveMode.Combat;
        _animationPhase = "interactive-combat";
        _animations.ForceClip(clip);
        UpdateStatus(_activity, $"acción especial: {CombatLabel(clip)}");
    }

    private void EndCombatInteraction()
    {
        if (_interactiveMode != InteractiveMode.Combat)
        {
            return;
        }

        _interactiveMode = InteractiveMode.None;
        _animations.EndForcedClip();
        _animationPhase = _animations.IsPlaying ? "playing" : "holding";
        UpdateStatus(_activity);
    }

    private void StartOcarinaSequence()
    {
        if (!_animations.HasClip("ocarina"))
        {
            return;
        }

        _interactiveMode = InteractiveMode.Ocarina;
        _animationPhase = "ocarina-playing";
        _animations.ForceClip("ocarina");
        _magicEffectWindow?.StartMusic(() => HeroAnchor(new Point(Left, Top)));
        UpdateStatus(_activity, "tocando la ocarina");
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_rightDragCandidate && e.RightButton == MouseButtonState.Pressed)
        {
            var rightCurrentScreen = GetMouseScreenDip(e);
            var rightDelta = rightCurrentScreen - _dragStartScreen;
            if (!_rightDragging && rightDelta.Length < InteractionPolicy.DragThreshold)
            {
                return;
            }

            _rightDragging = true;
            if (InteractionPolicy.IsRightButtonHookGesture(rightDelta))
            {
                UpdateHookExtension(rightCurrentScreen, rightDelta);
            }

            return;
        }

        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = GetMouseScreenDip(e);
        var delta = currentScreen - _dragStartScreen;
        if (!_dragging && delta.Length < InteractionPolicy.DragThreshold)
        {
            return;
        }

        _dragging = true;
        Left = _dragStartWindow.X + delta.X;
        Top = _dragStartWindow.Y + delta.Y;

        var desiredClip = InteractionPolicy.ResolveHeldDragClip(
            _dragClip,
            currentScreen.X,
            delta.X,
            ref _dragDirectionAnchorX);

        if (!string.Equals(_dragClip, desiredClip, StringComparison.OrdinalIgnoreCase))
        {
            _dragClip = desiredClip;
            _animations.ForceLoopingClip(desiredClip);
            _animationPhase = "drag-loop";
            _statusItem.Header = "Estado: Moviéndose";
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragCandidate)
        {
            return;
        }

        _dragCandidate = false;
        ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            _dragClip = null;
            _dragDirectionAnchorX = 0;
            _animations.EndForcedClip();
            _animationPhase = _animations.IsPlaying ? "playing" : "holding";
            UpdateStatus(_activity);
            SaveSettings();
        }

        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1 || _pulling || _interactiveMode != InteractiveMode.None)
        {
            return;
        }

        _contextMenu.IsOpen = false;
        _rightDragCandidate = true;
        _rightDragging = false;
        _dragStartScreen = GetMouseScreenDip(e);
        _dragStartWindow = new Point(Left, Top);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rightDragCandidate)
        {
            return;
        }

        _rightDragCandidate = false;
        ReleaseMouseCapture();

        if (_hookMode)
        {
            var delta = _hookTargetScreen - _dragStartScreen;
            _rightDragging = false;
            if (InteractionPolicy.ShouldPullWithRightButton(delta))
            {
                BeginHookPull();
            }
            else
            {
                CancelHookGesture();
            }

            e.Handled = true;
            return;
        }

        var openMenu = !_rightDragging;
        _rightDragging = false;
        if (openMenu)
        {
            _contextMenu.PlacementTarget = Root;
            _contextMenu.IsOpen = true;
        }

        e.Handled = true;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || _dragging
            || _rightDragging
            || _pulling
            || _interactiveMode != InteractiveMode.None)
        {
            return;
        }

        var direction = e.Delta > 0 ? 1 : -1;
        SetScale(_scale + direction * SizePolicy.WheelStep);
        e.Handled = true;
    }

    private void SetScale(double? requestedScale, bool preserveCenter = true, bool save = true)
    {
        var normalized = SizePolicy.Normalize(requestedScale);
        var center = new Point(Left + Width / 2, Top + Height / 2);

        _scale = normalized;
        Width = SizePolicy.BaseWidth * _scale;
        Height = SizePolicy.BaseHeight * _scale;

        if (preserveCenter && IsLoaded)
        {
            var position = ClampWindowPosition(new Point(
                center.X - Width / 2,
                center.Y - Height / 2));
            Left = position.X;
            Top = position.Y;
        }

        UpdateSizeMenu();

        if (IsLoaded)
        {
            UpdateStatus(_activity);
            if (save)
            {
                SaveSettings();
            }
        }
    }

    private void UpdateSizeMenu()
    {
        var percentage = (int)Math.Round(_scale * 100);
        _sizeMenu.Header = $"Tamaño ({percentage}%)";
        _sizeSmallItem.IsChecked = Math.Abs(_scale - SizePolicy.SmallScale) < 0.001;
        _sizeMediumItem.IsChecked = Math.Abs(_scale - SizePolicy.MediumScale) < 0.001;
        _sizeMaxItem.IsChecked = Math.Abs(_scale - SizePolicy.MaxScale) < 0.001;
    }

    private void OnTeleportTargetSelected(Point targetScreen)
    {
        if (_interactiveMode != InteractiveMode.TeleportReady)
        {
            return;
        }

        var desired = new Point(
            targetScreen.X - ActualWidth * 0.52,
            targetScreen.Y - ActualHeight * 0.48);
        _teleportWindowTarget = ClampWindowPosition(desired);
        _teleportPositionMoved = false;
        _interactiveMode = InteractiveMode.Teleporting;
        _animationPhase = "teleporting";
        _teleportTargetWindow?.Disarm();
        _magicEffectWindow?.StopMusic();
        UpdateStatus(_activity, "viajando con Navi");

        var startAnchor = HeroAnchor(new Point(Left, Top));
        var endAnchor = HeroAnchor(_teleportWindowTarget);
        if (_magicEffectWindow is null)
        {
            Left = _teleportWindowTarget.X;
            Top = _teleportWindowTarget.Y;
            CompleteTeleport();
            return;
        }

        _magicEffectWindow.PlayTeleport(
            startAnchor,
            endAnchor,
            OnTeleportProgress,
            CompleteTeleport);
    }

    private void OnTeleportProgress(double progress)
    {
        Opacity = InteractionPolicy.TeleportOpacity(progress);
        if (!_teleportPositionMoved && progress >= InteractionPolicy.TeleportMoveAt)
        {
            Left = _teleportWindowTarget.X;
            Top = _teleportWindowTarget.Y;
            _teleportPositionMoved = true;
        }
    }

    private void CompleteTeleport()
    {
        Left = _teleportWindowTarget.X;
        Top = _teleportWindowTarget.Y;
        Opacity = 1;
        _teleportPositionMoved = true;
        _interactiveMode = InteractiveMode.None;
        _animations.EndForcedClip();
        _animationPhase = _animations.IsPlaying ? "playing" : "holding";
        UpdateStatus(_activity);
        SaveSettings();
    }

    private void OnTeleportCancelled()
    {
        if (_interactiveMode is not (InteractiveMode.Ocarina or InteractiveMode.TeleportReady))
        {
            return;
        }

        _interactiveMode = InteractiveMode.None;
        _teleportTargetWindow?.Disarm();
        _magicEffectWindow?.StopAll();
        Opacity = 1;
        _animations.EndForcedClip();
        _animationPhase = _animations.IsPlaying ? "playing" : "holding";
        UpdateStatus(_activity, "teletransporte cancelado");
    }

    private string? InteractiveStatusSuffix()
        => _interactiveMode switch
        {
            InteractiveMode.Combat when _lastCombatClip is not null
                => $"acción especial: {CombatLabel(_lastCombatClip)}",
            InteractiveMode.Ocarina => "tocando la ocarina",
            InteractiveMode.TeleportReady => "elige destino con Ctrl + doble clic",
            InteractiveMode.Teleporting => "viajando con Navi",
            _ => null
        };

    private static string CombatLabel(string clip)
        => clip switch
        {
            "sword-combo" => "combo de espada",
            "bow-shot" => "disparo de arco",
            "spin-attack" => "ataque giratorio",
            "bomb-shield" => "bomba y escudo",
            _ => clip
        };

    private void UpdateHookExtension(Point currentScreen, Vector delta)
    {
        if (!_hookMode)
        {
            _hookMode = true;
            Left = _dragStartWindow.X;
            Top = _dragStartWindow.Y;
            _dragClip = "hookshot";
            _animations.ForceClip("hookshot");
            _animationPhase = "hook-extended";
            UpdateStatus(_activity, "gancho extendido");
        }

        _hookTargetScreen = currentScreen;
        PetImage.RenderTransform = new ScaleTransform(delta.X < 0 ? -1 : 1, 1);
        _hookLineWindow?.ShowBetween(HeroAnchor(_dragStartWindow), currentScreen);
    }

    private void BeginHookPull()
    {
        var anchor = HeroAnchor(new Point(Left, Top));
        var direction = _hookTargetScreen - anchor;
        if (direction.Length < 1)
        {
            CancelHookGesture();
            return;
        }

        direction.Normalize();
        var landingAnchor = _hookTargetScreen - direction * 24;
        var desired = new Point(
            landingAnchor.X - ActualWidth * 0.52,
            landingAnchor.Y - ActualHeight * 0.48);

        _hookPullFrom = new Point(Left, Top);
        _hookPullTo = ClampWindowPosition(desired);
        _hookPullStarted = DateTime.UtcNow;
        _pulling = true;
        _animationPhase = "hook-pulling";
        UpdateStatus(_activity, "desplazándose con el gancho");
        _hookPullTimer.Start();
    }

    private void OnHookPullTick(object? sender, EventArgs e)
    {
        var raw = (DateTime.UtcNow - _hookPullStarted).TotalMilliseconds
            / InteractionPolicy.HookPullDurationMilliseconds;
        var progress = Math.Clamp(raw, 0, 1);
        var eased = progress < 0.5
            ? 4 * progress * progress * progress
            : 1 - Math.Pow(-2 * progress + 2, 3) / 2;

        Left = _hookPullFrom.X + (_hookPullTo.X - _hookPullFrom.X) * eased;
        Top = _hookPullFrom.Y + (_hookPullTo.Y - _hookPullFrom.Y) * eased;
        _hookLineWindow?.ShowBetween(HeroAnchor(new Point(Left, Top)), _hookTargetScreen);

        if (progress >= 1)
        {
            CompleteHookPull();
        }
    }

    private void CompleteHookPull()
    {
        _hookPullTimer.Stop();
        _hookLineWindow?.HideHook();
        _hookMode = false;
        _pulling = false;
        _dragClip = null;
        PetImage.RenderTransform = Transform.Identity;
        _animations.EndForcedClip();
        _animationPhase = _animations.IsPlaying ? "playing" : "holding";
        UpdateStatus(_activity);
        SaveSettings();
    }

    private void CancelHookGesture()
    {
        _hookPullTimer.Stop();
        _hookLineWindow?.HideHook();
        Left = _dragStartWindow.X;
        Top = _dragStartWindow.Y;
        _hookMode = false;
        _pulling = false;
        _dragClip = null;
        PetImage.RenderTransform = Transform.Identity;
        _animations.EndForcedClip();
        _animationPhase = _animations.IsPlaying ? "playing" : "holding";
        UpdateStatus(_activity);
    }

    private Point HeroAnchor(Point windowLocation)
        => new(windowLocation.X + ActualWidth * 0.52, windowLocation.Y + ActualHeight * 0.48);

    private Point ClampWindowPosition(Point position)
    {
        var minLeft = SystemParameters.VirtualScreenLeft - ActualWidth + 48;
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 48;
        var minTop = SystemParameters.VirtualScreenTop - ActualHeight + 48;
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 48;
        return new Point(
            Math.Clamp(position.X, minLeft, maxLeft),
            Math.Clamp(position.Y, minTop, maxTop));
    }

    private Point GetMouseScreenDip(MouseEventArgs e)
    {
        var devicePoint = PointToScreen(e.GetPosition(this));
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice.Transform(devicePoint) ?? devicePoint;
    }

    private void RestorePosition(CompanionSettings? settings)
    {
        var work = SystemParameters.WorkArea;
        var left = settings?.Left ?? work.Right - Width - 36;
        var top = settings?.Top ?? work.Bottom - Height - 28;

        Left = Math.Clamp(left, work.Left - Width + 48, work.Right - 48);
        Top = Math.Clamp(top, work.Top - Height + 48, work.Bottom - 48);
    }

    private void SaveSettings()
    {
        SettingsStore.Save(new CompanionSettings(Left, Top, _monitor.IsPaused, _scale));
    }

    private void UpdateStatus(ActivityState state, string? suffix = null)
    {
        var label = state switch
        {
            ActivityState.Idle => "Inactivo",
            ActivityState.Thinking => "Pensando",
            ActivityState.Planning => "Planificando",
            ActivityState.Executing => "Ejecutando",
            ActivityState.Searching => "Buscando",
            ActivityState.Creating => "Creando",
            ActivityState.Viewing => "Viendo imágenes",
            ActivityState.Analyzing => "Analizando",
            ActivityState.Waiting => "Esperando",
            ActivityState.Success => "Listo",
            ActivityState.Failed => "Bloqueado",
            _ => state.ToString()
        };

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            label += $" ({suffix})";
        }

        _statusItem.Header = $"Estado: {label}";
        ToolTip = $"{BrandIdentity.DisplayName} — {label}\nAnimación: {_animations.CurrentClipName}\nFase: {_animationPhase}\nTamaño: {Math.Round(_scale * 100)}%";
        SettingsStore.WriteStatus(new CompanionStatus(
            state.ToString(),
            _animations.CurrentClipName,
            _animationPhase,
            DateTime.UtcNow));
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        _demoTimer?.Stop();
        _fallbackTimer.Stop();
        _hookPullTimer.Stop();
        _ctrlClickTimer.Stop();
        _hookLineWindow?.HideHook();
        _hookLineWindow?.Close();
        if (_teleportTargetWindow is not null)
        {
            _teleportTargetWindow.TargetSelected -= OnTeleportTargetSelected;
            _teleportTargetWindow.Cancelled -= OnTeleportCancelled;
            _teleportTargetWindow.Disarm();
            _teleportTargetWindow.Close();
        }
        _magicEffectWindow?.StopAll();
        _magicEffectWindow?.Close();
        _monitor.Dispose();
        _animations.Dispose();
    }
}
