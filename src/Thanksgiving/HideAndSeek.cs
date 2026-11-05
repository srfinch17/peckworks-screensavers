using Peckworks.Screensavers.Core;

namespace Thanksgiving;

/// <summary>
/// Hide and seek at the jack-o'-lantern (his line: "i love the little scenes
/// that play out like the squirrels and the nuts. give me a few of those").
///
/// THE STORY: a mouse is nosing about the acorns under the left maple. The
/// squirrel's head rises out of its hollow, sees it, and hops out. The mouse
/// bolts down through the patch, dodging between the pumpkins (the nearer
/// ones hide it as it passes behind them), with the squirrel bounding after
/// it, and dives into the jack-o'-lantern's grinning mouth. The squirrel
/// pulls up at the pumpkin, sits up and looks about, turns, looks the other
/// way, sniffs along the pumpkin's foot, and gives up: it bounds back and
/// climbs its tree. While it sniffs, the mouse's head rises in one of the
/// carved eyes, watching, and ducks. Once the squirrel has gone the mouse
/// looks out of the other eye, then creeps out of the mouth and scurries home
/// to the corn shock.
///
/// HOW IT HIDES: the painter made a stencil of the jack-o'-lantern's carved
/// holes (the candle-yellow pixels inside the face). The mouse going in and
/// coming out, and its head looking out of an eye, are stamped through that
/// stencil: they are seen only where the holes are, so the pumpkin's shell
/// hides the rest of the body and the mouse reads as being INSIDE it.
///
/// What it is solid to: the ground; the mouse and the squirrel pass behind
/// any pumpkin whose foot is lower on the screen than their own feet.
/// Claims: ground and cornshock (the mouse's home).
/// </summary>
internal sealed class HideAndSeek : Story
{
    public override string Name => "HideAndSeek";
    public override string Claims => "ground,cornshock";
    public override bool CanBegin => _s.Jack.R > 0;

    private readonly Cast _c;
    private readonly ThanksgivingScenery _s;
    private Script _script = null!;
    private float _end;

    public HideAndSeek(Cast cast) { _c = cast; _s = cast.S; }

