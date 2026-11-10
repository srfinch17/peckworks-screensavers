using System.Drawing.Drawing2D;
using Peckworks.Screensavers.Core;
using Peckworks.Screensavers.Core.Sakura;

namespace Christmas;

// ============================================================================
// THE TRIMMED TREE (LogMiceDecor.cs)
//
// The mice's story (Stories/MiceTrimTree.cs) hangs a red bauble, a spiral of fairy lights and a gold
// star on the tiny fir. The story draws them while it plays; when it ends the RESIDENT keeps drawing the
// same things for about four minutes (the tree stays lit and twinkling), then the lights go out one by
// one from the top (the quick way to "take it down": the mice do not come back for them), and last the
// bauble and star wink out. ONE class draws all of it from a few numbers, so the hand-over from the
// story to the resident is the same picture, frame for frame.
//
// THE PAYOFF is the lit tree, so it is built to glow from across the room:
//   - 34 bulbs wound about one and three quarter turns round the tree, in eight colours (the scene's six
//     pastels plus a warm red and a gold). Each is a soft glow of at least 0.0125 U radius with a bright dot
//     at its middle, twinkling on its OWN rhythm, and now and then it throws a tiny four-point glint.
//     The bulbs on the near side of the trunk are bright; the ones round the far side shine dimly
//     "through the boughs" (the same string, seen through the needles).
//   - A pool of coloured light lies on the snow at the tree's foot, slowly drifting pink to blue to gold.
//   - The star has a soft halo and a slow four-point sparkle that swells and fades every few seconds.
//   - The bauble is a red ball with a bright glint.
//
// What it is solid to: it sits ON the tree's boughs and is hidden by any prop NEARER than the tree's foot
// (the depth picture, one nearness step in front of the tree itself), and by nothing else. The pool is one
// step BEHIND the tree, so the tree's own foot covers it. Light is solid to nothing: the bulbs' glow is
// stamped over the snow beside them.
// ============================================================================

internal sealed class TreeDecor
{
    public const int Bulbs = 34;
    private static readonly Color[] Palette =
    [
        ChristmasPainter.Pastels[0], ChristmasPainter.Pastels[1], ChristmasPainter.Pastels[2], ChristmasPainter.Pastels[3],
        ChristmasPainter.Pastels[4], ChristmasPainter.Pastels[5], Color.FromArgb(255, 88, 80), Color.FromArgb(255, 204, 92),
    ];

    private readonly ChristmasScenery _s;
    private readonly float _top, _su, _yb, _yt;
    private readonly PointF _foot;
    private readonly PointF[] _at = new PointF[Bulbs];
    private readonly bool[] _front = new bool[Bulbs];
    private readonly int[] _color = new int[Bulbs];
    private readonly float[] _phase = new float[Bulbs], _rate = new float[Bulbs];
    private Sprite[]? _glow, _dot;
    private Sprite? _bauble, _star, _flare, _spark, _starSpark, _glint;
    private Sprite[]? _pool;
    private volatile bool _built;

    /// <summary>Where the bauble hangs and where the star sits (the tip).</summary>
    public PointF BaubleAt { get; }
    public PointF StarAt { get; }
    public float BaubleR { get; }
    public float Height { get; }

    // the resident's own timeline once the story hands over
    private float _lit;                 // seconds of "all lit" left
    private float _fade;                // seconds into the fade-out (negative: not fading)
    private bool _active;
    private double _clock;
    private float _out;                 // seconds into the final wink of the bauble and star
    public const float FadeSeconds = 14f;

    public bool Active => _active;
    /// <summary>Seconds of lit time left (0 when it is fading or off).</summary>
    public float LitLeft => _lit;

    public TreeDecor(ChristmasScenery s)
    {
        _s = s;
        PointF foot = s.SaplingFoot; float h = s.SaplingHeight;
        float top = foot.Y - h, su = h / 0.30f;
        _foot = foot; _top = top; _su = su; _yb = foot.Y - 0.14f * h; _yt = top + 0.17f * h;
        Height = h;
        for (int j = 0; j < Bulbs; j++)
        {
            (_at[j], float cs) = Spiral(j / (float)(Bulbs - 1));
            _front[j] = cs > -0.15f;
            _color[j] = j * 3 % Palette.Length;                         // neighbours differ: a 3-step walk through the eight colours
            _phase[j] = j * 2.399f;                                     // the golden angle: no two bulbs twinkle in step
            _rate[j] = 1.1f + 0.31f * (j % 7);
        }
        float yBau = foot.Y - 0.46f * h;
        BaubleR = Math.Max(3f, 0.066f * h);
        BaubleAt = new PointF(foot.X + Brushwork.PineHalfWidth(yBau, top, foot.Y, su) * 0.55f, yBau + BaubleR * 1.1f);
        StarAt = new PointF(foot.X, top + 0.02f * h);
    }

