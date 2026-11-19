using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // S12, the lost founder's grave (docs/quests-proposals.md; the rules in Flows/GraveFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Grave.xml). A people with a long past (the Quiet Bell, the keepers of the dead, first;
    // else any people at peace with a town near the colony). Part 1: their elders say one of the people who founded them is
    // buried on the colony's land and ask the colony to find the grave in a marked area and mark it: right-click inside the
    // area with a colonist to search it spot by spot (the lost keepsake's way); found, a grave marker of Rimfolk's own is set
    // there, the colony's. Not found in time, one of theirs comes and finds it. Built over first (a building of the colony's
    // on the very spot), it is lost. Part 2: on a set day three to five mourners come to the grave for the day, and the colony
    // hosts them (they eat at its table). Endings: kept, and they come each year for three years (StorylineAfter "grave") and
    // remember the colony well; the marker gone or built over, or the mourners refused, and they hold it against the colony.

    /// <summary>The founder's grave beyond its parts: the people, the marker, the yearly visits, the endings.</summary>
    public class GraveRules : StorylineRules
    {
        public override string Kind => GraveFlow.Key;

        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Sextons)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null);

        public static ThingDef MarkerDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_FounderGrave");

        /// <summary>The grave's marker on the map, still standing.</summary>
        public static Thing Marker(Storyline s, Map map) => s?.thing != null && !s.thing.Destroyed && s.thing.Spawned && s.thing.Map == map ? s.thing : null;

        /// <summary>Something of the colony's built (or being built) on the very spot: a building, a frame, a blueprint, other than the marker.</summary>
        public static bool BuiltOn(Map map, IntVec3 c, Thing marker) =>
            map != null && c.IsValid && c.InBounds(map) && c.GetThingList(map).Any(t => t != marker && t.Faction == Faction.OfPlayer
                                                                                      && (t.def.category == ThingCategory.Building || t.def.IsBlueprint || t.def.IsFrame));

        /// <summary>The grave lost: the marker gone, or something of the colony's built on its spot.</summary>
        public static bool Lost(Storyline s, Map map) => s?.thing == null ? BuiltOn(map, s?.spot ?? IntVec3.Invalid, null) : Marker(s, map) == null || BuiltOn(map, s.thing.Position, s.thing);

        /// <summary>The grave marked: Rimfolk's marker set on the spot (or the nearest free cell), the colony's.</summary>
        public static Thing Mark(Storyline s, Map map)
        {
            var def = MarkerDef;
            if (def == null || map == null || !s.spot.IsValid) return null;
            try
            {
                var at = s.spot;
                if (!at.Standable(map) || at.GetEdifice(map) != null)
                    CellFinder.TryFindRandomCellNear(s.spot, map, 3, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null, out at);
                if (!at.IsValid) return null;
                var t = ThingMaker.MakeThing(def);
                t.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(t, at, map);
                s.thing = t;
                s.spot = at;
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the grave marked", e);
                return null;
            }
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            switch (key)
            {
                case "kept":
                    Storylines.AddAfter(new StorylineAfter
                    {
                        kind = "grave", faction = s.giver, who = s.name, thing = s.thing, left = GraveFlow.Years,
                        next = Find.TickManager.TicksGame + Mathf.RoundToInt(GraveFlow.YearDays * GenDate.TicksPerDay),
                        until = Find.TickManager.TicksGame + Mathf.RoundToInt((GraveFlow.Years + 0.5f) * GraveFlow.YearDays * GenDate.TicksPerDay)
                    });
                    FactionMemory.Note(s.giver, "mourned", s.name);
                    return true;
                case "refused":
                    StoryChains.Goodwill(s.giver, GraveFlow.RefusedGoodwill, GoodwillWhy.Of(GoodwillReasonMath.TurnedAway));
                    FactionMemory.Note(s.giver, "refused", Storylines.Label(s));
                    return true;
                case "builtover":
                    StoryChains.Goodwill(s.giver, GraveFlow.BuiltOverGoodwill, GoodwillWhy.Of(GoodwillReasonMath.TheirDead));
                    FactionMemory.Note(s.giver, "slighted", Storylines.Label(s));
                    return true;
                case "harmed":
                    FactionMemory.Note(s.giver, "prisonerDied", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        /// <summary>
        /// Every year for three years after the grave was kept (a StorylineAfter "grave"): two or three of theirs come to the
        /// grave for a few hours and go home, thinking well of the colony. The marker gone or built over: they find it so,
        /// hold it against the colony, and come no more.
        /// </summary>
        public override bool After(StorylineAfter a, int now)
        {
            if (a.kind != "grave") return false;
            if (now < a.next || a.left <= 0) return true;
            var map = HappeningTracker.HomeMap();
            var f = a.faction;
            if (map == null || f == null || f.defeated || f.HostileTo(Faction.OfPlayer))
            {
                a.left = 0;
                a.until = now - 1;
                return true;
            }
            if (MapFacts.ThreatOn(map))
            {
                a.next = now + GenDate.TicksPerHour * 6;
                return true;
            }
            var marker = a.thing != null && !a.thing.Destroyed && a.thing.Spawned && a.thing.Map == map ? a.thing : null;
            if (marker == null || BuiltOn(map, marker.Position, marker))
            {
                Tell.Message(NewsKind.World, Importance.Important, map, "CC_Story3_Grave_YearLost".Translate(f.Name.Named("FACTION"), (a.who ?? "?").Named("NAME")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
                StoryChains.Goodwill(f, GraveFlow.BuiltOverGoodwill, GoodwillWhy.Of(GoodwillReasonMath.TheirDead));
                a.left = 0;
                a.until = now - 1;
                return true;
            }
            int n = Rand.RangeInclusive(2, 3);
            var people = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(f, new FloatRange(20f, 70f));
                if (p != null) people.Add(p);
            }
            if (people.Count == 0 || Arrivals.Visit(people, map, f, marker.Position, 6f) == null)
            {
                foreach (var p in people.Where(p => !p.Spawned)) Arrivals.Discard(p);
                a.next = now + GenDate.TicksPerDay;
                return true;
            }
            foreach (var p in people) Hosting.Provision(p, 0.5f);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Story3_Grave_Year".Translate(f.Name.Named("FACTION"), (a.who ?? "?").Named("NAME")).Resolve(),
                new LookTargets(people), MessageTypeDefOf.PositiveEvent);
            StoryChains.Goodwill(f, GraveFlow.YearGoodwill, GoodwillWhy.Of(GoodwillReasonMath.TheirDead));
            FactionMemory.Note(f, "mourned", a.who);
            a.left--;
            a.next = now + Mathf.RoundToInt(GraveFlow.YearDays * GenDate.TicksPerDay);
            return true;
        }

        public override void Gone(Storyline s, Map map)
        {
            var here = new[] { s.x, s.y }.Concat(s.group).Where(p => p != null && p.Spawned && p.Map == map && p.Faction != Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer)).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
        }

        public override void DebugEnded(Storyline s, string part, string outcome)
        {
            if (part == "search" && (outcome == "found" || outcome == "theyfound") && Marker(s, s.Map) == null) Mark(s, s.Map);
        }
    }

    // ===================== Part 1: the search for the grave =====================

    /// <summary>
    /// Their elders say one of the people who founded them is buried on the colony's land, somewhere in an area they mark on
    /// the map, and ask the colony to find the grave within four to six days. Searched spot by spot (right-click inside it
    /// with a colonist); found, the grave is marked. Not found in time, one of theirs comes and finds it. A building of the
    /// colony's on the spot first: built over.
    /// </summary>
    public class HappeningWorker_GraveSearch : HappeningWorker_StorylinePart, IStorylineSearch, IStorylineDebugNow
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.Rules(GraveFlow.Key).Giver(map) != null && GraveRules.MarkerDef != null && Place(map, out _, out _);

        /// <summary>A place out on the colony's land, with room around it for the area; the area's middle near it but not on it.</summary>
        private static bool Place(Map map, out IntVec3 cell, out IntVec3 area)
        {
            area = IntVec3.Invalid;
            if (!HomeQuests.FarCell(map, GraveFlow.AreaFrom, c => !HomeQuests.NearEdge(map, c, (int)GraveFlow.AreaRadius + 2), out cell)) return false;
            float off = GraveFlow.AreaRadius - GraveFlow.AreaMargin;
            var c0 = cell;
            for (int i = 0; i < 12; i++)
            {
                var a = c0 + (Rand.InsideUnitCircleVec3 * off).ToIntVec3();
                if (a.InBounds(map) && a.InHorDistOf(c0, off)) return (area = a).IsValid;
            }
            area = cell;
            return true;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(GraveFlow.Key).Giver(map);
            if (s == null || f == null || !Place(map, out var cell, out var area)) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            s.giver = f;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.name = InviteForms.Named(f, new FloatRange(50f, 80f), null, out _);
            s.spot = cell;
            s.way = HomeQuests.Direction(map, area);
            if (Peoples.CallingOf(f) == PeoplesMath.Sextons) s.Set("bell");
            m.faction = f;
            m.place = s.place;
            m.area = area;
            m.name = s.name;
            m.days = Rand.Range(GraveFlow.SearchMinDays, GraveFlow.SearchMaxDays);
            m.goodwill = GraveFlow.FoundGoodwill;
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || !m.area.IsValid) return false;
            StorylineRound3.Forget(m);
            m.stage = 1;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(new TargetInfo(m.area, map)));
            return true;
        }

        public bool Marked(Mission m, Map map, out IntVec3 center, out float radius)
        {
            center = m.area;
            radius = GraveFlow.AreaRadius;
            return m.stage == 1 && center.IsValid && center.InBounds(map);
        }

        public float LookRadius => GraveFlow.LookRadius;

        public string SearchLabel(Mission m) => "CC_Story3_Grave_Menu".Translate((m.name ?? "?").Named("NAME")).Resolve();

        public string SearchReport(Mission m) => "CC_Story3_Grave_Job".Translate((m.name ?? "?").Named("NAME")).Resolve();

        public bool Looked(Mission m, Pawn p, IntVec3 spot)
        {
            var s = Story(m);
            if (s == null || !GraveFlow.Covers(spot.x - s.spot.x, spot.z - s.spot.z)) return false;
            Found(m, s, p.Map, p, "found");
            return true;
        }

        /// <summary>Found: the grave marked, the area no longer marked.</summary>
        private void Found(Mission m, Storyline s, Map map, Pawn finder, string how)
        {
            var t = GraveRules.Mark(s, map);
            if (t == null)
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            m.area = IntVec3.Invalid;
            m.stage = 2;
            StorylineRound3.Forget(m);
            if (finder != null && finder.IsColonist) m.a = finder;
            Say(finder, m, how);
            if (how == "found") Missions.Goodwill(m, m.goodwill);
            End(m, MissionState.Done, how, new LookTargets(t), finder?.LabelShort);
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 1)
            {
                if (GraveRules.BuiltOn(map, s.spot, null))
                {
                    m.area = IntVec3.Invalid;
                    StorylineRound3.Forget(m);
                    End(m, MissionState.Failed, "builtover", new LookTargets(new TargetInfo(s.spot, map)));
                    return;
                }
                if (now < m.deadline) return;
                // The time is up: one of theirs comes and searches themselves.
                var searcher = HomeQuests.Person(s.giver, new FloatRange(30f, 70f));
                m.area = IntVec3.Invalid;
                StorylineRound3.Forget(m);
                if (searcher == null || Arrivals.Visit(new List<Pawn> { searcher }, map, s.giver, s.spot, GraveFlow.TheirSearchHours + 2f) == null)
                {
                    if (searcher != null && !searcher.Spawned) Arrivals.Discard(searcher);
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                Hosting.Provision(searcher, 0.5f);
                m.group.Add(searcher);
                m.stage = 3;
                m.lastLook = now;
                Note(m, map, "theirsearch", new LookTargets(searcher));
                return;
            }
            if (m.stage == 3)
            {
                var searcher = m.group.FirstOrDefault();
                bool there = searcher != null && searcher.Spawned && searcher.Position.InHorDistOf(s.spot, 3f);
                if (!there && now - m.lastLook < GraveFlow.TheirSearchHours * GenDate.TicksPerHour) return;
                if (searcher == null || searcher.Dead || !searcher.Spawned)
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                Arrivals.SendAway(new[] { searcher });
                Found(m, s, map, searcher, "theyfound");
            }
        }

        public override void Release(Mission m)
        {
            StorylineRound3.Forget(m);
            foreach (var p in m.group.Where(p => p != null && !p.Dead)) Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        public override LookTargets Targets(Mission m)
        {
            var map = m.Map;
            var s = Story(m);
            if (s?.thing != null && s.thing.Spawned) return new LookTargets(s.thing);
            if (map != null && m.area.IsValid && m.area.InBounds(map)) return new LookTargets(new TargetInfo(m.area, map));
            var p = m.group.FirstOrDefault(x => x != null && x.Spawned);
            return p != null ? new LookTargets(p) : LookTargets.Invalid;
        }

        public override string Status(Mission m) =>
            m.stage == 3 ? "CC_Story3_Grave_TheirSearch".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve()
                : "CC_Story3_Grave_Searching".Translate((m.name ?? "?").Named("NAME")).Resolve();

        public string DebugNow(Mission m, Map map)
        {
            var s = Story(m);
            if (m.stage != 1 || s == null) return "not being searched for now.";
            var p = m.a != null && m.a.Spawned ? m.a : map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            Found(m, s, map, p, "found");
            return "found now.";
        }
    }

    // ===================== Part 2: the mourners =====================

    /// <summary>
    /// On a set day three to five mourners of theirs come to the grave in the morning and stay about ten hours, eating at the
    /// colony's table (the colony's guests). Fed when hungry: hosted; not: poorly. One of them dead or taken prisoner: harmed.
    /// The marker gone or built over: built over.
    /// </summary>
    public class HappeningWorker_GraveMourners : HappeningWorker_StorylineHost, IStorylineDebugNow
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && GraveRules.Marker(Storylines.current, map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            int n = GraveFlow.Mourners(Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var p = Make(s.giver, new FloatRange(18f, 75f));
                if (p != null) m.group.Add(p);
            }
            if (m.group.Count == 0) return false;
            m.x = m.group[0];
            m.group.RemoveAt(0);
            s.count = n;
            m.faction = s.giver;
            m.place = s.place;
            m.name = s.name;
            m.thing = s.thing;
            m.target = n;
            m.days = 2f;
            m.goodwill = GraveFlow.HostedGoodwill;
            m.goals.Add(new MissionGoal { what = "meals", count = n });
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.count = n;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            // The set day: they come in the morning, at seven, the next morning when it is past.
            int now = Find.TickManager.TicksGame;
            int toSeven = ((7 - GenLocalDate.HourOfDay(map) + 24) % 24) * GenDate.TicksPerHour;
            m.due = now + (toSeven < GenDate.TicksPerHour * 4 ? toSeven + GenDate.TicksPerDay : toSeven);
            m.deadline = Mathf.Max(m.deadline, m.due + GenDate.TicksPerDay);
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.x == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (GraveRules.Lost(s, map))
            {
                SeeOff(m, map);
                End(m, MissionState.Failed, "builtover");
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.goodwill);
                End(m, MissionState.Failed, "harmed");
                return;
            }
            var marker = GraveRules.Marker(s, map);
            if (m.stage == 0)
            {
                if (now < m.due) return;
                if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerHour * 12) return;
                if (!Arrive(m, map, marker.Position, GraveFlow.MournHours + 2f))
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.lacks = 0;
                Note(m, map, "came", new LookTargets(Here(m, map).ToList()));
                return;
            }
            // Through the day: fed at the colony's table when hungry (beds don't matter for a day), a word at the grave once all are there.
            m.lacks |= (int)(Tend(m, map) & Hosting.Lack.Food);
            var here = Here(m, map).ToList();
            if (m.stage == 1 && here.Count > 0 && here.All(p => p.Position.InHorDistOf(marker.Position, 8f)))
            {
                m.stage = 2;
                Say(here[0], m, "mourn");
            }
            if (now - m.lastLook < GraveFlow.MournHours * GenDate.TicksPerHour && here.Count > 0) return;
            string how = (m.lacks & (int)Hosting.Lack.Food) == 0 ? "hosted" : "poorly";
            Missions.Goodwill(m, how == "hosted" ? m.goodwill : m.goodwill / 3);
            if (here.Count > 0) Say(here[0], m, how);
            SeeOff(m, map);
            End(m, MissionState.Done, how, new LookTargets(marker));
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            return m.stage == 0
                ? "CC_Story3_Grave_Coming".Translate(m.target.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve()
                : "CC_Story3_Grave_Mourning".Translate((s.name ?? "?").Named("NAME")).Resolve() + " " + MealsText(m);
        }

        public string DebugNow(Mission m, Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (m.stage == 0) m.due = now;
            else m.lastLook = now - Mathf.RoundToInt(GraveFlow.MournHours * GenDate.TicksPerHour) - 1;
            return m.stage == 0 ? "the mourners come now." : "their day at the grave is over now.";
        }
    }
}