    public override float Begin(Random rng)
    {
        _script = new Script(_c, 2);
        float k = _c.K, m = _c.M, u = _s.U;
        List<Seg> M = _script[0], A = _script[1];
        PointF[] lt = _s.LeftTrunk;
        float hx = _s.Hollow.At.X;
        (PointF at, float r) = _s.Jack;
        float footY = at.Y + 0.8f * r;
        PointF mouth = new(at.X, at.Y + 0.36f * r);
        PointF leftEye = new(at.X - 0.32f * r, at.Y - 0.20f * r), rightEye = new(at.X + 0.32f * r, at.Y - 0.20f * r);
        bool[] holes = _s.JackHoles;
        PointF OnGround(float x) => _script.OnGround(x);

        // The mouse at the nut pile under the left maple (the caper's pile spot), the jack-o'-lantern down in the front of the patch.
        float pileX = hx - 1.7f * k;
        PointF pile = OnGround(pileX);
        bool jackRight = at.X > pileX;
        int toJack = jackRight ? 1 : -1;
        PointF jackFoot = new(at.X - toJack * 0.25f * r, footY);           // the mouse's last step on the ground, under the mouth
        PointF squirrelStop = new(at.X - toJack * (r + 1.1f * k), footY);  // the squirrel pulls up beside the pumpkin

        // ---- the squirrel comes out and the mouse bolts ----
        float t = 0f;
        Script.MouseStill(M, 0f, 0.8f, pile, -1, false);
        Script.Add(A, Kind.Peek, t, 0.6f, _s.Hollow.At, _s.Hollow.At, -1, p0: 0f, p1: 1f); t += 0.6f;
        Script.Add(A, Kind.Peek, t, 0.7f, _s.Hollow.At, _s.Hollow.At, -1, p0: 1f, p1: 1f); t += 0.7f;
        float bolt = t + 0.1f;                                               // the mouse sees it and runs
        t = Script.Hop(A, t, new PointF(hx - 0.15f * k, _s.Hollow.At.Y + 0.2f * k), OnGround(hx - 0.95f * k), -1, false, 0.5f);
        PointF out_ = OnGround(hx - 0.95f * k);
        float mIn = _script.Mouse(M, bolt, pile, jackFoot, false, speed: 1.5f);
        float mInside = _script.Mouse(M, mIn, jackFoot, mouth, false, holes, speed: 1.2f);   // up and into the grin: seen only through the hole
        // The squirrel sets off so that it arrives a breath after the mouse has gone in.
        float runFor = Cast.Dist(out_, squirrelStop) / (Cast.Speed * u);
        float setOff = Math.Max(t + Cast.TurnFor + 0.3f, mInside + 0.5f - runFor);
        if (toJack != -1) t = Script.Turn(A, t, out_, toJack, false);
        t = Script.Sit(A, t, Math.Max(0.1f, setOff - t), out_, toJack, false, look: true);
        t = _script.Run(A, t, out_, squirrelStop, false, false);
        t = Script.Rise(A, t, squirrelStop, toJack, false);
        t = Script.Sit(A, t, 1.0f, squirrelStop, toJack, false, look: true);         // where did it go?
        t = Script.Turn(A, t, squirrelStop, -toJack, false);
        t = Script.Sit(A, t, 0.8f, squirrelStop, -toJack, false, look: true);
        t = Script.Turn(A, t, squirrelStop, toJack, false);
        t = Script.Rise(A, t, squirrelStop, toJack, false, -1);
        float sniff = t;
        t = Script.Dig(A, t, 1.6f, squirrelStop, toJack, 0f, false);                   // nose along the pumpkin's foot
        t = Script.Rise(A, t, squirrelStop, toJack, false);
        t = Script.Sit(A, t, 0.6f, squirrelStop, toJack, false);
        t = Script.Turn(A, t, squirrelStop, -toJack, false);
        PointF trunkFoot = AcornCaper.OnTrunk(_s, lt, _s.WalkY(lt[0].X));
        PointF beforeTrunk = OnGround(lt[0].X - u * 0.0065f - 0.25f * k);
        t = _script.Run(A, t, squirrelStop, beforeTrunk, false, false);
        t = Script.Mount(A, t, trunkFoot, 1, false, true);
        float rail = _c.RailAt(lt[0].X) - 1.5f;
        t = _script.Climb(A, t, trunkFoot, AcornCaper.OnTrunk(_s, lt, rail), true, false);
        t = _script.Climb(A, t, AcornCaper.OnTrunk(_s, lt, rail), new PointF(lt[1].X - u * 0.0065f, lt[1].Y), true, false);
        t = _script.Climb(A, t, new PointF(lt[1].X - u * 0.0065f, lt[1].Y), new PointF(lt[2].X - u * 0.0065f, lt[2].Y), true, false);
        float gone = t;

        // ---- the mouse peeks: out of one eye while the squirrel sniffs, out of the other once it has gone ----
        PointF eyeNear = jackRight ? leftEye : rightEye, eyeFar = jackRight ? rightEye : leftEye;
        float p = sniff + 0.3f;
        Script.Add(M, Kind.MousePeek, p, 0.3f, eyeNear, eyeNear, toJack, p0: 0f, p1: 1f, stencil: holes); p += 0.3f;
        Script.Add(M, Kind.MousePeek, p, 0.7f, eyeNear, eyeNear, toJack, p0: 1f, p1: 1f, stencil: holes); p += 0.7f;
        Script.Add(M, Kind.MousePeek, p, 0.25f, eyeNear, eyeNear, toJack, p0: 1f, p1: 0f, stencil: holes); p += 0.25f;
        p = Math.Max(p, gone - 1.5f);
        Script.Add(M, Kind.MousePeek, p, 0.3f, eyeFar, eyeFar, -toJack, p0: 0f, p1: 1f, stencil: holes); p += 0.3f;
        Script.Add(M, Kind.MousePeek, p, 1.2f, eyeFar, eyeFar, -toJack, p0: 1f, p1: 1f, stencil: holes); p += 1.2f;
        Script.Add(M, Kind.MousePeek, p, 0.25f, eyeFar, eyeFar, -toJack, p0: 1f, p1: 0f, stencil: holes); p += 0.25f;
        p = Math.Max(p, gone + 0.3f);
        p = _script.Mouse(M, p, mouth, jackFoot, false, holes, speed: 0.8f);           // creeps out of the grin
        p = Script.MouseStill(M, p, 0.5f, jackFoot, -toJack, false);                   // all clear?
        PointF home = OnGround(_s.CornShockFoot.X);
        PointF half = new((jackFoot.X + home.X) / 2, (jackFoot.Y + home.Y) / 2);
        p = _script.Mouse(M, p, jackFoot, half, false);
        p = Script.MouseStill(M, p, 0.15f, half, home.X >= half.X ? 1 : -1, false);
        p = _script.Mouse(M, p, half, home, false);
        _script.FixHops();
        _end = Math.Max(gone, p) + 0.5f;

        string? log = Environment.GetEnvironmentVariable("PECKWORKS_CAPERLOG");
        if (!string.IsNullOrEmpty(log))
        {
            using var sw = new StreamWriter(log, append: true);
            sw.WriteLine($"hideandseek: jack ({at.X:F0},{at.Y:F0}) r {r:F0} in {mInside:F2} sniff {sniff:F2} gone {gone:F2} end {_end:F2}");
            _script.Log(sw, ["mouse", "A"]);
        }
        return _end;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        // Two nuts at the pile, so the mouse had a reason to be there; they fade with the story.
        float k = _c.K, over = Cast.Smooth((_end - t) / 0.6f), pileIn = Cast.Smooth(t / 0.5f);
        float pileX = _s.Hollow.At.X - 1.7f * k, pileY = _s.WalkY(pileX);
        byte near = _s.Nearness(pileY);
        _c.Acorn.DrawCentered(fb, pileX - 0.16f * k, pileY - 0.07f * k, pileIn * over, null, false, _s.NearDepth, near);
        _c.Acorn.DrawCentered(fb, pileX + 0.14f * k, pileY - 0.07f * k, pileIn * over, null, false, _s.NearDepth, near);
        _script.Draw(fb, t);
    }
}
