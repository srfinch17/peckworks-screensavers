using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Christmas;

/// <summary>One page of a reindeer flip-book, with the three spots on it that other code needs (all in pixels from the page's CENTRE, already turned by the page's own pitch).</summary>
internal sealed class ReinPage
{
    public required Sprite Img { get; init; }
    public float NoseX, NoseY;      // the muzzle's tip: where Rudolph's red nose is stamped
    public float HarX, HarY;        // the harness ring at the shoulder: where a trace is tied on
    public int FeetY;               // the lowest painted row (the hooves), from the page centre: how far above the roof the centre rides when it stands
    public int[] RowFirst = [], RowLast = [];   // for each row, the first and last painted column: a stamp walks only that stretch (a reindeer is a quarter of its page)
}

/// <summary>One page of the sleigh with Santa in one of his poses, turned to one of the tilts (spots in pixels from the page's centre).</summary>
internal sealed class SleighPage
{
    public required Sprite Img { get; init; }
    public float HandX, HandY;      // his near hand: where the reins start
    public float HitchX, HitchY;    // the front scroll: where the traces are tied on
    public float RunnerX, RunnerY;  // the back of the runners: where the stardust comes from
    public float SeatX, SeatY;      // the middle of the seat, on the floor: where he stands up from
    public int[] RowFirst = [], RowLast = [];   // the painted stretch of each row (see ReinPage)
}

/// <summary>
/// ALL THE PICTURES OF SANTA'S NIGHT (painted once, by a helper thread, while the snow is
/// already falling): each reindeer's flying gallop, the same reindeer landing and
/// standing and pawing, the sleigh with Santa in each of his poses at each of its tilts,
/// and (for the story on the roof) Santa himself on foot.
///
/// WHY SO MANY SMALL SHEETS INSTEAD OF ONE BIG ONE: the old sleigh was one picture of the
/// whole team, four pages, so the legs could not be out of step and nothing could bend
/// with the path. Here every part is its own flip-book and the rig (SantaRig.cs) stamps
/// them one by one, each on its own beat, so the team ripples like a real one.
///
/// THE REINDEER (what a reindeer is, written down BEFORE drawing it, in units of "s",
/// the size unit of the whole rig, about 6 percent of the screen height):
///   - Body: 0.55 long chest to rump and 0.24 deep. Real reindeer are stocky, so the
///     body is a fat, rounded bean, not a racehorse's barrel.
///   - Legs: two bones of 0.18 each, hip to knee and knee to hoof, 0.36 in all (about
///     two thirds of the body length: reindeer are long in the leg). A FORE leg's knee
///     (really its wrist) points FORWARD when the leg folds; a HIND leg's hock points
///     BACKWARD. That one rule is what makes a folded leg read as a leg.
///   - Neck: short and deep, with a pale mane hanging under it. Head: a slightly big,
///     broad head with a pale muzzle, one round dark eye with a white glint, a leaf ear.
///   - Antlers: a main beam that sweeps up and back, then forward, with a brow tine, two
///     side tines and a forked tip; pale. The far antler is a shade darker.
///   - Harness: a red collar with a gold bell, a red girth strap, a ring at the shoulder
///     where the trace is tied.
///   - THE FLYING GALLOP (looked up first): a gallop has two moments. In the GATHER all
///     four legs come in under the body and the back is humped. In the STRETCH the fore
///     legs reach out forward, the hind legs trail out behind and the back is level or
///     hollow. Between them the legs sweep round like a loop, and the near and far leg
///     of a pair are a hair out of step. The whole beat is 16 pages.
///   - The landing: the same stretched legs, then the reach shortens page by page until
///     the legs hang straight: the "legs slowing from the gallop to a stand".
///   - Pawing: a reindeer scrapes the snow with a FORE hoof: lift it forward, set it
///     down and drag it back, three or four times, with the head low.
/// </summary>
internal sealed class SantaArt
{
    public const int FlyPages = 16, LandPages = 7, PawPages = 12;
    public const float PitchStep = 0.18f, TiltStep = 0.075f;
    public const int Pitches = 5, Tilts = 9;                // reindeer pitch steps (-2..2) and sleigh tilt steps (-4..4)
    public const int PoseCount = 8;                         // see SleighPose

    /// <summary>The sleigh's poses. The last two are for the story: Santa is out of the seat (with and without the sack).</summary>
    public enum SleighPose { Sit, WaveA, WaveB, Down, CrackUp, CrackDown, EmptyNoSack, EmptySack }

    public readonly float S;                                // the rig's size unit in pixels
    public readonly int ReinCanvas, SleighCanvas;
    public ReinPage[][] Fly = null!;                        // [pitch step 0..4][page]
    public ReinPage[] Land = null!;                         // [0] = full gallop reach ... [6] = standing
    public ReinPage[] Paw = null!;                          // one foreleg scrape cycle, head low
    public ReinPage[] Dip = null!;                          // standing, the head going down from level to the pawing height (and back up)
    public const int DipPages = 5;
    public SleighPage[][] Sleigh = null!;                   // [pose][tilt step 0..8]
    public SantaFigure Figure = null!;                      // Santa on foot, for the roof

