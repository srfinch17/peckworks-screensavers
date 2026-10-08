using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace CabinByStream;

/// <summary>
/// Squirrels running around the trees.
///
/// Each squirrel follows a PLAN: a short list of legs, each leg one thing to
/// do. Sit at the foot of a tree. Run across the grass to the next tree
/// (perhaps stopping halfway to sit up and look about). Climb the trunk.
/// Sit up there. Climb back down. Run back. When the plan runs out a new
/// one is made with fresh dice, so no two trips are quite the same.
///
/// A leg is a straight run from one point to another at a speed in u per
/// second (never "cross in N seconds": a wide screen would make the squirrel
/// a blur), or a sit of so many seconds. The squirrel's position is simply
/// "how far along the current leg am I", so there is nothing to drift.
///
/// THE DRAWING. One squirrel facing right is painted in a few poses (a
/// four-page running bound, and sitting up), each a sprite (Core/Sprite.cs).
/// A squirrel running left is the same sprite stamped mirrored. For climbing,
/// the running pages are painted again turned a quarter turn, head up, and
/// a quarter turn the other way, head down, because a squirrel comes down a
/// tree head first.
///
/// WHICH TREES. The scenery lists its trees left to right. The first
/// squirrel takes the two rightmost (they are close together, a natural pair
/// to dash between). The next takes the leftmost tree and a spot out in the
/// grass to run to and back. Further squirrels share those routes, starting
/// at different points of the plan.
/// </summary>
internal sealed class Squirrels
{
    private enum Mode { Run, ClimbUp, ClimbDown, Sit }

    private readonly record struct Leg(Mode Mode, PointF From, PointF To, float Seconds);

    private sealed class Squirrel
    {
        public TreeFacts Home = null!;         // the tree the plan starts at
        public TreeFacts? Other;               // the second tree, or null when the other end is a spot in the grass
        public PointF Spot;                    // that spot
        public List<Leg> Plan = [];
        public int LegIndex;
        public float Progress;                 // 0 to 1 along the current leg (or seconds sat, for a sit)
        public float Gait;                     // the flip-book clock, wrapped to one turn
        public bool FacingRight = true;
    }

    private const int RunPoses = 4;

    private readonly MeadowScenery _s;
    private readonly Squirrel[] _squirrels;
    private readonly Sprite[] _run = new Sprite[RunPoses], _up = new Sprite[RunPoses], _down = new Sprite[RunPoses];
    private readonly Sprite _sit;
    private readonly float _u, _len;
    private readonly Random _rng;

    public Squirrels(MeadowScenery s, int count, Random rng)
    {
        _s = s; _u = s.U; _rng = rng;
        _len = Math.Max(10f, s.U * 0.046f);                 // body length, nose to rump; never under 10 px for the preview box
        for (int i = 0; i < RunPoses; i++)
        {
            float bound = MathF.Sin(MathF.Tau * i / RunPoses);  // -1 gathered ... +1 stretched
            _run[i] = PaintSquirrel(_len, bound, sitting: false, turn: 0);
            _up[i] = PaintSquirrel(_len, bound, sitting: false, turn: -90);
            _down[i] = PaintSquirrel(_len, bound, sitting: false, turn: 90);
        }
        _sit = PaintSquirrel(_len, 0, sitting: true, turn: 0);

        _squirrels = new Squirrel[s.Trees.Count == 0 ? 0 : Math.Max(0, count)];
        for (int i = 0; i < _squirrels.Length; i++)
        {
            var sq = new Squirrel();
            if (i % 2 == 0 && s.Trees.Count >= 2)
            {
                // The rightmost pair.
                sq.Home = s.Trees[^1];
                sq.Other = s.Trees[^2];
            }
            else
            {
                // The leftmost tree and a spot out in the grass to its right.
                sq.Home = s.Trees[0];
                sq.Spot = new PointF(sq.Home.Foot.X + _u * (0.16f + 0.08f * (float)rng.NextDouble()), sq.Home.Foot.Y + _u * 0.012f);
            }
            MakePlan(sq);
            // Start part way through, so two squirrels on the same route are not in step.
            sq.LegIndex = rng.Next(sq.Plan.Count);
            sq.Progress = sq.Plan[sq.LegIndex].Mode == Mode.Sit ? 0 : (float)rng.NextDouble();
            _squirrels[i] = sq;
        }
    }

