using Peckworks.Screensavers.Core;

namespace Halloween.Happenings;

/// <summary>
/// The list of every small thing that can happen in the Halloween scene.
/// The director (Core/Happenings.cs) shuffles this list like a deck of cards
/// and deals one every few seconds.
///
/// To add a happening: write one class in this folder (copy ShootingStar.cs
/// for a sky one, SkeletonEyes.cs for one tied to a prop) and add one line
/// here. The name in quotes is what PECKWORKS_HAPPENING takes for testing.
/// The step-by-step is in docs/ADDING_HAPPENINGS.md.
///
/// Each line is "a name, and a recipe for building it". The "() =>" part is
/// the recipe: a little function the director runs the first time that
/// happening is dealt, so nothing is painted until it is needed.
/// </summary>
internal static class HalloweenHappenings
{
    public static (string Name, Func<Happening> Make)[] Cast(HalloweenScenery s) =>
    [
        // ---- the sky ----
        (nameof(ShootingStar), () => new ShootingStar(s)),
        (nameof(Fireworks), () => new Fireworks(s)),
        (nameof(Lightning), () => new Lightning(s)),
        (nameof(Constellation), () => new Constellation(s)),
        (nameof(GhostParade), () => new GhostParade(s)),
        // ---- the moon ----
        (nameof(BloodMoon), () => new BloodMoon(s)),
        (nameof(WitchFlyby), () => new WitchFlyby(s)),
        // ---- the far hill and the haunted house ----
        (nameof(HouseLights), () => new HouseLights(s)),
        (nameof(BatBurst), () => new BatBurst(s)),
        (nameof(HillMonster), () => new HillMonster(s)),
        // ---- the graveyard ----
        (nameof(GraveGhost), () => new GraveGhost(s)),
        (nameof(ZombieHand), () => new ZombieHand(s)),
        (nameof(WillOWisps), () => new WillOWisps(s)),
        (nameof(SkeletonEyes), () => new SkeletonEyes(s)),
        (nameof(PumpkinFace), () => new PumpkinFace(s)),
        (nameof(BlackCat), () => new BlackCat(s)),
        // ---- the trees ----
        (nameof(SpiderWeb), () => new SpiderWeb(s)),
        (nameof(DanglingSpider), () => new DanglingSpider(s)),
        (nameof(EyesInTheDark), () => new EyesInTheDark(s)),
    ];
}
