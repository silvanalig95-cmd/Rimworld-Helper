using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The shared memories the newer systems leave (Defs/SharedMemoryDefs/Rimfolk_SharedMemories_Times.xml).</summary>
    public static class TimesDefs
    {
        public static SharedMemoryDef CampFire => TalkDefs.Mem("Rimfolk_Mem_CampFire");
        public static SharedMemoryDef HealerRound => TalkDefs.Mem("Rimfolk_Mem_HealerRound");
        public static SharedMemoryDef SmithMending => TalkDefs.Mem("Rimfolk_Mem_SmithMending");
        public static SharedMemoryDef ChildFound => TalkDefs.Mem("Rimfolk_Mem_ChildFound");
        public static SharedMemoryDef PilgrimRoad => TalkDefs.Mem("Rimfolk_Mem_PilgrimRoad");
        public static SharedMemoryDef HarvestHands => TalkDefs.Mem("Rimfolk_Mem_HarvestHands");
        public static SharedMemoryDef WorldPart => TalkDefs.Mem("Rimfolk_Mem_WorldPart");
        public static SharedMemoryDef DayKept => TalkDefs.Mem("Rimfolk_Mem_DayKept");
        public static SharedMemoryDef FeastRain => TalkDefs.Mem("Rimfolk_Mem_FeastRain");
        public static SharedMemoryDef BurnedDish => TalkDefs.Mem("Rimfolk_Mem_BurnedDish");
        public static SharedMemoryDef Pyre => TalkDefs.Mem("Rimfolk_Mem_Pyre");
        public static SharedMemoryDef NightWatch => TalkDefs.Mem("Rimfolk_Mem_NightWatch");
        public static SharedMemoryDef LanternNight => TalkDefs.Mem("Rimfolk_Mem_LanternNight");
        public static SharedMemoryDef JokeHeard => TalkDefs.Mem("Rimfolk_Mem_JokeHeard");
    }

    /// <summary>
    /// What the newer systems leave behind among the colony's people: the shared memories two or more of them bring up
    /// later, the deeds the others talk about, and the setbacks that follow (Setbacks/SetbackTimes.cs). Each entry point
    /// is one line where the system already says the thing is over:
    /// <list type="bullet">
    /// <item>a camp gone (CampLife.Finish): evenings at its fire, the healer's last round, the smith's mending; a friend
    /// made there, or a quarrel picked;</item>
    /// <item>a mission ended (Missions.Finish): the lost child found, the pilgrim walked home, a neighbour's harvest
    /// brought in; or the neighbours let down;</item>
    /// <item>a calendar day kept (CalendarKeeping.KeepDay), and its gathering over (PlayOutTracker.Done): the day kept
    /// together, and as jokes once it's over, the feast in the rain and the burned dish;</item>
    /// <item>an answer played out on the map (PlayOutTracker.Done): the dead carried out to the pyre, the night watch on
    /// the walls, a lantern night, the rounds of the sick;</item>
    /// <item>a world event ended (WorldSituations.End): the colony's part in it, and who answered the call.</item>
    /// </list>
    /// Memories follow the social-life setting (<see cref="Persona.On"/>), deeds the reputation setting, setbacks their
    /// own. Nothing here ticks of its own; the mishaps waiting to become jokes are looked at in the setbacks' hour and
    /// aren't saved (a load forgets them, as the talk's own do).
    /// </summary>
    public static class SharedTimes
    {
        private static int Now => Find.TickManager.TicksGame;

        private static void Safe(string what, Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("shared times: " + what, e);
            }
        }

        // ---------------- Words ----------------

        /// <summary>A name with "the" in front, as a memory says it: "a long drought" is "the long drought"; names and possessives stay as they are.</summary>
        public static string The(string name)
        {
            if (name.NullOrEmpty()) return name;
            string s = name.Trim();
            string low = s.ToLowerInvariant();
            if (low.StartsWith("the ", StringComparison.Ordinal) || s.Contains("'s ") || s.Contains("s' ")) return s;
            if (low.StartsWith("a ", StringComparison.Ordinal)) s = s.Substring(2);
            else if (low.StartsWith("an ", StringComparison.Ordinal)) s = s.Substring(3);
            return "the " + s;
        }

        /// <summary>What a world event is called in a memory: its own spoken name if it has one (CC_Times_World_defName), else its label with "the".</summary>
        public static string WorldName(WorldSituationDef def)
        {
            if (def == null) return "?";
            string key = "CC_Times_World_" + def.defName;
            return key.CanTranslate() ? key.Translate().ToString() : The(def.label);
        }

        private static int Year(Map map) => GenLocalDate.Year(map ?? Find.AnyPlayerHomeMap);

        // ---------------- Memories between several people ----------------

        /// <summary>
        /// Everyone in it shares the memory with the ones they're closest to among the others (at most <paramref name="each"/>
        /// each), so a crowd doesn't fill everyone's memories (as the talk's own do).
        /// </summary>
        public static void AmongThem(IEnumerable<Pawn> people, SharedMemoryDef def, string label, float weight, int each)
        {
            if (def == null || people == null || label.NullOrEmpty() || !Persona.On) return;
            var list = people.Where(Talk.Eligible).Distinct().ToList();
            if (list.Count < 2) return;
            foreach (var p in list)
            {
                var closest = list.Where(o => o != p).OrderByDescending(o => p.relations?.OpinionOf(o) ?? 0).Take(each).ToList();
                foreach (var o in closest) TalkHooks.AddOnce(p, o, def, label, weight);
            }
        }

        /// <summary>One who did it (first, for [byme]) with each of the others.</summary>
        private static void WithEach(Pawn doer, IEnumerable<Pawn> others, SharedMemoryDef def, string label, float weight, int most)
        {
            if (def == null || doer == null || others == null || label.NullOrEmpty() || !Persona.On || !Talk.Eligible(doer)) return;
            foreach (var o in others.Where(o => o != doer && Talk.Eligible(o)).Distinct().OrderByDescending(o => doer.relations?.OpinionOf(o) ?? 0).Take(most).ToList())
                TalkHooks.AddOnce(doer, o, def, label, weight);
        }

        // ---------------- Mishaps waiting to be funny ----------------

        private sealed class Pending
        {
            public SharedMemoryDef def;
            public string label;
            public List<Pawn> people;
            public int tick;
        }

        private static readonly List<Pending> pending = new List<Pending>();
        private static Game pendingGame;

        /// <summary>A mishap at a gathering: once the day is over and everyone in it is up and about, it's a joke.</summary>
        internal static void Mishap(SharedMemoryDef def, string label, List<Pawn> people)
        {
            if (def == null || label.NullOrEmpty() || people == null || people.Count < 2 || !Persona.On) return;
            if (pendingGame != Current.Game)
            {
                pending.Clear();
                pendingGame = Current.Game;
            }
            if (pending.Count >= 12) pending.RemoveAt(0);
            pending.Add(new Pending { def = def, label = label, people = people.Where(p => p != null).Distinct().ToList(), tick = Now });
        }

        /// <summary>The setbacks' hour (SetbackTracker): mishaps half a day old, with everyone fine and nobody of ours dead meanwhile, become jokes.</summary>
        internal static void Hourly()
        {
            if (pending.Count == 0) return;
            if (pendingGame != Current.Game)
            {
                pending.Clear();
                return;
            }
            int now = Now;
            foreach (var m in pending.ToList())
            {
                if (now - m.tick > GenDate.TicksPerDay * 2)
                {
                    pending.Remove(m);
                    continue;
                }
                if (now - m.tick < GenDate.TicksPerDay / 2) continue;
                if (m.people.Any(p => p.Dead || p.Downed)) continue;
                pending.Remove(m);
                if (TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 1f) != null) continue;
                AmongThem(m.people, m.def, m.label, 1f, 2);
            }
        }

        // ---------------- Camps ----------------

        /// <summary>The endings of a camp that went in peace (its time up, asked, the weather, a raid, hunger, crowding).</summary>
        public static bool Peaceful(string ending) =>
            ending == "time" || ending == "asked" || ending == "weather" || ending == "raid" || ending == "hunger" || ending == "crowded";

        /// <summary>
        /// A camp has gone (CampLife.Finish): those who spent evenings at its fire remember them together; those its healer
        /// looked in on remember the round; those who were about the camp remember the smith mending the gate. The one
        /// who made friends there is talked of, and so is one who quarrelled with them. Then the setbacks (it ended badly
        /// for friends made there, or they left owing).
        /// </summary>
        public static void CampEnded(Camp c)
        {
            // apart: the camp is gone; what they lived through happened at its fire and on the colony's land while it stood.
            if (c?.kind == null || c.map == null || c.arrivedTick < 0) return;
            Safe("camp ended", () =>
            {
                bool peaceful = Peaceful(c.ending);
                var warmth = c.warmth.Where(kv => kv.Key != null && !kv.Key.Dead && kv.Key.IsColonist).OrderByDescending(kv => kv.Value).ToList();
                var warm = warmth.Where(kv => kv.Value >= 2).Select(kv => kv.Key).ToList();
                string campLabel = "CC_Times_Label_Camp".Translate(c.kind.label).Resolve();
                if (peaceful && warm.Count >= 2) AmongThem(warm, TimesDefs.CampFire, campLabel, 1f, 2);
                // The healer's last round: those looked in on, one after another.
                var walk = c.kind.farewellWalk;
                if (walk != null && walk.to == "sick" && c.walkVisits > 0)
                {
                    var seen = c.walkSeen.Where(p => p != null && !p.Dead).ToList();
                    string round = c.walker != null ? "CC_Times_Label_Round".Translate(c.walker.LabelShort).Resolve() : "CC_Times_Label_RoundNoName".Translate(c.kind.label).Resolve();
                    if (seen.Count >= 2) AmongThem(seen, TimesDefs.HealerRound, round, 1f, 3);
                }
                // The smith's mending, remembered by those who were about the camp while it was done.
                if (c.repairs > 0 && !c.repairedLabel.NullOrEmpty())
                {
                    var about = warmth.Where(kv => kv.Value >= 1).Select(kv => kv.Key).ToList();
                    if (about.Count >= 2) AmongThem(about, TimesDefs.SmithMending, The(c.repairedLabel), 1f, 2);
                }
                // A friend made at the camp: the warmest of them, if it was real (time at the fire and a good word between them).
                var them = c.members.Where(m => m != null && !m.Dead && !c.joined.Contains(m)).ToList();
                if (peaceful && warmth.Count > 0 && warmth[0].Value >= 4)
                {
                    var friend = warmth[0].Key;
                    if (them.Any(m => (friend.relations?.OpinionOf(m) ?? 0) >= 10))
                        Deeds.Record("Rimfolk_Deed_CampFriend", friend, null, -1f, null, c.kind.label, null, false);
                }
                // A quarrel with them: someone who can't stand one of them, when it ended badly or the camp was tense.
                if (!peaceful || c.tension >= 50f)
                {
                    Pawn foe = null;
                    int worst = -20;
                    foreach (var col in IncidentWorker_Happening.Colonists(c.map))
                        foreach (var m in them)
                        {
                            int op = col.relations?.OpinionOf(m) ?? 0;
                            if (op < worst)
                            {
                                worst = op;
                                foe = col;
                            }
                        }
                    if (foe != null) Deeds.Record("Rimfolk_Deed_CampQuarrel", foe, null, -1f, null, c.kind.label);
                }
                Setbacks.Notify_CampEnded(c, warm);
            });
        }

        // ---------------- Missions ----------------

        /// <summary>The colony's people in a caravan, from where a mission says it ended (its look target).</summary>
        private static List<Pawn> CaravanPeople(LookTargets look)
        {
            var list = new List<Pawn>();
            if (look?.targets == null) return list;
            foreach (var t in look.targets)
                if (t.HasWorldObject && t.WorldObject is Caravan c)
                    list.AddRange(c.PawnsListForReading.Where(p => p != null && p.IsColonist && !p.Dead));
            return list.Distinct().ToList();
        }

        /// <summary>Endings of a mission the colony let slip: too late, turned back, missed, the pilgrim left to walk alone, the child found by others.</summary>
        public static readonly HashSet<string> LetDown = new HashSet<string> { "late", "turned", "missed", "alone", "searchers" };

        /// <summary>
        /// A mission ended (Missions.Finish): the child found is remembered by the finder with those at home, the pilgrim
        /// walked home and the harvest brought in by those who went; the finder, the escort and the hands are talked of. A
        /// mission let slip is talked of too, and felt (Setbacks).
        /// </summary>
        public static void MissionEnded(Mission m, MissionState state, string outcome, bool wasActive, LookTargets look)
        {
            // apart: the mission's people did it together (the caravan, the search on the colony's land); this is its end.
            if (m == null || !wasActive) return;
            Safe("mission " + m.kind, () =>
            {
                var map = m.Map;
                var went = CaravanPeople(look);
                if (state == MissionState.Done)
                {
                    switch (m.kind)
                    {
                        case "lostchild":
                            var finder = m.a;
                            if (finder != null && !finder.Dead && finder.IsColonist && !m.name.NullOrEmpty())
                            {
                                if (map != null) WithEach(finder, IncidentWorker_Happening.Colonists(map), TimesDefs.ChildFound, m.name, 1f, 3);
                                Deeds.Record("Rimfolk_Deed_FoundChild", finder, m.x, -1f, null, null, null, false);
                            }
                            break;
                        case "escort":
                            if (went.Count > 0 && !m.name.NullOrEmpty())
                            {
                                AmongThem(went, TimesDefs.PilgrimRoad, "CC_Times_Label_Pilgrim".Translate(m.name).Resolve(), 1f, 3);
                                foreach (var p in went) Deeds.Record("Rimfolk_Deed_Pilgrim", p, null, 0.8f, null, m.place, went, false);
                            }
                            break;
                        case "harvest":
                            if (went.Count > 0 && !m.place.NullOrEmpty())
                            {
                                AmongThem(went, TimesDefs.HarvestHands, "CC_Times_Label_Harvest".Translate(m.place).Resolve(), 1f, 3);
                                float size = outcome == "partial" ? 0.5f : 1f;
                                foreach (var p in went) Deeds.Record("Rimfolk_Deed_HarvestHands", p, null, size, null, m.place, went, false);
                            }
                            break;
                    }
                }
                else if (state == MissionState.Failed && outcome != null && LetDown.Contains(outcome) && m.a != null && !m.a.Dead && m.a.IsColonist)
                    Deeds.Record("Rimfolk_Deed_LetDown", m.a, null, -1f, null, m.faction?.Name ?? "?", null, false);
                Setbacks.Notify_MissionEnded(m, state, outcome, went);
            });
        }

        // ---------------- The calendar ----------------

        /// <summary>
        /// The calendar day a gathering keeps: one kept this year whose name the gathering bears (CalendarKeeping.PlayDay
        /// names it after the day). Null for any other gathering.
        /// </summary>
        public static CalendarDay DayOf(PlayOut p)
        {
            if (p == null || !p.IsGathering || p.label.NullOrEmpty()) return null;
            var days = CalendarTracker.Instance?.days;
            if (days == null) return null;
            int year = Year(p.map);
            foreach (var d in days)
                if (d != null && d.label == p.label && d.lastKeptYear >= year - 1 && d.timesKept > 0)
                    return d;
            return null;
        }

        /// <summary>
        /// A calendar day was kept (CalendarKeeping.KeepDay). Kept on the map, its gathering says who was there when it's
        /// over (<see cref="PlayDone"/>); kept without one (a feast or games nobody could gather for), those at home share
        /// it. The setbacks look for an empty place at the table.
        /// </summary>
        public static void DayKept(CalendarDay d, Map map, List<Pawn> home)
        {
            // apart: a day the whole colony keeps, at home; who gathered for it is known when its gathering ends.
            if (d == null || map == null || home.NullOrEmpty()) return;
            Safe("day kept", () =>
            {
                bool gathering = d.way == DayWay.Feast || d.way == DayWay.Games || d.way == DayWay.Bonfire;
                bool playing = PlayOutTracker.Instance?.plays?.Any(p => p != null && p.state != PlayState.Done && p.label == d.label && p.IsGathering) ?? false;
                if (gathering && !playing) AmongThem(home, TimesDefs.DayKept, DayLabel(d, map), 0.8f, 2);
                Setbacks.Notify_DayKept(d, map, home);
            });
        }

        public static string DayLabel(CalendarDay d, Map map) => "CC_Times_Label_Day".Translate(The(d.label), Year(map)).Resolve();

        // ---------------- Answers played out on the map ----------------

        /// <summary>Lanterns set out on the map right now (OnMapTimed): a gathering that ends among them was lantern night.</summary>
        private static bool LanternsOut(Map map) =>
            map != null && (OnMapTimed.Instance?.kept?.Any(k => k != null && k.kind == TimedKind.Lights && k.map == map) ?? false);

        /// <summary>
        /// Something an answer set going on the map happened (PlayOutTracker.Done), with <paramref name="took"/> those who
        /// took part: the dead carried out to the pyre, a night watch walked, rounds gone, a calendar day's gathering, a
        /// lantern night.
        /// </summary>
        public static void PlayDone(PlayOut p, List<Pawn> took)
        {
            // together: those who took part did it side by side on the map (the play-out's own measure of taking part).
            if (p == null || took == null || took.Count == 0) return;
            Safe("play " + p.kind, () =>
            {
                var map = p.map;
                switch (p.kind)
                {
                    case PlayKind.Haul:
                        string fight = FightNames.Of(map);
                        AmongThem(took, TimesDefs.Pyre, fight, 1f, 3);
                        foreach (var h in took) Deeds.Record("Rimfolk_Deed_Pyre", h, null, -1f, fight, null, took, false);
                        break;
                    case PlayKind.Vigil:
                        if (!p.patrol) break;
                        string night = "CC_Times_Label_Watch".Translate(FightNames.DateNow(map)).Resolve();
                        AmongThem(took, TimesDefs.NightWatch, night, 1f, 3);
                        foreach (var h in took) Deeds.Record("Rimfolk_Deed_NightWatch", h, null, -1f, null, null, took, false);
                        break;
                    case PlayKind.Rounds:
                        var goer = p.hands.FirstOrDefault(h => h?.pawn != null && !h.pawn.Dead)?.pawn;
                        if (goer != null && took.Contains(goer)) Deeds.Record("Rimfolk_Deed_Rounds", goer, null, -1f, null, null, took, false);
                        break;
                }
                if (!p.IsGathering) return;
                var day = DayOf(p);
                if (day != null)
                {
                    AmongThem(took, TimesDefs.DayKept, DayLabel(day, map), 1f, 2);
                    var keeper = p.organizer;
                    if (keeper != null && took.Contains(keeper) && took.Count >= 3)
                        Deeds.Record("Rimfolk_Deed_KeptDay", keeper, null, -1f, null, The(day.label), took, false);
                    return;
                }
                if (LanternsOut(map)) AmongThem(took, TimesDefs.LanternNight, "CC_Times_Label_Lanterns".Translate(Year(map)).Resolve(), 1f, 2);
            });
        }

        // ---------------- World events ----------------

        /// <summary>
        /// A world event ended (WorldSituations.End): when the colony's part counted, those at home remember doing it
        /// together, and the one who went is talked of. Then the setbacks (the colony stood by).
        /// </summary>
        public static void SituationEnded(WorldSituation s, SituationEnding ending)
        {
            // apart: the colony's answers to a world event, decided at home and carried out over its days.
            if (s?.def == null || ending == null) return;
            Safe("world event " + s.def.defName, () =>
            {
                var map = s.ctx?.map ?? HappeningTracker.HomeMap();
                var home = map != null ? IncidentWorker_Happening.Colonists(map) : new List<Pawn>();
                if (ending.helped && s.help > 0)
                {
                    if (home.Count >= 2) AmongThem(home, TimesDefs.WorldPart, WorldName(s.def), 0.9f, 2);
                    var a = s.ctx?.a;
                    if (a != null && !a.Dead && a.IsColonist)
                    {
                        string who = s.ctx.faction?.Name ?? "CC_Times_TheRegion".Translate().ToString();
                        Deeds.Record("Rimfolk_Deed_AnsweredCall", a, null, s.help >= 3 ? 1.3f : 1f, null, who, home, false);
                    }
                }
                Setbacks.Notify_SituationEnded(s, ending, home);
            });
        }
    }
}
