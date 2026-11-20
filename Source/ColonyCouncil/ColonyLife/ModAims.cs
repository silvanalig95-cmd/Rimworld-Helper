using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A17: Vanilla Aspirations Expanded's aims, marked by the colony (the content update, the player's go of 20 November
    /// 2026). The mod (active in the player's list, read by name only) gives colonists aims of its own, a fulfilment need
    /// (VAspirE_Fulfillment) and a letter when one is reached (VAspirE_Fulfilled). Rimfolk reads that letter as it comes:
    /// the aim it names (its label in the letter's words, ModAimsMath.Match), and then, as for one of Rimfolk's own life
    /// goals, the colonist's closest friend raises a cup to it at the colony's next gathering (PlayOut/Stage.cs), the
    /// chronicle and the year's annals note it, and the deed "reached a life goal" is told round the colony. Aims the
    /// colony wouldn't toast (to kill someone, to overdose, to eat human meat and the like: ModAimsMath.NotToasted), or one
    /// the letter doesn't name, get a line in their journal only. The heart's need for purpose counts the fulfilment need
    /// (Heart/HeartNeeds.cs), and Rimfolk's own life goals come a third less often to someone who has the mod's aims
    /// (GoalMath.ModAimsFactor). Without the mod none of this is there.
    /// </summary>
    public static class ModAims
    {
        public const string NeedName = "VAspirE_Fulfillment";
        public const string LetterName = "VAspirE_Fulfilled";

        private static NeedDef need;
        private static bool needLooked;

        /// <summary>The mod's fulfilment need, when the mod is loaded.</summary>
        public static NeedDef Need
        {
            get
            {
                if (!needLooked)
                {
                    needLooked = true;
                    need = DefDatabase<NeedDef>.GetNamedSilentFail(NeedName);
                }
                return need;
            }
        }

        /// <summary>Their fulfilment need, or null (no mod, or none for them).</summary>
        public static Need NeedOf(Pawn p)
        {
            var d = Need;
            if (d == null || p?.needs == null) return null;
            try
            {
                return p.needs.TryGetNeed(d);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>They have the mod's aims (its fulfilment need).</summary>
        public static bool HasAims(Pawn p) => NeedOf(p) != null;

        private static List<Def> aims;

        /// <summary>The mod's aims, read once: every def of an "aspiration" kind whose name is the mod's.</summary>
        public static List<Def> All
        {
            get
            {
                if (aims != null) return aims;
                aims = new List<Def>();
                if (Need == null) return aims;
                try
                {
                    foreach (var type in GenDefDatabase.AllDefTypesWithDatabases())
                    {
                        if (type.Name.IndexOf("Aspiration", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        foreach (var d in GenDefDatabase.GetAllDefsInDatabaseForDef(type))
                            if (d?.defName != null && d.defName.StartsWith(ModAimsMath.Prefix, StringComparison.Ordinal) && !d.label.NullOrEmpty()) aims.Add(d);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("Vanilla Aspirations Expanded's aims", e);
                }
                return aims;
            }
        }

        /// <summary>A letter came: the mod's letter that an aim was reached is marked by the colony.</summary>
        public static void Received(Letter let)
        {
            if (let?.def == null || let.def.defName != LetterName || CouncilGameComponent.Instance == null) return;
            var p = PawnOf(let);
            if (p == null || p.Dead || !p.IsColonist) return;
            string text = let.Label.Resolve() + "\n" + (let is ChoiceLetter cl ? cl.Text.Resolve() : "");
            var all = All;
            int i = ModAimsMath.Match(text, all.Select(d => d.label).ToList());
            Def aim = i >= 0 ? all[i] : null;
            Marked(p, aim);
        }

        private static Pawn PawnOf(Letter let)
        {
            var look = let.lookTargets;
            if (look == null) return null;
            if (look.PrimaryTarget.Thing is Pawn p) return p;
            return look.targets?.Select(t => t.Thing).OfType<Pawn>().FirstOrDefault(x => x.RaceProps.Humanlike);
        }

        /// <summary>Their aim reached: a journal line; for an aim the colony would toast, the cup, the chronicle, the annals and the deed.</summary>
        public static void Marked(Pawn p, Def aim)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || p == null) return;
            string label = aim?.label ?? "";
            bool toast = aim != null && ModAimsMath.Toasted(aim.defName);
            string tag = aim == null ? "unknown" : toast ? "toasted" : "private";
            comp.journals.WriteTagged(p, "Rimfolk_Journal_ModAim", new[] { tag }, null, label.Named("AIM"));
            if (!toast) return;
            string deed = "CC_Life_ModAimDeed".Translate(p.Named("PAWN"), label.Named("AIM")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Chronicle_ModAim".Translate(p.Named("PAWN"), label.Named("AIM")).Resolve(), p));
            comp.annals?.Notify_Aspiration(p, label);
            LifeBook.Safe("an aim of their own: the deed", () =>
            {
                var heard = PawnsFinder.AllMaps_FreeColonists.Where(f => f != p && f.relations != null && f.relations.OpinionOf(p) >= 20).ToList();
                Deeds.Record("Rimfolk_Deed_GoalReached", p, null, -1f, null, deed, heard, false);
            });
            LifeBook.Safe("an aim of their own: the cup", () => Toast(p, label));
        }

        /// <summary>The one who thinks best of them raises a cup to them at the colony's next gathering (as for a life goal of Rimfolk's).</summary>
        private static void Toast(Pawn p, string label)
        {
            if (!PlayOuts.On || !p.Spawned || !p.Map.IsPlayerHome) return;
            var friend = p.Map.mapPawns.FreeColonistsSpawned.Where(o => o != p && !o.IsQuestLodger() && People.HasInnerLife(o) && o.relations != null
                                                                         && (o.relations.OpinionOf(p) >= 20 || LovePartnerRelationUtility.LovePartnerRelationExists(o, p)))
                .OrderByDescending(o => o.relations.OpinionOf(p)).FirstOrDefault();
            if (friend == null) return;
            string cause = "CC_Life_ModAimCause".Translate(p.Named("A"), label.Named("AIM")).Resolve();
            // The deed is left to the toast's own words ("said she would, and she did"): the mod's labels are aims, not deeds.
            if (Stage.Cup(p, friend, null, cause))
                Tell.Minor(NewsKind.People, Importance.Flavor, p.Map, "CC_Life_ModAimCup".Translate(friend.Named("RAISER"), p.Named("A"), label.Named("AIM")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>What the fulfilment need adds to the heart's need for purpose, and why, or 0 with no mod.</summary>
        public static float Purpose(Pawn p)
        {
            var n = NeedOf(p);
            return n == null ? 0f : ModAimsMath.Purpose(n.CurLevelPercentage);
        }
    }

    /// <summary>Vanilla Aspirations Expanded's letter that an aim was reached, read as it comes (A17, <see cref="ModAims"/>).</summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_ModAimLetter
    {
        public static void Postfix(Letter let)
        {
            try
            {
                if (let?.def != null && let.def.defName == ModAims.LetterName) ModAims.Received(let);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("an aim of their own", e);
            }
        }
    }
}
