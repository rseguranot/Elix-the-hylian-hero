using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HylianHeroCompanion;

public sealed class AnimationEngine : IDisposable
{
    public const string PlaybackMode = "one-shot";
    public const string HeldDragPlaybackMode = "loop-while-held";

    private readonly Image _target;
    private readonly string _assetRoot;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, AnimationClip> _clips;
    private readonly Dictionary<ActivityState, string> _lastClipByState = new();
    private ActivityState _state = ActivityState.Idle;
    private AnimationClip? _current;
    private int _frameIndex;
    private string? _forcedClip;
    private bool _forcedClipLoops;
    private string? _lastClipGlobal;
    private AnimationClip? _suspendedClip;
    private int _suspendedFrameIndex;
    private bool _suspendedWasPlaying;

    public AnimationEngine(Image target, string assetRoot)
    {
        _target = target;
        _assetRoot = assetRoot;
        _clips = LoadClips(assetRoot);
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _timer.Tick += OnTick;
    }

    public string CurrentClipName => _current?.Name ?? "none";

    public bool IsPlaying => _timer.IsEnabled;

    public bool IsForcedLooping => _forcedClip is not null && _forcedClipLoops;

    public event Action<string>? ClipChanged;

    public event Action<string>? ClipCompleted;

    public bool HasClip(string name) => _clips.ContainsKey(name);

    public void Start(ActivityState initialState = ActivityState.Idle)
    {
        _state = initialState;
        SelectNextClip();
    }

    public void SetActivity(ActivityState state)
    {
        if (_forcedClip is not null)
        {
            _state = state;
            return;
        }

        if (_state == state && _current is not null)
        {
            return;
        }

        _state = state;
        SelectNextClip();
    }

    public void ForceClip(string name)
    {
        ForceClip(name, loop: false);
    }

    public void ForceLoopingClip(string name)
    {
        ForceClip(name, loop: true);
    }

    private void ForceClip(string name, bool loop)
    {
        if (!_clips.TryGetValue(name, out var clip)
            || (_forcedClip == name && _forcedClipLoops == loop))
        {
            return;
        }

        if (_forcedClip is null)
        {
            _suspendedClip = _current;
            _suspendedFrameIndex = _frameIndex;
            _suspendedWasPlaying = _timer.IsEnabled;
        }

        _forcedClip = name;
        _forcedClipLoops = loop;
        BeginClip(clip);
    }

    public void EndForcedClip()
    {
        if (_forcedClip is null)
        {
            return;
        }

        _forcedClip = null;
        _forcedClipLoops = false;
        if (_suspendedClip is not null)
        {
            _current = _suspendedClip;
            _frameIndex = Math.Clamp(_suspendedFrameIndex, 0, _current.Frames.Count - 1);
            _timer.Interval = TimeSpan.FromMilliseconds(_current.FrameMilliseconds);
            ShowFrame();
            ClipChanged?.Invoke(_current.Name);
            if (_suspendedWasPlaying)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                ClipCompleted?.Invoke(_current.Name);
            }
        }

