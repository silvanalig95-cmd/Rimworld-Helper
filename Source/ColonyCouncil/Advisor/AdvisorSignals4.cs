using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Gentle checks for the quieter parts of colony life. Each runs once an hour per map and only looks at the
    // colonists, their gear and a handful of buildings, so they stay cheap.

    /// <summary>Weeks without fruit or vegetables bring scurvy (Rimfolk's serious illnesses).</summary>
    public class AdvisorWorker_FreshFood : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var tracker = AilmentTracker.Instance;
            if (!AilmentTracker.SeriousOn || !AilmentTracker.DietKnown || tracker == null) yield break;
            var without = Colonists(map).Where(p => p.RaceProps.Humanlike && p.needs?.food != null && !tracker.AteFreshWithin(p, 6f)).ToList();
            if (without.Count == 0) yield break;
            var s = Signal(0.3f + 0.05f * without.Count, "CC_Adv_Fresh_Title".Translate(without.Count),
                "CC_Adv_Fresh_Detail".Translate(string.Join(", ", without.Select(p => p.LabelShort))), 0.5f);
            s.look = new LookTargets(without);
            yield return s;
        }
    }

    /// <summary>Someone with nobody here they'd call a friend, after a fortnight among the others.</summary>
    public class AdvisorWorker_Lonely : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var colonists = Colonists(map);
            if (colonists.Count < 3) yield break;
            var lonely = new List<Pawn>();
            foreach (var p in colonists)
            {
                if (p.relations == null || p.DevelopmentalStage.Baby() || !People.HasInnerLife(p)) continue;
                if (p.story?.traits?.HasTrait(TraitDefOf.Psychopath) == true) continue;
                if ((p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) < GenDate.TicksPerDay * 15) continue;
                if (HeartNeeds.FriendsHere(p, out int others) == 0 && others > 0) lonely.Add(p);
            }
            if (lonely.Count == 0) yield break;
            var s = Signal(0.2f + 0.05f * lonely.Count, "CC_Adv_Lonely_Title".Translate(lonely.Count),
                "CC_Adv_Lonely_Detail".Translate(string.Join(", ", lonely.Select(p => p.LabelShort))), 0.3f);
            s.look = new LookTargets(lonely);
            yield return s;
        }
    }

    /// <summary>A camp on the map that is going hungry or growing tense with the colony.</summary>
    public class AdvisorWorker_Camp : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!CampTracker.Enabled) yield break;
            var camp = CampTracker.Instance?.camp;
            if (camp == null || camp.map != map || camp.phase != CampPhase.Living) yield break;
            var here = camp.members.Where(p => p != null && p.Spawned && !p.Dead).ToList();
            if (here.Count == 0) yield break;
            if (camp.tension >= 40f)
            {
                var t = Signal(camp.tension >= 70f ? 0.45f : 0.25f, "CC_Adv_CampTense_Title".Translate(camp.Label),
                    "CC_Adv_CampTense_Detail".Translate(camp.Label, camp.TensionWord), camp.tension >= 70f ? 0.8f : 0.4f);
                t.look = new LookTargets(here.Take(3));
                t.key = "tense";
                yield return t;
            }
            if (camp.provisions <= here.Count * 2 && here.Any(p => p.needs?.food != null && p.needs.food.CurLevelPercentage < 0.3f))
            {
                var h = Signal(0.18f, "CC_Adv_CampHungry_Title".Translate(camp.Label), "CC_Adv_CampHungry_Detail".Translate(camp.Label), 0.5f);
                h.look = new LookTargets(here.Take(3));
                h.key = "hungry";
                yield return h;
            }
        }
    }

    /// <summary>A day on the colony calendar coming round in the next day or two.</summary>
    public class AdvisorWorker_CalendarSoon : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var cal = CalendarTracker.Instance;
            if (!CalendarTracker.On || cal == null || map != HappeningTracker.HomeMap()) yield break;
            int today = GenLocalDate.DayOfYear(map);
            CalendarDay next = null;
            int soonest = 99;
            foreach (var d in cal.days)
            {
                if (d == null || d.kind == CalendarKind.FirstSnow || d.way == DayWay.Personal) continue;
                int until = CalendarMath.DaysUntil(today, d.day);
                if (until < 1 || until > 2 || until >= soonest) continue;
                // Tomorrow with nothing laid by: the unprepared day's advice says so instead (AdvisorSignals5.cs).
                if (until == 1 && AdvisorWorker_CalendarUnprepared.Lacks(map, d) != null) continue;
                next = d;
                soonest = until;
            }
            if (next == null) yield break;
            string detail = "CC_Adv_CalSoon_Detail".Translate(next.label, CalendarTracker.WayLabel(next.way)).Resolve();
            if (next.tradition) detail += " " + "CC_Adv_CalSoon_Tradition".Translate().Resolve();
            yield return Signal(next.tradition ? 0.3f : 0.15f, "CC_Adv_CalSoon_Title".Translate(next.label.CapitalizeFirst(), soonest), detail, 0.6f);
        }
    }

    /// <summary>The colony's own dead in graves left dirty, or outside the area anyone cleans.</summary>
    public class AdvisorWorker_Graves : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var home = map.areaManager.Home;
            var untended = new List<Thing>();
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
            {
                var dead = g.HasCorpse ? g.Corpse?.InnerPawn : null;
                if (dead == null || !dead.RaceProps.Humanlike || dead.Faction != Faction.OfPlayer) continue;
                bool dirty = false, outside = true;
                foreach (var c in g.OccupiedRect().ExpandedBy(1))
                {
                    if (!c.InBounds(map)) continue;
                    if (home[c]) outside = false;
                    if (!dirty && c.GetFirstThing<Filth>(map) != null) dirty = true;
                }
                if (dirty || outside) untended.Add(g);
            }
            if (untended.Count == 0) yield break;
            var s = Signal(0.12f + 0.02f * untended.Count, "CC_Adv_Graves_Title".Translate(untended.Count), "CC_Adv_Graves_Detail".Translate(), 0.2f);
            s.look = new LookTargets(untended.Take(5));
            yield return s;
        }
    }

    /// <summary>Clothes worn to rags, or taken off the dead: both weigh on the one wearing them.</summary>
    public class AdvisorWorker_Clothes : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var ragged = new List<Pawn>();
            var tainted = new List<Pawn>();
            foreach (var p in Colonists(map))
            {
                var worn = p.apparel?.WornApparel;
                if (worn == null) continue;
                if (worn.Any(a => a.def.useHitPoints && a.MaxHitPoints > 0 && a.HitPoints < a.MaxHitPoints * 0.5f)) ragged.Add(p);
                if (worn.Any(a => a.WornByCorpse)) tainted.Add(p);
            }
            int n = ragged.Union(tainted).Count();
            if (n == 0) yield break;
            var parts = new List<string>();
            if (ragged.Count > 0) parts.Add("CC_Adv_Clothes_Ragged".Translate(string.Join(", ", ragged.Select(p => p.LabelShort))).Resolve());
            if (tainted.Count > 0) parts.Add("CC_Adv_Clothes_Tainted".Translate(string.Join(", ", tainted.Select(p => p.LabelShort))).Resolve());
            var s = Signal(UnityEngine.Mathf.Min(0.35f, 0.12f + 0.04f * n), "CC_Adv_Clothes_Title".Translate(n), string.Join(" ", parts), 0.3f);
            s.look = new LookTargets(ragged.Union(tainted));
            yield return s;
        }
    }

    /// <summary>A schedule with no hour set aside for recreation.</summary>
    public class AdvisorWorker_NoJoyTime : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var none = new List<Pawn>();
            foreach (var p in Colonists(map))
            {
                if (p.timetable == null || p.needs?.joy == null) continue;
                bool any = false;
                for (int h = 0; h < 24 && !any; h++)
                {
                    var a = p.timetable.GetAssignment(h);
                    if (a == TimeAssignmentDefOf.Joy || a == TimeAssignmentDefOf.Anything) any = true;
                }
                if (!any) none.Add(p);
            }
            if (none.Count == 0) yield break;
            var s = Signal(0.2f + 0.04f * none.Count, "CC_Adv_JoyTime_Title".Translate(none.Count),
                "CC_Adv_JoyTime_Detail".Translate(string.Join(", ", none.Select(p => p.LabelShort))), 0.4f);
            s.look = new LookTargets(none);
            yield return s;
        }
    }

    /// <summary>Strain near breaking (feelings and strain on): the slow kind of trouble a bad mood alone doesn't show.</summary>
    public class AdvisorWorker_Strain : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Heart.On) yield break;
            var worn = Colonists(map).Where(p => Heart.Eligible(p) && Heart.BandOf(p) >= StrainBand.Worn).ToList();
            if (worn.Count == 0) yield break;
            int breaking = worn.Count(p => Heart.BandOf(p) == StrainBand.Breaking);
            var s = Signal(0.25f + 0.05f * worn.Count + 0.1f * breaking, "CC_Adv_Strain_Title".Translate(worn.Count),
                "CC_Adv_Strain_Detail".Translate(string.Join(", ", worn.Select(p => p.LabelShort))), breaking > 0 ? 0.8f : 0.5f);
            s.look = new LookTargets(worn);
            yield return s;
        }
    }

    /// <summary>Someone getting over an illness or an operation who is back at work instead of resting.</summary>
    public class AdvisorWorker_Convalescing : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!HealthTracker.ConvalescenceOn) yield break;
            var working = Colonists(map).Where(p => HealthTracker.ConvalescenceOf(p) != null && !p.InBed() && !p.Downed
                                                     && p.CurJob != null && p.CurJob.workGiverDef != null).ToList();
            if (working.Count == 0) yield break;
            var s = Signal(0.18f + 0.04f * working.Count, "CC_Adv_Conval_Title".Translate(working.Count),
                "CC_Adv_Conval_Detail".Translate(string.Join(", ", working.Select(p => p.LabelShort))), 0.4f);
            s.look = new LookTargets(working);
            yield return s;
        }
    }

    /// <summary>A wish about to lapse that nobody has promised anything on: letting it go costs trust.</summary>
    public class AdvisorWorker_WishLapsing : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) yield break;
            var soon = comp.OpenWishes.Where(w => w.pawn != null && w.pawn.Spawned && w.pawn.Map == map && !w.promised
                                                  && w.TicksLeft > 0 && w.TicksLeft < GenDate.TicksPerDay).ToList();
            if (soon.Count == 0) yield break;
            var lines = soon.Take(4).Select(w => "CC_Adv_WishLapse_Line".Translate(w.pawn.LabelShort, w.Label).Resolve());
            var s = Signal(0.2f + 0.05f * soon.Count, "CC_Adv_WishLapse_Title".Translate(soon.Count), "CC_Adv_WishLapse_Detail".Translate() + "\n" + string.Join("\n", lines), 0.9f);
            s.look = new LookTargets(soon.Select(w => (Thing)w.pawn).Distinct());
            yield return s;
        }
    }
}
