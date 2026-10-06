using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// A wrapped present tumbles off Santa's sleigh, falls to the snow, lands in
/// a little puff of snow, hops once, and sits there for a while before it
/// fades away (a present for whoever lives in the cabin).
///
/// It only happens while Santa is on screen. If he is not, Begin makes the
/// showing last a fraction of a second, so the director deals another card.
///
/// How it moves, all worked out from t:
///  - FALLING: gravity. Distance fallen = half of g times time squared, so it
///    starts slow and speeds up. g is in screen-heights-ish per second squared
///    (U per s^2), so the fall looks the same on any screen. The fall time T is
///    worked out in Begin from how far it has to drop to reach the snow.
///  - DRIFTING: it keeps Santa's forward speed, so it arcs forward like
///    something thrown from a moving vehicle.
///  - SPINNING: a flip-book of 12 poses of the box turned in 30 degree steps.
///    The number of whole turns is chosen so it ends exactly upright.
///  - LANDING: where the snow actually is, asked of Ground.YAt, so it never
///    sinks into or hovers above the hill.
///  - THE PUFF: 8 snow dots thrown up and out from the landing spot, each on a
///    small arc (up, then gravity pulls it back), fading as it falls.
/// No stencil: it falls in front of the trees.
/// </summary>
internal sealed class PresentDrop : Happening
{
    private const int Poses = 12;
    private const int Flakes = 8;
    private const float G = 0.9f;               // gravity, in U per second squared
    private const float Hop = 0.45f;            // how long the single bounce takes, in seconds

    private readonly ChristmasScenery _s;
    private readonly Sprite[][] _boxes = new Sprite[2][];   // [0] red with a gold ribbon, [1] green with a red ribbon
    private readonly Sprite _flake, _shadow;
    private readonly float[] _flakeAngle = new float[Flakes];
    private readonly float[] _flakeSpeed = new float[Flakes];
    private float _seconds = 0.2f;
    private bool _show;
    private int _style;
    private float _x0, _y0, _vx, _landX, _landY, _fall, _turns;

    public override float Seconds => _seconds;

    public PresentDrop(ChristmasScenery s)
    {
        _s = s;
        int side = Math.Max(6, (int)(s.U * 0.034f));
        _boxes[0] = MakeBoxes(side, Color.FromArgb(205, 40, 52), Color.FromArgb(255, 215, 100));
        _boxes[1] = MakeBoxes(side, Color.FromArgb(40, 140, 80), Color.FromArgb(215, 50, 60));
        // A snow dot: white with a soft blue-grey edge, so it still shows on the near-white snow.
        int fr = Math.Max(2, (int)(s.U * 0.006f));
        _flake = Sprite.Paint(fr * 2 + 2, fr * 2 + 2, g =>
        {
            using var edge = new SolidBrush(Color.FromArgb(200, 140, 165, 205));
            using var core = new SolidBrush(Color.White);
            g.FillEllipse(edge, 0, 0, fr * 2 + 1.5f, fr * 2 + 1.5f);
            g.FillEllipse(core, 1, 1, fr * 2 - 0.5f, fr * 2 - 0.5f);
        });
        // The dent a present makes in the snow: a soft blue-grey oval.
        int sw = side * 2;
        _shadow = Sprite.Paint(sw, Math.Max(3, sw / 3), g =>
        {
            using var b = new SolidBrush(Color.FromArgb(120, 120, 150, 195));
            g.FillEllipse(b, 0, 0, sw - 1, Math.Max(3, sw / 3) - 1);
        });
    }

    /// <summary>12 pictures of one box, each turned a further 30 degrees.</summary>
    private static Sprite[] MakeBoxes(int side, Color wrap, Color ribbon)
    {
        int size = (int)MathF.Ceiling(side * 1.5f) + 4;      // big enough that a turned corner is not cut off
        var poses = new Sprite[Poses];
        for (int i = 0; i < Poses; i++)
        {
            float angle = i * 360f / Poses;
            poses[i] = Sprite.Paint(size, size, g =>
            {
                g.TranslateTransform(size / 2f, size / 2f);
                g.RotateTransform(angle);
                float h = side / 2f;
                using var body = new SolidBrush(wrap);
                using var rib = new SolidBrush(ribbon);
                using var edge = new Pen(Color.FromArgb(200, 40, 40, 70), 1f);
                g.FillRectangle(body, -h, -h, side, side);
                g.FillRectangle(rib, -h, -side * 0.09f, side, side * 0.18f);     // ribbon across
                g.FillRectangle(rib, -side * 0.09f, -h, side * 0.18f, side);     // ribbon down
                g.DrawRectangle(edge, -h, -h, side, side);                      // a dark outline, so it reads on white snow
            });
        }
        return poses;
    }

