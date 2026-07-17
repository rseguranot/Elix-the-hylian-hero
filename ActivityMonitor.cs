using System.IO;
using System.Text;
using System.Text.Json;

namespace HylianHeroCompanion;

public sealed class CodexActivityMonitor : IDisposable
{
    private readonly string _sessionsRoot;
    private readonly Dictionary<string, FileCursor> _cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loopTask;
    private bool _initialized;
    private volatile bool _taskActive;
    private volatile bool _paused;

    public CodexActivityMonitor()
    {
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");
    }

    public event Action<ActivitySignal>? ActivityDetected;

    public bool IsTaskActive => _taskActive;

    public bool IsPaused
    {
        get => _paused;
        set => _paused = value;
    }

    public void Start()
    {
        _loopTask ??= Task.Run(() => MonitorLoop(_cancellation.Token));
    }

    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        var nextDiscovery = DateTime.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow >= nextDiscovery)
                {
                    DiscoverSessionFiles();
                    nextDiscovery = DateTime.UtcNow.AddSeconds(2);
                }

                foreach (var cursor in _cursors.Values.ToArray())
                {
                    ReadNewContent(cursor);
                }
            }
            catch
            {
                // A writer can rotate or replace a rollout while it is being read.
            }

            try
            {
                await Task.Delay(300, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void DiscoverSessionFiles()
    {
        if (!Directory.Exists(_sessionsRoot))
        {
            return;
        }

        var recent = Directory.EnumerateFiles(_sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(info => info.LastWriteTimeUtc >= DateTime.UtcNow.AddHours(-12))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Take(24)
            .ToList();

        foreach (var info in recent)
        {
            if (_cursors.ContainsKey(info.FullName))
            {
                continue;
            }

            _cursors[info.FullName] = new FileCursor(info.FullName)
            {
                Offset = _initialized ? 0 : info.Length
            };
        }

        _initialized = true;
    }

    private void ReadNewContent(FileCursor cursor)
    {
        using var stream = new FileStream(
            cursor.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length < cursor.Offset)
        {
            cursor.Offset = 0;
            cursor.Pending.SetLength(0);
            cursor.Pending.Position = 0;
        }

        if (stream.Length == cursor.Offset)
        {
            return;
        }

        stream.Position = cursor.Offset;
        var buffer = new byte[32 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ConsumeBytes(cursor, buffer, read);
            cursor.Offset += read;
        }
    }

    private void ConsumeBytes(FileCursor cursor, byte[] buffer, int count)
    {
        var segmentStart = 0;
        for (var i = 0; i < count; i++)
        {
            if (buffer[i] != (byte)'\n')
            {
                continue;
            }

            cursor.Pending.Write(buffer, segmentStart, i - segmentStart);
            ProcessLine(cursor.Pending);
            cursor.Pending.SetLength(0);
            cursor.Pending.Position = 0;
            segmentStart = i + 1;
        }

        if (segmentStart < count)
        {
            cursor.Pending.Write(buffer, segmentStart, count - segmentStart);
        }

        if (cursor.Pending.Length > 8 * 1024 * 1024)
        {
            cursor.Pending.SetLength(0);
            cursor.Pending.Position = 0;
        }
    }

    private void ProcessLine(MemoryStream pending)
    {
        if (_paused || pending.Length == 0)
        {
            return;
        }

        var bytes = pending.GetBuffer().AsMemory(0, checked((int)pending.Length));
        if (bytes.Length > 0 && bytes.Span[^1] == (byte)'\r')
        {
            bytes = bytes[..^1];
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            ProcessEvent(document.RootElement);
        }
        catch (JsonException)
        {
            // Ignore incomplete or non-event lines without affecting later events.
        }
    }

    private void ProcessEvent(JsonElement root)
    {
        if (!TryGetString(root, "type", out var envelopeType) || !root.TryGetProperty("payload", out var payload))
        {
            return;
        }

        if (envelopeType == "event_msg")
        {
            ProcessEventMessage(payload);
            return;
        }

        if (envelopeType == "response_item")
        {
            ProcessResponseItem(payload);
        }
    }

    private void ProcessEventMessage(JsonElement payload)
    {
        if (!TryGetString(payload, "type", out var type))
        {
            return;
        }

        switch (type)
        {
            case "task_started":
                _taskActive = true;
                Emit(ActivityState.Planning, 3.0, 55, type);
                break;
            case "agent_reasoning":
                _taskActive = true;
                Emit(ActivityState.Thinking, 2.2, 30, type);
                break;
            case "task_complete":
                _taskActive = false;
                Emit(ActivityState.Success, 4.5, 95, type);
                break;
            case "turn_aborted":
            case "task_failed":
                _taskActive = false;
                Emit(ActivityState.Failed, 5.0, 100, type);
                break;
        }
    }

    private void ProcessResponseItem(JsonElement payload)
    {
        if (!TryGetString(payload, "type", out var type))
        {
            return;
        }

        if (type == "reasoning")
        {
            _taskActive = true;
            Emit(ActivityState.Thinking, 2.2, 30, type);
            return;
        }

        if (type is "custom_tool_call" or "function_call")
        {
            _taskActive = true;
            TryGetString(payload, "name", out var name);
            var input = GetFlexibleText(payload, "input") ?? GetFlexibleText(payload, "arguments") ?? string.Empty;
            var state = ClassifyToolCall(name, input);
            var (hold, priority) = state switch
            {
                ActivityState.Waiting => (TimeSpan.FromSeconds(6), 85),
                ActivityState.Planning => (TimeSpan.FromSeconds(3.5), 60),
                _ => (TimeSpan.FromSeconds(5), 75)
            };
            ActivityDetected?.Invoke(new ActivitySignal(state, hold, priority, $"tool:{name}"));
            return;
        }

        if (type is "custom_tool_call_output" or "function_call_output")
        {
            var output = GetFlexibleText(payload, "output");
            if (LooksLikeFailure(output))
            {
                Emit(ActivityState.Failed, 4.0, 90, type);
            }
        }
    }

    public static ActivityState ClassifyToolCall(string? name, string? input)
    {
        var text = $"{name} {input}".ToLowerInvariant();

        if (ContainsAny(text, "request_user_input", "ask_user", "approval", "waiting for user"))
            return ActivityState.Waiting;
        if (ContainsAny(text, "image_gen", "imagegen", "generate_image"))
            return ActivityState.Creating;
        if (ContainsAny(text, "view_image", "screenshot", "open_image", "render_animation_previews"))
            return ActivityState.Viewing;
        if (ContainsAny(text, "web__run", "search_query", "image_query", "finance", "weather", "sports", "find\":", "browse"))
            return ActivityState.Searching;
        if (ContainsAny(text, "update_plan", "create_goal", "update_goal"))
            return ActivityState.Planning;
        if (ContainsAny(text, "validate", "inspect", "analyz", "evaluate", "self-test", "dotnet test", "git status", "read_thread_terminal"))
            return ActivityState.Analyzing;
        if (ContainsAny(text, "apply_patch", "create_new_file", "write", "edit", "document", "spreadsheet", "presentation", "compose_atlas"))
            return ActivityState.Creating;
        if (ContainsAny(text, "shell_command", "exec_command", "wait", "install", "build", "publish", "copy-item", "start-process"))
            return ActivityState.Executing;
        if (ContainsAny(text, "read_mcp", "list_mcp", "open", "get_", "list_"))
            return ActivityState.Searching;

        return ActivityState.Executing;
    }

    private static bool LooksLikeFailure(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var text = output.Length > 32_000 ? output[..32_000] : output;
        return ContainsAny(text.ToLowerInvariant(), "exit code: 1", "status\":\"failed", "script failed", "unhandled exception");
    }

    private void Emit(ActivityState state, double seconds, int priority, string source)
    {
        ActivityDetected?.Invoke(new ActivitySignal(state, TimeSpan.FromSeconds(seconds), priority, source));
    }

    private static bool ContainsAny(string text, params string[] values) => values.Any(text.Contains);

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static string? GetFlexibleText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Shutdown must remain quiet.
        }
        _cancellation.Dispose();
        foreach (var cursor in _cursors.Values)
        {
            cursor.Pending.Dispose();
        }
    }

    private sealed class FileCursor(string path)
    {
        public string Path { get; } = path;
        public long Offset { get; set; }
        public MemoryStream Pending { get; } = new();
    }
}
