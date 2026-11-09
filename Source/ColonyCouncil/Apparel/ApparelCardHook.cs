using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colonist card's clothes suggestions (UI/ColonistCard/ColonistCardModel.cs, <see cref="ColonistCardHooks.KitLines"/>):
    /// under their apparel, the same standing suggestions the gear tab shows (Apparel/ApparelPick.cs), each with its head,
    /// why, the piece's picture and the gear tab's own act. The card was built beside the apparel suggestions and left
    /// this hook for them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ApparelCardHook
    {
        static ApparelCardHook()
        {
            ColonistCardHooks.KitLines("apparel-suggestions", Lines);
        }

        private static IEnumerable<ColonistCardLine> Lines(Pawn p)
        {
            if (p == null || !ApparelPick.On) yield break;
            var picks = p.Spawned ? ApparelPick.For(p.Map) : null;
            if (picks == null) yield break;
            foreach (var s in ApparelPick.Standing(p).Take(3))
            {
                var suggestion = s;
                yield return new ColonistCardLine
                {
                    thing = picks.Thing(s.Piece),
                    text = ApparelPick.Head(picks, s),
                    tip = ApparelPick.Full(picks, s),
                    act = ApparelPickGearTab.ActLabel(picks, s),
                    whyNot = ApparelPick.OnTheWay(picks, s) ? "CC_WeaponPick_OnTheWay".Translate().Resolve() : ApparelPick.WhyNot(picks, s),
                    actTip = ApparelPickGearTab.ActTip(picks, s),
                    run = () => ApparelPick.Do(picks, suggestion)
                };
            }
        }
    }
}
