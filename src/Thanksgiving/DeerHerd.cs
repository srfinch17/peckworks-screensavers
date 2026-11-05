using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Minds;

namespace Thanksgiving;

/// <summary>
/// A white-tailed doe, and her fawn, that LIVE at the far side of the field
/// (his line: "add a deer ffs"). The doe grazes with her head down, biting
/// and chewing, flicks an ear, lifts her head to look about (and flicks her
/// tail), and walks a few steps; the fawn keeps close to her and trots after
/// her when she moves off. Like the turkeys, they are always there.
///
/// FEYNMAN VERSION: each deer is two flip-books stamped together. The BODY
/// book is a walk of 24 pages, plus four "settle" pages that take the legs
/// from the walk's rest page to a square stance (and two more with the tail
/// up, for the flick). The legs step in a deer's own order, the LATERAL
/// sequence a hoofed animal uses: left hind, left fore, right hind, right
/// fore, each a quarter of the stride after the last (the first version had
/// them in the diagonal order a monkey uses, and the hooves skated: each one
/// crept 11 pixels along the ground on every step because the stride did not
/// match how long a foot stays down). Each foot is down for 0.62 of the
/// stride and slides back at exactly the body's speed, so it stays planted.
/// The body has WEIGHT: the withers dip as each forefoot lands and the rump
/// dips as each hind foot lands, so the back rocks, and the neck's root dips
/// with the forefeet, so the head nods as she walks. The knee is worked out
/// by a little two-bone sum (a deer's hind "knee" is its hock and points
/// BACKWARD, the fore knee points forward), and the legs taper from a
/// muscled thigh to a thin cannon and a small dark hoof.
///
/// The HEAD book is 24 pages of the neck from alert (head up, ears pricked)
/// to grazing (nose in the stubble), under six degrees apart so a change of
/// neck never pops; each page comes in three kinds: jaw closed, jaw open
/// (chewing) and one ear flicked back. The head sheet's origin is the neck's
/// root, which the body book records for every page, so the head rides the
/// nod. The grazing bite is a quick tug up and back, a hold, and a slow ease
/// down, in bursts with pauses, which is what a real deer's head does in
/// grass. Walking never starts or stops in one frame: stopping eases the
/// walk down so it ends on the rest page, then plays the settle pages; a
/// start plays them backward and walks off.
///
/// One picture of each, facing right; the engine's mirror stamp turns them
/// to face left. They are stamped through the painter's OpenField stencil,
/// so the scarecrow, the fence, the near ground and the maples hide them
/// where they should, and they keep between the maples so a head is never
/// parked behind the leaves.
///
/// Test aid: PECKWORKS_DEER=walk keeps the doe walking (for the hoof
/// measure); PECKWORKS_DEER=graze keeps her grazing.
/// </summary>
internal sealed class DeerHerd
{
    private enum Doing { Graze, Look, Walk, Alert, Retreat }   // Alert: head up, still, facing what it heard; Retreat: a brisk walk away from it
    private enum Gait { Settled, Rising, Walking, Stopping, Settling, Turning }   // Rising: the settle pages backward (about to walk); Settling: forward (just stopped); Turning: coming round on the spot

    private sealed class Animal
    {
        public float H;                                     // body height in pixels (the unit everything is drawn in)
        public int BodyW, BodyH, BodyOx, BodyOy, HeadS;
        public Sprite[] Walk = new Sprite[WalkPages];
        public Sprite[] Settle = new Sprite[SettlePages];   // from the walk's rest page to the square stance
        public Sprite[] Tail = new Sprite[TailPages];       // the square stance with the tail down, half up, up
        public PointF[] NeckWalk = new PointF[WalkPages];   // where the neck's root is on each walk page (body heights from the middle, up from the ground)
        public PointF[] NeckSettle = new PointF[SettlePages];
        public Sprite[,] Head = new Sprite[HeadPages, HeadKinds];
        public Sprite[] TurnBody = new Sprite[TurnPages];   // the square stance narrowed about the feet, for a turn on the spot
        public Sprite[,] TurnHead = new Sprite[HeadPages, TurnPages];   // the head (jaw closed) narrowed the same, per neck page
        public int TurnTo;                                  // the way a turn ends facing; Dir flips at its middle
        public float TurnT;
        public int TurnWhenSettled;                         // a turn owed once the stop has settled (a wall, the fawn facing her mother's way), or 0
        public bool Fawn;
        public bool Buck;                                   // the third deer: bigger, with antlers
        public float X, Depth;
        public int Dir;
        public Doing Doing;
        public Gait Gait;
        public float Timer, Clock, Walked;
        public float GaitT;                                 // seconds into a Rising or Settling
        public Pace Pace = null!;                           // the walk's speed, eased (Mover.cs): leans in, runs down onto the rest page
        public Attention Senses = null!;                    // what it makes of the noticeboard (Senses.cs): timid or bold, curious or dull
        public float Timid;                                 // the dial, kept for the dice
        public float Restless;                              // seed for the slow drift of a standing head
        public float NeckF;                                 // where the neck is now, 0 up to 1 grazing (eased toward its target)
        public float NeckV;                                 // and how fast it is moving (the spring's state)
        public float BiteT = -1, NextBite, ChewT = -1, EarT = -1, NextEar, TailT = -1;
        public int BitesLeft;
        public bool Lent;                                   // a story is driving it: the herd keeps the neck, the bites and the ears, nothing else
        public float LentNeck;                              // where the story wants its neck
        public float HomeDepth;                             // its own line across the field, given back when the story is over
    }

    /// <summary>The fawn's index, or -1 when there is none (for a story that borrows it).</summary>
    public int FawnIndex => _deer.Length >= 2 ? 1 : -1;

    /// <summary>A story takes an animal over: its walk and its doings stop; the story moves it with Drive and gives it back with Release.</summary>
    public bool Lend(int i)
    {
        if (i < 0 || i >= _deer.Length || _deer[i].Lent) return false;
        Animal a = _deer[i];
        a.Lent = true; a.LentNeck = a.NeckF; a.HomeDepth = a.Depth; a.Gait = Gait.Settled; a.Doing = Doing.Look; a.Walked = 0;
        return true;
    }

