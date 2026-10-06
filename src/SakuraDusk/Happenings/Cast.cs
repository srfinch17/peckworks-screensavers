using Peckworks.Screensavers.Core;

namespace SakuraDusk.Happenings;

/// <summary>
/// The list of every small thing that can happen in this scene. The director
/// (Core/Happenings.cs) shuffles this list like a deck of cards and deals one
/// every few seconds. To add one: write a class in this folder and add one
/// line here. The step-by-step is in docs/ADDING_HAPPENINGS.md.
/// </summary>
internal static class DuskHappenings
{
    public static (string Name, Func<Happening> Make)[] Cast(DuskScenery s) =>
    [
        // ---- the sky and the sun ----
        (nameof(GreenFlash), () => new GreenFlash(s)),
        (nameof(GodRays), () => new GodRays(s)),
        (nameof(FirstStar), () => new FirstStar(s)),
        (nameof(Moonrise), () => new Moonrise(s)),
        (nameof(GoldenClouds), () => new GoldenClouds(s)),
        (nameof(MagicHour), () => new MagicHour(s)),
        (nameof(CraneAcrossSun), () => new CraneAcrossSun(s)),
        // ---- the hills ----
        (nameof(FoxLights), () => new FoxLights(s)),
        // ---- the pond ----
        (nameof(ToroNagashi), () => new ToroNagashi(s)),
        (nameof(KoiPond), () => new KoiPond(s)),
        (nameof(LotusBloom), () => new LotusBloom(s)),
        (nameof(PondMist), () => new PondMist(s)),
        (nameof(WindRipple), () => new WindRipple(s)),
        (nameof(BashoFrog), () => new BashoFrog(s)),
        // ---- the bridge and the banks ----
        (nameof(BridgeCat), () => new BridgeCat(s)),
        (nameof(ChochinLanterns), () => new ChochinLanterns(s)),
        (nameof(Tanuki), () => new Tanuki(s)),
        (nameof(Yozakura), () => new Yozakura(s)),
        // ---- the air ----
        (nameof(Fireflies), () => new Fireflies(s)),
        (nameof(RedDragonflies), () => new RedDragonflies(s)),
    ];
}
