using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace SakuraPond;

// ============================================================================
// THE DUCKS: a duck, or a mother with a line of ducklings, now and then swims
// across the pond. This file holds, top to bottom:
//   Ducks      the visitor: when a family comes, which species, the route, and
//              the director of the cute things (a lagging duckling, a ride on
//              the mother's back ...). The scene only ever talks to this one.
//   DuckWorld  the pond as a duck sees it (what a duck must go round).
//   Style, Look, Species, Roster   what each kind of duck looks like.
//   Duck       one grown duck: a Body with a mind (Cruise, Look, Dabble ...).
//   Duckling   one fluff ball.
// ============================================================================

/// <summary>The seven grown-up looks: each species has a drake and a hen (the spot-billed duck looks the same either way).</summary>
internal enum Style { Karugamo, MallardDrake, MallardHen, MandarinDrake, MandarinHen, TealDrake, TealHen }

/// <summary>
/// What one kind of grown duck looks like: the baked colours along and across
/// its back (the same trick the turtle's shell uses: work out the colour of
/// every (s, v) once, look it up while painting), and two pictures of its open
/// wings for the flap.
///
/// FEYNMAN VERSION: s is how far from the bill tip (0) to the tail tip (1), and
/// v is how far across (-1 the left edge, +1 the right). The colours are stuck
/// to (s, v) the way a pattern is printed on cloth, so they ride along when the
/// duck turns its neck.
/// </summary>
internal sealed class Look
{
    public const int SkinLen = 192, SkinWide = 48;

    public readonly Style Style;
    public readonly uint[] Skin;
    public readonly TurningStamp WingOut, WingUp;      // the wings flung wide (down stroke) and raised and swept forward (up stroke)

    public Look(Style style, float lenPx)
    {
        Style = style;
        Skin = Roster.BakeSkin(style);
        (Color main, Color band, Color edge) = Roster.WingColours(style);
        WingOut = Roster.MakeWing(lenPx, main, band, edge, reach: 0.80f, sweep: 0.55f);
        WingUp = Roster.MakeWing(lenPx, main, band, edge, reach: 0.52f, sweep: 0.05f);
    }
}

/// <summary>A species: how big, and what the drake, the hen and the ducklings look like.</summary>
internal sealed class Species
{
    public string Name = "";
    public float Len;            // bill to tail, in u
    public float KidLen;         // a duckling, in u
    public Look Drake = null!, Hen = null!;
    public uint[] KidSkin = null!;
}

/// <summary>
/// The four species, built once: eastern spot-billed duck (karugamo, the duck
/// of Japanese ponds), mallard (magamo), mandarin (oshidori) and common teal
/// (kogamo). Everything about colour lives here, as numbers, so the look of a
/// duck is a table and not code.
/// </summary>
internal sealed class Roster
{
    public readonly Species[] All;

    public Roster(float u)
    {
        var karugamo = new Species { Name = "karugamo", Len = 0.12f, KidLen = 0.040f };
        karugamo.Hen = karugamo.Drake = new Look(Style.Karugamo, karugamo.Len * u);
        karugamo.KidSkin = BakeKid(Style.Karugamo);
        var mallard = new Species { Name = "mallard", Len = 0.12f, KidLen = 0.040f };
        mallard.Drake = new Look(Style.MallardDrake, mallard.Len * u);
        mallard.Hen = new Look(Style.MallardHen, mallard.Len * u);
        mallard.KidSkin = BakeKid(Style.MallardHen);
        var mandarin = new Species { Name = "mandarin", Len = 0.10f, KidLen = 0.036f };
        mandarin.Drake = new Look(Style.MandarinDrake, mandarin.Len * u);
        mandarin.Hen = new Look(Style.MandarinHen, mandarin.Len * u);
        mandarin.KidSkin = BakeKid(Style.MandarinHen);
        var teal = new Species { Name = "teal", Len = 0.08f, KidLen = 0.030f };
        teal.Drake = new Look(Style.TealDrake, teal.Len * u);
        teal.Hen = new Look(Style.TealHen, teal.Len * u);
        teal.KidSkin = BakeKid(Style.TealHen);
        All = [karugamo, mallard, mandarin, teal];
    }

    // ------------------------------------------------------------- colour helpers

    private static (float R, float G, float B) C(int r, int g, int b) => (r, g, b);
    private static (float R, float G, float B) Mix((float R, float G, float B) a, (float R, float G, float B) b, float k) =>
        (a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k);
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>A cheap repeatable "random" number from 0 to 1 for a cell of the pattern (feather speckle).</summary>
    private static float Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393 + b * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    private static uint Pack((float R, float G, float B) c) =>
        (uint)(Math.Clamp((int)c.R, 0, 255) << 16 | Math.Clamp((int)c.G, 0, 255) << 8 | Math.Clamp((int)c.B, 0, 255));

    private enum Pat { Plain, Scallop, Fine, Mottle }

    /// <summary>
    /// The colours of a grown duck's back, neck and breast at every (s, v). Where
    /// things are: the head is s below 0.2 (painted separately as ovals), the neck
    /// to 0.36, the breast to about 0.5, the folded wings are two darker panels
    /// along the back with a pale line between them, the rump near the end, and
    /// the tail tip. The dome shading (brightest along the backbone) is what
    /// makes the back read as round.
    /// </summary>
    public static uint[] BakeSkin(Style st)
    {
        (float R, float G, float B) back, panel, breast, neck, rump, tail, line;
        Pat pat;
        switch (st)
        {
            case Style.Karugamo:
                back = C(118, 88, 58); panel = C(98, 72, 46); breast = C(138, 106, 74); neck = C(176, 150, 112); rump = C(62, 48, 36); tail = C(44, 36, 30); line = C(150, 122, 88); pat = Pat.Scallop; break;
            case Style.MallardDrake:
                back = C(164, 164, 160); panel = C(136, 134, 128); breast = C(112, 56, 42); neck = C(22, 102, 70); rump = C(22, 26, 28); tail = C(24, 28, 30); line = C(190, 190, 184); pat = Pat.Fine; break;
            case Style.MallardHen:
                back = C(138, 106, 72); panel = C(120, 90, 60); breast = C(156, 124, 90); neck = C(150, 118, 84); rump = C(100, 76, 52); tail = C(150, 128, 96); line = C(172, 142, 102); pat = Pat.Mottle; break;
            case Style.MandarinDrake:
                back = C(92, 80, 56); panel = C(84, 70, 48); breast = C(150, 72, 40); neck = C(196, 108, 50); rump = C(40, 30, 46); tail = C(46, 36, 50); line = C(130, 110, 80); pat = Pat.Plain; break;
            case Style.MandarinHen:
                back = C(122, 112, 98); panel = C(108, 98, 86); breast = C(142, 128, 110); neck = C(136, 126, 112); rump = C(84, 76, 66); tail = C(90, 82, 72); line = C(160, 150, 130); pat = Pat.Mottle; break;
            case Style.TealDrake:
                back = C(126, 130, 132); panel = C(112, 116, 120); breast = C(172, 132, 100); neck = C(124, 62, 46); rump = C(26, 28, 28); tail = C(30, 32, 32); line = C(160, 164, 166); pat = Pat.Fine; break;
            default:
                back = C(136, 112, 84); panel = C(118, 96, 70); breast = C(152, 124, 94); neck = C(146, 120, 90); rump = C(100, 80, 58); tail = C(110, 92, 70); line = C(166, 140, 104); pat = Pat.Mottle; break;
        }
        var skin = new uint[Look.SkinLen * Look.SkinWide];
        for (int si = 0; si < Look.SkinLen; si++)
            for (int vi = 0; vi < Look.SkinWide; vi++)
            {
                float s = (si + 0.5f) / Look.SkinLen, v = (vi + 0.5f) / Look.SkinWide * 2 - 1, av = MathF.Abs(v);
                (float R, float G, float B) c;
                if (s < 0.36f)
                {
                    c = neck;
                    // The mallard drake's white neck ring, a thin band just above the chestnut breast.
                    if (st == Style.MallardDrake && s > 0.295f && s < 0.335f) c = C(238, 238, 232);
                    else if (st == Style.MallardDrake && s >= 0.335f) c = breast;
                    // The teal drake's chestnut neck needs nothing more; the mandarin drake's ruff gets a darker edge.
                    if (st == Style.MandarinDrake) c = Mix(neck, C(150, 72, 40), Smooth((s - 0.25f) / 0.11f));
                }
                else
                {
                    float breastT = 1 - Smooth((s - 0.42f) / 0.14f);
                    c = Mix(back, breast, breastT);
                    float pm = Smooth((av - 0.16f) / 0.08f) * (1 - Smooth((av - 0.92f) / 0.06f)) * Smooth((s - 0.46f) / 0.06f) * (1 - Smooth((s - 0.92f) / 0.04f));
                    c = Mix(c, panel, pm * 0.85f);
                    float lm = (1 - Smooth(av / 0.07f)) * Smooth((s - 0.5f) / 0.06f) * (1 - Smooth((s - 0.95f) / 0.04f));
                    c = Mix(c, line, lm * 0.8f);
                    c = Mix(c, rump, Smooth((s - 0.84f) / 0.06f));
                    c = Mix(c, tail, Smooth((s - 0.93f) / 0.04f));
                    // The pattern on the feathers.
                    if (pat == Pat.Scallop && s > 0.42f && s < 0.93f)
                    {
                        float row = s * 15f, f = row - MathF.Floor(row), off = ((int)MathF.Floor(av * 7f) & 1) * 0.5f;
                        float edge = MathF.Abs((f + off) % 1f - 0.5f) * 2;
                        c = Mix(c, C(182, 154, 112), 0.55f * Smooth((edge - 0.62f) / 0.25f));
                    }
                    else if (pat == Pat.Fine)
                    {
                        float n = Hash(si / 2, vi / 2);
                        float k = 0.92f + 0.14f * n;
                        c = (c.R * k, c.G * k, c.B * k);
                    }
                    else if (pat == Pat.Mottle)
                    {
                        float n = Hash(si / 3, vi / 2);
                        c = n < 0.28f ? Mix(c, C(66, 48, 34), 0.55f) : n > 0.86f ? Mix(c, C(200, 170, 128), 0.4f) : c;
                    }
                    // The species' own marks.
                    if ((st == Style.MallardDrake || st == Style.MallardHen) && s > 0.74f && s < 0.84f && av > 0.5f && av < 0.8f)
                        c = Mix(c, C(48, 70, 150), 0.85f);              // the blue wing patch (speculum) showing at the folded wing's edge
                    if ((st == Style.TealDrake || st == Style.TealHen) && s > 0.74f && s < 0.84f && av > 0.5f && av < 0.8f)
                        c = Mix(c, C(36, 150, 90), 0.8f);               // the green one
                    if (st == Style.MandarinHen && s > 0.74f && s < 0.84f && av > 0.5f && av < 0.8f)
                        c = Mix(c, C(210, 214, 220), 0.7f);
                    if (st == Style.TealDrake && s > 0.5f && s < 0.88f && MathF.Abs(av - 0.76f) < 0.07f)
                        c = C(240, 240, 234);                           // the white stripe along the side
                    if (st == Style.TealDrake && s > 0.94f)
                        c = Mix(c, C(234, 216, 152), 0.9f);             // the cream patch under the tail
                    if (st == Style.MandarinHen && s > 0.4f && s < 0.7f && av > 0.7f && Hash(si, vi) > 0.9f)
                        c = C(232, 230, 224);                           // pale flecks on the flank
                }
                float dome = 0.62f + 0.38f * MathF.Sqrt(MathF.Max(0, 1 - v * v));
                c = (c.R * dome, c.G * dome, c.B * dome);
                skin[si * Look.SkinWide + vi] = Pack(c);
            }
        return skin;
    }

    /// <summary>
    /// A duckling from above: a dark brown back with a cream stripe or two, yellow
    /// down the flanks and rump, and on the head a dark crown flanked by a yellow
    /// cheek each side with a dark line through the eye. Mandarin ducklings are
    /// a touch paler, teal ducklings darker and brighter.
    /// </summary>
    public static uint[] BakeKid(Style st)
    {
        (float R, float G, float B) back = C(66, 48, 28), yellow = C(236, 204, 84), stripe = C(196, 160, 70);
        if (st == Style.MallardHen) { back = C(72, 54, 30); yellow = C(242, 208, 72); }
        if (st == Style.MandarinHen) { back = C(98, 78, 52); yellow = C(238, 222, 146); stripe = C(212, 190, 130); }
        if (st == Style.TealHen) { back = C(58, 46, 30); yellow = C(232, 198, 70); }
        var skin = new uint[Look.SkinLen * Look.SkinWide];
        for (int si = 0; si < Look.SkinLen; si++)
            for (int vi = 0; vi < Look.SkinWide; vi++)
            {
                float s = (si + 0.5f) / Look.SkinLen, v = (vi + 0.5f) / Look.SkinWide * 2 - 1, av = MathF.Abs(v);
                (float R, float G, float B) c = back;
                if (s < 0.30f)
                {
                    // The head: a dark crown down the middle, yellow cheeks, one dark line back from each eye. Kept simple: a fluff ball, not a shell.
                    c = Mix(yellow, back, 1 - Smooth((av - 0.20f) / 0.18f));
                    if (s > 0.14f && s < 0.28f && MathF.Abs(av - 0.62f) < 0.07f) c = Mix(c, C(50, 38, 22), 0.7f);
                    if (s < 0.06f) c = C(70, 54, 34);                  // the tiny dark bill
                }
                else if (s < 0.38f) c = Mix(yellow, back, 0.5f + 0.5f * Smooth((s - 0.30f) / 0.08f));
                else
                {
                    // The back: dark brown all the way to the edge (flanks shading to yellow were a yellow RIM round a dark oval,
                    // and the duckling read as a beetle), with the four yellow patches a real duckling has on its back: one at the
                    // base of each wing stub and one each side of the rump. The very edge a little lighter: down catching the light.
                    float wing = MathF.Max(0, 1 - Near(s, av, 0.50f, 0.58f) / 0.011f), rump = MathF.Max(0, 1 - Near(s, av, 0.86f, 0.50f) / 0.009f);
                    c = Mix(back, yellow, Smooth(MathF.Max(wing, rump)) * 0.85f);
                    c = Mix(c, C(110, 86, 48), Smooth((av - 0.80f) / 0.2f) * 0.35f);
                }
                float dome = 0.8f + 0.2f * MathF.Sqrt(MathF.Max(0, 1 - v * v));       // a soft dome, not a glossy shell
                c = (c.R * dome, c.G * dome, c.B * dome);
                skin[si * Look.SkinWide + vi] = Pack(c);
            }
        return skin;
    }

    /// <summary>Squared distance on the skin sheet from (s, av) to a spot, with "s" scaled so a patch comes out round on a body longer than it is wide.</summary>
    private static float Near(float s, float av, float s0, float av0) { float ds = (s - s0) * 2.4f, dv = av - av0; return ds * ds + dv * dv; }

    /// <summary>The colours a wing is painted in: its main feathers, the band along its trailing edge, and the pale tips.</summary>
    public static (Color Main, Color Band, Color Edge) WingColours(Style st) => st switch
    {
        Style.Karugamo => (Color.FromArgb(94, 70, 46), Color.FromArgb(56, 70, 120), Color.FromArgb(214, 200, 176)),
        Style.MallardDrake => (Color.FromArgb(146, 144, 138), Color.FromArgb(48, 70, 150), Color.FromArgb(236, 236, 230)),
        Style.MallardHen => (Color.FromArgb(126, 96, 64), Color.FromArgb(48, 70, 150), Color.FromArgb(232, 226, 212)),
        Style.MandarinDrake => (Color.FromArgb(104, 84, 54), Color.FromArgb(214, 106, 34), Color.FromArgb(52, 40, 56)),
        Style.MandarinHen => (Color.FromArgb(112, 102, 90), Color.FromArgb(206, 210, 218), Color.FromArgb(226, 224, 218)),
        Style.TealDrake => (Color.FromArgb(120, 124, 126), Color.FromArgb(36, 150, 90), Color.FromArgb(236, 236, 228)),
        _ => (Color.FromArgb(124, 100, 74), Color.FromArgb(36, 150, 90), Color.FromArgb(226, 214, 190)),
    };

