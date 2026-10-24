using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The setbacks that follow the newer systems (Defs/ThoughtDefs/Council_Thoughts_Setbacks3.xml).</summary>
    [DefOf]
    public static class SetbackTimesDefOf
    {
        // The calendar
        public static ThoughtDef Council_Setback_DayRainedOut;
        public static ThoughtDef Council_Setback_BurnedDish;
        public static ThoughtDef Council_Setback_TableQuarrel;
        public static ThoughtDef Council_Setback_EmptyChair;

        // Camps and guests
        public static ThoughtDef Council_Setback_CampSoured;
        public static ThoughtDef Council_Setback_CampOwed;
        public static ThoughtDef Council_Setback_GuestsEat;

        // Missions and the world
        public static ThoughtDef Council_Setback_MissionLetDown;
        public static ThoughtDef Council_Setback_WorldTurn;
        public static ThoughtDef Council_Setback_StoodBy;

        static SetbackTimesDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SetbackTimesDefOf));
    }

    /// <summary>
    /// Bad days that follow the newer systems, each from something that really happened: a calendar day's feast rained
    /// on, its dish burned by a cook who isn't much of one, two rivals quarrelling at its table, or the first time the
    /// day is kept without someone who died this year; a camp that left on bad terms with the friends made at its fire,
    /// or left owing; guests who stay on eating from stores that are running low; a mission let slip; a world event's
    /// gamble gone wrong, or one the colony stood by through. Each says what caused it, and its mood hint what helps
    /// (Defs/MoodHintDefs/Council_MoodHints_Setbacks_Times.xml). Hooked where the systems already say the thing is
    /// over (Social/Talk/SharedTimes.cs, the gathering's end, the setbacks' own hour).
    /// </summary>
    public static partial class Setbacks
    {
        private static bool Psychopath(Pawn p) => p?.story?.traits?.HasTrait(TraitDefOf.Psychopath) ?? false;

        private static bool Kind(Pawn p) => p?.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false;

        private static bool Grown(Pawn p) => p != null && !p.DevelopmentalStage.Juvenile();

        // ---------------- The calendar's gatherings ----------------

        /// <summary>Gatherings for a calendar day that were rained on (their play-out ids), for the joke afterwards. Not saved.</summary>
        private static readonly HashSet<int> rainedDays = new HashSet<int>();
        private static Game rainedGame;

        /// <summary>The setbacks' hour found rain on this calendar day's gathering (SetbackTracker.Gatherings).</summary>
        internal static void Notify_DayRained(int playId)
        {
            if (rainedGame != Current.Game)
            {
                rainedDays.Clear();
                rainedGame = Current.Game;
            }
            if (rainedDays.Count > 40) rainedDays.Clear();
            rainedDays.Add(playId);
        }

        /// <summary>
        /// A calendar day's gathering is over (from <see cref="Notify_GatheringOver"/>): the feast in the rain becomes a
        /// joke later; a feast's dish may burn, by how good a cook the colony has; two rivals at the same table may quarrel.
        /// </summary>
        private static void DayGatheringOver(PlayOut play, List<Pawn> attended, bool feast, Map map)
        {
            var day = SharedTimes.DayOf(play);
            if (day == null || attended.NullOrEmpty() || map == null) return;
            string dayName = SharedTimes.The(day.label);
            if (rainedGame == Current.Game && rainedDays.Remove(play.id) && attended.Count >= 2)
                SharedTimes.Mishap(TimesDefs.FeastRain, "CC_Times_Label_Rain".Translate(dayName).Resolve(), attended);
            if (feast && attended.Count >= 3) BurnedDish(attended, map, dayName);
            Quarrel(attended, dayName);
        }

        /// <summary>The colony's cook for a feast: whoever cooks best among those at home who cook at all.</summary>
        private static Pawn Cook(Map map)
        {
            Pawn best = null;
            int level = -1;
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p.IsQuestLodger() || (cooking != null && p.WorkTypeIsDisabled(cooking)) || !Eligible(p)) continue;
                var sk = p.skills?.GetSkill(SkillDefOf.Cooking);
                if (sk == null || sk.TotallyDisabled) continue;
                if (sk.Level > level)
                {
                    level = sk.Level;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>The feast's dish came off the fire black: everyone at the table minds a little, the cook more; it's talked of, and laughed at later.</summary>
        private static void BurnedDish(List<Pawn> attended, Map map, string dayName)
        {
            var cook = Cook(map);
            int level = cook?.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0;
            if (cook == null || !Rand.Chance(SetbackMath.BurnedChance(level)) || !On(SetbackTimesDefOf.Council_Setback_BurnedDish)) return;
            var dishes = "CC_Times_Dishes".Translate().ToString().Split('|').Where(s => !s.NullOrEmpty()).ToList();
            string dish = dishes.Count > 0 ? dishes.RandomElement() : "?";
            string cause = "CC_Setback_Cause_BurnedDish".Translate(dish, dayName, cook.LabelShort).Resolve();
            foreach (var p in attended)
            {
                if (p == null || p == cook || !Eligible(p) || HasTrait(p, "Ascetic")) continue;
                Give(p, SetbackTimesDefOf.Council_Setback_BurnedDish, null, cause, 0);
            }
            var sk = cook.skills?.GetSkill(SkillDefOf.Cooking);
            if (attended.Contains(cook) || cook.Spawned)
            {
                if (Give(cook, SetbackTimesDefOf.Council_Setback_BurnedDish, null, cause, sk != null && People.Passionate(sk) ? 2 : 1))
                {
                    Say(cook, "Rimfolk_Pool_Setback_Burned", dish.Named("THING"));
                    Write(cook, "Rimfolk_Journal_Setback_Burned", 0.6f, dish.Named("THING"), dayName.Named("GATHERING"));
                }
            }
            Deeds.Record("Rimfolk_Deed_SpoiledFeast", cook, null, -1f, null, dish, attended, false);
            News.Post("CC_Times_News_Burned".Translate(cook.Named("COOK"), dish.Named("DISH"), dayName.Named("DAY")).Resolve(),
                new LookTargets(cook), NewsKind.Colony, MessageTypeDefOf.NegativeEvent, Importance.Flavor);
            SharedTimes.Mishap(TimesDefs.BurnedDish, "CC_Times_Label_Burned".Translate(dish, dayName).Resolve(), attended);
        }

        /// <summary>
        /// Two rivals at the same day's table, one of them quick to anger or both set against each other: a quarrel, now and
        /// then (a third of the time). Both mind it and think less of each other; the one who started it is talked of.
        /// </summary>
        private static void Quarrel(List<Pawn> attended, string dayName)
        {
            if (!On(SetbackTimesDefOf.Council_Setback_TableQuarrel) || attended.Count < 2) return;
            // together: both were at the day's gathering, in its area, long enough to count as there (the gathering's own measure).
            var pairs = new List<(Pawn a, Pawn b)>();
            for (int i = 0; i < attended.Count; i++)
                for (int j = i + 1; j < attended.Count; j++)
                {
                    var a = attended[i];
                    var b = attended[j];
                    if (a == null || b == null || !Eligible(a) || !Eligible(b) || !Grown(a) || !Grown(b)) continue;
                    if (Rival(a, b) || Rival(b, a)) pairs.Add((a, b));
                }
            if (pairs.Count == 0 || !Rand.Chance(SetbackMath.QuarrelChance)) return;
            var (x, y) = pairs.RandomElement();
            // The one quicker to anger starts it.
            var tx = Persona.Values(x);
            var ty = Persona.Values(y);
            var starter = tx[(int)PersonaAxis.Temper] >= ty[(int)PersonaAxis.Temper] ? x : y;
            var other = starter == x ? y : x;
            if (!(Tracker?.Ready(starter, "tablequarrel", 6f) ?? false)) return;
            var reasons = "CC_Times_QuarrelOver".Translate().ToString().Split('|').Where(s => !s.NullOrEmpty()).ToList();
            string over = reasons.Count > 0 ? reasons.RandomElement() : "?";
            bool gave = Give(starter, SetbackTimesDefOf.Council_Setback_TableQuarrel, other, "CC_Setback_Cause_Quarrel".Translate(other.LabelShort, dayName, over).Resolve());
            gave |= Give(other, SetbackTimesDefOf.Council_Setback_TableQuarrel, starter, "CC_Setback_Cause_Quarrel".Translate(starter.LabelShort, dayName, over).Resolve());
            if (!gave) return;
            Deeds.Record("Rimfolk_Deed_TableQuarrel", starter, other, -1f, null, null, attended, false);
            News.Post("CC_Times_News_Quarrel".Translate(starter.Named("A"), other.Named("B"), dayName.Named("DAY"), over.Named("OVER")).Resolve(),
                new LookTargets(new[] { starter, other }.Where(p => p.Spawned)), NewsKind.Colony, MessageTypeDefOf.NegativeEvent, Importance.Flavor);
        }

        /// <summary>
        /// A day kept (SharedTimes.DayKept): the first time it comes round since someone the colony loved died this year,
        /// those who knew them notice the empty place (their family and partner more). Once a year for each of the dead.
        /// </summary>
        internal static void Notify_DayKept(CalendarDay d, Map map, List<Pawn> home)
        {
            try
            {
                if (d == null || home.NullOrEmpty() || !On(SetbackTimesDefOf.Council_Setback_EmptyChair)) return;
                if (d.way != DayWay.Feast && d.way != DayWay.Games && d.way != DayWay.Bonfire && d.way != DayWay.Gifts && d.way != DayWay.Faith) return;
                var days = CalendarTracker.Instance?.days;
                if (days == null) return;
                int now = Find.TickManager.TicksGame;
                string dayName = SharedTimes.The(d.label);
                foreach (var dead in days)
                {
                    if (dead == null || dead == d || dead.kind != CalendarKind.Death || dead.tick <= 0 || dead.pawnName.NullOrEmpty()) continue;
                    int since = now - dead.tick;
                    if (since < GenDate.TicksPerDay * 2 || since > GenDate.TicksPerYear) continue;
                    foreach (var p in home)
                    {
                        if (p == null || !Eligible(p) || Psychopath(p)) continue;
                        bool kin = dead.kin.Contains(p.ThingID);
                        if (!kin && !dead.knew.Contains(p.ThingID)) continue;
                        if (!(Tracker?.Ready(p, "emptychair|" + dead.id, 60f) ?? false)) continue;
                        if (Give(p, SetbackTimesDefOf.Council_Setback_EmptyChair, null, "CC_Setback_Cause_EmptyChair".Translate(dayName, dead.pawnName).Resolve(), kin ? 1 : 0))
                            Write(p, "Rimfolk_Journal_Setback_EmptyChair", kin ? 0.8f : 0.4f, dead.pawnName.Named("NAME"), dayName.Named("GATHERING"));
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: day kept", e);
            }
        }

        // ---------------- Camps ----------------

        /// <summary>
        /// A camp has gone (SharedTimes.CampEnded): if it ended in a falling-out or the colony sent them packing, those who
        /// had spent evenings at their fire mind it; if they left owing (a fee unpaid, a share of the hunt not given), the
        /// colony's grown-ups mind that, the greedy most and the kind not at all.
        /// </summary>
        internal static void Notify_CampEnded(Camp c, List<Pawn> warm)
        {
            try
            {
                if (c?.kind == null || c.map == null) return;
                string name = c.kind.label;
                if ((c.ending == "fallout" || c.ending == "forced") && !warm.NullOrEmpty())
                {
                    string cause = (c.ending == "forced" ? "CC_Setback_Cause_CampForced" : "CC_Setback_Cause_CampSoured").Translate(name).Resolve();
                    foreach (var p in warm)
                    {
                        if (!Eligible(p) || Psychopath(p) || !(Tracker?.Ready(p, "campsoured", 5f) ?? false)) continue;
                        if (Give(p, SetbackTimesDefOf.Council_Setback_CampSoured, null, cause))
                            Write(p, "Rimfolk_Journal_Setback_CampSoured", 0.5f, name.Named("GROUP"));
                    }
                }
                bool owedFee = c.termFee && c.feeLate;
                bool owedShare = c.termShare && c.skipShare;
                bool parted = c.ending != "harmed" && c.ending != "arrest" && c.ending != "fight" && c.ending != "empty";
                if ((owedFee || owedShare) && parted)
                {
                    string cause = (owedFee ? "CC_Setback_Cause_CampOwedFee" : "CC_Setback_Cause_CampOwedShare").Translate(name).Resolve();
                    foreach (var p in IncidentWorker_Happening.Colonists(c.map))
                    {
                        if (!Eligible(p) || !Grown(p) || Kind(p) || !(Tracker?.Ready(p, "campowed", 4f) ?? false)) continue;
                        Give(p, SetbackTimesDefOf.Council_Setback_CampOwed, null, cause, HasTrait(p, "Greedy") ? 1 : 0);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: camp ended", e);
            }
        }

        // ---------------- Guests at the table ----------------

        /// <summary>When each guest staying with the colony was first seen at home (thingIDNumber: tick). Not saved: a load starts the count again.</summary>
        private static readonly Dictionary<int, int> guestsSince = new Dictionary<int, int>();
        private static Game guestsGame;

        /// <summary>
        /// The setbacks' hour, at home: guests (the game's quest lodgers) who have stayed two days or more, eating from stores
        /// that are running low (under four days of food, under two the worse). The colony's grown-ups mind it, now and then;
        /// the kind and the ascetic don't. It goes when the stores fill up again, or the guests go.
        /// </summary>
        internal static void GuestsAtTable(Map map, int now)
        {
            if (map == null || !On(SetbackTimesDefOf.Council_Setback_GuestsEat)) return;
            if (guestsGame != Current.Game)
            {
                guestsSince.Clear();
                guestsGame = Current.Game;
            }
            int staying = 0;
            int longest = 0;
            var present = new HashSet<int>();
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || !p.IsQuestLodger() || p.RaceProps?.Humanlike != true || p.needs?.food == null) continue;
                present.Add(p.thingIDNumber);
                if (!guestsSince.TryGetValue(p.thingIDNumber, out int since) || since > now) guestsSince[p.thingIDNumber] = since = now;
                if (now - since < GenDate.TicksPerDay * 2) continue;
                staying++;
                longest = Math.Max(longest, now - since);
            }
            foreach (var id in guestsSince.Keys.Where(k => !present.Contains(k)).ToList()) guestsSince.Remove(id);
            if (staying == 0) return;
            float food = MapFacts.Of(map).FoodDays;
            int stage = SetbackMath.GuestsStage(food);
            if (stage < 0) return;
            string cause = "CC_Setback_Cause_Guests".Translate(staying, Days(longest), food.ToString("0.#")).Resolve();
            foreach (var p in IncidentWorker_Happening.Colonists(map))
            {
                if (!Eligible(p) || !Grown(p) || Kind(p) || HasTrait(p, "Ascetic")) continue;
                if (!(Tracker?.Ready(p, "guests", 2f) ?? false)) continue;
                Give(p, SetbackTimesDefOf.Council_Setback_GuestsEat, null, cause, stage);
            }
        }

        // ---------------- Missions ----------------

        /// <summary>
        /// A mission ended (SharedTimes.MissionEnded): let slip, the one who dealt with the neighbours and those who went
        /// mind it; only half done (a harvest left in the fields), those who went mind a little.
        /// </summary>
        internal static void Notify_MissionEnded(Mission m, MissionState state, string outcome, List<Pawn> went)
        {
            try
            {
                if (m == null || !On(SetbackTimesDefOf.Council_Setback_MissionLetDown)) return;
                string who = m.faction?.Name ?? "?";
                if (state == MissionState.Failed && outcome != null && SharedTimes.LetDown.Contains(outcome))
                {
                    string cause = "CC_Setback_Cause_LetDown".Translate(who, m.title ?? "?").Resolve();
                    var people = new List<Pawn>();
                    if (m.a != null && !m.a.Dead && m.a.IsColonist) people.Add(m.a);
                    if (went != null) people.AddRange(went);
                    foreach (var p in people.Distinct())
                        if (Eligible(p) && Grown(p) && Give(p, SetbackTimesDefOf.Council_Setback_MissionLetDown, null, cause, 1))
                            Write(p, "Rimfolk_Journal_Setback_LetDown", 0.5f, who.Named("FACTION"));
                    return;
                }
                if (state == MissionState.Done && outcome == "partial" && went != null)
                {
                    string cause = "CC_Setback_Cause_HalfDone".Translate(who, m.place ?? "?").Resolve();
                    foreach (var p in went)
                        if (Eligible(p) && Grown(p)) Give(p, SetbackTimesDefOf.Council_Setback_MissionLetDown, null, cause, 0);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: mission ended", e);
            }
        }

        // ---------------- World events ----------------

        /// <summary>
        /// A world event's answer whose gamble went wrong (from <see cref="Notify_Answer"/>): the grown-ups at home who
        /// weren't already upset by it hear the bad news and mind it a little.
        /// </summary>
        private static void WorldTurn(DilemmaContext ctx, string question)
        {
            if (!On(SetbackTimesDefOf.Council_Setback_WorldTurn)) return;
            var map = ctx.map ?? HappeningTracker.HomeMap();
            if (map == null) return;
            string cause = "CC_Setback_Cause_WorldTurn".Translate(question ?? "?").Resolve();
            foreach (var p in IncidentWorker_Happening.Colonists(map))
            {
                if (!Eligible(p) || !Grown(p) || p == ctx.a || UpsetJustNow(p) || !(Tracker?.Ready(p, "worldturn", 2f) ?? false)) continue;
                Give(p, SetbackTimesDefOf.Council_Setback_WorldTurn, null, cause);
            }
        }

        /// <summary>
        /// A world event ended (SharedTimes.SituationEnded) with nothing from the colony, though it could have helped (the
        /// event has endings where the colony's part counts): the kind at home mind that it stood by.
        /// </summary>
        internal static void Notify_SituationEnded(WorldSituation s, SituationEnding ending, List<Pawn> home)
        {
            try
            {
                if (s?.def == null || ending == null || home.NullOrEmpty() || !On(SetbackTimesDefOf.Council_Setback_StoodBy)) return;
                if (s.help > 0 || ending.helped || !s.def.endings.Any(e => e != null && e.helped)) return;
                string cause = "CC_Setback_Cause_StoodBy".Translate(SharedTimes.WorldName(s.def), s.ctx?.faction?.Name ?? "CC_Times_TheRegion".Translate().ToString()).Resolve();
                foreach (var p in home)
                {
                    if (!Eligible(p) || !Grown(p) || !Kind(p)) continue;
                    if (Give(p, SetbackTimesDefOf.Council_Setback_StoodBy, null, cause))
                        Write(p, "Rimfolk_Journal_Setback_StoodBy", 0.6f, SharedTimes.WorldName(s.def).Named("SITUATION"));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: world event ended", e);
            }
        }
    }
}
