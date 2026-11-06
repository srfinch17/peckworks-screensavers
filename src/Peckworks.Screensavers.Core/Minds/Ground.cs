namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// GROUND: walking on a surface with an edge you must not step off.
//
// FEYNMAN VERSION. Space.cs is for things that move through a volume (fish,
// birds). This file is for things that walk on a SURFACE with an edge: a
// lizard on a bank, a mouse in the grass, a cat on a wall. The surface is a
// question the animal answers ("is this spot ground I can stand on?"), so
// this file knows nothing about ponds or pixels. What it holds is the three
// small rules every walker needed and the lizard first learned the hard way:
//
//   1. STEP: try the step; if it is refused (water ahead) and the goal is
//      off to one side or behind, do not stop: slide along the edge, the
//      first heading round toward the goal that is ground, and carry on.
//      (A lizard that only ended its dash at the water faced it, stood for a
//      minute and never moved.)
//   2. LINE: a goal is only worth running at if the whole straight line to
//      it is ground, tested every few units. (Testing the midpoint alone let
//      a lizard on a wet rock pick spots across the water.)
//   3. RUN AWAY: the longest clear run from a point, straight away from it
//      if that is ground, else the clearest heading within about 90 degrees
//      of straight away; a slightly shorter run on the straight line wins, so
//      it does not swerve for nothing.
// ============================================================================

/// <summary>The three walking rules for anything that must stay on its surface.</summary>
public static class Ground
{
    /// <summary>The surface, as a question: can it stand here? The animal answers from its own mask, in its own unit.</summary>
    public delegate bool Solid(float x, float y);

    /// <summary>What a step did.</summary>
    public enum Stepped
    {
        /// <summary>It moved along its heading.</summary>
        Forward,
        /// <summary>The way ahead was refused; it moved along the edge instead, and its heading changed to match.</summary>
        Slid,
        /// <summary>Nothing was ground: it did not move.</summary>
        Refused,
    }

    /// <summary>
    /// Takes one step of "step" along "heading" if that spot is ground. If
    /// not, and the goal is mostly behind or off to one side (|turnErr| over
    /// about 30 degrees), tries headings round toward the goal, 35, 70, 105,
    /// 140 and 170 degrees off, and takes the first that is ground (an animal
    /// nose-to-water on a straight shore needs nearly a half turn). The
    /// heading is turned to the step actually taken.
    /// </summary>
    /// <param name="turnErr">How far round the goal is from the heading, radians, signed (0 when there is no goal: then only the straight step is tried).</param>
    public static Stepped Step(Solid ground, ref float x, ref float y, ref float heading, float step, float turnErr)
    {
        // The ground is asked at least two units ahead, however small the step (a dash's first step is a sixth of a pixel
        // while the speed builds from nothing): the line test that chose the goal (LineClear) samples every few units, and
        // a step that asked about the very next pixel disagreed with it at a jagged shore, refusing a dash whose line was
        // clear (Sakura Pond's lizard on a bank stone, round forty-seven). The move is still the step itself.
        // And a NOTCH is crossed: ground at two, four or six units ahead is a way forward (a three-pixel bay in a painted
        // shoreline refused every step while the line test, sampling every four, had passed over it).
        float probe = MathF.Max(step, 2f), ox = x, oy = y;   // (copies: a local function may not read a ref parameter)
        bool Ahead(float a)
        {
            float ca = MathF.Cos(a), sa = MathF.Sin(a);
            return ground(ox + ca * probe, oy + sa * probe) || ground(ox + ca * probe * 2, oy + sa * probe * 2) || ground(ox + ca * probe * 3, oy + sa * probe * 3);
        }
        if (Ahead(heading)) { x += MathF.Cos(heading) * step; y += MathF.Sin(heading) * step; return Stepped.Forward; }
        if (MathF.Abs(turnErr) > 0.5f)
            for (int k = 1; k <= 5; k++)
            {
                float a = heading + (turnErr > 0 ? 0.6f : -0.6f) * k;
                if (Ahead(a)) { x += MathF.Cos(a) * step; y += MathF.Sin(a) * step; heading = MathF.IEEERemainder(a, MathF.Tau); return Stepped.Slid; }
            }
        return Stepped.Refused;
    }

