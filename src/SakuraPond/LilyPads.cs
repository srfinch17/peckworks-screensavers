using Peckworks.Screensavers.Core;

namespace SakuraPond;

/// <summary>
/// The lily pads as a live layer (round forty): stamped on the water every frame, pushed a little by whatever
/// swims into them at the surface, and drawn back to their roots.
///
/// FEYNMAN VERSION: a lily pad is a leaf on the end of a long stalk that grows up from the bed. The stalk is
/// the string of a balloon: the leaf floats wherever the water puts it, but never further from the root than
/// the stalk reaches, and when nothing is pushing it, it settles back over the root. So each pad has two places:
/// its Anchor (the root, fixed when the pond was painted) and its Center (where the leaf is now). A koi, a
/// turtle, the snake or a duck that runs into the leaf shoves it aside by the amount of the overlap; the leaf
/// slides out of the way at the pace a floating leaf does, and when the fish has passed it eases home over a
/// second or two. A fish deeper than a hand under the surface passes beneath without touching it (the known
/// pass: a koi under a pad). The stalk's reach is half a pad across: past that the leaf stops and the fish
/// goes under. A pad with a frog or a dragonfly on it is held still (Hold): nothing shoves a pad with a sitter.
///
/// WHAT IS SOLID: a pad is solid to nothing (nothing is stopped by it); it is the light thing, moved by anything
/// at the surface. The pictures are stamped only where no still branch is (the scene's Open stencil); the
/// swaying boughs cover them on their own.
/// </summary>
internal sealed class Pads
{
    private readonly Pond _pond;
    private readonly float _u, _touchZ;

    // Each pad AT REST, laid over the backdrop once: the frame's pixel index and the finished colour of every pixel the
    // pad touches where no still branch covers it. A pad at rest (nearly all of them, nearly all the time) is then a
    // straight copy of these, not a blend of its sheet pixel by pixel through a stencil: a third of the stamping's cost,
    // and on a 4K screen the four tenths of a millisecond that stamping cost was the margin between one page and two.
    // (What is lost: a fish or a ring under the pad's thin shadow crescent shows the water there instead. A pad that has
    // been shoved is stamped from its sheet at its place, as before.)
    private readonly int[][] _restIndex;
    private readonly uint[][] _restPixel;

    public Pads(Pond pond, bool[] open)
    {
        _pond = pond; _u = pond.U;
        _touchZ = 0.12f * Pond.DepthU * _u;          // a body point deeper than this (pixels of depth) is under the pad, not against it
        int n = pond.Pads.Count;
        _restIndex = new int[n][]; _restPixel = new uint[n][];
        var idx = new List<int>(); var px = new List<uint>();
        for (int p = 0; p < n; p++)
        {
            LilyPad pad = pond.Pads[p];
            idx.Clear(); px.Clear();
            if (pad.Picture is Sprite s)
            {
                int left = (int)(pad.Anchor.X - s.Width / 2f), top = (int)(pad.Anchor.Y - s.Height / 2f);   // the same corner DrawCentered uses
                ReadOnlySpan<uint> argb = s.Argb;
                for (int sy = 0; sy < s.Height; sy++)
                {
                    int y = top + sy;
                    if (y < 0 || y >= pond.Height) continue;
                    for (int sx = 0; sx < s.Width; sx++)
                    {
                        int x = left + sx;
                        if (x < 0 || x >= pond.Width) continue;
                        uint c = argb[sy * s.Width + sx];
                        int a = (int)(c >> 24);
                        if (a == 0) continue;
                        int i = y * pond.Width + x;
                        if (!open[i]) continue;
                        uint bg = pond.Pixels[i];
                        int br = (int)((bg >> 16) & 0xFF), bgn = (int)((bg >> 8) & 0xFF), bb = (int)(bg & 0xFF);
                        int cr = (int)((c >> 16) & 0xFF), cg = (int)((c >> 8) & 0xFF), cb = (int)(c & 0xFF);
                        int rr = br + ((cr - br) * a >> 8), gg = bgn + ((cg - bgn) * a >> 8), b2 = bb + ((cb - bb) * a >> 8);   // the stamp's own sum (Sprite.DrawRows)
                        idx.Add(i); px.Add((uint)((rr << 16) | (gg << 8) | b2));
                    }
                }
            }
            _restIndex[p] = idx.ToArray(); _restPixel[p] = px.ToArray();
        }
    }

