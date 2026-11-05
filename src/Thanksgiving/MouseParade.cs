using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// The mouse parade. Four field mice, each with an acorn in its mouth, file
/// across the patch in a line from the nut pile under the left maple toward
/// their home in the corn shock. A sparrow drops out of the maple and swoops
/// low over the line; the mice scatter, each to the nearest pumpkin, and
/// crouch behind it (the pumpkin's depth hides them where they overlap, the
/// nose and an ear showing past its edge), noses twitching. The sparrow lands
/// on a pumpkin's shoulder, looks about, and flies off to the other maple.
/// One by one the mice come out, fall back into line, and finish the walk.
///
/// What it is solid to: the ground; every mouse passes behind any pumpkin
/// whose foot is lower on the screen than its own feet. The bird in the air
/// is in front of everything (nearness 254); on the pumpkin it has that
/// pumpkin's nearness plus one, as the patch's own birds do.
/// Claims: ground and cornshock.
/// </summary>
internal sealed class MouseParade : Story
{
    private const int Mice = 4;
    public override string Name => "MouseParade";
    public override string Claims => "ground,cornshock";

    private readonly Cast _c;
    private readonly ThanksgivingScenery _s;
    private Script _script = null!;
    private float _end;

    public MouseParade(Cast cast) { _c = cast; _s = cast.S; }