    /// <summary>
    /// The lights' spiral: where the string is at fraction f (0 the bottom, 1 the top), wound about one and three
    /// quarter turns round the tree, and the cosine of its angle (positive: on the near side of the trunk, in front
    /// of the tree; negative: round the back). The story's mouse runs along exactly this line.
    /// </summary>
    public (PointF At, float Cos) Spiral(float f)
    {
        float y = _yb + (_yt - _yb) * f;
        float phi = MathF.Tau * 1.75f * f + 0.6f;
        float hw = Brushwork.PineHalfWidth(y, _top, _foot.Y, _su) * 0.78f;
        return (new PointF(_foot.X + MathF.Sin(phi) * hw, y + MathF.Cos(phi) * (_foot.Y - _top) * 0.012f), MathF.Cos(phi));
    }

    public PointF Light(int j) => _at[j];
    public Sprite? BaubleSprite => _bauble;
    public Sprite? StarSprite => _star;
    /// <summary>A SMALL bright glow of one of the eight bulb colours (the coil the mouse carries is a ring of these).</summary>
    public Sprite? SmallGlow(int color) => _dot?[((color % Palette.Length) + Palette.Length) % Palette.Length];
    public bool LightVisible(int j) => _front[j];
    public int LightColor(int j) => _color[j];