    /// <summary>
    /// Both open wings of a duck seen from above, painted once facing RIGHT and
    /// centred on the middle of the body (so the stamp is simply laid on the
    /// body's middle and turned to its heading, TurningStamp.cs). "reach" is how
    /// far the wing tip sits out from the body's side in body lengths, and
    /// "sweep" how far back the tip is swept: the down stroke is long and swept
    /// back, the up stroke shorter and held forward (seen from above it is
    /// foreshortened). Feathers: the main wing, a coloured band along the
    /// trailing edge, and pale fingers at the tip.
    /// </summary>
    public static TurningStamp MakeWing(float L, Color main, Color band, Color edge, float reach, float sweep)
    {
        int w = (int)(L * 1.3f) + 4, h = (int)(L * 2.2f) + 4;
        return new TurningStamp(w, h, g =>
        {
            float cx = w / 2f, cy = h / 2f;
            using var mainB = new SolidBrush(main);
            using var bandB = new SolidBrush(band);
            using var edgeP = new Pen(edge, MathF.Max(1f, L * 0.012f));
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
            {
                float r = reach * L;
                PointF Pt(float x, float y) => new(cx + x * L, cy + side * y * L);
                // shoulder, wrist, tip, then back along the trailing edge to the rump.
                PointF[] outline =
                [
                    Pt(0.17f, 0.10f), Pt(0.12f - 0.10f * sweep, 0.10f + 0.5f * reach), Pt(-0.12f - 0.30f * sweep, 0.10f + reach),
                    Pt(-0.26f - 0.20f * sweep, 0.10f + 0.80f * reach), Pt(-0.30f, 0.10f + 0.45f * reach), Pt(-0.22f, 0.14f),
                ];
                g.FillClosedCurve(mainB, outline, FillMode.Winding, 0.35f);
                // The coloured band along the inner trailing edge (the speculum).
                PointF[] bandPts = [Pt(-0.10f, 0.16f), Pt(-0.12f, 0.10f + 0.34f * reach), Pt(-0.30f, 0.10f + 0.42f * reach), Pt(-0.22f, 0.15f)];
                g.FillPolygon(bandB, bandPts);
                // Pale fingers fanning out from the tip.
                for (int k = 0; k < 4; k++)
                {
                    float f = k / 3f;
                    g.DrawLine(edgeP, Pt(-0.12f - 0.30f * sweep, 0.10f + reach), Pt(-0.26f - 0.20f * sweep - 0.04f * f, 0.10f + (0.80f + 0.12f * f) * reach));
                }
                g.DrawCurve(edgeP, [Pt(0.17f, 0.10f), Pt(0.12f - 0.10f * sweep, 0.10f + 0.5f * reach), Pt(-0.12f - 0.30f * sweep, 0.10f + reach)]);
            }
        });
    }
}

/// <summary>
/// Now and then a duck swims by: usually a mother with a line of ducklings (the
/// eastern spot-billed duck, karugamo, is THE duck of Japanese ponds, and a
/// mother leading her line across a pond is on the news every June), sometimes
/// a drake and a hen, sometimes a lone drake; of one of four species (karugamo,
/// mallard, mandarin, teal). It is a visitor like the snake: it comes in from
/// beyond one edge of the screen over water, crosses by two to four stops in
/// open water that people can see, and leaves beyond another edge, in a minute
/// or two. This class is the one the scene talks to; it plays the part of the
/// DIRECTOR of a visit.
///
/// WHAT A FAMILY DOES, IN THE ORDER IT DOES IT.
///  1. The mother comes in first, head up, the ducklings following in a line.
///     (The ducklings aim at points on the trail the mother has left behind her,
///     one after another, like people following a leader through a field: Core
///     Minds Trail. With four or more they walk two abreast, so the line is
///     never longer than the mother can keep an eye on.)
///  2. She paddles from stop to stop, now and then looking about, dabbling,
///     preening, flapping (Duck.cs's mind). When she stops, the line closes up
///     into a cluster behind her.
///  3. About every 20 to 40 seconds, ONE cute thing happens in the family, never
///     two at once: a duckling lags to peck at something and then scrambles to
///     catch up (the mother waits, and looks back); two bump and spin; one climbs
///     on the mother's back and rides, then slides off with a tiny plop; one goes
///     to peck a floating petal; one goes to chase a flash of minnows and dabbles
///     there; all of them dabble bottoms up together; they all bunch under her
///     chin; or she flaps and they scatter a little and come back. A weighted
///     dice picks which, and the one just done is much less likely next.
///  4. If the snake comes within a quarter of a screen she herds them away at
///     1.5 times the speed, the ducklings hurrying behind (not again for 10 s).
///  5. She leaves beyond another edge.
/// A duck is never moved by a fish, but it is SOLID to them (its body is in the
/// scene's list of surface bodies), so a rising koi is pushed back down, the
/// fish steer round it, and floating petals are shoved aside by every duck
/// and duckling. Nothing clips through anything in this pond.
/// </summary>
internal sealed class Ducks
{
    private static readonly PointF[] NoHeads = [];

    /// <summary>The cute things a duckling family does; the numbers are the order in the dice.</summary>
    internal enum Cute { None, Lag, Bump, Ride, Peck, Chase, Dabble, Bunch, Flap }

    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly float _often;
    private readonly Random _rng;
    private readonly Petals _petals;
    private readonly Minnows? _minnows;
    private readonly DuckWorld? _world;
    private readonly Ground.Solid? _solidFn;       // made once (a method group turned into a delegate allocates every time)
    private readonly Roster? _roster;
    private readonly float _u;

    // ---- the visit in hand ----
    private bool _visiting, _leaving, _entered;
    private float _next, _visitT, _wpT;
    private Species _sp = null!;
    private Duck[] _adults = [];
    private Duckling[] _kids = [];
    private PointF[] _heads = NoHeads;
    private Trail? _trail;
    private readonly PointF[] _wp = new PointF[8];
    private int _wpCount, _wpAt, _extra;
    private PointF _exitG;
    private int _entryEdge;
    private float[] _slotX = [], _slotY = [], _slotAt = [], _slotLat = [], _carX = [], _carY = [];
    private const float TStep = 0.012f;       // the mother's trail sampled every 0.012u back to 0.66u, once a frame
    private readonly PointF[] _tp = new PointF[56];
    private float _fizzT;
    private int _fizzK;

    // ---- the cute things ----
    private Cute _cute, _lastCute;
    private float _cuteT, _cuteGap, _phaseT, _rideFor, _bunchFor;
    private int _ka, _kb, _phase, _ringCount;
    private bool _waitIsCute;
    private float _waitT, _noWait, _slideV, _goneT;
    private float _px, _py;                 // a petal or a school the duckling is going to (ground pixels)
    /// <summary>How many of each cute thing this visit has had (for the log and the tests; index by the Cute enum).</summary>
    public readonly int[] CuteCounts = new int[9];

