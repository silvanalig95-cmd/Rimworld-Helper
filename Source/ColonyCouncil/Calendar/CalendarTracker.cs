using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony calendar, in a game component of its own: the days that matter to this colony, and what happens when
    /// one comes round. Days are written down from the colony's history as it happens (the founding, the first good
    /// harvest, the first snow, a battle it remembers, the death of someone loved, a wedding, the arrival of one of its
    /// first members, a long-serving colonist's birthday, and in CalendarWatch.cs the first child born here, the walls
    /// going up, a caravan home safe, a new crop's first harvest, the first trade and a great hunt) and the player can
    /// name days of their own. Once an hour it
    /// looks at the date; a day that is due comes round with a letter or a message, a feast, games, a rest day or a quiet
    /// hour, journal lines and talk, and a day kept three years running becomes a tradition (CalendarKeeping.cs). With
    /// the "colony calendar" setting off, days are still written down but nothing happens when they come round.
    /// An older save gets its calendar filled in from the chronicle, the dead and the nemeses on the first day it runs.
    /// </summary>
    public partial class CalendarTracker : GameComponent
    {
        // ---------------- Saved ----------------

        public List<CalendarDay> days = new List<CalendarDay>();
        private bool initialized;
        private int nextNamedId = 1;
        /// <summary>The local date last seen (a new date runs the daily look).</summary>
        private int seenDay = -1;
        private int seenYear = -99999;
        /// <summary>The first good harvest: seen yet, and each colonist's harvest count and the raw food in store yesterday.</summary>
        private bool harvestSeen;
        private Dictionary<string, int> harvested = new Dictionary<string, int>();
        private int rawFoodYesterday = -1;
        /// <summary>Snow: seen at all, last seen, when the first snow last came round, and a first snow waiting to come round.</summary>
        private bool snowSeen;
        private int lastSnowTick = -9999999;
        private int lastSnowMarkTick = -9999999;
        private int snowPendingTick = -1;
        /// <summary>Nemeses brought down up to this tick are on the calendar (or were weighed for it).</summary>
        private int nemesisSeenTick = -1;
        /// <summary>The founding day is the founding event's to keep; until this tick the calendar watches for it.</summary>
        private int foundingWaitUntil = -1;
        private int foundingWaitYear = -99999;
        /// <summary>A rest day: each colonist's work hours turned to recreation ("hour=TimeAssignmentDef;..."), and its date.</summary>
        private Dictionary<string, string> restHours = new Dictionary<string, string>();
        /// <summary>Days the player took off the calendar: not written down again (a birthday, an arrival).</summary>
        private HashSet<string> forgotten = new HashSet<string>();
        private int restDate = -1;
        /// <summary>Today's kept day, which people talk about, and how many more conversations it has in it.</summary>
        private string talkDayId;
        private int talkDate = -1;
        private int talksLeft;
        private int eveTalkDate = -1;

        private List<string> tmpKeys1, tmpKeys2;
        private List<int> tmpInts;
        private List<string> tmpStrings;

        // ---------------- Not saved ----------------

        private static CalendarTracker cached;
        private static Game cachedGame;

        public static CalendarTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<CalendarTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public CalendarTracker(Game game)
        {
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Days come round with their letters, gatherings and moods (else they're only written down).</summary>
        public static bool On => S != null && S.colonyCalendar;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref days, "days", LookMode.Deep);
            Scribe_Values.Look(ref initialized, "initialized");
            Scribe_Values.Look(ref nextNamedId, "nextNamedId", 1);
            Scribe_Values.Look(ref seenDay, "seenDay", -1);
            Scribe_Values.Look(ref seenYear, "seenYear", -99999);
            Scribe_Values.Look(ref harvestSeen, "harvestSeen");
            Scribe_Collections.Look(ref harvested, "harvested", LookMode.Value, LookMode.Value, ref tmpKeys1, ref tmpInts);
            Scribe_Values.Look(ref rawFoodYesterday, "rawFoodYesterday", -1);
            Scribe_Values.Look(ref snowSeen, "snowSeen");
            Scribe_Values.Look(ref lastSnowTick, "lastSnowTick", -9999999);
            Scribe_Values.Look(ref lastSnowMarkTick, "lastSnowMarkTick", -9999999);
            Scribe_Values.Look(ref snowPendingTick, "snowPendingTick", -1);
            Scribe_Values.Look(ref nemesisSeenTick, "nemesisSeenTick", -1);
            Scribe_Values.Look(ref foundingWaitUntil, "foundingWaitUntil", -1);
            Scribe_Values.Look(ref foundingWaitYear, "foundingWaitYear", -99999);
            Scribe_Collections.Look(ref restHours, "restHours", LookMode.Value, LookMode.Value, ref tmpKeys2, ref tmpStrings);
            Scribe_Values.Look(ref restDate, "restDate", -1);
            Scribe_Collections.Look(ref forgotten, "forgotten", LookMode.Value);
            Scribe_Values.Look(ref talkDayId, "talkDayId");
            Scribe_Values.Look(ref talkDate, "talkDate", -1);
            Scribe_Values.Look(ref talksLeft, "talksLeft");
            Scribe_Values.Look(ref eveTalkDate, "eveTalkDate", -1);
            ExposeWatch();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (days == null) days = new List<CalendarDay>();
                // A day whose kind or way a later version no longer knows, or that lost its id, is let go quietly.
                days.RemoveAll(d => d == null || d.id.NullOrEmpty() || !Enum.IsDefined(typeof(CalendarKind), d.kind) || !Enum.IsDefined(typeof(DayWay), d.way));
                foreach (var d in days)
                {
                    d.day = CalendarMath.Wrap(d.day);
                    if (d.label.NullOrEmpty()) d.label = DefaultLabel(d);
                }
                if (harvested == null) harvested = new Dictionary<string, int>();
                if (restHours == null) restHours = new Dictionary<string, string>();
                if (forgotten == null) forgotten = new HashSet<string>();
            }
        }

        public override void GameComponentTick()
        {
            if (!Sched.Hourly(Find.TickManager.TicksGame, Sched.Calendar)) return;
            Safe("calendar", Hourly);
        }

        private static void Safe(string context, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        // ---------------- Dates ----------------

        private static Map Home => HappeningTracker.HomeMap();

        private static float Longitude(Map map) => map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;

        /// <summary>The day of the year (0 to 59) a game tick fell on, at the home map.</summary>
        public static int DayOf(int gameTick, Map map) => GenDate.DayOfYear(GenDate.TickGameToAbs(gameTick), Longitude(map));

        public static int YearOf(int gameTick, Map map) => GenDate.Year(GenDate.TickGameToAbs(gameTick), Longitude(map));

        /// <summary>A date to show: "5th of Aprimay".</summary>
        public static string DateText(int day, Map map = null)
        {
            map = map ?? Home;
            if (map == null || Find.TickManager == null)
            {
                CalendarMath.Split(day, out int q, out int d);
                return d + " / " + (q + 1);
            }
            int today = GenLocalDate.DayOfYear(map);
            long abs = Find.TickManager.TicksAbs + (long)(CalendarMath.Wrap(day) - today) * GenDate.TicksPerDay;
            return GenDate.QuadrumDateStringAt(abs, Longitude(map));
        }

        /// <summary>Today's date as a single number, for "has this happened today".</summary>
        private static int DateKey(Map map) => GenLocalDate.Year(map) * CalendarMath.DaysPerYear + GenLocalDate.DayOfYear(map);

        // ---------------- The hour ----------------

        private void Hourly()
        {
            var map = Home;
            if (map == null) return;
            int now = Find.TickManager.TicksGame;
            Safe("calendar snow", () => WatchSnow(map, now));
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            if (today != seenDay || year != seenYear)
            {
                seenDay = today;
                seenYear = year;
                // Yesterday's rest day is over: work hours go back to work (also with the calendar off).
                if (restDate >= 0) Safe("calendar rest day", RestoreRest);
                Safe("calendar daily", () => Daily(map, today, year));
            }
            if (!On) return;
            int hour = GenLocalDate.HourOfDay(map);
            Safe("calendar talk", () => Talk(map, hour, today, year));
            if (foundingWaitUntil >= 0) Safe("calendar founding", () => WatchFoundingEvent(map, now));
            if (hour < 9 || hour > 20) return;
            if (Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_CalendarDay c && !c.resolved)) return;
            var due = NextDue(today, year, now, out int occurrence);
            if (due == null) return;
            // A fight on the home map: the day waits (until the grace day runs out).
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            Safe("calendar day", () => ComeRound(due, occurrence, map));
        }

        /// <summary>The most meaningful day that is due now, if any.</summary>
        private CalendarDay NextDue(int today, int year, int now, out int occurrence)
        {
            occurrence = year;
            CalendarDay best = null;
            int bestOccurrence = year;
            foreach (var d in days)
            {
                int occ;
                if (d.kind == CalendarKind.FirstSnow)
                {
                    // The first snow comes with the snow, not on a date.
                    if (snowPendingTick < 0 || now - snowPendingTick > GenDate.TicksPerDay || d.lastYear >= year) continue;
                    occ = year;
                }
                else if (!CalendarMath.Due(today, year, d.day, d.lastYear, out occ)) continue;
                if (best == null || d.Standing > best.Standing)
                {
                    best = d;
                    bestOccurrence = occ;
                }
            }
            occurrence = bestOccurrence;
            return best;
        }

        // ---------------- The day ----------------

        private void Daily(Map map, int today, int year)
        {
            if (!initialized)
            {
                initialized = true;
                Safe("calendar backfill", () => Backfill(map, today, year));
            }
            EnsureFounding(map, today, year);
            var known = Known();
            Safe("calendar people", () => ScanPeople(map, known, today, year));
            Safe("calendar harvest", () => WatchHarvest(map, known, today, year));
            Safe("calendar nemeses", () => WatchNemeses(map, known, today, year));
            WatchMore(map, known, today, year);
            Prune(known);
            MarkMissed(today, year);
            if (snowPendingTick >= 0 && Find.TickManager.TicksGame - snowPendingTick > GenDate.TicksPerDay) snowPendingTick = -1;
        }

        /// <summary>Days that went by without coming round (a fight, the calendar off, nobody home): their year is spent.</summary>
        private void MarkMissed(int today, int year)
        {
            foreach (var d in days)
            {
                if (d.kind == CalendarKind.FirstSnow) continue;
                if (CalendarMath.DaysSince(today, d.day) != CalendarMath.GraceDays + 1) continue;
                int occ = CalendarMath.LastOccurrence(today, year, d.day);
                if (d.lastYear >= occ) continue;
                d.lastYear = occ;
                if (On && !d.Personal && occ > d.sinceYear)
                    News.Post("CC_Cal_Missed".Translate(d.label.CapitalizeFirst()).Resolve(), null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        // ---------------- Who is who ----------------

        /// <summary>
        /// Everyone the game knows, alive or dead, by ThingID (once a day): the calendar keeps people by id, and this is
        /// how it finds them again.
        /// </summary>
        private static Dictionary<string, Pawn> Known()
        {
            var map = new Dictionary<string, Pawn>();
            void Add(Pawn p)
            {
                if (p != null && !p.Discarded && p.RaceProps != null && p.RaceProps.Humanlike) map[p.ThingID] = p;
            }
            foreach (var p in PawnsFinder.All_AliveOrDead.ToList()) Add(p);
            foreach (var p in People.OffMapColonists()) Add(p);
            // The dead in graves and sarcophagi, and corpses lying on the colony's maps.
            foreach (var m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                foreach (var t in m.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    if (t is Corpse c) Add(c.InnerPawn);
                foreach (var b in m.listerBuildings.allBuildingsColonist)
                    if (b is Building_Grave g && g.Corpse != null) Add(g.Corpse.InnerPawn);
            }
            return map;
        }

        /// <summary>One of the colony's own people, alive, with an inner life (not a slave, prisoner or lodger).</summary>
        public static bool Ours(Pawn p) => People.StillOurs(p) && People.HasInnerLife(p) && !p.IsSlave && !p.IsQuestLodger();

        /// <summary>The colony's people at home on this map, who can take part in a day.</summary>
        public static List<Pawn> AtHome(Map map) =>
            map == null
                ? new List<Pawn>()
                : map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();

        private static Pawn Living(string id, Dictionary<string, Pawn> known) =>
            id != null && known != null && known.TryGetValue(id, out var p) && !p.Dead && Ours(p) ? p : null;

        private static float YearsWithUs(Pawn p) => (p?.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;

        // ---------------- The calendar itself ----------------

        public CalendarDay Get(string id) => id == null ? null : days.FirstOrDefault(d => d.id == id);

        public IEnumerable<CalendarDay> Traditions => days.Where(d => d.tradition);

        /// <summary>The calendar's traditions, for the founding day's letter and the like.</summary>
        public static IEnumerable<string> TraditionLabels() => Instance?.Traditions.Select(d => d.label).Where(l => !l.NullOrEmpty()) ?? Enumerable.Empty<string>();

        /// <summary>How many days of a kind the calendar keeps at most.</summary>
        private static int KindLimit(CalendarKind k)
        {
            switch (k)
            {
                case CalendarKind.Battle: return 3;
                case CalendarKind.Death: return 4;
                case CalendarKind.Wedding: return 3;
                case CalendarKind.Arrival: return 3;
                case CalendarKind.Birthday: return 4;
                case CalendarKind.NewCrop: return 2;
                case CalendarKind.GreatHunt: return 2;
                default: return 1;
            }
        }

        /// <summary>
        /// Writes a day down, if it means enough: a full calendar (or a kind with all its places taken) lets its least
        /// meaningful day go for a more meaningful one. Traditions, the founding day and the player's days never make way.
        /// Returns the day as it is on the calendar, or null.
        /// </summary>
        public CalendarDay TryAdd(CalendarDay d, bool announce)
        {
            if (d == null || d.id.NullOrEmpty()) return null;
            var existing = Get(d.id);
            if (existing != null) return existing;
            if (forgotten.Contains(d.id)) return null;
            if (d.label.NullOrEmpty()) d.label = DefaultLabel(d);
            if (d.Auto)
            {
                var same = days.Where(x => x.Auto && x.kind == d.kind).ToList();
                int drop = CalendarMath.MakeRoom(same.Select(x => x.Standing).ToList(), same.Select(x => x.Fixed).ToList(), d.weight, KindLimit(d.kind));
                if (drop == -2) return null;
                if (drop >= 0) days.Remove(same[drop]);
                var autos = days.Where(x => x.Auto).ToList();
                drop = CalendarMath.MakeRoom(autos.Select(x => x.Standing).ToList(), autos.Select(x => x.Fixed).ToList(), d.weight, CalendarMath.MaxAutoDays);
                if (drop == -2) return null;
                if (drop >= 0) days.Remove(autos[drop]);
            }
            days.Add(d);
            if (announce && On && !d.Personal)
                News.Post("CC_Cal_NewDay".Translate(d.label, DateText(d.day)).Resolve(), null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
            return d;
        }

        /// <summary>The player takes a day off the calendar; one the colony wrote down isn't written down again.</summary>
        public void Remove(CalendarDay d)
        {
            if (d == null) return;
            days.Remove(d);
            if (d.Auto) forgotten.Add(d.id);
        }

        /// <summary>The name a day gets when it's written down.</summary>
        public static string DefaultLabel(CalendarDay d)
        {
            switch (d.kind)
            {
                case CalendarKind.Founding: return "CC_Cal_Name_Founding".Translate();
                case CalendarKind.Harvest: return "CC_Cal_Name_Harvest".Translate();
                case CalendarKind.FirstSnow: return "CC_Cal_Name_FirstSnow".Translate();
                case CalendarKind.Battle:
                    if (d.id != null && d.id.StartsWith("nemesis|")) return "CC_Cal_Name_Nemesis".Translate(d.enemy ?? "?");
                    return (d.fallen.Count > 0 ? "CC_Cal_Name_BattleCostly" : "CC_Cal_Name_Battle").Translate(d.enemy ?? "?");
                case CalendarKind.Death: return "CC_Cal_Name_Death".Translate(d.pawnName ?? "?");
                case CalendarKind.Wedding: return "CC_Cal_Name_Wedding".Translate(d.pawnName ?? "?", d.otherName ?? "?");
                case CalendarKind.Arrival: return "CC_Cal_Name_Arrival".Translate(d.pawnName ?? "?");
                case CalendarKind.Birthday: return "CC_Cal_Name_Birthday".Translate(d.pawnName ?? "?");
                case CalendarKind.FirstChild: return "CC_Cal_Name_FirstChild".Translate(d.pawnName ?? "?");
                case CalendarKind.Walls: return "CC_Cal_Name_Walls".Translate();
                case CalendarKind.Homecoming: return "CC_Cal_Name_Homecoming".Translate();
                case CalendarKind.NewCrop: return "CC_Cal_Name_NewCrop".Translate(d.subject ?? "?");
                case CalendarKind.FirstTrade: return "CC_Cal_Name_FirstTrade".Translate();
                case CalendarKind.GreatHunt: return "CC_Cal_Name_GreatHunt".Translate();
                default: return "CC_Cal_Name_Holiday".Translate();
            }
        }

        /// <summary>How a kind of day is kept unless the player says otherwise.</summary>
        public static DayWay DefaultWay(CalendarKind kind, bool fallen = false)
        {
            switch (kind)
            {
                case CalendarKind.FirstSnow: return DayWay.Games;
                case CalendarKind.Battle: return fallen ? DayWay.Remembrance : DayWay.Feast;
                case CalendarKind.Death: return DayWay.Remembrance;
                case CalendarKind.Arrival:
                case CalendarKind.Birthday: return DayWay.Personal;
                case CalendarKind.FirstChild:
                case CalendarKind.FirstTrade: return DayWay.Gifts;
                case CalendarKind.Walls: return DayWay.Bonfire;
                case CalendarKind.GreatHunt: return DayWay.Hunt;
                default: return DayWay.Feast;
            }
        }

        /// <summary>A day dated at a game tick: its date, its year, and whether it has come round this year already.</summary>
        private static CalendarDay Dated(CalendarDay d, int gameTick, Map map, int today, int year, bool happenedNow)
        {
            d.tick = gameTick;
            d.day = CalendarMath.Wrap(DayOf(gameTick, map));
            d.sinceYear = YearOf(gameTick, map);
            d.lastYear = happenedNow ? year : CalendarMath.InitialLastYear(today, year, d.day, false);
            return d;
        }

        private void EnsureFounding(Map map, int today, int year)
        {
            if (Get("founding") != null) return;
            var d = new CalendarDay { id = "founding", kind = CalendarKind.Founding, way = DayWay.Feast, weight = CalendarMath.FoundingWeight };
            // The game's first tick, as a game tick (0); its date where the colony stands.
            d.tick = 0;
            d.day = CalendarMath.Wrap(GenDate.DayOfYear(Find.TickManager.gameStartAbsTick, Longitude(map)));
            d.sinceYear = GenDate.Year(Find.TickManager.gameStartAbsTick, Longitude(map));
            d.lastYear = Find.TickManager.TicksGame < GenDate.TicksPerDay * 2 ? year : CalendarMath.InitialLastYear(today, year, d.day, false);
            TryAdd(d, false);
        }

        // ---------------- People: founding members and birthdays ----------------

        private void ScanPeople(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var p in known.Values)
            {
                if (p.Dead || !Ours(p) || p.records == null) continue;
                int here = (int)p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal);
                float years = here / (float)GenDate.TicksPerYear;
                int joined = now - here;
                // One of the colony's first members: came after the founding day itself, within the colony's first year,
                // and has been here a year since.
                if (years >= 1f && joined > GenDate.TicksPerDay * 2 && joined <= GenDate.TicksPerYear && Get("arrival|" + p.ThingID) == null)
                {
                    var d = Dated(new CalendarDay
                    {
                        id = "arrival|" + p.ThingID, kind = CalendarKind.Arrival, way = DayWay.Personal, pawnId = p.ThingID, pawnName = p.LabelShort,
                        weight = CalendarMath.ArrivalWeight(years)
                    }, Mathf.Max(0, joined), map, today, year, false);
                    TryAdd(d, false);
                }
                // A long-serving colonist's birthday (two years with the colony, children born here too).
                if (years >= 2f && p.ageTracker != null)
                {
                    var b = Get("birthday|" + p.ThingID);
                    int bday = CalendarMath.Wrap(p.ageTracker.BirthDayOfYear);
                    if (b != null)
                    {
                        b.day = bday;
                        b.pawnName = p.LabelShort;
                        b.weight = CalendarMath.BirthdayWeight(years);
                        continue;
                    }
                    var nd = new CalendarDay
                    {
                        id = "birthday|" + p.ThingID, kind = CalendarKind.Birthday, way = DayWay.Personal, pawnId = p.ThingID, pawnName = p.LabelShort,
                        weight = CalendarMath.BirthdayWeight(years), day = bday, sinceYear = year, tick = now,
                        lastYear = CalendarMath.InitialLastYear(today, year, bday, false)
                    };
                    TryAdd(nd, false);
                }
            }
        }

        // ---------------- The first harvest ----------------

        /// <summary>
        /// Once a day: the colony's first good harvest is a day when its people brought in at least ten plants and the raw
        /// food in store rose by twenty or more (so a day of felling trees doesn't count).
        /// </summary>
        private void WatchHarvest(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            if (harvestSeen) return;
            int delta = 0;
            var seen = new HashSet<string>();
            foreach (var p in known.Values)
            {
                if (p.Dead || !Ours(p) || p.records == null) continue;
                int n = p.records.GetAsInt(RecordDefOf.PlantsHarvested);
                if (harvested.TryGetValue(p.ThingID, out int before)) delta += Mathf.Max(0, n - before);
                harvested[p.ThingID] = n;
                seen.Add(p.ThingID);
            }
            foreach (var k in harvested.Keys.Where(k => !seen.Contains(k)).ToList()) harvested.Remove(k);
            int raw = ThingCategoryDefOf.PlantFoodRaw != null ? map.resourceCounter.GetCountIn(ThingCategoryDefOf.PlantFoodRaw) : 0;
            int rawDelta = rawFoodYesterday >= 0 ? raw - rawFoodYesterday : 0;
            rawFoodYesterday = raw;
            if (GenDate.DaysPassed < 3 || delta < 10 || rawDelta < 20) return;
            AddHarvest(map, Find.TickManager.TicksGame - GenDate.TicksPerDay, today, year, true);
        }

        private void AddHarvest(Map map, int tick, int today, int year, bool now)
        {
            harvestSeen = true;
            harvested.Clear();
            if (Get("harvest") != null) return;
            var d = Dated(new CalendarDay { id = "harvest", kind = CalendarKind.Harvest, way = DayWay.Feast, weight = CalendarMath.HarvestWeight },
                Mathf.Max(0, tick), map, today, year, now);
            if (TryAdd(d, now) != null && now && On)
            {
                var grower = AtHome(map).Where(p => !p.DevelopmentalStage.Juvenile())
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).FirstOrDefault();
                if (grower != null) CouncilGameComponent.Instance?.journals.WriteTagged(grower, "Council_Journal_Cal_Harvest", new[] { "new" }, null, d.label.Named("NAME"), 0.Named("YEARS"));
            }
        }

        // ---------------- The first snow ----------------

        /// <summary>
        /// Every hour: the colony's first snow goes on the calendar, and after that the first snow of each winter (after
        /// ten days or more without, and a month since the last) brings the day round.
        /// </summary>
        private void WatchSnow(Map map, int now)
        {
            var weather = map.weatherManager;
            if (weather == null || weather.SnowRate < 0.05f || map.mapTemperature.OutdoorTemp > 2f) return;
            bool longGap = now - lastSnowTick > GenDate.TicksPerDay * 10;
            lastSnowTick = now;
            if (!snowSeen)
            {
                snowSeen = true;
                lastSnowMarkTick = now;
                int today = GenLocalDate.DayOfYear(map);
                int year = GenLocalDate.Year(map);
                var d = new CalendarDay
                {
                    id = "snow", kind = CalendarKind.FirstSnow, way = DayWay.Games, weight = CalendarMath.SnowWeight,
                    day = today, sinceYear = year, tick = now, lastYear = year
                };
                if (TryAdd(d, false) == null || !On) return;
                bool young = now < GenDate.TicksPerYear;
                News.Post((young ? "CC_Cal_FirstSnowEver" : "CC_Cal_FirstSnowNoted").Translate().Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                var writer = AtHome(map).RandomElementWithFallback();
                if (writer != null)
                    CouncilGameComponent.Instance?.journals.WriteTagged(writer, "Council_Journal_Cal_FirstSnow", new[] { "new" }, null, d.label.Named("NAME"), 0.Named("YEARS"));
                return;
            }
            if (longGap && now - lastSnowMarkTick > GenDate.TicksPerDay * 30 && Get("snow") != null)
            {
                snowPendingTick = now;
                lastSnowMarkTick = now;
            }
        }

        // ---------------- Battles ----------------

        /// <summary>A fight at home is over and won (from WorldReactions.Victory): a big one, or one that cost us, is remembered.</summary>
        public static void Notify_Victory(List<Pawn> fighters, Faction foe, int kills, List<string> fallen)
        {
            var self = Instance;
            if (self == null) return;
            try
            {
                fallen = fallen ?? new List<string>();
                if (kills < 6 && fallen.Count == 0) return;
                var map = Home;
                if (map == null) return;
                string foeName = foe == null ? "CC_Victory_Beasts".Translate().ToString() : foe.Hidden ? foe.def.pawnsPlural : foe.Name;
                int now = Find.TickManager.TicksGame;
                var knew = (fighters ?? new List<Pawn>()).Concat(AtHome(map)).Where(p => p != null && !p.Dead).Select(p => p.ThingID).Distinct().ToList();
                self.AddBattle(map, "battle|" + now, foeName, kills, fallen, knew, now, CalendarMath.BattleWeight(kills, fallen.Count), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar victory", e);
            }
        }

        private CalendarDay AddBattle(Map map, string id, string enemy, int kills, List<string> fallen, List<string> knew, int tick, float weight, bool now)
        {
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            // A nemesis brought down in a battle already on the calendar is the same day.
            var same = days.FirstOrDefault(x => x.kind == CalendarKind.Battle && Mathf.Abs(x.tick - tick) < GenDate.TicksPerDay * 1.5f);
            if (same != null)
            {
                bool nemesis = id.StartsWith("nemesis|");
                if (nemesis && !same.id.StartsWith("nemesis|"))
                {
                    // The day takes the nemesis's name, unless the player has named it themselves.
                    bool ownName = same.byPlayer || (!same.label.NullOrEmpty() && same.label != DefaultLabel(same));
                    same.id = id;
                    same.enemy = enemy;
                    if (!ownName) same.label = null;
                }
                same.weight = Mathf.Max(same.weight, weight);
                same.kills = Mathf.Max(same.kills, kills);
                foreach (var f in fallen.Where(f => !same.fallen.Contains(f))) same.fallen.Add(f);
                foreach (var k in knew.Where(k => !same.knew.Contains(k))) same.knew.Add(k);
                if (same.label.NullOrEmpty()) same.label = DefaultLabel(same);
                return same;
            }
            var d = Dated(new CalendarDay
            {
                id = id, kind = CalendarKind.Battle, way = DefaultWay(CalendarKind.Battle, fallen.Count > 0), enemy = enemy, kills = kills,
                fallen = new List<string>(fallen), knew = new List<string>(knew), weight = weight
            }, Mathf.Max(0, tick), map, today, year, now);
            return TryAdd(d, now);
        }

        /// <summary>Once a day: a nemesis brought down goes on the calendar as the day they fell.</summary>
        private void WatchNemeses(Map map, Dictionary<string, Pawn> known, int today, int year)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.nemeses?.all == null) return;
            int now = Find.TickManager.TicksGame;
            int newest = nemesisSeenTick;
            foreach (var n in comp.nemeses.all)
            {
                if (n == null || !n.ended || n.endedTick <= nemesisSeenTick) continue;
                newest = Mathf.Max(newest, n.endedTick);
                if (n.pawn == null || !n.pawn.Dead) continue;
                var knew = known.Values.Where(p => !p.Dead && Ours(p)).Select(p => p.ThingID).ToList();
                if (n.victim != null && !knew.Contains(n.victim.ThingID)) knew.Add(n.victim.ThingID);
                AddBattle(map, "nemesis|" + n.endedTick, n.Name, 1, new List<string>(), knew, n.endedTick, CalendarMath.NemesisWeight, now - n.endedTick < GenDate.TicksPerDay * 2);
            }
            nemesisSeenTick = newest;
        }

        // ---------------- Deaths ----------------

        /// <summary>A colonist has died (from the vendettas' watch on losses): if the colony loved them, their day is remembered.</summary>
        public static void Notify_Death(Pawn dead)
        {
            var self = Instance;
            if (self == null || dead == null || dead.RaceProps == null || !dead.RaceProps.Humanlike) return;
            try
            {
                var map = Home;
                if (map == null) return;
                self.AddDeath(map, dead, Find.TickManager.TicksGame, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar death", e);
            }
        }

        /// <summary>The dead, as the living felt about them: who knew them, how many loved them, and whether family is left.</summary>
        private CalendarDay AddDeath(Map map, Pawn dead, int tick, bool now)
        {
            if (dead == null || Get("death|" + dead.ThingID) != null) return null;
            var comp = CouncilGameComponent.Instance;
            var living = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(o => o != dead && People.HasInnerLife(o) && !o.DevelopmentalStage.Baby() && o.relations != null && !o.IsQuestLodger()).Distinct().ToList();
            int loved = 0;
            bool family = false;
            var knew = new List<string>();
            var kinIds = new List<string>();
            foreach (var o in living)
            {
                var rels = o.GetRelations(dead).ToList();
                bool kin = rels.Any(r => r.familyByBloodRelation || r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance);
                int opinion = o.relations.OpinionOf(dead);
                float warmth = comp?.social?.Warmth(o, dead) ?? 0f;
                if (rels.Count > 0 || opinion >= 15 || warmth >= 25f) knew.Add(o.ThingID);
                if (kin || opinion >= 40 || warmth >= 50f) loved++;
                if (kin)
                {
                    family = true;
                    kinIds.Add(o.ThingID);
                }
            }
            float years = YearsWithUs(dead);
            // Someone loved them, or they were here a good while and known.
            if (loved == 0 && !(years >= 2f && knew.Count >= 2)) return null;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var d = Dated(new CalendarDay
            {
                id = "death|" + dead.ThingID, kind = CalendarKind.Death, way = DayWay.Remembrance, pawnId = dead.ThingID, pawnName = dead.LabelShort,
                knew = knew, kin = kinIds, weight = CalendarMath.DeathWeight(loved, years, family)
            }, Mathf.Max(0, tick), map, today, year, now);
            return TryAdd(d, false);
        }

        // ---------------- Weddings ----------------

        /// <summary>Two people were married (from WorldReactions.Wedding): the colony's wedding days.</summary>
        public static void Notify_Wedding(Pawn a, Pawn b)
        {
            var self = Instance;
            if (self == null || a == null || b == null) return;
            try
            {
                if (!(Ours(a) || Ours(b))) return;
                var map = Home;
                if (map == null) return;
                self.AddWedding(map, a, b, Find.TickManager.TicksGame, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar wedding", e);
            }
        }

        private CalendarDay AddWedding(Map map, Pawn a, Pawn b, int tick, bool now)
        {
            if (a == null || b == null) return null;
            // The same id whoever is named first.
            if (string.CompareOrdinal(a.ThingID, b.ThingID) > 0) (a, b) = (b, a);
            string id = "wedding|" + a.ThingID + "|" + b.ThingID;
            if (Get(id) != null) return null;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var d = Dated(new CalendarDay
            {
                id = id, kind = CalendarKind.Wedding, way = DayWay.Feast, pawnId = a.ThingID, pawnName = a.LabelShort, otherId = b.ThingID,
                otherName = b.LabelShort, weight = CalendarMath.WeddingWeight
            }, Mathf.Max(0, tick), map, today, year, now);
            return TryAdd(d, now);
        }

        // ---------------- Named days ----------------

        /// <summary>A day the player names from the Calendar page.</summary>
        public CalendarDay AddNamed(string label, DayWay way, int day)
        {
            if (label.NullOrEmpty() || days.Count(d => d.byPlayer) >= CalendarMath.MaxNamedDays) return null;
            var map = Home;
            int today = map != null ? GenLocalDate.DayOfYear(map) : 0;
            int year = map != null ? GenLocalDate.Year(map) : 0;
            int hour = map != null ? GenLocalDate.HourOfDay(map) : 0;
            day = CalendarMath.Wrap(day);
            var d = new CalendarDay
            {
                id = "holiday|" + nextNamedId++, kind = CalendarKind.Holiday, way = way == DayWay.Personal ? DayWay.Feast : way, label = label.Trim(),
                byPlayer = true, day = day, sinceYear = year, tick = Find.TickManager.TicksGame,
                // A day named for today still comes today, if there's time left in it.
                lastYear = CalendarMath.InitialLastYear(today, year, day, hour <= 18)
            };
            days.Add(d);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Cal_Chronicle_Named".Translate(d.label, DateText(day)).Resolve()));
            return d;
        }

        // ---------------- Letting go ----------------

        /// <summary>
        /// Once a day: a day about someone who is gone is let go quietly (a birthday, an arrival, a wedding once either
        /// is gone), and so is a remembrance nobody left in the colony shares, unless it became a tradition. Someone the
        /// game can't find at all (away somewhere it doesn't look) is given three days before their day is let go.
        /// </summary>
        private void Prune(Dictionary<string, Pawn> known)
        {
            foreach (var d in days.ToList())
            {
                switch (d.kind)
                {
                    case CalendarKind.Birthday:
                    case CalendarKind.Arrival:
                        if (Gone(d, d.pawnId, known)) days.Remove(d);
                        break;
                    case CalendarKind.Wedding:
                        if (Gone(d, d.pawnId, known) || Gone(d, d.otherId, known)) days.Remove(d);
                        break;
                    case CalendarKind.Death:
                        // Brought back (a resurrection): no day to remember.
                        if (Living(d.pawnId, known) != null) days.Remove(d);
                        else if (!d.tradition && d.knew.Count > 0 && !d.knew.Any(k => Living(k, known) != null)) days.Remove(d);
                        break;
                    case CalendarKind.Battle:
                    case CalendarKind.Homecoming:
                    case CalendarKind.GreatHunt:
                        if (!d.tradition && d.knew.Count > 0 && !d.knew.Any(k => Living(k, known) != null)) days.Remove(d);
                        break;
                }
            }
        }

        private readonly Dictionary<string, int> missing = new Dictionary<string, int>();

        private bool Gone(CalendarDay d, string id, Dictionary<string, Pawn> known)
        {
            if (id == null) return true;
            if (known.TryGetValue(id, out var p))
            {
                missing.Remove(id);
                return p.Dead || !Ours(p);
            }
            missing.TryGetValue(id, out int n);
            missing[id] = ++n;
            return n >= 3;
        }

        // ---------------- An older save ----------------

        /// <summary>
        /// The first time the calendar runs in a game already under way: the founding day, then what the chronicle still
        /// remembers (weddings, battles, eulogies, the harvest home), the colony's dead whose bodies are still about, and
        /// the nemeses brought down. Whatever can't be read is left out.
        /// </summary>
        private void Backfill(Map map, int today, int year)
        {
            EnsureFounding(map, today, year);
            if (Find.TickManager.TicksGame < GenDate.TicksPerDay) return;
            var comp = CouncilGameComponent.Instance;
            var known = Known();
            if (comp != null)
            {
                var wedding = Pattern("CC_World_Wedding", true);
                var clean = Pattern("CC_Victory_Clean", false);
                var costly = Pattern("CC_Victory_Costly", false);
                var eulogy = Pattern("CC_Eulogy_Text", false);
                var dilemma = Pattern("CC_Chronicle_Dilemma", true);
                string harvestHome = DefDatabase<DilemmaDef>.GetNamedSilentFail("Council_Dilemma_HarvestHome")?.label;
                foreach (var e in comp.chronicle.ToList())
                {
                    if (e?.text == null) continue;
                    try
                    {
                        switch (e.kind)
                        {
                            case ChronicleKind.Life:
                            {
                                var parts = CalendarMath.Parse(wedding, e.text);
                                if (parts == null || parts.Count < 2) break;
                                Pawn a = e.pawnId != null && known.TryGetValue(e.pawnId, out var pa) ? pa : ByName(known, parts[0]);
                                Pawn b = a?.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse) ?? ByName(known, a?.LabelShort == parts[0] ? parts[1] : parts[0]);
                                if (a != null && b != null && !a.Dead && !b.Dead && (Ours(a) || Ours(b))) AddWedding(map, a, b, e.tick, false);
                                break;
                            }
                            case ChronicleKind.World:
                            {
                                var parts = CalendarMath.Parse(costly, e.text);
                                bool cost = parts != null && parts.Count >= 3;
                                if (!cost) parts = CalendarMath.Parse(clean, e.text);
                                if (parts == null || parts.Count < 2 || !int.TryParse(parts[1], out int kills)) break;
                                var fallen = cost ? new List<string> { parts[2] } : new List<string>();
                                if (kills < 6 && fallen.Count == 0) break;
                                int ago = Find.TickManager.TicksGame - e.tick;
                                var knew = known.Values.Where(p => !p.Dead && Ours(p) && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) >= ago)
                                    .Select(p => p.ThingID).ToList();
                                if (knew.Count == 0) break;
                                AddBattle(map, "battle|" + e.tick, parts[0], kills, fallen, knew, e.tick, CalendarMath.BattleWeight(kills, cost ? 1 : 0), false);
                                break;
                            }
                            case ChronicleKind.Loss:
                            {
                                var parts = CalendarMath.Parse(eulogy, e.text);
                                if (parts == null || parts.Count < 1) break;
                                var dead = known.Values.FirstOrDefault(p => p.Dead && p.Faction == Faction.OfPlayer && p.LabelShort == parts[0]);
                                if (dead != null) AddDeath(map, dead, Mathf.Max(0, e.tick - GenDate.TicksPerHour * 15), false);
                                break;
                            }
                            case ChronicleKind.Council:
                            {
                                if (harvestHome.NullOrEmpty() || Get("harvest") != null) break;
                                var parts = CalendarMath.Parse(dilemma, e.text);
                                if (parts != null && parts.Count >= 1 && parts[0] == harvestHome) AddHarvest(map, e.tick, today, year, false);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce("calendar backfill entry", ex);
                    }
                }
            }
            // The colony's dead whose bodies are still about (in a grave, a sarcophagus, on the map): their death's date.
            foreach (var p in known.Values.Where(p => p.Dead && p.Faction == Faction.OfPlayer).ToList())
            {
                var corpse = p.Corpse;
                if (corpse == null || corpse.timeOfDeath <= 0 || Get("death|" + p.ThingID) != null) continue;
                AddDeath(map, p, corpse.timeOfDeath, false);
            }
            WatchNemeses(map, known, today, year);
        }

        /// <summary>A chronicle line's template as a pattern (null when the key has no text).</summary>
        private static System.Text.RegularExpressions.Regex Pattern(string key, bool whole) =>
            Translator.TryTranslate(key, out TaggedString raw) && !raw.RawText.NullOrEmpty() ? CalendarMath.TemplateRegex(raw.RawText, whole) : null;

        private static Pawn ByName(Dictionary<string, Pawn> known, string name) =>
            name.NullOrEmpty() ? null : known.Values.FirstOrDefault(p => p.LabelShort == name);
    }
}
