using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// A small lizard on the bank: a kanahebi, the Japanese grass lizard, the
/// brown lizard with the very long tail that lives in every sunny Japanese
/// garden. It is hidden most of the time; now and then it comes out, runs
/// about the grass and the rocks, basks on a stone, and is gone.
///
/// WHAT A GRASS LIZARD DOES (watch one on a warm wall): it moves in DASHES,
/// a quick sprint of half a second or so and then dead still, head up,
/// for a second or three, then another dash. Running, its body and tail
/// throw an S-wave side to side, the legs on opposite corners stepping
/// together; still, it is a straight stick with its tail in a lazy curve.
/// It climbs onto a rock and lies flat in the sun for a while. Anything
/// startling and it is under the grass in a blink.
///
/// HOW IT IS DRAWN: along a bending spine like the koi (Body.cs), the head
/// following its path and the body and long tail laid along the trail
/// behind it, so every turn bends it through the curve; a running wave is
/// added when it moves and dies away when it stops. The four legs are small
/// ovals swinging from the body, as the turtle's are. A faint shadow is the
/// same body painted dark a little down and to the right, so it sits ON the
/// ground rather than floating over it. Drawn after the lawn (it runs over
/// the grass and the rocks) and before the irises (it can pass under their
/// leaves), only where no branch is in the way, in the branches' shade.
/// </summary>
internal sealed class Lizard : Body
{
    private const int Points = 16;
    private enum Doing { Hidden, Dash, Freeze, Bask }

    private readonly Pond _pond;
    private readonly Random _rng;
    private readonly float _u, _len, _wide, _often;
    private float _x, _y, _heading, _speed, _amp, _phase;
    private float _goalX, _goalY, _timer, _gait;
    private int _dashes;                      // dashes left before it heads for a rock or leaves
    private Doing _doing = Doing.Hidden;
    private bool _leaving, _shadowPass;
    private readonly PointF[] _trail = new PointF[64];
    private int _trailHead, _trailCount;

    /// <summary>Where its nose is on the screen (for the instruments). Meaningless while hidden.</summary>
    public PointF Nose => Spine[0];
    public PointF TailTip => Spine[Points - 1];
    public bool Visible => _doing != Doing.Hidden;

    public Lizard(Pond pond, float often, Random rng) : base(Points)
    {
        _pond = pond;
        _rng = rng;
        _u = pond.U;
        _often = often;
        _len = _u * 0.09f;                   // nose to tail tip; the tail is well over half of it
        _wide = _len * 0.05f;
        for (int i = 0; i < Points; i++) Along[i] = i / (Points - 1f);
        _timer = often > 0 ? (15 + 30 * (float)rng.NextDouble()) / often : float.MaxValue;
    }

    private float CentreY => _pond.Centre.Y;
    private (float, float) ToGround(float x, float y) => (x, CentreY + (y - CentreY) / Ripples.Squash);
    private PointF ToScreen(float gx, float gy) => new(gx, CentreY + (gy - CentreY) * Ripples.Squash);
    private static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
    private static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Is this ground point on the land? A point just off the screen counts as whatever the nearest edge pixel is (the land runs on out of view).</summary>
    private bool OnLand(float gx, float gy)
    {
        PointF s = ToScreen(gx, gy);
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Land[iy * _pond.Width + ix];
    }

    private bool LandAt(PointF s)
    {
        int ix = Math.Clamp((int)s.X, 0, _pond.Width - 1), iy = Math.Clamp((int)s.Y, 0, _pond.Height - 1);
        return _pond.Land[iy * _pond.Width + ix];
    }

    private void Remember(PointF p)
    {
        _trailHead = (_trailHead + 1) % _trail.Length;
        _trail[_trailHead] = p;
        _trailCount = Math.Min(_trail.Length, _trailCount + 1);
    }

