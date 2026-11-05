namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// MOVER: the small physics of moving like a living thing instead of a toy.
//
// FEYNMAN VERSION. A toy starts at full speed the instant you switch it on
// and stops dead when you switch it off; its head snaps round to face the
// new way. A living thing LEANS into a walk, gathers speed over a few steps,
// slows into a stop so its last step lands square, and comes round through
// a turn that takes time. The eye reads those ramps as life even when it
// cannot say why. This file holds the three ramps every walker in these
// screensavers uses:
//
//   1. PACE: a speed that eases toward what the animal wants, with a cap on
//      how fast it may change (an acceleration), and a BRAKE that runs the
//      speed down to nothing over EXACTLY a chosen distance, so a walk can
//      end on the page where both feet are on the ground. It also keeps the
//      odometer (how far it has walked), which is what a flip-book of planted
//      feet is paged by: a foot stays planted only when the page follows the
//      distance, never the clock.
//   2. TURN: a heading that turns toward a target at a rate, slowing as it
//      gets there (no overshoot, no snap).
//   3. ORGANIC: a slow drift made of three waves that never line up, for the
//      small restlessness a standing animal has (a head that is never quite
//      still), and a jitter for timers so nothing happens on a metronome.
//
// None of this knows about ponds, fields or pixels: speeds and distances
// are whatever unit the animal uses. The recipe is docs/MINDS.md.
// ============================================================================

/// <summary>A speed that eases, a brake that stops on a chosen distance, and an odometer for paging planted feet.</summary>
public sealed class Pace
{
    /// <summary>How fast it is going now (units a second).</summary>
    public float Speed { get; private set; }
    /// <summary>How fast it wants to go. Set it and Tick eases Speed toward it.</summary>
    public float Want;
    /// <summary>The most Speed may change in a second (units a second a second). A deer leans into a walk over about half a second: Accel about twice its walking speed.</summary>
    public float Accel;
    /// <summary>How far it has walked, in units: page a walk's flip-book by this, never by the clock.</summary>
    public float Walked { get; set; }

    private bool _braking;
    private float _brakeFrom, _brakeDist, _brakeT, _brakeFor;

    public Pace(float accel) { Accel = accel; }

    /// <summary>Is the brake on (a stop in progress)?</summary>
    public bool Braking => _braking;
    /// <summary>Standing: no speed and no stop in progress.</summary>
    public bool Still => !_braking && Speed <= 0f;

    /// <summary>
    /// Begins a stop that covers exactly "dist" more units then ends at nothing: the speed falls evenly from what it
    /// is now to zero over twice the time the distance would take at that speed (the walked distance follows
    /// 2k - k*k). Choose dist so the walk ends on the rest page (a whole number of strides ahead). While braking,
    /// Want is ignored; set Want after it ends to walk on.
    /// </summary>
    public void Brake(float dist)
    {
        if (Speed <= 0f || dist <= 0f) { Speed = 0f; _braking = false; Want = 0f; return; }
        _braking = true;
        _brakeFrom = Walked; _brakeDist = dist; _brakeT = 0f;
        _brakeFor = 2f * dist / Speed;
        Want = 0f;
    }

    /// <summary>Stops at once (a startle has no run-down).</summary>
    public void Halt() { Speed = 0f; Want = 0f; _braking = false; }

    /// <summary>Eases the speed and advances the odometer. Returns how far it moved this frame (units), for the animal to move its body by.</summary>
    public float Tick(float dt)
    {
        if (_braking)
        {
            _brakeT += dt;
            float k = Math.Min(1f, _brakeT / _brakeFor);
            float walked = _brakeFrom + _brakeDist * (2 * k - k * k);
            float moved = walked - Walked;
            Walked = walked;
            Speed = _brakeDist * 2 * (1 - k) / _brakeFor;              // the derivative: falls evenly to zero
            if (k >= 1f) { _braking = false; Speed = 0f; }
            return moved;
        }
        // Ease toward the wanted speed: a lag (slow near the target, so the change is smooth) under an acceleration cap.
        float gap = Want - Speed;
        float step = gap * Math.Min(1f, dt * 8f);
        float cap = Accel * dt;
        step = Math.Clamp(step, -cap, cap);
        if (MathF.Abs(gap) < 1e-4f) Speed = Want; else Speed += step;
        float d = Speed * dt;
        Walked += d;
        return d;
    }

    /// <summary>The page of an n-page flip-book for one stride of "stride" units, by the odometer (a planted foot stays planted).</summary>
    public int Page(float stride, int pages)
    {
        float cyc = Walked / stride;
        return (int)MathF.Floor((cyc - MathF.Floor(cyc)) * pages) % pages;
    }

    /// <summary>How far to the next rest page (the start of a stride), at least a quarter stride away (closer than that it takes one more stride, so the stop can be even).</summary>
    public float ToRestPage(float stride)
    {
        float into = Walked % stride;
        float dist = stride - into;
        if (dist < 0.25f * stride) dist += stride;
        return dist;
    }
}

/// <summary>A heading that turns at a rate and slows into the target.</summary>
public static class Turn
{
    /// <summary>
    /// Turns "heading" toward "target" (radians) by at most "rate" radians a second, easing to a third of that
    /// within the last fifteen degrees so it settles instead of ticking back and forth across the target.
    /// Returns the new heading, wrapped to -pi..pi.
    /// </summary>
    public static float Toward(float heading, float target, float rate, float dt)
    {
        float err = MathF.IEEERemainder(target - heading, MathF.Tau);
        float ease = Math.Clamp(MathF.Abs(err) / 0.26f, 0.33f, 1f);
        float step = Math.Clamp(err, -rate * ease * dt, rate * ease * dt);
        return MathF.IEEERemainder(heading + step, MathF.Tau);
    }

    /// <summary>The shortest signed angle from a to b, radians.</summary>
    public static float Error(float a, float b) => MathF.IEEERemainder(b - a, MathF.Tau);
}

/// <summary>Slow drift and uneven timers: the small restlessness of a living thing.</summary>
public static class Organic
{
    /// <summary>
    /// A slow wander between -1 and 1 made of three waves whose periods never line up (about 7, 11 and 17 units of
    /// t at scale 1), so it never repeats within a watch. "seed" shifts it so two animals do not nod together.
    /// Feed it the scene's clock in seconds, scaled by how restless the thing is.
    /// </summary>
    public static float Drift(double t, float seed = 0f)
    {
        double s = seed * 7.31;
        return (float)((Math.Sin(t * 0.893 + s) + Math.Sin(t * 0.571 + s * 1.7 + 1.3) * 0.7 + Math.Sin(t * 0.367 + s * 0.4 + 2.1) * 0.5) / 2.2);
    }

    /// <summary>A timer with a natural spread: mostly near "mean", sometimes well over, never under a third of it. Seconds.</summary>
    public static float Jitter(Random rng, float mean, float spread = 0.6f)
    {
        double u = rng.NextDouble();
        return mean * (float)(0.34 + 0.66 * (u + u * u * spread * 2));
    }

    /// <summary>Ease in and out: 0 to 1 with a soft start and finish.</summary>
    public static float Smooth(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
}
