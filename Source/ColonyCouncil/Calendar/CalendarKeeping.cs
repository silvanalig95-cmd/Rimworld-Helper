using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    [DefOf]
    public static class CalendarDefOf
    {
        public static ThoughtDef Council_Cal_Remembered;
        public static ThoughtDef Council_Cal_Birthday;
        public static ThoughtDef Council_Cal_FeastDay;
        public static ThoughtDef Council_Cal_GamesDay;
        public static ThoughtDef Council_Cal_RestDay;

        static CalendarDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(CalendarDefOf));
    }

    /// <summary>
    /// What happens when a day comes round. A feast, games or a rest day comes as a letter the council answers (keep it,
    /// keep it with the faith's own rite where Ideology has one, or let it pass this year); a remembrance comes as a
    /// letter and is kept; a birthday or a founding member's arrival comes as a message. Keeping a day does the thing (a
    /// vanilla party for a feast, recreation and a lift for games, work hours turned to recreation for a rest day, a quiet
    /// bittersweet hour for those who knew the dead), writes journal lines, sets people talking, and counts toward a
    /// tradition: three years running and the colony expects it, through the same moods, letters, chronicle and annals
    /// as the council's other traditions. The founding day is the founding event's to keep when Rimfolk events are on.
    /// </summary>
    public partial class CalendarTracker
    {
        // The game's own rites and other mods' that can be held any day: Alpha Memes' tea ceremony, coffee roasting and
        // banquet, Vanilla Ideology Expanded - Memes' bonfire, trading fair, wicker man and star-gazing.
        private static readonly string[] FeastWords =
        {
            "feast", "party", "celebrat", "festival", "dance", "drum", "skylantern", "banquet", "bonfire", "concert", "teaceremony", "coffeeroasting",
            "tradingfair", "wickerman", "divinethestars"
        };

        public static int YearsOf(CalendarDay d, int occurrence) => Mathf.Max(d.byPlayer ? 0 : 1, occurrence - d.sinceYear);

        /// <summary>Which line pools speak for a day: its own kind's, or, for the player's days and a day kept another way, the way's.</summary>
        public static string LinesKey(CalendarDay d)
        {
            if (d.kind == CalendarKind.Holiday) return d.way.ToString();
            if (d.way == DayWay.Personal || d.way == DefaultWay(d.kind, d.fallen.Count > 0)) return d.kind.ToString();
            return d.way.ToString();
        }

        public static string WayLabel(DayWay way) => ("CC_Cal_Way_" + way).Translate();

        /// <summary>One of the colony's own people, alive, wherever they are (at home, with a caravan, at an outpost).</summary>
        private static Pawn FindOurs(string id)
        {
            if (id == null) return null;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                if (p.ThingID == id) return Ours(p) ? p : null;
            foreach (var p in People.OffMapColonists())
                if (p.ThingID == id) return Ours(p) ? p : null;
            return null;
        }

        /// <summary>Who stands for the day: the one it's about, the closest to the dead, the best grower, the longest here.</summary>
        private static Pawn Keeper(CalendarDay d, List<Pawn> home)
        {
            if (home.Count == 0) return null;
            var adults = home.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            if (adults.Count == 0) adults = home;
            switch (d.kind)
            {
                case CalendarKind.Death:
                case CalendarKind.Battle:
                    return adults.Where(p => d.knew.Contains(p.ThingID)).OrderByDescending(p => d.kin.Contains(p.ThingID)).ThenByDescending(YearsWithUs).FirstOrDefault()
                           ?? adults.OrderByDescending(YearsWithUs).First();
                case CalendarKind.Wedding:
                case CalendarKind.Birthday:
                case CalendarKind.Arrival:
                    return home.FirstOrDefault(p => p.ThingID == d.pawnId) ?? home.FirstOrDefault(p => p.ThingID == d.otherId) ?? adults.RandomElement();
                case CalendarKind.Harvest:
                    return adults.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).First();
                default:
                    return adults.OrderByDescending(YearsWithUs).First();
            }
        }

        private static List<string> Tags(CalendarDay d, int years)
        {
            var t = new List<string> { years <= 1 ? "first" : "years", "kind:" + d.kind.ToString().ToLowerInvariant(), "way:" + d.way.ToString().ToLowerInvariant() };
            if (d.tradition) t.Add("tradition");
            if (d.byPlayer) t.Add("named");
            if (d.kind == CalendarKind.Battle)
            {
                // A nemesis brought down has lines of its own; a battle won cleanly or at a cost, its own.
                if (d.fallen.Count > 0) t.Add("fallen");
                else t.Add(d.id.StartsWith("nemesis|") ? "nemesis" : "clean");
            }
            return t;
        }

        /// <summary>The values a day's lines may use: {NAME}, {YEARS}, {DEAD}, {ENEMY}, {COUNT}, {PLACE}.</summary>
        private static List<NamedArgument> Args(CalendarDay d, int years, Map map)
        {
            string dead = d.kind == CalendarKind.Death ? d.pawnName : d.kind == CalendarKind.Battle ? TextUtil.JoinAnd(d.fallen) : "";
            return new List<NamedArgument>
            {
                (d.label ?? "").Named("NAME"), years.Named("YEARS"), (dead ?? "").Named("DEAD"), (d.enemy ?? "").Named("ENEMY"),
                d.kills.Named("COUNT"), (map?.Biome?.label ?? "").Named("PLACE")
            };
        }

        private static string Fill(string line, CalendarDay d, int years, Map map, Pawn a, Pawn b)
        {
            if (line.NullOrEmpty()) return line;
            var args = Args(d, years, map);
            if (a != null) args.Add(a.Named("A"));
            if (b != null) args.Add(b.Named("B"));
            return line.Formatted(args.ToArray()).Resolve();
        }

        // ---------------- Coming round ----------------

        private static bool FoundingEventKeepsIt() =>
            S.worldEvents && GenDate.DaysPassed >= GenDate.DaysPerYear - 2 && DefDatabase<IncidentDef>.GetNamedSilentFail(HappeningWorker_Founding.DefName) != null;

        private void ComeRound(CalendarDay d, int occurrence, Map map)
        {
            d.lastYear = occurrence;
            if (d.kind == CalendarKind.FirstSnow) snowPendingTick = -1;
            var home = AtHome(map);
            // Nobody at home to mark it: it passes this year, without blame.
            if (home.Count == 0) return;
            int years = YearsOf(d, occurrence);
            if (d.Personal)
            {
                Personal(d, occurrence, map, years);
                return;
            }
            if (d.way == DayWay.Remembrance)
            {
                RemembranceLetter(d, map, home, years);
                KeepDay(d, occurrence, map, null, false);
                return;
            }
            // (Kept another way than a feast, it's the calendar's own day again.)
            if (d.kind == CalendarKind.Founding && d.way == DayWay.Feast && FoundingEventKeepsIt())
            {
                // The founding event comes today with its own letter and choices; whatever the colony does with it keeps the day.
                foundingWaitYear = occurrence;
                foundingWaitUntil = Find.TickManager.TicksGame + GenDate.TicksPerDay * 3;
                return;
            }
            SendLetter(d, occurrence, map, home, years);
        }

        private string Opening(CalendarDay d, Map map, Pawn keeper, Pawn other, int years)
        {
            string line = Flavor.FromPool("Council_Cal_Letter_" + LinesKey(d), keeper, null, null, Tags(d, years).ToArray());
            return Fill(line, d, years, map, keeper, other);
        }

        /// <summary>A wedding day's couple, as {A} and {B} in its lines (the keeper otherwise, and nobody as {B}).</summary>
        private static void Couple(CalendarDay d, Pawn keeper, out Pawn a, out Pawn b)
        {
            a = keeper;
            b = null;
            if (d.kind != CalendarKind.Wedding) return;
            var first = FindOurs(d.pawnId);
            var second = FindOurs(d.otherId);
            if (first == null || second == null) return;
            a = first;
            b = second;
        }

        private static string StatusLine(CalendarDay d)
        {
            if (d.tradition) return "CC_Cal_Status_Tradition".Translate(d.timesKept).Resolve();
            int left = CalendarMath.YearsToTradition(d.keptYears, false);
            if (d.keptYears <= 0) return "CC_Cal_Status_New".Translate(CalendarMath.TraditionAfter).Resolve();
            if (left == 1) return "CC_Cal_Status_Close".Translate(d.keptYears).Resolve();
            return "CC_Cal_Status_Count".Translate(d.keptYears, left).Resolve();
        }

        private void SendLetter(CalendarDay d, int occurrence, Map map, List<Pawn> home, int years)
        {
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("Council_CalendarLetter");
            if (def == null)
            {
                KeepDay(d, occurrence, map, null, true);
                return;
            }
            Couple(d, Keeper(d, home), out var keeper, out var other);
            // A wedding day whose couple can't be found this year (away somewhere the game doesn't look) passes quietly.
            if (d.kind == CalendarKind.Wedding && other == null) return;
            var ritual = RitualFor(d.way);
            var parts = new List<string> { Opening(d, map, keeper, other, years), ("CC_Cal_Way_" + d.way + "_Letter").Translate().Resolve(), StatusLine(d) };
            if (ritual != null) parts.Add("CC_Cal_Ritual_Feast".Translate(IdeoName(), ritual.LabelCap).Resolve());
            int hour = GenLocalDate.HourOfDay(map);
            int ticks = Mathf.Max(2, 22 - hour) * GenDate.TicksPerHour;
            string keepLabel = ("CC_Cal_Keep_" + d.way).Translate();
            parts.Add("CC_Cal_Deadline".Translate(TextUtil.DaysString(ticks), keepLabel).Resolve());
            var look = new LookTargets(new[] { keeper, other }.Where(p => p != null && p.Spawned).Distinct());
            var letter = LetterMaker.MakeLetter(d.label.CapitalizeFirst(), string.Join("\n\n", parts.Where(p => !p.NullOrEmpty())), def, look) as ChoiceLetter_CalendarDay;
            if (letter == null)
            {
                KeepDay(d, occurrence, map, null, true);
                return;
            }
            letter.dayId = d.id;
            letter.occurrence = occurrence;
            letter.ritual = ritual?.LabelCap;
            letter.keepLabel = keepLabel;
            letter.StartTimeout(ticks);
            Find.LetterStack.ReceiveLetter(letter);
        }

        private void RemembranceLetter(CalendarDay d, Map map, List<Pawn> home, int years)
        {
            var keeper = Keeper(d, home);
            var who = Mourners(d, home);
            var parts = new List<string> { Opening(d, map, keeper, null, years) };
            if (d.kind == CalendarKind.Holiday || d.knew.Count == 0 || who.Count == 0) parts.Add("CC_Cal_Way_RemembranceAll_Letter".Translate().Resolve());
            else parts.Add("CC_Cal_Way_Remembrance_Letter".Translate(TextUtil.JoinAnd(who.Select(p => p.LabelShort).ToList())).Resolve());
            var ritual = RitualFor(DayWay.Remembrance);
            if (ritual != null) parts.Add("CC_Cal_Ritual_Remembrance".Translate(IdeoName(), ritual.LabelCap).Resolve());
            Find.LetterStack.ReceiveLetter(d.label.CapitalizeFirst(), string.Join("\n\n", parts.Where(p => !p.NullOrEmpty())), LetterDefOf.NeutralEvent,
                new LookTargets(who.Count > 0 ? who : new List<Pawn> { keeper }));
        }

        /// <summary>
        /// Those at home who keep a remembrance: the ones who knew the dead or fought that day; for a named day, or a day
        /// that isn't about anyone in particular, everyone.
        /// </summary>
        private static List<Pawn> Mourners(CalendarDay d, List<Pawn> home)
        {
            if (d.kind == CalendarKind.Holiday || d.knew.Count == 0) return home.Where(p => !IsPsychopath(p)).ToList();
            return home.Where(p => d.knew.Contains(p.ThingID) && !IsPsychopath(p)).ToList();
        }

        private static bool IsPsychopath(Pawn p) => p.story?.traits?.HasTrait(TraitDefOf.Psychopath) ?? false;

        private static bool Industrious(Pawn p) => TraitDefOf.Industriousness != null && (p.story?.traits?.DegreeOfTrait(TraitDefOf.Industriousness) ?? 0) >= 2;

        /// <summary>The council answered a day's letter (or it ran out of time: the day is kept).</summary>
        public void Resolve(ChoiceLetter_CalendarDay letter, int choice, bool byDefault)
        {
            if (letter == null || letter.resolved) return;
            letter.resolved = true;
            var d = Get(letter.dayId);
            var map = Home;
            if (d == null || map == null) return;
            try
            {
                if (choice == ChoiceLetter_CalendarDay.PassChoice) PassDay(d, map);
                else KeepDay(d, letter.occurrence, map, choice == ChoiceLetter_CalendarDay.RitualChoice ? letter.ritual : null, byDefault);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar answer", e);
            }
        }

        // ---------------- Keeping a day ----------------

        /// <summary>
        /// The colony keeps the day: the way it's kept (unless the faith's rite keeps it, or the founding event already
        /// did), a step toward a tradition, journal lines, talk, and a line in the chronicle.
        /// </summary>
        private void KeepDay(CalendarDay d, int occurrence, Map map, string ritual, bool byDefault, bool quiet = false)
        {
            var comp = CouncilGameComponent.Instance;
            var home = AtHome(map);
            int years = YearsOf(d, occurrence);
            using var _cause = Causes.Push("CC_Cal_Cause_Day".Translate(d.label).Resolve());
            bool told = false;
            if (ritual != null)
            {
                Messages.Message("CC_Cal_Ritual_Chosen".Translate(d.label, ritual).Resolve(), MessageTypeDefOf.NeutralEvent, false);
                told = true;
            }
            else if (!quiet) told = DoWay(d, map, home);
            var result = CalendarMath.Keep(ref d.keptYears, ref d.tradition, ref d.breaks);
            d.timesKept++;
            d.lastKeptYear = occurrence;
            Tradition(d, result, home, comp);
            if (!quiet) comp?.offices.Notify_CustomKept(home);
            if (!quiet) Journals(d, map, home, years);
            if (!quiet) Interplay.Notify_DayKept(d, home);
            talkDayId = d.id;
            talkDate = DateKey(map);
            talksLeft = Rand.RangeInclusive(2, 3);
            var keeper = Keeper(d, home);
            if (!quiet && keeper != null) CouncilRecords.Add(keeper, CouncilRecords.DaysKept);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Cal_Chronicle_Kept".Translate(d.label).Resolve(), keeper));
            if (!told && !quiet && d.way != DayWay.Remembrance)
                News.Post("CC_Cal_Kept".Translate(d.label.CapitalizeFirst(), WayLabel(d.way)).Resolve(), new LookTargets(home), NewsKind.Colony, MessageTypeDefOf.PositiveEvent,
                    byDefault ? Importance.Notable : Importance.Flavor);
        }

        /// <summary>The way a day is kept. Returns true when something the player sees already says so (the party's own letter).</summary>
        private bool DoWay(CalendarDay d, Map map, List<Pawn> home)
        {
            switch (d.way)
            {
                case DayWay.Feast:
                    // With answers playing out: a meal from the stores eaten together, then the evening; its lift comes after.
                    if (PlayDay(d, map, home, PlayKind.Feast, CalendarDefOf.Council_Cal_FeastDay, null, null, null)) return true;
                    if (StartParty(map, Interplay.CalendarOrganizer(d, home) ?? Keeper(d, home))) return true;
                    // No gathering possible (nowhere to gather, everyone busy): a better supper, and a little lift.
                    foreach (var p in home) CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_FeastDay);
                    return false;
                case DayWay.Games:
                    if (PlayDay(d, map, home, PlayKind.Gathering, CalendarDefOf.Council_Cal_GamesDay, null,
                            new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Outdoors, PlaySpot.Gather, PlaySpot.Dining }, null))
                        return true;
                    foreach (var p in home)
                    {
                        p.needs?.joy?.GainJoy(0.15f, JoyKindDefOf.Social);
                        CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_GamesDay);
                    }
                    return false;
                case DayWay.Rest:
                    GiveRest(map, home);
                    foreach (var p in home.Where(p => !Industrious(p))) CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_RestDay);
                    return false;
                case DayWay.Remembrance:
                    string subject = d.kind == CalendarKind.Death ? d.pawnName
                        : d.kind == CalendarKind.Battle ? "CC_Cal_Subject_Battle".Translate(d.enemy ?? "?").Resolve()
                        : d.label;
                    string cause = "CC_Cal_Cause_Remembered".Translate(subject ?? "?").Resolve();
                    var mourners = Mourners(d, home);
                    // With answers playing out, those who knew them stand a while at the grave, if there is one here.
                    var grave = d.kind == CalendarKind.Death ? PlaySpots.GraveOf(map, d.pawnId) : null;
                    if (grave != null && PlayDay(d, map, mourners, PlayKind.Toast, CalendarDefOf.Council_Cal_Remembered, cause,
                            new List<PlaySpot> { PlaySpot.Grave }, grave))
                        return false;
                    foreach (var p in mourners) CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_Remembered, null, -1f, cause);
                    return false;
            }
            return false;
        }

        /// <summary>
        /// A day kept on the map (PlayOut/, with "Answers play out on the map" on): those it's for gather for it, and
        /// <paramref name="thought"/> comes to those who were there when it's over (to all of them if it never happens).
        /// False when there's nothing to set going: the day is kept the old way.
        /// </summary>
        private static bool PlayDay(CalendarDay d, Map map, List<Pawn> who, PlayKind kind, ThoughtDef thought, string cause, List<PlaySpot> spots, Thing grave)
        {
            var tracker = PlayOutTracker.Instance;
            if (!PlayOuts.On || tracker == null || map == null || who == null || who.Count == 0) return false;
            try
            {
                int now = Find.TickManager.TicksGame;
                // The keeper of days organises the colony's own days; those deep in grief sit out a feast or games (Interplay/).
                var keeper = Interplay.CalendarOrganizer(d, who) ?? Keeper(d, who);
                var p = new PlayOut
                {
                    kind = kind,
                    map = map,
                    created = now,
                    label = d.label,
                    invited = Interplay.WithoutMourners(d, kind, grave != null, who.Where(PlayOuts.OurPerson).ToList()),
                    organizer = keeper != null && who.Contains(keeper) ? keeper : null,
                    spots = spots ?? new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors },
                    place = grave,
                    durationTicks = PlayOutMath.DurationTicks(kind == PlayKind.Toast ? 1f : 3f),
                    pool = kind == PlayKind.Feast ? "Council_Pool_PlayFeast" : grave != null ? "Council_Pool_PlayRemembrance" : "Council_Pool_PlayParty"
                };
                if (p.invited.Count == 0) return false;
                p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), p.hourFrom, p.hourTo, PlayOutMath.DefaultWaitHours);
                if (thought != null)
                    foreach (var x in p.invited)
                        p.moods.Add(new HeldMood { pawn = x, thought = thought, cause = cause ?? Causes.Current });
                tracker.Add(p);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar play out", e);
                return false;
            }
        }

        /// <summary>A vanilla party, thrown by whoever stands for the day if they can, else by anyone.</summary>
        private static bool StartParty(Map map, Pawn organizer)
        {
            var g = GatheringDefOf.Party;
            if (g?.Worker == null || map == null) return false;
            try
            {
                if (organizer != null && organizer.Spawned && organizer.Map == map && g.Worker.CanExecute(map, organizer) && g.Worker.TryExecute(map, organizer)) return true;
                return g.Worker.CanExecute(map) && g.Worker.TryExecute(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar feast", e);
                return false;
            }
        }

        // ---------------- A rest day ----------------

        /// <summary>
        /// The rest of today's work hours (Work or Anything) become recreation, for everyone at home except the
        /// industrious, who'd rather work. Sleep and other assignments are left alone. Tomorrow they go back.
        /// </summary>
        private void GiveRest(Map map, List<Pawn> home)
        {
            if (restDate >= 0) RestoreRest();
            int hour = GenLocalDate.HourOfDay(map);
            var joy = TimeAssignmentDefOf.Joy;
            if (joy == null) return;
            foreach (var p in home)
            {
                if (p.timetable == null || Industrious(p)) continue;
                var changed = new List<string>();
                for (int h = hour; h < GenDate.HoursPerDay; h++)
                {
                    var a = p.timetable.GetAssignment(h);
                    if (a != TimeAssignmentDefOf.Work && a != TimeAssignmentDefOf.Anything) continue;
                    changed.Add(h + "=" + a.defName);
                    p.timetable.SetAssignment(h, joy);
                }
                if (changed.Count > 0) restHours[p.ThingID] = string.Join(";", changed);
            }
            restDate = DateKey(map);
        }

        /// <summary>
        /// Time off for a few, from an answer that plays out (PlayOut/): the rest of today's work hours become recreation
        /// (or, with <paramref name="sleep"/>, sleep, recreation hours too) for these colonists, the industrious keeping
        /// theirs unless sent to bed, and they go back tomorrow with the calendar's own rest day. Returns who got any.
        /// </summary>
        public List<Pawn> RestFor(Map map, List<Pawn> pawns, bool sleep)
        {
            var given = new List<Pawn>();
            var to = sleep ? TimeAssignmentDefOf.Sleep : TimeAssignmentDefOf.Joy;
            if (map == null || pawns == null || to == null) return given;
            int today = DateKey(map);
            if (restDate >= 0 && restDate != today) RestoreRest();
            int hour = GenLocalDate.HourOfDay(map);
            foreach (var p in pawns)
            {
                if (p?.timetable == null || (!sleep && Industrious(p))) continue;
                restHours.TryGetValue(p.ThingID, out var had);
                var changed = had.NullOrEmpty() ? new List<string>() : had.Split(';').Where(s => !s.NullOrEmpty()).ToList();
                bool any = false;
                for (int h = hour; h < GenDate.HoursPerDay; h++)
                {
                    var a = p.timetable.GetAssignment(h);
                    bool free = a == TimeAssignmentDefOf.Work || a == TimeAssignmentDefOf.Anything || (sleep && a == TimeAssignmentDefOf.Joy);
                    if (!free || a == to) continue;
                    // An hour already given today keeps what it was first; only what it's turned into changes.
                    int i = changed.FindIndex(s => s.StartsWith(h + "="));
                    string original = i >= 0 ? changed[i].Substring(changed[i].IndexOf('=') + 1).Split('>')[0] : a.defName;
                    string entry = h + "=" + original + (to == TimeAssignmentDefOf.Joy ? "" : ">" + to.defName);
                    if (i >= 0) changed[i] = entry;
                    else changed.Add(entry);
                    p.timetable.SetAssignment(h, to);
                    any = true;
                }
                if (!any) continue;
                restHours[p.ThingID] = string.Join(";", changed);
                given.Add(p);
            }
            if (given.Count > 0) restDate = today;
            return given;
        }

        /// <summary>
        /// The day after a rest day: each hour turned to recreation (or to sleep, "hour=Work>Sleep") goes back to what it
        /// was, unless someone changed it since.
        /// </summary>
        private void RestoreRest()
        {
            try
            {
                var joy = TimeAssignmentDefOf.Joy;
                foreach (var kv in restHours)
                {
                    var p = FindOurs(kv.Key);
                    if (p?.timetable == null || kv.Value.NullOrEmpty()) continue;
                    foreach (var part in kv.Value.Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0 || !int.TryParse(part.Substring(0, eq), out int h) || h < 0 || h >= GenDate.HoursPerDay) continue;
                        string was = part.Substring(eq + 1);
                        var set = joy;
                        int gt = was.IndexOf('>');
                        if (gt >= 0)
                        {
                            set = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(was.Substring(gt + 1));
                            was = was.Substring(0, gt);
                        }
                        var def = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(was);
                        if (def != null && set != null && p.timetable.GetAssignment(h) == set) p.timetable.SetAssignment(h, def);
                    }
                }
            }
            finally
            {
                restHours.Clear();
                restDate = -1;
            }
        }

        // ---------------- Letting a day pass ----------------

        /// <summary>The council lets the day pass this year. Nobody minds unless it was a tradition.</summary>
        private void PassDay(CalendarDay d, Map map)
        {
            var comp = CouncilGameComponent.Instance;
            var result = CalendarMath.Pass(ref d.keptYears, ref d.tradition, ref d.breaks);
            if (result == CalendarMath.PassResult.Passed)
            {
                News.Post("CC_Cal_LetPass".Translate(d.label.CapitalizeFirst()).Resolve(), null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                return;
            }
            using var _cause = Causes.Push("CC_Cause_Tradition".Translate(d.label).Resolve());
            foreach (var p in AtHome(map))
            {
                if (p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false) continue;
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_TraditionBroken);
                comp?.standings.Change(p, -2f, null);
            }
            comp?.annals.Notify_Tradition(d.label, false);
            bool lapsed = result == CalendarMath.PassResult.Lapsed;
            News.Post((lapsed ? "CC_Tradition_Lapsed" : "CC_Tradition_Broken").Translate(d.label).Resolve(), null, NewsKind.Colony, MessageTypeDefOf.NegativeEvent);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, (lapsed ? "CC_Chronicle_TraditionLapsed" : "CC_Chronicle_TraditionBroken").Translate(d.label).Resolve(), null));
        }

        // ---------------- Traditions ----------------

        /// <summary>A day kept: a new tradition is announced, an old one kept is felt, as for the council's other traditions.</summary>
        private void Tradition(CalendarDay d, CalendarMath.KeepResult result, List<Pawn> home, CouncilGameComponent comp)
        {
            if (result == CalendarMath.KeepResult.Kept) return;
            // A remembrance kept every year is a tradition too, but its quiet hour is the mood.
            bool moods = d.way != DayWay.Remembrance;
            using var _cause = Causes.Push("CC_Cause_Tradition".Translate(d.label).Resolve());
            if (moods)
                foreach (var p in home) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_TraditionKept);
            VisualTouches.Tradition(home, d.label);
            if (result == CalendarMath.KeepResult.KeptTradition)
            {
                comp?.annals.Notify_Tradition(d.label, true);
                comp?.character?.Shift(new WayShift { tradition = 1f }, "CC_Cal_Way_Why".Translate(d.label).Resolve());
                Interplay.Notify_DayTradition(d, false);
                return;
            }
            Find.LetterStack.ReceiveLetter("CC_Tradition_BornTitle".Translate(d.label.CapitalizeFirst()).Resolve(), "CC_Tradition_BornText".Translate(d.label).Resolve(),
                LetterDefOf.PositiveEvent);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_TraditionBorn".Translate(d.label).Resolve(), null));
            comp?.annals.Notify_TraditionBorn(d.label);
            comp?.character?.Shift(new WayShift { tradition = 2f }, "CC_Cal_Way_Why".Translate(d.label).Resolve());
            Interplay.Notify_DayTradition(d, true);
            foreach (var p in home.Where(p => !p.DevelopmentalStage.Juvenile()).InRandomOrder().Take(2))
                comp?.journals.Write(p, "Council_Journal_TraditionBorn", null, d.label.Named("TRADITION"));
        }

        // ---------------- Personal days ----------------

        /// <summary>A birthday or a founding member's arrival: a message, a small mood for them, journal lines and good wishes.</summary>
        private void Personal(CalendarDay d, int occurrence, Map map, int years)
        {
            var comp = CouncilGameComponent.Instance;
            var p = FindOurs(d.pawnId);
            if (p == null) return;
            d.timesKept++;
            d.lastKeptYear = occurrence;
            var home = AtHome(map);
            var friend = home.Where(f => f != p && !f.DevelopmentalStage.Juvenile() && f.relations != null && f.relations.OpinionOf(p) >= 15)
                .OrderByDescending(f => f.relations.OpinionOf(p)).FirstOrDefault();
            var args = Args(d, years, map);
            if (d.kind == CalendarKind.Arrival)
            {
                // Their own anniversary is marked by the anniversaries (when that's on): the calendar adds a friend's memory of it.
                if (!S.anniversaries)
                {
                    string msg = Fill(Flavor.FromPool("Council_Cal_Letter_Arrival", p, null, null, Tags(d, years).ToArray()), d, years, map, p, null);
                    if (!msg.NullOrEmpty()) News.Post(msg, new LookTargets(p), NewsKind.People, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                    using (Causes.Push("CC_Cause_Anniversary".Translate().Resolve())) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Anniversary);
                }
                if (friend != null) comp?.journals.WriteTagged(friend, "Council_Journal_Cal_Arrival", Tags(d, years).ToArray(), null, args.Concat(new[] { p.Named("OTHER") }).ToArray());
                if (p.MapHeld == map)
                {
                    talkDayId = d.id;
                    talkDate = DateKey(map);
                    talksLeft = 1;
                }
                return;
            }
            string line = Fill(Flavor.FromPool("Council_Cal_Letter_Birthday", p, null, null, Tags(d, years).ToArray()), d, years, map, p, friend);
            if (!line.NullOrEmpty()) News.Post(line, new LookTargets(p), NewsKind.People, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            CouncilGameComponent.GiveThought(p, CalendarDefOf.Council_Cal_Birthday, null, -1f, "CC_Cal_Cause_Birthday".Translate().Resolve());
            var tags = Tags(d, years);
            comp?.journals.WriteTagged(p, "Council_Journal_Cal_Birthday", tags.Concat(new[] { "self" }).ToArray(), null, args.ToArray());
            if (friend != null)
                comp?.journals.WriteTagged(friend, "Council_Journal_Cal_Birthday", tags.Concat(new[] { "friend" }).ToArray(), null, args.Concat(new[] { p.Named("OTHER") }).ToArray());
            if (p.MapHeld == map)
            {
                talkDayId = d.id;
                talkDate = DateKey(map);
                talksLeft = Rand.RangeInclusive(1, 2);
            }
        }

        // ---------------- Journals ----------------

        private static List<Pawn> Writers(CalendarDay d, List<Pawn> home)
        {
            var adults = home.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            var list = new List<Pawn>();
            switch (d.kind)
            {
                case CalendarKind.Death:
                case CalendarKind.Battle:
                    list.AddRange(home.Where(p => d.knew.Contains(p.ThingID)).OrderByDescending(p => d.kin.Contains(p.ThingID)).ThenBy(_ => Rand.Value).Take(2));
                    break;
                case CalendarKind.Wedding:
                    list.AddRange(home.Where(p => p.ThingID == d.pawnId || p.ThingID == d.otherId));
                    break;
                case CalendarKind.Harvest:
                    var grower = adults.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).FirstOrDefault();
                    if (grower != null) list.Add(grower);
                    break;
            }
            foreach (var p in home.InRandomOrder())
            {
                if (list.Count >= 2) break;
                if (!list.Contains(p)) list.Add(p);
            }
            return list;
        }

        private static void Journals(CalendarDay d, Map map, List<Pawn> home, int years)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            string pool = "Council_Journal_Cal_" + LinesKey(d);
            foreach (var w in Writers(d, home))
            {
                var tags = Tags(d, years);
                var args = Args(d, years, map);
                if (d.kind == CalendarKind.Wedding)
                {
                    var spouse = FindOurs(w.ThingID == d.pawnId ? d.otherId : d.pawnId);
                    if (spouse == null) continue;
                    args.Add(spouse.Named("OTHER"));
                }
                if (d.kind == CalendarKind.Death && d.kin.Contains(w.ThingID)) tags.Add("kin");
                comp.journals.WriteTagged(w, pool, tags.ToArray(), null, args.ToArray());
            }
        }

        // ---------------- Talk ----------------

        private static bool CanTalk(Pawn p) =>
            p != null && p.Spawned && p.Awake() && !p.Downed && !p.Drafted && !p.InMentalState && !p.DevelopmentalStage.Baby()
            && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false);

        /// <summary>
        /// Through the day a kept day is talked about (two or three conversations in the social log, by those it matters
        /// to most), and the evening before a day, someone brings it up.
        /// </summary>
        private void Talk(Map map, int hour, int today, int year)
        {
            int date = year * CalendarMath.DaysPerYear + today;
            if (talkDate == date && talksLeft > 0 && hour >= 10 && hour <= 21 && Rand.Chance(0.35f))
            {
                var d = Get(talkDayId);
                if (d == null) talksLeft = 0;
                else if (DoTalk(d, map, "Council_Cal_Talk_" + LinesKey(d), YearsOf(d, d.lastYear))) talksLeft--;
            }
            if (eveTalkDate == date || hour < 12 || hour > 20 || !Rand.Chance(0.25f)) return;
            int tomorrowYear = today + 1 >= CalendarMath.DaysPerYear ? year + 1 : year;
            var eve = days.Where(x => x.kind != CalendarKind.FirstSnow && !x.Personal && CalendarMath.DaysUntil(today, x.day) == 1 && x.lastYear < tomorrowYear
                                      && (x.byPlayer || tomorrowYear > x.sinceYear))
                .OrderByDescending(x => x.Standing).FirstOrDefault();
            // Brought up once that evening, by two who are together (tried again at a later hour if nobody is).
            if (eve == null || DoTalk(eve, map, "Council_Cal_Talk_Eve", YearsOf(eve, tomorrowYear))) eveTalkDate = date;
        }

        private static bool DoTalk(CalendarDay d, Map map, string pool, int years)
        {
            var home = AtHome(map).Where(CanTalk).ToList();
            if (home.Count < 2) return false;
            Pawn a = null, b = null;
            var tags = Tags(d, years);
            bool personal = pool != "Council_Cal_Talk_Eve" && LinesKey(d) == d.kind.ToString();
            switch (d.kind)
            {
                case CalendarKind.Death:
                case CalendarKind.Battle:
                    a = home.Where(p => d.knew.Contains(p.ThingID)).RandomElementWithFallback();
                    break;
                case CalendarKind.Birthday:
                case CalendarKind.Arrival:
                    // Good wishes go to the one whose day it is, from a friend: no talk without them.
                    b = home.FirstOrDefault(p => p.ThingID == d.pawnId);
                    if (b == null) return false;
                    a = home.Where(p => p != b).OrderByDescending(p => p.relations?.OpinionOf(b) ?? 0).FirstOrDefault();
                    if (a == null) return false;
                    break;
                case CalendarKind.Wedding:
                    a = home.FirstOrDefault(p => p.ThingID == d.pawnId);
                    b = home.FirstOrDefault(p => p.ThingID == d.otherId);
                    if (a == null) a = b;
                    if (a == b) b = null;
                    // The wedding's own lines are about one of the couple; with both there, about the two of them.
                    if (a == null && personal) return false;
                    if (a != null && b != null) tags.Add("couple");
                    break;
            }
            if (a == null) a = home.RandomElement();
            if (b == null || b == a)
                b = home.Where(p => p != a).OrderByDescending(p => SocialLog.Near(a, p) ? 2f : 0f).ThenByDescending(p => d.knew.Contains(p.ThingID) ? 1f : 0f).ThenBy(_ => Rand.Value).FirstOrDefault();
            // Only a talk between two who are really together counts; otherwise it comes up at a later hour.
            if (b == null || !Together.Can(a, b)) return false;
            string line = Flavor.FromPool(pool, a, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return false;
            SocialLog.Add(a, b, Fill(line, d, years, map, a, b));
            return true;
        }

        // ---------------- The founding event ----------------

        /// <summary>
        /// The founding day with Rimfolk events on: the founding event's letter is the day's. Whatever the colony chose
        /// there (a feast, a quiet supper, the founding story) shows in its memories, and keeps the day.
        /// </summary>
        private void WatchFoundingEvent(Map map, int now)
        {
            if (now > foundingWaitUntil)
            {
                foundingWaitUntil = -1;
                return;
            }
            var feast = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_FoundingFeast");
            var supper = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_FoundingDay");
            bool marked = AtHome(map).Any(p => p.needs?.mood?.thoughts?.memories?.Memories?.Any(m => m != null && (m.def == feast || m.def == supper) && m.age < GenDate.TicksPerDay) ?? false);
            if (!marked) return;
            foundingWaitUntil = -1;
            var d = Get("founding");
            if (d != null) KeepDay(d, foundingWaitYear, map, null, false, true);
        }

        // ---------------- Ideology ----------------

        private static string IdeoName()
        {
            try
            {
                return Faction.OfPlayer?.ideos?.PrimaryIdeo?.name ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// With Ideology, the colony's own rite for a day like this, if its faith has one: a funeral rite for a remembrance,
        /// a feast, party, dance or festival the colony can hold any time for a feast or games. Read only, never started
        /// by Rimfolk: the letter suggests it, and the player holds it from the ritual's spot.
        /// </summary>
        public static Precept_Ritual RitualFor(DayWay way)
        {
            if (!ModsConfig.IdeologyActive || way == DayWay.Personal || way == DayWay.Rest) return null;
            try
            {
                var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (ideo == null) return null;
                foreach (var precept in ideo.PreceptsListForReading)
                {
                    if (!(precept is Precept_Ritual r)) continue;
                    string names = ((r.def?.defName ?? "") + " " + (r.sourcePattern?.defName ?? "") + " " + (r.patternGroupTag ?? "")).ToLowerInvariant();
                    if (way == DayWay.Remembrance)
                    {
                        if (names.Contains("funeral") || names.Contains("memorial")) return r;
                    }
                    else if ((r.isAnytime || r.canBeAnytime) && FeastWords.Any(names.Contains)) return r;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar ritual", e);
            }
            return null;
        }

        // ---------------- The World page ----------------

        /// <summary>The next few days on the calendar (within five days), for the World page's list of what's coming.</summary>
        public static List<WorldNote> Coming(int now)
        {
            var list = new List<WorldNote>();
            var self = Instance;
            var map = Home;
            if (self == null || map == null || !On) return list;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            int hour = GenLocalDate.HourOfDay(map);
            foreach (var d in self.days.Where(x => x.kind != CalendarKind.FirstSnow).OrderBy(x => CalendarMath.DaysUntil(today, x.day)))
            {
                int until = CalendarMath.DaysUntil(today, d.day);
                if (until == 0 || until > 5) continue;
                int occ = today + until >= CalendarMath.DaysPerYear ? year + 1 : year;
                if (d.lastYear >= occ || (!d.byPlayer && occ <= d.sinceYear)) continue;
                list.Add(new WorldNote
                {
                    title = d.label.CapitalizeFirst(),
                    text = "CC_Cal_World_Coming".Translate(WayLabel(d.way), DateText(d.day, map)).Resolve(),
                    tip = d.tradition ? "CC_Cal_Status_Tradition".Translate(d.timesKept).Resolve() : null,
                    icon = CalendarPanel.Icon,
                    color = UiKit.Gold,
                    due = now + until * GenDate.TicksPerDay + (9 - hour) * GenDate.TicksPerHour,
                    click = () => MainTabWindow_Council.OpenTab("Calendar")
                });
                if (list.Count >= 3) break;
            }
            return list;
        }
    }

    /// <summary>A day on the colony calendar has come: keep it, keep it with the faith's own rite, or let it pass this year.</summary>
    public class ChoiceLetter_CalendarDay : ChoiceLetter
    {
        public const int KeepChoice = 0;
        public const int RitualChoice = 1;
        public const int PassChoice = 2;

        public string dayId;
        public int occurrence;
        public bool resolved;
        /// <summary>The faith's rite that would suit the day (Ideology), by name; null when there's none.</summary>
        public string ritual;
        public string keepLabel;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || resolved)
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return Option(keepLabel.NullOrEmpty() ? "CC_Cal_Keep_Feast".Translate().ToString() : keepLabel, KeepChoice);
                if (!ritual.NullOrEmpty()) yield return Option("CC_Cal_KeepRitual".Translate(ritual).Resolve(), RitualChoice);
                yield return Option("CC_Cal_Pass".Translate(), PassChoice);
                yield return Option_Postpone;
            }
        }

        private DiaOption Option(string label, int choice)
        {
            var opt = new DiaOption(label)
            {
                action = () =>
                {
                    CalendarTracker.Instance?.Resolve(this, choice, false);
                    Find.LetterStack.RemoveLetter(this);
                },
                resolveTree = true
            };
            return opt;
        }

        /// <summary>Unanswered when the time runs out: the day is kept.</summary>
        public override void Removed()
        {
            base.Removed();
            if (!resolved) CalendarTracker.Instance?.Resolve(this, KeepChoice, true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dayId, "dayId");
            Scribe_Values.Look(ref occurrence, "occurrence");
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref ritual, "ritual");
            Scribe_Values.Look(ref keepLabel, "keepLabel");
        }
    }
}