    public void Update(float dt)
    {
        _timer -= dt;
        switch (_doing)
        {
            case Doing.Hidden:
                if (_timer <= 0 && Appear()) { _doing = Doing.Freeze; _timer = 0.5f; }
                return;
            case Doing.Freeze:
            case Doing.Bask:
                if (_timer <= 0) NextDash();
                break;
            case Doing.Dash:
                if (_timer <= 0)
                {
                    // The dash is over: stand still, or lie down if this was the run up onto a rock.
                    bool onRock = _pond.Rocks.Any(r => !r.InWater && Dist(r.Centre, ToScreen(_x, _y)) < r.Radius * 0.6f);
                    if (onRock && _rng.NextDouble() < 0.7) { _doing = Doing.Bask; _timer = 4 + 8 * (float)_rng.NextDouble(); }
                    else { _doing = Doing.Freeze; _timer = 0.6f + 2.4f * (float)_rng.NextDouble(); }
                }
                break;
        }

        // ---- Steering toward the goal, turning on a circle no tighter than about half its body ----
        float gdx = _goalX - _x, gdy = _goalY - _y, gd = MathF.Sqrt(gdx * gdx + gdy * gdy);
        bool moving = _doing == Doing.Dash;
        if (moving)
        {
            float want = gd > 1 ? MathF.Atan2(gdy, gdx) : _heading;
            float most = MathF.Min(3.5f, MathF.Max(1.5f, _speed / (_len * 0.4f)));   // a pivot faster than this folded the tail into a hairpin
            _heading = Wrap(_heading + Math.Clamp(Wrap(want - _heading), -most * dt, most * dt));
            if (gd < _len * 0.15f) _timer = 0;                             // arrived: the dash ends here
        }
        float targetSpeed = moving ? _len * 2.6f : 0;
        _speed += (targetSpeed - _speed) * (1 - MathF.Exp(-dt * (moving ? 12f : 10f)));   // off like a shot, stops dead
        float step = _speed * dt;
        if (step > 0.01f)
        {
            float nx = _x + MathF.Cos(_heading) * step, ny = _y + MathF.Sin(_heading) * step;
            if (OnLand(nx, ny)) { _x = nx; _y = ny; }
            else if (moving) _timer = 0;                                    // water ahead: the dash ends here (a still lizard just stays put)
            PointF last = _trail[_trailHead];
            if ((last.X - _x) * (last.X - _x) + (last.Y - _y) * (last.Y - _y) > (_len / 30) * (_len / 30)) Remember(new PointF(_x, _y));
        }
        // Off the screen while leaving: gone until next time.
        PointF here = ToScreen(_x, _y);
        if (_leaving && (here.X < -_len || here.Y > _pond.Height + _len || here.X > _pond.Width + _len))
        {
            _doing = Doing.Hidden;
            _timer = (60 + 120 * (float)_rng.NextDouble()) / MathF.Max(0.01f, _often);
            return;
        }

        // ---- The running wave and the legs' rhythm: fast when running, dying to nothing when still ----
        float run = MathF.Min(1, _speed / (_len * 2f));
        _gait = (_gait + MathF.Tau * 5.5f * run * dt) % MathF.Tau;        // about five and a half steps a second flat out
        _phase = _gait;
        _amp += (_wide * 1.3f * run - _amp) * (1 - MathF.Exp(-dt * 8));
        LaySpine();
    }

    /// <summary>Comes out at the screen's edge on the land (the bottom or the side), facing in. False if no spot was found.</summary>
    private bool Appear()
    {
        for (int tries = 0; tries < 80; tries++)
        {
            bool bottom = _rng.Next(2) == 0;
            float x = bottom ? (float)_rng.NextDouble() * _pond.Width : (_pond.Land[_pond.Height - 1] ? 0 : _pond.Width - 1);
            float y = bottom ? _pond.Height - 1 : _pond.Height * (0.6f + 0.38f * (float)_rng.NextDouble());
            int ix = Math.Clamp((int)x, 0, _pond.Width - 1), iy = Math.Clamp((int)y, 0, _pond.Height - 1);
            if (!_pond.Land[iy * _pond.Width + ix] || !_pond.Open[iy * _pond.Width + ix]) continue;
            // Start just off the edge, so it runs IN rather than popping up.
            PointF s = new(bottom ? x : (ix == 0 ? -_len * 0.8f : _pond.Width + _len * 0.8f), bottom ? _pond.Height + _len * 0.8f : y);
            (_x, _y) = ToGround(s.X, s.Y);
            _heading = bottom ? -MathF.PI / 2 : (ix == 0 ? 0 : MathF.PI);
            _speed = 0; _amp = 0; _trailCount = 0; _trailHead = 0;
            for (int i = 20; i >= 0; i--) Remember(new PointF(_x - MathF.Cos(_heading) * _len * i / 18f, _y - MathF.Sin(_heading) * _len * i / 18f));
            _leaving = false;
            _dashes = 5 + _rng.Next(8);
            LaySpine();
            return true;
        }
        return false;
    }

