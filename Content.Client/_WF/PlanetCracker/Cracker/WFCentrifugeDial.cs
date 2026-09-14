using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Client._WF.PlanetCracker.Cracker;

/// <summary>
/// The gravitic centrifuge as a face dial: a static ring, a rotor that actually turns at the spin it is reading, the
/// spin percentage, an AT FULL pip and a load-against-capacity bar.
/// It takes spin, at-full, load and capacity as plain values so the crack console (which reads them out of
/// WFCrackConsoleState) and the machine's own window (which reads the networked WFCentrifugeComponent) feed one dial
/// rather than two subtly different ones.
/// </summary>
public sealed class WFCentrifugeDial : WFDiagramControl
{
    /// <summary>Spokes on the rotor.</summary>
    private const int Spokes = 6;

    /// <summary>Vertices in the face ring; fixed, which is why it is a cached line strip and not DrawCircle.</summary>
    private const int FaceSegments = 96;

    /// <summary>Revolutions per second the rotor turns at full spin.</summary>
    private const float MaxRps = 1.6f;

    /// <summary>Fraction of the face radius the rotor hub sits at.</summary>
    private const float HubFraction = 0.12f;

    /// <summary>Fraction of the face radius the spokes reach.</summary>
    private const float SpokeFraction = 0.78f;

    /// <summary>Fraction of the control's smaller side the face radius takes.</summary>
    private const float FaceFraction = 0.34f;

    /// <summary>Height of the load bar, in pixels.</summary>
    private const float BarHeight = 8f;

    /// <summary>Padding around the readouts, in pixels.</summary>
    private const float Pad = 4f;

    /// <summary>Radius of the AT FULL pip, in pixels.</summary>
    private const float PipRadius = 4f;

    /// <summary>Load fraction above which the bar reads Caution.</summary>
    private const float LoadCaution = 0.75f;

    /// <summary>Load fraction above which the bar reads Danger.</summary>
    private const float LoadDanger = 0.95f;

    /// <summary>Blink period of the AT FULL pip while the rotor is still climbing, in seconds.</summary>
    private const float PipBlinkPeriod = 0.8f;

    /// <summary>Unit-space spoke endpoints, built once; each Draw rotates them by the accumulated phase.</summary>
    private static readonly Vector2[] SpokeUnits = BuildSpokes();

    /// <summary>Rotor angle in radians, accumulated in FrameUpdate so the rotor keeps turning between states.</summary>
    private float _phase;

    private Ring _face;

    /// <summary>Line buffer for the rotor, cleared and refilled each Draw.</summary>
    private ValueList<Vector2> _rotorLines;

    /// <summary>Rotor spin as a fraction of full, 0 to 1.</summary>
    public float Spin { get; private set; }

    /// <summary>True once the rotor counts as at full, with the design hysteresis already applied server-side.</summary>
    public bool AtFull { get; private set; }

    /// <summary>Mass the hull's pooled gravgens are carrying.</summary>
    public float Load { get; private set; }

    /// <summary>Mass the hull's pooled gravgens can carry.</summary>
    public float Capacity { get; private set; }

    /// <summary>Feeds the dial one reading. Both hosts call this with the same four values.</summary>
    public void SetReadout(float spin, bool atFull, float load, float capacity)
    {
        Spin = Math.Clamp(spin, 0f, 1f);
        AtFull = atFull;
        Load = load;
        Capacity = capacity;
    }

    /// <inheritdoc/>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Accumulated, not derived from the clock, so the rotor never snaps when the spin changes.
        _phase = (_phase + args.DeltaSeconds * Spin * MaxRps * MathF.Tau) % MathF.Tau;
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

        var radius = MathF.Min(box.Width, box.Height) * FaceFraction;
        var centre = new Vector2(box.Width / 2f, box.Height * 0.42f);

        EnsureRing(ref _face, centre, radius, FaceSegments);
        DrawRing(handle, ref _face, AtFull ? Skin.Good : Skin.AccentDim);

