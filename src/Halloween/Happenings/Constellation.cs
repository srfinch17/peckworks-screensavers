using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A constellation: a handful of stars brighten one after another in the dark
/// upper sky, then lines draw themselves from star to star until they make a
/// little Halloween picture (a bat, a jack-o'-lantern, a ghost, a witch's hat
/// or a cat's head). The finished picture holds for about three seconds with
/// its stars twinkling, then everything fades away.
///
/// This is how people have always made pictures from stars: join the dots.
/// Each picture here is two lists:
///   - STARS: points on a grid from -1 to 1 (x across, y down, 0,0 in the middle)
///   - JOINS: pairs of star numbers that get a line between them, in drawing order
/// A star with no join is a lone star (the eyes and nose of the ghost and cat).
///
/// The picture is scaled so it is about 0.20 of U across, and placed
/// in the dark upper sky left of the moon.
///
/// THE SCRIPT (seconds): stars pop on one every 0.22 s from 0.5; then the
/// lines draw one at a time, 0.22 s apart, each growing over 0.25 s; then the
/// picture holds and twinkles; the last 1.2 s fade everything out.
/// </summary>
internal sealed class Constellation : Happening
{
    private static readonly Color Line = Color.FromArgb(236, 226, 255);       // the same cool white as the painted stars
    private static readonly Color Star = Color.FromArgb(236, 226, 255);

    // Each picture: star points, then pairs of joined stars.
    private static readonly (PointF[] Stars, (int A, int B)[] Joins)[] Pictures =
    [
        // A bat with wings spread: two pointed ears with a dip between, a peak at each wrist, a pointed wing tip, and a scalloped lower edge.
        ([new(-0.14f,-1.15f), new(-0.78f,-0.85f), new(-1.3f,-0.35f), new(-0.95f,0.15f), new(-0.62f,-0.02f), new(-0.45f,0.4f), new(0f,0.75f),
          new(0.45f,0.4f), new(0.62f,-0.02f), new(0.95f,0.15f), new(1.3f,-0.35f), new(0.78f,-0.85f), new(0.14f,-1.15f), new(0f,-0.75f)],
         [(0,1),(1,2),(2,3),(3,4),(4,5),(5,6),(6,7),(7,8),(8,9),(9,10),(10,11),(11,12),(12,13),(13,0)]),

        // A jack-o'-lantern: a lobed body, a stem on top, and two eyes and a mouth as lone stars.
        ([new(-0.12f,-0.7f), new(-0.62f,-0.85f), new(-1.12f,-0.15f), new(-0.95f,0.5f), new(-0.45f,0.85f), new(0f,0.75f),
          new(0.45f,0.85f), new(0.95f,0.5f), new(1.1f,-0.2f), new(0.6f,-0.82f), new(0.12f,-0.7f), new(0.16f,-1.2f), new(-0.16f,-1.2f),
          new(-0.4f,-0.15f), new(0.4f,-0.15f), new(0f,0.35f)],
         [(0,1),(1,2),(2,3),(3,4),(4,5),(5,6),(6,7),(7,8),(8,9),(9,10),(10,11),(11,12),(12,0)]),

        // A ghost: domed head, wavy hem, and two eyes and a mouth that are just lone stars.
        ([new(0f,-1f), new(-0.7f,-0.55f), new(-0.8f,0.9f), new(-0.4f,0.6f), new(0f,0.95f), new(0.4f,0.6f),
          new(0.8f,0.9f), new(0.7f,-0.55f), new(-0.3f,-0.3f), new(0.3f,-0.3f), new(0f,0.15f)],
         [(0,1),(1,2),(2,3),(3,4),(4,5),(5,6),(6,7),(7,0)]),

        // A witch's hat: wide brim, a cone with a bent tip, a band across the base.
        ([new(-1f,0.75f), new(1f,0.75f), new(-0.5f,0.55f), new(0.5f,0.55f), new(-0.25f,-0.35f),
          new(-0.05f,-0.75f), new(0.6f,-1f), new(0.15f,-0.4f)],
         [(2,4),(4,5),(5,6),(6,7),(7,3),(3,2),(2,0),(0,1),(1,3)]),

        // A cat's head: two pointed ears, round cheeks, a chin. Eyes and nose are lone stars.
        ([new(-0.75f,-0.95f), new(-0.95f,0.1f), new(-0.5f,0.75f), new(0.5f,0.75f), new(0.95f,0.1f),
          new(0.75f,-0.95f), new(0.25f,-0.5f), new(-0.25f,-0.5f), new(-0.4f,-0.1f), new(0.4f,-0.1f), new(0f,0.3f)],
         [(0,1),(1,2),(2,3),(3,4),(4,5),(5,6),(6,7),(7,0)]),
    ];