    public Ducks(Pond pond, Ripples ripples, float often, Random rng, Petals petals, Minnows? minnows, bool[] view)
    {
        _pond = pond; _ripples = ripples; _often = Math.Max(0, often); _rng = rng; _petals = petals; _minnows = minnows; _u = pond.U;
        if (_often <= 0) return;
        _world = new DuckWorld(pond, view);
        _solidFn = _world.SolidG;
        _roster = new Roster(pond.U);              // the pictures are painted once, here, never during a visit
        _next = 20 + 40 * (float)rng.NextDouble();
        // And the CODE is compiled now too, on a helper thread, not on the first visit's first frame: the JIT compiling every
        // duck method at once there cost that frame 16 ms more than its neighbours (720p), a hitch just as the family arrived.
        System.Threading.Tasks.Task.Run(static () =>
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (Type t in new[] { typeof(Ducks), typeof(Duck), typeof(Duckling), typeof(DuckWorld), typeof(Look), typeof(TurningStamp) })
                foreach (System.Reflection.MethodInfo m in t.GetMethods(all))
                    if (!m.IsAbstract && !m.ContainsGenericParameters) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle);
        });
    }

    /// <summary>Is a duck family on the water right now?</summary>
    public bool Visiting => _visiting;

    /// <summary>The head of every duck and duckling on the water, screen pixels (the minnows, tadpoles and frogs keep clear of them). Empty when none.</summary>
    public PointF[] Heads => _visiting ? _heads : NoHeads;

    /// <summary>Every other point along every duck and duckling on the picture, screen pixels: what the snake swims round and is startled by, and what a swimming frog dives from. Empty when none.</summary>
    public PointF[] Beads => _visiting ? _beads : NoHeads;
    private PointF[] _beads = NoHeads;
    private readonly List<PointF> _beadList = new(160);

    /// <summary>For logs and tests.</summary>
    public IReadOnlyList<Duck> Adults => _adults;
    public IReadOnlyList<Duckling> Kids => _kids;
    public Cute Doing => _cute;
    public string SpeciesName => _visiting ? _sp.Name : "";

    private static float Dist(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return MathF.Sqrt(dx * dx + dy * dy); }
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    // ============================================================ a visit begins

    private void BeginVisit()
    {
        DuckWorld w = _world!;
        if (!w.FindEdge(_rng, -1, out int e1, out PointF entry, out PointF inward) || !w.FindEdge(_rng, e1, out _, out PointF exit, out PointF exitIn))
        {
            _next = 15;                            // no water to come in over just now (the land corner, a full pond): try again soon
            return;
        }
        float u = _u;
        int shape = Dice.Pick(_rng, [0.60f, 0.25f, 0.15f]);                // a mother with ducklings, a pair, a lone drake
        _sp = _roster!.All[Dice.Pick(_rng, [0.40f, 0.30f, 0.15f, 0.15f])];
        int nKids = shape == 0 ? 3 + _rng.Next(4) : shape == 1 && _rng.NextDouble() < 0.3 ? 3 + _rng.Next(3) : 0;

        float cruise = u * (0.05f + 0.03f * (float)_rng.NextDouble());
        var leader = new Duck(w, _ripples, _rng, _sp, shape == 2 ? _sp.Drake : _sp.Hen, shape == 2, cruise, Traits.Roll(_rng), Traits.Roll(_rng));
        _adults = shape == 1
            ? [leader, new Duck(w, _ripples, _rng, _sp, _sp.Drake, true, cruise * (0.96f + 0.08f * (float)_rng.NextDouble()), Traits.Roll(_rng), Traits.Roll(_rng))]
            : [leader];

        // The family starts beyond the edge, in a line, heading in.
        float heading = MathF.Atan2(inward.Y / Ripples.Squash, inward.X);
        (float lx, float ly) = w.ToGround(entry.X - inward.X * 0.17f * u, entry.Y - inward.Y * 0.17f * u);
        leader.Place(lx, ly, heading);
        if (_adults.Length > 1)
        {
            float cs = MathF.Cos(heading), sn = MathF.Sin(heading), lat = 0.62f * leader.Len;
            _adults[1].Place(lx - cs * 0.07f * u - sn * lat, ly - sn * 0.07f * u + cs * lat, heading);
        }
        _trail = new Trail(360);
        _trail.Straight(new PointF(lx, ly), heading, 0.7f * u, 175);
        _kids = new Duckling[nKids];
        for (int k = 0; k < nKids; k++) _kids[k] = new Duckling(w, _ripples, _rng, _sp.KidSkin, _sp.KidLen, k);
        _slotX = new float[nKids]; _slotY = new float[nKids]; _slotAt = new float[nKids]; _slotLat = new float[nKids]; _carX = new float[nKids]; _carY = new float[nKids];
        ComputeSlots(leader);
        for (int k = 0; k < nKids; k++) _kids[k].Place(_slotX[k], _slotY[k], heading);
        _heads = new PointF[_adults.Length + nKids];

        // The route: two to four stops in open water that can be seen, then out beyond another edge.
        _wpCount = 0; _wpAt = 0; _extra = 0; _wpT = 0;
        PointF from = new(lx, ly);
        int want = 2 + _rng.Next(3);
        for (int i = 0; i < want; i++)
        {
            if (!w.FindWaypoint(_rng, from.X, from.Y, 0.3f * u, out PointF p)) break;
            _wp[_wpCount++] = p;
            from = p;
        }
        (float ex, float ey) = w.ToGround(exit.X - exitIn.X * 1.2f * u, exit.Y - exitIn.Y * 1.2f * u);   // well beyond the edge: it swims straight on out, never wheeling round a goal
        _exitG = new PointF(ex, ey);
        _entryEdge = e1;
        _leaving = false;
        if (_wpCount == 0) Leave(leader);
        _visiting = true; _entered = false; _visitT = 0;
        _cute = Cute.None; _lastCute = Cute.None; _cuteGap = 7 + 6 * (float)_rng.NextDouble(); _waitIsCute = false; _fizzT = 0;
        Array.Clear(CuteCounts);
    }

    // ============================================================ every frame

    /// <param name="snakeHead">The snake's head if it is out (the mother herds the ducklings away from it), else null.</param>
    /// <param name="turtles">The turtles: one at the surface is a thing a duck feels round (it is never moved by a duck).</param>
    /// <param name="surface">The scene's surface list (the snake while it swims, the frogs in the water, the ducks themselves): every body in it that is not a duck is a thing a duck feels round.</param>
    public void Update(float dt, PointF? snakeHead, IReadOnlyList<Turtle>? turtles = null, IReadOnlyList<Body>? surface = null)
    {
        if (_world == null) return;
        // THE POND'S LAW, the duck's side of it: a duck is never moved by anything, so it must go ROUND whatever
        // else is at the surface. The turtle's shell (when it is up near the surface), the snake's body and a
        // swimming frog are handed to the world as circles for this frame (every other bead of each chain); the
        // feelers and the step rule read them like rocks. A frog dives from a duck's head on its own, but a duck's
        // body is longer than its head's reach, and a frog beside the bill was run over before it saw the duck.
        DuckWorld w0 = _world;
        w0.ClearMoving();
        if (turtles != null)
            foreach (Turtle t in turtles)
                if (t.InWater && t.Z < 0.12f)
                {
                    (float gx, float gy) = w0.ToGround(t.Middle.X, t.Middle.Y);
                    w0.AddMoving(gx, gy, t.Length * 0.5f);
                }
        if (surface != null)
            foreach (Body b in surface)
            {
                if (b is Duck || b is Duckling) continue;
                var sp = b.Shape;
                for (int i = 0; i < sp.Points.Length; i += 2) w0.AddMoving(sp.Points[i].X, sp.Points[i].Y, sp.Radius[i]);
            }
        if (!_visiting)
        {
            _next -= dt;
            if (_next <= 0) BeginVisit();
            return;
        }
        _visitT += dt;
        DuckWorld w = _world;
        Duck leader = _adults[0];

        // ---- the route: the next stop, or out ----
        if (_visitT > 150 && !_leaving) Leave(leader);
        if (!_leaving)
        {
            _wpT += dt;
            PointF g = _wp[_wpAt];
            if (Dist(leader.X, leader.Y, g.X, g.Y) < 0.07f * _u || _wpT > 40) AdvanceWaypoint(leader);
        }
        PointF goal = _leaving ? _exitG : _wp[_wpAt];
        leader.GoalX = goal.X; leader.GoalY = goal.Y;
        if (_adults.Length > 1) FollowLeader(leader, _adults[1]);

        // ---- the snake: the mother herds them away ----
        if (snakeHead is PointF sh)
            foreach (Duck a in _adults)
                if (Dist(a.Centre.X, a.Centre.Y, sh.X, sh.Y) < 0.25f * _u && a.StartHerd(sh) && a == leader) AbortCute(leader);

        // ---- the cute things, and the rule that the mother waits for a straggler ----
        Cutes(dt, leader);
        leader.AllowBig = _cute == Cute.None && !_leaving;
        if (_adults.Length > 1) _adults[1].AllowBig = false;
        foreach (Duck a in _adults) a.Update(dt);
        if (_adults.Length > 1) Mirror(leader, _adults[1]);
        // A pair are solid to each other: two bodies that overlap are each pushed half the way out along the line between
        // their closest parts, no faster than a body length a second (a recoil, as the koi do it; one shove would read as a
        // hop). The second duck only aims for a place beside the first, and when the first stopped the second was told to
        // stop wherever it was, which was often on top of her: a hen under a drake with only her head showing for whole
        // seconds at a time.
        if (_adults.Length > 1 && Overlap(_adults[0].Shape, _adults[1].Shape, _adults[0].X, _adults[0].Y, _adults[0].Heading, out float px, out float py, out float depth))
        {
            float d = MathF.Min(_adults[0].Len * dt, depth * 0.5f);
            _adults[0].Shove(px * d, py * d);
            _adults[1].Shove(-px * d, -py * d);
        }
        // And every duck is solid to the snake and the frogs the same way. Its feelers keep a duck off them only while it moves;
        // a snake swimming into a waiting hen, or a frog crawling under her, got onto her and nothing moved either. Now the duck
        // recoils off whatever is on it, along the line between their closest parts, onto open water only.
        if (surface != null)
            foreach (Body b in surface)
            {
                if (b is Duck || b is Duckling) continue;
                foreach (Duck a in _adults) if (Recoil(a.Shape, b.Shape, a.X, a.Y, a.Heading, a.Len, dt, out float sx, out float sy)) a.Shove(sx, sy);
                foreach (Duckling k in _kids) if (Recoil(k.Shape, b.Shape, k.X, k.Y, k.Heading, k.Len, dt, out float sx, out float sy)) k.Shove(sx, sy);
            }
        // And off a turtle up at the surface (the surface list holds no turtles; two ducklings sat on a slider that surfaced under the line).
        if (turtles != null)
            foreach (Turtle t in turtles)
            {
                if (!t.InWater || t.Z >= 0.12f) continue;
                foreach (Duck a in _adults) if (Recoil(a.Shape, t.Shape, a.X, a.Y, a.Heading, a.Len, dt, out float sx, out float sy)) a.Shove(sx, sy);
                foreach (Duckling k in _kids) if (Recoil(k.Shape, t.Shape, k.X, k.Y, k.Heading, k.Len, dt, out float sx, out float sy)) k.Shove(sx, sy);
            }

        // ---- the line behind her ----
        _trail!.RememberIfMoved(new PointF(leader.X, leader.Y), 0.004f * _u);
        ComputeSlots(leader);
        for (int k = 0; k < _kids.Length; k++) UpdateKid(k, dt, leader);
        KeepApart(dt);
        if (_kids.Length > 0)
        {
            _fizzT -= dt;
            if (_fizzT <= 0)
            {
                _fizzT = 0.5f; _fizzK = (_fizzK + 1) % _kids.Length;
                Duckling d = _kids[_fizzK];
                if (!d.OnMother && d.Speed > 0.35f * leader.Cruise && !_ripples.Crowded) _ripples.Add(d.Centre.X, d.Centre.Y, 0.08f);     // the fizz of tiny feet (skipped when the pond is near its ring cap)
            }
        }

        // ---- heads for the small animals to keep clear of ----
        int n = 0;
        foreach (Duck a in _adults) _heads[n++] = a.HeadSpot;
        foreach (Duckling d in _kids) _heads[n++] = d.HeadSpot;
        _beadList.Clear();
        foreach (Duck a in _adults) if (a.OnScreen) { ReadOnlySpan<PointF> dr = a.Drawn; for (int i = 0; i < dr.Length; i += 2) _beadList.Add(dr[i]); }
        foreach (Duckling d in _kids) if (d.OnScreen) { ReadOnlySpan<PointF> dr = d.Drawn; for (int i = 0; i < dr.Length; i += 2) _beadList.Add(dr[i]); }
        if (_beads.Length != _beadList.Count) _beads = new PointF[_beadList.Count];   // reused between frames: a fresh array every frame was a collection every few seconds
        _beadList.CopyTo(_beads);

        // ---- the visit is over when every middle is beyond the screen again ----
        bool anyIn = false;
        foreach (Duck a in _adults) anyIn |= Inside(a.Centre, a.Len * 0.6f);
        foreach (Duckling d in _kids) anyIn |= Inside(d.Centre, d.Len * 0.6f);
        if (anyIn) _entered = true;
        // Over when every middle is beyond the screen again; or when the mother has been gone ten seconds (anyone still straggling is swimming on after her).
        float u = _u;
        PointF lc = leader.Centre;
        bool leaderGone = lc.X < -0.1f * u || lc.X > w.W + 0.1f * u || lc.Y < -0.1f * u || lc.Y > w.H + 0.1f * u;
        _goneT = _entered && _leaving && leaderGone ? _goneT + dt : 0;
        if (_entered && !anyIn && (_leaving || _visitT > 150) || _goneT > 10 || _visitT > 260 && !leader.OnScreen || _visitT > 420)
        {
            _visiting = false;
            _next = (240 + 240 * (float)_rng.NextDouble()) / _often;       // four to eight minutes at the normal setting
        }
    }

    /// <summary>
    /// Time to go. Picks the way out again from where the mother is now: an edge (not the one they came in by) whose first stretch is
    /// water, with a clear straight swim to it (the planned exit was chosen before the route and could have a pad field or the
    /// land corner between it and the last stop, where the first family to try it waded onto the bank).
    /// </summary>
    private void Leave(Duck leader)
    {
        if (_leaving) return;
        _leaving = true;
        DuckWorld w = _world!;
        for (int tries = 0; tries < 24; tries++)
        {
            if (!w.FindEdge(_rng, _entryEdge, out _, out PointF p, out PointF n)) break;
            (float ex, float ey) = w.ToGround(p.X, p.Y);
            if (!Ground.LineClear(_solidFn!, new PointF(leader.X, leader.Y), new PointF(ex, ey), 0.015f * _u)) continue;
            (float fx, float fy) = w.ToGround(p.X - n.X * 1.2f * _u, p.Y - n.Y * 1.2f * _u);
            _exitG = new PointF(fx, fy);
            return;
        }
    }

    /// <summary>Is the point on the picture, or within "m" of it (a body whose middle is just past the edge still has half of itself on the picture: ending the visit then cut it off in one frame)?</summary>
    private bool Inside(PointF p, float m = 0) => p.X >= -m && p.X < _world!.W + m && p.Y >= -m && p.Y < _world.H + m;

    private void AdvanceWaypoint(Duck leader)
    {
        _wpAt++; _wpT = 0;
        if (_wpAt < _wpCount) return;
        // Out of stops. A visit should last a minute at least: find another stop if it has been short, else leave.
        if (_visitT < 60 && _extra < 3 && _wpCount < _wp.Length && _world!.FindWaypoint(_rng, leader.X, leader.Y, 0.3f * _u, out PointF p))
        {
            _wp[_wpCount++] = p; _extra++;
            return;
        }
        Leave(leader);
        _wpAt = Math.Max(0, _wpCount - 1);
    }

    /// <summary>The second duck of a pair swims abreast of the first, a body's width off her right, matching her pace.</summary>
    private void FollowLeader(Duck leader, Duck f)
    {
        float cs = MathF.Cos(leader.Heading), sn = MathF.Sin(leader.Heading), lat = 0.62f * (leader.Len + f.Len) * 0.5f;
        f.GoalX = leader.X - sn * lat - cs * 0.04f * leader.Len;
        f.GoalY = leader.Y + cs * lat - sn * 0.04f * leader.Len;
        float d = Dist(f.X, f.Y, f.GoalX, f.GoalY);
        f.Pace = Math.Clamp(leader.Speed / f.Cruise + Math.Clamp((d - 0.03f * _u) / (0.08f * _u), 0f, 1f) * 0.9f, 0f, 1.7f);
    }

    /// <summary>When the first duck stands still for a dabble, a preen, a flap or a wait, the second stands with her; when she moves off, so does it.</summary>
    /// <summary>
    /// Do two bodies overlap, and which way (on the ground) does "mine" go to get off "other"? Chain.Touch answers straight UP
    /// when the two closest pieces cross exactly (there is no direction between coincident points), and every duck lies at
    /// the surface, so a push read in X and Y alone was nothing at the deepest overlap there is: a drake swam straight across
    /// the hen. Then the push is sideways, along the line between the two middles (or across my own heading if those coincide),
    /// the koi school's own fallback.
    /// </summary>
    private static bool Overlap(Peckworks.Screensavers.Core.Minds.Chain mine, Peckworks.Screensavers.Core.Minds.Chain other, float myX, float myY, float heading, out float px, out float py, out float depth)
    {
        px = py = 0;
        if (!Peckworks.Screensavers.Core.Minds.Chain.Touch(mine, other, out System.Numerics.Vector3 p, out depth)) return false;
        px = p.X; py = p.Y;
        if (MathF.Abs(p.Z) > 0.7f || px * px + py * py < 0.1f)
        {
            System.Numerics.Vector3 om = other.Points[other.Points.Length / 2];
            float dx = myX - om.X, dy = myY - om.Y, l = MathF.Sqrt(dx * dx + dy * dy);
            if (l > 1e-3f) { px = dx / l; py = dy / l; }
            else { px = -MathF.Sin(heading); py = MathF.Cos(heading); }
        }
        return true;
    }

    /// <summary>How far to push "mine" off "other" this frame, if they overlap: the whole overlap, no faster than two body lengths a second.</summary>
    private static bool Recoil(Peckworks.Screensavers.Core.Minds.Chain mine, Peckworks.Screensavers.Core.Minds.Chain other, float myX, float myY, float heading, float len, float dt, out float sx, out float sy)
    {
        sx = sy = 0;
        if (!Overlap(mine, other, myX, myY, heading, out float px, out float py, out float depth)) return false;
        float d = MathF.Min(len * 2f * dt, depth);
        sx = px * d; sy = py * d;
        return true;
    }

    private static void Mirror(Duck leader, Duck f)
    {
        bool still = leader.Now is Duck.Doing.Dabble or Duck.Doing.Preen or Duck.Doing.Flap or Duck.Doing.Wait;
        if (still && f.Now is not (Duck.Doing.Wait or Duck.Doing.Herd)) f.BeginWait(false, 0, 0);
        else if (!still && f.Now == Duck.Doing.Wait) f.EndWait();
    }

    // ============================================================ the line

    /// <summary>
    /// Where each duckling should be: a point on the mother's trail a set way
    /// back, one row after another (single file for up to three ducklings, two
    /// abreast for more). When she stops, the line closes up to six tenths of its
    /// length: a cluster behind her.
    /// </summary>
    private void ComputeSlots(Duck leader)
    {
        int n = _kids.Length;
        if (n == 0) return;
        float u = _u;
        bool two = n >= 4;
        int rows = two ? (n + 1) / 2 : n;
        float comp = Math.Clamp(leader.Speed / MathF.Max(1e-3f, leader.Cruise), 0f, 1f);
        // Rows a little more than a duckling's length apart, so one is never laid on the back of the one ahead; when she stops
        // the line closes up, but never closer than a length (the first rows were 0.036u apart for a 0.040u duckling, and six
        // tenths of that in the cluster: the ducklings were drawn on top of each other whenever the family slowed).
        float rowGap = MathF.Max(0.036f * u, 1.15f * _sp.KidLen * u);
        float squeeze = MathF.Max(0.6f + 0.4f * comp, 1.02f * _sp.KidLen * u / rowGap);
        // The trail, sampled at even steps back from the mother's middle: everything below reads this one row of points.
        var walk = _trail!.Begin(new PointF(leader.X, leader.Y), 0, 0, leader.Heading);
        for (int j = 0; j < _tp.Length; j++) _tp[j] = walk.Advance(j * TStep * u).P;
        float first = 0.4f * leader.Len + 0.5f * _sp.KidLen * u + 0.006f * u;
        for (int r = 0; r < rows; r++)
        {
            float at = (first + rowGap * r) * squeeze / (TStep * u);              // how many steps back along the trail
            for (int c = 0; c < (two ? 2 : 1); c++)
            {
                int k = two ? r * 2 + c : r;
                if (k >= n) break;
                (PointF p, PointF dir) = TrailAt(at);
                float lat = two ? (c == 0 ? -1 : 1) * 0.017f * u * (n % 2 == 1 && k == n - 1 ? 0 : 1) : 0;
                _slotAt[k] = at; _slotLat[k] = lat;
                _slotX[k] = p.X - dir.Y * lat; _slotY[k] = p.Y + dir.X * lat;
            }
        }
        // The carrot each duckling chases: its slot, straight, when the water between is clear; otherwise (a pad between it and
        // its place, the mother having gone round it) a short way along her trail from where it is. The search for "where it is
        // on the trail" is kept to the stretch just behind its own slot: nearest over the WHOLE trail picked an old part of the
        // path that happened to pass close by, and the duckling followed the whole loop of her old route round again.
        for (int k = 0; k < n; k++)
        {
            Duckling d = _kids[k];
            if (Ground.LineClear(_solidFn!, new PointF(d.X, d.Y), new PointF(_slotX[k], _slotY[k]), 0.012f * u))
            {
                _carX[k] = _slotX[k]; _carY[k] = _slotY[k];
                continue;
            }
            int lo = (int)_slotAt[k], hi = Math.Min(_tp.Length - 1, lo + 20), near = lo;
            float best = float.MaxValue;
            for (int j = lo; j <= hi; j++)
            {
                float dx = _tp[j].X - d.X, dy = _tp[j].Y - d.Y, dd = dx * dx + dy * dy;
                if (dd < best) { best = dd; near = j; }
            }
            float ci = near > _slotAt[k] ? MathF.Max(_slotAt[k], near - 4f) : _slotAt[k];
            (PointF p, PointF dir) = TrailAt(ci);
            _carX[k] = p.X - dir.Y * _slotLat[k]; _carY[k] = p.Y + dir.X * _slotLat[k];
        }
    }

    /// <summary>The point "at" steps back along the sampled trail (fractions allowed) and the forward direction there.</summary>
    private (PointF P, PointF Dir) TrailAt(float at)
    {
        at = Math.Clamp(at, 0f, _tp.Length - 1.001f);
        int i = (int)at; float f = at - i;
        PointF a = _tp[i], b = _tp[i + 1];
        float dx = a.X - b.X, dy = a.Y - b.Y, l = MathF.Max(1e-3f, MathF.Sqrt(dx * dx + dy * dy));
        return (new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f), new PointF(dx / l, dy / l));
    }

    /// <summary>One duckling's frame: carry out its doing (the director has set its targets), then its body.</summary>
    private void UpdateKid(int k, float dt, Duck leader)
    {
        Duckling d = _kids[k];
        float cr = leader.Cruise, ms = leader.Speed, u = _u;
        bool scramble = d.Doing == KDoing.Scramble;
        switch (d.Doing)
        {
            case KDoing.Follow:
                d.Steer(dt, _carX[k], _carY[k], 1.7f * cr, ms);
                break;
            case KDoing.Scramble:
                d.Steer(dt, _carX[k], _carY[k], 2.3f * cr, ms);
                if (d.DistTo(_slotX[k], _slotY[k]) < 0.03f * u) d.Begin(KDoing.Follow);
                break;
            case KDoing.Lag:
                d.Hold(dt);
                d.Dip = 0.5f + 0.5f * MathF.Sin(d.T * 5f);                 // pecking at something on the surface
                break;
            case KDoing.Bump:
            case KDoing.Peck:
            case KDoing.Chase:
                if (_phase == 0) d.Steer(dt, d.TX, d.TY, d.MaxV, 0); else d.Hold(dt);
                break;
            case KDoing.Ride:
                if (_phase == 0) { d.Steer(dt, d.TX, d.TY, d.MaxV, ms); scramble = true; }
                break;                                                    // from the climb on, the director pins it to the mother
            case KDoing.Dabble:
                d.Hold(dt);
                break;
            case KDoing.Bunch:
                d.Steer(dt, d.TX, d.TY, d.MaxV, 0);
                break;
            case KDoing.Scatter:
                if (d.DistTo(d.TX, d.TY) > 0.01f * u) d.Steer(dt, d.TX, d.TY, d.MaxV, 0); else d.Hold(dt);
                break;
        }
        d.Update(dt, scramble);
    }

    /// <summary>
    /// Nothing is drawn through anything: two ducklings that are closer than their
    /// sizes are eased apart (half each, no faster than a few body lengths a
    /// second), and a duckling is eased out of any grown duck's body. Two that
    /// are bumping on purpose, one riding, and one gathering under a chin are left
    /// alone.
    /// </summary>
    private void KeepApart(float dt)
    {
        int n = _kids.Length;
        for (int i = 0; i < n; i++)
        {
            Duckling a = _kids[i];
            if (a.OnMother) continue;
            for (int j = i + 1; j < n; j++)
            {
                Duckling b = _kids[j];
                if (b.OnMother || _cute == Cute.Bump && i == _ka && j == _kb) continue;
                // Two BODIES that overlap (the chains, as the koi test each other: a duckling is four times longer than it is
                // wide, so a disc round each middle let two in a line lie on each other by half a body) are each pushed half
                // the way out along the line between their closest parts, no faster than a recoil.
                if (!Overlap(a.Shape, b.Shape, a.X, a.Y, a.Heading, out float pvx, out float pvy, out float depth)) continue;
                float push = MathF.Min(a.Len * 2.5f * dt, depth * 0.5f);
                a.Shove(pvx * push, pvy * push);
                b.Shove(-pvx * push, -pvy * push);
            }
            if (a.Doing == KDoing.Bunch) continue;
            // Off a grown duck's head and body: every point of the duckling against the adult's points 0 to 11 as discs (the
            // tail tip stays free: the line's first slot sits just behind it). The old test, the duckling's middle against the
            // body points only, let one following close ride up her flank or sit under her chin with no push. Two ground discs
            // never push straight up; two coincident points push along the line between the middles.
            foreach (Duck m in _adults)
                for (int q = 0; q < a.Shape.Points.Length; q++)
                    for (int s = 0; s < 12; s++)
                    {
                        System.Numerics.Vector3 p = a.Shape.Points[q], c = m.Shape.Points[s];
                        float r = m.Shape.Radius[s] + a.Shape.Radius[q];
                        float dx = p.X - c.X, dy = p.Y - c.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                        if (d >= r) continue;
                        if (d < 1e-3f) { dx = a.X - m.X; dy = a.Y - m.Y; d = MathF.Sqrt(dx * dx + dy * dy); if (d < 1e-3f) { dx = 1; dy = 0; d = 1; } }
                        float push = MathF.Min(a.Len * 3f * dt, r - d);
                        a.Shove(dx / d * push, dy / d * push);
                    }
        }
    }

    // ============================================================ the cute things

    /// <summary>
    /// The director of the cute things. Two jobs. (1) The straggler rule: if a
    /// duckling (not one riding) is more than 0.15u from its mother she stops and
    /// looks back at it until it is within 0.08u. (2) Between times, roll for the
    /// next cute thing and run the one in hand, one phase at a time.
    /// </summary>
    private void Cutes(float dt, Duck leader)
    {
        int n = _kids.Length;
        if (n == 0) return;
        float u = _u;
        bool herding = leader.Now == Duck.Doing.Herd;

        // 1. The straggler rule. A duckling is a straggler when it is more than 0.15u from its mother AND more than 0.07u
        // behind its own place in the line (a duckling at the back of a double file is a long way from her and is not late).
        // She stops and looks back at the worst of them; she goes on when each is back in its place (within 0.04u of it) or
        // within 0.08u of her, or after 14 seconds (a duckling that cannot get to her is not worth a whole visit).
        float lagWorst = 0; int lagKid = -1; bool allBack = true;
        for (int k = 0; k < n; k++)
        {
            Duckling d = _kids[k];
            if (d.OnMother) continue;
            float lag = d.DistTo(_slotX[k], _slotY[k]), dm = d.DistTo(leader.X, leader.Y);
            if (dm > 0.15f * u && lag > 0.07f * u && lag > lagWorst) { lagWorst = lag; lagKid = k; }
            if (lag >= 0.04f * u && dm >= 0.08f * u) allBack = false;
        }
        _noWait -= dt;
        if (!herding && !_waitIsCute)
        {
            if (leader.Now == Duck.Doing.Wait)
            {
                _waitT += dt;
                if (allBack || _waitT > 14) { leader.EndWait(); if (_waitT > 14) _noWait = 15; }
                else
                {
                    int w = lagKid >= 0 ? lagKid : 0;
                    leader.LookAt(_kids[w].X, _kids[w].Y);
                }
            }
            else if (lagKid >= 0 && _noWait <= 0 && leader.Now is Duck.Doing.Cruise or Duck.Doing.Look)
            {
                leader.BeginWait(true, _kids[lagKid].X, _kids[lagKid].Y);
                _waitT = 0;
            }
        }

        // 2. The cute thing in hand, or the roll for the next.
        if (_cute == Cute.None)
        {
            if (_entered) _cuteGap -= dt;
            if (_cuteGap <= 0 && _entered && !_leaving && !herding && leader.OnScreen && leader.Now is Duck.Doing.Cruise or Duck.Doing.Look) StartCute(leader);
            return;
        }
        _cuteT += dt; _phaseT += dt;
        if (herding && _cute != Cute.Ride) { AbortCute(leader); return; }
        if (_cuteT > 30) { AbortCute(leader); return; }
        RunCute(dt, leader);
    }

    private void EndCute()
    {
        _lastCute = _cute; _cute = Cute.None;
        _cuteGap = 8 + 10 * (float)_rng.NextDouble();
        _waitIsCute = false;
        if (_adults.Length > 0 && _adults[0].Now == Duck.Doing.Wait && _kids.Length > 0) { /* the straggler rule decides from here */ }
    }

    /// <summary>The snake came, or the thing ran too long: everyone goes back to the line (a duckling already on her back stays for the ride).</summary>
    private void AbortCute(Duck leader)
    {
        if (_cute == Cute.None) return;
        foreach (Duckling d in _kids)
        {
            if (d.OnMother) { _phase = 3; _phaseT = 0; continue; }
            d.Dip = 0; d.WantTip = false;
            d.Begin(KDoing.Scramble);
        }
        if (_waitIsCute) leader.EndWait();
        _waitIsCute = false;
        if (_cute != Cute.Ride) EndCute();
    }

    private bool FindPetal(Duck leader, out float gx, out float gy)
    {
        gx = gy = 0;
        PointF c = leader.Centre;
        if (!_petals.NearestFloating(c.X, c.Y, 0.17f * _u, out PointF at)) return false;
        (gx, gy) = _world!.ToGround(at.X, at.Y);
        return _world.SolidG(gx, gy) && _world.Clear(gx, gy) > 0.03f * _u && Dist(gx, gy, leader.X, leader.Y) < 0.17f * _u;
    }

    private bool FindSchool(Duck leader, out float gx, out float gy)
    {
        gx = gy = 0;
        if (_minnows == null) return false;
        PointF c = leader.Centre;
        if (_minnows.NearestSchool(c.X, c.Y) is not PointF s) return false;
        (gx, gy) = _world!.ToGround(s.X, s.Y);
        return _world.SolidG(gx, gy) && _world.Clear(gx, gy) > 0.04f * _u && Dist(gx, gy, leader.X, leader.Y) < 0.19f * _u;
    }

    private void StartCute(Duck leader)
    {
        int n = _kids.Length;
        Span<float> w = stackalloc float[8];                      // Lag, Bump, Ride, Peck, Chase, all dabble, bunch, flap
        bool petal = FindPetal(leader, out float ptx, out float pty), school = FindSchool(leader, out float scx, out float scy);
        w[0] = 1.0f;
        w[1] = n >= 2 ? 0.9f : 0f;
        w[2] = 0.8f;
        w[3] = petal ? 1.1f : 0f;
        w[4] = school ? 1.2f : 0f;
        w[5] = 0.8f;
        w[6] = 0.7f;
        w[7] = 0.7f * (1.3f - leader.Calm);
        if (_lastCute != Cute.None) w[(int)_lastCute - 1] *= 0.15f;
        int pick = Dice.Pick(_rng, w);
        if (pick < 0) { _cuteGap = 5; return; }
        _cute = (Cute)(pick + 1);
        _cuteT = 0; _phase = 0; _phaseT = 0; _ringCount = 0;
        CuteCounts[(int)_cute]++;
        switch (_cute)
        {
            case Cute.Lag:
                _ka = _rng.Next(n);
                _kids[_ka].Begin(KDoing.Lag, 3f + 2f * (float)_rng.NextDouble());
                break;
            case Cute.Bump:
                _ka = n >= 4 ? 2 * _rng.Next(n / 2) : _rng.Next(n - 1);     // side by side in a double file, neighbours in a single one
                _kb = _ka + 1;
                _kids[_ka].Begin(KDoing.Bump); _kids[_kb].Begin(KDoing.Bump);
                break;
            case Cute.Ride:
                _ka = 0;
                _kids[0].Begin(KDoing.Ride);
                _rideFor = 5 + 10 * (float)_rng.NextDouble();
                break;
            case Cute.Peck:
                _ka = _rng.Next(n); _px = ptx; _py = pty;
                _kids[_ka].Begin(KDoing.Peck);
                break;
            case Cute.Chase:
                _ka = _rng.Next(n); _px = scx; _py = scy;
                _kids[_ka].Begin(KDoing.Chase);
                break;
            case Cute.Dabble:
                leader.BeginWait(false, 0, 0); _waitIsCute = true;
                foreach (Duckling d in _kids) { d.Begin(KDoing.Dabble, 2.4f + 0.6f * (float)_rng.NextDouble()); d.Delay = 0.5f * (float)_rng.NextDouble(); }
                break;
            case Cute.Bunch:
                leader.BeginWait(false, 0, 0); _waitIsCute = true;
                _bunchFor = 3 + 2 * (float)_rng.NextDouble();
                foreach (Duckling d in _kids) d.Begin(KDoing.Bunch);
                break;
            case Cute.Flap:
                leader.BeginFlap();
                for (int k = 0; k < n; k++)
                {
                    Duckling d = _kids[k];
                    d.Begin(KDoing.Scatter);
                    float ax = d.X - leader.X, ay = d.Y - leader.Y, al = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
                    float tx = d.X + ax / al * 0.07f * _u, ty = d.Y + ay / al * 0.07f * _u;
                    d.TX = _world!.SolidG(tx, ty) ? tx : d.X; d.TY = _world.SolidG(tx, ty) ? ty : d.Y;
                    d.MaxV = 2.0f * leader.Cruise;
                }
                break;
        }
    }

    private bool AllKids(KDoing what)
    {
        foreach (Duckling d in _kids) if (d.Doing != what) return false;
        return true;
    }

    private void SendBack()
    {
        foreach (Duckling d in _kids) if (d.Doing != KDoing.Follow) d.Begin(KDoing.Scramble);
    }

    private void RunCute(float dt, Duck leader)
    {
        float u = _u, cr = leader.Cruise;
        switch (_cute)
        {
            case Cute.Lag:
                {
                    Duckling a = _kids[_ka];
                    if (_phase == 0) { if (a.Mind.Over) { a.Dip = 0; a.Begin(KDoing.Scramble); _phase = 1; } }       // it notices and scrambles
                    else if (a.Doing == KDoing.Follow) EndCute();
                    break;
                }
            case Cute.Bump:
                {
                    Duckling a = _kids[_ka], b = _kids[_kb];
                    if (_phase == 0)
                    {
                        a.TX = b.X; a.TY = b.Y; b.TX = a.X; b.TY = a.Y; a.MaxV = b.MaxV = 0.9f * cr;
                        if (a.DistTo(b.X, b.Y) < (a.Radius + b.Radius) * 1.1f || _phaseT > 2.5f)
                        {
                            _phase = 1; _phaseT = 0;
                            PointF m = new((a.Centre.X + b.Centre.X) / 2, (a.Centre.Y + b.Centre.Y) / 2);
                            _ripples.Add(m.X, m.Y, 0.2f);
                            a.StartSpin(MathF.PI, 0.9f); b.StartSpin(-MathF.PI, 0.9f);               // bumped heads, and each spins half round
                        }
                    }
                    else if (_phase == 1) { if (_phaseT > 1.3f) { a.Begin(KDoing.Scramble); b.Begin(KDoing.Scramble); _phase = 2; } }
                    else if (a.Doing == KDoing.Follow && b.Doing == KDoing.Follow) EndCute();
                    break;
                }
            case Cute.Ride: RunRide(dt, leader); break;
            case Cute.Peck:
                {
                    Duckling a = _kids[_ka];
                    if (_phase == 0)
                    {
                        PointF s = _world!.ToScreen(_px, _py);
                        if (_petals.NearestFloating(s.X, s.Y, 0.06f * u, out PointF at)) (_px, _py) = _world.ToGround(at.X, at.Y);     // it drifts: keep the aim on it
                        a.TX = _px; a.TY = _py; a.MaxV = 1.4f * cr;
                        if (a.DistTo(_px, _py) < 0.014f * u) { _phase = 1; _phaseT = 0; }
                        else if (_phaseT > 8 || a.DistTo(leader.X, leader.Y) > 0.2f * u) _phase = 2;      // too far from her: give up and go back
                    }
                    else if (_phase == 1)
                    {
                        a.Dip = 0.5f - 0.5f * MathF.Cos(_phaseT * MathF.Tau * 1.6f);               // two quick dips
                        int cycles = (int)(_phaseT * 1.6f + 0.25f);
                        if (cycles > _ringCount) { _ringCount = cycles; _ripples.Add(a.Bill.X, a.Bill.Y, 0.12f); }
                        if (_phaseT > 1.3f) _phase = 2;
                    }
                    else if (_phase == 2) { a.Dip = 0; a.Begin(KDoing.Scramble); _phase = 3; }       // then hurries back
                    else if (a.Doing == KDoing.Follow) EndCute();
                    break;
                }
            case Cute.Chase:
                {
                    Duckling a = _kids[_ka];
                    if (_phase == 0)
                    {
                        PointF c = a.Centre;
                        if (_minnows!.NearestSchool(c.X, c.Y) is PointF s)
                        {
                            (float sx, float sy) = _world!.ToGround(s.X, s.Y);
                            if (Dist(sx, sy, a.X, a.Y) < 0.25f * u && Dist(sx, sy, leader.X, leader.Y) < 0.19f * u && _world.SolidG(sx, sy)) { _px = sx; _py = sy; }
                        }
                        a.TX = _px; a.TY = _py; a.MaxV = 1.5f * cr;
                        if (a.DistTo(_px, _py) < 0.035f * u) { _phase = 1; _phaseT = 0; a.WantTip = true; _ripples.Add(a.Centre.X, a.Centre.Y, 0.15f); }
                        else if (_phaseT > 9 || a.DistTo(leader.X, leader.Y) > 0.2f * u) _phase = 2;
                    }
                    else if (_phase == 1) { a.WantTip = true; if (_phaseT > 3.0f) _phase = 2; }      // bottoms up in the flash of minnows
                    else if (_phase == 2) { a.WantTip = false; a.Begin(KDoing.Scramble); _phase = 3; }
                    else if (a.Doing == KDoing.Follow) EndCute();
                    break;
                }
            case Cute.Dabble:
                if (_phase == 0)
                {
                    bool done = true;
                    foreach (Duckling d in _kids) if (!d.Mind.Over) done = false;
                    if (done || _phaseT > 6) { SendBack(); leader.EndWait(); _waitIsCute = false; _phase = 1; }
                }
                else if (AllKids(KDoing.Follow)) EndCute();
                break;
            case Cute.Bunch:
                if (_phase == 0)
                {
                    // Under her chin: two arcs of places in front of her head, the near arc first.
                    int n = _kids.Length, per = (n + 1) / 2;
                    float h = leader.Heading, cs = MathF.Cos(h), sn = MathF.Sin(h);
                    (float hx, float hy) = leader.GroundPoint(0.12f, 0);
                    for (int k = 0; k < n; k++)
                    {
                        int ring = k & 1, idx = k >> 1, cnt = ring == 0 ? per : n - per;
                        float ang = cnt <= 1 ? 0 : -0.95f + 1.9f * idx / (cnt - 1);
                        float rad = (0.038f + 0.034f * ring) * u;
                        _kids[k].TX = hx + MathF.Cos(h + ang) * rad; _kids[k].TY = hy + MathF.Sin(h + ang) * rad;
                        _kids[k].MaxV = 2.0f * cr;
                    }
                    if (_phaseT > _bunchFor) { SendBack(); leader.EndWait(); _waitIsCute = false; _phase = 1; }
                }
                else if (AllKids(KDoing.Follow)) EndCute();
                break;
            case Cute.Flap:
                if (_phase == 0) { if (leader.Now != Duck.Doing.Flap) { SendBack(); _phase = 1; } }     // she has settled: they come back
                else if (AllKids(KDoing.Follow)) EndCute();
                break;
        }
    }

    /// <summary>
    /// The ride: the duckling scrambles to the mother's tail, climbs up onto her
    /// back over 0.7 s, rides there for 5 to 15 s keeping her place (it is pinned
    /// to her spine, at her height plus its own, turning as she turns), then slides
    /// off her side with a tiny plop ring and scrambles back to the line.
    /// </summary>
    private void RunRide(float dt, Duck leader)
    {
        Duckling a = _kids[_ka];
        float u = _u;
        (float tx, float ty) = leader.GroundPoint(1.05f, 0);
        (float bx, float by) = leader.GroundPoint(0.66f, 0);
        float rideH = leader.HeightOfBack + 0.001f;
        switch (_phase)
        {
            case 0:
                a.TX = tx; a.TY = ty; a.MaxV = 2.3f * leader.Cruise;
                if (a.DistTo(tx, ty) < 0.03f * u) { _phase = 1; _phaseT = 0; a.OnMother = true; }
                else if (_phaseT > 7) { a.Begin(KDoing.Scramble); _phase = 4; }               // could not catch her: forget it
                break;
            case 1:
                {
                    float k = Smooth(_phaseT / 0.7f);
                    a.Pin(tx + (bx - tx) * k, ty + (by - ty) * k, leader.Heading);
                    a.RideH = rideH * k;
                    if (_phaseT >= 0.7f) { _phase = 2; _phaseT = 0; }
                    break;
                }
            case 2:
                {
                    (float sx, float sy) = leader.GroundPoint(0.66f, 0.12f * MathF.Sin(_phaseT * 1.1f));
                    a.Pin(sx, sy, leader.Heading + 0.06f * MathF.Sin(_phaseT * 0.7f));
                    a.RideH = rideH;
                    if (_phaseT >= _rideFor)
                    {
                        // Off her side: whichever side is open water (beside a pad she may have none: then it rides a little longer).
                        (float lx, float ly) = leader.GroundPoint(0.66f, 1.9f);
                        (float rx, float ry) = leader.GroundPoint(0.66f, -1.9f);
                        if (_world!.SolidG(lx, ly) && _world.Clear(lx, ly) > 0.01f * u) { _slideV = 1.9f; _phase = 3; _phaseT = 0; }
                        else if (_world.SolidG(rx, ry) && _world.Clear(rx, ry) > 0.01f * u) { _slideV = -1.9f; _phase = 3; _phaseT = 0; }
                        else _rideFor += 0.5f;
                    }
                    break;
                }
            case 3:
                {
                    float k = Smooth(_phaseT / 0.7f);
                    (float ox, float oy) = leader.GroundPoint(0.66f, _slideV);
                    a.Pin(bx + (ox - bx) * k, by + (oy - by) * k, leader.Heading);
                    a.RideH = rideH * (1 - k);
                    if (_phaseT >= 0.7f)
                    {
                        a.OnMother = false; a.RideH = 0;
                        _ripples.Add(a.Centre.X, a.Centre.Y, 0.22f);                           // the tiny plop
                        a.Begin(KDoing.Scramble); _phase = 4;
                    }
                    break;
                }
            default:
                if (a.Doing == KDoing.Follow) EndCute();
                break;
        }
    }

    // ============================================================ the scene's other calls

    /// <summary>Adds every duck and duckling on the water to the scene's list of what is solid at the surface, without allocating.</summary>
    public void AddSwimmers(List<Body> into)
    {
        if (!_visiting) return;
        foreach (Duck a in _adults) if (a.OnScreen) into.Add(a);
        foreach (Duckling d in _kids) if (d.InWater && d.OnScreen) into.Add(d);
    }

    /// <summary>The ducks shove the floating petals in their way aside.</summary>
    public void PushPetals(Petals petals)
    {
        if (!_visiting) return;
        foreach (Duck a in _adults) petals.PushAside(a.Footprint, a.GroundHalf);
        foreach (Duckling d in _kids) if (d.InWater) petals.PushAside(d.Footprint, d.GroundHalf);
    }

    /// <summary>On the water, above everything that swims: after the leaping koi, before the cattails.</summary>
    public void Draw(FrameBuffer fb)
    {
        if (!_visiting) return;
        foreach (Duck a in _adults) a.DrawShadow(fb);
        foreach (Duckling d in _kids) d.DrawShadow(fb);
        foreach (Duckling d in _kids) if (!d.OnMother) d.Draw(fb);          // the line first: the grown ducks cover a duckling tucked under a chin
        foreach (Duck a in _adults) a.Draw(fb);
        foreach (Duckling d in _kids) if (d.OnMother) d.Draw(fb);           // one riding is on top of her back
    }
}

