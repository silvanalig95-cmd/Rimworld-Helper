using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Three ambitions of the colony's people (A15 of docs/content-update-proposals.md, the player's go of 20 November
    // 2026; Defs/AmbitionDefs/Rimfolk_Ambitions_LifeVariety.xml): nobody left out, a season of kept promises, a lighter
    // season. Each is pitched only when it answers something on the map, and counts only what the colony does while it runs.

    /// <summary>
    /// Nobody left out: every grown colonist at home belongs to a group or has a friend. Pitched when two or more stand
    /// outside every group with no friend (groups on); met when nobody does, in stages as each outsider finds a place.
    /// </summary>
    public class Goal_NobodyOutside : AmbitionGoal
    {
        public static void Count(Map map, out int grown, out int outside)
        {
            grown = outside = 0;
            if (map == null) return;
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p)).ToList();
            var social = CouncilGameComponent.Instance?.social;
            foreach (var p in people)
            {
                grown++;
                bool inGroup = Groups.Of(p).Count > 0;
                if (inGroup) continue;
                int bestOpinion = -100;
                float bestTie = 0f;
                foreach (var o in people)
                {
                    if (o == p || p.relations == null || o.relations == null) continue;
                    bestOpinion = Mathf.Max(bestOpinion, Mathf.Min(p.relations.OpinionOf(o), o.relations.OpinionOf(p)));
                    if (social != null) bestTie = Mathf.Max(bestTie, social.Strength(p, o, TieKind.Friends));
                }
                if (ColonyAmbitionMath.Outside(false, ColonyAmbitionMath.HasFriend(bestOpinion, bestTie))) outside++;
            }
        }

        public override bool Possible(Map map)
        {
            if (!GroupLife.Active) return false;
            Count(map, out _, out int outside);
            return outside >= ColonyAmbitionMath.OutsideToPropose;
        }

        public override float Progress(Map map, float baseline)
        {
            Count(map, out int grown, out int outside);
            return ColonyAmbitionMath.NobodyOutside(grown, outside);
        }

        public override string Describe(Map map, float baseline)
        {
            Count(map, out int grown, out int outside);
            return "CC_Goal_NobodyOutside".Translate(grown - outside, grown);
        }
    }

    /// <summary>
    /// A season of kept promises: fifteen days running with no promised wish let lapse, and two promises kept along the
    /// way. Pitched after a promise was broken (lately: someone still minds it). The streak's start is the baseline; the
    /// colony's count of promises kept and broken when it began is kept in the book (LifeVarietyBook), so a load keeps it.
    /// </summary>
    public class Goal_KeptPromises : AmbitionGoal
    {
        private static void Totals(out int kept, out int broken)
        {
            kept = broken = 0;
            var st = CouncilGameComponent.Instance?.standings?.standings;
            if (st == null) return;
            foreach (var s in st)
            {
                if (s == null) continue;
                kept += s.promisesKept;
                broken += s.promisesBroken;
            }
        }

        public override bool Possible(Map map) =>
            ColonyCouncilMod.Settings.enableWishes && map.mapPawns.FreeColonistsSpawned.Any(p => p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(CouncilDefOf.Council_BrokenPromise) != null);

        public override float Baseline(Map map)
        {
            var book = LifeVarietyBook.Instance;
            Totals(out int kept, out int broken);
            if (book != null)
            {
                book.keptAtStart = kept;
                book.brokenSeen = broken;
            }
            return Find.TickManager.TicksGame;
        }

        public override float Update(Map map, float baseline)
        {
            var book = LifeVarietyBook.Instance;
            if (book == null) return baseline;
            Totals(out int kept, out int broken);
            if (book.brokenSeen < 0 || book.keptAtStart < 0)
            {
                book.brokenSeen = broken;
                book.keptAtStart = kept;
                return baseline;
            }
            if (ColonyAmbitionMath.StreakBroken(broken, book.brokenSeen))
            {
                // A promise let lapse: the season starts again from today.
                book.brokenSeen = broken;
                book.keptAtStart = kept;
                return Find.TickManager.TicksGame;
            }
            book.brokenSeen = broken;
            return baseline;
        }

        private static int KeptSince()
        {
            var book = LifeVarietyBook.Instance;
            if (book == null || book.keptAtStart < 0) return 0;
            Totals(out int kept, out _);
            return Mathf.Max(0, kept - book.keptAtStart);
        }

        private static float Days(float baseline) => Mathf.Max(0f, (Find.TickManager.TicksGame - baseline) / GenDate.TicksPerDay);

        public override float Progress(Map map, float baseline) => ColonyAmbitionMath.KeptProgress(Days(baseline), KeptSince());

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_KeptPromises".Translate(ColonyAmbitionMath.PromiseDays.ToString("0"), ColonyAmbitionMath.PromisesKept, Days(baseline).ToString("0.#"), KeptSince());
    }

    /// <summary>
    /// A lighter season: fifteen days running with nobody at home worn down or near breaking (the heart's strain of 55 or
    /// more; feelings on). Pitched when someone is worn down. A calm season counts mental breaks; this one counts the heart.
    /// </summary>
    public class Goal_LighterSeason : AmbitionGoal
    {
        private static bool AnyWorn(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Any(p => !p.IsQuestLodger() && ColonyAmbitionMath.Worn(Heart.StrainOf(p)));

        public override bool Possible(Map map) => Heart.On && RoomUtil.ColonistCount(map) >= 2 && AnyWorn(map);

        public override float Baseline(Map map) => Find.TickManager.TicksGame;

        public override float Update(Map map, float baseline) => Heart.On && AnyWorn(map) ? Find.TickManager.TicksGame : baseline;

        private static float Days(float baseline) => Mathf.Max(0f, (Find.TickManager.TicksGame - baseline) / GenDate.TicksPerDay);

        public override float Progress(Map map, float baseline) => Mathf.Clamp01(Days(baseline) / ColonyAmbitionMath.LighterDays);

        public override string Describe(Map map, float baseline) =>
            "CC_Goal_LighterSeason".Translate(ColonyAmbitionMath.LighterDays.ToString("0"), Days(baseline).ToString("0.#"),
                map.mapPawns.FreeColonistsSpawned.Count(p => ColonyAmbitionMath.Worn(Heart.StrainOf(p))));
    }
}