        _suspendedClip = null;
        _suspendedFrameIndex = 0;
        _suspendedWasPlaying = false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_current is null || _current.Frames.Count == 0)
        {
            return;
        }

        _frameIndex++;
        if (_frameIndex >= _current.Frames.Count)
        {
            if (_forcedClipLoops
                && string.Equals(_forcedClip, _current.Name, StringComparison.OrdinalIgnoreCase))
            {
                _frameIndex = 0;
                ShowFrame();
                return;
            }

            _frameIndex = _current.Frames.Count - 1;
            _timer.Stop();
            ShowFrame();
            ClipCompleted?.Invoke(_current.Name);
            return;
        }

        ShowFrame();
    }

    private void SelectNextClip()
    {
        var configured = AnimationCatalog.For(_state);
        var available = configured.Where(_clips.ContainsKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (available.Count == 0 && _clips.ContainsKey("idle"))
        {
            available.Add("idle");
        }

        if (available.Count == 0)
        {
            _current = null;
            _target.Source = null;
            return;
        }

        _lastClipByState.TryGetValue(_state, out var last);
        var candidates = available
            .Where(name => !string.Equals(name, last, StringComparison.OrdinalIgnoreCase))
            .Where(name => !string.Equals(name, _lastClipGlobal, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = available
                .Where(name => !string.Equals(name, _lastClipGlobal, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            candidates = available
                .Where(name => !string.Equals(name, last, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            candidates = available;
        }

        var selected = candidates[Random.Shared.Next(candidates.Count)];
        _lastClipByState[_state] = selected;
        _lastClipGlobal = selected;
        BeginClip(_clips[selected]);
    }

    private void BeginClip(AnimationClip clip)
    {
        _current = clip;
        _frameIndex = 0;
        _timer.Interval = TimeSpan.FromMilliseconds(clip.FrameMilliseconds);
        ShowFrame();
        ClipChanged?.Invoke(clip.Name);
        _timer.Start();
    }

    private void ShowFrame()
    {
        if (_current is null || _current.Frames.Count == 0)
        {
            return;
        }

        _target.Source = _current.Frames[_frameIndex];
    }

    private static Dictionary<string, AnimationClip> LoadClips(string root)
    {
        var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
        {
            return clips;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(directory);
            var frames = Directory.EnumerateFiles(directory, "*.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(LoadBitmap)
                .ToList();

            if (frames.Count > 0)
            {
                clips[name] = new AnimationClip(name, frames, AnimationCatalog.FrameMilliseconds(name));
            }
        }

        return clips;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private sealed record AnimationClip(string Name, IReadOnlyList<BitmapImage> Frames, int FrameMilliseconds);
}

public static class AnimationCatalog
{
    public static readonly IReadOnlyList<string> InteractiveCombatClips =
        ["sword-combo", "bow-shot", "spin-attack", "bomb-shield"];

    private static readonly IReadOnlyDictionary<ActivityState, string[]> StateClips =
        new Dictionary<ActivityState, string[]>
        {
            [ActivityState.Idle] = ["idle", "waiting"],
            [ActivityState.Thinking] = ["fairy-think", "running", "review"],
            [ActivityState.Planning] = ["fairy-think", "review", "bow-shot"],
            [ActivityState.Executing] = ["sword-combo", "spin-attack", "running"],
            [ActivityState.Searching] = ["bow-shot", "hookshot", "review"],
            [ActivityState.Creating] = ["bomb-shield", "spin-attack", "sword-combo"],
            [ActivityState.Viewing] = ["review", "fairy-think"],
            [ActivityState.Analyzing] = ["review", "bow-shot", "fairy-think"],
            [ActivityState.Waiting] = ["waiting", "waving"],
            [ActivityState.Success] = ["spin-attack", "jumping", "waving"],
            [ActivityState.Failed] = ["failed", "bomb-shield"]
        };

    private static readonly IReadOnlyDictionary<string, int> Durations =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = 180,
            ["waiting"] = 190,
            ["waving"] = 150,
            ["jumping"] = 120,
            ["failed"] = 180,
            ["review"] = 160,
            ["running"] = 145,
            ["running-left"] = 105,
            ["running-right"] = 105,
            ["sword-combo"] = 145,
            ["spin-attack"] = 130,
            ["bomb-shield"] = 170,
            ["fairy-think"] = 190,
            ["bow-shot"] = 165,
            ["hookshot"] = 145,
            ["ocarina"] = 180
        };

    public static IReadOnlyList<string> For(ActivityState state) => StateClips[state];

    public static int FrameMilliseconds(string clip) => Durations.TryGetValue(clip, out var duration) ? duration : 150;

    public static IReadOnlyCollection<string> RequiredClips => StateClips.Values
        .SelectMany(x => x)
        .Append("ocarina")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
