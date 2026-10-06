using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// Hanafubuki, the "flower blizzard": a sudden gust of spring wind tears a
/// cloud of cherry petals off the branches and sweeps it across the screen,
/// right past your nose. In Japan people stand in it and laugh.
///
/// THE RIVER. Imagine a wide, invisible river of air slanting across the
/// screen. We describe every petal in the river's own terms: "u" is how far
/// along the river it has gone, "v" is how far it sits to one side of the
/// river's middle. Most petals sit near the middle (dense) and a few drift
/// out toward the banks (sparse), which is what makes it look like a stream
/// and not like noise.
///
/// WHERE PETALS START. Some start on the blossom clusters of the branches
/// (they are "torn off": they sit still, then speed up over about a second and
/// merge into the stream). The rest start just beyond the windward edge of the
/// screen and arrive already racing. All are released within the first second.
///
/// THE VORTEX. A big slow whirl of air rides along inside the river at the
/// same speed as the petals. Around its center, each petal is turned about the
/// center by an angle that grows with time and fades with distance. Petals
/// close to the middle go round the most; ones further out are only bent, so
/// the cloud winds into a spiral, like cream stirred into coffee.
///
/// THE PETALS. A petal is a small pink sprite. To tumble, it is painted at 12
/// turns of rotation and at 4 amounts of "edge on" (a petal seen flat, then
/// tilting away to a sliver). Four sizes: the big ones are nearest, fastest,
/// and a little soft. Everything is a function of t, so it can be rendered at
/// any moment.
/// </summary>
internal sealed class Hanafubuki : Happening
{
    private const int Sizes = 4, Shades = 4, Turns = 12, Squashes = 4;
    private const int MaxPetals = 400;
    private const float FlowU = 0.55f;               // the river's speed, screen heights per second
    private static readonly float[] SizeU = [0.009f, 0.014f, 0.020f, 0.027f];   // petal length as a fraction of U
    private static readonly float[] SizeFloor = [4f, 5f, 6f, 8f];                // never smaller than this many pixels
    private static readonly float[] SquashLevel = [1f, 0.68f, 0.40f, 0.20f];    // how flat-on the petal is

    // Petal colors: (tip, base) pairs from pale to deep pink.
    private static readonly (Color Tip, Color Base)[] Pinks =
    [
        (Color.FromArgb(255, 242, 246), Color.FromArgb(252, 224, 234)),
        (Color.FromArgb(254, 226, 236), Color.FromArgb(247, 194, 212)),
        (Color.FromArgb(250, 202, 220), Color.FromArgb(240, 156, 188)),
        (Color.FromArgb(244, 166, 196), Color.FromArgb(224, 112, 154)),
    ];

    private readonly Scenery _s;
    private readonly Sprite[,,,] _petal;              // [size, shade, turn, squash]
    private float _seconds = 7f;

    // The river and its vortex (this showing)
    private PointF _a, _d, _n;                        // river centre, direction along it, direction across it (unit vectors)
    private float _vMid, _uVortex0, _vVortex;
    // The petals (this showing)
    private int _count;
    private readonly byte[] _size = new byte[MaxPetals], _shade = new byte[MaxPetals];
    private readonly bool[] _fromBranch = new bool[MaxPetals];
    private readonly float[] _u0 = new float[MaxPetals], _v0 = new float[MaxPetals], _vLane = new float[MaxPetals];
    private readonly float[] _speed = new float[MaxPetals], _release = new float[MaxPetals];
    private readonly float[] _spin = new float[MaxPetals], _rot0 = new float[MaxPetals];
    private readonly float[] _tumble = new float[MaxPetals], _tum0 = new float[MaxPetals];
    private readonly float[] _psi1 = new float[MaxPetals], _psi2 = new float[MaxPetals];
    private readonly int[] _order = new int[MaxPetals];   // petal numbers sorted small (far) to big (near)

    public override float Seconds => _seconds;
    public override int Layer => 2;                   // right in front of the viewer, even over the falling petals

    public Hanafubuki(Scenery s)
    {
        _s = s;
        _petal = new Sprite[Sizes, Shades, Turns, Squashes];
        for (int z = 0; z < Sizes; z++)
        {
            float len = Math.Max(SizeFloor[z], SizeU[z] * s.U);
            for (int c = 0; c < Shades; c++)
                for (int r = 0; r < Turns; r++)
                    for (int q = 0; q < Squashes; q++)
                        _petal[z, c, r, q] = PaintPetal(len, Pinks[c], 360f * r / Turns, SquashLevel[q], z == Sizes - 1);
        }
    }