    /// <summary>
    /// Writes one round trip: sit at home, run to the other end (sometimes
    /// stopping to sit up halfway), climb it if it is a tree (sit at the
    /// top, climb down), run home, climb home, sit, climb down. Speeds are
    /// in u per second; a sit's length is in seconds.
    /// </summary>
    private void MakePlan(Squirrel sq)
    {
        float R() => (float)_rng.NextDouble();
        var plan = new List<Leg>();
        PointF homeFoot = Stand(sq.Home.Foot), farFoot = sq.Other != null ? Stand(sq.Other.Foot) : sq.Spot;

        void Sit(PointF at, float secs) => plan.Add(new Leg(Mode.Sit, at, at, secs));
        void Run(PointF a, PointF b)
        {
            if (R() < 0.45f)
            {
                // Stop part way: run, sit up and look about, run on.
                float t = 0.3f + 0.4f * R();
                PointF mid = new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                plan.Add(new Leg(Mode.Run, a, mid, RunSeconds(a, mid)));
                Sit(mid, 0.8f + 1.4f * R());
                plan.Add(new Leg(Mode.Run, mid, b, RunSeconds(mid, b)));
            }
            else plan.Add(new Leg(Mode.Run, a, b, RunSeconds(a, b)));
        }
        void Climb(TreeFacts tree)
        {
            PointF foot = Stand(tree.Foot), top = tree.ClimbTop;
            // Not always all the way up: a squirrel often stops part way.
            float how = 0.55f + 0.45f * R();
            PointF perch = new(foot.X + (top.X - foot.X) * how, foot.Y + (top.Y - foot.Y) * how);
            plan.Add(new Leg(Mode.ClimbUp, foot, perch, Dist(foot, perch) / (_u * 0.22f)));
            Sit(perch, 1.5f + 3f * R());
            plan.Add(new Leg(Mode.ClimbDown, perch, foot, Dist(perch, foot) / (_u * 0.20f)));
        }

        Sit(homeFoot, 1f + 2f * R());
        Run(homeFoot, farFoot);
        if (sq.Other != null) Climb(sq.Other); else Sit(farFoot, 1.5f + 2.5f * R());
        Run(farFoot, homeFoot);
        Climb(sq.Home);
        sq.Plan = plan;
    }