    /// <summary>Where a lent animal's feet are now, its height (pixels) and its depth (0 the far side of the field, 1 at the fence).</summary>
    public (float X, float FeetY, float H, float Depth) Where(int i) => (_deer[i].X, FootY(_deer[i]), _deer[i].H, _deer[i].Depth);

    /// <summary>Where an animal's feet would be if it stood at x on the line "depth" across the field (its own line when depth is left out).</summary>
    public float FeetYAt(int i, float x, float depth = -1f) => _s.FieldY(x) + (_s.Ground.YAt(Math.Clamp(x, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(x)) * (depth < 0 ? _deer[i].Depth : depth);

    /// <summary>
    /// Moves a lent animal to x (and to the line "depth" across the field, if given) facing dir, with walking pages if it
    /// moved, its neck eased toward "neck" (0 up, 1 nose in the stubble).
    /// </summary>
    public void Drive(int i, float x, int dir, float neck, float depth = -1f)
    {
        Animal a = _deer[i];
        float y0 = FootY(a), dx = x - a.X;
        if (depth >= 0) a.Depth = depth;
        a.X = x;
        float dy = FootY(a) - y0;
        float moved = MathF.Sqrt(dx * dx + dy * dy);
        if (moved > 0.01f) { a.Walked += moved; a.Gait = Gait.Walking; }
        else a.Gait = Gait.Settled;
        a.Dir = dir; a.LentNeck = neck;
    }

    /// <summary>Lets the animal chew for a moment (the jaw pages), for a story that fed it.</summary>
    public void Chew(int i) { _deer[i].ChewT = 0; }

    public void Release(int i)
    {
        Animal a = _deer[i];
        a.Lent = false; a.Depth = a.HomeDepth; a.Gait = Gait.Settled; a.Doing = Doing.Look; a.Timer = 1.5f; a.Clock = 0;
    }

    private const int WalkPages = 48, SettlePages = 8, TailPages = 3, HeadPages = 24, HeadKinds = 3;   // 48 walk pages: a planted hoof shimmers by half the stride a page covers, about a pixel; 8 settle pages: two per foot
    private const float Speed = 0.05f;                      // in U per second
    private const float RetreatSpeed = 0.085f;              // a startled deer's brisk walk away
    private const float StepReach = 0.17f;                  // how far a foot travels in front of and behind the hip, in body heights
    private const float Duty = 0.62f;                       // the share of the stride a foot is on the ground
    private const float NeckX = 0.30f, NeckY = 0.72f;       // where the neck leaves the body (body heights from the middle, up from the ground), standing
    private const float HeadRootF = 0.42f;                  // the neck's root is this far down the head sheet
    private const float NeckSpring = 60f, NeckDamp = 15.5f; // the neck eases to where it is wanted like a damped spring: slow off the mark, fastest in the middle, settling in about half a second (a fixed rate started and stopped dead)
    private const float SettleFor = 0.4f;                   // eight settle pages: the feet come square one at a time, each with a lift (blended together they skated 17 px)
    private const int TurnPages = 3;
    private const float TurnFor = 0.3f;                     // a turn on the spot: three narrowing pages facing the old way, three widening facing the new
    private static readonly float[] Squash = [0.72f, 0.42f, 0.14f];

    private static readonly Color Tan = Color.FromArgb(140, 92, 58);
    private static readonly Color TanDark = Color.FromArgb(104, 66, 40);
    private static readonly Color TanFawn = Color.FromArgb(176, 112, 64);
    private static readonly Color TanFawnDark = Color.FromArgb(130, 80, 46);
    private static readonly Color Cream = Color.FromArgb(232, 208, 170);
    private static readonly Color White = Color.FromArgb(246, 236, 214);
    private static readonly Color Hoof = Color.FromArgb(40, 30, 26);
    private static readonly Color Nose = Color.FromArgb(32, 26, 26);
    private static readonly Color EarIn = Color.FromArgb(190, 140, 120);
    private static readonly Color EarEdge = Color.FromArgb(70, 44, 32);
    private static readonly Color Bone = Color.FromArgb(214, 194, 160);     // the buck's antlers

    private readonly ThanksgivingScenery _s;
    private readonly Random _rng;
    private readonly Noticeboard _board;
    private readonly Animal[] _deer;
    private readonly string? _test = Environment.GetEnvironmentVariable("PECKWORKS_DEER");
    private readonly StreamWriter? _log;
    private double _time;

    public DeerHerd(ThanksgivingScenery s, int count, Random sceneRng, Noticeboard board)
    {
        _s = s;
        _board = board;
        _rng = new Random(sceneRng.Next());                 // its own dice, so the rest of the scene never reshuffles
        _log = MindLog.Open();
        _deer = new Animal[count];
        float x0 = s.Width * (0.24f + 0.50f * (float)_rng.NextDouble());   // between the maples, never behind their leaves
        int dir = _rng.Next(2) == 0 ? 1 : -1;
        float[] timid = Traits.Deal(_rng, Math.Max(1, count)), curious = Traits.Deal(_rng, Math.Max(1, count));
        for (int i = 0; i < count; i++)
        {
            bool fawn = i == 1, buck = i == 2;              // the second deer is the fawn; the third a buck with antlers (his line: "some deer should have antlers")
            var a = new Animal { Fawn = fawn, Buck = buck, Dir = dir, Depth = 0.18f + 0.10f * (float)_rng.NextDouble() + (fawn ? 0.06f : 0f) };
            a.X = x0 - dir * (i == 0 ? 0 : (fawn ? s.U * 0.09f : s.U * 0.22f));
            a.Doing = _test == "walk" ? Doing.Walk : Doing.Graze;
            a.Gait = _test == "walk" ? Gait.Walking : Gait.Settled;
            a.NeckF = _test == "walk" ? 0.25f : 1f;
            a.Timer = 2f + 4f * (float)_rng.NextDouble();
            a.NextBite = 0.5f; a.NextEar = 1f + 3f * (float)_rng.NextDouble();
            a.Timid = fawn ? 0.5f + 0.5f * timid[i] : timid[i];          // a fawn is never bold
            a.Senses = new Attention(a.Timid, curious[i]) { Keen = 1.3f };   // big ears: it feels a notice from further off
            a.Pace = new Pace(Speed * s.U * 2.5f);                           // leans into a walk over about half a second
            if (_test == "walk") a.Pace.Want = Speed * s.U;
            a.Restless = (float)_rng.NextDouble() * 10;
            Make(a, Math.Max(14, (int)(s.U * (fawn ? 0.048f : buck ? 0.086f : 0.078f))));
            _deer[i] = a;
        }
    }

    // ================================================================ painting (once)

    private static void Make(Animal a, int h)
    {
        a.H = h;
        a.BodyW = (int)(1.3f * h) + 4;
        a.BodyOx = a.BodyW / 2;
        a.BodyH = (int)(0.95f * h) + 4;
        a.BodyOy = a.BodyH - 2;
        // The head sheet is square, 1.5 body heights, with the neck's root HeadRootF of the way down: 0.63 above the root for the
        // alert head and 0.87 below it for the grazing one (at 1.05 with the root 0.55 down it held 0.50 below, and the grazing
        // doe's muzzle, cream ring and jaw were cut off along a ruled line: the animator's blocker).
        a.HeadS = (int)(1.5f * h) + 4;
        Sprite Body(Action<Graphics> paint) => Sprite.Paint(a.BodyW, a.BodyH, g =>
        {
            g.TranslateTransform(a.BodyOx, a.BodyOy);
            g.ScaleTransform(h, -h);                        // body heights, y UP
            paint(g);
        });
        for (int i = 0; i < WalkPages; i++)
        {
            float p = i / (float)WalkPages;
            a.NeckWalk[i] = NeckRoot(p);
            a.Walk[i] = Body(g => PaintBody(g, WalkFeet(p), WithersDip(p), CroupDip(p), 0, a.Fawn, h));
        }
        // The settle pages: the feet go from the rest page's places to square under the hips ONE AT A TIME, each lifting as it
        // moves (far fore, far hind, near fore, and the near hind last), two pages per foot; the body's dip eases out.
        // (Blending all four together along the ground slid a planted hoof 17 px.)
        PointF[] rest = WalkFeet(0f), square = SquareFeet();
        int[] order = [3, 2, 1, 0];                                        // which foot moves first, second, ...
        for (int i = 0; i < SettlePages; i++)
        {
            float k = (i + 1) / (float)SettlePages;
            var feet = new PointF[4];
            for (int n = 0; n < 4; n++)
            {
                int j = order[n];
                float kj = Math.Clamp((i + 1 - 2 * n) / 2f, 0f, 1f);       // this foot's own progress: pages 2n and 2n+1
                feet[j] = new PointF(rest[j].X + (square[j].X - rest[j].X) * kj, rest[j].Y * (1 - kj) + 0.05f * MathF.Sin(MathF.PI * kj));
            }
            float dip = 0.008f * (1 - k);
            a.NeckSettle[i] = new PointF(NeckX, NeckRoot(0f).Y + (NeckY - NeckRoot(0f).Y) * k);
            a.Settle[i] = Body(g => PaintBody(g, feet, -dip, -dip, 0, a.Fawn, h));
        }
        for (int t = 0; t < TailPages; t++)
        {
            int tail = t;
            a.Tail[t] = Body(g => PaintBody(g, square, 0, 0, tail, a.Fawn, h));
        }
        // The turn pages: the square stance narrowed about the feet (a deer coming round through the front view is a sliver).
        for (int t = 0; t < TurnPages; t++)
        {
            float sq = Squash[t];
            a.TurnBody[t] = Sprite.Paint(a.BodyW, a.BodyH, g =>
            {
                g.TranslateTransform(a.BodyOx, a.BodyOy);
                g.ScaleTransform(h * sq, -h);
                PaintBody(g, square, 0, 0, 0, a.Fawn, h);
            });
        }
        for (int j = 0; j < HeadPages; j++)
        {
            float f = j / (float)(HeadPages - 1);
            for (int kind = 0; kind < HeadKinds; kind++)
            {
                int kk = kind;
                a.Head[j, kind] = Sprite.Paint(a.HeadS, a.HeadS, g =>
                {
                    g.TranslateTransform(a.HeadS / 2f, a.HeadS * HeadRootF);
                    g.ScaleTransform(h, -h);
                    PaintHead(g, f, kk, a.Fawn, a.Buck, h);
                });
            }
            for (int t = 0; t < TurnPages; t++)
            {
                float sq = Squash[t];
                a.TurnHead[j, t] = Sprite.Paint(a.HeadS, a.HeadS, g =>
                {
                    g.TranslateTransform(a.HeadS / 2f, a.HeadS * HeadRootF);
                    g.ScaleTransform(h * sq, -h);
                    PaintHead(g, f, 0, a.Fawn, a.Buck, h);
                });
            }
        }
    }

    private static PointF P(float x, float y) => new(x, y);

    // ---- the walk: where each foot is at phase p of the stride (0 to 1), in body heights from the hip; order near hind, near fore, far hind, far fore ----
    //      The LATERAL sequence: left (near) hind lands at 0, left fore at 0.25, right (far) hind at 0.5, right fore at 0.75.
    private static readonly float[] Offsets = [0f, 0.75f, 0.5f, 0.25f];
    private static readonly float[] HipX = [-0.22f, 0.22f, -0.26f, 0.26f];

    private static PointF[] WalkFeet(float p)
    {
        var feet = new PointF[4];
        for (int j = 0; j < 4; j++)
        {
            float ph = (p + Offsets[j]) % 1f;
            float dx, up = 0;
            if (ph < Duty) dx = StepReach * (1 - 2 * ph / Duty);          // on the ground: the foot slides back at the body's speed
            else
            {
                float q = (ph - Duty) / (1 - Duty);
                dx = StepReach * (-1 + 2 * q);                            // in the air: it swings forward again
                up = 0.07f * MathF.Sin(MathF.PI * q);
            }
            feet[j] = new PointF(dx, up);
        }
        return feet;
    }

    private static PointF[] SquareFeet() => [P(-0.03f, 0), P(0.03f, 0), P(-0.03f, 0), P(0.03f, 0)];

    /// <summary>The withers dip as each forefoot lands (at 0.25 and 0.75 of the stride); positive is DOWN, in body heights.</summary>
    private static float WithersDip(float p) => 0.012f * (0.5f + 0.5f * MathF.Cos(4 * MathF.PI * (p - 0.25f)));
    private static float CroupDip(float p) => 0.012f * (0.5f + 0.5f * MathF.Cos(4 * MathF.PI * p));
    /// <summary>The neck's root on a walk page: it rides the withers and dips a little more as each forefoot lands, which is the nod.</summary>
    private static PointF NeckRoot(float p) => P(NeckX, NeckY - WithersDip(p) - 0.018f * (0.5f + 0.5f * MathF.Cos(4 * MathF.PI * (p - 0.25f))));

    /// <summary>
    /// The body and legs: x forward, y up, the origin at the middle of the body on the ground. "feet" are the four
    /// feet's places from their hips (near hind, near fore, far hind, far fore); the dips lower the front and the
    /// back of the body (body heights); "tail" 0 down, 1 half up, 2 up (the white flag).
    /// </summary>
    private static void PaintBody(Graphics g, PointF[] feet, float withersDip, float croupDip, int tail, bool fawn, int h)
    {
        float pw = MathF.Max(0.01f, 1.0f / h);                 // one pixel, in body heights
        Color coat = fawn ? TanFawn : Tan, coatDark = fawn ? TanFawnDark : TanDark;
        void Blob(PointF[] pts, Color c, float tension = 0.5f)
        {
            using var path = new GraphicsPath();
            path.AddClosedCurve(pts, tension);
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        // A limb bone as a tapered quad (wide end at "a"), with round ends.
        void Bone(PointF a, PointF b, float wa, float wb, Color c)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy) + 1e-5f, nx = -dy / l, ny = dx / l;
            using var br = new SolidBrush(c);
            g.FillPolygon(br, [P(a.X + nx * wa / 2, a.Y + ny * wa / 2), P(b.X + nx * wb / 2, b.Y + ny * wb / 2), P(b.X - nx * wb / 2, b.Y - ny * wb / 2), P(a.X - nx * wa / 2, a.Y - ny * wa / 2)]);
            g.FillEllipse(br, a.X - wa / 2, a.Y - wa / 2, wa, wa);
            g.FillEllipse(br, b.X - wb / 2, b.Y - wb / 2, wb, wb);
        }

        // Legs: far legs first (darker), the body, then the near legs. Two-bone IK: the joint goes where two bones of
        // equal length reach from hip to foot; hocks point back, knees forward. The thigh is thick, the cannon thin.
        void Leg(int j, bool hind, Color upper, Color lower)
        {
            float hipY = 0.52f - (hind ? croupDip : withersDip);
            PointF hip = P(HipX[j], hipY), foot = P(HipX[j] + feet[j].X, 0.02f + feet[j].Y);
            const float bone = 0.265f;                                  // two bones just longer than hip to ground: a standing leg is nearly straight
            float ddx = foot.X - hip.X, ddy = foot.Y - hip.Y;
            float d = MathF.Min(MathF.Sqrt(ddx * ddx + ddy * ddy), 2 * bone - 0.002f);
            float alpha = MathF.Acos(Math.Clamp(d / (2 * bone), -1, 1));
            float theta = MathF.Atan2(ddy, ddx) + (hind ? alpha : -alpha);
            PointF joint = P(hip.X + bone * MathF.Cos(theta), hip.Y + bone * MathF.Sin(theta));
            Bone(hip, joint, MathF.Max(0.085f, 2.6f * pw), MathF.Max(0.042f, 1.8f * pw), upper);
            Bone(joint, foot, MathF.Max(0.040f, 1.8f * pw), MathF.Max(0.028f, 1.4f * pw), lower);
            using var hoof = new SolidBrush(Hoof);
            g.FillPolygon(hoof, [P(foot.X - 0.018f, foot.Y + 0.02f), P(foot.X + 0.024f, foot.Y + 0.02f), P(foot.X + 0.028f, foot.Y - 0.02f), P(foot.X - 0.02f, foot.Y - 0.02f)]);
        }
        Leg(2, true, coatDark, coatDark);
        Leg(3, false, coatDark, coatDark);

        // The torso: a deep chest, a straight back that dips with the weight, a rounded haunch; the dark topline; the
        // cream underside that wraps up into the flank (one soft shape, not a pill); the shoulder and thigh as masses.
        float wd = withersDip, cd = croupDip;
        Blob([P(0.44f, 0.60f - wd), P(0.36f, 0.78f - wd), P(0.10f, 0.83f - wd * 0.5f - cd * 0.5f), P(-0.26f, 0.81f - cd), P(-0.46f, 0.66f - cd), P(-0.40f, 0.44f - cd), P(-0.10f, 0.40f), P(0.26f, 0.42f), P(0.44f, 0.50f - wd)], coat);
        Blob([P(0.38f, 0.79f - wd), P(0.10f, 0.84f - wd * 0.5f - cd * 0.5f), P(-0.28f, 0.82f - cd), P(-0.42f, 0.70f - cd), P(-0.30f, 0.73f - cd), P(0.08f, 0.76f - wd * 0.5f - cd * 0.5f), P(0.34f, 0.72f - wd)], coatDark, 0.4f);   // the topline
        Blob([P(0.38f, 0.48f - wd), P(0.20f, 0.41f), P(-0.10f, 0.40f), P(-0.36f, 0.45f - cd), P(-0.42f, 0.60f - cd), P(-0.26f, 0.50f - cd), P(0.00f, 0.46f), P(0.26f, 0.50f - wd), P(0.42f, 0.62f - wd)], Cream, 0.45f);   // the underside: a crescent, thin under the barrel and rising up into the chest and the haunch
        using (var thigh = new SolidBrush(Color.FromArgb(70, coatDark)))
        {
            g.FillEllipse(thigh, -0.44f, 0.46f - cd, 0.30f, 0.30f);        // the haunch
            g.FillEllipse(thigh, 0.14f, 0.48f - wd, 0.24f, 0.24f);         // the shoulder
        }
        if (fawn)
        {
            // A fawn's spots: scattered along the back and upper flank, in mixed sizes, never in rows.
            using var spot = new SolidBrush(Color.FromArgb(236, 214, 176));
            (float X, float Y, float R)[] spots = [(-0.36f, 0.70f, 0.020f), (-0.30f, 0.76f, 0.014f), (-0.24f, 0.66f, 0.024f), (-0.18f, 0.74f, 0.016f), (-0.12f, 0.68f, 0.020f),
                (-0.06f, 0.77f, 0.014f), (0.00f, 0.71f, 0.026f), (0.06f, 0.64f, 0.016f), (0.10f, 0.76f, 0.018f), (0.16f, 0.69f, 0.022f), (0.22f, 0.74f, 0.014f),
                (0.28f, 0.66f, 0.018f), (-0.34f, 0.60f, 0.014f), (-0.20f, 0.58f, 0.016f), (0.02f, 0.60f, 0.014f), (0.20f, 0.60f, 0.016f), (0.34f, 0.60f, 0.014f)];
            foreach (var s in spots) g.FillEllipse(spot, s.X - s.R, s.Y - cd * 0.5f - wd * 0.5f - s.R, s.R * 2, s.R * 2);
        }
        // The tail: a tuft at the rump that overlaps the body, white underneath (the flag), raised by "tail".
        float lift = tail * 0.5f;
        // At rest it lies along the curve of the rump, brown on top, mostly inside the body's outline, with the white
        // showing only at the tip below the rump; flagged, it swings up and the white underside shows whole.
        PointF root = P(-0.40f, 0.70f - cd);
        float ang = 4.1f - 1.4f * lift;                                   // radians: down and back along the rump; up when flagged
        float len = 0.13f;
        PointF tip = P(root.X + len * MathF.Cos(ang), root.Y + len * MathF.Sin(ang));
        float tx = -(tip.Y - root.Y), ty = tip.X - root.X;                // across the tail
        Blob([root, P(root.X + tx * 0.2f, root.Y + ty * 0.2f), P(tip.X + tx * 0.12f, tip.Y + ty * 0.12f), tip, P(tip.X - tx * 0.12f, tip.Y - ty * 0.12f), P(root.X - tx * 0.2f, root.Y - ty * 0.2f)], coatDark, 0.4f);
        float ws = 0.55f - 0.45f * lift;                                  // how far down the tail the white starts
        PointF wr = P(root.X + (tip.X - root.X) * ws, root.Y + (tip.Y - root.Y) * ws);
        Blob([P(wr.X - tx * 0.04f, wr.Y - ty * 0.04f), P(tip.X + tx * 0.08f, tip.Y + ty * 0.08f), P(tip.X - tx * 0.14f, tip.Y - ty * 0.14f), P(wr.X - tx * 0.2f, wr.Y - ty * 0.2f)], White, 0.4f);

        Leg(0, true, coat, coatDark);
        Leg(1, false, coat, coatDark);
    }

    /// <summary>
    /// The neck and head on a sheet whose origin is the neck's root. f: 0 = head up and alert, 1 = nose down in the
    /// stubble. kind: 0 jaw closed, 1 jaw open (chewing), 2 the near ear flicked back.
    /// </summary>
    private static void PaintHead(Graphics g, float f, int kind, bool fawn, bool buck, int h)
    {
        float pw = MathF.Max(0.01f, 1.0f / h);
        Color coat = fawn ? TanFawn : Tan, coatDark = fawn ? TanFawnDark : TanDark;
        float a = (24f + 136f * f) * MathF.PI / 180f;                      // the neck's angle from straight up, forward
        float ha = (84f + 90f * f) * MathF.PI / 180f;                      // the head's axis: forward when alert, down when grazing
        float len = 0.30f + 0.04f * f;                                     // the neck stretches a little to reach the ground (0.10 put the grazing nose below the hoof line)
        PointF root = P(0, 0);
        PointF top = P(len * MathF.Sin(a), len * MathF.Cos(a));
        PointF Along(PointF from, float ang, float d) => P(from.X + d * MathF.Sin(ang), from.Y + d * MathF.Cos(ang));
        PointF headMid = Along(top, ha, 0.10f);

        // The neck: a tapered shape, thick at the shoulders and slimmer at the throat, with a soft cream patch at its base.
        {
            float nx = MathF.Cos(a), ny = -MathF.Sin(a);                  // across the neck
            float w0 = 0.19f, w1 = 0.12f;
            using var b = new SolidBrush(coat);
            using var path = new GraphicsPath();
            path.AddClosedCurve([P(root.X + nx * w0 / 2, root.Y + ny * w0 / 2), P(top.X + nx * w1 / 2, top.Y + ny * w1 / 2), P(top.X - nx * w1 / 2, top.Y - ny * w1 / 2), P(root.X - nx * w0 / 2, root.Y - ny * w0 / 2)], 0.2f);
            g.FillPath(b, path);
            using var mane = new SolidBrush(Color.FromArgb(90, coatDark));
            PointF m0 = P(root.X - nx * w0 * 0.28f, root.Y - ny * w0 * 0.28f), m1 = P(top.X - nx * w1 * 0.25f, top.Y - ny * w1 * 0.25f);
            using var mp = new GraphicsPath();
            mp.AddClosedCurve([m0, m1, P(m1.X - nx * 0.03f, m1.Y - ny * 0.03f), P(m0.X - nx * 0.05f, m0.Y - ny * 0.05f)], 0.2f);
            g.FillPath(mane, mp);                                           // the darker back of the neck
        }

        // Two big ears, 0.14 long, angled outward from the back of the head, the dark inner edge showing; the far one first.
        foreach ((float spread, bool near) in ((float, bool)[])[(-0.34f, false), (0.38f, true)])
        {
            float ea = ha - 1.95f + spread + (near && kind == 2 ? 0.9f : 0f);   // the flick: the near ear swung back
            PointF mid = Along(top, ea, 0.11f);
            var es = g.Save();
            g.TranslateTransform(mid.X, mid.Y);
            g.RotateTransform(90f - ea * 180f / MathF.PI);
            using (var edge = new SolidBrush(EarEdge)) g.FillEllipse(edge, -0.085f, -0.045f, 0.17f, 0.09f);
            using (var ear = new SolidBrush(coat)) g.FillEllipse(ear, -0.08f, -0.04f, 0.15f, 0.075f);
            using (var inner = new SolidBrush(EarIn)) g.FillEllipse(inner, -0.05f, -0.02f, 0.10f, 0.038f);
            g.Restore(es);
        }

        // The head: a longer wedge (a skull oval and a muzzle polygon), the cream muzzle ring, a big dark nose, the eye with a
        // glint, and the jaw, dropped when chewing. In this turned frame +x is the way the nose points.
        var st = g.Save();
        g.TranslateTransform(headMid.X, headMid.Y);
        g.RotateTransform(90f - ha * 180f / MathF.PI);
        float ml = fawn ? 0.19f : 0.25f;                                   // a fawn's face is shorter
        if (buck)
        {
            // The antlers first, so the skull covers their roots: the ridge deer's shape (Happenings/Deer.cs), in bone
            // over a darker edge so they stand off the coat and the dusk field alike.
            Happenings.Deer.Antlers(g, EarEdge, MathF.Max(0.05f, 2.4f / h), 0.85f);
            Happenings.Deer.Antlers(g, Bone, MathF.Max(0.03f, 1.4f / h), 0.85f);
        }
        using (var skull = new SolidBrush(coat)) g.FillEllipse(skull, -0.13f, -0.085f, 0.26f, 0.17f);
        using (var muzzle = new SolidBrush(coat))
        using (var mp = new GraphicsPath())
        {
            // The muzzle tapers to a rounded nose, and the jaw curves under it (a straight chin line read as chopped off).
            mp.AddClosedCurve([P(0.0f, 0.07f), P(ml * 0.6f, 0.055f), P(ml, 0.04f), P(ml + 0.035f, -0.005f), P(ml, -0.045f), P(ml * 0.6f, -0.065f), P(0.02f, -0.08f), P(-0.06f, -0.06f)], 0.4f);
            g.FillPath(muzzle, mp);
        }
        if (kind == 1)
        {
            using var jaw = new SolidBrush(coatDark);
            g.FillPolygon(jaw, [P(0.04f, -0.06f), P(ml - 0.02f, -0.075f), P(ml + 0.01f, -0.03f), P(ml - 0.04f, -0.035f)]);   // the lower jaw dropped a little
        }
        using (var ring = new SolidBrush(Cream))
            g.FillPolygon(ring, [P(ml - 0.07f, 0.035f), P(ml, 0.035f), P(ml + 0.035f, -0.005f), P(ml, -0.045f), P(ml - 0.07f, -0.06f)]);
        using (var nose = new SolidBrush(Nose)) g.FillEllipse(nose, ml - 0.015f, -0.035f, 0.06f, 0.05f);
        using (var eye = new SolidBrush(Nose)) g.FillEllipse(eye, -0.01f, 0.0f, MathF.Max(0.04f, 2.2f * pw), MathF.Max(0.04f, 2.2f * pw));
        using (var glint = new SolidBrush(Color.White)) g.FillEllipse(glint, 0.01f, 0.02f, MathF.Max(0.013f, pw), MathF.Max(0.013f, pw));
        g.Restore(st);
    }

    // ================================================================ living

    private float FootY(Animal a) => _s.FieldY(a.X) + (_s.Ground.YAt(Math.Clamp(a.X, 0, _s.Width - 1)) - _s.U * 0.006f - _s.FieldY(a.X)) * a.Depth;
    private float Stride(Animal a) => 2 * StepReach * a.H / Duty;        // the foot slides 2R while it is down for Duty of the stride: planted

    public void Update(float dt)
    {
        float u = _s.U;
        _time += dt;
        Animal doe = _deer.Length > 0 ? _deer[0] : null!;
        for (int i = 0; i < _deer.Length; i++)
        {
            Animal a = _deer[i];
            a.Timer -= dt;
            a.Clock += dt;
            a.Senses.Tick(dt);
            _log?.WriteLine($"{_time,8:F3} deer{i} frame x {a.X:F1} v {a.Pace.Speed:F2} {a.Doing}/{a.Gait}");

            // ---- the neck: a damped spring toward where the doing wants it (slow off the mark, fastest in the middle, settling in about half a second) ----
            float want = a.Lent ? a.LentNeck : a.Doing switch { Doing.Graze => 1f, Doing.Look => 0f, Doing.Alert => 0f, Doing.Retreat => 0.15f, _ => 0.25f };
            a.NeckV += ((want - a.NeckF) * NeckSpring - a.NeckV * NeckDamp) * dt;
            a.NeckF = Math.Clamp(a.NeckF + a.NeckV * dt, 0f, 1f);
            // Bites while grazing with the nose down: bursts of a few, each a tug up, a hold and an ease down, then chewing.
            if (a.BiteT >= 0) { a.BiteT += dt; if (a.BiteT > 0.27f) { a.BiteT = -1; a.ChewT = 0; } }
            if (a.ChewT >= 0) { a.ChewT += dt; if (a.ChewT > 1.4f) a.ChewT = -1; }
            if (a.Doing == Doing.Graze && a.NeckF > 0.95f && a.BiteT < 0)
            {
                a.NextBite -= dt;
                if (a.NextBite <= 0)
                {
                    a.BiteT = 0;
                    if (a.BitesLeft <= 0) a.BitesLeft = 3 + _rng.Next(4);
                    a.BitesLeft--;
                    a.NextBite = a.BitesLeft > 0 ? 0.5f + 0.7f * (float)_rng.NextDouble() : 2f + 3f * (float)_rng.NextDouble();
                }
            }
            // The ear flick: six frames, now and then.
            if (a.EarT >= 0) { a.EarT += dt; if (a.EarT > 0.1f) a.EarT = -1; }
            else { a.NextEar -= dt; if (a.NextEar <= 0) { a.EarT = 0; a.NextEar = 2f + 4f * (float)_rng.NextDouble(); } }
            if (a.TailT >= 0) { a.TailT += dt; if (a.TailT > 0.27f) a.TailT = -1; }
            if (a.Lent) continue;                                   // a story moves it

            // ---- the senses: what happened, and what this deer makes of it (never while already retreating) ----
            if (a.Doing != Doing.Retreat && _test == null)
            {
                Reaction r = a.Senses.Check(_board, new PointF(a.X, FootY(a)), a, out Notice cause);
                if (r != Reaction.None) React(a, i, r, cause);
            }

            // ---- the gait ----
            switch (a.Gait)
            {
                case Gait.Rising:
                    a.GaitT += dt;
                    if (a.GaitT >= SettleFor) { a.Gait = Gait.Walking; a.Walked = 0; a.Pace.Walked = 0; a.Pace.Want = (a.Doing == Doing.Retreat ? RetreatSpeed : Speed) * u; }
                    break;
                case Gait.Walking:
                {
                    // The speed eases toward the walk's pace (Mover.cs); the stride is paged by the distance walked.
                    float move = a.Pace.Tick(dt);
                    a.X += a.Dir * move;
                    a.Walked = a.Pace.Walked;
                    break;
                }
                case Gait.Stopping:
                {
                    // The brake: the speed falls evenly to nothing over exactly the distance to the rest page.
                    float move = a.Pace.Tick(dt);
                    a.X += a.Dir * move;
                    a.Walked = a.Pace.Walked;
                    if (a.Pace.Still) { a.Gait = Gait.Settling; a.GaitT = 0; a.Walked = 0; a.Pace.Walked = 0; }
                    break;
                }
                case Gait.Settling:
                    a.GaitT += dt;
                    if (a.GaitT >= SettleFor)
                    {
                        a.Gait = Gait.Settled;
                        if (a.TurnWhenSettled != 0) { BeginTurn(a, a.TurnWhenSettled); a.TurnWhenSettled = 0; }
                    }
                    break;
                case Gait.Turning:
                {
                    // Coming round on the spot: Dir flips at the middle; a walk that was waiting sets off after it.
                    float was = a.TurnT;
                    a.TurnT += dt;
                    if (was < TurnFor / 2 && a.TurnT >= TurnFor / 2) a.Dir = a.TurnTo;
                    if (a.TurnT >= TurnFor) { if (a.Doing == Doing.Walk) { a.Gait = Gait.Rising; a.GaitT = 0; } else a.Gait = Gait.Settled; }
                    break;
                }
            }
            if (a.Gait == Gait.Walking)
            {
                // At the edge of the herd's range (between the maples' leaves) it stops, settles and turns, then walks on.
                float lo = _s.Width * 0.20f, hi = _s.Width * 0.78f;
                if (a.X < lo) { a.X = lo; StopWalk(a); a.Doing = Doing.Walk; a.TurnWhenSettled = 1; }
                if (a.X > hi) { a.X = hi; StopWalk(a); a.Doing = Doing.Walk; a.TurnWhenSettled = -1; }
            }

            if (a.Fawn && doe != null && !doe.Lent && a.Doing != Doing.Retreat && a.Doing != Doing.Alert)
            {
                // The fawn keeps close (Herd.cs): her place is a gap behind her mother on the side she came from; if the gap is
                // wrong by more than she can bear she trots to it, and stops once she is near it.
                float gap = Herd.FollowError(a.X, doe.X, doe.Dir, u * 0.09f, u * 0.05f);
                if (gap != 0f && a.Doing != Doing.Walk && a.Gait == Gait.Settled)
                {
                    StartWalk(a, gap > 0 ? 1 : -1);
                    a.Timer = 99f;
                    continue;
                }
                if (a.Doing == Doing.Walk && a.Gait == Gait.Walking && MathF.Abs(Herd.FollowX(doe.X, doe.Dir, u * 0.09f) - a.X) <= u * 0.02f) { StopWalk(a); a.Timer = 2f + 3f * (float)_rng.NextDouble(); if (doe.Dir != a.Dir) a.TurnWhenSettled = doe.Dir; }
                if (a.Doing == Doing.Walk) continue;
            }
            if (_test == "walk" && !a.Fawn) { a.Doing = Doing.Walk; a.Gait = a.Gait == Gait.Settled ? Gait.Walking : a.Gait; continue; }
            if (_test == "graze" && !a.Fawn) { a.Doing = Doing.Graze; continue; }
            if (_test == "cycle" && !a.Fawn)
            {
                // Walks for three seconds, stops, grazes for three, walks again: for watching the stop and the start.
                float c = a.Clock % 6.5f;
                if (c < 3f && a.Doing != Doing.Walk && a.Gait == Gait.Settled) StartWalk(a, -a.Dir);   // the other way each time: the turn pages show too
                if (c >= 3f && a.Doing == Doing.Walk && a.Gait == Gait.Walking) StopWalk(a);
                continue;
            }
            if (a.Timer > 0) continue;
            if (a.Gait != Gait.Settled && a.Gait != Gait.Walking) continue;   // finish the step, the settle or the turn first

            a.Clock = 0;
            if (a.Doing is Doing.Walk or Doing.Retreat) { StopWalk(a); a.Timer = Organic.Jitter(_rng, 5f); Log(a, i, "stop"); continue; }
            if (a.Doing == Doing.Alert) { a.Doing = Doing.Look; a.Timer = Organic.Jitter(_rng, 1.5f); continue; }   // the alarm passes: a look, then whatever is next
            // The dice: graze (the usual), look about (a timid deer more), walk off (a doe only; the fawn follows).
            int face = Dice.Pick(_rng, [0.55f, 0.25f + 0.2f * a.Timid, a.Fawn ? 0f : 0.2f]);
            switch (face)
            {
                case 0: a.Doing = Doing.Graze; a.Timer = Organic.Jitter(_rng, 5.5f); break;
                case 1: a.Doing = Doing.Look; a.Timer = Organic.Jitter(_rng, 2.5f); a.TailT = 0; break;
                default: StartWalk(a, _rng.Next(3) == 0 ? -a.Dir : a.Dir); a.Timer = Organic.Jitter(_rng, 4f); break;
            }
            Log(a, i, "roll");
        }
    }

    /// <summary>
    /// What a reaction is for a deer: a look is the head up that way for a moment; a freeze is the head up, still,
    /// ears forward, facing what it heard, for a few seconds (a walking deer stops square first); a flight is a brisk
    /// walk away from it, which also tells the herd (a Friend notice), so the others look up at once.
    /// </summary>
    private void React(Animal a, int i, Reaction r, Notice cause)
    {
        float u = _s.U;
        int toward = cause.At.X >= a.X ? 1 : -1;
        switch (r)
        {
            case Reaction.Look:
                if (a.Gait == Gait.Settled && a.Doing is Doing.Graze or Doing.Look) { a.Doing = Doing.Look; a.Timer = 1.2f + 1.5f * a.Senses.Curious; a.EarT = 0; if (toward != a.Dir) BeginTurn(a, toward); }
                break;
            case Reaction.Freeze:
            case Reaction.Approach:
                if (a.Gait == Gait.Walking) { StopWalk(a); a.TurnWhenSettled = toward != a.Dir ? toward : 0; }
                else if (a.Gait == Gait.Settled && toward != a.Dir) BeginTurn(a, toward);
                a.Doing = Doing.Alert; a.Timer = 2f + 3f * a.Timid; a.EarT = 0; a.TailT = 0;
                break;
            case Reaction.Flee:
                a.Doing = Doing.Retreat;
                a.Timer = 2f + 2.5f * a.Timid;
                if (a.Gait == Gait.Walking) { a.Pace.Want = RetreatSpeed * u; if (a.Dir == toward) { StopWalk(a); a.Doing = Doing.Retreat; a.TurnWhenSettled = -toward; } }
                else if (a.Gait == Gait.Settled) { StartWalk(a, -toward); a.Doing = Doing.Retreat; }
                a.TailT = 0;                                                                     // the white flag goes up
                _board.Post(Sense.Friend, new PointF(a.X, FootY(a)), 0.6f * u, 1f, a);        // the herd looks up
                break;
        }
        Log(a, i, $"react {r} to {cause.Kind}");
    }

    private void Log(Animal a, int i, string what)
    {
        _log?.WriteLine($"{_time,8:F2} deer{i} x {a.X:F0} v {a.Pace.Speed:F1} {a.Doing}/{a.Gait} {what}");
    }

    private void StartWalk(Animal a, int dir)
    {
        a.Doing = Doing.Walk;
        a.Clock = 0;
        if (dir != a.Dir) { BeginTurn(a, dir); return; }                   // the turn pages first; the walk rises after them (a mirror flip before)
        a.Gait = Gait.Rising;
        a.GaitT = 0;
    }

    private static void BeginTurn(Animal a, int dir)
    {
        a.Gait = Gait.Turning; a.TurnTo = dir; a.TurnT = 0;
    }

    /// <summary>Begins a stop: the brake runs the walk down so that it ends exactly on the rest page, then the settle pages play.</summary>
    private void StopWalk(Animal a)
    {
        a.Doing = Doing.Graze;
        if (a.Gait != Gait.Walking) return;
        a.Gait = Gait.Stopping;
        a.Pace.Brake(a.Pace.ToRestPage(Stride(a)));                       // at least a quarter stride away: closer, it takes one more, so the stop is even
    }

    /// <summary>Stamps the herd through the field stencil: the doe, then the fawn (nearer), then any other doe.</summary>
    public void Draw(FrameBuffer fb)
    {
        foreach (Animal a in _deer)
        {
            bool mirror = a.Dir < 0;
            float foot = FootY(a);
            Sprite body;
            PointF neck;
            float stride = Stride(a);
            int turnPage = -1;                                              // the turn's squash page, or -1
            switch (a.Gait)
            {
                case Gait.Turning:
                {
                    // Narrowing while it still faces the old way (Dir flips at the middle), then widening facing the new.
                    float q = a.TurnT / TurnFor;
                    turnPage = q < 0.5f ? Math.Min(TurnPages - 1, (int)(q * 2 * TurnPages)) : Math.Min(TurnPages - 1, (int)((1 - q) * 2 * TurnPages));
                    body = a.TurnBody[turnPage]; neck = P(NeckX * Squash[turnPage], NeckY);
                    break;
                }
                case Gait.Walking:
                case Gait.Stopping:
                {
                    float cycle = a.Walked / stride;
                    int page = (int)MathF.Floor((cycle - MathF.Floor(cycle)) * WalkPages) % WalkPages;
                    body = a.Walk[page]; neck = a.NeckWalk[page];
                    break;
                }
                case Gait.Settling:
                case Gait.Rising:
                {
                    float k = Math.Clamp(a.GaitT / SettleFor, 0f, 0.999f);
                    int page = (int)(k * SettlePages);
                    if (a.Gait == Gait.Rising) page = SettlePages - 1 - page;
                    body = a.Settle[page]; neck = a.NeckSettle[page];
                    break;
                }
                default:
                {
                    // Standing: the tail flicks up and down twice when she lifts her head.
                    int tp = 0;
                    if (a.TailT >= 0) tp = new[] { 1, 2, 1, 0, 1, 2, 1, 0 }[Math.Min(7, (int)(a.TailT / 0.27f * 8))];
                    body = a.Tail[tp]; neck = P(NeckX, NeckY);
                    break;
                }
            }
            int left = (int)MathF.Round(a.X) - a.BodyOx;
            int top = (int)MathF.Round(foot) - a.BodyOy;
            body.Draw(fb, left, top, 1f, _s.OpenField, mirror);

            // The head: the neck page, with the bite's tug (up and back, held, eased down) taken off it; chewing opens the
            // jaw on alternate beats; the ear flick is its own kind of page.
            float f = a.NeckF;
            // A standing head is never quite still: a slow drift of a page or so (Organic.Drift), each deer on its own seed.
            if (a.Gait == Gait.Settled && !a.Lent && a.Doing != Doing.Alert) f += 0.03f * Organic.Drift(_time * 0.6, a.Restless);
            if (a.BiteT >= 0)
            {
                float t = a.BiteT;
                float tug = t < 0.067f ? t / 0.067f : t < 0.1f ? 1f : 1f - Smooth((t - 0.1f) / 0.17f);
                f -= 0.09f * tug;
            }
            int hp = Math.Clamp((int)MathF.Round(f * (HeadPages - 1)), 0, HeadPages - 1);
            int kind = a.ChewT >= 0 ? ((int)(a.ChewT * 3) % 2) : a.EarT >= 0 ? 2 : 0;      // a chew is a jaw cycle and a half a second (six was a shiver)
            float neckX = a.X + (mirror ? -1 : 1) * neck.X * a.H, neckY = foot - neck.Y * a.H;
            int hl = (int)MathF.Round(neckX) - a.HeadS / 2, ht = (int)MathF.Round(neckY) - (int)(a.HeadS * HeadRootF);
            (turnPage < 0 ? a.Head[hp, kind] : a.TurnHead[hp, turnPage]).Draw(fb, hl, ht, 1f, _s.OpenField, mirror);
        }
    }

    private static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
}
