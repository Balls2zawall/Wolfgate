using System.Numerics;
using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared._WF.PlanetCracker.Cracker.BUI;
using Robust.Client.Graphics;

namespace Content.Client._WF.PlanetCracker.Cracker;

/// <summary>
/// The nine crack stages as one strip: the stage the hull is in lit, the ones behind it dimmed, the ones ahead in
/// glass, and a fill bar under the Cracking segment showing how far through the cut the hull is. The bar blinks while
/// a damaged anchor holds the cut.
/// </summary>
public sealed class WFCrackTimeline : WFDiagramControl
{
    /// <summary>The nine stages, in order; one segment each.</summary>
    private static readonly WFCrackState[] Stages =
    {
        WFCrackState.Idle,
        WFCrackState.Surveying,
        WFCrackState.AnchorsPlaced,
        WFCrackState.AnchorsLocked,
        WFCrackState.Cracking,
        WFCrackState.Cracked,
        WFCrackState.Disconnecting,
        WFCrackState.Released,
        WFCrackState.Falling,
    };

    /// <summary>Gap between segments, in pixels.</summary>
    private const float Gap = 2f;

    /// <summary>Padding around the strip, in pixels.</summary>
    private const float Pad = 4f;

    /// <summary>Height of a segment bar, in pixels.</summary>
    private const float SegmentHeight = 10f;

    /// <summary>Height of the progress bar drawn under the Cracking segment, in pixels.</summary>
    private const float ProgressHeight = 5f;

    /// <summary>Blink period of the paused progress bar, in seconds.</summary>
    private const float PausedBlinkPeriod = 0.7f;

    private WFCrackConsoleState? _state;

    /// <summary>The state the strip draws; null until the console has pushed one.</summary>
    public void SetState(WFCrackConsoleState? state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Vector2(0f, Pad * 2f + SegmentHeight + ProgressHeight + FontSize * 2f);
    }

    /// <inheritdoc/>
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        RefreshSkin();

        handle.DrawRect(PixelSizeBox, Geom(Skin.Ink));

        var box = PixelSizeBox;

        if (box.Width <= 0 || box.Height <= 0)
            return;

        var current = _state?.State ?? WFCrackState.Idle;
        var currentIndex = Array.IndexOf(Stages, current);
        var width = (box.Width - Pad * 2f - Gap * (Stages.Length - 1)) / Stages.Length;

        if (width <= 0f)
            return;

        var top = Pad + FontSize + 2f;

        for (var i = 0; i < Stages.Length; i++)
        {
            var left = Pad + (width + Gap) * i;
            var bar = new UIBox2(left, top, left + width, top + SegmentHeight);

            var colour = i == currentIndex ? Skin.Accent
                : i < currentIndex ? Skin.AccentDim
                : Skin.Glass;

            // DrawRect is a linear-target call.
            handle.DrawRect(bar, Geom(colour));

            var label = Loc.GetString(StageKey(Stages[i]));
            var size = handle.GetDimensions(Font, label, 1f);
            var textColour = i == currentIndex ? Skin.Text : Skin.TextMuted;

            handle.DrawString(Font, new Vector2(left + (width - size.X) / 2f, Pad), label, textColour);

            if (i != currentIndex || Stages[i] != WFCrackState.Cracking)
                continue;

            DrawProgress(handle, bar);
        }
    }

    /// <summary>The cut's own fill bar, under the Cracking segment.</summary>
    private void DrawProgress(DrawingHandleScreen handle, UIBox2 segment)
    {
        if (_state is not { } state)
            return;

        var total = (float)state.CrackTotal.TotalSeconds;
        var fraction = total > 0f
            ? Math.Clamp(1f - (float)state.CrackRemaining.TotalSeconds / total, 0f, 1f)
            : 0f;

        var track = new UIBox2(segment.Left, segment.Bottom + 1f, segment.Right, segment.Bottom + 1f + ProgressHeight);

        handle.DrawRect(track, Geom(Skin.Glass));

        if (fraction <= 0f)
            return;

        var colour = state.CrackPaused
            ? Blink(PausedBlinkPeriod) ? Skin.Caution : Skin.Glass
            : Skin.Good;

        handle.DrawRect(new UIBox2(track.Left, track.Top, track.Left + track.Width * fraction, track.Bottom),
            Geom(colour));
    }

    /// <summary>Locale key naming one stage; the strip never shows a server-sent string.</summary>
    private static string StageKey(WFCrackState state)
    {
        return state switch
        {
            WFCrackState.Idle => "wf-crack-console-state-idle",
            WFCrackState.Surveying => "wf-crack-console-state-surveying",
            WFCrackState.AnchorsPlaced => "wf-crack-console-state-anchors-placed",
            WFCrackState.AnchorsLocked => "wf-crack-console-state-anchors-locked",
            WFCrackState.Cracking => "wf-crack-console-state-cracking",
            WFCrackState.Cracked => "wf-crack-console-state-cracked",
            WFCrackState.Disconnecting => "wf-crack-console-state-disconnecting",
            WFCrackState.Released => "wf-crack-console-state-released",
            WFCrackState.Falling => "wf-crack-console-state-falling",
            _ => "wf-crack-console-state-idle",
        };
    }
}
