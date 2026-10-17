using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ---------------- The newer systems, and the low-tech winter ----------------
    //
    // Checks for what Rimfolk's own systems leave waiting on the player (a mission running out, a world event hurting the
    // colony, a camp's question, a decree that costs more than it gives, an office empty when it matters, a promise about
    // to break, a calendar day with nothing laid by), and for what a tribal or medieval colony must do before the cold
    // (a roof over the stores, food that keeps, wood for the fires), and two everyday ones: the only doctor, and people
    // eating without a table. Each says why, what helps, and leads to the page, the letter or the plan that deals with
    // it. They run once an hour with the rest of the advisor and read only what the game and Rimfolk keep anyway; the
    // thresholds are in AdvisorMath.cs.

    /// <summary>A mission the colony took on, near its deadline with its goods not all there.</summary>
    public class AdvisorWorker_MissionDue : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (map != HappeningTracker.HomeMap()) yield break;
            int now = Find.TickManager.TicksGame;
            foreach (var m in Missions.All.ToList())
            {
                if (m == null || m.state != MissionState.Active || m.deadline <= 0) continue;
                int left = m.deadline - now;
                bool hasGoals = !m.goals.NullOrEmpty();
                bool met = hasGoals && m.goals.All(g => g.have >= g.count);
                int total = m.started > 0 ? m.deadline - m.started : -1;
                // A mission with nothing to deliver (an escort, word to carry) only in its last day.
                if (hasGoals ? !AdvisorMath.MissionPressing(left, total, met) : left <= 0 || left > GenDate.TicksPerDay) continue;
                string title = m.title ?? m.def?.LabelCap.ToString() ?? "?";
                string detail;
                if (hasGoals)
                {
                    var short_ = m.goals.Where(g => g.have < g.count).Select(g => "CC_Adv_MissionDue_Goal".Translate(Missions.GoalLabel(g), g.have, g.count).Resolve()).ToList();
                    detail = "CC_Adv_MissionDue_Detail".Translate(TextUtil.JoinAnd(short_), TextUtil.DaysString(left)).Resolve();
                }
                else
                {
                    string status = null;
                    try
                    {
                        status = Missions.Worker(m)?.Status(m);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("advisor mission status", e);
                    }
                    detail = "CC_Adv_MissionDue_DetailNoGoods".Translate(TextUtil.DaysString(left)).Resolve() + (status.NullOrEmpty() ? "" : " " + status);
                }
                var s = Signal(left <= GenDate.TicksPerDay ? 0.5f : 0.38f, "CC_Adv_MissionDue_Title".Translate(title, TextUtil.DaysString(left)).Resolve(), detail, 0.9f);
                s.page = "World";
                var mission = m;
                if (Missions.QuestOf(m) != null)
                {
                    s.act = () => Missions.ShowQuest(mission);
                    s.actLabel = "CC_Adv_Act_ShowQuest".Translate();
                }
                s.key = "mission:" + m.id;
                yield return s;
            }
        }
    }

    /// <summary>A plague or a famine in the region, while the colony itself is short of what gets it through.</summary>
    public class AdvisorWorker_WorldTrouble : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || map != HappeningTracker.HomeMap()) yield break;
            var colonists = Colonists(map);
            if (colonists.Count == 0) yield break;
            foreach (var sit in ws.Running.ToList())
            {
                if (sit?.def == null || !sit.def.bad) continue;
                AdvisorSignal s = null;
                switch (sit.def.kind)
                {
                    case "sickness":
                        s = Sickness(map, colonists, sit);
                        break;
                    case "hunger":
                        s = Hunger(map, sit);
                        break;
                }
                if (s == null) continue;
                s.page = "World";
                s.key = "world:" + sit.id;
                yield return s;
            }
        }

        /// <summary>Medicine, a bed for the sick, someone to tend them: what's missing of the three.</summary>
        private AdvisorSignal Sickness(Map map, List<Pawn> colonists, WorldSituation sit)
        {
            // The herds' sickness is the animals' business, not the colony's medicine.
            if (sit.def.defName == "Council_WS_Murrain") return null;
            var lacks = new List<string>();
            int meds = MapFacts.Of(map).Medicine;
            if (meds < colonists.Count * 2) lacks.Add("CC_Adv_World_Sick_Meds".Translate(meds).Resolve());
            bool noBed = colonists.Count >= 3 && !Avail.ColonyBuildings(map, d => d.IsBed).OfType<Building_Bed>().Any(b => b.Medical);
            if (noBed) lacks.Add("CC_Adv_World_Sick_Bed".Translate().Resolve());
            if (!colonists.Any(p => p.workSettings != null && WorkTypeDefOf.Doctor != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)))
                lacks.Add("CC_Adv_World_Sick_Doctor".Translate().Resolve());
            if (lacks.Count == 0) return null;
            int ill = colonists.Count(WishWorker_Sickbed.Ailing);
            var s = Signal(0.3f + 0.08f * lacks.Count + (ill > 0 ? 0.1f : 0f), "CC_Adv_World_Title".Translate(sit.Label).Resolve(),
                "CC_Adv_World_SickDetail".Translate(TextUtil.JoinAnd(lacks), TextUtil.DaysString(sit.TicksLeft)).Resolve(), ill > 0 ? 0.8f : 0.5f);
            s.template = noBed ? DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Hospital") : null;
            return s;
        }

        /// <summary>Food for under three weeks while the region goes hungry: trade won't fill the stores.</summary>
        private AdvisorSignal Hunger(Map map, WorldSituation sit)
        {
            float days = AdvisorWorker_Food.DaysOfFood(map);
            if (days >= 20f) return null;
            bool locusts = sit.def.defName == "Council_WS_Locusts" && !sit.Has("struck");
            string help = locusts ? "CC_Adv_World_HungerLocusts".Translate().Resolve() : "CC_Adv_World_HungerHelp".Translate().Resolve();
            return Signal(days < 8f ? 0.55f : 0.35f, "CC_Adv_World_Title".Translate(sit.Label).Resolve(),
                "CC_Adv_World_HungerDetail".Translate(days.ToString("0"), help).Resolve(), days < 8f ? 0.8f : 0.5f);
        }
    }

    /// <summary>The camp's question has waited a day, or its time to answer is nearly up.</summary>
    public class AdvisorWorker_CampWaiting : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!CampTracker.Enabled) yield break;
            var camp = CampTracker.Instance?.camp;
            if (camp == null || camp.map != map) yield break;
            int now = Find.TickManager.TicksGame;
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Camp>().ToList())
            {
                if (l.resolved || l.campId != camp.id) continue;
                int left = l.TimeoutActive ? l.disappearAtTick - now : -1;
                bool closing = left > 0 && left < GenDate.TicksPerDay;
                bool waited = !l.TimeoutActive && now - l.arrivalTick > GenDate.TicksPerDay;
                if (!closing && !waited) continue;
                string detail = closing
                    ? "CC_Adv_CampWaiting_Closing".Translate(camp.Label, l.Label.ToString(), TextUtil.DaysString(left)).Resolve()
                    : "CC_Adv_CampWaiting_Waited".Translate(camp.Label, l.Label.ToString()).Resolve();
                var s = Signal(closing ? 0.32f : 0.2f, "CC_Adv_CampWaiting_Title".Translate(camp.Label).Resolve(), detail, closing ? 0.9f : 0.4f);
                var letter = l;
                s.act = () => letter.OpenLetter();
                s.actLabel = "CC_Adv_Act_OpenLetter".Translate();
                var here = camp.members.Where(p => p != null && p.Spawned && !p.Dead).Take(3).ToList();
                if (here.Count > 0) s.look = new LookTargets(here);
                s.key = "camp-letter:" + l.ID;
                yield return s;
            }
        }
    }

    /// <summary>A decree in force that the colony's moods say costs more than it gives.</summary>
    public class AdvisorWorker_DecreeCost : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            var settings = ColonyCouncilMod.Settings;
            if (comp == null || settings == null || !settings.MoodEffects || map != HappeningTracker.HomeMap()) yield break;
            var people = Colonists(map).Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && p.IsFreeNonSlaveColonist).ToList();
            if (people.Count < 2) yield break;
            int now = Find.TickManager.TicksGame;
            foreach (var a in comp.decrees.active.ToList())
            {
                var stages = a?.def?.thought?.stages;
                if (stages == null || stages.Count < 4 || now - a.startTick < GenDate.TicksPerDay) continue;
                // What it was enacted for (the moments it's asked for at): with none named, its purpose can't be judged here.
                var reasons = (a.def.requestWhen ?? new List<DecreeMoment>()).Concat(a.def.orWhen ?? new List<DecreeMoment>()).Distinct().ToList();
                if (reasons.Count == 0) continue;
                bool holds = reasons.Any(m => DecreeTracker.MomentNow(m, map, people, a.def));
                float good = 0f, bad = 0f;
                int against = 0, forIt = 0;
                float badScale = settings.ChosenPenaltyScale * (1f + 0.35f * a.fatigue);
                foreach (var p in people)
                {
                    int stage;
                    switch (a.def.ReactionOf(p))
                    {
                        case DecreeReaction.Hate: stage = 0; break;
                        case DecreeReaction.Dislike: stage = 1; break;
                        case DecreeReaction.Like: stage = 2; break;
                        case DecreeReaction.Love: stage = 3; break;
                        default: continue;
                    }
                    float mood = stages[stage]?.baseMoodEffect ?? 0f;
                    if (mood < 0f)
                    {
                        bad += mood * badScale;
                        against++;
                    }
                    else if (mood > 0f)
                    {
                        good += mood;
                        forIt++;
                    }
                }
                if (!AdvisorMath.DecreeCostsMore(good, bad, against, forIt, holds)) continue;
                float net = good + bad;
                var s = Signal(Mathf.Min(0.5f, 0.2f + 0.02f * -net), "CC_Adv_DecreeCost_Title".Translate(a.def.LabelCap.ToString()).Resolve(),
                    "CC_Adv_DecreeCost_Detail".Translate(against, forIt, net.ToString("0"), TextUtil.DaysString(Mathf.Max(0, a.endTick - now))).Resolve()
                    + (a.fatigue > 0 ? " " + "CC_Adv_DecreeCost_Again".Translate().Resolve() : ""), 0.4f);
                s.page = "Decrees";
                s.look = new LookTargets(people.Where(p => a.def.ReactionOf(p) <= DecreeReaction.Dislike).Take(6));
                s.key = "decree:" + a.def.defName;
                yield return s;
            }
        }
    }

    /// <summary>An office that matters right now stands empty: no physician with the sick in bed, no reeve at harvest, no keeper before a day.</summary>
    public class AdvisorWorker_OfficeEmpty : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.offices || map != HappeningTracker.HomeMap()) yield break;
            var colonists = Colonists(map);
            int voters = PawnsFinder.AllMaps_FreeColonistsSpawned.Count(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave && CouncilGameComponent.CanVoiceWishes(p));

            // The physician, with someone ill or hurt and needing care.
            var sick = colonists.Where(p => WishWorker_Sickbed.Ailing(p) || HealthAIUtility.ShouldBeTendedNowByPlayer(p)).ToList();
            if (sick.Count > 0)
            {
                var s = Empty(comp, colonists, voters, Interplay.Physician, "CC_Adv_Office_Physician".Translate(TextUtil.JoinAnd(sick.Select(p => p.LabelShort).ToList())).Resolve(),
                    0.3f + 0.05f * Mathf.Min(4, sick.Count));
                if (s != null) yield return s;
            }

            // The reeve, with the harvest coming in.
            if (SeasonUtil.HasSeasons(map) && GenLocalDate.Season(map) == Season.Fall && map.zoneManager.AllZones.OfType<Zone_Growing>().Sum(z => z.CellCount) >= 40)
            {
                var s = Empty(comp, colonists, voters, Interplay.Reeve, "CC_Adv_Office_Reeve".Translate().Resolve(), 0.16f);
                if (s != null) yield return s;
            }

            // The keeper of days, with a day on the calendar within three days.
            var cal = CalendarTracker.Instance;
            if (CalendarTracker.On && cal != null)
            {
                int today = GenLocalDate.DayOfYear(map);
                var day = cal.days.Where(d => d != null && d.way != DayWay.Personal && d.kind != CalendarKind.FirstSnow)
                    .Select(d => (d, until: CalendarMath.DaysUntil(today, d.day))).Where(x => x.until >= 1 && x.until <= 3).OrderBy(x => x.until).FirstOrDefault();
                if (day.d != null)
                {
                    var s = Empty(comp, colonists, voters, Interplay.Keeper, "CC_Adv_Office_Keeper".Translate(day.d.label).Resolve(), 0.14f);
                    if (s != null) yield return s;
                }
            }
        }

        /// <summary>The advice for one office, if it's open in this colony, empty, and someone here could hold it.</summary>
        private AdvisorSignal Empty(CouncilGameComponent comp, List<Pawn> colonists, int voters, string defName, string why, float severity)
        {
            var office = DefDatabase<OfficeDef>.GetNamedSilentFail(defName);
            if (office == null || !office.OpenFor(voters) || comp.offices.HolderOf(office) != null) return null;
            var best = colonists.Where(p => !p.DevelopmentalStage.Juvenile() && !p.IsSlave && office.CanHold(p)).OrderByDescending(office.Competence).FirstOrDefault();
            if (best == null) return null;
            var s = Signal(severity, "CC_Adv_Office_Title".Translate(office.label).Resolve(),
                why + " " + "CC_Adv_Office_Best".Translate(office.label, best.LabelShort, office.Competence(best).ToString("0")).Resolve(), 0.5f);
            s.page = "Decrees";
            s.look = new LookTargets(best);
            s.key = "office:" + defName;
            return s;
        }
    }

    /// <summary>A calendar day tomorrow, kept a way that needs something laid by, and nothing is.</summary>
    public class AdvisorWorker_CalendarUnprepared : AdvisorSignalWorker
    {
        /// <summary>What the day still lacks (a key for the text), or null: the soon-day advice leaves it to this one when it does.</summary>
        public static string Lacks(Map map, CalendarDay d)
        {
            if (map == null || d == null) return null;
            var home = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            int meals = d.way == DayWay.Feast ? AdvisorFacts.Meals(map) : 0;
            int hunters = d.way == DayWay.Hunt
                ? home.Count(p => !p.DevelopmentalStage.Juvenile() && WorkTypeDefOf.Hunting != null && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting)
                                  && !p.WorkTagIsDisabled(WorkTags.Violent) && (p.equipment?.Primary?.def.IsRangedWeapon ?? false))
                : 0;
            bool fire = d.way == DayWay.Bonfire && Avail.ColonyHas(map, RoomTemplateDef.RolePredicate("campfire"));
            int wood = d.way == DayWay.Bonfire ? Goal_Woodpile.Firewood(map) : 0;
            return AdvisorMath.CalendarLacks(d.way.ToString(), meals, home.Count, hunters, fire, wood);
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var cal = CalendarTracker.Instance;
            if (!CalendarTracker.On || cal == null || map != HappeningTracker.HomeMap()) yield break;
            int today = GenLocalDate.DayOfYear(map);
            foreach (var d in cal.days.ToList())
            {
                if (d == null || d.way == DayWay.Personal || d.kind == CalendarKind.FirstSnow || CalendarMath.DaysUntil(today, d.day) != 1) continue;
                string lacks = Lacks(map, d);
                if (lacks == null) continue;
                string what;
                switch (lacks)
                {
                    case "Feast":
                        what = "CC_Adv_CalPrep_Feast".Translate().Resolve();
                        break;
                    case "Hunt":
                        what = "CC_Adv_CalPrep_Hunt".Translate().Resolve();
                        break;
                    default:
                        what = "CC_Adv_CalPrep_Bonfire".Translate().Resolve();
                        break;
                }
                var s = Signal(d.tradition ? 0.3f : 0.18f, "CC_Adv_CalPrep_Title".Translate(d.label.CapitalizeFirst()).Resolve(),
                    "CC_Adv_CalPrep_Detail".Translate(d.label, CalendarTracker.WayLabel(d.way), what).Resolve(), 0.7f);
                s.page = "Calendar";
                s.key = "calendar-prep:" + d.id;
                yield return s;
            }
        }
    }

    /// <summary>A promise the council made, with less than a day before it's broken.</summary>
    public class AdvisorWorker_PromiseBreaking : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) yield break;
            var soon = comp.OpenWishes.Where(w => w.promised && w.pawn != null && w.pawn.MapHeld == map && w.TicksLeft > 0 && w.TicksLeft < GenDate.TicksPerDay).ToList();
            if (soon.Count == 0) yield break;
            var lines = soon.Take(4).Select(w => "CC_Adv_WishLapse_Line".Translate(w.pawn.LabelShort, w.Label).Resolve());
            var s = Signal(0.35f + 0.08f * soon.Count, "CC_Adv_PromiseBreaking_Title".Translate(soon.Count).Resolve(),
                "CC_Adv_PromiseBreaking_Detail".Translate().Resolve() + "\n" + string.Join("\n", lines), 1f);
            s.look = new LookTargets(soon.Select(w => (Thing)w.pawn).Where(p => p.Spawned).Distinct());
            s.page = "Wishes";
            yield return s;
        }
    }

    /// <summary>A tribal or medieval colony whose stockpiles stand under the open sky with winter coming.</summary>
    public class AdvisorWorker_StorageRoof : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Avail.IsPreIndustrial || !SeasonUtil.HasSeasons(map)) yield break;
            SeasonUtil.Winter(map, out float until, out float length, out bool now);
            if (until < 0f || (!now && until > 15f) || length < 5f) yield break;
            int stacks = 0;
            Thing example = null;
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Stockpile)) continue;
                foreach (var c in zone.Cells)
                {
                    if (c.Roofed(map)) continue;
                    var things = c.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        var t = things[i];
                        if (t.def.category != ThingCategory.Item || !t.def.useHitPoints || t.IsForbidden(Faction.OfPlayer)) continue;
                        if (SteadyEnvironmentEffects.FinalDeteriorationRate(t) <= 0f) continue;
                        stacks++;
                        example ??= t;
                    }
                }
            }
            if (stacks < 8) yield break;
            string when = now ? "CC_Adv_StorageRoof_Now".Translate().Resolve() : "CC_Adv_StorageRoof_Soon".Translate(Mathf.Max(1, Mathf.RoundToInt(until))).Resolve();
            var s = Signal(0.22f + Mathf.Min(0.2f, stacks / 120f), "CC_Adv_StorageRoof_Title".Translate(stacks).Resolve(),
                "CC_Adv_StorageRoof_Detail".Translate(when).Resolve(), 0.5f);
            if (example != null) s.look = new LookTargets(example);
            s.template = DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Storeroom");
            s.key = "storage-roof";
            yield return s;
        }
    }

    /// <summary>
    /// A tribal or medieval colony heading into a winter that won't freeze its stores, with food that spoils and little
    /// that keeps: smoke it, or make pemmican.
    /// </summary>
    public class AdvisorWorker_WinterStores : AdvisorSignalWorker
    {
        private static readonly string[] SmokeWords = { "smokehouse", "smoker", "drying rack", "dryingrack" };

        public static bool IsSmokehouse(ThingDef d)
        {
            if (d?.building == null) return false;
            string n = (d.defName + " " + d.label).ToLowerInvariant();
            return SmokeWords.Any(n.Contains);
        }

        /// <summary>Whether the advice applies right now (the spoiling advice leaves it to this one when it does).</summary>
        public static bool Applies(Map map) => Assess(map, out _, out _, out _);

        /// <summary>Days of food that keeps and that spoils, and the winter's length, when the advice applies.</summary>
        private static bool Assess(Map map, out float keeps, out float spoils, out float winter)
        {
            keeps = spoils = winter = 0f;
            if (map == null || !Avail.IsPreIndustrial || !SeasonUtil.HasSeasons(map) || RoomUtil.HasFreezer(map)) return false;
            SeasonUtil.Winter(map, out float until, out float length, out bool now);
            if (until < 0f || (!now && until > 20f)) return false;
            // A winter cold enough freezes whatever is stored out of the warm: nothing to preserve then.
            if (SeasonUtil.MinTempNextTwelfths(map, Mathf.Clamp(Mathf.CeilToInt((until + length) / GenDate.DaysPerTwelfth), 1, 6)) < -5f) return false;
            float eaters = map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;
            if (eaters <= 0f) return false;
            winter = length;
            float keepN = 0f, spoilN = 0f;
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (t is Corpse || t.def.category != ThingCategory.Item || t.def.ingestible == null || !t.def.ingestible.HumanEdible) continue;
                if (!t.Spawned || t.IsForbidden(Faction.OfPlayer) || !(t.IsInAnyStorage() || home[t.Position])) continue;
                float n = t.def.GetStatValueAbstract(StatDefOf.Nutrition) * t.stackCount;
                var rot = t.TryGetComp<CompRottable>();
                if (rot != null && rot.PropsRot.daysToRotStart < 30f) spoilN += n;
                else keepN += n;
            }
            keeps = keepN / (eaters * 1.6f);
            spoils = spoilN / (eaters * 1.6f);
            return AdvisorMath.StoresWontKeep(keeps, spoils, winter) && (Pemmican(map) != null || SmokehouseHere(map) || Avail.AnyBuildable(map, IsSmokehouse));
        }

        private static bool SmokehouseHere(Map map) => Avail.ColonyHas(map, IsSmokehouse);

        /// <summary>Pemmican, if the colony can make it now.</summary>
        private static ThingDef Pemmican(Map map)
        {
            var pemmican = DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican");
            if (pemmican == null) return null;
            return DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef == pemmican && r.AvailableNow
                                                                         && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)))
                ? pemmican
                : null;
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Assess(map, out float keeps, out float spoils, out float winter)) yield break;
            var pemmican = Pemmican(map);
            bool smokehouse = SmokehouseHere(map);
            var build = smokehouse ? null : Avail.BestBuildable(map, IsSmokehouse);
            var ways = new List<string>();
            if (smokehouse) ways.Add("CC_Adv_WinterStores_Smoke".Translate().Resolve());
            else if (build != null) ways.Add("CC_Adv_WinterStores_Build".Translate(Avail.Indefinite(build)).Resolve());
            if (pemmican != null) ways.Add("CC_Adv_WinterStores_Pemmican".Translate(pemmican.label).Resolve());
            var s = Signal(0.3f + Mathf.Clamp01((winter * 0.5f - keeps) / 30f) * 0.25f, "CC_Adv_WinterStores_Title".Translate().Resolve(),
                "CC_Adv_WinterStores_Detail".Translate(spoils.ToString("0"), keeps.ToString("0"), winter.ToString("0"), TextUtil.JoinAnd(ways)).Resolve(), 0.6f);
            s.suggestThing = build ?? pemmican;
            s.key = "winter-stores";
            yield return s;
        }
    }

    /// <summary>The colony's fires (hearths, campfires, torches) will burn more wood before spring than there is.</summary>
    public class AdvisorWorker_Firewood : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!SeasonUtil.HasSeasons(map) || !Goal_Woodpile.ColdComing(map)) yield break;
            SeasonUtil.Winter(map, out float until, out float length, out bool now);
            if (until < 0f || (!now && until > 20f)) yield break;
            var woods = Goal_Woodpile.FirewoodDefs.ToList();
            float perDay = 0f;
            int burners = 0;
            Thing example = null;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b.def.IsFrame) continue;
                var r = b.TryGetComp<CompRefuelable>();
                if (r?.Props?.fuelFilter == null || r.Props.consumeFuelOnlyWhenUsed || r.Props.consumeFuelOnlyWhenPowered || r.Props.fuelConsumptionRate <= 0f) continue;
                if (!woods.Any(r.Props.fuelFilter.Allows)) continue;
                perDay += r.Props.fuelConsumptionRate;
                burners++;
                example ??= b;
            }
            if (burners == 0) yield break;
            int needed = AdvisorMath.FirewoodNeeded(perDay, (now ? 0f : until) + length);
            int have = Goal_Woodpile.Firewood(map);
            if (!AdvisorMath.FirewoodShort(have, needed)) yield break;
            var s = Signal(0.3f + Mathf.Clamp01(1f - have / (float)needed) * 0.3f, "CC_Adv_Firewood_Title".Translate().Resolve(),
                "CC_Adv_Firewood_Detail".Translate(burners, needed, have).Resolve(), now ? 0.8f : 0.5f);
            if (example != null) s.look = new LookTargets(example);
            s.suggestThing = ThingDefOf.WoodLog;
            s.key = "firewood";
            yield return s;
        }
    }

    /// <summary>One doctor for the whole colony: when they're the one hurt, nobody tends them.</summary>
    public class AdvisorWorker_DoctorBackup : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var doctor = WorkTypeDefOf.Doctor;
            if (doctor == null) yield break;
            var colonists = Colonists(map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            var doctors = colonists.Where(p => p.workSettings != null && p.workSettings.WorkIsActive(doctor)).ToList();
            if (!AdvisorMath.NeedsBackupDoctor(doctors.Count, colonists.Count)) yield break;
            var only = doctors[0];
            var next = colonists.Where(p => p != only && !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(doctor))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
            string detail = next != null
                ? "CC_Adv_DoctorBackup_Detail".Translate(only.LabelShort, next.LabelShort, next.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).Resolve()
                : "CC_Adv_DoctorBackup_DetailNobody".Translate(only.LabelShort).Resolve();
            var s = Signal(next != null ? 0.3f : 0.2f, "CC_Adv_DoctorBackup_Title".Translate(only.LabelShort).Resolve(), detail, 0.4f);
            s.look = next != null ? new LookTargets(new[] { only, next }) : new LookTargets(only);
            var work = DefDatabase<MainButtonDef>.GetNamedSilentFail("Work");
            if (work != null && next != null)
            {
                s.act = () => Find.MainTabsRoot.SetCurrentTab(work);
                s.actLabel = "CC_Adv_Act_WorkTab".Translate();
            }
            s.key = "doctor-backup";
            yield return s;
        }
    }

    /// <summary>
    /// Beds in small and medium bedrooms and barracks that no light reaches. Offers to plan wall lamps there (or a
    /// standing light where none can be hung), wherever they light what's dark.
    /// </summary>
    public class AdvisorWorker_BedsDark : AdvisorSignalWorker
    {
        /// <summary>A room this advice speaks for, so the plain darkness advice leaves it alone.</summary>
        public static bool Covers(Room room) =>
            room != null && (room.Role == RoomRoleDefOf.Bedroom || room.Role == RoomRoleDefOf.Barracks) && room.CellCount <= LightMath.MediumRoomMaxCells;

        /// <summary>A bed for people in the room that no light (built, framed or planned) reaches.</summary>
        private static bool HasDarkBed(Room room)
        {
            var lights = RoomLights.In(room);
            foreach (var bed in room.ContainedBeds)
                if (!bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && !AdvisorMath.Lit(bed.Position.x, bed.Position.z, lights)) return true;
            return false;
        }

        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var light = WallLights.ForRoom(map, LightMath.SmallRoomMaxCells);
            if (light == null) yield break;
            var rooms = new List<Room>();
            var owners = new List<Pawn>();
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (!Covers(room) || room.OpenRoofCount > 0) continue;
                var lights = RoomLights.In(room);
                bool dark = false;
                foreach (var bed in room.ContainedBeds)
                {
                    if (bed.Medical || bed.ForPrisoners || !bed.def.building.bed_humanlike) continue;
                    if (AdvisorMath.Lit(bed.Position.x, bed.Position.z, lights)) continue;
                    dark = true;
                    foreach (var o in bed.OwnersForReading)
                        if (o != null && !owners.Contains(o)) owners.Add(o);
                }
                if (dark) rooms.Add(room);
            }
            if (rooms.Count == 0) yield break;
            string who = owners.Count > 0 ? " " + "CC_Adv_BedsDark_Who".Translate(TextUtil.JoinAnd(owners.Take(6).Select(p => p.LabelShort).ToList())).Resolve() : "";
            var s = Signal(0.18f + Mathf.Min(0.15f, 0.04f * rooms.Count), "CC_Adv_BedsDark_Title".Translate(rooms.Count).Resolve(),
                (WallLights.IsAttached(light) ? "CC_Adv_BedsDark_DetailWall" : "CC_Adv_BedsDark_Detail").Translate(Avail.Indefinite(light)).Resolve() + who, 0.3f);
            s.look = new LookTargets(rooms.Take(5).Select(r => new TargetInfo(r.Cells.First(), map)));
            s.suggestThing = light;
            var list = rooms.Take(8).ToList();
            // Only rooms still dark when the button is pressed: a second press (the advice stays until the next look)
            // plans nothing more where the first one's lights already reach the beds.
            s.act = () => RoomLights.Plan(list.Where(r => r != null && !r.Dereferenced && HasDarkBed(r)).ToList(), map);
            s.actLabel = "CC_Adv_Act_PlanLamps".Translate();
            s.key = "beds-dark";
            yield return s;
        }
    }

    /// <summary>People who ate without a table in the last day, where the colony has a table and seats enough on paper.</summary>
    public class AdvisorWorker_NoSeat : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var thought = ThoughtDefOf.AteWithoutTable;
            if (thought == null || !WishWorker_DiningTable.HasDiningSpot(map)) yield break;
            var colonists = Colonists(map);
            if (colonists.Count < 2) yield break;
            // Fewer seats than people is the outgrown dining room's advice.
            int seats = RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).Sum(r => RoomUtil.RoomCount(r, Avail.IsChair));
            if (RoomUtil.DiningRoom != null && seats > 0 && seats < RoomGrowth.People(map).Count - 1) yield break;
            var ate = colonists.Where(p =>
            {
                var m = p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(thought);
                return m != null && m.age < GenDate.TicksPerDay;
            }).ToList();
            if (ate.Count < 2) yield break;
            var chair = Avail.BestBuildable(map, Avail.IsChair, "DiningChair", "Stool");
            var s = Signal(0.18f + 0.04f * Mathf.Min(5, ate.Count), "CC_Adv_NoSeat_Title".Translate(ate.Count).Resolve(),
                "CC_Adv_NoSeat_Detail".Translate(TextUtil.JoinAnd(ate.Select(p => p.LabelShort).ToList()), seats).Resolve(), 0.4f);
            s.look = new LookTargets(ate);
            s.suggestThing = chair;
            s.key = "no-seat";
            yield return s;
        }
    }

    /// <summary>Small facts the newer checks share.</summary>
    public static class AdvisorFacts
    {
        private static ThingCategoryDef meals;
        private static WorkTypeDef cooking;

        /// <summary>Meals in the colony's stores (the game's own count).</summary>
        public static int Meals(Map map)
        {
            meals ??= DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
            return meals == null || map == null ? 0 : map.resourceCounter.GetCountIn(meals);
        }

        public static WorkTypeDef Cooking => cooking ??= DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
    }

    /// <summary>The lights in a room, and planning lamps where it's dark: shared by the bed and workshop advice.</summary>
    public static class RoomLights
    {
        /// <summary>Every light in the room, built, framed or planned, with how far it lights the floor well.</summary>
        public static List<(int x, int z, float reach)> In(Room room)
        {
            var list = new List<(int x, int z, float reach)>();
            if (room == null) return list;
            foreach (var t in room.ContainedAndAdjacentThings)
            {
                if (t == null || !room.ContainsCell(t.Position)) continue;
                var def = t is Blueprint || t is Frame ? t.def.entityDefToBuild as ThingDef : t.def;
                if (def == null || def.category != ThingCategory.Building || !Avail.IsLight(def)) continue;
                list.Add((t.Position.x, t.Position.z, WallLights.Reach(def)));
            }
            return list;
        }

        /// <summary>
        /// Plans lights in these rooms where they're dark: wall lamps on their walls (WallLights.SpotsInRoom), or one
        /// standing light where the colony can't hang one. Says how many it planned.
        /// </summary>
        public static int Plan(List<Room> rooms, Map map)
        {
            int planned = 0;
            try
            {
                foreach (var room in rooms)
                {
                    if (room == null || room.Dereferenced) continue;
                    var def = WallLights.ForRoom(map, room.CellCount);
                    if (def == null || !Avail.CanBuild(def, map)) continue;
                    var stuff = def.MadeFromStuff ? Avail.BestStuff(def, map) : null;
                    if (WallLights.IsAttached(def))
                    {
                        foreach (var (cell, rot) in WallLights.SpotsInRoom(room, map, def, stuff, LightMath.MaxWallLamps(room.CellCount), null, LightMath.MinWallLamps(room.CellCount)))
                        {
                            GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction.OfPlayer, stuff);
                            planned++;
                        }
                    }
                    else if (PersonalTouchTracker.FindCell(room, map, def, stuff, out var c, out var r))
                    {
                        GenConstruct.PlaceBlueprintForBuild(def, c, map, r, Faction.OfPlayer, stuff);
                        planned++;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("advisor plan lights", e);
            }
            Messages.Message(planned > 0 ? "CC_Adv_PlannedLamps".Translate(planned).Resolve() : "CC_Adv_PlannedLamps_None".Translate().Resolve(),
                MessageTypeDefOf.SilentInput, false);
            return planned;
        }
    }
}
