namespace Peckworks.Screensavers.Core.Minds;

// ============================================================================
// HERD: the two sums a group needs so that its members read as a group.
//
// FEYNMAN VERSION. A fawn that walks to exactly where its mother is walks
// INTO her; one that aims at the same spot she aims at orbits (the pond's
// follower rule). A follower keeps a GAP behind its leader, on the leader's
// own side, and only moves when the gap is wrong by more than it can bear.
// And animals of a kind keep a little room between them: when two stand too
// close, the one that is moving gives way. Both are a few lines, but every
// group that was written without them bumped or merged, so they live here.
// ============================================================================

/// <summary>Keeping a gap behind a leader, and keeping room between neighbours.</summary>
public static class Herd
{
    /// <summary>Where a follower belongs: "gap" behind the leader, on the side the leader came from.</summary>
    public static float FollowX(float leaderX, int leaderDir, float gap) => leaderX - leaderDir * gap;

    /// <summary>
    /// How far a follower is from where it belongs behind its leader (signed: positive means it must move toward
    /// +x), or 0 when the gap is within "slack" of right. A follower that only moves when this is not 0 does not
    /// fidget after every step its leader takes.
    /// </summary>
    public static float FollowError(float meX, float leaderX, int leaderDir, float gap, float slack)
    {
        float err = FollowX(leaderX, leaderDir, gap) - meX;
        return MathF.Abs(err) <= slack ? 0f : err;
    }

    /// <summary>
    /// The push this member feels from neighbours standing too close: the sum, over every other member within
    /// "room", of how far inside the room it stands, away from it (signed, along x). 0 when it has room.
    /// </summary>
    public static float Spacing(ReadOnlySpan<float> xs, int me, float room)
    {
        float push = 0;
        for (int i = 0; i < xs.Length; i++)
        {
            if (i == me) continue;
            float d = xs[me] - xs[i];
            if (MathF.Abs(d) < room) push += (d >= 0 ? room - d : -(room + d));
        }
        return push;
    }
}