/// <summary>
/// The pond as a duck sees it: what it must swim round, and where the open
/// water is. Everything here is in GROUND coordinates (the pond as if seen
/// straight down: the screen's y stretched back out from the pond's middle by
/// Ripples.Squash), the same plane the turtle and the koi move in, so a duck
/// is round on the water and a pad is round too.
///
/// FEYNMAN VERSION: a duck has two kinds of "no". Land, a rock, a pad or the
/// log is a hard no. The cattails' foot and the irises are too (a duck would
/// be drawn through the reeds). Everything else is open water. The snake does
/// the same sum; a duck only needs the answer "how much clear water is round
/// this spot?" so its feelers can ask it ahead of the bill.
/// </summary>
internal sealed class DuckWorld
{
    public readonly Pond Pond;
    public readonly bool[] View;                   // water that no branch hides: where a shadow may be painted (painted on ALL water, a duck hidden under the blossom showed as a grey duck, wings and all, lying on the flowers)
    public readonly float U, CY;
    public readonly int W, H;

    private readonly float[] _cx, _cy, _cr;        // round things in the water, ground plane
    // MOVING things in the water this frame, as circles on the ground plane: a turtle's shell at the
    // surface, the beads of the snake's body while it swims. Refilled by Ducks.Update every frame; a
    // duck's feelers and step rule read them through Clear, so it goes round a turtle instead of over it.
    private float[] _mx = new float[48], _my = new float[48], _mr = new float[48];
    private int _mn;
    private readonly bool _hasLog;
    private readonly float _ax, _ay, _bx, _by, _lhw;   // the log: a thick line from a to b, half-thickness lhw

