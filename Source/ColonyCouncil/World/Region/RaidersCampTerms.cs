using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A raiders' camp's terms (docs/design/stage1-region.md, item 4, "Paid"): a day after it is found, when one of
    /// Rimfolk's questions may come (Tell.CanAsk), word of their price comes: about half a raid's points in silver, to
    /// break camp and keep away for a year. Paid, the silver goes out by hand (<see cref="DilemmaEffect_RaidersCampPrice"/>).
    /// The region page offers the same price again while the camp stands.
    /// </summary>
    public static class RaidersCampTerms
    {
        /// <summary>Their terms, as a question: pay, or refuse.</summary>
        internal static void Offer(Map home, RaidersCamp c)
        {
            if (home == null || c == null || RaidersCampDefOf.Rimfolk_RaidersCampTerms == null) return;
            c.offered = true;
            var site = RaidersCamps.SiteOf(c);
            string label = "CC_RaidCamp_Terms_Label".Translate(c.kind.Named("KIND")).Resolve().CapitalizeFirst();
            string text = "CC_RaidCamp_Terms_Text".Translate(c.kind.Named("KIND"), c.people.Name.Named("PEOPLE"), RaidersCamps.Where(home, c).Named("WHERE"),
                c.price.Named("SILVER"), Homes.Name(home).Named("COLONY")).Resolve();
            text += "\n\n" + "CC_RaidCamp_Terms_How".Translate().Resolve();
            var letter = LetterMaker.MakeLetter(label, text, RaidersCampDefOf.Rimfolk_RaidersCampTerms, site != null ? new LookTargets(site) : LookTargets.Invalid, c.people);
            if (letter is ChoiceLetter_RaidersCamp l)
            {
                l.home = Homes.KeyOf(home);
                l.setAt = c.setAt;
            }
            Tell.Ask(letter, home);
        }

        /// <summary>Why the colony can't pay now (not enough silver), or null.</summary>
        public static string CantPay(Map home, RaidersCamp c)
        {
            if (home == null || c == null) return "";
            int have = PlayCarry.Have(home, "silver", ThingDefOf.Silver);
            return have < c.price ? "CC_RaidCamp_NotEnough".Translate(have.Named("HAVE"), c.price.Named("SILVER")).Resolve() : null;
        }
    }

    /// <summary>
    /// Their terms, as a letter: pay their price (greyed out without the silver) or refuse. Once the camp is gone, or
    /// paid by other means, only closing is left.
    /// </summary>
    public class ChoiceLetter_RaidersCamp : ChoiceLetter
    {
        /// <summary>The home it threatens (Homes.KeyOf) and when the camp was set up, to know it is still the same one.</summary>
        public int home = HomesMath.None;
        public int setAt = -1;

        private RaidersCamp Live(out Map map)
        {
            map = Homes.MapOf(home);
            var c = RaidersCamps.CampOf(map);
            return c != null && c.setAt == setAt && c.stage == CampStage.Found ? c : null;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var c = ArchivedOnly ? null : Live(out _);
                if (c == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                var pay = new DiaOption("CC_RaidCamp_Pay".Translate(c.price.Named("SILVER")).Resolve())
                {
                    action = () =>
                    {
                        var now = Live(out var map);
                        if (now != null) RaidersCamps.Pay(map, now);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                string why = RaidersCampTerms.CantPay(Homes.MapOf(home), c);
                if (why != null) pay.Disable(why);
                yield return pay;
                yield return new DiaOption("CC_RaidCamp_Refuse".Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref setAt, "setAt", -1);
        }
    }

    /// <summary>
    /// The camp's price carried out by hand: the colony's errand (DilemmaEffect_CarryOut) to the edge of its ground, where
    /// the silver is left for them. No courier of another people comes for it; the camp breaks once it is handed over.
    /// </summary>
    public class DilemmaEffect_RaidersCampPrice : DilemmaEffect_CarryOut
    {
        /// <summary>Sets the errand going: the plan, or null when nothing was set going (answers don't play out, nobody to carry it).</summary>
        public PlayOut Run(DilemmaContext ctx) => ctx?.map == null ? null : Create(ctx);

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map != null && Run(ctx) == null) PlayCarry.Take(ctx.map, what, ThingDefOf.Silver, count);
        }
    }

    [DefOf]
    public static class RaidersCampDefOf
    {
        public static LetterDef Rimfolk_RaidersCampTerms;

        static RaidersCampDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RaidersCampDefOf));
        }
    }

    /// <summary>
    /// The camp's map made (the colony came to attack it): how many fighters hold it (World/Region/RaidersCamp.cs). Only
    /// looks; with the deeper game hooks off, the region's hour looks instead.
    /// </summary>
    [HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]
    public static class Patch_RaidersCampMapMade
    {
        public static void Postfix(Site __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                RaidersCamps.MapMade(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its map made", e);
            }
        }
    }

    /// <summary>
    /// The colony leaving the camp's map: how many of its fighters still stand, and whether any threat is left, read just
    /// before the game takes the map away (and with it the site, cleared or not). Only looks.
    /// </summary>
    [HarmonyPatch(typeof(Site), nameof(Site.Notify_MyMapAboutToBeRemoved))]
    public static class Patch_RaidersCampMapGone
    {
        public static void Prefix(Site __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                RaidersCamps.MapLeaving(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its map left", e);
            }
        }
    }
}