    private float RunSeconds(PointF a, PointF b) => Dist(a, b) / (_u * (0.30f + 0.12f * (float)_rng.NextDouble()));
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Where to stand at a tree: at the foot of its trunk, a little in front (the trunk's foot is painted slightly above its base line).</summary>
    private PointF Stand(PointF foot) => new(foot.X, foot.Y + _u * 0.004f);

    public void Update(float dt)
    {
        foreach (Squirrel sq in _squirrels)
        {
            Leg leg = sq.Plan[sq.LegIndex];
            if (leg.Mode == Mode.Sit)
            {
                sq.Progress += dt;
                if (sq.Progress < leg.Seconds) continue;
            }
            else
            {
                sq.Progress += dt / Math.Max(0.05f, leg.Seconds);
                if (leg.To.X != leg.From.X) sq.FacingRight = leg.To.X > leg.From.X;
                // The bound: about seven pages a second on the ground, slower on the trunk.
                sq.Gait = (sq.Gait + (leg.Mode == Mode.Run ? 7f : 5f) * MathF.Tau / RunPoses * dt) % MathF.Tau;
                if (sq.Progress < 1f) continue;
            }
            // On to the next leg; a fresh plan when this one is done.
            sq.Progress = 0;
            if (++sq.LegIndex >= sq.Plan.Count) { MakePlan(sq); sq.LegIndex = 0; }
        }
    }

    public void Draw(FrameBuffer fb)
    {
        foreach (Squirrel sq in _squirrels)
        {
            Leg leg = sq.Plan[sq.LegIndex];
            float t = leg.Mode == Mode.Sit ? 0 : Math.Min(1f, sq.Progress);
            float x = leg.From.X + (leg.To.X - leg.From.X) * t, y = leg.From.Y + (leg.To.Y - leg.From.Y) * t;
            int page = (int)(sq.Gait / MathF.Tau * RunPoses) % RunPoses;
            Sprite sprite = leg.Mode switch
            {
                Mode.Run => _run[page],
                Mode.ClimbUp => _up[page],
                Mode.ClimbDown => _down[page],
                _ => _sit,
            };
            // The sprite's centre is above the feet; on a trunk it is beside
            // the climbing point (the squirrel hugs the near face of the trunk).
            float cy = leg.Mode is Mode.Run or Mode.Sit ? y - sprite.Height * 0.40f : y;
            bool climbing = leg.Mode is Mode.ClimbUp or Mode.ClimbDown;
            // Sitting on the trunk after a climb: the sit pose, facing out the way it came from.
            sprite.DrawCentered(fb, MathF.Round(x), MathF.Round(cy), 1f, null, mirror: !sq.FacingRight && !climbing);
        }
    }

    /// <summary>
    /// One squirrel facing right. "bound" says where it is in a running
    /// leap: +1 stretched out (legs reaching fore and aft, tail streaming),
    /// -1 gathered (legs bunched under it, back arched, tail curled high).
    /// Sitting: the body upright on its haunches, paws together, the tail
    /// up behind in an S. "turn" rotates the whole drawing, for climbing.
    /// </summary>
    private static Sprite PaintSquirrel(float L, float bound, bool sitting, float turn)
    {
        int size = (int)(L * 1.7f) + 4;
        Color fur = Color.FromArgb(128, 74, 40), dark = Color.FromArgb(78, 44, 26), belly = Color.FromArgb(196, 170, 140);
        return Sprite.Paint(size, size, g =>
        {
            g.TranslateTransform(size / 2f, size / 2f);
            g.RotateTransform(turn);
            PointF P(float ax, float ay) => new(ax * L, ay * L);             // positions in body lengths, (0,0) at the body's middle
            void Oval(Brush b, float cx, float cy, float rx, float ry) => g.FillEllipse(b, (cx - rx) * L, (cy - ry) * L, rx * 2 * L, ry * 2 * L);
            Pen Stroke(Color c, float width) => new(c, Math.Max(1f, width * L)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var furBrush = new SolidBrush(fur);
            using var darkBrush = new SolidBrush(dark);
            using var bellyBrush = new SolidBrush(belly);
            using var eye = new SolidBrush(Color.FromArgb(24, 18, 16));

            if (sitting)
            {
                // Tail first (behind), a thick S curve up and over the back.
                using (Pen tail = Stroke(dark, 0.20f))
                    g.DrawBezier(tail, P(-0.30f, 0.30f), P(-0.62f, 0.10f), P(-0.60f, -0.50f), P(-0.25f, -0.62f));
                Oval(furBrush, -0.12f, 0.08f, 0.26f, 0.34f);                      // haunches and back
                Oval(bellyBrush, 0.02f, 0.14f, 0.13f, 0.24f);                      // pale belly
                Oval(furBrush, 0.08f, -0.36f, 0.15f, 0.13f);                       // head
                g.FillPolygon(furBrush, [P(0.00f, -0.46f), P(-0.02f, -0.60f), P(0.08f, -0.47f)]);   // ear
                using (Pen paw = Stroke(fur, 0.07f)) g.DrawLine(paw, P(0.10f, -0.14f), P(0.20f, -0.08f));   // paws held up
                Oval(furBrush, -0.10f, 0.40f, 0.18f, 0.06f);                       // hind feet
                g.FillEllipse(eye, 0.16f * L - 0.025f * L, -0.38f * L - 0.025f * L, 0.05f * L, 0.05f * L);
                return;
            }

            // Running. The body is a leaning oval that arches when gathered;
            // the tail streams behind when stretched and curls up when gathered.
            float arch = 0.10f * (1 - bound) / 2;                                  // 0 stretched ... 0.1 gathered
            float tailLift = -0.25f - 0.35f * (1 - bound) / 2;
            using (Pen tail = Stroke(dark, 0.19f))
                g.DrawBezier(tail, P(-0.42f, 0.02f), P(-0.70f, 0.05f + tailLift * 0.5f), P(-0.85f, tailLift), P(-0.62f, tailLift - 0.25f));
            // Legs: fore legs reach forward when stretched, hind legs reach back.
            using (Pen leg = Stroke(dark, 0.075f))
            {
                float reach = 0.22f * bound;
                g.DrawLine(leg, P(0.22f, 0.08f), P(0.32f + reach, 0.30f));            // fore
                g.DrawLine(leg, P(0.16f, 0.10f), P(0.20f + reach * 0.7f, 0.30f));
                g.DrawLine(leg, P(-0.28f, 0.06f), P(-0.36f - reach, 0.30f));          // hind
                g.DrawLine(leg, P(-0.22f, 0.08f), P(-0.26f - reach * 0.7f, 0.30f));
            }
            Oval(furBrush, -0.05f, 0.0f - arch * 0.5f, 0.40f, 0.17f + arch);      // body
            Oval(bellyBrush, 0.0f, 0.10f, 0.26f, 0.08f);                           // belly
            Oval(furBrush, 0.40f, -0.08f, 0.14f, 0.12f);                           // head
            g.FillPolygon(furBrush, [P(0.36f, -0.17f), P(0.33f, -0.30f), P(0.42f, -0.18f)]);   // ear
            g.FillEllipse(eye, 0.44f * L - 0.025f * L, -0.11f * L - 0.025f * L, 0.05f * L, 0.05f * L);
        });
    }
}