    public DuckWorld(Pond pond, bool[] view)
    {
        Pond = pond; View = view; U = pond.U; CY = pond.Centre.Y; W = pond.Width; H = pond.Height;
        var cx = new List<float>(); var cy = new List<float>(); var cr = new List<float>();
        void Add(PointF screen, float radius)
        {
            (float gx, float gy) = ToGround(screen.X, screen.Y);
            cx.Add(gx); cy.Add(gy); cr.Add(radius);
        }
        foreach (LilyPad p in pond.Pads) Add(p.Center, p.Radius);
        foreach (BankRock r in pond.Rocks) Add(r.Centre, r.Radius);
        Add(pond.CattailSpot, U * 0.075f);                 // the cattails stand in front of the ducks: keep the whole clump clear
        foreach ((PointF root, _) in pond.IrisSpots) Add(root, U * 0.05f);
        _cx = cx.ToArray(); _cy = cy.ToArray(); _cr = cr.ToArray();
        if (pond.Log is Log log)
        {
            _hasLog = true;
            (_ax, _ay) = ToGround(log.At(0).X, log.At(0).Y);
            (_bx, _by) = ToGround(log.At(1).X, log.At(1).Y);
            _lhw = log.Width * 0.5f;
        }
    }

    public (float X, float Y) ToGround(float sx, float sy) => (sx, CY + (sy - CY) / Ripples.Squash);

    /// <summary>Forgets last frame's moving things.</summary>
    public void ClearMoving() => _mn = 0;

    /// <summary>Adds a moving thing for this frame: a circle on the ground plane (ground px).</summary>
    public void AddMoving(float gx, float gy, float r)
    {
        if (_mn == _mx.Length) { Array.Resize(ref _mx, _mn * 2); Array.Resize(ref _my, _mn * 2); Array.Resize(ref _mr, _mn * 2); }
        _mx[_mn] = gx; _my[_mn] = gy; _mr[_mn] = r; _mn++;
    }
    public PointF ToScreen(float gx, float gy) => new(gx, CY + (gy - CY) * Ripples.Squash);

    /// <summary>Is a screen point off the picture altogether (where the pond simply carries on)?</summary>
    public bool OffScreen(float sx, float sy) => sx < 3 || sy < 3 || sx >= W - 3 || sy >= H - 3;      // the last few pixels count as beyond the edge (a duck on the very rim is half out of sight, and the bank's last row was a trap there)

    /// <summary>
    /// Open water at a ground point? Under a pad, a rock, a bank plant or on
    /// land it is not. Beyond the screen's edge the pond goes on, so a point
    /// off the picture reads the last pixel on the picture in its row or
    /// column: water beside water, LAND beside the land corner. (The first
    /// version called everything off the screen water, and a drake skirted
    /// the bank along that strip into the bottom corner, where the bank meets
    /// both edges, and sat in the grass for the rest of the run.)
    /// </summary>
    public bool WaterG(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = Math.Clamp((int)s.X, 0, W - 1), iy = Math.Clamp((int)s.Y, 0, H - 1);
        return Pond.Water[iy * W + ix];
    }

    /// <summary>
    /// The distance from a ground point to the edge of the nearest round thing
    /// or the log (negative inside it); U if nothing is near.
    /// </summary>
    public float Clear(float gx, float gy, bool moving = true)
    {
        float best = U;
        for (int i = 0; i < _cx.Length; i++)
        {
            float dx = gx - _cx[i], dy = gy - _cy[i];
            float d = MathF.Sqrt(dx * dx + dy * dy) - _cr[i];
            if (d < best) best = d;
        }
        for (int i = 0; moving && i < _mn; i++)
        {
            float dx = gx - _mx[i], dy = gy - _my[i];
            float d = MathF.Sqrt(dx * dx + dy * dy) - _mr[i];
            if (d < best) best = d;
        }
        if (_hasLog)
        {
            float ex = _bx - _ax, ey = _by - _ay, e2 = MathF.Max(1e-3f, ex * ex + ey * ey);
            float t = Math.Clamp(((gx - _ax) * ex + (gy - _ay) * ey) / e2, 0, 1);
            float dx = gx - (_ax + ex * t), dy = gy - (_ay + ey * t);
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy) - _lhw);
        }
        return best;
    }

    /// <summary>
    /// How much clear water there is for a body about "w" wide (half width) at a
    /// ground point: 0 if the spot itself is land, a pad or a rock, a little if
    /// the ring of water one width out is broken (the bank is right there), more
    /// if the ring two widths out is, else the distance to the nearest round
    /// thing. Off the screen it is wide open. Nine mask lookups and a loop over
    /// the round things: cheap enough to ask a dozen times a frame.
    /// </summary>
    public float RoomG(float gx, float gy, float w)
    {
        PointF s = ToScreen(gx, gy);
        if (!WaterG(gx, gy)) return 0;                                    // land, a pad, a plant: on the picture or beyond its edge (WaterG)
        if (OffScreen(s.X, s.Y)) return MathF.Min(U, Clear(gx, gy));        // open water off the picture: only the things that reach past the edge count
        float room = Clear(gx, gy);
        if (room <= 0) return 0;
        if (!RingWater(gx, gy, w)) return MathF.Min(room, w * 0.2f);
        if (!RingWater(gx, gy, w * 2.2f)) return MathF.Min(room, w * 0.8f);
        return room;
    }

    private bool RingWater(float gx, float gy, float r) =>
        WaterG(gx + r, gy) && WaterG(gx - r, gy) && WaterG(gx, gy + r) && WaterG(gx, gy - r);

    /// <summary>Is this ground point somewhere a swimmer may be (water, and outside every round thing)?</summary>
    public bool SolidG(float gx, float gy) => WaterG(gx, gy) && Clear(gx, gy) > 0;

    /// <summary>The same against the fixed world only: for a shove OFF something in the moving list (tested against that list too, every push off the snake was refused, since the duck was still inside it).</summary>
    public bool SolidFixedG(float gx, float gy) => WaterG(gx, gy) && Clear(gx, gy, moving: false) > 0;

    // ------------------------------------------------------------------ where to come in, where to go

    /// <summary>
    /// A place on a screen edge where a family can swim in (or out): water for
    /// the first 0.22u inward and clear of every round thing. The pond reaches
    /// every edge except the land corner, so most edge points do; this tries
    /// random ones until one does. "exclude" is an edge already used (0 left,
    /// 1 right, 2 top, 3 bottom), or -1.
    /// </summary>
    public bool FindEdge(Random rng, int exclude, out int edge, out PointF p, out PointF inward)
    {
        edge = 0; p = default; inward = default;
        for (int tries = 0; tries < 120; tries++)
        {
            int e = rng.Next(4);
            if (e == exclude) continue;
            float t = 0.12f + 0.76f * (float)rng.NextDouble();
            PointF q = e switch { 0 => new PointF(0, t * H), 1 => new PointF(W - 1, t * H), 2 => new PointF(t * W, 0), _ => new PointF(t * W, H - 1) };
            PointF n = e switch { 0 => new PointF(1, 0), 1 => new PointF(-1, 0), 2 => new PointF(0, 1), _ => new PointF(0, -1) };
            bool ok = true;
            foreach (float d in (ReadOnlySpan<float>)[0.03f, 0.10f, 0.16f, 0.22f])
            {
                float sx = Math.Clamp(q.X + n.X * d * U, 0, W - 1), sy = Math.Clamp(q.Y + n.Y * d * U, 0, H - 1);
                (float gx, float gy) = ToGround(sx, sy);
                if (!Pond.Water[(int)sy * W + (int)sx] || Clear(gx, gy) < 0.07f * U) { ok = false; break; }
            }
            if (!ok) continue;
            edge = e; p = q; inward = n;
            return true;
        }
        return false;
    }

    /// <summary>
    /// A spot to swim to in open water that people can see: water, not under a
    /// branch, with room round it, at least "minDist" from "from" (ground px).
    /// </summary>
    public bool FindWaypoint(Random rng, float fromX, float fromY, float minDist, out PointF ground)
    {
        ground = default;
        for (int tries = 0; tries < 80; tries++)
        {
            float x = (0.08f + 0.84f * (float)rng.NextDouble()) * W, y = (0.08f + 0.84f * (float)rng.NextDouble()) * H;
            int i = (int)y * W + (int)x;
            if (!Pond.Water[i] || !Pond.Open[i] || Pond.RoomAt(x, y) < 0.1f * U) continue;
            (float gx, float gy) = ToGround(x, y);
            if (Clear(gx, gy) < 0.1f * U) continue;
            float dx = gx - fromX, dy = gy - fromY;
            if (dx * dx + dy * dy < minDist * minDist) continue;
            ground = new PointF(gx, gy);
            return true;
        }
        return false;
    }
}

/// <summary>
/// A grown duck seen from above: a spot-billed duck, mallard, mandarin or teal,
/// drake or hen.
///
/// WHAT A DUCK DOES (watch one at any pond): it paddles along with its head
/// rocking a touch fore and aft in time with its feet, leaving a small ring off
/// each side of its breast every few moments, so a V of rings spreads out
/// behind it. Now and then it LOOKS about (the head turns up to 60 degrees,
/// the body does not follow, like the turtle's neck), DABBLES (tips up: head
/// and neck go under and vanish into a ring, the tail end lifts clear for a
/// second or two, then it comes up and shakes), PREENS (the head turns right
/// back over one shoulder and nibbles the flank), and now and then FLAPS (it
/// rises on the water, throws its wings wide three times, throws a little
/// spray, settles and wags its tail). A hen with ducklings stops and WAITS
/// for a straggler and looks back at it; and if the snake comes close she
/// herds the family away from it.
///
/// THE MIND (Core/Minds). Two dials rolled when the duck is born (each visit
/// is a new duck): CALM (flaps less, cruises longer between fusses) and FUSSY
/// (looks about, dabbles and preens more). One thing at a time (Doing:
/// Cruise, Look, Dabble, Preen, Flap, Wait, Herd); when the timer runs out
/// Decide() rolls weighted dice for the next. The reflexes underneath (feelers
/// round the pads, rocks, log and reeds, the "never stuck" step rule, an eased
/// turn rate) keep the last word.
///
/// THE FOUR GATES for a living thing here. VISIBLE: it floats ON the water, the
/// most visible thing in the pond. PERSPECTIVE: it is a teardrop on the ground
/// plane, squashed by the slant of the view, and its height is a lift up the
/// screen (back 0.01u, head 0.03u) with a shadow on the water that hugs it
/// (a shadow thrown far from its thing reads as lift, Light.cs). TURNING: the
/// body is rigid and turns as a whole, about its middle, on an eased turn rate;
/// the neck bends. MOVEMENT: it paddles, rocks its head, dabbles, preens,
/// flaps; nothing slides or flips.
///
/// HOW IT IS DRAWN. The body (neck, back and tail) is a Body along a straight
/// spine of 13 points from bill to tail, painted from the baked colours
/// (Look.Skin); the head, bill, eyes and the species' marks are ovals on top, at
/// the head's own height. The ground position is the BODY'S MIDDLE (not the
/// nose), so turning on the spot swings the bill and tail round the middle.
/// </summary>
internal sealed class Duck : Body
{
    public enum Doing { Cruise, Look, Dabble, Preen, Flap, Wait, Herd }

    private const int Points = 13;
    // How far along from the bill tip (0) to the tail tip (1) each spine point is, and how wide the body is there (a share of the half width).
    private static readonly float[] S = [0f, 0.10f, 0.19f, 0.27f, 0.36f, 0.44f, 0.52f, 0.60f, 0.68f, 0.76f, 0.84f, 0.92f, 1.0f];
    private static readonly float[] W = [0.05f, 0.20f, 0.40f, 0.28f, 0.55f, 0.86f, 0.98f, 1.0f, 0.96f, 0.82f, 0.62f, 0.36f, 0.07f];
    private const int MidAt = 7, NeckIdx = 4;
    private const float Mid = 0.60f, NeckAt = 0.36f, HeadEnd = 0.205f;
    private const float BodyH = 0.010f, HeadH = 0.030f;       // heights above the water, in u

    private readonly DuckWorld _w;
    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly float _u, _wide, _calm, _fussy;
    public readonly Species Sp;
    public readonly Look Look;
    public readonly bool Drake;
    public readonly float Len, Cruise;

    /// <summary>Where the body's middle is, ground pixels; which way it faces (radians); how fast it goes (ground pixels a second).</summary>
    public float X, Y, Heading, Speed;
    /// <summary>Where the leader wants it to go (ground pixels), and how fast as a share of its cruise.</summary>
    public float GoalX, GoalY, Pace = 1;
    /// <summary>May it dabble, preen and flap just now? (Not while its ducklings are in the middle of something.)</summary>
    public bool AllowBig = true;

    private float _turnRate, _neck, _neckGoal, _phase, _t, _bob, _tip, _flapK, _wingK, _wag, _wakeT, _herdCool, _dabHold;
    private float _lookX, _lookY, _herdX, _herdY, _stuckT, _unstickT, _unstickH, _inT, _evacT, _sinceUnstick = 99;
    private bool _hasLook, _blocked, _ring1, _ring2;
    private int _flapRings, _sprays;
    private float _headAlpha = 1;
    private readonly Mind<Doing> _mind = new(Doing.Cruise);
    private Doing _doing => _mind.Doing;

    private readonly float[] _h = new float[Points];           // how high each spine point is above the water, u
    private readonly PointF[] _g = new PointF[Points];         // each spine point on the water (ground, squashed to the screen, NOT lifted)
    private readonly float[] _gHalf = new float[Points];
    private readonly PointF[] _lifted = new PointF[Points];    // the drawn spine, kept aside while the shadow borrows Spine
    // A handful of spray droplets for the flap, reused every time.
    private readonly float[] _dx = new float[16], _dy = new float[16], _dvx = new float[16], _dvy = new float[16], _dl = new float[16];
    private int _dropAt;

    public Duck(DuckWorld w, Ripples ripples, Random rng, Species sp, Look look, bool drake, float cruise, float calm, float fussy) : base(Points)
    {
        _w = w; _pond = w.Pond; _ripples = ripples; _rng = rng; _u = w.U;
        Sp = sp; Look = look; Drake = drake;
        Len = sp.Len * _u; _wide = Len * 0.19f; Cruise = cruise;
        _calm = calm; _fussy = fussy;
        for (int i = 0; i < Points; i++) Along[i] = S[i];
        _mind.Begin(Doing.Cruise, 4 + 6 * (float)rng.NextDouble());
        _phase = (float)rng.NextDouble() * MathF.Tau;
        _herdCool = 0;
    }

    // ------------------------------------------------------------------ what the director reads

    public Doing Now => _doing;
    public string State => _doing.ToString();
    public float Wide => _wide;
    public float Calm => _calm;
    /// <summary>Its middle on the water, screen pixels.</summary>
    public PointF Centre => _g[MidAt];
    /// <summary>Its head on the water (not lifted), screen pixels: what the small animals keep clear of.</summary>
    public PointF HeadSpot => _g[2];
    /// <summary>The drawn bill tip and tail tip, screen pixels (for logs).</summary>
    public PointF Bill => Spine[0];
    public PointF TailTip => Spine[Points - 1];
    /// <summary>Where it is, as the shapes and the petals see it: on the water, unlifted.</summary>
    public ReadOnlySpan<PointF> Footprint => _g;
    public ReadOnlySpan<float> GroundHalf => _gHalf;
    public float HeightOfBack => BodyH + _flapK * 0.02f;
    public float NeckAngle => _neck;
    public float FlapAmount => _flapK;
    public float Tip => _tip;
    public bool Blocked => _blocked;