    /// <summary>One cherry petal: a long oval with a notch in its wide end, pale at the tip and deeper at the stalk.</summary>
    private static Sprite PaintPetal(float len, (Color Tip, Color Base) col, float rotDeg, float squash, bool soft)
    {
        int box = (int)MathF.Ceiling(len * 1.2f) + 4;
        return Sprite.Paint(box, box, g =>
        {
            g.TranslateTransform(box / 2f, box / 2f);
            g.RotateTransform(rotDeg);
            g.ScaleTransform(len * squash, len);          // squash narrows it across its width, as if tilting it away
            PointF P(float x, float y) => new(x, y);
            using var path = new GraphicsPath();
            // The outline, from the stalk (bottom) round to the two notched tips (top) and back.
            path.AddBezier(P(0, 0.5f), P(-0.12f, 0.38f), P(-0.46f, 0.10f), P(-0.36f, -0.28f));
            path.AddBezier(P(-0.36f, -0.28f), P(-0.30f, -0.42f), P(-0.26f, -0.52f), P(-0.20f, -0.50f));
            path.AddBezier(P(-0.20f, -0.50f), P(-0.14f, -0.48f), P(-0.06f, -0.40f), P(0, -0.35f));
            path.AddBezier(P(0, -0.35f), P(0.06f, -0.40f), P(0.14f, -0.48f), P(0.20f, -0.50f));
            path.AddBezier(P(0.20f, -0.50f), P(0.26f, -0.52f), P(0.30f, -0.42f), P(0.36f, -0.28f));
            path.AddBezier(P(0.36f, -0.28f), P(0.46f, 0.10f), P(0.12f, 0.38f), P(0, 0.5f));
            path.CloseFigure();

            // Nearest petals are slightly out of focus: a faint wider halo round the edge.
            if (soft)
            {
                using var halo = new Pen(Color.FromArgb(70, col.Base), 0.10f);
                g.DrawPath(halo, path);
            }
            using var br = new LinearGradientBrush(P(0, -0.5f), P(0, 0.5f), col.Tip, col.Base);
            g.FillPath(br, path);
            // A pale vein from the stalk up toward the notch, only worth drawing on the big ones.
            if (len >= 18)
            {
                using var vein = new Pen(Color.FromArgb(70, 255, 255, 255), 0.03f);
                g.DrawLine(vein, 0, 0.42f, 0, -0.30f);
            }
        });
    }