    /// <summary>Picks the next dash: a spot on the land nearby, a rock to climb, or (when its visit is over) the way out.</summary>
    private void NextDash()
    {
        _dashes--;
        PointF here = ToScreen(_x, _y);
        PointF? goal = null;
        if (_dashes <= 0)
        {
            // Time to go: toward the nearest screen edge that has land on it, off the screen.
            _leaving = true;
            bool bottomNear = _pond.Height - here.Y < MathF.Min(here.X, _pond.Width - here.X);
            goal = bottomNear ? new PointF(here.X + (float)(_rng.NextDouble() - 0.5) * _len * 2, _pond.Height + _len * 1.5f)
                              : new PointF(here.X < _pond.Width / 2 ? -_len * 1.5f : _pond.Width + _len * 1.5f, here.Y + (float)(_rng.NextDouble() - 0.5) * _len);
        }
        else if (_doing != Doing.Bask && _rng.NextDouble() < 0.35)
        {
            // A rock to climb: a dry one within reach, not the one it is on.
            var near = _pond.Rocks.Where(r => !r.InWater && r.Radius > _u * 0.02f && Dist(r.Centre, here) < _u * 0.35f && Dist(r.Centre, here) > r.Radius).ToList();
            if (near.Count > 0) { BankRock r = near[_rng.Next(near.Count)]; goal = r.Centre; }
        }
        if (goal == null)
        {
            // Somewhere on the land a little way off, in view, on a line that stays on land.
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)(_rng.NextDouble() * MathF.Tau), d = _len * (0.6f + 2f * (float)_rng.NextDouble());
                var s = new PointF(here.X + MathF.Cos(a) * d, here.Y + MathF.Sin(a) * d * Ripples.Squash);
                int ix = (int)s.X, iy = (int)s.Y;
                if (ix < 0 || iy < 0 || ix >= _pond.Width || iy >= _pond.Height) continue;
                int i = iy * _pond.Width + ix;
                if (!_pond.Land[i] || !_pond.Open[i]) continue;
                if (!LandAt(new PointF((here.X + s.X) / 2, (here.Y + s.Y) / 2))) continue;   // the way there stays on land (here may be off the screen's edge)
                goal = s;
                break;
            }
        }
        if (goal is not PointF gp) { _doing = Doing.Freeze; _timer = 1; return; }
        (_goalX, _goalY) = ToGround(gp.X, gp.Y);
        _doing = Doing.Dash;
        _timer = 0.3f + 0.7f * (float)_rng.NextDouble() + (_leaving ? 3 : 0);
    }

    /// <summary>Lays the spine along the trail with the running wave (the koi's method, with the wave strongest down the tail).</summary>
    private void LaySpine()
    {
        int slot = _trailHead, left = _trailCount - 1;
        PointF a = new(_x, _y), b = _trail[slot];
        float travelled = 0, segLen = Dist(a, b);
        Span<PointF> mid = stackalloc PointF[Points];
        Span<PointF> dir = stackalloc PointF[Points];
        for (int i = 0; i < Points; i++)
        {
            float want = Along[i] * _len;
            while (travelled + segLen < want && left > 0)
            {
                travelled += segLen;
                a = b;
                slot = (slot - 1 + _trail.Length) % _trail.Length;
                left--;
                b = _trail[slot];
                segLen = Dist(a, b);
            }
            if (left <= 0 && travelled + segLen < want)
            {
                float ex = segLen > 1e-4f ? (b.X - a.X) / segLen : -MathF.Cos(_heading), ey = segLen > 1e-4f ? (b.Y - a.Y) / segLen : -MathF.Sin(_heading);
                mid[i] = new PointF(b.X + ex * (want - travelled - segLen), b.Y + ey * (want - travelled - segLen));
            }
            else
            {
                float f = segLen > 1e-4f ? (want - travelled) / segLen : 0;
                mid[i] = new PointF(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
            }
        }
        for (int i = 0; i < Points; i++)
        {
            PointF p = mid[Math.Max(0, i - 1)], q = mid[Math.Min(Points - 1, i + 1)];
            float dx = q.X - p.X, dy = q.Y - p.Y, l = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
            dir[i] = new PointF(dx / l, dy / l);
        }
        for (int i = 0; i < Points; i++)
        {
            float s = Along[i];
            // The running S-wave: small at the head, growing down the body and tail, two waves along the whole length.
            float sway = _amp * (0.1f + 0.9f * s) * MathF.Sin(MathF.Tau * 1.6f * s - _phase);
            float nx = -dir[i].Y, ny = dir[i].X;
            float cx = mid[i].X + nx * sway, cy = mid[i].Y + ny * sway;
            float hw = _wide * Profile(s);
            PointF l = ToScreen(cx + nx * hw, cy + ny * hw), r = ToScreen(cx - nx * hw, cy - ny * hw);
            Spine[i] = new PointF((l.X + r.X) / 2, (l.Y + r.Y) / 2);
            HalfWidth[i] = Dist(l, r) / 2;
        }
    }

    /// <summary>A pointed head, a slim neck, the body widest behind the front legs, hips, then the long tail tapering to a hair.</summary>
    private static float Profile(float s)
    {
        if (s < 0.08f) return 0.35f + 0.5f * MathF.Sqrt(s / 0.08f);
        if (s < 0.13f) return 0.85f - 0.15f * (s - 0.08f) / 0.05f;
        if (s < 0.4f) { float k = (s - 0.13f) / 0.27f; return 0.7f + 0.3f * MathF.Sin(k * MathF.PI); }
        return MathF.Max(0.06f, 0.7f * (1 - (s - 0.4f) / 0.6f) + 0.06f);
    }

    protected override bool Colour(float s, float v, out int r, out int g, out int b, out float alpha)
    {
        if (_shadowPass) { r = 10; g = 16; b = 10; alpha = 0.3f; return true; }
        float av = MathF.Abs(v);
        // Olive-brown back with a darker line down the spine, a pale stripe along each side of the body
        // (the grass lizard's mark), the tail a touch greyer, and a fine speckle so it is not one flat colour.
        float speck = 0.92f + 0.16f * MathF.Abs(MathF.Sin(s * 173f + v * 41f) * MathF.Sin(s * 97f - v * 59f));
        float back = Math.Clamp((0.2f - av) * 8, 0, 1);
        float stripe = s < 0.45f ? Math.Clamp(1 - MathF.Abs(av - 0.62f) * 9, 0, 1) : 0;
        float tail = Math.Clamp((s - 0.4f) * 3, 0, 1);
        r = (int)((112 + (72 - 112) * back + (190 - 112) * stripe + (104 - 112) * tail) * speck);
        g = (int)((92 + (58 - 92) * back + (172 - 92) * stripe + (92 - 92) * tail) * speck);
        b = (int)((54 + (36 - 54) * back + (120 - 54) * stripe + (62 - 54) * tail) * speck);
        alpha = 1;
        return true;
    }

    public void Draw(FrameBuffer fb)
    {
        if (_doing == Doing.Hidden) return;
        float soft = MathF.Max(0.8f, _u * 0.0006f);
        // The shadow: the same body, dark and faint, a little down and to the right of where it lies.
        float sx = _u * 0.0025f, sy = _u * 0.003f;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X + sx, Spine[i].Y + sy);
        _shadowPass = true;
        Paint(fb, _pond.Open, null, 0, soft * 1.5f);
        _shadowPass = false;
        for (int i = 0; i < Points; i++) Spine[i] = new PointF(Spine[i].X - sx, Spine[i].Y - sy);
        // Legs first (the body covers their roots): front pair at the shoulders, hind pair at the hips, opposite corners in step.
        Legs(fb, 2, 0.17f, 0.06f, 0);
        Legs(fb, 6, 0.2f, 0.065f, MathF.PI);
        Paint(fb, _pond.Open, _pond.Sun, 0, soft);
        // Two tiny dark eyes.
        PointF e0 = Spine[0], e1 = Spine[1];
        float ex = e1.X - e0.X, ey = e1.Y - e0.Y, el = MathF.Max(1e-3f, MathF.Sqrt(ex * ex + ey * ey));
        float er = MathF.Max(0.6f, _len * 0.012f);
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            var c = new PointF(e0.X + ex / el * HalfWidth[1] * 0.3f - ey / el * HalfWidth[1] * 0.7f * side, e0.Y + ey / el * HalfWidth[1] * 0.3f + ex / el * HalfWidth[1] * 0.7f * side);
            Oval(fb, _pond.Open, c, 0, er, er, 16, 14, 12, 0.95f, 0, _pond.Sun, rays: false);
        }
    }

    /// <summary>A pair of legs at spine point i: each a thin oval out from the body, swinging fore and aft with the gait (the two sides half a step apart).</summary>
    private void Legs(FrameBuffer fb, int i, float len, float wid, float phaseOffset)
    {
        PointF p = Spine[i], q = Spine[Math.Max(0, i - 1)];
        float back = MathF.Atan2(p.Y - q.Y, p.X - q.X);                   // toward the tail
        float run = MathF.Min(1, _speed / (_len * 2f));
        foreach (int side in (ReadOnlySpan<int>)[1, -1])
        {
            // Standing: legs out sideways and a little back. Running: each swings forward and back about that.
            float swing = 0.7f * run * MathF.Sin(_gait + phaseOffset + (side > 0 ? 0 : MathF.PI));
            float a = back - side * (MathF.PI / 2 + 0.35f) + swing;
            float l = _len * len;
            var c = new PointF(p.X + MathF.Cos(a) * (l * 0.5f + HalfWidth[i] * 0.6f), p.Y + MathF.Sin(a) * (l * 0.5f + HalfWidth[i] * 0.6f) * Ripples.Squash);
            Oval(fb, _pond.Open, c, a, l * 0.5f, _len * wid * 0.5f, 98, 80, 48, 1f, 0, _pond.Sun, rays: false);
        }
    }
}
