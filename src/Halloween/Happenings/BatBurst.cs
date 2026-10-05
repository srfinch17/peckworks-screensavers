using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// A swarm of bats pours out of the haunted house's tower roof, boils up into
/// the sky, fans out left, up and right, and melts away into the night.
///
/// Every bat has its own private numbers, rolled once in Begin: the moment it
/// leaves, the angle it flies at, how fast, how widely it weaves, how fast it
/// flaps, which size it is. Draw then works out where each bat is from its
/// own clock ("age" = seconds since this bat left) and nothing else. That is
/// how a swarm of 40 stays simple: one rule, 36 sets of numbers.
///
/// The bats are the scene's own bat (HalloweenScene.PaintBat) in three small
/// sizes, each painted in 10 wing poses ahead of time. Smaller bats count as
/// farther away: they fly a little slower, like the far-off ones in a
/// painting.
///
/// They are stamped with the OpenFromHouse stencil: in front of the house and
/// the sky, behind anything nearer (the hills, trees, branches).
/// </summary>
internal sealed class BatBurst : Happening
{
    private const int Bats = 40;
    private const int Poses = 10;                          // wing positions per size (the flip-book)
    private const float Burst = 1.5f;                      // every bat has left by this many seconds in

    private readonly HalloweenScenery _s;
    private readonly Sprite[,] _sprites;                   // [size, pose]
    private readonly PointF _mouth;                        // where they pour out: just under the tower's tip

    // One slot per bat.
    private readonly float[] _leaves = new float[Bats];    // when it leaves, in seconds into the showing
    private readonly float[] _life = new float[Bats];      // how long it flies before it has faded away
    private readonly float[] _dirX = new float[Bats], _dirY = new float[Bats];   // its heading (a unit arrow)
    private readonly float[] _speed = new float[Bats];     // pixels per second at the start
    private readonly float[] _weaveAmp = new float[Bats], _weaveRate = new float[Bats], _weavePhase = new float[Bats];
    private readonly float[] _flapRate = new float[Bats], _flapPhase = new float[Bats];
    private readonly int[] _size = new int[Bats];
    private readonly float[] _startSide = new float[Bats]; // a small sideways offset, so they do not all leave from one pixel

    public override float Seconds => 7f;

    public BatBurst(HalloweenScenery s)
    {
        _s = s;
        // Three half-wingspans (0.006, 0.010, 0.014 u), never under 3 pixels.
        float[] spans = [Math.Max(3f, s.U * 0.006f), Math.Max(3.5f, s.U * 0.010f), Math.Max(4f, s.U * 0.014f)];
        _sprites = new Sprite[spans.Length, Poses];
        for (int size = 0; size < spans.Length; size++)
            for (int pose = 0; pose < Poses; pose++)
                _sprites[size, pose] = HalloweenScene.PaintBat(spans[size], pose / (Poses - 1f) * 2 - 1);

        // The tower's pointed roof is thin at the top, so a bat just under the
        // tip is already mostly in open sky. (A bat leaving from the window
        // would be black on the black tower and invisible.)
        _mouth = new PointF(s.HouseTowerTip.X, s.HouseTowerTip.Y + s.U * 0.03f);
    }

    public override void Begin(Random rng)
    {
        float u = _s.U;
        for (int i = 0; i < Bats; i++)
        {
            // Leaving times bunch up near the start (cubing a 0-to-1 number
            // makes small values much more likely): a sudden gush, then a trickle.
            float r = (float)rng.NextDouble();
            _leaves[i] = Burst * r * r * r;
            _life[i] = Seconds - _leaves[i] - 0.2f * (float)rng.NextDouble();

            // The fan: 20 to 160 degrees, measured from "right" going
            // counter-clockwise, so 90 is straight up. Screen y grows downward, hence the minus.
            // Two dice averaged (a "triangle" spread) crowd the middle: most
            // bats go roughly upward, a few stray far to either side.
            float spread = ((float)rng.NextDouble() + (float)rng.NextDouble()) / 2;
            float angle = (20f + 140f * spread) * MathF.PI / 180f;
            _dirX[i] = MathF.Cos(angle);
            _dirY[i] = -MathF.Sin(angle);

            _size[i] = Math.Max(rng.Next(3), rng.Next(3));   // the bigger sizes come up more often (max of two dice)
            _speed[i] = u * (0.25f + 0.25f * (float)rng.NextDouble()) * (0.85f + 0.15f * _size[i]);   // near (big) bats a touch faster
            _weaveAmp[i] = u * (0.010f + 0.020f * (float)rng.NextDouble());
            _weaveRate[i] = 3f + 4f * (float)rng.NextDouble();
            _weavePhase[i] = (float)rng.NextDouble() * MathF.Tau;
            _flapRate[i] = 22f + 10f * (float)rng.NextDouble();      // radians per second: about 3.5 to 5 beats a second
            _flapPhase[i] = (float)rng.NextDouble() * MathF.Tau;
            _startSide[i] = u * 0.008f * ((float)rng.NextDouble() * 2 - 1);
        }
    }

    public override void Draw(FrameBuffer fb, float t)
    {
        for (int i = 0; i < Bats; i++)
        {
            float age = t - _leaves[i];
            if (age < 0 || age >= _life[i]) continue;

            // How far along its flight path: it starts at full speed and
            // slows by about a third (the 0.35 and the square).
            float along = _speed[i] * (age - 0.35f * age * age / (2 * _life[i]));

            // The weave: a sideways wobble (at right angles to its heading)
            // that grows from nothing, so it leaves in a straight rush and
            // then starts to flutter. A second slower wave on top keeps it
            // from looking like a tidy snake.
            float grow = Smooth(age / 0.8f);
            float sway = grow * _weaveAmp[i] * (MathF.Sin(age * _weaveRate[i] + _weavePhase[i]) + 0.4f * MathF.Sin(age * _weaveRate[i] * 0.37f + 2f * _weavePhase[i]));
            float x = _mouth.X + _startSide[i] + _dirX[i] * along - _dirY[i] * sway;     // (-dirY, dirX) is the heading turned 90 degrees
            float y = _mouth.Y + _dirY[i] * along + _dirX[i] * sway;

            // Fade in over the first 0.15 s (it emerges) and out over the last 1.5 s.
            float seen = Fade(age, _life[i], 0.15f, 1.5f);

            float lift = MathF.Sin(age * _flapRate[i] + _flapPhase[i]);                    // -1 = wings down ... +1 = wings up
            int pose = Math.Clamp((int)MathF.Round((lift + 1) / 2 * (Poses - 1)), 0, Poses - 1);
            _sprites[_size[i], pose].DrawCentered(fb, x, y, seen, _s.OpenFromHouse);
        }
    }
}