    public override void Begin(Random rng)
    {
        // Is Santa actually over the screen? If not, there is nothing to drop it from.
        _show = _s.SantaOnScreen && _s.SantaSleigh.X > _s.Width * 0.05f && _s.SantaSleigh.X < _s.Width * 0.95f;
        if (!_show) { _seconds = 0.2f; return; }
        _seconds = 6f;

        float u = _s.U;
        _style = rng.Next(2);
        _x0 = _s.SantaSleigh.X;
        _y0 = _s.SantaSleigh.Y + 0.02f * u;                  // it slips out just under the sleigh
        _vx = 0.10f * u;                                     // Santa's forward speed (pixels per second)

        // Work out where and when it lands. The landing spot depends on the fall
        // time and the fall time on the height of the snow at that spot, so
        // guess, check, and guess again (three rounds settle it).
        // The snow field runs from the crest (Ground.YAt) down to the bottom of the screen, and lower
        // is nearer. Landing right on the crest line would look like it stopped in mid-air in the
        // distance, so it lands a chosen depth in front of it.
        float depth = (0.04f + 0.10f * (float)rng.NextDouble()) * u;
        float fall = 1.2f;
        for (int i = 0; i < 3; i++)
        {
            _landX = Math.Clamp(_x0 + _vx * fall, 0.03f * _s.Width, 0.97f * _s.Width);
            _landY = Math.Min(_s.Height - 0.02f * u, _s.Ground.YAt(_landX) + depth);
            fall = MathF.Sqrt(2 * Math.Max(0.02f * u, _landY - _y0) / (G * u));
        }
        _fall = fall;
        _turns = (1 + rng.Next(2)) * (rng.Next(2) == 0 ? 1 : -1);   // 1 or 2 whole turns, either way
        for (int i = 0; i < Flakes; i++)
        {
            _flakeAngle[i] = -MathF.PI * (0.08f + 0.84f * (i + (float)rng.NextDouble() * 0.6f) / Flakes);   // fanned across the upper half
            _flakeSpeed[i] = u * (0.20f + 0.14f * (float)rng.NextDouble());
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (!_show) return;
        float u = _s.U;
        float fade = Fade(t, _seconds, 0.1f, 1.2f);

        float x, y, turn;
        if (t < _fall)
        {
            float k = t / _fall;
            x = _x0 + _vx * t;
            y = _y0 + 0.5f * G * u * t * t;
            turn = _turns * k;                               // spin through the fall, in whole turns
        }
        else
        {
            float h = (t - _fall) / Hop;                     // 0 to 1 over the bounce, then it stays at 1
            x = _x0 + _vx * _fall + _vx * 0.15f * Math.Min(1, h);   // a little slide on landing
            y = _landY;
            if (h < 1) y -= 4 * h * (1 - h) * 0.018f * u;    // one small hop
            turn = 0;
        }
        x = Math.Clamp(x, 0, _s.Width);

        float baseY = _landY - 0.006f * u;                   // box centre when resting
        if (t >= _fall) y = y - 0.006f * u;
        else y = Math.Min(y, baseY);

        if (t >= _fall)
        {
            // The dent in the snow under it, then a puff of snow thrown up.
            _shadow.DrawCentered(fb, x, _landY + 0.002f * u, 0.8f * fade);
            float age = t - _fall;
            for (int i = 0; i < Flakes && age < 1.0f; i++)
            {
                float fx = x + MathF.Cos(_flakeAngle[i]) * _flakeSpeed[i] * age;
                float fy = _landY + MathF.Sin(_flakeAngle[i]) * _flakeSpeed[i] * age + 0.35f * u * age * age;
                _flake.DrawCentered(fb, fx, fy, (1 - age) * fade);
            }
        }

        // Pick the pose nearest the angle. Whole turns end back at pose 0, so it lands upright.
        int pose = ((int)MathF.Round(turn * Poses) % Poses + Poses) % Poses;
        _boxes[_style][pose].DrawCentered(fb, x, y, fade);
    }
}
