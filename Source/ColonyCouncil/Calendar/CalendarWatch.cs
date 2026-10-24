using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The newer kinds of day, watched for once a day: the first child born into the colony, the day its walls went up,
    /// a caravan home safe from a long journey, the first harvest of a crop it had never grown, its first trade, and a
    /// great hunt. Each is written down from what the game already counts (records, the stores, the map), dated as near
    /// as the daily look allows. What happened before the calendar first looked (an older save's walls, its trades) is
    /// left alone rather than given a date it never had.
    /// </summary>
    public partial class CalendarTracker
    {
        // ---------------- Saved ----------------

        private bool firstChildSeen;
        private bool wallsSeen;
        private bool wallsLooked;
        /// <summary>The colony's people away from home, by ThingID: since when.</summary>
        private Dictionary<string, int> awaySince = new Dictionary<string, int>();
        /// <summary>Crops (plant defNames) the colony has grown, whether it has looked yet, and new ones waiting for their first harvest (and the stock then).</summary>
        private HashSet<string> knownCrops = new HashSet<string>();
        private bool cropsLooked;
        private Dictionary<string, int> pendingCrops = new Dictionary<string, int>();
        /// <summary>The colony's first trade: -1 not looked yet, -2 done (or traded before the calendar looked), else 0.</summary>
        private int tradesSeen = -1;
        /// <summary>Each colonist's animal kills yesterday, for a great hunt.</summary>
        private Dictionary<string, int> huntKills = new Dictionary<string, int>();

        private List<string> tmpKeys3, tmpKeys4, tmpKeys5;
        private List<int> tmpInts3, tmpInts4, tmpInts5;

        private void ExposeWatch()
        {
            Scribe_Values.Look(ref firstChildSeen, "firstChildSeen");
            Scribe_Values.Look(ref wallsSeen, "wallsSeen");
            Scribe_Values.Look(ref wallsLooked, "wallsLooked");
            Scribe_Collections.Look(ref awaySince, "awaySince", LookMode.Value, LookMode.Value, ref tmpKeys3, ref tmpInts3);
            Scribe_Collections.Look(ref knownCrops, "knownCrops", LookMode.Value);
            Scribe_Values.Look(ref cropsLooked, "cropsLooked");
            Scribe_Collections.Look(ref pendingCrops, "pendingCrops", LookMode.Value, LookMode.Value, ref tmpKeys4, ref tmpInts4);
            Scribe_Values.Look(ref tradesSeen, "tradesSeen", -1);
            Scribe_Collections.Look(ref huntKills, "huntKills", LookMode.Value, LookMode.Value, ref tmpKeys5, ref tmpInts5);
            ExposeLived();
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (awaySince == null) awaySince = new Dictionary<string, int>();
            if (knownCrops == null) knownCrops = new HashSet<string>();
            if (pendingCrops == null) pendingCrops = new Dictionary<string, int>();
            if (huntKills == null) huntKills = new Dictionary<string, int>();
        }

        private void WatchMore(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            Safe("calendar first child", () => WatchFirstChild(map, known, today, year));
            Safe("calendar walls", () => WatchWalls(map, today, year));
            Safe("calendar homecoming", () => WatchHomecoming(map, known, today, year));
            Safe("calendar new crop", () => WatchCrops(map, today, year));
            Safe("calendar first trade", () => WatchTrade(map, known, today, year));
            Safe("calendar great hunt", () => WatchHunt(map, known, today, year));
            // A camp's gift, a world event, the faith's own days, one of ours brought home (CalendarLived.cs).
            WatchLived(map, known, today, year);
        }

        private static int Now => Find.TickManager.TicksGame;

        private void JournalNew(Pawn p, string pool, CalendarDay d)
        {
            if (p == null || d == null || !On) return;
            CouncilGameComponent.Instance?.journals.WriteTagged(p, pool, new[] { "new" }, null, d.label.Named("NAME"), 0.Named("YEARS"),
                (d.subject ?? "").Named("SUBJECT"), d.kills.Named("COUNT"), (Home?.Biome?.label ?? "").Named("PLACE"), (d.otherName ?? "").Named("WHO"),
                (d.enemy ?? "").Named("CAMP"));
        }

        // ---------------- The first child ----------------

        /// <summary>
        /// The first child born into the colony (Biotech): one of its children who has been the colony's since birth.
        /// Dated at the birth, so an older save finds its first child too.
        /// </summary>
        private void WatchFirstChild(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            if (firstChildSeen || !ModsConfig.BiotechActive) return;
            int now = Now;
            Pawn first = null;
            int born = int.MaxValue;
            foreach (var p in known.Values)
            {
                if (p.Dead || p.Faction != Faction.OfPlayer || p.ageTracker == null || p.records == null || p.RaceProps?.Humanlike != true) continue;
                if (Flavor.HumanAge(p) >= 13f) continue;
                long age = p.ageTracker.AgeBiologicalTicks;
                float here = p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal);
                // The colony's since birth (give or take a couple of days for the record to start).
                if (here < age - GenDate.TicksPerDay * 2) continue;
                int tick = Mathf.Max(0, now - (int)Mathf.Min(here, now));
                if (tick < born)
                {
                    born = tick;
                    first = p;
                }
            }
            if (first == null) return;
            firstChildSeen = true;
            if (Get("firstchild") != null) return;
            var parent = first.GetMother() ?? first.GetFather();
            bool fresh = now - born < GenDate.TicksPerDay * 2;
            var d = Dated(new CalendarDay
            {
                id = "firstchild", kind = CalendarKind.FirstChild, way = DefaultWay(CalendarKind.FirstChild), pawnId = first.ThingID, pawnName = first.LabelShort,
                subject = first.LabelShort, otherId = parent?.ThingID, otherName = parent?.LabelShort, weight = CalendarMath.FirstChildWeight
            }, born, map, today, year, fresh);
            if (TryAdd(d, fresh) != null && fresh && parent != null && Ours(parent)) JournalNew(parent, "Council_Journal_Cal_FirstChild", d);
        }

        // ---------------- The walls ----------------

        private static Func<ThingDef, bool> wallPred;

        /// <summary>The day the colony first had a real wall round it: <see cref="CalendarMath.WallsUp"/> sections of wall.</summary>
        private void WatchWalls(Map map, int today, int year)
        {
            if (wallsSeen) return;
            wallPred ??= RoomTemplateDef.RolePredicate("wall");
            if (wallPred == null) return;
            int walls = Avail.ColonyCount(map, wallPred);
            if (!wallsLooked)
            {
                wallsLooked = true;
                // Already walled before the calendar looked: that day went by unwritten.
                if (walls >= CalendarMath.WallsUp)
                {
                    wallsSeen = true;
                    return;
                }
            }
            if (walls < CalendarMath.WallsUp || GenDate.DaysPassed < 3) return;
            wallsSeen = true;
            var builder = AtHome(map).Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).FirstOrDefault();
            var d = Dated(new CalendarDay
            {
                id = "walls", kind = CalendarKind.Walls, way = DefaultWay(CalendarKind.Walls), pawnId = builder?.ThingID, pawnName = builder?.LabelShort,
                kills = walls, weight = CalendarMath.WallsWeight
            }, Mathf.Max(0, Now - GenDate.TicksPerHour * 6), map, today, year, true);
            if (TryAdd(d, true) != null) JournalNew(builder, "Council_Journal_Cal_Walls", d);
        }

        // ---------------- A caravan home ----------------

        /// <summary>
        /// The colony's people away (a caravan, a journey, an outpost of the world) are noted; when they come home after
        /// <see cref="CalendarMath.HomecomingMinDays"/> days or more, the day is written down. One such day is kept, the
        /// longest journey's.
        /// </summary>
        private void WatchHomecoming(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            int now = Now;
            var back = new List<(Pawn p, int since)>();
            foreach (var p in known.Values)
            {
                if (p.Dead || !Ours(p)) continue;
                bool home = p.MapHeld?.IsPlayerHome ?? false;
                if (!home)
                {
                    if (!awaySince.ContainsKey(p.ThingID)) awaySince[p.ThingID] = now;
                    continue;
                }
                if (!awaySince.TryGetValue(p.ThingID, out int since)) continue;
                awaySince.Remove(p.ThingID);
                if (now - since >= CalendarMath.HomecomingMinDays * GenDate.TicksPerDay) back.Add((p, since));
            }
            foreach (var k in awaySince.Keys.ToList())
                if (!known.TryGetValue(k, out var q) || q.Dead || !Ours(q)) awaySince.Remove(k);
            if (back.Count == 0) return;
            float days = back.Max(x => now - x.since) / (float)GenDate.TicksPerDay;
            var lead = back.Select(x => x.p).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First();
            var d = Dated(new CalendarDay
            {
                id = "homecoming|" + now, kind = CalendarKind.Homecoming, way = DefaultWay(CalendarKind.Homecoming), pawnId = lead.ThingID, pawnName = lead.LabelShort,
                kills = Mathf.RoundToInt(days), subject = TextUtil.JoinAnd(back.Select(x => x.p.LabelShort).ToList()),
                knew = back.Select(x => x.p.ThingID).ToList(), weight = CalendarMath.HomecomingWeight(days, back.Count)
            }, Mathf.Max(0, now - GenDate.TicksPerHour * 6), map, today, year, true);
            if (TryAdd(d, true) != null) JournalNew(lead, "Council_Journal_Cal_Homecoming", d);
        }

        // ---------------- A new crop ----------------

        /// <summary>What the colony grows now: the crops of its growing zones, hydroponics and plant pots (not trees).</summary>
        private static HashSet<string> CropsGrown(Map map)
        {
            var set = new HashSet<string>();
            void Add(ThingDef d)
            {
                if (d?.plant == null || d.plant.harvestedThingDef == null || d.plant.IsTree || !d.plant.Sowable) return;
                set.Add(d.defName);
            }
            foreach (var z in map.zoneManager.AllZones)
                if (z is Zone_Growing g) Add(g.GetPlantDefToGrow());
            foreach (var b in Avail.ColonyBuildings(map, d => typeof(Building_PlantGrower).IsAssignableFrom(d.thingClass)))
                if (b is Building_PlantGrower pg) Add(pg.GetPlantDefToGrow());
            return set;
        }

        /// <summary>
        /// A crop the colony has never grown before is noted when first sown; its first real harvest (ten or more of what it
        /// gives, come into the stores) goes on the calendar. The crops of the colony's first days don't count as new.
        /// </summary>
        private void WatchCrops(Map map, int today, int year)
        {
            var crops = CropsGrown(map);
            if (!cropsLooked || GenDate.DaysPassed < 5)
            {
                cropsLooked = true;
                knownCrops.UnionWith(crops);
                return;
            }
            foreach (var c in crops)
            {
                if (knownCrops.Contains(c)) continue;
                knownCrops.Add(c);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(c);
                if (def?.plant?.harvestedThingDef != null) pendingCrops[c] = map.resourceCounter.GetCount(def.plant.harvestedThingDef);
            }
            foreach (var kv in pendingCrops.ToList())
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                var product = def?.plant?.harvestedThingDef;
                if (product == null)
                {
                    pendingCrops.Remove(kv.Key);
                    continue;
                }
                int have = map.resourceCounter.GetCount(product);
                if (have - kv.Value < 10)
                {
                    // Eaten or sold meanwhile: count from the lower stock.
                    if (have < kv.Value) pendingCrops[kv.Key] = have;
                    continue;
                }
                pendingCrops.Remove(kv.Key);
                if (Get("crop|" + kv.Key) != null) continue;
                string name = product.label ?? def.label;
                if (name.StartsWith("raw ")) name = name.Substring(4);
                var grower = AtHome(map).Where(p => !p.DevelopmentalStage.Juvenile())
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).FirstOrDefault();
                var d = Dated(new CalendarDay
                {
                    id = "crop|" + kv.Key, kind = CalendarKind.NewCrop, way = DefaultWay(CalendarKind.NewCrop), subject = name, pawnId = grower?.ThingID,
                    pawnName = grower?.LabelShort, weight = CalendarMath.NewCropWeight
                }, Mathf.Max(0, Now - GenDate.TicksPerHour * 6), map, today, year, true);
                if (TryAdd(d, true) != null) JournalNew(grower, "Council_Journal_Cal_NewCrop", d);
            }
        }

        // ---------------- The first trade ----------------

        /// <summary>The colony's first trade: the day someone first struck a deal as its negotiator (Rimfolk counts them).</summary>
        private void WatchTrade(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            if (tradesSeen == -2) return;
            var rec = DefDatabase<RecordDef>.GetNamedSilentFail(CouncilRecords.TradesMade);
            if (rec == null) return;
            int total = 0;
            Pawn trader = null;
            int best = 0;
            foreach (var p in known.Values)
            {
                if (p.records == null || p.Faction != Faction.OfPlayer) continue;
                int n = p.records.GetAsInt(rec);
                total += n;
                if (!p.Dead && n > best)
                {
                    best = n;
                    trader = p;
                }
            }
            if (tradesSeen == -1)
            {
                // Traded before the calendar looked: that day went by unwritten.
                tradesSeen = total > 0 ? -2 : 0;
                return;
            }
            if (total <= 0) return;
            tradesSeen = -2;
            if (Get("firsttrade") != null) return;
            var d = Dated(new CalendarDay
            {
                id = "firsttrade", kind = CalendarKind.FirstTrade, way = DefaultWay(CalendarKind.FirstTrade), pawnId = trader?.ThingID, pawnName = trader?.LabelShort,
                weight = CalendarMath.FirstTradeWeight
            }, Mathf.Max(0, Now - GenDate.TicksPerHour * 6), map, today, year, true);
            if (TryAdd(d, true) != null && trader != null && Ours(trader)) JournalNew(trader, "Council_Journal_Cal_FirstTrade", d);
        }

        // ---------------- A great hunt ----------------

        /// <summary>
        /// A day the colony's hunters brought down <see cref="CalendarMath.GreatHuntKills"/> animals or more, with no raid or
        /// pack of maddened animals behind it (those are battles). Only kills made while the colonist was ours count.
        /// </summary>
        private void WatchHunt(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            var rec = RecordDefOf.KillsAnimals;
            if (rec == null) return;
            int delta = 0;
            var hunters = new List<(Pawn p, int n)>();
            var seen = new HashSet<string>();
            foreach (var p in known.Values)
            {
                if (p.Dead || !Ours(p) || p.records == null) continue;
                int n = p.records.GetAsInt(rec);
                if (huntKills.TryGetValue(p.ThingID, out int before) && n > before)
                {
                    delta += n - before;
                    hunters.Add((p, n - before));
                }
                huntKills[p.ThingID] = n;
                seen.Add(p.ThingID);
            }
            foreach (var k in huntKills.Keys.Where(k => !seen.Contains(k)).ToList()) huntKills.Remove(k);
            if (delta < CalendarMath.GreatHuntKills || GenDate.DaysPassed < 3) return;
            int now = Now;
            int threat = map.storyState.LastThreatBigTick;
            if (threat > 0 && now - threat < GenDate.TicksPerDay * 2) return;
            if (days.Any(x => x.kind == CalendarKind.Battle && Mathf.Abs(x.tick - now) < GenDate.TicksPerDay * 2)) return;
            var lead = hunters.OrderByDescending(x => x.n).First().p;
            var d = Dated(new CalendarDay
            {
                id = "hunt|" + now, kind = CalendarKind.GreatHunt, way = DefaultWay(CalendarKind.GreatHunt), pawnId = lead.ThingID, pawnName = lead.LabelShort,
                kills = delta, subject = Game(map, now), knew = hunters.Select(x => x.p.ThingID).ToList(), weight = CalendarMath.HuntWeight(delta)
            }, Mathf.Max(0, now - GenDate.TicksPerHour * 6), map, today, year, true);
            if (TryAdd(d, true) != null) JournalNew(lead, "Council_Journal_Cal_GreatHunt", d);
        }

        /// <summary>The most common game among the animals that died on the map in the last day ("deer"), or the word for game.</summary>
        private static string Game(Map map, int now)
        {
            try
            {
                var kinds = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                    .Where(c => c.InnerPawn?.RaceProps != null && c.InnerPawn.RaceProps.Animal && c.timeOfDeath > now - GenDate.TicksPerDay * 2)
                    .GroupBy(c => c.InnerPawn.kindDef).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (kinds?.Key != null) return kinds.Key.GetLabelPlural();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar hunt game", e);
            }
            return "CC_Cal_Game".Translate();
        }
    }
}