    // ---- Colours: warm brown coat, cream belly and mane, a darker far side. Dark on dark is invisible, so the coat is a little light for a night sky. ----
    private static readonly Color Coat = Color.FromArgb(156, 110, 70), CoatFar = Color.FromArgb(110, 76, 52);
    private static readonly Color Cream = Color.FromArgb(232, 214, 184), CreamFar = Color.FromArgb(170, 150, 126);
    private static readonly Color LowLeg = Color.FromArgb(206, 178, 142), LowLegFar = Color.FromArgb(146, 122, 98);
    private static readonly Color Hoof = Color.FromArgb(52, 40, 38);
    private static readonly Color Antler = Color.FromArgb(228, 204, 158), AntlerFar = Color.FromArgb(170, 148, 112);
    private static readonly Color Red = Color.FromArgb(208, 36, 48), Gold = Color.FromArgb(255, 214, 98);
    private static readonly Color Ink = Color.FromArgb(30, 22, 24);

    public ReinPage[] Look = null!;                         // standing, the head turning back over the shoulder: level, thrown back, looking behind
    public const int LookPages = 3;
    public long Bytes;                                      // the pictures' memory, for the report
    public int RunnerFeet;                                  // how far below the sleigh page's centre its runners reach (measured off the pixels)

    /// <summary>
    /// "full" builds everything; otherwise only what a descent or a climb-out needs (the gallop at every pitch,
    /// the landing reach, the sleigh seated, waving and looking down), because the story steps the whole rig
    /// through a ladder of seven sizes as it flies toward the far cabin and back, and the middle rungs
    /// would cost memory for pictures nobody can see at that size. "figure" adds Santa on foot (the roof size only).
    /// </summary>
    public SantaArt(float s, bool full = true, bool figure = false)
    {
        S = s;
        ReinCanvas = (int)MathF.Ceiling(1.9f * s / 2) * 2;
        SleighCanvas = (int)MathF.Ceiling(2.5f * s / 2) * 2;

        Fly = new ReinPage[Pitches][];
        for (int pi = 0; pi < Pitches; pi++)
        {
            Fly[pi] = new ReinPage[FlyPages];
            for (int k = 0; k < FlyPages; k++)
                Fly[pi][k] = PaintReindeer(new RPose(Phase: k / (float)FlyPages, Amp: 1f, Bob: 0.02f * MathF.Sin(MathF.Tau * k / FlyPages), HeadUp: 0.10f * MathF.Sin(MathF.Tau * (k / (float)FlyPages - 0.15f)), Paw: -1f), (pi - 2) * PitchStep);
        }
        Land = new ReinPage[LandPages];
        for (int k = 0; k < LandPages; k++)
            Land[k] = PaintReindeer(new RPose(Phase: 0.5f, Amp: 1f - k / (float)(LandPages - 1), Bob: 0f, HeadUp: 0.10f * (1f - k / (float)(LandPages - 1)), Paw: -1f), 0f);
        Paw = []; Dip = []; Look = [];
        if (full)
        {
            Paw = new ReinPage[PawPages];
            for (int k = 0; k < PawPages; k++)
            {
                float p = k / (float)PawPages;
                // The head nods with the scrape: down as the hoof drags back, a little up as it is lifted.
                Paw[k] = PaintReindeer(new RPose(Phase: 0.5f, Amp: 0f, Bob: 0f, HeadUp: -0.45f + 0.18f * MathF.Sin(MathF.Tau * p), Paw: p), 0f);
            }
            Dip = new ReinPage[DipPages];
            for (int k = 0; k < DipPages; k++)
                Dip[k] = PaintReindeer(new RPose(Phase: 0.5f, Amp: 0f, Bob: 0f, HeadUp: -0.45f * k / (DipPages - 1), Paw: -1f), 0f);
            Look = new ReinPage[LookPages];
            for (int k = 0; k < LookPages; k++)
                Look[k] = PaintReindeer(new RPose(Phase: 0.5f, Amp: 0f, Bob: 0f, HeadUp: 0f, Paw: -1f, Turn: k / (float)(LookPages - 1)), 0f);
        }

        Sleigh = new SleighPage[PoseCount][];
        for (int po = 0; po < PoseCount; po++)
        {
            if (!full && po > (int)SleighPose.Down) continue;       // the middle rungs of the size ladder only ever show him seated, waving or looking down
            Sleigh[po] = new SleighPage[Tilts];
            for (int ti = 0; ti < Tilts; ti++)
                Sleigh[po][ti] = PaintSleigh((SleighPose)po, (ti - Tilts / 2) * TiltStep);
        }
        RunnerFeet = LowestRow(Sleigh[0][Tilts / 2].Img, SleighCanvas / 2);
        Figure = figure ? new SantaFigure(s) : null!;

        long bytes = 0;
        foreach (var row in Fly) foreach (var p in row) bytes += p.Img.Width * p.Img.Height * 4L;
        foreach (var p in Land) bytes += p.Img.Width * p.Img.Height * 4L;
        foreach (var p in Paw) bytes += p.Img.Width * p.Img.Height * 4L;
        foreach (var p in Dip) bytes += p.Img.Width * p.Img.Height * 4L;
        foreach (var p in Look) bytes += p.Img.Width * p.Img.Height * 4L;
        foreach (var row in Sleigh) if (row != null) foreach (var p in row) bytes += p.Img.Width * p.Img.Height * 4L;
        Bytes = bytes;
    }

