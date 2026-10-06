using Peckworks.Screensavers.Core;

namespace Christmas.Happenings;

/// <summary>
/// The list of every small thing that can happen in the Christmas scene.
/// The director (Core/Happenings.cs) shuffles this list like a deck of cards
/// and deals one every few seconds.
///
/// To add a happening: write one class in this folder (copy ShootingStar.cs
/// for a sky one, StarFlare.cs for one tied to a prop) and add one line
/// here. The name in quotes is what PECKWORKS_HAPPENING takes for testing.
/// The step-by-step is in docs/ADDING_HAPPENINGS.md.
/// </summary>
internal static class ChristmasHappenings
{
    public static (string Name, Func<Happening> Make)[] Cast(ChristmasScenery s) =>
    [
        // ---- the sky ----
        (nameof(ShootingStar), () => new ShootingStar(s)),
        (nameof(Aurora), () => new Aurora(s)),
        (nameof(Constellation), () => new Constellation(s)),
        (nameof(Fireworks), () => new Fireworks(s)),
        (nameof(SnowGust), () => new SnowGust(s)),
        (nameof(FairyDust), () => new FairyDust(s)),
        // ---- the moon ----
        (nameof(MoonHalo), () => new MoonHalo(s)),
        // ---- Santa ----
        (nameof(PresentDrop), () => new PresentDrop(s)),
        // ---- far off ----
        (nameof(ToyTrain), () => new ToyTrain(s)),
        (nameof(RidgeReindeer), () => new RidgeReindeer(s)),
        // ---- the snow ----
        (nameof(Fox), () => new Fox(s)),
        (nameof(Rabbit), () => new Rabbit(s)),
        (nameof(Snowman), () => new Snowman(s)),
        (nameof(RollingSnowball), () => new RollingSnowball(s)),
        (nameof(SnowSlide), () => new SnowSlide(s)),
        // ---- the cabin and the lights ----
        (nameof(ChimneyRings), () => new ChimneyRings(s)),
        (nameof(LightWave), () => new LightWave(s)),
        (nameof(StarFlare), () => new StarFlare(s)),
        // ---- the corner branches ----
        (nameof(FrostCrystals), () => new FrostCrystals(s)),
        (nameof(SnowyOwl), () => new SnowyOwl(s)),
    ];
}
