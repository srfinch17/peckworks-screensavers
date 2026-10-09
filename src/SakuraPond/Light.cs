namespace SakuraPond;

/// <summary>
/// Where the sun is and how we look at the pond, shared by everything that
/// flies or falls, so heights and shadows agree across petals, dragonflies
/// and swallows.
///
/// We look down at the pond from above and a little in front. A thing HIGH
/// above a spot on the water therefore shows a little further UP the screen
/// than the spot itself ("Lift", per unit of height), and its shadow, cast
/// by the sun up and to the left, falls down and to the right of the spot
/// ("ShadowX", "ShadowY", per unit of height). Heights are in u, the size unit.
/// </summary>
internal static class Light
{
    public const float Lift = 0.75f;
    public const float ShadowX = 0.45f, ShadowY = 0.6f;
}