    /// <summary>A push from the other adult of a pair (ground px), taken only onto open water.</summary>
    public void Shove(float sx, float sy)
    {
        if (_w.SolidFixedG(X + sx, Y + sy)) { X += sx; Y += sy; }
    }

    /// <summary>Is any of it on the picture?</summary>
    public bool OnScreen
    {
        get
        {
            PointF c = _g[MidAt];
            float m = Len * 0.8f;
            return c.X > -m && c.X < _w.W + m && c.Y > -m && c.Y < _w.H + m;
        }
    }

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Puts it down at a ground spot facing a heading (the start of a visit), body laid out.</summary>
    public void Place(float gx, float gy, float heading)
    {
        X = gx; Y = gy; Heading = heading; Speed = 0; GoalX = gx; GoalY = gy;
        LaySpine();
    }

    private void Start(Doing d, float seconds)
    {
        _mind.Begin(d, seconds);
        _t = 0;
        if (d != Doing.Wait) _hasLook = false;
    }

    /// <summary>Stand and wait (a straggler to look back at, or ducklings gathering). Lasts until EndWait.</summary>
    public void BeginWait(bool look, float lookX, float lookY)
    {
        if (_doing == Doing.Herd) return;
        Start(Doing.Wait, Mind<Doing>.Forever);
        _hasLook = look; _lookX = lookX; _lookY = lookY;
    }

    /// <summary>Keep looking at a spot while waiting (the straggler moves).</summary>
    public void LookAt(float gx, float gy) { _lookX = gx; _lookY = gy; _hasLook = true; }

    public void EndWait()
    {
        if (_doing != Doing.Wait) return;
        _hasLook = false;
        Start(Doing.Cruise, 4 + 5 * (float)_rng.NextDouble());
    }

    public void BeginFlap()
    {
        Start(Doing.Flap, 2.6f);
        _flapRings = 0; _sprays = 0;
    }

    /// <summary>The snake is near: swing away from it at 1.5 times the cruise for a few seconds (not again for ten).</summary>
    public bool StartHerd(PointF snakeScreen)
    {
        if (_herdCool > 0) return false;
        (float sx, float sy) = _w.ToGround(snakeScreen.X, snakeScreen.Y);
        float away = MathF.Atan2(Y - sy, X - sx);
        // Where to herd them: straight away from the snake if that is open water on the picture with room, else the nearest way round
        // either side (a place in a corner of pads is a trap, and a family herded off the edge is a family that has gone).
        float inset = 0.1f * _u;
        _herdX = X + MathF.Cos(away) * 0.3f * _u; _herdY = Y + MathF.Sin(away) * 0.3f * _u;
        foreach (float off in (ReadOnlySpan<float>)[0f, 0.5f, -0.5f, 1.0f, -1.0f, 1.6f, -1.6f])
        {
            float gx = X + MathF.Cos(away + off) * 0.3f * _u, gy = Y + MathF.Sin(away + off) * 0.3f * _u;
            PointF gs = _w.ToScreen(gx, gy);
            if (gs.X < inset || gs.Y < inset || gs.X > _w.W - inset || gs.Y > _w.H - inset) continue;
            if (_w.RoomG(gx, gy, _wide * 1.5f) < 0.06f * _u || _pond.RoomAt(gs.X, gs.Y) < 0.08f * _u) continue;
            _herdX = gx; _herdY = gy;
            break;
        }
        Start(Doing.Herd, 3.5f + 1.5f * (float)_rng.NextDouble());
        _herdCool = 10;
        return true;
    }

    // ------------------------------------------------------------------ the dice

    /// <summary>When a doing ends: roll for the next. Fussy ducks look, dabble and preen more; calm ones flap less.</summary>
    private void Decide()
    {
        bool fromCruise = _doing == Doing.Cruise;
        Span<float> w = stackalloc float[5];            // Cruise, Look, Dabble, Preen, Flap
        w[0] = fromCruise ? 2.2f + 2f * _calm : 4f;
        w[1] = (0.7f + 1.5f * _fussy) * (fromCruise ? 1f : 0.4f);
        w[2] = AllowBig && fromCruise ? 0.5f + 1.1f * _fussy : 0f;
        w[3] = AllowBig && fromCruise ? 0.35f + 0.5f * _fussy + 0.3f * _calm : 0f;
        w[4] = AllowBig && fromCruise ? 0.22f * (1.3f - _calm) : 0f;
        float side = _rng.Next(2) == 0 ? 1 : -1;
        switch (Dice.Pick(_rng, w))
        {
            case 1:
                Start(Doing.Look, 1.5f + 1.6f * (float)_rng.NextDouble());
                _neckGoal = side * (0.45f + 0.6f * (float)_rng.NextDouble());     // up to 60 degrees either way
                break;
            case 2:
                _dabHold = 1f + 2f * (float)_rng.NextDouble();                    // tipped up for one to three seconds
                Start(Doing.Dabble, 0.35f + _dabHold + 0.35f + 0.7f);             // tip up, hold, come up, shake
                _ring1 = _ring2 = false;
                break;
            case 3:
                Start(Doing.Preen, 2f + 3f * (float)_rng.NextDouble());
                _neckGoal = side * 2.35f;                                         // the head right back over the shoulder
                break;
            case 4:
                BeginFlap();
                break;
            default:
                Start(Doing.Cruise, 5f + 7f * (float)_rng.NextDouble());
                break;
        }
    }

    // ------------------------------------------------------------------ the frame

    public void Update(float dt)
    {
        _t += dt;
        _mind.Tick(dt);
        _herdCool -= dt;
        if (_mind.Over) Decide();

        // ---- what this doing asks for: a speed, a neck angle, and its animation ----
        float want = 0, neckWant = 0, neckRate = 5f;
        switch (_doing)
        {
            case Doing.Cruise:
                want = Cruise * Pace * (1 + 0.1f * MathF.Sin(2 * _phase));
                neckWant = Math.Clamp(_turnRate * 0.35f, -0.4f, 0.4f);          // the head leads a turn
                break;
            case Doing.Look:
                want = Cruise * Pace * (1 + 0.1f * MathF.Sin(2 * _phase));
                neckWant = _mind.Left > 0.5f ? _neckGoal : 0;
                break;
            case Doing.Herd:
                want = Cruise * 1.5f * (1 + 0.1f * MathF.Sin(2 * _phase));
                neckWant = Math.Clamp(_turnRate * 0.35f, -0.4f, 0.4f);
                break;
            case Doing.Dabble:
                {
                    want = Cruise * 0.06f;
                    float down = 0.35f + _dabHold;                                // when it starts to come up
                    if (_t < 0.35f) _tip = _t / 0.35f;
                    else if (_t < down) _tip = 1;
                    else if (_t < down + 0.35f) _tip = 1 - (_t - down) / 0.35f;
                    else _tip = 0;
                    if (!_ring1 && _t > 0.3f) { _ring1 = true; Ring(0.35f); }
                    if (!_ring2 && _t > down + 0.2f) { _ring2 = true; Ring(0.3f); }
                    float shakeT = _t - down - 0.35f;
                    if (shakeT > 0) { neckWant = MathF.Sin(shakeT * MathF.Tau * 6) * 0.45f * Math.Max(0, 1 - shakeT / 0.7f); neckRate = 30f; }   // the shake as it comes up
                    break;
                }
            case Doing.Preen:
                neckWant = _mind.Left > 0.5f && _t > 0.4f ? _neckGoal + MathF.Sin(_t * 13) * 0.12f : (_t <= 0.4f ? _neckGoal * Smooth(_t / 0.4f) : 0);
                neckRate = 9f;
                break;
            case Doing.Flap:
                {
                    want = 0;
                    _flapK = _t < 0.4f ? Smooth(_t / 0.4f) : _t < 1.1f ? 1 : _t < 1.5f ? 1 - Smooth((_t - 1.1f) / 0.4f) : 0;
                    _wingK = _t > 0.4f && _t < 1.1f ? 0.5f - 0.5f * MathF.Cos((_t - 0.4f) / 0.2f * MathF.Tau) : 0;   // three beats over 0.6 s (down stroke at 1)
                    _wag = _t > 1.5f ? MathF.Sin((_t - 1.5f) * MathF.Tau * 7) * Math.Max(0, 1 - (_t - 1.5f) / 1.0f) : 0;
                    if (_flapRings < 3 && _t > 0.4f + 0.3f * _flapRings) { _flapRings++; Ring(0.5f); }
                    if (_sprays < 3 && _t > 0.5f + 0.2f * _sprays) { _sprays++; Spray(); }
                    break;
                }
            case Doing.Wait:
                if (_hasLook)
                {
                    float rel = Wrap(MathF.Atan2(_lookY - Y, _lookX - X) - Heading);
                    neckWant = Math.Clamp(rel, -1.05f, 1.05f);
                }
                break;
        }
        if (_doing != Doing.Flap) { _flapK += (0 - _flapK) * (1 - MathF.Exp(-dt * 8f)); _wingK = 0; _wag *= MathF.Exp(-dt * 6f); }
        if (_doing != Doing.Dabble) _tip += (0 - _tip) * (1 - MathF.Exp(-dt * 8f));
        _headAlpha = Math.Clamp(1 - Smooth(_tip) * 1.7f, 0f, 1f);
        _neck = neckRate >= 30f ? neckWant : _neck + (neckWant - _neck) * (1 - MathF.Exp(-dt * neckRate));

        // ---- speed, stroke, head rock ----
        Speed += (want - Speed) * (1 - MathF.Exp(-dt * 3f));
        float moving = Math.Min(1, Speed / MathF.Max(1e-3f, Cruise));
        _phase = (_phase + MathF.Tau * 1.3f * dt * (0.25f + moving)) % MathF.Tau;
        _bob = MathF.Sin(_phase * 2) * moving;

        // ---- steering: a pull toward the goal, the feelers, an eased turn ----
        float turn = 0, most = 0.9f;
        if (_unstickT > 0)
        {
            // Pinned (pushing for over a second without getting anywhere): ignore the goal for a couple of seconds and swing to the
            // clearest way out, found by feeling all the way round.
            _unstickT -= dt;
            turn = Wrap(_unstickH - Heading) * 3f;
            most = 1.6f;
        }
        else if (want > 0.05f * Cruise)
        {
            float gx = _doing == Doing.Herd ? _herdX : GoalX, gy = _doing == Doing.Herd ? _herdY : GoalY;
            turn = Wrap(MathF.Atan2(gy - Y, gx - X) - Heading) * (_doing == Doing.Herd ? 1.6f : 0.9f);
            Feel(ref turn, ref most);
        }
        else if (_doing == Doing.Wait && _hasLook)
        {
            // Standing, it can still swing round to look back at a straggler: the body turns slowly about its middle once the head can turn no further.
            float rel = Wrap(MathF.Atan2(_lookY - Y, _lookX - X) - Heading);
            if (MathF.Abs(rel) > 1.0f) turn = MathF.Sign(rel) * 0.4f;
        }
        // The turning circle of a body this long: no tighter than about a third of its length when moving.
        most = MathF.Min(most, MathF.Max(Speed, Len * 0.3f) / (0.30f * Len));
        turn = Math.Clamp(turn, -most, most);
        // The rate of turn is eased, never set outright: a steering sum that flips sign frame to frame is laid into the heading as a jitter.
        _turnRate += (turn - _turnRate) * (1 - MathF.Exp(-dt * 6f));
        // A turn swings the bill and the tail round the middle: never into a pad or the bank (it would then be inside it, and stuck).
        float newHeading = Wrap(Heading + _turnRate * dt);
        if (BodyRoom(X, Y, newHeading) <= 0 && BodyRoom(X, Y, Heading) > 0) _turnRate = 0;
        else Heading = newHeading;

        // ---- forward, unless that would put it on something ----
        float step = Speed * dt;
        float x0 = X, y0 = Y;
        if (step > 1e-4f)
        {
            float nx = X + MathF.Cos(Heading) * step, ny = Y + MathF.Sin(Heading) * step;
            float roomN = BodyRoom(nx, ny), roomH = BodyRoom(X, Y);
            _blocked = !(roomN > 0 || roomN > roomH);                            // never stuck, never onto land: a step is allowed if it has water, or if it has more than here
            if (!_blocked) { X = nx; Y = ny; }
        }
        else _blocked = false;
        // Already inside something (a pad's soft edge, a rock's circle): ease out toward the clearest water, never staying put.
        if (BodyRoom(X, Y) <= 0) { _inT += dt; if (_inT > 0.3f) Rescue(MathF.Max(0.35f, step)); }
        else _inT = 0;
        // Watchdog: wanting to go, and not getting anywhere.
        if (want > 0.3f * Cruise && _unstickT <= 0 && (X - x0) * (X - x0) + (Y - y0) * (Y - y0) < 0.04f * step * step) _stuckT += dt;
        else _stuckT = MathF.Max(0, _stuckT - 2 * dt);
        _sinceUnstick += dt; _evacT -= dt;
        if (_stuckT > 1.2f)
        {
            // Pinned again soon after the last try: the trap is a narrow pocket, so for three seconds only its middle has to be in clear water.
            if (_sinceUnstick < 6) _evacT = 3;
            _stuckT = 0; _unstickT = 2.5f; _unstickH = ClearestWay(); _sinceUnstick = 0;
        }

        // ---- the wake: a ring off each side of the breast every 0.3 s while it paddles ----
        _wakeT -= dt;
        // The wake is decoration: it yields when the pond is near its ring cap (Ripples.Crowded), so a koi's gulp or a
        // dragonfly's tap is never refused because a duck was paddling. A pair every 0.45 s keeps one adult's wake to about
        // twelve live rings.
        if (_wakeT <= 0 && Speed > 0.45f * Cruise && _doing != Doing.Dabble && !_ripples.Crowded)
        {
            _wakeT = 0.45f;
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
            {
                PointF p = BreastPoint(side);
                _ripples.Add(p.X, p.Y, 0.2f, thin: true);
            }
        }

        // ---- spray droplets fly and fall ----
        for (int i = 0; i < _dl.Length; i++)
        {
            if (_dl[i] <= 0) continue;
            _dl[i] -= dt;
            _dx[i] += _dvx[i] * dt; _dy[i] += _dvy[i] * dt;
            _dvy[i] += _u * 1.6f * dt;
        }
        LaySpine();
    }

    private PointF BreastPoint(int side)
    {
        float cs = MathF.Cos(Heading), sn = MathF.Sin(Heading), ahead = (Mid - 0.42f) * Len, lat = side * _wide * 0.85f;
        return _w.ToScreen(X + cs * ahead - sn * lat, Y + sn * ahead + cs * lat);
    }

    private void Ring(float strength)
    {
        PointF p = BreastPoint(0);
        _ripples.Add(p.X, p.Y, strength);
    }