    /// <summary>
    /// Like Step, but a thing straight AHEAD is gone round, never refused: when the way ahead is not ground it tries
    /// 30 and 60 degrees off, the goal's side of the heading first and then the other, and takes the first that is
    /// ground (no further round: a side-view animal stepping 90 degrees off slides up or down the screen on the spot). Step's rule (slide only when the goal is well off the heading) is right for a shore,
    /// where straight ahead is the water and there is nothing round it; among obstacles on open ground (pumpkins in a
    /// patch) the animal running straight at one has a side to go round on, and refusing left it hung at the pumpkin.
    /// The heading is turned to the step actually taken; the caller's own turning brings it back toward the goal next
    /// frame, so the path round the thing is a curve, not a corner.
    /// </summary>
    public static Stepped StepRound(Solid ground, ref float x, ref float y, ref float heading, float step, float turnErr)
    {
        float probe = MathF.Max(step, 2f), ox = x, oy = y;
        bool Ahead(float a)
        {
            float ca = MathF.Cos(a), sa = MathF.Sin(a);
            return ground(ox + ca * probe, oy + sa * probe) && ground(ox + ca * probe * 2, oy + sa * probe * 2);
        }
        if (Ahead(heading)) { x += MathF.Cos(heading) * step; y += MathF.Sin(heading) * step; return Stepped.Forward; }
        float first = turnErr >= 0 ? 1f : -1f;
        for (int k = 1; k <= 2; k++)
            foreach (float side in new[] { first, -first })
            {
                float a = heading + side * 0.52f * k;
                if (Ahead(a)) { x += MathF.Cos(a) * step; y += MathF.Sin(a) * step; heading = MathF.IEEERemainder(a, MathF.Tau); return Stepped.Slid; }
            }
        return Stepped.Refused;
    }

    /// <summary>Is the straight line from a to b all ground, tested about every "every" units (the way the animal can actually walk it)?</summary>
    public static bool LineClear(Solid ground, PointF a, PointF b, float every)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        int steps = (int)(l / MathF.Max(0.5f, every)) + 1;
        for (int i = 1; i <= steps; i++)
            if (!ground(a.X + dx * i / steps, a.Y + dy * i / steps)) return false;
        return true;
    }

    /// <summary>
    /// Where to run to get away from "from": the end of the longest clear run
    /// from "me", straight away if that is clear, else the clearest heading
    /// within about 90 degrees of straight away. Each heading is marched in
    /// "stride" steps up to "most" while "clear" says yes; a run must beat the
    /// best so far by half a stride to replace it (the straight line wins
    /// ties). Null if no run reaches "least".
    /// </summary>
    /// <param name="clear">Ground AND whatever else a run needs (in view, say): the animal's own test.</param>
    public static PointF? FleeRun(Solid clear, PointF me, PointF from, float stride, float most, float least)
    {
        float ax = me.X - from.X, ay = me.Y - from.Y, al = MathF.Max(1e-3f, MathF.Sqrt(ax * ax + ay * ay));
        float baseA = MathF.Atan2(ay / al, ax / al), bestD = 0, bestA = baseA;
        for (int k = 0; k < 7; k++)
        {
            float off = k == 0 ? 0 : ((k + 1) / 2) * 0.5f * (k % 2 == 1 ? 1 : -1);
            float a = baseA + off, cos = MathF.Cos(a), sin = MathF.Sin(a), d = 0;
            for (float t = stride; t <= most; t += stride)
            {
                if (!clear(me.X + cos * t, me.Y + sin * t)) break;
                d = t;
            }
            if (d > bestD + stride * 0.5f) { bestD = d; bestA = a; }
        }
        if (bestD < least) return null;
        return new PointF(me.X + MathF.Cos(bestA) * bestD, me.Y + MathF.Sin(bestA) * bestD);
    }
}