        DrawRotor(handle, centre, radius);

        // DrawString takes the raw skin colour so it matches the stock Labels around the dial.
        var spinText = Loc.GetString("wf-centrifuge-spin", ("percent", (int)MathF.Round(Spin * 100f)));
        var spinSize = handle.GetDimensions(Font, spinText, 1f);
        handle.DrawString(Font, new Vector2((box.Width - spinSize.X) / 2f, Pad), spinText, Skin.Text);

        DrawPip(handle, box);
        DrawLoadBar(handle, box);
    }

    /// <summary>The hub and its spokes, rotated by the accumulated phase.</summary>
    private void DrawRotor(DrawingHandleScreen handle, Vector2 centre, float radius)
    {
        _rotorLines.Clear();

        var sin = MathF.Sin(_phase);
        var cos = MathF.Cos(_phase);

        foreach (var unit in SpokeUnits)
        {
            var turned = new Vector2(unit.X * cos - unit.Y * sin, unit.X * sin + unit.Y * cos);
            AddLine(ref _rotorLines, centre + turned * radius * HubFraction, centre + turned * radius * SpokeFraction);
        }

        Flush(handle, ref _rotorLines, AtFull ? Skin.Accent : Skin.AccentDim);

        // Filled DrawCircle converts internally, so the hub takes the raw skin colour.
        handle.DrawCircle(centre, radius * HubFraction, Skin.EdgeLight, true);
    }

    /// <summary>The AT FULL pip: lit and steady at full, blinking dim while the rotor climbs.</summary>
    private void DrawPip(DrawingHandleScreen handle, UIBox2i box)
    {
        var text = Loc.GetString("wf-centrifuge-at-full");
        var size = handle.GetDimensions(Font, text, 1f);
        var y = box.Height - BarHeight - Pad * 3f - size.Y;
        var x = (box.Width - size.X - PipRadius * 3f) / 2f;

        var lit = AtFull || Blink(PipBlinkPeriod);
        var colour = AtFull ? Skin.Good : Skin.TextMuted;

        if (lit)
            handle.DrawCircle(new Vector2(x + PipRadius, y + size.Y / 2f), PipRadius, colour, true);

        handle.DrawString(Font, new Vector2(x + PipRadius * 3f, y), text, colour);
    }

    /// <summary>Load against capacity, with the Good/Caution/Danger bands.</summary>
    private void DrawLoadBar(DrawingHandleScreen handle, UIBox2i box)
    {
        var fraction = Capacity > 0f ? Math.Clamp(Load / Capacity, 0f, 1f) : 0f;

        var colour = fraction switch
        {
            >= LoadDanger => Skin.Danger,
            >= LoadCaution => Skin.Caution,
            _ => Skin.Good,
        };

        var top = box.Height - BarHeight - Pad;
        var track = new UIBox2(Pad, top, box.Width - Pad, top + BarHeight);

        // DrawRect writes straight into Vertex2D.Modulate, which is linear, so both rects take the converted colour.
        handle.DrawRect(track, Geom(Skin.Glass));

        if (fraction > 0f)
            handle.DrawRect(new UIBox2(track.Left, track.Top, track.Left + track.Width * fraction, track.Bottom), Geom(colour));

        handle.DrawRect(track, Geom(Skin.Edge), false);

        var text = Loc.GetString("wf-centrifuge-load",
            ("mass", (int)MathF.Round(Load)),
            ("capacity", (int)MathF.Round(Capacity)));

        var size = handle.GetDimensions(Font, text, 1f);
        handle.DrawString(Font, new Vector2((box.Width - size.X) / 2f, top - size.Y - 1f), text, Skin.TextMuted);
    }

    /// <summary>Unit-space spoke directions, evenly spaced; the phase does the turning.</summary>
    private static Vector2[] BuildSpokes()
    {
        var spokes = new Vector2[Spokes];

        for (var i = 0; i < Spokes; i++)
        {
            var angle = MathF.Tau * i / Spokes;
            spokes[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return spokes;
    }
}
