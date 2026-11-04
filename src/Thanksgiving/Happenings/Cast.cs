using Peckworks.Screensavers.Core;

namespace Thanksgiving.Happenings;

/// <summary>
/// The list of every small thing that can happen in the Thanksgiving scene.
/// The director (Core/Happenings.cs) shuffles this list like a deck of cards
/// and deals one every few seconds.
///
/// To add a happening: write one class in this folder (copy ShootingStar.cs
/// for a sky one, Turkey.cs for a walker on the ground) and add one line
/// here. The name in quotes is what PECKWORKS_HAPPENING takes for testing.
/// The step-by-step is in docs/ADDING_HAPPENINGS.md.
/// </summary>
internal static class ThanksgivingHappenings
{
    public static (string Name, Func<Happening> Make)[] Cast(ThanksgivingScenery s) =>
    [
        // ---- the sky ----
        (nameof(ShootingStar), () => new ShootingStar(s)),
        (nameof(Constellation), () => new Constellation(s)),
        (nameof(Fireworks), () => new Fireworks(s)),
        (nameof(LeafGust), () => new LeafGust(s)),
        (nameof(Hawk), () => new Hawk(s)),
        (nameof(ParadeBalloon), () => new ParadeBalloon(s)),
        // ---- the farm and the field ----
        (nameof(PorchLight), () => new PorchLight(s)),
        (nameof(WindowSilhouettes), () => new WindowSilhouettes(s)),
        (nameof(BarnOwl), () => new BarnOwl(s)),
        (nameof(Tractor), () => new Tractor(s)),
        (nameof(Deer), () => new Deer(s)),
        // ---- the near ground ----
        (nameof(Turkey), () => new Turkey(s)),
        (nameof(Squirrel), () => new Squirrel(s)),
        (nameof(Crow), () => new Crow(s)),
        (nameof(Raccoon), () => new Raccoon(s)),
        (nameof(Pheasant), () => new Pheasant(s)),
        (nameof(ScarecrowWink), () => new ScarecrowWink(s)),
        // ---- the cornucopia and the corner branches ----
        (nameof(AppleRoll), () => new AppleRoll(s)),
        (nameof(AcornDrop), () => new AcornDrop(s)),
        (nameof(Cardinal), () => new Cardinal(s)),
    ];
}
