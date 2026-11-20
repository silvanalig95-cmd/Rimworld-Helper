using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// How raids meet the lines, for the lessons the helper draws from them. Every 250 ticks, and only while hostile
    /// raiders (people or mechanoids) are on the map and the map has lines: where each raider is in front of or behind
    /// each line, and when one gets behind, whether it came through the gate (its walk passed close by the gate),
    /// broke through (sappers and breachers) or went round; those first seen behind a line early in the raid came down
    /// inside (drop pods). Colonists downed near a line are noted with the place. The colony's animals are followed too
    /// (C22 of docs/content-update-proposals.md): those that fight near a line, the enemies they bring down there (from
    /// their own records, before and after) and those of them downed or killed there. When the raid is over, each line it
    /// touched keeps one to three lessons (keep the animals back, when several fell), with two notes beside them: the
    /// animals that held, and the weather the raid came in (C28: the game takes aim off every shot in fog, rain or snow
    /// where the shooter or the target is under the open sky). An animal that brought an enemy down has it on its card,
    /// and its person writes it down. One message says how the busiest line did. The raid's first look says what the
    /// weather does to the shooting today, and, with the setting on, which lines have their gate shut.
    /// </summary>
    public partial class DefenseLines
    {
        private bool raidOn, reminded;
        private int raidStart, quiet;
        private List<DefenseLineRaid> running = new List<DefenseLineRaid>();
        private List<int> downedSeen = new List<int>();

        private class Track
        {
            public IntVec3 last;
            /// <summary>The lines (by id) it has been out in front of, and those its way past was already counted for.</summary>
            public readonly HashSet<int> outside = new HashSet<int>(), counted = new HashSet<int>();
        }

        private readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();

        /// <summary>The colony's animals in this raid (not saved: after a load, followed again from then).</summary>
        private class Beast
        {
            public Pawn pawn;
            public int downs, kills;
            public IntVec3 last;
            /// <summary>The line it last fought at (-1: none), and whether its fall was counted.</summary>
            public int line = -1;
            public bool fell;
        }

        private readonly Dictionary<int, Beast> beasts = new Dictionary<int, Beast>();

        /// <summary>Raiders first seen behind a line this long after the raid began didn't come down inside it.</summary>
        private const int LandedWithin = 1500;

        private void ExposeWatch()
        {
            Scribe_Values.Look(ref raidOn, "raidOn");
            Scribe_Values.Look(ref reminded, "reminded");
            Scribe_Values.Look(ref raidStart, "raidStart");
            Scribe_Values.Look(ref quiet, "quiet");
            Scribe_Collections.Look(ref running, "running", LookMode.Deep);
            Scribe_Collections.Look(ref downedSeen, "downedSeen", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                running ??= new List<DefenseLineRaid>();
                running.RemoveAll(r => r == null);
                downedSeen ??= new List<int>();
            }
        }

        public override void MapComponentTick()
        {
            if (lines.Count == 0 && !raidOn) return;
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareDefenseLines)) return;
            using var clock = PassClock.Time(Pass.RareDefenseLines);
            try
            {
                Watch(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense lines: watching a raid", e);
                raidOn = false;
            }
        }

        private List<Pawn> Raiders()
        {
            var list = new List<Pawn>();
            var targets = map.attackTargetsCache?.TargetsHostileToColony;
            if (targets == null) return list;
            foreach (var t in targets)
            {
                if (!(t?.Thing is Pawn p) || !p.Spawned || p.Dead || p.Downed || p.IsPrisoner) continue;
                if (p.RaceProps == null || !(p.RaceProps.Humanlike || p.RaceProps.IsMechanoid)) continue;
                if (p.Faction == null || !p.Faction.HostileTo(Faction.OfPlayer)) continue;
                list.Add(p);
            }
            return list;
        }

        private DefenseLineRaid RaidFor(DefenseLine line)
        {
            var r = running.FirstOrDefault(x => x.line == line.id);
            if (r == null)
            {
                r = new DefenseLineRaid { line = line.id, tick = raidStart };
                running.Add(r);
            }
            return r;
        }

        private void Watch(int now)
        {
            if (!map.IsPlayerHome) return;
            var raiders = MapFacts.ThreatOn(map) ? Raiders() : new List<Pawn>();
            if (!raidOn)
            {
                if (raiders.Count == 0 || lines.Count == 0) return;
                raidOn = true;
                raidStart = now;
                quiet = 0;
                reminded = false;
                running.Clear();
                tracks.Clear();
                downedSeen.Clear();
                beasts.Clear();
                string faction = raiders.Select(p => p.Faction?.Name).FirstOrDefault(n => !n.NullOrEmpty());
                // The weather the raid comes in, and the aim it leaves shots in the open (the game's own factor).
                var wm = map.weatherManager;
                int aim = DefenseLineMath.AimPercent(wm?.CurWeatherAccuracyMultiplier ?? 1f);
                string weather = wm?.curWeather?.label;
                foreach (var l in lines)
                {
                    var r = RaidFor(l);
                    r.faction = faction;
                    r.aim = aim;
                    r.weather = weather;
                }
                WeatherLook(aim, weather);
                Remind(raiders);
            }
            if (raiders.Count == 0)
            {
                if (++quiet >= 2) Finish();
                return;
            }
            quiet = 0;
            foreach (var p in raiders) Follow(p, now);
            Downed();
            Animals(now);
        }

        private void Follow(Pawn p, int now)
        {
            var pos = p.Position;
            bool fresh = !tracks.TryGetValue(p.thingIDNumber, out var tr);
            if (fresh)
            {
                tr = new Track { last = pos };
                tracks[p.thingIDNumber] = tr;
            }
            var home = map.areaManager.Home;
            // First seen after the raid's landing (a late band, or the raid followed again after a load): one still in
            // front of a line, however close, came from outside.
            bool late = fresh && now - raidStart > LandedWithin;
            foreach (var line in lines)
            {
                if (tr.counted.Contains(line.id)) continue;
                int ahead = line.Ahead(pos), lat = line.Lateral(pos);
                if (ahead >= 2 || (late && ahead >= 0))
                {
                    tr.outside.Add(line.id);
                    continue;
                }
                bool behind = ahead <= -1 && (Mathf.Abs(lat) <= 30 || home[pos]);
                if (!behind) continue;
                var raid = RaidFor(line);
                if (tr.outside.Contains(line.id))
                {
                    if (DefenseLineMath.PassedNear(tr.last.x, tr.last.z, pos.x, pos.z, line.gate.x, line.gate.z, 3.5f) || pos.DistanceTo(line.gate) <= 4f) raid.through++;
                    else if (Breaching(p)) raid.broke++;
                    else if (lat < 0) raid.roundLeft++;
                    else raid.roundRight++;
                    tr.counted.Add(line.id);
                }
                else if (fresh && now - raidStart <= LandedWithin && home[pos])
                {
                    raid.inside++;
                    tr.counted.Add(line.id);
                }
            }
            tr.last = pos;
        }

        /// <summary>Sappers dig and breachers smash their own way in: their raid's current stage says so.</summary>
        private static bool Breaching(Pawn p)
        {
            try
            {
                string toil = p.GetLord()?.CurLordToil?.GetType().Name;
                return toil != null && (toil.IndexOf("Sapper", StringComparison.OrdinalIgnoreCase) >= 0 || toil.IndexOf("Breach", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                return false;
            }
        }

        private void Downed()
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (!p.Downed || downedSeen.Contains(p.thingIDNumber)) continue;
                downedSeen.Add(p.thingIDNumber);
                var pos = p.Position;
                var line = lines.Where(l => l.gate.DistanceTo(pos) <= 30f).OrderBy(l => l.gate.DistanceTo(pos)).FirstOrDefault();
                if (line == null) continue;
                var raid = RaidFor(line);
                string where;
                int ahead = line.Ahead(pos), lat = line.Lateral(pos);
                var cover = GenAdj.CellsAdjacent8Way(new TargetInfo(pos, map)).Select(c => c.InBounds(map) ? c.GetEdifice(map) : null)
                    .FirstOrDefault(b => b != null && Avail.IsCover(b.def));
                if (pos.DistanceTo(line.gate) <= 3f)
                {
                    raid.downedGate++;
                    where = "CC_DefLine_At_Gate".Translate();
                }
                else if (ahead >= 1)
                {
                    raid.downedFront++;
                    where = "CC_DefLine_At_Front".Translate();
                }
                else if (cover != null)
                {
                    if (lat < 0) raid.downedCoverLeft++;
                    else raid.downedCoverRight++;
                    where = (lat < 0 ? "CC_DefLine_At_CoverLeft" : "CC_DefLine_At_CoverRight").Translate(cover.def.label).Resolve();
                }
                else if (ahead >= -12 && Mathf.Abs(lat) <= 8)
                {
                    raid.downedZone++;
                    where = "CC_DefLine_At_Zone".Translate();
                }
                else
                {
                    raid.downedBehind++;
                    where = "CC_DefLine_At_Behind".Translate();
                }
                if (raid.downed.Count < 4) raid.downed.Add("CC_DefLine_DownedAt".Translate(p.Named("PAWN"), where).Resolve());
            }
        }

        // ---------------- The colony's animals at the lines ----------------

        private static int Rec(Pawn a, RecordDef r) => a?.records == null || r == null ? 0 : a.records.GetAsInt(r);

        /// <summary>The line whose gate is nearest a cell, within 30 cells of it, or null.</summary>
        private DefenseLine NearestLine(IntVec3 pos) => lines.Where(l => l.gate.DistanceTo(pos) <= 30f).OrderBy(l => l.gate.DistanceTo(pos)).FirstOrDefault();

        /// <summary>
        /// The colony's animals, every look: each one's records when first seen in the raid; the line it fights at (it went
        /// for a raider since the last look); and one downed or killed near a line, counted there once.
        /// </summary>
        private void Animals(int now)
        {
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (a?.RaceProps == null || !a.RaceProps.Animal) continue;
                if (!beasts.TryGetValue(a.thingIDNumber, out var b))
                {
                    b = new Beast { pawn = a, downs = Rec(a, RecordDefOf.PawnsDowned), kills = Rec(a, RecordDefOf.Kills) };
                    beasts[a.thingIDNumber] = b;
                }
                b.last = a.Position;
                var ms = a.mindState;
                if (ms != null && ms.lastAttackTargetTick >= now - 300 && ms.lastAttackedTarget.Thing is Pawn t && t.HostileTo(Faction.OfPlayer))
                {
                    var line = NearestLine(a.Position);
                    if (line != null) b.line = line.id;
                }
            }
            foreach (var b in beasts.Values)
            {
                if (b.fell || b.pawn == null || !(b.pawn.Dead || b.pawn.Downed)) continue;
                b.fell = true;
                var line = NearestLine(b.last);
                if (line != null) RaidFor(line).animalsDowned++;
            }
        }

        /// <summary>
        /// The raid is over: each animal that fought at a line is counted there with the enemies it brought down; one that
        /// brought any down has it on its card (its temperament's "lately"), and its person writes it down, unless the fight
        /// came to them (the pets' own heroics say that, Strangers/Pets.cs).
        /// </summary>
        private void AnimalsAfter()
        {
            var kinds = new Dictionary<int, HashSet<string>>();
            foreach (var b in beasts.Values)
            {
                var a = b.pawn;
                if (a == null || b.line < 0) continue;
                var line = Get(b.line);
                if (line == null) continue;
                var raid = RaidFor(line);
                int brought = DefenseLineMath.Brought(b.downs, Rec(a, RecordDefOf.PawnsDowned), b.kills, Rec(a, RecordDefOf.Kills));
                raid.animalsFought++;
                raid.animalsBrought += brought;
                if (!kinds.TryGetValue(line.id, out var k)) kinds[line.id] = k = new HashSet<string>();
                k.Add(AnimalFacts.KindLabel(a));
                if (brought <= 0 || a.Dead || a.Downed || a.Faction != Faction.OfPlayer) continue;
                string name = DefenseLineReview.NameOf(line, map);
                try
                {
                    AnimalTempers.Noted(a, "CC_DefLine_AnimalNoted", name.Named("LINE"), brought.Named("COUNT"));
                    var person = PetTracker.PersonOf(a);
                    bool harmed = person != null && (person.Downed || person.mindState != null && person.mindState.lastHarmTick >= raidStart);
                    if (person != null && !harmed) CouncilGameComponent.Instance?.journals.Write(person, "Rimfolk_Journal_AnimalAtLine", null, a.Named("PET"), name.Named("LINE"));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("defense lines: an animal at the line", e);
                }
            }
            foreach (var kv in kinds)
            {
                var r = running.FirstOrDefault(x => x.line == kv.Key);
                if (r != null && kv.Value.Count == 1) r.animalKind = kv.Value.First();
            }
            beasts.Clear();
        }

        // ---------------- The raid's first look: the weather ----------------

        /// <summary>The raid comes in weather that takes aim off the shooting: said once, with what it does to both sides.</summary>
        private void WeatherLook(int aim, string weather)
        {
            if (!DefenseLineMath.AimLost(aim) || weather.NullOrEmpty()) return;
            Notify.Message("CC_DefLine_WeatherRaid".Translate(weather.Named("WEATHER"), (aim + "%").Named("AIM")).Resolve(), null, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        /// <summary>
        /// Dev: the first line's last raid as if one had just ended there with the colony's animals in it (three fought, four
        /// brought down, none fell) in today's weather, so its lessons and notes show on the card; and the raid's word on the
        /// weather, when today's takes aim off the shooting.
        /// </summary>
        internal string DevRaidNotes()
        {
            var line = lines.FirstOrDefault();
            if (line == null) return "no defense line on this map.";
            var wm = map.weatherManager;
            int aim = DefenseLineMath.AimPercent(wm?.CurWeatherAccuracyMultiplier ?? 1f);
            var raid = new DefenseLineRaid
            {
                line = line.id, tick = Find.TickManager.TicksGame, through = 6, animalsFought = 3, animalsBrought = 4, aim = aim, weather = wm?.curWeather?.label,
                animalKind = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault()?.kindDef?.label
            };
            raid.lessons = DefenseLineMath.Lessons(raid.Tally).Select(l => l.ToString()).ToList();
            line.raids.Add(raid);
            if (line.raids.Count > DefenseLine.MaxRaids) line.raids.RemoveRange(0, line.raids.Count - DefenseLine.MaxRaids);
            line.Changed();
            WeatherLook(aim, raid.weather);
            return "a raid's notes added to " + DefenseLineReview.NameOf(line, map) + " (" + string.Join(", ", raid.lessons) + "); the weather takes the aim to " + aim + "%.";
        }

        // ---------------- The raid's first look: a gate left shut ----------------

        private void Remind(List<Pawn> raiders)
        {
            if (reminded || !(ColonyCouncilMod.Settings?.defenseGateReminder ?? false)) return;
            reminded = true;
            // A raid that came down inside every line has no use for their gates.
            if (raiders.All(p => map.areaManager.Home[p.Position])) return;
            var shut = new List<(DefenseLine line, Building_Door door)>();
            foreach (var l in lines)
            {
                var door = l.gate.InBounds(map) ? l.gate.GetEdifice(map) as Building_Door : null;
                if (door == null || door.HoldOpen || door.Faction != Faction.OfPlayer) continue;
                // The review kept from the page, if there is one (nothing is worked out on the tick): a gate in a line
                // with a way round, or a gap still open, isn't what holds them anyway.
                var review = DefenseLineReview.Kept(map, l);
                if (review != null && (!review.f.Closed || review.f.Leaks)) continue;
                shut.Add((l, door));
            }
            if (shut.Count == 0) return;
            string names = TextUtil.JoinAnd(shut.Select(x => DefenseLineReview.NameOf(x.line, map)).ToList());
            Notify.Message("CC_DefLine_Remind".Translate(names).Resolve(), new LookTargets(shut.Select(x => (Thing)x.door)), MessageTypeDefOf.CautionInput, Importance.Important);
        }

        // ---------------- The raid is over ----------------

        private void Finish()
        {
            raidOn = false;
            AnimalsAfter();
            DefenseLine busiest = null;
            DefenseLineRaid busiestRaid = null;
            foreach (var raid in running)
            {
                var line = Get(raid.line);
                if (line == null || raid.Empty) continue;
                raid.lessons = DefenseLineMath.Lessons(raid.Tally).Select(l => l.ToString()).ToList();
                line.raids.Add(raid);
                if (line.raids.Count > DefenseLine.MaxRaids) line.raids.RemoveRange(0, line.raids.Count - DefenseLine.MaxRaids);
                line.Changed();
                if (busiestRaid == null || raid.Tally.Raiders + raid.Tally.Downed > busiestRaid.Tally.Raiders + busiestRaid.Tally.Downed)
                {
                    busiest = line;
                    busiestRaid = raid;
                }
            }
            running.Clear();
            tracks.Clear();
            downedSeen.Clear();
            if (busiest != null)
                Notify.Message("CC_DefLine_RaidOver".Translate(DefenseLineReview.NameOf(busiest, map), Summary(busiest, busiestRaid, map)).Resolve(),
                    new LookTargets(busiest.gate, map), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            // The line held (they came through the gate, and nobody fell): a tale of the game's for the colony's art, with the
            // one of the colony standing nearest its gate (Things/ArtTales.cs).
            if (busiest != null && ArtTalesMath.Held(busiestRaid.lessons))
            {
                var by = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.Dead).OrderByDescending(p => p.Drafted)
                    .ThenBy(p => p.Position.DistanceToSquared(busiest.gate)).FirstOrDefault();
                if (by != null) ArtTales.Record(ArtTales.LineHeld, by, null, null, DefenseLineReview.NameOf(busiest, map));
                // The gate may take a name from it: "the gate that held" (Places/PlaceNames.cs).
                if (by != null) PlaceNames.LineHeld(map, busiest.gate, by);
            }
        }

        // ---------------- Words ----------------

        /// <summary>"9 came through the gate, 3 went round to the west; Hana was downed at the sandbags on the left."</summary>
        public static string Summary(DefenseLine line, DefenseLineRaid raid, Map map)
        {
            var parts = new List<string>();
            if (raid.through > 0) parts.Add("CC_DefLine_Raid_Through".Translate(raid.through).Resolve());
            DefenseLineMath.Side(line.facing, out int sx, out int sz);
            if (raid.roundLeft > 0) parts.Add("CC_DefLine_Raid_Round".Translate(raid.roundLeft, DefenseSurvey.FacingWord(DefenseLineMath.FacingOf(-sx, -sz))).Resolve());
            if (raid.roundRight > 0) parts.Add("CC_DefLine_Raid_Round".Translate(raid.roundRight, DefenseSurvey.FacingWord(DefenseLineMath.FacingOf(sx, sz))).Resolve());
            if (raid.broke > 0) parts.Add("CC_DefLine_Raid_Broke".Translate(raid.broke).Resolve());
            if (raid.inside > 0) parts.Add("CC_DefLine_Raid_Inside".Translate(raid.inside).Resolve());
            string s = parts.Count > 0 ? string.Join(", ", parts) : "CC_DefLine_Raid_NoneNear".Translate().ToString();
            if (raid.downed.Count > 0) s += "; " + TextUtil.JoinAnd(raid.downed);
            if (raid.animalsFought > 0)
                s += "; " + (raid.animalsDowned > 0 ? "CC_DefLine_Raid_AnimalsFell" : "CC_DefLine_Raid_Animals")
                    .Translate(raid.animalsFought.Named("COUNT"), raid.animalsBrought.Named("BROUGHT"), raid.animalsDowned.Named("FELL")).Resolve();
            return s.CapitalizeFirst() + ".";
        }

        /// <summary>The animals in a lesson: "the dogs" when they were all one kind, else "the animals".</summary>
        private static string AnimalsWord(DefenseLineRaid raid) =>
            raid.animalKind.NullOrEmpty() ? "CC_DefLine_TheAnimals".Translate().Resolve() : "CC_DefLine_TheKind".Translate(Find.ActiveLanguageWorker.Pluralize(raid.animalKind).Named("KIND")).Resolve();

        /// <summary>What a lesson says, in the line's terms.</summary>
        public static string LessonText(DefenseLine line, DefenseLineRaid raid, string lesson)
        {
            DefenseLineMath.Side(line.facing, out int sx, out int sz);
            switch (lesson)
            {
                case nameof(DefenseLineMath.Lesson.Held): return "CC_DefLine_Lesson_Held".Translate();
                case nameof(DefenseLineMath.Lesson.Leak):
                {
                    bool left = raid.roundLeft >= raid.roundRight;
                    string side = left ? DefenseSurvey.FacingWord(DefenseLineMath.FacingOf(-sx, -sz)) : DefenseSurvey.FacingWord(DefenseLineMath.FacingOf(sx, sz));
                    return "CC_DefLine_Lesson_Leak".Translate(side).Resolve();
                }
                case nameof(DefenseLineMath.Lesson.Harden): return "CC_DefLine_Lesson_Harden".Translate();
                case nameof(DefenseLineMath.Lesson.CoverLeft): return "CC_DefLine_Lesson_Cover".Translate("CC_DefLine_Left".Translate()).Resolve();
                case nameof(DefenseLineMath.Lesson.CoverRight): return "CC_DefLine_Lesson_Cover".Translate("CC_DefLine_Right".Translate()).Resolve();
                case nameof(DefenseLineMath.Lesson.StayBehind): return "CC_DefLine_Lesson_StayBehind".Translate();
                case nameof(DefenseLineMath.Lesson.Fallback): return "CC_DefLine_Lesson_Fallback".Translate();
                case nameof(DefenseLineMath.Lesson.AnimalsBack):
                    return "CC_DefLine_Lesson_AnimalsBack".Translate(AnimalsWord(raid).Named("ANIMALS"), raid.animalsDowned.Named("COUNT")).Resolve();
                case nameof(DefenseLineMath.Lesson.AnimalsHeld):
                    return "CC_DefLine_Lesson_AnimalsHeld".Translate(AnimalsWord(raid).CapitalizeFirst().Named("ANIMALS"), raid.animalsBrought.Named("COUNT")).Resolve();
                case nameof(DefenseLineMath.Lesson.Weather):
                    return raid.weather.NullOrEmpty() ? null : "CC_DefLine_Lesson_Weather".Translate(raid.weather.Named("WEATHER"), (raid.aim + "%").Named("AIM")).Resolve();
                default: return null;
            }
        }
    }
}
