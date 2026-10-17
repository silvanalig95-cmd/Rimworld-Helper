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
    /// inside (drop pods). Colonists downed near a line are noted with the place. When the raid is over, each line it
    /// touched keeps one to three lessons, and one message says how the busiest line did. With the setting on, the
    /// raid's first look also says, once, which lines have their gate shut.
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
            public long outMask, countedMask;
        }

        private readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();

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
                string faction = raiders.Select(p => p.Faction?.Name).FirstOrDefault(n => !n.NullOrEmpty());
                foreach (var l in lines) RaidFor(l).faction = faction;
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
            for (int i = 0; i < lines.Count && i < 60; i++)
            {
                var line = lines[i];
                long bit = 1L << i;
                if ((tr.countedMask & bit) != 0) continue;
                int ahead = line.Ahead(pos), lat = line.Lateral(pos);
                if (ahead >= 2)
                {
                    tr.outMask |= bit;
                    continue;
                }
                bool behind = ahead <= -1 && (Mathf.Abs(lat) <= 30 || home[pos]);
                if (!behind) continue;
                var raid = RaidFor(line);
                if ((tr.outMask & bit) != 0)
                {
                    if (DefenseLineMath.PassedNear(tr.last.x, tr.last.z, pos.x, pos.z, line.gate.x, line.gate.z, 3.5f) || pos.DistanceTo(line.gate) <= 4f) raid.through++;
                    else if (Breaching(p)) raid.broke++;
                    else if (lat < 0) raid.roundLeft++;
                    else raid.roundRight++;
                    tr.countedMask |= bit;
                }
                else if (fresh && now - raidStart <= LandedWithin && home[pos])
                {
                    raid.inside++;
                    tr.countedMask |= bit;
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
            return s.CapitalizeFirst() + ".";
        }

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
                default: return null;
            }
        }
    }
}
