namespace HylianHeroCompanion;

public enum ActivityState
{
    Idle,
    Thinking,
    Planning,
    Executing,
    Searching,
    Creating,
    Viewing,
    Analyzing,
    Waiting,
    Success,
    Failed
}

public readonly record struct ActivitySignal(
    ActivityState State,
    TimeSpan Hold,
    int Priority,
    string Source);

public sealed record CompanionSettings(
    double Left,
    double Top,
    bool MonitorPaused = false,
    double? Scale = null);

public sealed record CompanionStatus(string State, string Clip, string Phase, DateTime UpdatedUtc);
