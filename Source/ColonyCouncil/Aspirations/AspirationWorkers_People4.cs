using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Two sparks for life goals from what the newest content brings (6 November 2026): a hurt from everyday life lately
    /// (a fall at the dig, a burn stamping out a smoulder, a crush under a tree, an ailment: whatever the harm service
    /// gave them, read from its book), and a war among the neighbours running now (a world event of the war kind that
    /// is bad news). A war is a spark of fighting: one fighting goal from it at a time (GoalMath.FightMaySpark).
    /// </summary>
    public static class GoalSparks
    {
        public const string Hurt = "hurt";
        public const string War = "war";

        /// <summary>A hurt from everyday life in the last <paramref name="days"/> days.</summary>
        public static bool HurtLately(Pawn p, float days) => p != null && Harm.On && Harm.DaysSince(p) < days;

        private static int warHour = -1;
        private static bool war;

        /// <summary>A war among the neighbours is running (looked at once an hour: every goal asks for every colonist).</summary>
        public static bool WarNear()
        {
            if (Current.Game == null || ColonyCouncilMod.Settings?.worldSituations != true) return false;
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (hour != warHour)
            {
                warHour = hour;
                war = AnyWar();
            }
            return war;
        }

        private static bool AnyWar()
        {
            try
            {
                var ws = WorldSituations.Instance;
                if (ws == null) return false;
                foreach (var s in ws.Running)
                    if (s?.def != null && s.def.kind == "war" && s.def.bad)
                        return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("life goals: a war near", e);
            }
            return false;
        }
    }

    /// <summary>
    /// Sure on their feet again: a month without another hurt from everyday life or being downed, for someone hurt in the
    /// last ten days. The clock starts again at each hurt or fall (PeopleFourMath.CleanDays). Only with hurts on.
    /// </summary>
    public class AspirationWorker_SureFooted : AspirationWorker
    {
        private static int LastHurt(Pawn p)
        {
            int t = HarmBook.Instance?.Last(p) ?? HarmMath.Never;
            return t == HarmMath.Never ? -1 : t;
        }

        public override bool CanHave(Pawn p) => Harm.On && Harm.DaysSince(p) < PeopleFourMath.RecentHurtDays;

        public override bool Init(Aspiration a)
        {
            a.target = def.amount > 1f ? def.amount : PeopleFourMath.SureFootedDays;
            return true;
        }

        private static float Days(Aspiration a) =>
            PeopleFourMath.CleanDays(Find.TickManager.TicksGame, a.startedTick, LastHurt(a.pawn), CouncilGameComponent.Instance?.aspirations.When(a.pawn, "downed") ?? -1, GenDate.TicksPerDay);

        public override float Progress(Aspiration a) => PeopleFourMath.Share(Days(a), a.target);

        public override bool StillPossible(Aspiration a) => Harm.On;

        public override string ProgressText(Aspiration a) => "CC_Asp_SureFootedProgress".Translate(Mathf.FloorToInt(Days(a)), a.target.ToString("0"));
    }

    /// <summary>
    /// A new colony grown: their own home (one of several) reaching <see cref="AspirationDef.amount"/> people, two more than
    /// now at least (PeopleFourMath.HomeTarget). The home is kept by its key; living elsewhere since lets it go.
    /// </summary>
    public class AspirationWorker_HomeGrows : AspirationWorker
    {
        private static int cachedKey = HomesMath.None, cachedHour = -1, cachedCount;

        private static int PeopleAt(int key)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (key != cachedKey || hour != cachedHour)
            {
                cachedKey = key;
                cachedHour = hour;
                var map = Homes.MapOf(key);
                cachedCount = map == null ? 0 : Homes.People(map).Count(p => !p.IsQuestLodger());
            }
            return cachedCount;
        }

        public override bool CanHave(Pawn p)
        {
            if (Homes.All.Count < 2) return false;
            var home = Homes.Of(p);
            return home != null && PeopleAt(Homes.KeyOf(home)) < def.amount;
        }

        public override bool Init(Aspiration a)
        {
            var home = Homes.Of(a.pawn);
            if (home == null) return false;
            a.pawnRef = Homes.KeyOf(home);
            a.target = PeopleFourMath.HomeTarget(PeopleAt(a.pawnRef), (int)def.amount);
            return true;
        }

        public override float Progress(Aspiration a) => PeopleAt(a.pawnRef) / Mathf.Max(1f, a.target);

        public override bool StillPossible(Aspiration a) => Homes.MapOf(a.pawnRef) != null && Homes.KeyOf(Homes.Of(a.pawn)) == a.pawnRef;

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate(PeopleAt(a.pawnRef), a.target.ToString("0"));
    }

    /// <summary>
    /// The journey to the other colony: someone with a person they're fond of (opinion 30 or more) at another of the colony's
    /// homes goes there, by caravan, pods or ship, the game's own way. Reached the day they stand on a home of the colony's
    /// that isn't the one they belonged to when they set their heart on it (kept by its key).
    /// </summary>
    public class AspirationWorker_Visit : AspirationWorker
    {
        public override bool CanHave(Pawn p)
        {
            if (Homes.All.Count < 2 || p?.relations == null) return false;
            var home = Homes.Of(p);
            if (home == null) return false;
            foreach (var o in Homes.FreeColonistsAnywhere())
                if (o != p && !o.Dead && Homes.Of(o) != null && Homes.Of(o) != home && p.relations.OpinionOf(o) >= PeopleFourMath.CloseOpinion)
                    return true;
            return false;
        }

        public override bool Init(Aspiration a)
        {
            var home = Homes.Of(a.pawn);
            if (home == null) return false;
            a.pawnRef = Homes.KeyOf(home);
            a.baseline = 0f;
            a.target = 1f;
            return true;
        }

        public override void Daily(Aspiration a)
        {
            var here = a.pawn?.MapHeld;
            if (here != null && Homes.IsHome(here) && PeopleFourMath.Visited(a.pawnRef, Homes.KeyOf(here), HomesMath.None)) a.baseline = 1f;
        }

        public override float Progress(Aspiration a) => a.baseline >= 1f ? 1f : 0f;

        /// <summary>Gone with the homes: one colony left.</summary>
        public override bool StillPossible(Aspiration a) => a.baseline >= 1f || Homes.All.Count >= 2;

        public override string ProgressText(Aspiration a) => (a.baseline >= 1f ? "CC_Asp_VisitDone" : "CC_Asp_VisitNotYet").Translate();
    }

    /// <summary>
    /// A record of the colony's own (Defs/Misc: evenings at camps' fires, neighbours' calls answered, days kept, quarrels
    /// mended, deals struck, rites led), counted only while its feature is on: <see cref="AspirationDef.counter"/> names
    /// it (camps, worldSituations, colonyCalendar, socialWeb, ideology; anything else always). With the feature off, nobody
    /// sets their heart on it, and a goal held is let go.
    /// </summary>
    public class AspirationWorker_FeatureRecord : AspirationWorker_Record
    {
        public static bool FeatureOn(string feature)
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null) return false;
            switch (feature)
            {
                case "camps": return s.camps;
                case "worldSituations": return s.worldSituations;
                case "colonyCalendar": return s.colonyCalendar;
                case "socialWeb": return s.socialWeb;
                case "ideology": return ModsConfig.IdeologyActive;
                default: return true;
            }
        }

        public override bool CanHave(Pawn p) => FeatureOn(def.counter) && base.CanHave(p);

        public override bool StillPossible(Aspiration a) => Progress(a) >= 1f || FeatureOn(def.counter);
    }
}
