using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Days that come from what the colony lived through: the day a camp stayed and left something behind (a ring of
    /// saplings, a carved bird, a backdrop: the camp says so when it leaves it, Camps/CampLook.cs), the anniversary of a
    /// world event the colony took part in (read from its history, with the date it ended), the holidays of the
    /// colony's own faith (its rites held on a date, with Ideology), and the day one of the colony's people was brought
    /// home from captivity. Each has a quiet end: the gift gone from the map, nobody left who lived through the world
    /// event, the rite no longer the faith's, the one brought home gone. And a way to keep any day by walking out to
    /// what it remembers (<see cref="DayWay.Walk"/>): to the gift, the grave, the fields, the gate.
    /// </summary>
    public partial class CalendarTracker
    {
        // ---------------- Saved ----------------

        /// <summary>World events that ended up to this tick have been weighed for the calendar.</summary>
        private int situationsSeenTick = -1;
        /// <summary>The colony's people carried off, by ThingID: "tick|the captors' name", until they're home or lost to us.</summary>
        private Dictionary<string, string> captives = new Dictionary<string, string>();

        private List<string> tmpKeys6, tmpVals6;

        private void ExposeLived()
        {
            Scribe_Values.Look(ref situationsSeenTick, "situationsSeenTick", -1);
            Scribe_Collections.Look(ref captives, "captives", LookMode.Value, LookMode.Value, ref tmpKeys6, ref tmpVals6);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && captives == null) captives = new Dictionary<string, string>();
        }

        private void WatchLived(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            Safe("calendar world events", () => WatchSituations(map, known, today, year));
            Safe("calendar faith days", () => WatchFaithDays(today, year));
            Safe("calendar rescues", () => WatchRescues(map, known, today, year));
        }

        // ---------------- A camp's gift ----------------

        /// <summary>
        /// A camp left something behind at the end of its stay (from Camp.LeaveGifts and the farewell walk): the day goes on
        /// the calendar, kept by walking out to it, and the colonist who was closest to the camp writes it down.
        /// </summary>
        public static void Notify_CampGift(Camp c, CampGift g, List<Thing> placed, IntVec3 at)
        {
            var self = Instance;
            if (self == null || c == null || g == null || placed.NullOrEmpty()) return;
            try
            {
                var map = Home;
                if (map == null || c.map != map) return;
                string id = "camp|" + c.id + "|" + c.arrivedTick;
                var have = self.Get(id);
                if (have != null)
                {
                    // A second gift of the same stay (a sprig by the gate as well as the ring): the same day.
                    foreach (var t in placed.Where(t => t != null && !have.things.Contains(t.ThingID)).Take(CalendarMath.MaxDayThings - have.things.Count))
                        have.things.Add(t.ThingID);
                    return;
                }
                var friends = c.warmth.Where(kv => kv.Key != null && kv.Value > 0 && Ours(kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
                var friend = friends.FirstOrDefault();
                var leader = c.leader ?? c.members.FirstOrDefault();
                int today = GenLocalDate.DayOfYear(map);
                int year = GenLocalDate.Year(map);
                var d = Dated(new CalendarDay
                {
                    id = id, kind = CalendarKind.CampGift, way = DefaultWay(CalendarKind.CampGift), about = g.key, place = at,
                    subject = !g.named.NullOrEmpty() ? g.named : placed[0].def.label, enemy = c.Label, otherName = leader?.LabelShort,
                    pawnId = friend?.ThingID, pawnName = friend?.LabelShort, kills = Mathf.Max(1, Mathf.RoundToInt(c.DaysHere)),
                    things = placed.Where(t => t != null).Select(t => t.ThingID).Take(CalendarMath.MaxDayThings).ToList(),
                    knew = friends.Select(p => p.ThingID).ToList(),
                    weight = CalendarMath.CampGiftWeight(c.warmth.Values.Sum(), c.helps + c.gifts, c.DaysHere)
                }, Find.TickManager.TicksGame, map, today, year, true);
                if (!g.dayName.NullOrEmpty()) d.label = g.dayName;
                if (self.TryAdd(d, true) != null && friend != null) self.JournalNew(friend, "Council_Journal_Cal_CampGift", d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar camp gift", e);
            }
        }

        /// <summary>
        /// What a day remembers on the map, if it's still there: the gift near where it was left (saplings, a backdrop), or
        /// anywhere among the colony's art and crates (a carving set up indoors, or still packed). The crate itself when packed.
        /// </summary>
        public static Thing DayThing(CalendarDay d, Map map)
        {
            if (d?.things == null || d.things.Count == 0 || map == null) return null;
            var ids = new HashSet<string>(d.things);
            bool Is(Thing t) => t != null && (ids.Contains(t.ThingID) || (t is MinifiedThing m && m.InnerThing != null && ids.Contains(m.InnerThing.ThingID)));
            if (d.place.IsValid && d.place.InBounds(map))
                foreach (var t in GenRadial.RadialDistinctThingsAround(d.place, map, 10f, true))
                    if (Is(t)) return t;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Art))
                if (Is(t)) return t;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                if (Is(t)) return t;
            return null;
        }

        /// <summary>The gift a day remembers is gone from the map (cut down, sold, burned) for three looks running.</summary>
        private bool GiftGone(CalendarDay d)
        {
            var map = Home;
            if (map == null || d.things.Count == 0) return false;
            string key = "gift|" + d.id;
            if (DayThing(d, map) != null)
            {
                missing.Remove(key);
                return false;
            }
            missing.TryGetValue(key, out int n);
            missing[key] = ++n;
            return n >= 3;
        }

        // ---------------- A world event the colony took part in ----------------

        /// <summary>
        /// Once a day: a world event that has ended, and that the colony took part in (its answers counted as help), goes on
        /// the calendar on the date it ended, remembered by those who were with the colony then. An older save's history
        /// has its dates too, so those days are written down as they fell.
        /// </summary>
        private void WatchSituations(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            var history = WorldSituations.Instance?.history;
            if (history == null) return;
            int now = Find.TickManager.TicksGame;
            int newest = situationsSeenTick;
            foreach (var r in history.ToList())
            {
                if (r == null || r.endTick <= situationsSeenTick) continue;
                newest = Mathf.Max(newest, r.endTick);
                if (!CalendarMath.TookPart(r.help, r.helped)) continue;
                string id = "ws|" + r.defName + "|" + r.startTick;
                if (Get(id) != null) continue;
                var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "");
                int ago = Mathf.Max(0, now - r.endTick);
                var knew = known.Values.Where(p => !p.Dead && Ours(p) && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) + GenDate.TicksPerDay >= ago)
                    .Select(p => p.ThingID).ToList();
                if (knew.Count == 0) continue;
                bool fresh = ago < GenDate.TicksPerDay * 2;
                string who = r.faction?.Name ?? "";
                var d = Dated(new CalendarDay
                {
                    id = id, kind = CalendarKind.Situation, way = SituationWay(def?.kind), subject = r.Called, otherName = who, enemy = who, about = def?.kind,
                    kills = r.help, knew = knew, weight = CalendarMath.SituationWeight(r.help, r.helped)
                }, Mathf.Max(0, r.endTick), map, today, year, fresh);
                if (TryAdd(d, fresh) != null && fresh)
                    JournalNew(AtHome(map).Where(p => !p.DevelopmentalStage.Juvenile() && knew.Contains(p.ThingID)).RandomElementWithFallback(), "Council_Journal_Cal_Situation", d);
            }
            situationsSeenTick = newest;
        }

        /// <summary>How a world event's day is kept: a supper for a hard year come through, a walk to the gate for a war or a plague, a fire for a sign in the sky.</summary>
        public static DayWay SituationWay(string kind)
        {
            switch (kind)
            {
                case "sickness":
                case "mourning":
                case "mourned":
                case "war":
                case "fire":
                case "weather":
                case "water":
                case "exile":
                case "sheltered":
                case "levy":
                case "sellswords":
                    return DayWay.Walk;
                case "comet":
                case "sky":
                    return DayWay.Bonfire;
                case "fair":
                case "songs":
                    return DayWay.Games;
                default:
                    return DayWay.Feast;
            }
        }

        // ---------------- The faith's own days ----------------

        /// <summary>
        /// Once a day, with Ideology: each rite of the colony's own faith that is held on a date (the game's date rituals)
        /// is a day on the calendar, on that date; its letter offers the rite itself. A rite the faith no longer has, or a
        /// faith the colony no longer holds, ends its day quietly.
        /// </summary>
        private void WatchFaithDays(int today, int year)
        {
            var live = new HashSet<string>();
            Ideo ideo = null;
            if (ModsConfig.IdeologyActive && Find.IdeoManager != null && !Find.IdeoManager.classicMode) ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo != null)
                foreach (var r in ideo.PreceptsListForReading.OfType<Precept_Ritual>().ToList())
                {
                    var date = r.obligationTriggers?.OfType<RitualObligationTrigger_Date>().FirstOrDefault();
                    if (date == null) continue;
                    string id = "faith|" + r.GetUniqueLoadID();
                    live.Add(id);
                    int day = CalendarMath.Wrap(date.triggerDaysSinceStartOfYear);
                    string rite = r.LabelCap.ToString();
                    var have = Get(id);
                    if (have != null)
                    {
                        have.day = day;
                        have.subject = ideo.name;
                        have.otherName = rite;
                        continue;
                    }
                    // The faith kept its day before the calendar knew of it: it counts as an old day, not a new one.
                    var d = new CalendarDay
                    {
                        id = id, kind = CalendarKind.FaithDay, way = DefaultWay(CalendarKind.FaithDay), subject = ideo.name, otherName = rite, about = r.def?.defName,
                        day = day, sinceYear = year - 1, tick = Find.TickManager.TicksGame, weight = CalendarMath.FaithDayWeight,
                        lastYear = CalendarMath.InitialLastYear(today, year, day, false)
                    };
                    TryAdd(d, true);
                }
            foreach (var d in days.Where(x => x.kind == CalendarKind.FaithDay && !x.tradition && !live.Contains(x.id)).ToList()) days.Remove(d);
        }

        /// <summary>The faith's own rite a faith day names, if the colony's faith still has it.</summary>
        public static Precept_Ritual FaithRite(CalendarDay d)
        {
            if (d?.kind != CalendarKind.FaithDay || !ModsConfig.IdeologyActive || d.id == null) return null;
            try
            {
                var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                return ideo?.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(r => "faith|" + r.GetUniqueLoadID() == d.id);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar faith rite", e);
                return null;
            }
        }

        // ---------------- Brought home ----------------

        /// <summary>
        /// Once a day: the colony's people carried off by raiders are noted; when one is home again and ours (rescued,
        /// ransomed, escaped), the day goes on the calendar, with how long they were gone and who held them.
        /// </summary>
        private void WatchRescues(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                var list = f?.kidnapped?.KidnappedPawnsListForReading;
                if (list == null) continue;
                foreach (var p in list)
                    if (p != null && !p.Dead && p.RaceProps?.Humanlike == true && p.Faction == Faction.OfPlayer && People.HasInnerLife(p) && !captives.ContainsKey(p.ThingID))
                        captives[p.ThingID] = now + "|" + (f.Name ?? "");
            }
            foreach (var kv in captives.ToList())
            {
                if (!known.TryGetValue(kv.Key, out var p) || p.Dead)
                {
                    captives.Remove(kv.Key);
                    continue;
                }
                if (People.Kidnapped(p)) continue;
                // Taken into their captors' ranks, or lost to the colony some other way.
                if (!Ours(p))
                {
                    if (!p.IsPrisoner) captives.Remove(kv.Key);
                    continue;
                }
                // Free, but still on the road home.
                if (!(p.MapHeld?.IsPlayerHome ?? false)) continue;
                captives.Remove(kv.Key);
                int bar = kv.Value.IndexOf('|');
                if (bar <= 0 || !int.TryParse(kv.Value.Substring(0, bar), out int since)) continue;
                string captors = kv.Value.Substring(bar + 1);
                float gone = (now - since) / (float)GenDate.TicksPerDay;
                if (gone < 1f) continue;
                AddRescue(map, p, captors, gone, today, year);
            }
        }

        private void AddRescue(Map map, Pawn p, string captors, float gone, int today, int year)
        {
            int now = Find.TickManager.TicksGame;
            var friends = AtHome(map).Where(o => o != p && !o.DevelopmentalStage.Juvenile() && o.relations != null && o.relations.OpinionOf(p) >= 15)
                .OrderByDescending(o => o.relations.OpinionOf(p)).ToList();
            var d = Dated(new CalendarDay
            {
                id = "rescue|" + p.ThingID + "|" + now, kind = CalendarKind.Rescue, way = DefaultWay(CalendarKind.Rescue), pawnId = p.ThingID, pawnName = p.LabelShort,
                enemy = captors, otherName = captors, kills = Mathf.RoundToInt(gone), knew = new[] { p }.Concat(friends).Select(o => o.ThingID).ToList(),
                weight = CalendarMath.RescueWeight(gone)
            }, now, map, today, year, true);
            if (TryAdd(d, true) == null) return;
            JournalNew(p, "Council_Journal_Cal_Rescue", d);
            var friend = friends.FirstOrDefault();
            if (friend != null && On)
                CouncilGameComponent.Instance?.journals.WriteTagged(friend, "Council_Journal_Cal_Rescue", new[] { "new", "friend" }, null, d.label.Named("NAME"), 0.Named("YEARS"),
                    (d.subject ?? "").Named("SUBJECT"), d.kills.Named("COUNT"), (Home?.Biome?.label ?? "").Named("PLACE"), p.Named("OTHER"), captors.Named("WHO"));
        }

        // ---------------- Walking out to it ----------------

        /// <summary>Where a walk on a day goes when there's nothing of it on the map to walk to.</summary>
        private static PlaySpot WalkTo(CalendarKind kind)
        {
            switch (kind)
            {
                case CalendarKind.Harvest:
                case CalendarKind.NewCrop:
                    return PlaySpot.Fields;
                case CalendarKind.Walls:
                case CalendarKind.Battle:
                case CalendarKind.Homecoming:
                case CalendarKind.Rescue:
                case CalendarKind.Situation:
                case CalendarKind.FirstTrade:
                case CalendarKind.Founding:
                case CalendarKind.CampGift:
                    return PlaySpot.Gate;
                default:
                    return PlaySpot.Walk;
            }
        }

        /// <summary>
        /// The colony walks out together to what the day remembers (with answers playing out): to the gift a camp left, to
        /// the grave, to the fields for a harvest, to the gate for the walls or a homecoming, else a long walk out. Those who
        /// went have a good hour of it; with nothing to set going, everyone at home has it.
        /// </summary>
        private bool WalkDay(CalendarDay d, Map map, List<Pawn> home)
        {
            var keeper = Interplay.CalendarOrganizer(d, home) ?? Keeper(d, home);
            Thing place = DayThing(d, map);
            var to = PlaySpot.Place;
            if (place == null && (d.kind == CalendarKind.Death || d.kind == CalendarKind.Companion)) place = PlaySpots.GraveOf(map, d.pawnId);
            if (place == null) to = WalkTo(d.kind);
            if (PlayOuts.On && home.Count > 0)
            {
                try
                {
                    var walk = new DilemmaEffect_Procession { to = to, label = d.label, count = Mathf.Min(8, home.Count), duration = new FloatRange(1.5f, 2f) };
                    var plan = walk.Begin(new HappeningContext { map = map, a = keeper, place = place, why = d.label });
                    if (plan != null)
                    {
                        foreach (var x in plan.invited)
                            plan.moods.Add(new HeldMood { pawn = x, thought = CalendarDefOf.Council_Cal_WalkDay, cause = Causes.Current });
                        return true;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("calendar walk", e);
                }
            }
            foreach (var p in home) CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_WalkDay);
            return false;
        }
    }
}