    public override void Begin(Random rng)
    {
        float u = _s.U, W = _s.Width, H = _s.Height;
        // The river: slants across the screen, coming from the left or the right,
        // tilted a little downhill (the gust pushes down and across).
        float dirx = rng.Next(2) == 0 ? 1f : -1f;
        float theta = (0.10f + 0.28f * rng.NextSingle()) * (rng.Next(3) == 0 ? -1f : 1f);
        _d = new PointF(dirx * MathF.Cos(theta), MathF.Sin(theta));
        _n = new PointF(-_d.Y, _d.X);
        _a = new PointF(W / 2f, H * (0.45f + 0.1f * rng.NextSingle()));
        _vMid = (rng.NextSingle() - 0.5f) * 0.2f * u;
        // Half the screen's length measured along the river (how far from the middle to the edge).
        float h = (W * MathF.Abs(_d.X) + H * MathF.Abs(_d.Y)) / 2f + 0.05f * u;

        _count = rng.Next(340, MaxPetals + 1);
        float spread = 0.38f * u;                          // the cloud is about this long
        float uStart = -(h + 0.12f * u);                   // just beyond the windward edge
        _uVortex0 = uStart - spread * 0.5f;                // the whirl sits in the middle of the cloud
        _vVortex = _vMid + (rng.NextSingle() - 0.5f) * 0.16f * u;

        bool useBranches = _s.BranchSpots.Count >= 4;
        float laneSigma = 0.11f * u;
        float lastLeave = 0;
        for (int i = 0; i < _count; i++)
        {
            float z = rng.NextSingle();                    // 0 far ... 1 near
            _size[i] = (byte)(z < 0.30f ? 0 : z < 0.62f ? 1 : z < 0.88f ? 2 : 3);
            _shade[i] = (byte)rng.Next(Shades);
            _speed[i] = FlowU * u * (0.95f + 0.10f * z);   // the nearer ones are faster

            // A lane across the river: a bell curve, so the middle is dense and the banks sparse.
            float g = (rng.NextSingle() + rng.NextSingle() + rng.NextSingle() - 1.5f) / 0.5f;   // roughly -3..3, bell-shaped
            _vLane[i] = _vMid + Math.Clamp(g, -2.4f, 2.4f) * laneSigma * 0.8f;

            _fromBranch[i] = useBranches && rng.NextSingle() < 0.38f;
            // Branch petals let go anywhere in the first second; edge ones arrive in a tighter burst so the cloud is dense.
            _release[i] = _fromBranch[i] ? rng.NextSingle() : 0.45f * rng.NextSingle();
            if (_fromBranch[i])
            {
                PointF spot = _s.BranchSpots[rng.Next(_s.BranchSpots.Count)];
                float dx = spot.X - _a.X, dy = spot.Y - _a.Y;
                _u0[i] = dx * _d.X + dy * _d.Y;
                _v0[i] = dx * _n.X + dy * _n.Y;
            }
            else
            {
                _u0[i] = uStart - spread * rng.NextSingle();
                _v0[i] = _vLane[i];
            }
            _spin[i] = (1.5f + 5f * rng.NextSingle()) * (rng.Next(2) == 0 ? -1f : 1f);
            _rot0[i] = rng.NextSingle() * MathF.Tau;
            _tumble[i] = 2f + 4f * rng.NextSingle();
            _tum0[i] = rng.NextSingle() * MathF.Tau;
            _psi1[i] = rng.NextSingle() * MathF.Tau;
            _psi2[i] = rng.NextSingle() * MathF.Tau;
            _order[i] = i;

            // When does this petal's trail leave the screen at the far end?
            float uEnd = h + 0.1f * u;
            float ramp = _fromBranch[i] ? 0.45f : 0;       // a branch petal loses about half a second getting up to speed
            lastLeave = Math.Max(lastLeave, _release[i] + (uEnd - _u0[i]) / _speed[i] + ramp);
        }
        // Sort petals small to big so the nearest are drawn last (on top).
        var keys = new byte[_count];
        Array.Copy(_size, keys, _count);
        Array.Sort(keys, _order, 0, _count);
        _seconds = Math.Clamp(lastLeave + 0.3f, 4f, 12f);
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        float u = _s.U;
        float fade = Fade(t, Seconds, 0.2f, 0.7f);
        if (fade <= 0.01f) return;
        float uC = _uVortex0 + FlowU * u * t;              // where the whirl is along the river now
        float sigma = 0.20f * u;
        const float Omega = 1.3f;                          // how fast the middle of the whirl turns, radians per second

        for (int k = 0; k < _count; k++)
        {
            int i = _order[k];
            float a = t - _release[i];                     // this petal's age
            if (a < 0) continue;

            // 1. Along the river: a branch petal eases up to speed, an edge petal is already at speed.
            float uu, vv;
            if (_fromBranch[i])
            {
                const float ta = 0.9f;
                float dist = a < ta ? _speed[i] * a * a / (2 * ta) : _speed[i] * (a - ta / 2);
                uu = _u0[i] + dist;
                vv = _v0[i] + (_vLane[i] - _v0[i]) * Smooth(a / 1.6f);   // and drifts across into its lane
            }
            else
            {
                uu = _u0[i] + _speed[i] * a;
                vv = _vLane[i];
            }
            // 2. The river wanders a little, and each petal flutters on its own.
            float zf = 0.5f + 0.25f * _size[i];
            vv += 0.045f * u * MathF.Sin(uu / u * 1.6f + 0.8f) + 0.016f * u * zf * MathF.Sin(3.1f * a + _psi2[i]);
            uu += 0.012f * u * zf * MathF.Sin(2.2f * a + _psi1[i]);

            // 3. The vortex: turn the petal about the whirl's center, more the closer it is.
            float qu = uu - uC, qv = vv - _vVortex;
            float g = MathF.Exp(-(qu * qu + qv * qv) / (2 * sigma * sigma));
            float phi = Omega * a * g;
            float cs = MathF.Cos(phi), sn = MathF.Sin(phi);
            uu = uC + qu * cs - qv * sn;
            vv = _vVortex + qu * sn + qv * cs;

            float x = _a.X + _d.X * uu + _n.X * vv;
            float y = _a.Y + _d.Y * uu + _n.Y * vv;
            if (x < -40 || x > _s.Width + 40 || y < -40 || y > _s.Height + 40) continue;   // off screen: no work

            // Tumble: which turn of rotation, and how edge-on it is right now.
            float rot = (_rot0[i] + _spin[i] * a) % MathF.Tau;
            if (rot < 0) rot += MathF.Tau;
            int r = (int)(rot / MathF.Tau * Turns) % Turns;
            float flat = MathF.Abs(MathF.Cos(_tum0[i] + _tumble[i] * a));          // 1 = full face, 0 = edge on
            int q = flat > 0.84f ? 0 : flat > 0.55f ? 1 : flat > 0.28f ? 2 : 3;

            float opacity = fade * (_size[i] == 3 ? 0.80f : 0.93f);
            if (_fromBranch[i]) opacity *= Smooth(a / 0.25f);                      // a torn-off petal appears from the blossom, not from nowhere
            _petal[_size[i], _shade[i], r, q].DrawCentered(fb, x, y, opacity);
        }
    }
}