    /// <summary>A few droplets thrown off each wing tip on a down stroke.</summary>
    private void Spray()
    {
        float cs = MathF.Cos(Heading), sn = MathF.Sin(Heading);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            float lat = side * 0.75f * Len, back = -0.2f * Len;
            PointF tip = _w.ToScreen(X + cs * back - sn * lat, Y + sn * back + cs * lat);
            for (int k = 0; k < 3; k++)
            {
                int i = _dropAt = (_dropAt + 1) % _dl.Length;
                _dx[i] = tip.X; _dy[i] = tip.Y - HeightOfBack * _u * Light.Lift;
                _dvx[i] = (float)(_rng.NextDouble() - 0.5) * _u * 0.12f + side * _u * 0.05f;
                _dvy[i] = -_u * (0.10f + 0.12f * (float)_rng.NextDouble());
                _dl[i] = 0.45f;
            }
        }
    }

    // ------------------------------------------------------------------ feeling the way

    /// <summary>The least clear water a body of this size has at three places along itself (bill, middle, tail) if its middle were here.</summary>
    private float BodyRoom(float gx, float gy) => BodyRoom(gx, gy, Heading);

    private float BodyRoom(float gx, float gy, float h)
    {
        if (_evacT > 0) return _w.RoomG(gx, gy, _wide * 0.6f);               // getting out of a trap: only the middle must be in clear water
        float cs = MathF.Cos(h), sn = MathF.Sin(h);
        float r = _wide * 0.6f;
        float a = _w.RoomG(gx + cs * 0.55f * Len, gy + sn * 0.55f * Len, r);
        float b = _w.RoomG(gx, gy, r);
        float c = _w.RoomG(gx - cs * 0.36f * Len, gy - sn * 0.36f * Len, r);
        return MathF.Min(a, MathF.Min(b, c));
    }

    /// <summary>
    /// Clear water along a heading, felt at five places from where the bill WOULD be (a little over half a body length from the middle,
    /// along that heading) out to "dist" beyond it. Measured from the middle, not from the bill as it is now: a bill that is already
    /// against a pad has no room in any direction, and the search then could not tell which way to turn (it held a duck bill-to-pad for
    /// minutes); from the middle each heading asks about its own bill.
    /// </summary>
    private float ClearAlong(float a, float dist)
    {
        float ca = MathF.Cos(a), sa = MathF.Sin(a);
        // One more feeler close in, just ahead of the middle: a duck pressed against a rock with its bill out over open water sees
        // "clear" ahead and so never turned away from the rock its middle was touching.
        float best = _w.RoomG(X + ca * Len * 0.28f, Y + sa * Len * 0.28f, _wide * 0.6f);
        foreach (float f in (ReadOnlySpan<float>)[0.0f, 0.2f, 0.45f, 0.7f, 1f])
        {
            float d = Len * 0.55f + dist * f;
            best = MathF.Min(best, _w.RoomG(X + ca * d, Y + sa * d, _wide * 0.6f));
        }
        return best;
    }

    /// <summary>
    /// The snake's trick, for a duck: look a little way past the bill and, if
    /// something is in the way (or a step was refused last frame), try headings
    /// either side and take the smallest turn that clears it, then settle back
    /// toward the goal. A pad, a rock, the log, the reeds or the bank are all
    /// "the way".
    /// </summary>
    private void Feel(ref float turn, ref float most)
    {
        float look = Len * 0.55f + Speed * 1.3f + _u * 0.04f;
        float margin = _wide * 1.25f;
        float clearAhead = _blocked ? 0 : ClearAlong(Heading, look);
        if (clearAhead >= margin) return;
        float best = Heading, bestScore = MathF.Min(clearAhead, margin * 2);
        for (int k = 1; k <= 6; k++)
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
            {
                float a = Heading + side * k * 0.32f;
                float score = MathF.Min(ClearAlong(a, look), margin * 2) - k * margin * 0.1f;
                if (score > bestScore) { bestScore = score; best = a; }
            }
        // The nearer the obstacle, the less the goal's pull counts: a goal pull and an avoidance turn that simply add can cancel
        // and hold the bill against a pad for ever (the first build did, for four minutes).
        float urgency = Math.Clamp(1 - clearAhead / margin, 0, 1);
        turn = turn * (1 - 0.85f * urgency) + Wrap(best - Heading) * (2f + 3f * urgency);
        most = MathF.Max(most, 1.8f);
    }

    /// <summary>Moves the body a little toward whichever of eight ways has the most room (used only when it is already inside something).</summary>
    private void Rescue(float by)
    {
        float best = float.MinValue, bx = 0, byy = 0;
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.PI / 4, ca = MathF.Cos(a), sa = MathF.Sin(a);
            float score = BodyRoom(X + ca * by * 3, Y + sa * by * 3);
            if (score > best) { best = score; bx = ca; byy = sa; }
        }
        X += bx * by; Y += byy * by;
    }

    /// <summary>The heading with the most clear water ahead, tried every 15 degrees all the way round (a slight liking for the goal's side breaks ties).</summary>
    private float ClearestWay()
    {
        float look = Len * 0.55f + _u * 0.1f, margin = _wide * 1.25f, toGoal = MathF.Atan2(GoalY - Y, GoalX - X), best = Heading, bestScore = float.MinValue;
        for (int k = 0; k < 24; k++)
        {
            float a = k * MathF.Tau / 24;
            float score = MathF.Min(ClearAlong(a, look), margin * 2) - MathF.Abs(Wrap(a - toGoal)) * margin * 0.1f;
            if (score > bestScore) { bestScore = score; best = a; }
        }
        return best;
    }

    // ------------------------------------------------------------------ the body

    /// <summary>The ground point of body point i at "v" across (-1 left edge to +1 right), before any lift.</summary>
    public (float X, float Y) GroundPoint(float s, float v)
    {
        float cs = MathF.Cos(Heading), sn = MathF.Sin(Heading);
        float ahead = (Mid - s) * Len;
        return (X + cs * ahead - sn * v * _wide, Y + sn * ahead + cs * v * _wide);
    }

    /// <summary>
    /// Lays the 13 spine points out: the body straight back from its middle, the
    /// neck points swung about the body's front by the neck angle (the head
    /// leads a turn and looks about; the body never moves for it), the tail end
    /// wagged. Each point also gets its HEIGHT: 0.01u for the back, 0.03u for the
    /// head, and the dabble and flap change them. Height is drawn as a lift up
    /// the screen (Light.Lift), the shadow stays on the water.
    /// </summary>
    private void LaySpine()
    {
        float cs = MathF.Cos(Heading), sn = MathF.Sin(Heading);
        float hcs = MathF.Cos(Heading + _neck), hsn = MathF.Sin(Heading + _neck);
        float px = X + cs * (Mid - NeckAt) * Len, py = Y + sn * (Mid - NeckAt) * Len;        // where the neck leaves the body
        float tip = Smooth(_tip);
        float bodyH = BodyH + _flapK * 0.02f;
        float headH = HeadH + (-0.042f) * tip;                                              // a dabbling head goes under
        for (int i = 0; i < Points; i++)
        {
            float tail = S[i] > Mid ? (S[i] - Mid) / (1 - Mid) : 0;
            float w = _wide * W[i] * (1 + 0.12f * tip * tail);                              // the lifted tail is nearer us, so a touch bigger
            float cx, cy, rx, ry;
            if (i >= NeckIdx)
            {
                float ahead = (Mid - S[i]) * Len;
                float lat = S[i] > 0.76f ? _wag * (S[i] - 0.76f) / 0.24f * Len * 0.10f : 0;
                cx = X + cs * ahead - sn * lat; cy = Y + sn * ahead + cs * lat;
                rx = -sn; ry = cs;
            }
            else
            {
                float ahead = (NeckAt - S[i]) * Len * (1 + 0.05f * _bob);                     // the head rocks fore and aft with the stroke
                cx = px + hcs * ahead; cy = py + hsn * ahead;
                rx = -hsn; ry = hcs;
            }
            PointF c = _w.ToScreen(cx, cy), l = _w.ToScreen(cx - rx * w, cy - ry * w), r = _w.ToScreen(cx + rx * w, cy + ry * w);
            _g[i] = c;
            _gHalf[i] = MathF.Max(1f, Dist(l, r) / 2);
            HalfWidth[i] = _gHalf[i];
            Shape.Points[i] = new System.Numerics.Vector3(cx, cy, 0);
            Shape.Radius[i] = w;
            float h = i <= 2 ? headH : i == 3 ? (headH + bodyH) * 0.5f : i <= 5 ? bodyH - 0.004f * tip : bodyH + 0.022f * tip * Smooth((S[i] - 0.5f) / 0.5f);
            _h[i] = h;
            Spine[i] = new PointF(c.X, c.Y - h * _u * Light.Lift);
        }
        Shape.Seal();
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        r = g = b = 0; alpha = 0;
        if (s < HeadEnd) return false;                       // the head and bill are ovals, painted on top
        int vi = Math.Clamp((int)((v + 1) * 0.5f * Look.SkinWide), 0, Look.SkinWide - 1);
        uint c = Look.Skin[Math.Clamp((int)(s * Look.SkinLen), 0, Look.SkinLen - 1) * Look.SkinWide + vi];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        alpha = s < NeckAt ? _headAlpha : 1f;                // the neck goes under with the head
        return true;
    }

    // ------------------------------------------------------------------ drawing

    private float Soft => MathF.Max(1f, _u * 0.0012f);

    /// <summary>
    /// The shadow on the water: the body's own shape, soft and faint, each point
    /// moved down-right by ITS OWN height (so the head's shadow lies a little
    /// further out than the back's), plus the head's. It hugs the duck: a shadow
    /// thrown far from its thing reads as lift, and a duck sits ON the water.
    /// </summary>
    public void DrawShadow(FrameBuffer fb)
    {
        if (!OnScreen) return;
        for (int i = 0; i < Points; i++)
        {
            _lifted[i] = Spine[i];
            float h = MathF.Max(0, _h[i]);
            Spine[i] = new PointF(_g[i].X + Light.ShadowX * h * _u, _g[i].Y + Light.ShadowY * h * _u);
        }
        PaintShadow(fb, _w.View, 0, 0, 0.30f, Soft * 1.5f);
        for (int i = 0; i < Points; i++) Spine[i] = _lifted[i];
        if (_headAlpha > 0.05f)
        {
            float hh = MathF.Max(0, _h[2]) * _u;
            PointF hs = new(_g[2].X + Light.ShadowX * hh, _g[2].Y + Light.ShadowY * hh);
            float ang = MathF.Atan2(Spine[0].Y - Spine[3].Y, Spine[0].X - Spine[3].X);
            Oval(fb, _w.View, hs, ang, Len * 0.09f, Len * 0.072f, 8, 22, 22, 0.20f * _headAlpha, 0, null, false);
        }
        if (_flapK > 0.05f)
        {
            float hh = (HeightOfBack) * _u;
            Look.WingOut.Draw(fb, _g[MidAt].X + Light.ShadowX * hh, _g[MidAt].Y + Light.ShadowY * hh, Heading, 1f, Ripples.Squash, 0.22f * _flapK * (0.4f + 0.6f * _wingK), Color.FromArgb(10, 24, 24), _w.View);
        }
    }

    public void Draw(FrameBuffer fb)
    {
        if (!OnScreen) return;
        bool[] stencil = _pond.Open;
        byte[] sun = _pond.Sun;
        // The wings, if it is flapping: under the body, which covers where they join it.
        if (_flapK > 0.04f)
        {
            PointF c = Spine[MidAt];
            // The raised stroke solid, the flung-wide stroke laid over it as it comes down: the wings stay solid and their reach swells and shrinks.
            Look.WingUp.Draw(fb, c.X, c.Y, Heading, 1f, Ripples.Squash, _flapK, null, stencil);
            Look.WingOut.Draw(fb, c.X, c.Y, Heading, 1f, Ripples.Squash, _wingK * _flapK, null, stencil);
        }
        Paint(fb, stencil, sun, 0f, Soft, _pond.Pixels);
        DrawMarks(fb, stencil, sun);
        DrawHead(fb, stencil, sun);
        // Spray.
        for (int i = 0; i < _dl.Length; i++)
        {
            if (_dl[i] <= 0) continue;
            float a = Math.Clamp(_dl[i] / 0.3f, 0, 1) * 0.8f;
            Oval(fb, stencil, new PointF(_dx[i], _dy[i]), 0, MathF.Max(1f, _u * 0.0012f), MathF.Max(1f, _u * 0.0012f), 232, 242, 246, a, 0, null, false);
        }
    }

    /// <summary>The marks on the body that are not colours on the back: the mandarin drake's two orange sails and the mallard drake's curled black tail feather.</summary>
    private void DrawMarks(FrameBuffer fb, bool[] stencil, byte[] sun)
    {
        if (Look.Style == Style.MandarinDrake)
        {
            // Two orange "sails" stand up on the flanks, tilted out: from above they are the most striking thing about the bird.
            float lift = (HeightOfBack + 0.02f) * _u * Light.Lift;
            float ang = MathF.Atan2(Spine[6].Y - Spine[8].Y, Spine[6].X - Spine[8].X);
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
            {
                (float gx, float gy) = GroundPoint(0.64f, side * 0.80f);
                PointF p = _w.ToScreen(gx, gy);
                PointF c = new(p.X, p.Y - lift);
                Oval(fb, stencil, c, ang, Len * 0.17f, Len * 0.062f, 168, 70, 24, 0.97f, 0, sun, false, _pond.Pixels);
                Oval(fb, stencil, new PointF(c.X + 0.01f * Len, c.Y), ang, Len * 0.14f, Len * 0.045f, 226, 118, 36, 0.95f, 0, sun, false, _pond.Pixels);
            }
        }
        else if (Look.Style == Style.MallardDrake)
        {
            PointF t = Spine[Points - 1];
            Oval(fb, stencil, new PointF(t.X, t.Y - 0.006f * _u * Light.Lift), MathF.Atan2(Spine[Points - 1].Y - Spine[Points - 3].Y, Spine[Points - 1].X - Spine[Points - 3].X) - 0.8f, Len * 0.04f, Len * 0.022f, 22, 26, 28, 0.95f, 0, sun, false, _pond.Pixels);
        }
    }

    /// <summary>
    /// The head and bill: an oval for the head at the head's own height, the
    /// species' marks on it, a flat oval bill, and two eyes. The mallard drake's
    /// bottle-green head with a sheen; the mandarin drake's chestnut crest, white
    /// eyebrows and orange whiskers; the teal drake's chestnut head with a green
    /// patch; the hens' mottled brown with a dark crown and eye line (the
    /// mandarin hen's white eye ring); the spot-billed duck's pale head with a
    /// dark crown, a dark eye line and its black bill with a yellow tip.
    /// </summary>
    private void DrawHead(FrameBuffer fb, bool[] stencil, byte[] sun)
    {
        if (_headAlpha <= 0.02f) return;
        uint[] water = _pond.Pixels;
        PointF hc = Spine[2], b0 = Spine[0], b1 = Spine[1];
        float ang = MathF.Atan2(b0.Y - Spine[3].Y, b0.X - Spine[3].X);
        float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
        float a = _headAlpha, hl = Len * 0.118f, hw = Len * 0.098f;
        PointF At(float along, float across) => new(hc.X + ca * along - sa * across, hc.Y + sa * along + ca * across);
        void O(PointF c, float len, float wid, int r, int g, int b, float alpha) => Oval(fb, stencil, c, ang, len, wid, r, g, b, alpha * a, 0, sun, false, water);

        (int R, int G, int B) head, billC;
        (int R, int G, int B)? tip = null;
        switch (Look.Style)
        {
            case Style.Karugamo: head = (182, 156, 118); billC = (30, 28, 26); tip = (226, 194, 44); break;
            case Style.MallardDrake: head = (22, 104, 72); billC = (222, 190, 48); break;
            case Style.MallardHen: head = (152, 122, 88); billC = (226, 152, 54); break;
            case Style.MandarinDrake: head = (150, 66, 40); billC = (208, 50, 46); tip = (230, 206, 176); break;
            case Style.MandarinHen: head = (126, 116, 104); billC = (84, 78, 72); break;
            case Style.TealDrake: head = (128, 62, 44); billC = (58, 62, 64); break;
            default: head = (140, 116, 88); billC = (98, 94, 72); break;
        }
        if (Look.Style == Style.MandarinDrake)
        {
            // Orange whiskers (cheek ruff) behind the head's sides, drawn first so the head sits on them.
            O(At(-hl * 0.15f, hw * 1.05f), hl * 0.75f, hw * 0.38f, 214, 118, 48, 0.95f);
            O(At(-hl * 0.15f, -hw * 1.05f), hl * 0.75f, hw * 0.38f, 214, 118, 48, 0.95f);
            O(At(-hl * 0.9f, 0), hl * 0.85f, hw * 0.55f, 168, 86, 52, 0.95f);          // the crest
        }
        O(hc, hl, hw, head.R, head.G, head.B, 1f);
        switch (Look.Style)
        {
            case Style.Karugamo:
            case Style.MallardHen:
            case Style.TealHen:
                O(At(-hl * 0.05f, 0), hl * 0.82f, hw * 0.34f, 66, 50, 36, 0.9f);       // dark crown
                O(At(hl * 0.05f, hw * 0.55f), hl * 0.62f, hw * 0.12f, 52, 40, 28, 0.9f);   // dark line through each eye
                O(At(hl * 0.05f, -hw * 0.55f), hl * 0.62f, hw * 0.12f, 52, 40, 28, 0.9f);
                break;
            case Style.MallardDrake:
                O(At(-hl * 0.2f, -hw * 0.35f), hl * 0.55f, hw * 0.28f, 78, 170, 126, 0.55f);   // the gloss of the sun on the green
                break;
            case Style.MandarinDrake:
                O(At(-hl * 0.45f, 0), hl * 0.5f, hw * 0.4f, 40, 112, 92, 0.85f);        // green on the crest
                O(At(hl * 0.05f, hw * 0.52f), hl * 0.9f, hw * 0.12f, 242, 238, 228, 0.95f);   // the white eyebrow stripe
                O(At(hl * 0.05f, -hw * 0.52f), hl * 0.9f, hw * 0.12f, 242, 238, 228, 0.95f);
                break;
            case Style.MandarinHen:
                O(At(hl * 0.30f, hw * 0.62f), hw * 0.34f, hw * 0.34f, 238, 236, 230, 0.95f);   // the white eye ring
                O(At(hl * 0.30f, -hw * 0.62f), hw * 0.34f, hw * 0.34f, 238, 236, 230, 0.95f);
                O(At(-hl * 0.25f, hw * 0.55f), hl * 0.6f, hw * 0.10f, 238, 236, 230, 0.8f);     // and the streak behind it
                O(At(-hl * 0.25f, -hw * 0.55f), hl * 0.6f, hw * 0.10f, 238, 236, 230, 0.8f);
                break;
            case Style.TealDrake:
                O(At(-hl * 0.3f, hw * 0.45f), hl * 0.7f, hw * 0.3f, 24, 116, 78, 0.9f);        // the green eye patch
                O(At(-hl * 0.3f, -hw * 0.45f), hl * 0.7f, hw * 0.3f, 24, 116, 78, 0.9f);
                break;
        }
        // The bill, a flat oval a little ahead of the head.
        PointF bc = new((b0.X + b1.X) / 2, (b0.Y + b1.Y) / 2);
        O(bc, Len * 0.07f, Len * 0.04f, billC.R, billC.G, billC.B, 1f);
        if (Look.Style == Style.MallardHen) O(new PointF(bc.X + ca * Len * 0.005f, bc.Y + sa * Len * 0.005f), Len * 0.05f, Len * 0.016f, 96, 64, 30, 0.8f);   // the dark saddle on the hen's bill
        if (tip is { } tp) O(new PointF(b0.X + (b1.X - b0.X) * 0.2f, b0.Y + (b1.Y - b0.Y) * 0.2f), Len * 0.026f, Len * 0.03f, tp.R, tp.G, tp.B, 1f);
        // Eyes.
        float er = MathF.Max(0.9f, Len * 0.011f);
        {
            Oval(fb, stencil, At(hl * 0.30f, hw * 0.62f), 0, er, er, 14, 14, 12, 0.95f * a, 0, sun, false, water);
            Oval(fb, stencil, At(hl * 0.30f, -hw * 0.62f), 0, er, er, 14, 14, 12, 0.95f * a, 0, sun, false, water);
        }
    }
}

