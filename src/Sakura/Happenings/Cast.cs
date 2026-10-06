using Peckworks.Screensavers.Core;

namespace Sakura.Happenings;

/// <summary>
/// The list of every small thing that can happen in this scene. The director
/// (Core/Happenings.cs) shuffles this list like a deck of cards and deals one
/// every few seconds. To add one: write a class in this folder and add one
/// line here. The step-by-step is in docs/ADDING_HAPPENINGS.md.
/// </summary>
internal static class SakuraHappenings
{
    public static (string Name, Func<Happening> Make)[] Cast(Scenery s) =>
    [
        // ---- the sky and Fuji ----
        (nameof(KasaGumo), () => new KasaGumo(s)),
        (nameof(RedFuji), () => new RedFuji(s)),
        (nameof(DiamondFuji), () => new DiamondFuji(s)),
        (nameof(PaperCranes), () => new PaperCranes(s)),
        (nameof(YakkoKite), () => new YakkoKite(s)),
        (nameof(Rainbow), () => new Rainbow(s)),
        (nameof(CloudShadows), () => new CloudShadows(s)),
        // ---- the far shore and the lake ----
        (nameof(Shinkansen), () => new Shinkansen(s)),
        (nameof(SunShower), () => new SunShower(s)),
        (nameof(DuckFamily), () => new DuckFamily(s)),
        (nameof(Hanaikada), () => new Hanaikada(s)),
        (nameof(Kingfisher), () => new Kingfisher(s)),
        (nameof(Swallows), () => new Swallows(s)),
        // ---- the banks ----
        (nameof(Heron), () => new Heron(s)),
        (nameof(ShibaInu), () => new ShibaInu(s)),
        (nameof(Butterflies), () => new Butterflies(s)),
        // ---- the branches ----
        (nameof(Mejiro), () => new Mejiro(s)),
        (nameof(Squirrel), () => new Squirrel(s)),
        (nameof(Furin), () => new Furin(s)),
        // ---- the air ----
        (nameof(Hanafubuki), () => new Hanafubuki(s)),
    ];
}