    /// <summary>The ground a standing reindeer's centre rides above: measured off the pixels of the standing page.</summary>
    public int StandFeet => Land[LandPages - 1].FeetY;

    // ==================================================================
    //  THE REINDEER
    // ==================================================================

    internal readonly record struct RPose(float Phase, float Amp, float Bob, float HeadUp, float Paw, float Turn = 0f);

    private const float Leg1 = 0.18f, Leg2 = 0.18f;

    /// <summary>
    /// Two-bone leg: the knee that goes with a hip and a foot. The leg is two sticks of fixed
    /// length; when the foot is closer than their full reach the knee has to stick out
    /// sideways, and "bend" says which side (+1: toward the animal's front, a fore leg;
    /// -1: toward its back, a hind leg's hock). The foot is first pulled in if it is out of reach.
    /// </summary>
    private static PointF Knee(PointF hip, ref PointF foot, float bend)
    {
        float dx = foot.X - hip.X, dy = foot.Y - hip.Y;
        float reach = Leg1 + Leg2, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > reach * 0.999f) { float k = reach * 0.999f / d; dx *= k; dy *= k; d = reach * 0.999f; foot = new PointF(hip.X + dx, hip.Y + dy); }
        if (d < 0.05f) { d = 0.05f; dx = 0; dy = 0.05f; }
        float ux = dx / d, uy = dy / d;
        float half = d / 2f, h = MathF.Sqrt(MathF.Max(0f, Leg1 * Leg1 - half * half));
        // (uy, -ux) is the direction to the RIGHT of hip-to-foot as the picture is drawn: for a leg pointing down, that is toward the front.
        return new PointF(hip.X + ux * half + uy * h * bend, hip.Y + uy * half - ux * h * bend);
    }

    internal static Pen Stroke(Color c, float width, float s) => new(c, MathF.Max(width, 1.15f / s)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

    /// <summary>
    /// Paints one reindeer facing right, centred on the middle of its body, turned by "pitch"
    /// (nose down is positive) so a climb or a dive is a different page, not a rotated
    /// stamp. The anchors (nose, harness ring, hooves) are measured and handed back.
    /// </summary>
    private ReinPage PaintReindeer(RPose p, float pitch)
    {
        float s = S;
        int c = ReinCanvas;
        float bodyTilt = p.Amp * 0.05f * MathF.Cos(MathF.Tau * (p.Phase + 0.25f));   // the whole animal rocks a little with the beat
        float e0 = MathF.Pow(0.5f - 0.5f * MathF.Cos(MathF.Tau * p.Phase), 0.85f);    // 0 in the gather, 1 in the stretch
        float arch = p.Amp * (0.035f * (1 - e0) - 0.012f * e0);                      // back humped in the gather
        PointF noseAt = default, harAt = default;

        Sprite img = Sprite.Paint(c, c, g =>
        {
            g.TranslateTransform(c / 2f, c / 2f);
            g.RotateTransform(pitch * 57.29578f);
            g.ScaleTransform(s, s);
            g.TranslateTransform(0, p.Bob);
            g.RotateTransform(bodyTilt * 57.29578f);

            PointF Pt(float x, float y) => new(x, y);

            // ---- One leg, drawn as two round-ended strokes and a hoof ----
            void Leg(PointF hip, PointF foot, float bend, bool near)
            {
                PointF knee = Knee(hip, ref foot, bend);
                using (var up = Stroke(near ? Coat : CoatFar, 0.085f, s)) g.DrawLine(up, hip, knee);
                using (var lo = Stroke(near ? LowLeg : LowLegFar, 0.052f, s)) g.DrawLine(lo, knee, foot);
                using var hoof = new SolidBrush(Hoof);
                g.FillEllipse(hoof, foot.X - 0.034f, foot.Y - 0.022f, 0.068f, 0.05f);
            }
            PointF Foot(PointF hip, bool fore, float ph)
            {
                float e = MathF.Pow(0.5f - 0.5f * MathF.Cos(MathF.Tau * ph), 0.85f);
                float th = fore ? p.Amp * (-0.25f + 1.40f * e) : p.Amp * (0.45f - 1.75f * e);
                float tuck = fore ? 0.60f + 0.40f * e : 0.55f + 0.45f * e;
                float len = (0.96f - p.Amp * (0.96f - tuck)) * (Leg1 + Leg2);
                return new PointF(hip.X + len * MathF.Sin(th), hip.Y + len * MathF.Cos(th));
            }
            PointF foreHip = Pt(0.15f, 0.05f), hindHip = Pt(-0.20f, 0.05f);

            // The scrape: where the pawing fore hoof is, from a cycle position 0..1.
            PointF PawFoot(float q)
            {
                float stand = 0.96f * (Leg1 + Leg2);
                float x, lift;
                if (q < 0.5f) { float k = q / 0.5f; x = -0.06f + 0.30f * (0.5f - 0.5f * MathF.Cos(k * MathF.PI)); lift = 0.17f * MathF.Sin(k * MathF.PI * 0.5f); }
                else { float k = (q - 0.5f) / 0.5f; x = 0.24f - 0.30f * k; lift = 0.17f * MathF.Max(0f, 1f - 4f * k); }
                return new PointF(foreHip.X + x, foreHip.Y + stand - lift);
            }

            // Far legs first (a shade darker), then the body, then the near legs.
            Leg(hindHip, Foot(hindHip, false, p.Phase - 0.02f), -1f, false);
            Leg(foreHip, p.Paw >= 0 ? Foot(foreHip, true, 0.5f) : Foot(foreHip, true, p.Phase - 0.06f), +1f, false);

            // ---- The head, worked out now because the far antler and ear go behind it ----
            float turn = p.Turn;                                      // 0 head forward, 1 looking back over the shoulder
            float na = 0.85f + p.HeadUp + 0.45f * turn;               // neck angle above level (more upright when it looks back)
            PointF nb = Pt(0.16f, -0.05f);
            PointF nt = Pt(nb.X + 0.22f * MathF.Cos(na), nb.Y - 0.22f * MathF.Sin(na));
            float ha = turn == 0f ? na - 0.80f : (na - 0.80f) + ((MathF.PI - 0.28f) - (na - 0.80f)) * turn;   // the head hangs forward of the neck, or swings round to point behind
            // The head's own "up" must stay up: forward-facing it is the left of its direction, looking back it is the right
            // (the near side of the face is still the side we see).
            float hand = turn > 0.7f ? -1f : 1f;
            PointF dirH = Pt(MathF.Cos(ha), -MathF.Sin(ha)), upH = Pt(hand * dirH.Y, -hand * dirH.X);
            PointF hc = Pt(nt.X + dirH.X * 0.065f, nt.Y + dirH.Y * 0.065f);
            PointF H(float f, float u) => new(hc.X + dirH.X * f + upH.X * u, hc.Y + dirH.Y * f + upH.Y * u);

            // Far antler (behind everything on the head).
            void Antlers(Color col, float shift)
            {
                using var beam = Stroke(col, 0.030f, s);
                using var tine = Stroke(col, 0.022f, s);
                PointF[] b = [H(-0.03f + shift, 0.05f), H(-0.10f + shift, 0.16f), H(-0.09f + shift, 0.28f), H(-0.01f + shift, 0.36f), H(0.07f + shift, 0.40f)];
                g.DrawLines(beam, b);
                g.DrawLine(tine, H(0.02f + shift, 0.065f), H(0.09f + shift, 0.12f));                       // brow tine
                g.DrawLine(tine, H(-0.095f + shift, 0.17f), H(0.00f + shift, 0.19f));                      // first side tine
                g.DrawLine(tine, H(-0.09f + shift, 0.27f), H(-0.17f + shift, 0.31f));                      // second, pointing back
                g.DrawLine(tine, H(0.07f + shift, 0.40f), H(0.05f + shift, 0.47f));                        // the fork at the tip
                g.DrawLine(tine, H(0.07f + shift, 0.40f), H(0.13f + shift, 0.44f));
            }
            Antlers(AntlerFar, -0.045f);
            using (var farEar = new SolidBrush(CoatFar))
                g.FillClosedCurve(farEar, [H(-0.07f, 0.04f), H(-0.14f, 0.14f), H(-0.05f, 0.095f)], FillMode.Winding, 0.5f);

            // ---- The body: a fat bean, humped in the gather ----
            using (var coat = new SolidBrush(Coat))
                g.FillClosedCurve(coat, [Pt(0.27f, 0.00f), Pt(0.19f, -0.12f), Pt(-0.02f, -0.115f - arch), Pt(-0.22f, -0.10f - arch * 0.6f),
                                         Pt(-0.31f, -0.02f), Pt(-0.25f, 0.09f), Pt(-0.02f, 0.12f - arch * 0.4f), Pt(0.20f, 0.10f)], FillMode.Winding, 0.5f);
            using (var cream = new SolidBrush(Cream))
            {
                g.FillClosedCurve(cream, [Pt(0.17f, 0.075f), Pt(0.02f, 0.095f - arch * 0.4f), Pt(-0.17f, 0.085f), Pt(-0.02f, 0.125f - arch * 0.4f), Pt(0.14f, 0.115f)], FillMode.Winding, 0.5f);   // belly
                g.FillClosedCurve(cream, [Pt(-0.27f, -0.07f), Pt(-0.31f, -0.02f), Pt(-0.26f, 0.05f), Pt(-0.235f, -0.02f)], FillMode.Winding, 0.5f);   // the pale rump
                g.FillEllipse(cream, -0.355f, -0.095f, 0.09f, 0.06f);                                                                                   // the short white tail
            }

            // Near hind leg over the body.
            Leg(hindHip, Foot(hindHip, false, p.Phase + 0.04f), -1f, true);

            // ---- Neck: a thick round-ended stroke, with a pale mane hanging under it ----
            using (var neck = Stroke(Coat, 0.155f, s)) g.DrawLine(neck, Pt(nb.X - 0.02f, nb.Y + 0.02f), nt);
            PointF perp = Pt(MathF.Sin(na), MathF.Cos(na));           // points down and forward from the neck
            using (var mane = Stroke(Cream, 0.075f, s)) g.DrawLine(mane, Pt(nb.X + perp.X * 0.045f, nb.Y + 0.03f + perp.Y * 0.03f), Pt(nt.X + perp.X * 0.055f, nt.Y + perp.Y * 0.045f));

            // ---- Head ----
            void HeadOval(Brush b, float f, float u, float rx, float ry)
            {
                var st = g.Save();
                g.TranslateTransform(H(f, u).X, H(f, u).Y);
                g.RotateTransform(MathF.Atan2(dirH.Y, dirH.X) * 57.29578f);
                g.FillEllipse(b, -rx, -ry, rx * 2, ry * 2);
                g.Restore(st);
            }
            using (var coat = new SolidBrush(Coat)) HeadOval(coat, 0.0f, 0.0f, 0.115f, 0.072f);
            using (var cream = new SolidBrush(Color.FromArgb(222, 200, 168))) HeadOval(cream, 0.105f, -0.008f, 0.065f, 0.052f);   // the broad pale muzzle
            using (var nostril = new SolidBrush(Ink)) HeadOval(nostril, 0.158f, 0.0f, 0.020f, 0.016f);
            using (var ear = new SolidBrush(Coat))
                g.FillClosedCurve(ear, [H(-0.05f, 0.05f), H(-0.13f, 0.15f), H(-0.14f, 0.09f), H(-0.02f, 0.03f)], FillMode.Winding, 0.5f);
            using (var earIn = new SolidBrush(Color.FromArgb(214, 160, 140)))
                g.FillClosedCurve(earIn, [H(-0.065f, 0.055f), H(-0.115f, 0.12f), H(-0.05f, 0.07f)], FillMode.Winding, 0.5f);
            using (var eye = new SolidBrush(Ink)) HeadOval(eye, 0.035f, 0.022f, 0.024f, 0.026f);
            using (var glint = new SolidBrush(Color.White)) HeadOval(glint, 0.043f, 0.031f, 0.009f, 0.009f);
            Antlers(Antler, 0f);

            // ---- Harness: collar, bell, girth strap, the ring the trace is tied to ----
            PointF cc = Pt(nb.X + (nt.X - nb.X) * 0.30f, nb.Y + (nt.Y - nb.Y) * 0.30f);
            using (var collar = Stroke(Red, 0.05f, s)) g.DrawLine(collar, Pt(cc.X - perp.X * 0.075f, cc.Y - perp.Y * 0.075f), Pt(cc.X + perp.X * 0.085f, cc.Y + perp.Y * 0.085f));
            using (var strap = Stroke(Red, 0.03f, s))
            {
                g.DrawLine(strap, Pt(cc.X + perp.X * 0.07f, cc.Y + perp.Y * 0.07f), Pt(0.13f, 0.11f));
                g.DrawLine(strap, Pt(0.05f, -0.115f - arch), Pt(0.03f, 0.115f - arch * 0.4f));
            }
            using (var bell = new SolidBrush(Gold)) g.FillEllipse(bell, cc.X + perp.X * 0.085f - 0.034f, cc.Y + perp.Y * 0.085f - 0.03f, 0.068f, 0.068f);
            using (var shine = new SolidBrush(Color.FromArgb(255, 255, 236))) g.FillEllipse(shine, cc.X + perp.X * 0.085f - 0.014f, cc.Y + perp.Y * 0.085f - 0.018f, 0.022f, 0.022f);
            using (var ring = new SolidBrush(Gold)) g.FillEllipse(ring, 0.085f, -0.035f, 0.04f, 0.04f);

            // Near fore leg over everything (it is the one that paws).
            PointF nearFore = p.Paw >= 0 ? PawFoot(p.Paw) : Foot(foreHip, true, p.Phase + 0.02f);
            Leg(foreHip, nearFore, +1f, true);

            // ---- Anchors, worked out here where the local numbers are known ----
            PointF nose = Pt(hc.X + dirH.X * 0.168f, hc.Y + dirH.Y * 0.168f);
            noseAt = nose;
            harAt = Pt(0.105f, -0.015f);
        });

        // Turn a local point into pixels from the centre: tilt, bob, scale, pitch (the same order the painting used).
        PointF ToPx(PointF a)
        {
            float ct = MathF.Cos(bodyTilt), st = MathF.Sin(bodyTilt);
            float x = a.X * ct - a.Y * st, y = a.X * st + a.Y * ct + p.Bob;
            x *= s; y *= s;
            float cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
            return new PointF(x * cp - y * sp, x * sp + y * cp);
        }
        PointF n = ToPx(noseAt), h = ToPx(harAt);
        Spans(img, out int[] first, out int[] last);
        return new ReinPage { Img = img, NoseX = n.X, NoseY = n.Y, HarX = h.X, HarY = h.Y, FeetY = LowestRow(img, c / 2), RowFirst = first, RowLast = last };
    }

    /// <summary>For each row of a picture, the first and last column that holds any paint (-1 / width when the row is empty).</summary>
    internal static void Spans(Sprite img, out int[] first, out int[] last)
    {
        first = new int[img.Height]; last = new int[img.Height];
        for (int y = 0; y < img.Height; y++)
        {
            int f = img.Width, l = -1;
            for (int x = 0; x < img.Width; x++)
                if ((img.PixelAt(x, y) >> 24) != 0) { if (f == img.Width) f = x; l = x; }
            first[y] = f; last[y] = l;
        }
    }

    /// <summary>The lowest row that holds real paint (more than a third solid), counted from the page's centre row.</summary>
    internal static int LowestRow(Sprite img, int centreRow)
    {
        for (int y = img.Height - 1; y >= 0; y--)
            for (int x = 0; x < img.Width; x++)
                if ((img.PixelAt(x, y) >> 24) > 90) return y - centreRow;
        return 0;
    }

    // ==================================================================
    //  THE SLEIGH AND SANTA IN IT
    // ==================================================================

    /// <summary>
    /// The sleigh, facing right, with Santa seated in one of his poses, turned to "tilt"
    /// (nose down is positive). Everything is in units of s with (0, 0) the middle of the
    /// sleigh's body; Santa sits BEHIND the sleigh's near side, so the body hides his legs.
    /// </summary>
    private SleighPage PaintSleigh(SleighPose pose, float tilt)
    {
        float s = S;
        int c = SleighCanvas;
        bool santa = pose != SleighPose.EmptyNoSack && pose != SleighPose.EmptySack;
        bool sack = pose != SleighPose.EmptyNoSack;
        PointF hand = new(0.34f, -0.20f);                           // where his near hand is (poses move it)
        PointF elbow = new(0.12f, -0.20f);
        bool open = false;                                          // mouth open ("Ho ho ho!")
        float lookDown = 0f;
        switch (pose)
        {
            case SleighPose.WaveA: elbow = new PointF(0.20f, -0.48f); hand = new PointF(0.30f, -0.78f); open = true; break;
            case SleighPose.WaveB: elbow = new PointF(0.20f, -0.48f); hand = new PointF(0.10f, -0.78f); open = true; break;
            case SleighPose.Down: lookDown = 1f; break;
            case SleighPose.CrackUp: elbow = new PointF(0.22f, -0.46f); hand = new PointF(0.40f, -0.76f); open = true; break;
            case SleighPose.CrackDown: elbow = new PointF(0.20f, -0.32f); hand = new PointF(0.54f, -0.30f); open = true; break;
        }

        Sprite img = Sprite.Paint(c, c, g =>
        {
            g.TranslateTransform(c / 2f, c / 2f);
            g.RotateTransform(tilt * 57.29578f);
            g.ScaleTransform(s, s);
            PointF Pt(float x, float y) => new(x, y);

            // ---- Runners: gold rails that curl up at the front and a little at the back ----
            using (var gold = Stroke(Color.FromArgb(240, 200, 100), 0.042f, s))
            {
                g.DrawLine(gold, Pt(-0.58f, 0.50f), Pt(0.50f, 0.50f));
                g.DrawBezier(gold, Pt(0.50f, 0.50f), Pt(0.76f, 0.50f), Pt(0.82f, 0.30f), Pt(0.68f, 0.22f));
                g.DrawBezier(gold, Pt(-0.58f, 0.50f), Pt(-0.70f, 0.50f), Pt(-0.72f, 0.42f), Pt(-0.67f, 0.39f));
            }
            using (var post = Stroke(Color.FromArgb(226, 184, 92), 0.03f, s))
            {
                g.DrawLine(post, Pt(-0.36f, 0.30f), Pt(-0.36f, 0.50f));
                g.DrawLine(post, Pt(0.30f, 0.30f), Pt(0.30f, 0.50f));
            }

            // ---- The sack of presents behind the seat ----
            if (sack)
            {
                using var bag = new SolidBrush(Color.FromArgb(140, 100, 62));
                using var bagLight = new SolidBrush(Color.FromArgb(172, 128, 84));
                g.FillClosedCurve(bag, [Pt(-0.42f, -0.52f), Pt(-0.58f, -0.34f), Pt(-0.62f, -0.10f), Pt(-0.46f, 0.02f), Pt(-0.26f, -0.04f), Pt(-0.20f, -0.26f), Pt(-0.30f, -0.46f)], FillMode.Winding, 0.5f);
                g.FillEllipse(bagLight, -0.50f, -0.36f, 0.14f, 0.20f);
                using var tie = Stroke(Gold, 0.035f, s);
                g.DrawLine(tie, Pt(-0.50f, -0.47f), Pt(-0.33f, -0.46f));
                using var box = new SolidBrush(Red);
                var st = g.Save();
                g.TranslateTransform(-0.36f, -0.54f); g.RotateTransform(-14f);
                g.FillRectangle(box, -0.05f, -0.05f, 0.10f, 0.09f);
                using (var rib = new SolidBrush(Gold)) { g.FillRectangle(rib, -0.05f, -0.012f, 0.10f, 0.024f); g.FillRectangle(rib, -0.012f, -0.05f, 0.024f, 0.09f); }
                g.Restore(st);
                using var cane = Stroke(Color.White, 0.025f, s);
                g.DrawBezier(cane, Pt(-0.46f, -0.50f), Pt(-0.47f, -0.62f), Pt(-0.54f, -0.64f), Pt(-0.55f, -0.58f));
            }

            // ---- Santa, seated ----
            if (santa)
            {
                using var coat = new SolidBrush(Color.FromArgb(214, 38, 50));
                using var coatDark = new SolidBrush(Color.FromArgb(160, 26, 40));
                // The far arm, behind the body, holding the reins forward.
                using (var farArm = Stroke(Color.FromArgb(160, 26, 40), 0.078f, s)) g.DrawLine(farArm, Pt(0.06f, -0.32f), Pt(0.32f, -0.22f));
                g.FillEllipse(coat, -0.19f, -0.46f, 0.36f, 0.50f);                                  // torso
                using (var belt = Stroke(Ink, 0.05f, s)) g.DrawLine(belt, Pt(-0.16f, -0.07f), Pt(0.14f, -0.07f));
                using (var buckle = new SolidBrush(Gold)) g.FillRectangle(buckle, -0.03f, -0.095f, 0.055f, 0.05f);

                // Head, beard, hat (the hat's tail streams back in the wind).
                float hx = 0.06f + 0.03f * lookDown, hy = -0.56f + 0.035f * lookDown;
                // A slightly big head, a clear white beard, a red hat with a fat pom-pom: it must still read at 40 pixels.
                using (var skin = new SolidBrush(Color.FromArgb(255, 214, 188))) g.FillEllipse(skin, hx - 0.125f, hy - 0.125f, 0.25f, 0.25f);
                using (var cheek = new SolidBrush(Color.FromArgb(130, 255, 130, 130))) g.FillEllipse(cheek, hx + 0.03f, hy + 0.005f, 0.085f, 0.055f);
                using (var white = new SolidBrush(Color.FromArgb(252, 252, 255)))
                {
                    g.FillClosedCurve(white, [Pt(hx - 0.11f, hy - 0.005f), Pt(hx - 0.09f, hy + 0.16f), Pt(hx + 0.03f, hy + 0.25f + 0.03f * lookDown), Pt(hx + 0.16f, hy + 0.15f), Pt(hx + 0.14f, hy + 0.02f)], FillMode.Winding, 0.5f);
                    g.FillEllipse(white, hx + 0.03f, hy + 0.05f, 0.16f, 0.07f);                            // moustache
                }
                using (var nose = new SolidBrush(Color.FromArgb(246, 150, 140))) g.FillEllipse(nose, hx + 0.12f, hy - 0.005f, 0.06f, 0.055f);
                using (var eye = new SolidBrush(Ink)) g.FillEllipse(eye, hx + 0.05f, hy - 0.05f + 0.02f * lookDown, 0.036f, 0.04f);
                using (var glint = new SolidBrush(Color.White)) g.FillEllipse(glint, hx + 0.062f, hy - 0.044f + 0.02f * lookDown, 0.013f, 0.013f);
                using (var brow = Stroke(Color.White, 0.025f, s)) g.DrawLine(brow, Pt(hx + 0.035f, hy - 0.075f + 0.01f * lookDown), Pt(hx + 0.10f, hy - 0.08f));
                if (open) using (var mouth = new SolidBrush(Color.FromArgb(120, 30, 40))) g.FillEllipse(mouth, hx + 0.085f, hy + 0.10f, 0.065f, 0.06f);
                using (var hat = new SolidBrush(Color.FromArgb(214, 38, 50)))
                {
                    g.FillClosedCurve(hat, [Pt(hx - 0.125f, hy - 0.085f), Pt(hx - 0.02f, hy - 0.23f), Pt(hx - 0.20f, hy - 0.21f), Pt(hx - 0.38f, hy - 0.05f), Pt(hx - 0.22f, hy - 0.15f), Pt(hx + 0.08f, hy - 0.10f), Pt(hx + 0.125f, hy - 0.08f)], FillMode.Winding, 0.4f);
                    using var pom = new SolidBrush(Color.White);
                    g.FillEllipse(pom, hx - 0.46f, hy - 0.11f, 0.13f, 0.13f);
                }
                using (var brim = Stroke(Color.White, 0.075f, s)) g.DrawLine(brim, Pt(hx - 0.11f, hy - 0.09f), Pt(hx + 0.135f, hy - 0.085f));
            }
            else
            {
                using var cushion = new SolidBrush(Color.FromArgb(160, 24, 38));
                g.FillEllipse(cushion, -0.22f, -0.07f, 0.44f, 0.12f);
            }

            // ---- The sleigh's body, in front of Santa: a curved red shape that scrolls up at the front ----
            using (var body = new SolidBrush(Color.FromArgb(206, 34, 46)))
                g.FillClosedCurve(body, [Pt(-0.60f, -0.16f), Pt(-0.64f, 0.08f), Pt(-0.50f, 0.30f), Pt(-0.20f, 0.37f), Pt(0.30f, 0.37f), Pt(0.58f, 0.30f),
                                         Pt(0.74f, 0.10f), Pt(0.80f, -0.10f), Pt(0.70f, -0.20f), Pt(0.62f, -0.04f), Pt(0.48f, 0.06f), Pt(0.20f, 0.02f), Pt(-0.30f, 0.0f)], FillMode.Winding, 0.4f);
            using (var shade = new SolidBrush(Color.FromArgb(70, 120, 10, 30)))
                g.FillClosedCurve(shade, [Pt(-0.50f, 0.20f), Pt(-0.20f, 0.30f), Pt(0.30f, 0.30f), Pt(0.55f, 0.22f), Pt(0.25f, 0.34f), Pt(-0.20f, 0.355f)], FillMode.Winding, 0.4f);
            using (var trim = Stroke(Gold, 0.028f, s)) g.DrawBezier(trim, Pt(-0.56f, 0.10f), Pt(-0.30f, 0.20f), Pt(0.20f, 0.20f), Pt(0.56f, 0.08f));
            using (var gleam = Stroke(Color.FromArgb(150, 255, 150, 150), 0.02f, s)) g.DrawBezier(gleam, Pt(-0.52f, -0.06f), Pt(-0.40f, -0.02f), Pt(-0.30f, -0.02f), Pt(-0.20f, -0.02f));

            // ---- Santa's near arm, in front of the sleigh ----
            if (santa)
            {
                using (var arm = Stroke(Color.FromArgb(214, 38, 50), 0.082f, s)) g.DrawLines(arm, [Pt(-0.04f, -0.33f), elbow, hand]);
                using (var cuff = Stroke(Color.White, 0.04f, s)) g.DrawLine(cuff, Pt(hand.X - (hand.X - elbow.X) * 0.12f, hand.Y - (hand.Y - elbow.Y) * 0.12f), hand);
                using (var mitten = new SolidBrush(Ink)) g.FillEllipse(mitten, hand.X - 0.045f, hand.Y - 0.045f, 0.09f, 0.09f);
            }
        });

        PointF ToPx(float x, float y)
        {
            float ct = MathF.Cos(tilt), st = MathF.Sin(tilt);
            return new PointF((x * ct - y * st) * s, (x * st + y * ct) * s);
        }
        PointF hd = ToPx(hand.X, hand.Y), hi = ToPx(0.78f, -0.04f), ru = ToPx(-0.66f, 0.50f), se = ToPx(0.0f, 0.02f);
        Spans(img, out int[] first, out int[] last);
        return new SleighPage { Img = img, HandX = hd.X, HandY = hd.Y, HitchX = hi.X, HitchY = hi.Y, RunnerX = ru.X, RunnerY = ru.Y, SeatX = se.X, SeatY = se.Y, RowFirst = first, RowLast = last };
    }

    // ==================================================================
    //  THE POSE SHEET (PECKWORKS_SHEET=santa;file.png): every page of every flip-book
    // ==================================================================

    /// <summary>
    /// Writes every page of every flip-book to one labelled picture on the snow colour, and the
    /// same pages on night blue beside it (file_night.png). A reindeer is judged HERE first:
    /// would a child name it from one page, with no caption?
    /// </summary>
    public void WriteSheet(string path)
    {
        var rows = new List<(string Name, List<(Sprite Img, int Cx, int Cy)> Pages)>();
        int rc = ReinCanvas / 2, sc = SleighCanvas / 2;
        void Add(string name, IEnumerable<(Sprite, int, int)> pages) => rows.Add((name, [.. pages]));
        for (int pi = 0; pi < Pitches; pi += 2) Add($"fly pitch {pi - 2}", Fly[pi].Select(p => (p.Img, rc, rc)));
        Add("land (reach to stand)", Land.Select(p => (p.Img, rc, rc)));
        Add("paw", Paw.Select(p => (p.Img, rc, rc)));
        Add("dip (head down to paw height)", Dip.Select(p => (p.Img, rc, rc)));
        Add("sleigh poses, tilt 0", Sleigh.Select(p => (p[Tilts / 2].Img, sc, sc)));
        Add("sleigh tilts, pose 0", Sleigh[0].Select(p => (p.Img, sc, sc)));
        Figure.AddSheetRows(rows);

        int cell = SleighCanvas + 4, perRow = Math.Max(1, 1800 / cell);
        int total = 0;
        foreach (var r in rows) total += (r.Pages.Count + perRow - 1) / perRow;
        int width = perRow * cell, height = total * cell + rows.Count * 18 + 8;
        foreach (bool night in new[] { false, true })
        {
            var fb = new FrameBuffer(width, height);
            uint bg = night ? FrameBuffer.Rgb(20, 36, 92) : FrameBuffer.Rgb(222, 233, 252);
            Array.Fill(fb.Pixels, bg);
            int y = 0;
            var labelList = new List<(string, int)>();
            foreach (var r in rows)
            {
                labelList.Add((r.Name, y));
                y += 18;
                for (int i = 0; i < r.Pages.Count; i++)
                {
                    if (i > 0 && i % perRow == 0) y += cell;
                    var (img, cx, cy) = r.Pages[i];
                    img.Draw(fb, (i % perRow) * cell + cell / 2 - cx, y + cell / 2 - cy);
                }
                y += cell;
            }
            using var bmp = fb.ToBitmap();
            using (var g = Graphics.FromImage(bmp))
            using (var f = new Font("Segoe UI", 9f))
            using (var br = new SolidBrush(night ? Color.White : Color.Black))
                foreach (var (name, ly) in labelList) g.DrawString(name, f, br, 2, ly);
            string file = night ? Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_night.png") : path;
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