    private const float StarGap = 0.22f, StarFirst = 0.5f, JoinGap = 0.22f, JoinGrow = 0.25f, FadeOut = 1.2f;

    private readonly HalloweenScenery _s;
    private readonly Sprite _star, _pop;
    private PointF[] _at = [];                       // the stars' real screen positions
    private (int A, int B)[] _joins = [];
    private float[] _phase = [];                     // each star's own twinkle offset
    private float _joinsStart, _done;                // when the lines begin, and when the last one is finished

    public override float Seconds => 11f;

    public Constellation(HalloweenScenery s)
    {
        _s = s;
        _star = Sprite.Glow(Math.Max(3, (int)(s.U * 0.015f)), Star);          // the settled star
        _pop = Sprite.Glow(Math.Max(5, (int)(s.U * 0.034f)), Star);           // the bigger flare it arrives with
    }

    public override void Begin(Random rng)
    {
        var (stars, joins) = Pictures[rng.Next(Pictures.Length)];
        float u = _s.U;
        float scale = u * 0.10f;                                              // half the picture's width: 0.20 U across

        // Pick the middle of the picture, then nudge it down if the top of the
        // picture would poke off the top of the screen (a small screen, or a tall one).
        float cx = _s.Width * (0.30f + 0.22f * (float)rng.NextDouble());
        float cy = _s.Height * (0.10f + 0.20f * (float)rng.NextDouble());
        float top = stars.Min(p => p.Y) * scale;
        cy = Math.Max(cy, _s.Height * 0.03f - top + u * 0.01f);

        _at = stars.Select(p => new PointF(cx + p.X * scale, cy + p.Y * scale)).ToArray();
        _joins = joins;
        _phase = stars.Select(_ => (float)(rng.NextDouble() * Math.PI * 2)).ToArray();
        _joinsStart = StarFirst + stars.Length * StarGap + 0.2f;
        _done = _joinsStart + joins.Length * JoinGap + JoinGrow;
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        if (_at.Length == 0) return;
        float vis = Smooth((Seconds - t) / FadeOut);                          // the closing fade, applied to everything
        if (vis <= 0) return;
        float u = _s.U;

        // Lines first, so every star sits on top of the line ends.
        float thick = Math.Max(1f, u * 0.002f);
        for (int j = 0; j < _joins.Length; j++)
        {
            float grow = (t - (_joinsStart + j * JoinGap)) / JoinGrow;        // 0 to 1 while this line is being drawn
            if (grow <= 0) continue;
            grow = Smooth(grow);
            PointF a = _at[_joins[j].A], b = _at[_joins[j].B];
            PointF tip = new(a.X + (b.X - a.X) * grow, a.Y + (b.Y - a.Y) * grow);
            fb.Line(a, tip, Line, 0.6f * vis, thick, _s.OpenSky);
        }

        // Once the picture is finished the stars twinkle harder, to say "look, it is done".
        float swing = t >= _done ? 0.28f : 0.12f;
        for (int i = 0; i < _at.Length; i++)
        {
            float age = t - (StarFirst + i * StarGap);                        // seconds since this star lit
            if (age < 0) continue;
            float arrive = Smooth(age / 0.2f);
            float tw = 1 - swing + swing * MathF.Sin(t * (4f + _phase[i]) + _phase[i] * 3f);
            _star.DrawCentered(fb, _at[i].X, _at[i].Y, arrive * tw * vis, _s.OpenSky);
            // The pop: a bigger, fainter flare that dies away in half a second, so each star "lights" rather than appears.
            if (age < 0.6f)
                _pop.DrawCentered(fb, _at[i].X, _at[i].Y, 0.7f * (1 - Smooth(age / 0.6f)) * arrive * vis, _s.OpenSky);
        }
    }
}
