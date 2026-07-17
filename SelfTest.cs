using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HylianHeroCompanion;

public static class SelfTest
{
    public static int Run()
    {
        var lines = new List<string>();
        var assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", "animations");
        var ok = true;

        var identityOk = typeof(SelfTest).Assembly.GetName().Name == BrandIdentity.TechnicalName
            && BrandIdentity.DisplayName == "Elix — The Hylian Hero";
        ok &= identityOk;
        lines.Add($"identity: display={BrandIdentity.DisplayName}, executable={BrandIdentity.TechnicalName}, ok={identityOk}");

        foreach (var clip in AnimationCatalog.RequiredClips.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.Combine(assetRoot, clip);
            var frameCount = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.png").Count() : 0;
            var clipOk = frameCount >= 4;
            ok &= clipOk;
            lines.Add($"{clip}: frames={frameCount}, ok={clipOk}");
        }

        using (var animation = new AnimationEngine(new Image(), assetRoot))
        {
            animation.Start(ActivityState.Executing);
            var firstClip = animation.CurrentClipName;
            PumpDispatcher(TimeSpan.FromSeconds(2));
            var stoppedAfterOnePass = !animation.IsPlaying;

            animation.SetActivity(ActivityState.Executing);
            var sameStateStayedStill = !animation.IsPlaying && animation.CurrentClipName == firstClip;

            animation.SetActivity(ActivityState.Thinking);
            var transitionedOnce = animation.IsPlaying && animation.CurrentClipName != firstClip;
            PumpDispatcher(TimeSpan.FromSeconds(2));
            var secondPassStopped = !animation.IsPlaying;

            var oneShotOk = stoppedAfterOnePass && sameStateStayedStill && transitionedOnce && secondPassStopped;
            ok &= oneShotOk;
            lines.Add($"one-shot runtime: first={firstClip}, stopped={stoppedAfterOnePass}, same-state-hold={sameStateStayedStill}, transition={transitionedOnce}, second-stopped={secondPassStopped}, ok={oneShotOk}");

            var ambientStarted = animation.PlayOneShotClip("idle-breathe")
                && animation.IsPlaying
                && animation.CurrentClipName == "idle-breathe";
            PumpDispatcher(TimeSpan.FromSeconds(2));
            var ambientStopped = !animation.IsPlaying
                && animation.CurrentClipName == "idle-breathe";
            var ambientOneShotOk = ambientStarted && ambientStopped;
            ok &= ambientOneShotOk;
            lines.Add($"ambient idle one-shot: started={ambientStarted}, stopped={ambientStopped}, ok={ambientOneShotOk}");

            animation.ForceLoopingClip("running-right");
            PumpDispatcher(TimeSpan.FromMilliseconds(1900));
            var rightLooped = animation.IsPlaying
                && animation.IsForcedLooping
                && animation.CurrentClipName == "running-right";

            animation.ForceLoopingClip("running-left");
            PumpDispatcher(TimeSpan.FromMilliseconds(1000));
            var switchedDirection = animation.IsPlaying
                && animation.IsForcedLooping
                && animation.CurrentClipName == "running-left";

            animation.EndForcedClip();
            var stoppedOnRelease = !animation.IsForcedLooping
                && !animation.IsPlaying
                && animation.CurrentClipName != "running-left";
            var heldDragLoopOk = rightLooped && switchedDirection && stoppedOnRelease;
            ok &= heldDragLoopOk;
            lines.Add($"held drag loop: right={rightLooped}, switched-left={switchedDirection}, stopped-on-release={stoppedOnRelease}, mode={AnimationEngine.HeldDragPlaybackMode}, ok={heldDragLoopOk}");
        }

        foreach (var state in Enum.GetValues<ActivityState>())
        {
            var variantCount = AnimationCatalog.For(state).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var stateOk = variantCount >= 2;
            ok &= stateOk;
            lines.Add($"rotation {state}: variants={variantCount}, ok={stateOk}");
        }

        var playbackOk = AnimationEngine.PlaybackMode == "one-shot";
        ok &= playbackOk;
        lines.Add($"playback mode={AnimationEngine.PlaybackMode}, held-drag={AnimationEngine.HeldDragPlaybackMode}, ok={playbackOk}");

        var sizePolicyOk = SizePolicy.Normalize(null) == SizePolicy.MaxScale
            && SizePolicy.Normalize(1.5) == SizePolicy.MaxScale
            && SizePolicy.Normalize(0.1) == SizePolicy.MinScale
            && SizePolicy.Normalize(0.83) == 0.83
            && SizePolicy.BaseWidth * SizePolicy.MaxScale == 240
            && SizePolicy.BaseHeight * SizePolicy.MaxScale == 260;
        ok &= sizePolicyOk;
        lines.Add($"size policy: min={SizePolicy.MinScale:P0}, max={SizePolicy.MaxScale:P0}, step={SizePolicy.WheelStep:P0}, ok={sizePolicyOk}");

        double turnAnchorX = 0;
        var initialRight = InteractionPolicy.ResolveHeldDragClip(null, 100, 20, ref turnAnchorX);
        var continuedRight = InteractionPolicy.ResolveHeldDragClip(initialRight, 110, 30, ref turnAnchorX);
        var ignoredJitter = InteractionPolicy.ResolveHeldDragClip(continuedRight, 108, 28, ref turnAnchorX);
        var reversedLeft = InteractionPolicy.ResolveHeldDragClip(ignoredJitter, 106.9, 26.9, ref turnAnchorX);
        var continuedLeft = InteractionPolicy.ResolveHeldDragClip(reversedLeft, 100, 20, ref turnAnchorX);
        var reversedRight = InteractionPolicy.ResolveHeldDragClip(continuedLeft, 103.1, 23.1, ref turnAnchorX);
        var fastDirectionOk = initialRight == "running-right"
            && continuedRight == "running-right"
            && ignoredJitter == "running-right"
            && reversedLeft == "running-left"
            && continuedLeft == "running-left"
            && reversedRight == "running-right"
            && InteractionPolicy.DragDirectionSwitchDistance <= 3;
        ok &= fastDirectionOk;
        lines.Add($"fast drag direction: threshold={InteractionPolicy.DragDirectionSwitchDistance}px, right={initialRight}, jitter={ignoredJitter}, left={reversedLeft}, right-again={reversedRight}, ok={fastDirectionOk}");

        var ctrlClickOk = InteractionPolicy.ClassifyCtrlLeftClick(true, 1) == CtrlLeftClickIntent.PendingCombat
            && InteractionPolicy.ClassifyCtrlLeftClick(true, 2) == CtrlLeftClickIntent.Ocarina
            && InteractionPolicy.ClassifyCtrlLeftClick(false, 2) == CtrlLeftClickIntent.None
            && InteractionPolicy.CtrlSingleClickDelayMilliseconds is >= 250 and <= 1000;
        ok &= ctrlClickOk;
        lines.Add($"ctrl click gestures: delay={InteractionPolicy.CtrlSingleClickDelayMilliseconds}ms, ok={ctrlClickOk}");

        var teleportOpacityOk = InteractionPolicy.TeleportOpacity(0) == 1
            && InteractionPolicy.TeleportOpacity(InteractionPolicy.TeleportMoveAt) == 0
            && InteractionPolicy.TeleportOpacity(1) == 1;
        ok &= teleportOpacityOk;
        lines.Add($"teleport opacity timeline: ok={teleportOpacityOk}");

        var combatPoolOk = AnimationCatalog.InteractiveCombatClips.Count >= 3
            && AnimationCatalog.InteractiveCombatClips.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                == AnimationCatalog.InteractiveCombatClips.Count;
        ok &= combatPoolOk;
        lines.Add($"interactive combat pool: clips={string.Join(',', AnimationCatalog.InteractiveCombatClips)}, ok={combatPoolOk}");

        var idleLifePool = AnimationCatalog.IdleLifeClips
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var idleLifeDelaysOk = Enumerable.Range(0, 50)
            .Select(seed => IdleLifePolicy.NextDelay(random: new Random(seed)).TotalMilliseconds)
            .All(delay => delay >= IdleLifePolicy.MinDelayMilliseconds
                && delay <= IdleLifePolicy.MaxDelayMilliseconds);
        var idleLifeNoImmediateRepeat = Enumerable.Range(0, 50)
            .Select(seed => IdleLifePolicy.SelectNextClip(idleLifePool, "idle", new Random(seed)))
            .All(clip => clip is not null && !string.Equals(clip, "idle", StringComparison.OrdinalIgnoreCase));
        var idleLifeOk = idleLifePool.Count >= 3
            && idleLifeDelaysOk
            && idleLifeNoImmediateRepeat;
        ok &= idleLifeOk;
        lines.Add($"ambient idle life: clips={string.Join(',', idleLifePool)}, delay={IdleLifePolicy.MinDelayMilliseconds}-{IdleLifePolicy.MaxDelayMilliseconds}ms, no-repeat={idleLifeNoImmediateRepeat}, ok={idleLifeOk}");

        var hookCases = new Dictionary<Vector, bool>
        {
            [new Vector(100, 100)] = true,
            [new Vector(-90, 110)] = true,
            [new Vector(120, 5)] = false,
            [new Vector(20, 20)] = false
        };

        foreach (var pair in hookCases)
        {
            var actual = InteractionPolicy.IsHookGesture(pair.Key);
            var caseOk = actual == pair.Value;
            ok &= caseOk;
            lines.Add($"hook gesture {pair.Key}: actual={actual}, expected={pair.Value}, ok={caseOk}");
        }

        var rightButtonOnlyOk = InteractionPolicy.HookActivation == "right-drag"
            && InteractionPolicy.IsRightButtonHookGesture(new Vector(120, 5))
            && !InteractionPolicy.IsRightButtonHookGesture(new Vector(20, 20));
        ok &= rightButtonOnlyOk;
        lines.Add($"hook activation={InteractionPolicy.HookActivation}, ok={rightButtonOnlyOk}");

        var pullOk = InteractionPolicy.ShouldPullWithRightButton(new Vector(90, 5))
            && !InteractionPolicy.ShouldPullWithRightButton(new Vector(40, 40));
        ok &= pullOk;
        lines.Add($"right-button hook pull threshold={InteractionPolicy.HookPullDistance}, ok={pullOk}");

        var hookWindow = new HookLineWindow { Opacity = 0 };
        hookWindow.ShowBetween(
            new Point(SystemParameters.VirtualScreenLeft + 100, SystemParameters.VirtualScreenTop + 100),
            new Point(SystemParameters.VirtualScreenLeft + 260, SystemParameters.VirtualScreenTop + 220));
        PumpDispatcher(TimeSpan.FromMilliseconds(100));
        var hookOverlayOk = hookWindow.IsVisible;
        hookWindow.HideHook();
        hookWindow.Close();
        ok &= hookOverlayOk;
        lines.Add($"hook overlay runtime: visible={hookOverlayOk}, ok={hookOverlayOk}");

        var magicCompleted = false;
        var magicWindow = new MagicEffectWindow { Opacity = 0 };
        magicWindow.PlayTeleport(
            new Point(SystemParameters.VirtualScreenLeft + 120, SystemParameters.VirtualScreenTop + 120),
            new Point(SystemParameters.VirtualScreenLeft + 360, SystemParameters.VirtualScreenTop + 260),
            _ => { },
            () => magicCompleted = true);
        PumpDispatcher(TimeSpan.FromMilliseconds(InteractionPolicy.TeleportDurationMilliseconds + 250));
        magicWindow.StopAll();
        magicWindow.Close();
        ok &= magicCompleted;
        lines.Add($"teleport effect runtime: completed={magicCompleted}, ok={magicCompleted}");

        var classifierCases = new Dictionary<string, ActivityState>
        {
            ["await tools.update_plan({plan: []})"] = ActivityState.Planning,
            ["await tools.image_gen__imagegen({prompt: 'x'})"] = ActivityState.Creating,
            ["await tools.view_image({path: 'x'})"] = ActivityState.Viewing,
            ["await tools.web__run({search_query: []})"] = ActivityState.Searching,
            ["await tools.apply_patch('x')"] = ActivityState.Creating,
            ["await tools.shell_command({command: 'dotnet build'})"] = ActivityState.Executing
        };

        foreach (var pair in classifierCases)
        {
            var actual = CodexActivityMonitor.ClassifyToolCall("exec", pair.Key);
            var caseOk = actual == pair.Value;
            ok &= caseOk;
            lines.Add($"classifier {pair.Value}: actual={actual}, ok={caseOk}");
        }

        lines.Insert(0, ok ? "PASS" : "FAIL");
        var logPath = Path.Combine(Path.GetTempPath(), $"{BrandIdentity.TechnicalName}-self-test.txt");
        File.WriteAllLines(logPath, lines);
        return ok ? 0 : 1;
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
