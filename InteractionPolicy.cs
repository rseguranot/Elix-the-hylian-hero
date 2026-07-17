using System.Runtime.InteropServices;
using System.Windows;

namespace HylianHeroCompanion;

public enum CtrlLeftClickIntent
{
    None,
    PendingCombat,
    Ocarina
}

public static class InteractionPolicy
{
    public const string HookActivation = "right-drag";
    public const double DragThreshold = 7;
    public const double DragDirectionSwitchDistance = 3;
    public const double HookPreviewDistance = 36;
    public const double HookPullDistance = 80;
    public const double HookVerticalRatio = 0.45;
    public const int HookPullDurationMilliseconds = 460;
    public const int TeleportDurationMilliseconds = 1550;
    public const double TeleportFadeOutEnd = 0.28;
    public const double TeleportMoveAt = 0.50;
    public const double TeleportFadeInStart = 0.72;

    public static int CtrlSingleClickDelayMilliseconds
        => (int)Math.Clamp(GetDoubleClickTime() + 40, 250, 1000);

    public static CtrlLeftClickIntent ClassifyCtrlLeftClick(bool controlPressed, int clickCount)
    {
        if (!controlPressed)
        {
            return CtrlLeftClickIntent.None;
        }

        return clickCount switch
        {
            1 => CtrlLeftClickIntent.PendingCombat,
            2 => CtrlLeftClickIntent.Ocarina,
            _ => CtrlLeftClickIntent.None
        };
    }

    public static bool IsHookGesture(Vector delta)
    {
        return delta.Length >= HookPreviewDistance
            && HasDiagonalIntent(delta);
    }

    public static bool ShouldPull(Vector delta) => IsHookGesture(delta) && delta.Length >= HookPullDistance;

    public static bool IsRightButtonHookGesture(Vector delta) => delta.Length >= HookPreviewDistance;

    public static bool ShouldPullWithRightButton(Vector delta) => delta.Length >= HookPullDistance;

    public static bool HasDiagonalIntent(Vector delta)
        => Math.Abs(delta.Y) > Math.Max(16, Math.Abs(delta.X) * HookVerticalRatio);

    public static string ResolveHeldDragClip(
        string? currentClip,
        double currentX,
        double initialDeltaX,
        ref double turnAnchorX)
    {
        if (!string.Equals(currentClip, "running-left", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(currentClip, "running-right", StringComparison.OrdinalIgnoreCase))
        {
            turnAnchorX = currentX;
            return initialDeltaX < 0 ? "running-left" : "running-right";
        }

        if (string.Equals(currentClip, "running-right", StringComparison.OrdinalIgnoreCase))
        {
            if (currentX > turnAnchorX)
            {
                turnAnchorX = currentX;
            }
            else if (turnAnchorX - currentX >= DragDirectionSwitchDistance)
            {
                turnAnchorX = currentX;
                return "running-left";
            }

            return "running-right";
        }

        if (currentX < turnAnchorX)
        {
            turnAnchorX = currentX;
        }
        else if (currentX - turnAnchorX >= DragDirectionSwitchDistance)
        {
            turnAnchorX = currentX;
            return "running-right";
        }

        return "running-left";
    }

    public static double TeleportOpacity(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (progress <= TeleportFadeOutEnd)
        {
            return 1 - progress / TeleportFadeOutEnd;
        }

        if (progress < TeleportFadeInStart)
        {
            return 0;
        }

        return (progress - TeleportFadeInStart) / (1 - TeleportFadeInStart);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}
