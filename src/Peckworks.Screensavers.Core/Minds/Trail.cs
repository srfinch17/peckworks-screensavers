namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// TRAIL: the path a head has taken, and a body laid along it.
//
// FEYNMAN VERSION. Every bending animal in these screensavers moves the same
// way: only its HEAD is steered, and the body follows the path the head has
// already taken, like the carriages of a train following the engine along
// the track. So a turn bends the body exactly through the curve, and nothing
// is ever flipped or slid. To do that the animal remembers where its head has
// been (a trail of points) and, every frame, walks back along that trail
// laying the spine down: "the neck is this far behind the nose, the hips
// this far, the tail tip this far".
//
// This file is that memory and that walk, written once. A koi, a snake, a
// lizard and a mouse all use it; what differs per animal (how far apart the
// spine points are, the swimming wave, the width along the body) stays in the
// animal. The trail knows nothing about pixels or ponds: points are whatever
// unit the animal moves in (Sakura Pond: ground pixels), and each point may
// carry a depth, so a diving fish's body is laid at the depths its head swam.
// ============================================================================

/// <summary>
/// A ring of remembered head positions, newest last, with an optional depth
/// per point. A fixed row of slots used round and round, so remembering never
/// allocates while the screensaver runs.
/// </summary>
public sealed class Trail
{
    private readonly PointF[] _pts;
    private readonly float[] _z;
    private int _head, _count;

    /// <param name="slots">How many points to keep: enough for the body's length at the smallest step it remembers (a koi keeps 96 at a fortieth of a length).</param>
    public Trail(int slots)
    {
        _pts = new PointF[Math.Max(2, slots)];
        _z = new float[_pts.Length];
    }

    /// <summary>How many points are remembered (up to the slots).</summary>
    public int Count => _count;

    /// <summary>The newest point (meaningless while Count is 0).</summary>
    public PointF Newest => _pts[_head];

    /// <summary>The newest point's depth.</summary>
    public float NewestZ => _z[_head];

    /// <summary>Forgets everything.</summary>
    public void Clear() { _count = 0; _head = 0; }

    /// <summary>Remembers where the head is now (and how deep).</summary>
    public void Remember(PointF p, float z = 0)
    {
        _head = (_head + 1) % _pts.Length;
        _pts[_head] = p;
        _z[_head] = z;
        _count = Math.Min(_pts.Length, _count + 1);
    }

    /// <summary>
    /// Remembers the head only if it has moved at least "minStep" from the
    /// last remembered point: the guard every animal wrote by hand, so a
    /// still animal does not fill its memory with one spot.
    /// </summary>
    public void RememberIfMoved(PointF p, float minStep, float z = 0)
    {
        if (_count > 0)
        {
            PointF last = Newest;
            float dx = p.X - last.X, dy = p.Y - last.Y;
            if (dx * dx + dy * dy <= minStep * minStep) return;
        }
        Remember(p, z);
    }

    /// <summary>
    /// A straight trail running "length" back from "head" against "heading",
    /// in n steps, for an animal that has just appeared: so its spine means
    /// something before it has moved.
    /// </summary>
    public void Straight(PointF head, float heading, float length, int n, float z = 0)
    {
        Clear();
        float cx = MathF.Cos(heading), cy = MathF.Sin(heading);
        for (int i = n; i >= 0; i--) Remember(new PointF(head.X - cx * length * i / n, head.Y - cy * length * i / n), z);
    }

    /// <summary>Moves every remembered point (the whole body when the animal is shoved: moving the head alone leaves the tail behind).</summary>
    public void Shift(float dx, float dy, float dz = 0)
    {
        for (int i = 0; i < _count; i++)
        {
            int slot = (_head - i + _pts.Length) % _pts.Length;
            _pts[slot] = new PointF(_pts[slot].X + dx, _pts[slot].Y + dy);
            _z[slot] = MathF.Max(0, _z[slot] + dz);
        }
    }

    /// <summary>
    /// Changes every remembered depth: adds "settled" (a drift of the whole
    /// body up or down) and then eases each toward "toward" by "levelling"
    /// (0 none, 1 all the way): a resting animal levels out over a second or
    /// two. Depths never go above 0 (the surface).
    /// </summary>
    public void EaseDepths(float toward, float settled, float levelling)
    {
        for (int i = 0; i < _count; i++)
        {
            int slot = (_head - i + _pts.Length) % _pts.Length;
            float z = _z[slot] + settled;
            _z[slot] = MathF.Max(0, z + (toward - z) * levelling);
        }
    }

    /// <summary>
    /// Starts a walk back along the trail from the head's position NOW (which
    /// is not yet in the trail). Ask it for points at growing distances back
    /// (Walk.Advance), each at most once per distance, in increasing order.
    /// </summary>
    /// <param name="head">Where the head is now.</param>
    /// <param name="headZ">How deep the head is now.</param>
    /// <param name="depthPx">What one unit of depth is in the trail's unit of distance, so a dive counts as distance travelled (0: measure on the plane only).</param>
    /// <param name="heading">The way the head faces: where the trail runs out, the body continues straight back against this.</param>
    public Walk Begin(PointF head, float headZ, float depthPx, float heading) => new(this, head, headZ, depthPx, heading);

    /// <summary>
    /// A walk back along a trail: hands out the point a given distance behind
    /// the head, measured along the path the head took (in three dimensions
    /// when depths are kept). Call Advance with distances that never decrease.
    /// </summary>
    public struct Walk
    {
        private readonly Trail _t;
        private readonly float _depthPx, _heading;
        private int _slot, _left;
        private PointF _a, _b;
        private float _za, _zb, _travelled, _segLen;

        internal Walk(Trail t, PointF head, float headZ, float depthPx, float heading)
        {
            _t = t; _depthPx = depthPx; _heading = heading;
            _slot = t._head; _left = t._count - 1;
            _a = head; _za = headZ;
            if (t._count > 0) { _b = t._pts[_slot]; _zb = t._z[_slot]; }
            else { _b = head; _zb = headZ; _left = 0; }
            _travelled = 0;
            _segLen = Dist(_a, _za, _b, _zb);
        }

        private float Dist(PointF a, float za, PointF b, float zb)
        {
            float dz = (zb - za) * _depthPx;
            return MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + dz * dz);
        }

        /// <summary>The point "want" behind the head along the path, and its depth. Past the end of the trail the body carries straight on back.</summary>
        public (PointF P, float Z) Advance(float want)
        {
            while (_travelled + _segLen < want && _left > 0)
            {
                _travelled += _segLen;
                _a = _b; _za = _zb;
                _slot = (_slot - 1 + _t._pts.Length) % _t._pts.Length;
                _left--;
                _b = _t._pts[_slot]; _zb = _t._z[_slot];
                _segLen = Dist(_a, _za, _b, _zb);
            }
            if (_left <= 0 && _travelled + _segLen < want)
            {
                // Ran out of trail (just appeared, or a short memory): carry on straight back.
                float ex = _segLen > 1e-4f ? (_b.X - _a.X) / _segLen : -MathF.Cos(_heading), ey = _segLen > 1e-4f ? (_b.Y - _a.Y) / _segLen : -MathF.Sin(_heading);
                float over = want - _travelled - _segLen;
                return (new PointF(_b.X + ex * over, _b.Y + ey * over), _zb);
            }
            float f = _segLen > 1e-4f ? (want - _travelled) / _segLen : 0;
            return (new PointF(_a.X + (_b.X - _a.X) * f, _a.Y + (_b.Y - _a.Y) * f), _za + (_zb - _za) * f);
        }
    }
}