    public void BuildInBackground(ChristmasScenery s, float unit)
    {
        float u = s.U;
        int gr = Math.Max(5, (int)MathF.Ceiling(u * 0.0125f));          // at least 0.0125 U radius
        _glow = new Sprite[Palette.Length]; _dot = new Sprite[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            _glow[i] = Sprite.Glow(gr, Palette[i]);
            _dot[i] = PaintDot(Math.Max(3f, u * 0.0036f), Saturated[i]);
        }
        int d = Math.Max(6, (int)MathF.Ceiling(BaubleR * 2));
        _bauble = Sprite.Paint(d + 4, d + 6, g =>
        {
            float cx = (d + 4) / 2f, cy = d / 2f + 4f;
            using var gold = new SolidBrush(Color.FromArgb(235, 196, 96));
            g.FillRectangle(gold, cx - d * 0.12f, 0.5f, d * 0.24f, d * 0.2f + 1.5f);
            using var ball = new LinearGradientBrush(new PointF(cx - d / 2f, cy - d / 2f), new PointF(cx + d / 2f, cy + d / 2f), Color.FromArgb(255, 110, 104), Color.FromArgb(150, 18, 38));
            g.FillEllipse(ball, cx - d / 2f, cy - d / 2f, d, d);
            using var hi = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
            g.FillEllipse(hi, cx - d * 0.30f, cy - d * 0.32f, d * 0.26f, d * 0.20f);
            using var hi2 = new SolidBrush(Color.FromArgb(120, 255, 220, 220));
            g.FillEllipse(hi2, cx + d * 0.12f, cy + d * 0.14f, d * 0.12f, d * 0.12f);
        });
        int sd = Math.Max(8, (int)MathF.Ceiling(Height * 0.17f));
        _star = Sprite.Paint(sd + 2, sd + 2, g =>
        {
            var pts = new PointF[10];
            float c = (sd + 2) / 2f;
            for (int i = 0; i < 10; i++)
            {
                float a = -MathF.PI / 2 + i * MathF.PI / 5, r = (i % 2 == 0 ? 0.5f : 0.22f) * sd;
                pts[i] = new PointF(c + MathF.Cos(a) * r, c + 0.05f * sd + MathF.Sin(a) * r);
            }
            using var b = new SolidBrush(Color.FromArgb(255, 214, 90));
            g.FillPolygon(b, pts);
            using var core = new SolidBrush(Color.FromArgb(255, 250, 206));
            g.FillEllipse(core, c - sd * 0.14f, c - sd * 0.10f, sd * 0.28f, sd * 0.28f);
        });
        _flare = Sprite.Glow(Math.Max(8, (int)(Height * 0.30f)), Color.FromArgb(255, 222, 120));
        _spark = SantaRig.PaintSparkle(Math.Max(5, (int)(Height * 0.07f)));
        _starSpark = SantaRig.PaintSparkle(Math.Max(9, (int)(Height * 0.52f)));       // the star's slow four-point sparkle
        _glint = SantaRig.PaintSparkle(Math.Max(5, (int)(u * 0.016f)));                 // a bulb's quick glint
        // the pool of coloured light on the snow: a flat soft ellipse, in three tints
        int pw = Math.Max(16, (int)(Height * 1.5f)), ph = Math.Max(6, (int)(Height * 0.40f));
        Color[] tints = [Color.FromArgb(255, 150, 200), Color.FromArgb(140, 196, 255), Color.FromArgb(255, 214, 120)];
        _pool = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            Color tint = tints[i];
            _pool[i] = Sprite.Paint(pw, ph, g =>
            {
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, pw - 1, ph - 1);
                using var br = new PathGradientBrush(path) { CenterColor = Color.FromArgb(200, tint), SurroundColors = [Color.FromArgb(0, tint)], Blend = Sprite.SoftFalloff };
                g.FillPath(br, path);
            });
        }
        _built = true;
    }

    /// <summary>The bulbs' own colours, saturated: a pastel dot on the green tree washes out to a haze, so the bright little BULB at the middle of each glow is a deeper colour.</summary>
    private static readonly Color[] Saturated =
    [
        Color.FromArgb(255, 96, 170), Color.FromArgb(70, 235, 150), Color.FromArgb(70, 160, 255), Color.FromArgb(176, 110, 255),
        Color.FromArgb(255, 226, 60), Color.FromArgb(255, 146, 50), Color.FromArgb(255, 50, 50), Color.FromArgb(255, 196, 40),
    ];

    /// <summary>A small solid bulb: a bright coloured disc with a white hot spot, soft at the rim.</summary>
    private static Sprite PaintDot(float r, Color c)
    {
        int d = (int)MathF.Ceiling(r * 2f) + 4;
        return Sprite.Paint(d, d, g =>
        {
            float cx = d / 2f, cy = d / 2f;
            using var rim = new SolidBrush(Color.FromArgb(150, c));
            g.FillEllipse(rim, cx - r * 1.2f, cy - r * 1.2f, r * 2.4f, r * 2.4f);
            using var core = new SolidBrush(c);
            g.FillEllipse(core, cx - r * 0.9f, cy - r * 0.9f, r * 1.8f, r * 1.8f);
            using var hot = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
            g.FillEllipse(hot, cx - r * 0.38f - r * 0.12f, cy - r * 0.38f - r * 0.12f, r * 0.76f, r * 0.76f);
        });
    }

    public Sprite? Spark => _spark;
    public bool Built => _built;

    // ---------------- the resident's timeline ----------------

    /// <summary>The story is over: the tree stays lit for "seconds", then the lights fade one by one.</summary>
    public void Keep(float seconds) { _active = true; _lit = seconds; _fade = -1f; _out = -1f; }

    public void Update(float dt)
    {
        _clock += dt;
        if (!_active) return;
        if (_lit > 0f) { _lit -= dt; if (_lit <= 0f) { _lit = 0f; _fade = 0f; } return; }
        if (_fade >= 0f && _out < 0f) { _fade += dt; if (_fade >= FadeSeconds) _out = 0f; return; }
        if (_out >= 0f) { _out += dt; if (_out > 1.2f) _active = false; }
    }

    public void Collect(DepthQueue q, IDepthDrawn owner)
    {
        if (_active && _built) q.Add(_s.SaplingFoot.Y + 1.5f, owner, 2000);
    }

    public void DrawIndex(FrameBuffer fb, int _)
    {
        float lit = _fade < 0f ? Bulbs : Math.Max(0f, Bulbs * (1f - _fade / FadeSeconds));
        bool ornaments = _out < 0f;
        Draw(fb, lit, ornaments, ornaments ? 0f : -1f, _clock, _out < 0f ? 1f : Math.Max(0f, 1f - _out / 1.2f));
    }

    // ---------------- the drawing, shared with the story ----------------

    /// <summary>
    /// Draws the decorations. "lit" is how many bulbs are on (a fraction ramps the next one up; as it falls the TOP
    /// ones go out first). "ornaments" draws the bauble and the star; "starFlare" (0 to 1, or negative for none) is
    /// an extra flare of the star. "wound" is how much of the cord has been wound round (0 to 1). "clock" drives
    /// every twinkle.
    /// </summary>
    public void Draw(FrameBuffer fb, float lit, bool ornaments, float starFlare, double clock, float ornamentAlpha = 1f, bool bauble = true, bool star = true, float wound = 1f)
    {
        if (!_built || _glow == null || _dot == null || _pool == null) return;
        byte[] depth = _s.NearDepth;
        int tree = _s.Nearness(_s.SaplingFoot.Y);
        int near = Math.Min(254, tree + 1);
        float litFrac = Math.Clamp(lit / Bulbs, 0f, 1f) * ornamentAlpha;

        // the pool of coloured light on the snow: drifting pink, blue, gold; one step behind the tree so its foot covers it
        if (litFrac > 0.02f)
        {
            for (int i = 0; i < 3; i++)
            {
                float w = 0.5f + 0.5f * MathF.Sin((float)clock * 0.45f + i * 2.094f);
                _pool[i].DrawCentered(fb, _foot.X, _foot.Y + 0.004f * _s.U, litFrac * (0.22f + 0.55f * w * w), null, false, depth, Math.Max(1, tree - 1));
            }
        }

        // the cord: a thin pale green line along the spiral, wherever it is on the near side, as far as it has been wound
        if (wound > 0f)
        {
            const int steps = 56;
            PointF prev = default; bool havePrev = false;
            for (int i = 0; i <= steps; i++)
            {
                float f = i / (float)steps;
                if (f > wound) break;
                var (p, cs) = Spiral(f);
                if (cs > -0.15f && havePrev) fb.Line(prev, p, Color.FromArgb(120, 190, 130), 0.85f * ornamentAlpha, 1f, null, depth, near);
                prev = p; havePrev = cs > -0.15f;
            }
        }
        for (int j = 0; j < Bulbs; j++)
        {
            if (j / (float)(Bulbs - 1) > wound) continue;
            float on = Math.Max(Math.Clamp(lit - j, 0f, 1f), 0.30f * ornamentAlpha * (wound < 1f ? 1f : 0f));
            if (on <= 0f) continue;
            float tw = 0.78f + 0.22f * MathF.Sin((float)(clock * _rate[j]) + _phase[j]);
            if (_front[j])
            {
                _glow[_color[j]].DrawCentered(fb, _at[j].X, _at[j].Y, on * tw, null, false, depth, near);
                _glow[_color[j]].DrawCentered(fb, _at[j].X, _at[j].Y, on * tw * 0.55f, null, false, depth, near);     // twice: a pastel haze alone washes out on the green
                _dot[_color[j]].DrawCentered(fb, _at[j].X, _at[j].Y, on, null, false, depth, near);
                // now and then a bulb throws a tiny four-point glint (the sine raised to a high power: rarely high)
                float fl = MathF.Pow(MathF.Max(0f, MathF.Sin((float)(clock * (0.5 + 0.13 * (j % 5))) + _phase[j] * 1.7f)), 40);
                if (fl > 0.05f && lit >= Bulbs * 0.5f && _glint != null) _glint.DrawCentered(fb, _at[j].X, _at[j].Y, fl * on, null, false, depth, near);
            }
            else
            {
                // round the far side: the same string shining dimly through the boughs
                _glow[_color[j]].DrawCentered(fb, _at[j].X, _at[j].Y, on * tw * 0.30f, null, false, depth, near);
            }
        }
        if (!ornaments) return;
        if (bauble && _bauble != null)
        {
            float sway = MathF.Sin((float)clock * 1.3f) * 0.6f;
            _bauble.DrawCentered(fb, BaubleAt.X + sway, BaubleAt.Y, ornamentAlpha, null, false, depth, near);
        }
        if (star && _star != null)
        {
            float halo = (0.62f + 0.12f * MathF.Sin((float)clock * 1.7f)) * ornamentAlpha;
            float fl = starFlare >= 0f ? Math.Max(halo, starFlare * ornamentAlpha) : 0f;
            _flare?.DrawCentered(fb, StarAt.X, StarAt.Y, fl, null, false, depth, near);
            _star.DrawCentered(fb, StarAt.X, StarAt.Y, ornamentAlpha, null, false, depth, near);
            // the slow four-point sparkle: swells and fades about every four seconds
            float sw = MathF.Pow(MathF.Max(0f, MathF.Sin((float)clock * 1.55f)), 3);
            if (sw > 0.02f) _starSpark?.DrawCentered(fb, StarAt.X, StarAt.Y, sw * 0.95f * ornamentAlpha, null, false, depth, near);
        }
    }
}