/// <summary>What a duckling is doing. The director (Ducks) chooses; the duckling carries it out.</summary>
internal enum KDoing
{
    Follow,     // in the line behind the mother, paddling fast on tiny strokes
    Lag,        // stopped to peck at something while the line goes on
    Scramble,   // a frantic burst at twice the speed to get back
    Bump,       // two bumped heads and are spinning half round
    Ride,       // on the mother's back
    Peck,       // gone to a floating petal
    Chase,      // gone to a minnow school
    Dabble,     // bottoms up
    Bunch,      // gathered under the mother's chin
    Scatter,    // sent a little way off by her flapping
}

/// <summary>
/// A duckling seen from above: a round fluff ball, dark brown on top with a
/// yellow cheek on each side of a dark crown, a tiny dark bill, yellow down
/// the flanks and rump. A Body with only five spine points and no neck: it is
/// small enough that the whole animal is one soft shape.
///
/// WHAT A DUCKLING DOES. It follows its mother in a line, each one behind the
/// one ahead, paddling so fast that from above you see only the body rocking a
/// hair and a faint fizz of rings. It lags to peck at something, scrambles to
/// catch up, bumps a sibling and spins, climbs on the mother's back and rides,
/// goes to peck a petal or chase a flash of minnows, dabbles bottoms up with
/// all its brothers, and bunches under her chin. WHICH of those, and when, is
/// the director's choice (Ducks.Cutes): no two happen at once in a family. The
/// duckling here only carries out the doing it is given: where to go, how fast,
/// and what its body does (tip up, dip, spin, ride).
///
/// THE FOUR GATES. VISIBLE: yellow and brown on blue-green water. PERSPECTIVE: a
/// ground-plane teardrop squashed by the view and lifted a hair (0.005u). TURNING:
/// rigid, on an eased turn rate. MOVEMENT: a tiny fast paddle, a line behind
/// its mother, never a slide.
/// </summary>
internal sealed class Duckling : Body
{
    private const int Points = 11;
    private static readonly float[] S = [0f, 0.07f, 0.14f, 0.21f, 0.28f, 0.36f, 0.46f, 0.58f, 0.70f, 0.85f, 1.0f];
    private static readonly float[] W = [0.10f, 0.51f, 0.61f, 0.59f, 0.45f, 0.42f, 0.73f, 0.95f, 1.0f, 0.85f, 0.20f];
    private const float Mid = 0.55f;
    private const float BodyH = 0.005f;

    private readonly DuckWorld _w;
    private readonly Pond _pond;
    private readonly Ripples _ripples;
    private readonly Random _rng;
    private readonly uint[] _skin;
    private readonly float _u, _wide;
    private readonly Ground.Solid _solid;
    public readonly float Len;

    /// <summary>Where its middle is (ground pixels), which way it faces, how fast it goes.</summary>
    public float X, Y, Heading, Speed;
    /// <summary>What the director has told it to go to (ground pixels) and the fastest it may go there.</summary>
    public float TX, TY, MaxV;

    public readonly Mind<KDoing> Mind = new(KDoing.Follow);
    public KDoing Doing => Mind.Doing;
    /// <summary>Where it is in its doing (the director's own count), and seconds in it.</summary>
    public int Phase;
    public float T;
    /// <summary>Seconds to wait before a dabble starts (so a family does not tip up in step).</summary>
    public float Delay;
    /// <summary>Asked to be tipped up (a minnow chase's dabble); the dabble doing asks for itself.</summary>
    public bool WantTip;
    /// <summary>How high above the water it is lifted by what it is sitting on (u): the mother's back.</summary>
    public float RideH;
    /// <summary>True while it is on the mother's back (not in the water: it is nobody's obstacle and its shadow is not on the water).</summary>
    public bool OnMother;
    /// <summary>How deep the head is dipped for a peck, 0 to 1 (set by the director).</summary>
    public float Dip;

    private float _turnRate, _tip, _phase, _spinLeft, _spinRate, _fizz, _rock, _refusedT;
    private float _headAlpha = 1;
    private readonly float[] _h = new float[Points];
    private readonly PointF[] _g = new PointF[Points];
    private readonly float[] _gHalf = new float[Points];
    private readonly PointF[] _lifted = new PointF[Points];

    public Duckling(DuckWorld w, Ripples ripples, Random rng, uint[] skin, float lenU, int index) : base(Points)
    {
        _w = w; _pond = w.Pond; _ripples = ripples; _rng = rng; _skin = skin; _u = w.U;
        Len = lenU * _u; _wide = Len * 0.30f;
        _solid = w.SolidG;
        for (int i = 0; i < Points; i++) Along[i] = S[i];
        _phase = index * 1.3f;
    }

    public PointF Centre => _g[7];
    public PointF HeadSpot => _g[2];
    public float Radius => _wide * 1.05f;
    public bool InWater => !OnMother;
    public float Tip => _tip;
    public string State => Doing.ToString();
    public ReadOnlySpan<PointF> Footprint => _g;
    public ReadOnlySpan<float> GroundHalf => _gHalf;
    public PointF Bill => Spine[0];

    public bool OnScreen
    {
        get
        {
            PointF c = _g[7];
            float m = Len;
            return c.X > -m && c.X < _w.W + m && c.Y > -m && c.Y < _w.H + m;
        }
    }

    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Smooth(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    public void Place(float gx, float gy, float heading)
    {
        X = gx; Y = gy; Heading = heading; Speed = 0; TX = gx; TY = gy;
        LaySpine(0);
    }

    public void Begin(KDoing d, float seconds = Mind<KDoing>.Forever)
    {
        Mind.Begin(d, seconds);
        Phase = 0; T = 0; Delay = 0; WantTip = false; Dip = 0;
    }

    public float DistTo(float gx, float gy) { float dx = gx - X, dy = gy - Y; return MathF.Sqrt(dx * dx + dy * dy); }

    /// <summary>Turn on the spot by "by" radians over "secs" seconds (two bumped heads and spin half round).</summary>
    public void StartSpin(float by, float secs) { _spinRate = by / secs; _spinLeft = secs; }

    /// <summary>
    /// Swim toward (tx, ty): as fast as the mother's own speed plus enough to
    /// close the gap (so it catches up smoothly and stops when she stops), never
    /// faster than "maxV". Its heading turns toward where it is going on an eased
    /// turn rate. A step onto land, a pad or a rock is refused and slid along the
    /// edge instead (Ground.Step).
    /// </summary>
    public void Steer(float dt, float tx, float ty, float maxV, float motherSpeed)
    {
        float dx = tx - X, dy = ty - Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float want = MathF.Min(maxV, motherSpeed * 0.92f + d * 3f);
        if (d < _u * 0.004f) want = MathF.Min(want, motherSpeed);
        // Feel the way, as a grown duck does (a pad or rock between it and where it is going: the smallest turn that is clear).
        float desired = MathF.Atan2(dy, dx);
        if (d > _u * 0.01f)
        {
            float look = MathF.Min(d, Len * 1.3f + Speed * 0.4f);
            if (!ClearAhead(desired, look))
                for (int k = 1; k <= 6; k++)
                {
                    float a1 = desired + k * 0.4f, a2 = desired - k * 0.4f;
                    if (ClearAhead(a1, look)) { desired = a1; break; }
                    if (ClearAhead(a2, look)) { desired = a2; break; }
                }
        }
        Speed += (want - Speed) * (1 - MathF.Exp(-dt * 8f));
        float turn = 0;
        // A turn is no tighter than about half a body length across at this speed (a duckling that swings round faster than it moves is
        // pivoting on the spot, and reads as going tail first).
        float most = Math.Clamp(MathF.Max(Speed, _u * 0.02f) / (0.5f * Len), 1.5f, 7f);
        if (d > _u * 0.006f && Speed > _u * 0.004f) turn = Math.Clamp(Wrap(desired - Heading) * 6f, -most, most);
        _turnRate += (turn - _turnRate) * (1 - MathF.Exp(-dt * 12f));
        Heading = Wrap(Heading + _turnRate * dt);
        float step = Speed * dt;
        if (step > 1e-4f)
        {
            float x = X, y = Y, h = Heading;
            float err = Wrap(desired - Heading);
            // If the way ahead is refused, slide along it whichever side the goal is (Ground.Step only slides for a goal well off the heading).
            Ground.Stepped r = Ground.Step(_solid, ref x, ref y, ref h, step, MathF.Abs(err) > 0.5f ? err : 1f);
            if (r == Ground.Stepped.Refused) r = Ground.Step(_solid, ref x, ref y, ref h, step, -1f);
            if (r == Ground.Stepped.Refused)
            {
                Speed = 0;
                // Inside something (pushed there, or the edge of a pad grew round it): ease out toward the clearest water, never staying put.
                _refusedT += dt;
                if (_refusedT > 0.4f) Rescue(step * 1.5f);
            }
            else { X = x; Y = y; if (h != Heading) Heading = h; _refusedT = 0; }
        }
    }

    /// <summary>Moves it a little toward whichever of eight ways has water and the most room (used only when every step is refused).</summary>
    private void Rescue(float by)
    {
        float best = float.MinValue, bx = 0, by2 = 0;
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.PI / 4, px = X + MathF.Cos(a) * by * 2, py = Y + MathF.Sin(a) * by * 2;
            float score = (_w.WaterG(px, py) ? 1000f : 0f) + _w.Clear(px, py);
            if (score > best) { best = score; bx = MathF.Cos(a); by2 = MathF.Sin(a); }
        }
        X += bx * by; Y += by2 * by;
    }

    /// <summary>Is the water clear along a heading, felt at three places from the bill out to "dist"?</summary>
    private bool ClearAhead(float a, float dist)
    {
        float ca = MathF.Cos(a), sa = MathF.Sin(a), nx = X + ca * 0.5f * Len, ny = Y + sa * 0.5f * Len;
        foreach (float f in (ReadOnlySpan<float>)[0.25f, 0.6f, 1f])
            if (_w.RoomG(nx + ca * dist * f, ny + sa * dist * f, _wide * 0.5f) <= _wide * 0.35f) return false;
        return true;
    }

    /// <summary>Slow to a stop where it is (a lag, a dabble, a peck); a spin in progress carries on.</summary>
    public void Hold(float dt, float motherSpeed = 0)
    {
        Speed += (0 - Speed) * (1 - MathF.Exp(-dt * 8f));
        _turnRate += (0 - _turnRate) * (1 - MathF.Exp(-dt * 12f));
        if (_spinLeft > 0)
        {
            float k = MathF.Min(dt, _spinLeft);
            Heading = Wrap(Heading + _spinRate * k);
            _spinLeft -= dt;
        }
    }

    /// <summary>Put it exactly here (riding the mother, climbing on, sliding off): the director owns its place.</summary>
    public void Pin(float gx, float gy, float heading)
    {
        X = gx; Y = gy; Heading = heading; Speed = 0; _turnRate = 0;
    }

    /// <summary>Moves it by a shove (two ducklings kept apart; the mother's flank).</summary>
    public void Shove(float sx, float sy)
    {
        if (_w.SolidFixedG(X + sx, Y + sy)) { X += sx; Y += sy; }
    }

    /// <summary>Per frame, after the steering: the mind's clock, the body's animation, the fizz of rings, and the spine laid out.</summary>
    public void Update(float dt, bool scrambling)
    {
        Mind.Tick(dt);
        T += dt;
        // Tip up (dabble) and dip (peck) ease toward what the doing asks.
        bool tip = Doing == KDoing.Dabble ? (T > Delay && Mind.Left > 0.5f) : WantTip;
        _tip += ((tip ? 1f : 0f) - _tip) * (1 - MathF.Exp(-dt * 7f));
        _headAlpha = Math.Clamp(1 - Smooth(_tip) * 1.5f, 0.1f, 1f);
        // The paddle: tiny and fast; from above you see the body rock a hair. Faster when it hurries.
        float moving = Math.Min(1, Speed / MathF.Max(1e-3f, _u * 0.06f));
        _phase = (_phase + MathF.Tau * (6f + 4f * moving) * dt) % MathF.Tau;
        _rock = MathF.Sin(_phase) * 0.07f * Math.Min(1, 0.3f + moving);
        if (scrambling)
        {
            // Half running on the water: a ring now and then off the feet.
            _fizz -= dt;
            if (_fizz <= 0 && Speed > _u * 0.08f) { _fizz = 0.22f; PointF c = _g[7]; _ripples.Add(c.X, c.Y, 0.16f); }
        }
        LaySpine(_rock);
    }

    private void LaySpine(float rock)
    {
        float hd = Heading + rock, cs = MathF.Cos(hd), sn = MathF.Sin(hd);
        float tip = Smooth(_tip);
        float dip = Smooth(Dip);
        for (int i = 0; i < Points; i++)
        {
            float tail = S[i] > Mid ? (S[i] - Mid) / (1 - Mid) : 0;
            float w = _wide * W[i] * (1 + 0.1f * tip * tail);
            float ahead = (Mid - S[i]) * Len;
            if (i <= 3) ahead += Len * 0.05f * dip;                                          // a peck reaches the bill forward
            float cx = X + cs * ahead, cy = Y + sn * ahead;
            PointF c = _w.ToScreen(cx, cy), l = _w.ToScreen(cx + sn * w, cy - cs * w), r = _w.ToScreen(cx - sn * w, cy + cs * w);
            _g[i] = c;
            _gHalf[i] = MathF.Max(1f, MathF.Sqrt((l.X - r.X) * (l.X - r.X) + (l.Y - r.Y) * (l.Y - r.Y)) / 2);
            HalfWidth[i] = _gHalf[i];
            Shape.Points[i] = new System.Numerics.Vector3(cx, cy, 0);
            Shape.Radius[i] = w;
            // Heights: a hair above the water; tipped up, the tail end rises well clear and the head goes under; a peck lowers the head.
            float h = BodyH + RideH;
            if (i <= 3) h += -0.012f * tip - 0.003f * dip;
            else h += 0.014f * tip * Smooth((S[i] - 0.4f) / 0.6f);
            _h[i] = h;
            Spine[i] = new PointF(c.X, c.Y - h * _u * Light.Lift);
        }
        Shape.Seal();
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        int vi = Math.Clamp((int)((v + 1) * 0.5f * Look.SkinWide), 0, Look.SkinWide - 1);
        uint c = _skin[Math.Clamp((int)(s * Look.SkinLen), 0, Look.SkinLen - 1) * Look.SkinWide + vi];
        r = (int)((c >> 16) & 0xFF); g = (int)((c >> 8) & 0xFF); b = (int)(c & 0xFF);
        alpha = s < 0.4f ? _headAlpha : 1f;
        return true;
    }

    private float Soft => MathF.Max(1f, _u * 0.0012f);

    /// <summary>A faint shadow that hugs it. Not drawn while it rides (its shadow would fall on the mother's back, not the water).</summary>
    public void DrawShadow(FrameBuffer fb)
    {
        if (OnMother || !OnScreen) return;
        for (int i = 0; i < Points; i++)
        {
            _lifted[i] = Spine[i];
            float h = MathF.Max(0, _h[i]);
            Spine[i] = new PointF(_g[i].X + Light.ShadowX * h * _u, _g[i].Y + Light.ShadowY * h * _u);
        }
        PaintShadow(fb, _w.View, 0, 0, 0.28f, Soft * 1.4f);
        for (int i = 0; i < Points; i++) Spine[i] = _lifted[i];
    }

    public void Draw(FrameBuffer fb)
    {
        if (!OnScreen) return;
        Paint(fb, _pond.Open, _pond.Sun, 0f, Soft, _pond.Pixels);
        // Two tiny dark eyes, either side of the crown.
        if (_headAlpha > 0.4f)
        {
            PointF h = Spine[2], f = Spine[0];
            float ex = f.X - h.X, ey = f.Y - h.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
            float er = MathF.Max(0.7f, Len * 0.035f), off = HalfWidth[2] * 0.55f;
            foreach (int side in (ReadOnlySpan<int>)[1, -1])
                Oval(fb, _pond.Open, new PointF(h.X + ex / el * HalfWidth[2] * 0.25f - ey / el * off * side, h.Y + ey / el * HalfWidth[2] * 0.25f + ex / el * off * side), 0, er, er, 16, 14, 10, 0.9f * _headAlpha, 0, _pond.Sun, false, _pond.Pixels);
        }
    }
}