    public override float Begin(Random rng)
    {
        _script = new Script(_c, Mice + 1);
        float k = _c.K, m = _c.M, u = _s.U;
        List<Seg> bird = _script[Mice];
        PointF OnGround(float x) => _script.OnGround(x);
        float pileX = _s.Hollow.At.X - 1.7f * k;
        float homeX = _s.CornShockFoot.X;
        int dir = homeX >= pileX ? 1 : -1;
        float gap = 1.4f * m;
        PointF pile = OnGround(pileX), home = OnGround(homeX);
        float tripFor = Cast.Dist(pile, home) / (Cast.MouseSpeed * u);

        // The swoop comes when the line is a third of the way across.
        float swoopAt = 0.33f * tripFor + 0.4f * Mice * gap / (Cast.MouseSpeed * u);
        float leadX = pileX + dir * Cast.MouseSpeed * u * swoopAt;

        // Each mouse's hiding pumpkin: the nearest front pumpkin to where it will be when the bird comes, no two the same.
        var taken = new HashSet<int>();
        var hide = new PointF[Mice];
        var hideDir = new int[Mice];
        for (int i = 0; i < Mice; i++)
        {
            float mx = leadX - dir * i * gap;
            int best = -1; float bestD = float.MaxValue;
            for (int j = 0; j < _s.Pumpkins.Count; j++)
            {
                if (taken.Contains(j)) continue;
                var (pat, pr) = _s.Pumpkins[j];
                float foot = pat.Y + 0.8f * pr;
                if (pr < 0.6f * m) continue;                                                      // big enough to hide behind (either band: a mouse behind a back pumpkin is hidden by it too)
                if (foot - 0.30f * pr > _s.Height - 0.6f * m) continue;                           // a pumpkin whose foot is off the bottom edge would hide the mouse off the screen too
                float d = MathF.Abs(pat.X - mx) + MathF.Abs(foot - _s.WalkY(mx)) * 0.5f;
                if (d > 0.4f * u) continue;                                                       // within a bolt: no mouse crosses the whole patch to hide
                if (d < bestD) { bestD = d; best = j; }
            }
            if (best < 0) { hide[i] = OnGround(mx); hideDir[i] = dir; continue; }           // nothing near (a narrow screen): it freezes where it is, which is what a mouse does
            taken.Add(best);
            var (at, r) = _s.Pumpkins[best];
            int side = mx < at.X ? -1 : 1;                                                   // it hides on the side it came from, nose showing past the edge
            hide[i] = new PointF(at.X + side * (r * 0.85f + 0.15f * m), at.Y + 0.8f * r - 0.30f * r);
            hideDir[i] = -side;
        }

        // ---- the mice ----
        float latest = 0f;
        for (int i = 0; i < Mice; i++)
        {
            List<Seg> M = _script[i];
            float start = i * gap / (Cast.MouseSpeed * u);
            PointF atSwoop = OnGround(leadX - dir * i * gap);
            float t = _script.Mouse(M, start, pile, atSwoop, true);
            t = Script.MouseStill(M, t, Math.Max(0f, swoopAt + 0.15f + 0.05f * i - t), atSwoop, dir, true);   // a frozen instant, then
            t = _script.Mouse(M, t, atSwoop, hide[i], true, speed: 1.6f);                                      // bolts behind a pumpkin
            t = Script.MouseStill(M, t, 3.2f + 0.45f * i, hide[i], hideDir[i], true);                          // crouches, nose twitching
            PointF back = OnGround(leadX - dir * i * gap + dir * 0.4f * m);
            t = _script.Mouse(M, t, hide[i], back, true);
            t = Script.MouseStill(M, t, 0.2f, back, dir, true);
            t = _script.Mouse(M, t, back, home, true);
            latest = Math.Max(latest, t);
        }

        // ---- the sparrow: out of the left maple, low over the line, onto a pumpkin, off to the right maple ----
        var lm = _s.LeftMaple; var rm = _s.RightMaple;
        PointF from = new(lm.At.X + 0.2f * lm.R, lm.At.Y + 0.3f * lm.R);
        PointF low = new(leadX - dir * 1.5f * gap, _s.WalkY(leadX) - 0.9f * _c.B);
        int perch = -1; float pd = float.MaxValue;
        for (int j = 0; j < _s.Pumpkins.Count; j++)
        {
            if (taken.Contains(j)) continue;
            var (pat, pr) = _s.Pumpkins[j];
            if (pat.Y + 0.8f * pr < _s.WalkY(pat.X) + 0.03f * u) continue;
            float d = MathF.Abs(pat.X - (leadX + dir * 3f * gap));
            if (d < pd) { pd = d; perch = j; }
        }
        float tb = swoopAt - 0.9f;
        tb = _script.BirdFly(bird, tb, from, low, 1, -0.2f * u);                       // a dive: the arc bows down
        if (perch >= 0)
        {
            var (pat, pr) = _s.Pumpkins[perch];
            PointF shoulder = new(pat.X - 0.25f * pr, pat.Y - 0.72f * pr);
            int near = Math.Min(254, _s.Nearness(pat.Y + 0.8f * pr) + 1);
            tb = _script.BirdFly(bird, tb, low, shoulder, 1, 0.25f * u);
            Script.Add(bird, Kind.Bird, tb, 2.4f, shoulder, shoulder, -dir, p0: 1f, p1: 1f).Near = near; tb += 2.4f;
            Script.Add(bird, Kind.Bird, tb, 0.6f, shoulder, shoulder, dir, p0: 0f, p1: 1f).Near = near; tb += 0.6f;
            tb = _script.BirdFly(bird, tb, shoulder, new PointF(rm.At.X - 0.2f * rm.R, rm.At.Y + 0.3f * rm.R), 1, 0.3f * u);
        }
        else tb = _script.BirdFly(bird, tb, low, new PointF(rm.At.X - 0.2f * rm.R, rm.At.Y + 0.3f * rm.R), 1, 0.3f * u);
        _script.FixHops();
        _end = Math.Max(latest, tb) + 0.4f;

        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"parade: pile {pileX:F0} home {homeX:F0} swoop {swoopAt:F2} perch {perch} end {_end:F2}");
            _script.Log(sw, ["m1", "m2", "m3", "m4", "bird"]);
        }
        return _end;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // The bird flies behind the maples' leaves on its way out and in: it is drawn first, through the props stencil
        // only while it is in the canopies' bounds (the leaves hide it), then the mice far to near.
        foreach (Seg s in _script[Mice])
            if (t >= s.T0 && t < s.T1) { _c.DrawSeg(fb, s, t); break; }
        for (int i = Mice - 1; i >= 0; i--)
            foreach (Seg s in _script[i])
                if (t >= s.T0 && t < s.T1) { _c.DrawSeg(fb, s, t); break; }
    }
}
