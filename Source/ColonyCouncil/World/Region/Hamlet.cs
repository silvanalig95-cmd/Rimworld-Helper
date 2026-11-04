using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One home's hamlet (kept on its <see cref="RegionHome"/>): the settlers who asked, where they come from and where
    /// they mean to settle, how far their story has gone (asked, waiting at the gate, on the road with a caravan or alone,
    /// standing, sworn, over), the hamlet once it stands (its town, its Fealty tie to the colony, its fortune's days at 40,
    /// the asks it made and those refused, its harvests), and when the home was last looked at and asked. One hamlet a
    /// home; a story over leaves its record until the next settlers ask.
    /// </summary>
    public class HamletHome : IExposable
    {
        public HamletStage stage;
        public HamletWay way;
        public HamletEnd end;
        public Faction people;
        /// <summary>The settlers on their way (8 at most), kept in the world for it (KeepFor.Hamlet); their leader first.</summary>
        public List<Pawn> settlers = new List<Pawn>();
        public Pawn leader;
        /// <summary>The place they mean to settle (a tile).</summary>
        public Ref tile;
        /// <summary>The hamlet's town (a settlement's id) once it stands, and its name.</summary>
        public int town = -1;
        public string name;
        /// <summary>The town they came from, and its name for when it is gone.</summary>
        public Ref from;
        public string fromName;
        public int lookedAt = -1, lastAsk = -1, askedAt = -1;
        /// <summary>Their camp at the gate (a camp's id) and since when they wait, or -1.</summary>
        public int camp = -1, waitSince = -1;
        /// <summary>A colonist of the caravan they go with, and every grown colonist of it when it set out (8 at most).</summary>
        public Pawn escort;
        public List<Pawn> escorts = new List<Pawn>();
        /// <summary>When they reached the place, and whether the road's one roll at the founding was made.</summary>
        public int arrivedAt = -1;
        public bool rolled;
        /// <summary>Going alone: when they get there, and whether they will be turned back on the way.</summary>
        public int due = -1;
        public bool turnedBack;
        public int founded = -1;
        /// <summary>The Fealty tie from the hamlet to the colony (its id), or -1.</summary>
        public int tie = -1;
        /// <summary>Since when its fortune has been 40 or more, or -1.</summary>
        public int highSince = -1;
        /// <summary>When its asks for help were refused (the newest few).</summary>
        public List<int> refused = new List<int>();
        public int helpAt = -1, oathAt = -1, harvestAt = -1, endedAt = -1;
        /// <summary>What it asked for last (a HamletNeed), or -1.</summary>
        public int lastNeed = -1;
        /// <summary>What the colony did in its first years: 1 food sent, 2 other help sent, 4 help refused (the grown town's first year).</summary>
        public int firstYear;
        /// <summary>Its second year's end was noted (the grown town); it has grown into a town (the ledger keeps it as one).</summary>
        public bool secondYear, grown;
        /// <summary>Its people turned on the colony and the question was asked.</summary>
        public bool hostileAsked;
        /// <summary>How many hamlets this home has seen stand.</summary>
        public int count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref way, "way");
            Scribe_Values.Look(ref end, "end");
            Scribe_References.Look(ref people, "people");
            Scribe_Collections.Look(ref settlers, "settlers", LookMode.Reference);
            Scribe_References.Look(ref leader, "leader");
            Refs.Look(ref tile, "tile");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref name, "name");
            Refs.Look(ref from, "from");
            Scribe_Values.Look(ref fromName, "fromName");
            Scribe_Values.Look(ref lookedAt, "lookedAt", -1);
            Scribe_Values.Look(ref lastAsk, "lastAsk", -1);
            Scribe_Values.Look(ref askedAt, "askedAt", -1);
            Scribe_Values.Look(ref camp, "camp", -1);
            Scribe_Values.Look(ref waitSince, "waitSince", -1);
            Scribe_References.Look(ref escort, "escort");
            Scribe_Collections.Look(ref escorts, "escorts", LookMode.Reference);
            Scribe_Values.Look(ref arrivedAt, "arrivedAt", -1);
            Scribe_Values.Look(ref rolled, "rolled");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref turnedBack, "turnedBack");
            Scribe_Values.Look(ref founded, "founded", -1);
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref highSince, "highSince", -1);
            Scribe_Collections.Look(ref refused, "refused", LookMode.Value);
            Scribe_Values.Look(ref helpAt, "helpAt", -1);
            Scribe_Values.Look(ref oathAt, "oathAt", -1);
            Scribe_Values.Look(ref harvestAt, "harvestAt", -1);
            Scribe_Values.Look(ref endedAt, "endedAt", -1);
            Scribe_Values.Look(ref lastNeed, "lastNeed", -1);
            Scribe_Values.Look(ref firstYear, "firstYear");
            Scribe_Values.Look(ref secondYear, "secondYear");
            Scribe_Values.Look(ref grown, "grown");
            Scribe_Values.Look(ref hostileAsked, "hostileAsked");
            Scribe_Values.Look(ref count, "count");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                settlers ??= new List<Pawn>();
                settlers.RemoveAll(p => p == null);
                escorts ??= new List<Pawn>();
                escorts.RemoveAll(p => p == null);
                refused ??= new List<int>();
                // Their people gone from the game (another mod's people removed): the story is over.
                if (people == null && stage != HamletStage.None && stage != HamletStage.Over)
                {
                    stage = HamletStage.Over;
                    end = HamletEnd.Gone;
                }
            }
        }

        /// <summary>The settlers still alive and not lost to the game.</summary>
        public List<Pawn> Living => settlers.Where(p => p != null && !p.Dead && !p.Discarded).ToList();
    }

    /// <summary>
    /// Settlers who found a hamlet under the colony's protection (docs/design/stage1-region.md, item 6; decisions 9 and 10):
    /// families of a people at peace ask to settle a few days off (a holding a year on the colony's land, refugees of a town
    /// given up, or settlers from a poor or struggling town). Taken under its protection, they camp at the gate until a
    /// caravan of the colony's sets out for the place, and travel beside it; after a night there the hamlet stands, a town of
    /// their own people (the game's own settlement) with its founder, its families and a Fealty tie to the colony. It brings
    /// produce at harvest, asks for help in hard seasons (refused twice in one, the tie breaks), and in time asks to swear
    /// fealty: Faction Territories' vassal when that mod's vassalage is on, else the tie, sworn. If its people turn on the
    /// colony first, its families choose. Looked at in the region's day and hour (<see cref="Pass.DailyRegion"/>,
    /// <see cref="Pass.Region"/>); nothing per tick, no pass of its own. Off, no settlers ask, and a hamlet standing goes on
    /// as their town while its record is kept.
    /// </summary>
    public static partial class Hamlets
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;
        private const int Day = HamletMath.DayTicks;

        /// <summary>The setting "Hamlets".</summary>
        public static bool On => (ColonyCouncilMod.Settings?.hamlets ?? true) && Current.Game != null;

        // ---------------- Reading ----------------

        /// <summary>A home's hamlet record (made when it has none); null for a map that isn't a home or without a game.</summary>
        public static HamletHome Of(Map home)
        {
            if (home == null || !Homes.IsHome(home)) return null;
            var rh = RegionBook.Instance?.HomeOf(home);
            if (rh == null) return null;
            return rh.hamlet ??= new HamletHome();
        }

        /// <summary>A home's hamlet record, without making one; null for none.</summary>
        public static HamletHome Peek(Map home) => home == null || !Homes.IsHome(home) ? null : RegionBook.Instance?.HomeOf(home)?.hamlet;

        /// <summary>Every home that has a hamlet record, with it, in the homes' order.</summary>
        public static List<(Map home, HamletHome h)> All()
        {
            var list = new List<(Map, HamletHome)>();
            if (RegionBook.Instance == null) return list;
            foreach (var m in Homes.All)
            {
                var h = Peek(m);
                if (h != null) list.Add((m, h));
            }
            return list;
        }

        /// <summary>The hamlet a town is, standing as the colony's, with its home; null when it is none.</summary>
        public static HamletHome HamletAt(int town, out Map home)
        {
            home = null;
            if (town < 0 || RegionBook.Instance == null) return null;
            foreach (var (m, h) in All())
                if (h.town == town && HamletMath.Stands(h.stage))
                {
                    home = m;
                    return h;
                }
            return null;
        }

        /// <summary>The home a hamlet record is kept at, or null.</summary>
        internal static Map HomeOf(HamletHome h)
        {
            foreach (var (m, x) in All())
                if (x == h)
                    return m;
            return null;
        }

        /// <summary>A town is a hamlet the ledger keeps (its record's kind).</summary>
        public static bool IsHamlet(int town)
        {
            var rec = town >= 0 ? WorldLedger.TownFor(town, null, null, false) : null;
            return rec != null && rec.kind == TownKind.Hamlet && rec.gone < 0;
        }

        /// <summary>Someone is one of the settlers on their way (KeepFor.Hamlet's reason still holds).</summary>
        public static bool Holds(Pawn p)
        {
            if (p == null || RegionBook.Instance == null) return false;
            foreach (var (_, h) in All())
                if (HamletMath.OnTheirWay(h.stage) && h.settlers.Contains(p))
                    return true;
            return false;
        }

        /// <summary>The hamlet's town while it stands; null once it is gone.</summary>
        public static Settlement TownOf(HamletHome h)
        {
            var t = h != null && h.town >= 0 ? NotableBook.TownOf(h.town) : null;
            return t != null && !t.Destroyed ? t : null;
        }

        /// <summary>The place the settlers mean to settle (or where the hamlet stands).</summary>
        public static PlanetTile TileOf(HamletHome h) => h == null ? PlanetTile.Invalid : RiseFall.TileOf(h.tile);

        /// <summary>The hamlet's Fealty tie to the colony while the ledger keeps it, or null.</summary>
        public static Tie TieOf(HamletHome h) => h == null || h.tie < 0 ? null : WorldLedger.Get(h.tie);

        /// <summary>The grown colonists at a home, the ones who could protect a hamlet.</summary>
        public static int Adults(Map home) => home == null ? 0 : IncidentWorker_Happening.Colonists(home).Count(p => !p.DevelopmentalStage.Juvenile());

        /// <summary>A people that could found a hamlet near the colony: one of the world's real humanlike peoples at peace, with people to send and a naming rule for towns.</summary>
        public static bool CanFound(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def != null
            && HamletMath.PeopleFits(f.def.humanlikeFaction, f.HostileTo(Faction.OfPlayer), Arrivals.PersonKind(f) != null, f.def.settlementNameMaker != null);

        // ---------------- The day and the hour ----------------

        /// <summary>
        /// The region's day: each home's hamlet looked after (an ask unanswered too long, the hamlet's own day), then a look
        /// for new settlers; and ledger records of kind Hamlet no home holds any more become ordinary towns.
        /// </summary>
        internal static void Daily(RegionBook book, int now)
        {
            if (book == null) return;
            var held = new HashSet<int>();
            foreach (var home in Homes.All.ToList())
            {
                var h = Of(home);
                if (h == null) continue;
                try
                {
                    if (h.stage == HamletStage.Asked && now - h.askedAt >= (long)HamletMath.AnswerDays * Day) End(home, h, HamletEnd.Refused, null);
                    if (HamletMath.Stands(h.stage)) Life(home, h, now);
                    if (!HamletMath.Busy(h.stage)) Look(home, h, now);
                    if (HamletMath.Stands(h.stage) && !h.grown) held.Add(h.town);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: the day", e);
                }
            }
            try
            {
                Strays(held);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: records no hamlet holds", e);
            }
        }

        /// <summary>The region's hour: settlers at the gate, on the road with a caravan, or on the road alone.</summary>
        internal static void Hourly(RegionBook book, int now)
        {
            if (book == null) return;
            foreach (var home in Homes.All.ToList())
            {
                var h = Of(home);
                if (h == null) continue;
                try
                {
                    switch (h.stage)
                    {
                        case HamletStage.Waiting:
                            Wait(home, h, now);
                            break;
                        case HamletStage.Escorted:
                            Road(home, h, now);
                            break;
                        case HamletStage.Alone:
                            RoadAlone(home, h, now);
                            break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: the hour", e);
                }
            }
        }

        /// <summary>Any settlers on their way at any home (the hour has something to do).</summary>
        internal static bool AnyOnTheRoad()
        {
            foreach (var (_, h) in All())
                if (h.stage == HamletStage.Waiting || h.stage == HamletStage.Escorted || h.stage == HamletStage.Alone)
                    return true;
            return false;
        }

        /// <summary>A town of kind Hamlet that no home's hamlet holds (the home lost, the story over): an ordinary town of theirs again.</summary>
        private static void Strays(HashSet<int> held)
        {
            var l = WorldLedger.Instance;
            if (l == null) return;
            bool changed = false;
            foreach (var t in l.Towns)
                if (t != null && t.kind == TownKind.Hamlet && !held.Contains(t.id))
                {
                    t.kind = TownKind.Town;
                    changed = true;
                }
            if (changed) WorldLedger.TownChanged();
        }

        // ---------------- Who asks ----------------

        /// <summary>
        /// The look at a home every 10 days: settlers may ask (day 90, five grown colonists, no hamlet of the colony's there,
        /// a year since the last ask, a question may come now), some qualify (a holding a year settled, else the poorest
        /// struggling or poor town of a people at peace in reach), the 15% chance, then a place for them.
        /// </summary>
        private static void Look(Map home, HamletHome h, int now)
        {
            if (!On || !HamletMath.LookDue(h.lookedAt, now) || !Tell.CanAsk) return;
            h.lookedAt = now;
            if (!HamletMath.AskAllowed(GenDate.DaysPassed, Adults(home), HamletMath.Busy(h.stage), h.lastAsk, now)) return;
            var holding = HoldingThatAsks(home);
            Settlement town = holding == null ? PoorTown(home) : null;
            if (holding == null && town == null || !Rand.Chance(HamletMath.Chance)) return;
            if (holding != null) AskFromHolding(home, h, holding, now);
            else AskFromTown(home, h, town, now);
        }

        /// <summary>The colony's holding, when it has lived a year on its land with low unease and its people at peace.</summary>
        private static Camp HoldingThatAsks(Map home)
        {
            var c = CampTracker.HoldingAt(home);
            if (c == null || !c.CanTalk || !CanFound(c.faction) || c.Here.Count(p => !p.DevelopmentalStage.Juvenile()) == 0) return null;
            return HamletMath.HoldingFits(c.DaysSettled, c.tension, !c.Hostile) ? c : null;
        }

        /// <summary>The poorest town in the home's reach of a people at peace that could send settlers (its fortune under 40), or null.</summary>
        private static Settlement PoorTown(Map home)
        {
            var r = RegionTowns.Of(home);
            if (r == null) return null;
            Settlement best = null;
            float low = float.MaxValue;
            foreach (var t in r.towns)
            {
                if (t == null || t.Destroyed || !CanFound(t.Faction)) continue;
                var rec = WorldLedger.TownFor(t.ID, null, null, false);
                if (rec == null || !rec.Seeded || rec.gone >= 0 || rec.kind != TownKind.Town) continue;
                if (!HamletMath.TownFits(rec.prosperity, true) || rec.prosperity >= low) continue;
                best = t;
                low = rec.prosperity;
            }
            return best;
        }

        /// <summary>The holding asks: its own people are the settlers, its leader theirs.</summary>
        internal static bool AskFromHolding(Map home, HamletHome h, Camp holding, int now)
        {
            var people = holding.faction;
            var tile = FindTile(home, people);
            if (!tile.Valid) return false;
            var settlers = holding.Here.Where(p => p.Faction == people).OrderBy(p => p == holding.leader ? 0 : 1).Take(HamletMath.MaxSettlers).ToList();
            if (settlers.Count == 0) return false;
            Begin(home, h, HamletWay.Holding, people, settlers, tile, NearestTownOf(people, home), now);
            h.camp = holding.id;
            HamletAsk.Ask(home, h);
            return true;
        }

        /// <summary>Settlers from a poor or struggling town: a family of theirs, made now and kept in the world until the colony answers.</summary>
        internal static bool AskFromTown(Map home, HamletHome h, Settlement town, int now)
        {
            var people = town?.Faction;
            if (!CanFound(people)) return false;
            var tile = FindTile(home, people);
            if (!tile.Valid) return false;
            var settlers = MakeSettlers(people, Rand.RangeInclusive(4, 6));
            if (settlers.Count == 0) return false;
            Begin(home, h, HamletWay.Town, people, settlers, tile, town, now);
            HamletAsk.Ask(home, h);
            return true;
        }

        /// <summary>
        /// Refugees of a town given up, offered a place near the colony at its gate (the town-abandoned event's answer): the
        /// family at the gate goes back for the others, and comes again with them if the colony takes them under its
        /// protection. Asked at once (the colony has just offered).
        /// </summary>
        internal static bool AskFromRefugees(Map home, Faction people, Settlement town, List<Pawn> atGate)
        {
            var h = Of(home);
            int now = Now;
            if (h == null || !CanAskHere(home, people, out _)) return false;
            var tile = FindTile(home, people);
            if (!tile.Valid) return false;
            var settlers = new List<Pawn>();
            foreach (var p in atGate ?? new List<Pawn>())
                if (p != null && !p.Dead && p.Faction == people && p.RaceProps.Humanlike && settlers.Count < HamletMath.MaxSettlers)
                    settlers.Add(p);
            string last = settlers.Select(p => (p.Name as NameTriple)?.Last).FirstOrDefault(n => !n.NullOrEmpty());
            foreach (var p in MakeSettlers(people, Math.Max(0, Rand.RangeInclusive(4, 6) - settlers.Count), settlers.Count == 0 ? null : RegionSurname(last)))
                if (settlers.Count < HamletMath.MaxSettlers) settlers.Add(p);
            if (settlers.Count == 0) return false;
            Begin(home, h, HamletWay.Refugees, people, settlers, tile, town, now);
            // The family at the gate walks back up the road for the others; they are held in the world meanwhile.
            Arrivals.SendAway(settlers.Where(p => p.Spawned).ToList());
            foreach (var p in settlers) KeepSettler(p);
            HamletAsk.Ask(home, h);
            return true;
        }

        /// <summary>A family name for a second family of refugees (the first came to the gate), or null.</summary>
        private static string RegionSurname(string not)
        {
            for (int i = 0; i < 4; i++)
            {
                string n = Surname();
                if (!n.NullOrEmpty() && n != not) return n;
            }
            return null;
        }

        /// <summary>Settlers could ask at this home now (for the refugees' answer): why not, in words, or true.</summary>
        public static bool CanAskHere(Map home, Faction people, out string why)
        {
            why = null;
            var h = Of(home);
            if (!On || h == null)
            {
                why = "CC_Hamlet_Why_Off".Translate().Resolve();
                return false;
            }
            if (HamletMath.Busy(h.stage)) why = "CC_Hamlet_Why_Busy".Translate().Resolve();
            else if (GenDate.DaysPassed < HamletMath.FirstDay) why = "CC_Hamlet_Why_Early".Translate().Resolve();
            else if (Adults(home) < HamletMath.MinAdults) why = "CC_Hamlet_Why_Few".Translate(HamletMath.MinAdults.Named("COUNT")).Resolve();
            else if (h.lastAsk >= 0 && Now - h.lastAsk < (long)HamletMath.AskGapDays * Day) why = "CC_Hamlet_Why_Lately".Translate().Resolve();
            else if (!CanFound(people)) why = "CC_Hamlet_Why_People".Translate().Resolve();
            return why == null;
        }

        /// <summary>The ask's record: who, from where, to where; the settlers kept in the world while the colony answers.</summary>
        private static void Begin(Map home, HamletHome h, HamletWay way, Faction people, List<Pawn> settlers, PlanetTile tile, Settlement from, int now)
        {
            ReleaseSettlers(h);
            h.stage = HamletStage.Asked;
            h.way = way;
            h.end = HamletEnd.None;
            h.people = people;
            h.settlers = settlers.Take(HamletMath.MaxSettlers).ToList();
            h.leader = h.settlers.FirstOrDefault(p => !p.DevelopmentalStage.Juvenile()) ?? h.settlers.FirstOrDefault();
            h.tile = Refs.Of(tile);
            h.town = -1;
            h.name = null;
            h.from = Refs.Of(from);
            h.fromName = from != null && from.HasName ? from.Name : null;
            h.askedAt = now;
            h.lastAsk = now;
            h.camp = -1;
            h.waitSince = -1;
            h.escort = null;
            h.escorts.Clear();
            h.arrivedAt = -1;
            h.rolled = false;
            h.due = -1;
            h.turnedBack = false;
            h.founded = -1;
            h.tie = -1;
            h.highSince = -1;
            h.refused.Clear();
            h.helpAt = h.oathAt = h.harvestAt = h.endedAt = -1;
            h.lastNeed = -1;
            h.firstYear = 0;
            h.secondYear = h.grown = h.hostileAsked = false;
            foreach (var p in h.settlers) WorldLedger.Keep(p, KeepFor.Hamlet, !p.Spawned);
        }

        /// <summary>Their people's standing town nearest the home, or null.</summary>
        private static Settlement NearestTownOf(Faction f, Map home)
        {
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || s.Faction != f || !s.HasName || s.Tile.Layer != home.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile);
                if (d >= bestD) continue;
                best = s;
                bestD = d;
            }
            return best;
        }

        /// <summary>A family of their people, made now and held in the world: a leader of an age to lead settlers out, their kin, children when the game has them.</summary>
        internal static List<Pawn> MakeSettlers(Faction f, int n, string lastName = null)
        {
            var list = new List<Pawn>();
            var kind = Arrivals.PersonKind(f);
            if (kind == null || n <= 0) return list;
            Pawn first = null;
            for (int i = 0; i < n && list.Count < HamletMath.MaxSettlers; i++)
            {
                bool child = i >= 2 && ModsConfig.BiotechActive && Rand.Chance(0.4f);
                try
                {
                    FloatRange? age = child ? (FloatRange?)null : i == 0 ? new FloatRange(RegionMath.FounderMinAge, RegionMath.FounderMaxAge) : new FloatRange(18f, 55f);
                    Gender? gender = i == 1 && first != null ? (first.gender == Gender.Male ? Gender.Female : Gender.Male) : (Gender?)null;
                    var p = Arrivals.Make(kind, f, age, gender, lastName, child, first);
                    if (p == null) continue;
                    if (first == null)
                    {
                        first = p;
                        lastName ??= (p.Name as NameTriple)?.Last;
                    }
                    Arrivals.Hold(p);
                    list.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: settlers made", e);
                }
            }
            return list;
        }

        /// <summary>One of the settlers kept in the world for the hamlet, the game told (out of a camp or off the map).</summary>
        internal static void KeepSettler(Pawn p)
        {
            if (p == null || p.Dead || p.Discarded) return;
            WorldLedger.Keep(p, KeepFor.Hamlet, false);
            Find.WorldPawns?.ForcefullyKeptPawns.Add(p);
        }

        /// <summary>The settlers no longer kept for the hamlet: back to the world's own keeping when nothing else of Rimfolk holds them.</summary>
        internal static void ReleaseSettlers(HamletHome h)
        {
            if (h?.settlers == null) return;
            foreach (var p in h.settlers.ToList())
            {
                if (p == null || p.Discarded) continue;
                // Their founder stays kept as the hamlet's founder and head.
                if (p == h.leader && h.town >= 0 && HamletMath.Stands(h.stage)) continue;
                try
                {
                    if (WorldLedger.Release(p, KeepFor.Hamlet) && !p.Spawned) Arrivals.Release(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: settlers let go", e);
                }
            }
            // Once they are no longer on their way, the record keeps only their leader (the game may let the rest go).
            if (!HamletMath.OnTheirWay(h.stage))
            {
                h.settlers.RemoveAll(p => p != h.leader);
                h.escorts.Clear();
            }
        }

        // ---------------- Ends ----------------

        /// <summary>
        /// The story is over: the settlers no longer kept, the tie closed as the end calls for, the ledger's record an
        /// ordinary town again (or the ruin it became), the history and the colony told.
        /// </summary>
        internal static void End(Map home, HamletHome h, HamletEnd why, string extra)
        {
            if (h == null || h.stage == HamletStage.Over) return;
            bool stood = HamletMath.Stands(h.stage);
            var wasStage = h.stage;
            h.end = why;
            h.endedAt = Now;
            var tie = TieOf(h);
            if (tie != null && tie.Live)
                WorldLedger.Set(tie, why == HamletEnd.Broken || why == HamletEnd.Stayed ? Tie.State.Broken : Tie.State.Ended);
            var rec = h.town >= 0 ? WorldLedger.TownRecord(h.town) : null;
            if (rec != null && rec.kind == TownKind.Hamlet)
            {
                rec.kind = TownKind.Town;
                WorldLedger.TownChanged();
            }
            h.stage = HamletStage.Over;
            ReleaseSettlers(h);
            // The settlers' own camp still at the gate packs up and goes (a holding that asked stays as it was).
            var c = CampOf(h);
            if (c != null && c.CanTalk && c.kind?.defName == SettlersCamp)
            {
                c.told = true;
                c.Leave("hamlet");
            }
            string kind = HamletMath.EndKind(why);
            if (kind != null && home != null) HistoryOf(kind, home, h, HamletWords.HistoryEnded(h, why));
            if (home != null) HamletWords.TellEnded(home, h, why, wasStage, stood, extra);
            RegionTowns.Invalidate();
        }

        /// <summary>The settlers' camp at the gate (or the holding they lived in), while it is on the map; null when none.</summary>
        internal static Camp CampOf(HamletHome h)
        {
            if (h == null || h.camp < 0) return null;
            var c = CampTracker.Instance?.ById(h.camp);
            return c != null && c.phase != CampPhase.Over ? c : null;
        }

        /// <summary>A hamlet's own event in the history: at its home, its people and its place named.</summary>
        internal static void HistoryOf(string kind, Map home, HamletHome h, string line, Pawn colonist = null)
        {
            if (home == null || h == null) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(h.people), HistoryRole.Did) };
            if (h.town >= 0) actors.Add(new HistoryActor(Ref.Town(h.town), HistoryRole.About));
            else if (!h.tile.IsNone) actors.Add(new HistoryActor(h.tile, HistoryRole.About));
            if (h.leader != null && kind == HamletMath.KindFounded) actors.Insert(1, new HistoryActor(WorldLedger.PersonOf(h.leader), HistoryRole.Did));
            if (colonist != null) actors.Add(new HistoryActor(Refs.Of(colonist), HistoryRole.With));
            History.Add(kind, actors, Refs.Colony(home), HamletMath.Milestone(kind) ? HistoryMath.Milestone : HistoryMath.Everyday, line);
        }

        /// <summary>A home lost (Homes.Changed): its hamlet's story, if one is under way, ends without a word; a hamlet standing is just their town.</summary>
        internal static void HomeLost(RegionHome rh)
        {
            var h = rh?.hamlet;
            if (h == null || h.stage == HamletStage.None || h.stage == HamletStage.Over) return;
            try
            {
                var tie = TieOf(h);
                if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Ended);
                h.stage = HamletStage.Over;
                h.end = HamletEnd.Gone;
                h.endedAt = Now;
                ReleaseSettlers(h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a home lost", e);
            }
        }
    }
}