    /// <summary>Pushes every pad by the bodies at the surface this frame, then eases each toward its root.</summary>
    /// <param name="surface">The scene's surface list (the snake, the frogs in the water, the ducks).</param>
    /// <param name="koi">The koi (any depth: the deep ones are skipped by their chain's depth).</param>
    /// <param name="turtles">The turtles (the ones in the water).</param>
    public void Update(float dt, List<Body> surface, Body[] koi, IReadOnlyList<Turtle> turtles)
    {
        IReadOnlyList<LilyPad> pads = _pond.Pads;
        for (int p = 0; p < pads.Count; p++)
        {
            LilyPad pad = pads[p];
            if (pad.Hold > 0) { pad.Hold -= dt; continue; }
            float pushX = 0, pushY = 0, most = 0;
            for (int k = 0; k < surface.Count; k++) Shove(pad, surface[k], ref pushX, ref pushY, ref most);
            for (int k = 0; k < koi.Length; k++) Shove(pad, koi[k], ref pushX, ref pushY, ref most);
            for (int k = 0; k < turtles.Count; k++) if (turtles[k].InWater) Shove(pad, turtles[k], ref pushX, ref pushY, ref most);
            if (most > 0)
            {
                // Out of the way by the deepest overlap, along the sum of the shoves, at the pace a floating leaf
                // slides (it clears a body's overlap over about a tenth of a second), and the ground's squash on the y.
                float l = MathF.Sqrt(pushX * pushX + pushY * pushY);
                if (l > 1e-4f)
                {
                    float step = most * (1 - MathF.Exp(-dt * 10f));
                    pad.Center = new PointF(pad.Center.X + pushX / l * step, pad.Center.Y + pushY / l * step * Ripples.Squash);
                }
            }
            // The stalk: no further from the root than half a pad across; then home again, easing over a second or two
            // (slower while something is still against it, so the leaf does not fight its way back into the fish).
            float dx = pad.Center.X - pad.Anchor.X, dy = pad.Center.Y - pad.Anchor.Y, reach = pad.Radius * 0.5f;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > reach) { dx *= reach / d; dy *= reach / d; }
            float home = 1 - MathF.Exp(-dt * (most > 0 ? 0.15f : 0.8f));
            dx *= 1 - home; dy *= 1 - home;
            if (MathF.Abs(dx) < 0.3f && MathF.Abs(dy) < 0.3f) { dx = 0; dy = 0; }   // home: within a third of a pixel is at rest (the cheap copy below)
            pad.Center = new PointF(pad.Anchor.X + dx, pad.Anchor.Y + dy);
        }
    }

    /// <summary>One body against one pad: the deepest overlap of any of its surface points with the leaf, and the way out of it.</summary>
    private void Shove(LilyPad pad, Body body, ref float pushX, ref float pushY, ref float most)
    {
        ReadOnlySpan<PointF> spine = body.Drawn;
        ReadOnlySpan<float> half = body.DrawnHalfWidth;
        int n = Math.Min(spine.Length, body.Shape.Points.Length);
        float ry = pad.Radius * 0.82f;                       // the leaf is a little squashed down the screen, as everything on the water is
        for (int i = 0; i < n; i++)
        {
            if (body.Shape.Points[i].Z > _touchZ) continue;  // under the pad
            float dx = spine[i].X - pad.Center.X, dy = (spine[i].Y - pad.Center.Y) * pad.Radius / ry;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            float overlap = pad.Radius + (i < half.Length ? half[i] : 0) - d;
            if (overlap <= 0) continue;
            if (d < 1e-3f) { dx = 1; dy = 0; d = 1; }
            // The pad goes AWAY from the point, by the overlap. Several points of one body (the whole flank of a koi
            // lying across the leaf) add up in direction and the largest overlap is the distance.
            pushX -= dx / d * overlap; pushY -= dy / d * overlap;
            if (overlap > most) most = overlap;
        }
    }

    /// <summary>Stamps every pad at its place, over the water and whatever swims under it.</summary>
    public void Draw(FrameBuffer fb, bool[] onlyWhere)
    {
        IReadOnlyList<LilyPad> pads = _pond.Pads;
        uint[] frame = fb.Pixels;
        for (int p = 0; p < pads.Count; p++)
        {
            LilyPad pad = pads[p];
            if (pad.Center == pad.Anchor)
            {
                int[] idx = _restIndex[p]; uint[] px = _restPixel[p];
                for (int k = 0; k < idx.Length; k++) frame[idx[k]] = px[k];
            }
            else pad.Picture?.DrawCentered(fb, pad.Center.X, pad.Center.Y, onlyWhere: onlyWhere);
        }
    }
}
