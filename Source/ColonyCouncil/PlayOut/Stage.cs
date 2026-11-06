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
    /// One colony's count of what it gathered for (PlayOut/Stage.cs): when each gathering or outing began in the last ten
    /// days, the last of any kind (the game's own parties, rituals and weddings too), the season its last seasonal custom
    /// was asked, and the toasts of life goals waiting for the next gathering.
    /// </summary>
    public class StageColony : IExposable
    {
        public int colony = HomesMath.None;
        /// <summary>When each gathering and outing began, the last <see cref="StageMath.KeepDays"/> days.</summary>
        public List<int> held = new List<int>();
        /// <summary>The last gathering of any kind; when the colony was first seen, until it has one.</summary>
        public int last = -1;
        /// <summary>The season (<see cref="StageMath.SeasonKey"/>) of the last seasonal custom asked; -1 never.</summary>
        public int customSeason = -1;
        /// <summary>The colony's own evening the floor planned, while it waits or runs; -1 none.</summary>
        public int floorPlay = -1;
        public List<StageCup> cups = new List<StageCup>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref colony, "colony", HomesMath.None);
            Scribe_Collections.Look(ref held, "held", LookMode.Value);
            Scribe_Values.Look(ref last, "last", -1);
            Scribe_Values.Look(ref customSeason, "customSeason", -1);
            Scribe_Values.Look(ref floorPlay, "floorPlay", -1);
            Scribe_Collections.Look(ref cups, "cups", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                held ??= new List<int>();
                cups ??= new List<StageCup>();
                cups.RemoveAll(c => c == null || c.honoured == null || c.honoured.Dead);
            }
        }
    }

    /// <summary>A life goal's toast waiting to be raised at the colony's next gathering, or at supper.</summary>
    public class StageCup : IExposable
    {
        public Pawn honoured;
        /// <summary>The one who thinks best of them, who raises it.</summary>
        public Pawn raiser;
        /// <summary>What they did, as the toast says it ("found something worth doing").</summary>
        public string deed;
        /// <summary>Why the moods come (the hover text under them).</summary>
        public string cause;
        public int promised;
        /// <summary>The gathering it is to be raised at, while that waits or runs; -1 none.</summary>
        public int playId = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref honoured, "honoured");
            Scribe_References.Look(ref raiser, "raiser");
            Scribe_Values.Look(ref deed, "deed");
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref playId, "playId", -1);
        }
    }

    /// <summary>
    /// The colony's pace (the player's decision 1 of 6 November 2026; the rules are StageMath.cs). Every gathering on a
    /// home is counted, one count a colony, whoever called it: the game's parties, rituals and weddings, Rimfolk's
    /// play-outs, meetings and the election's gatherings (each a lord, seen as it starts), and its outings (work parties,
    /// walks, vigils: counted where they start). What the player chose always happens but counts; the colony's own
    /// doings (<see cref="PlayOut.routine"/>) start only when there is room, wait up to three days for it and are let go
    /// quietly. A floor: from the seventh day without a gathering the colony's own come at once, and from the eighth, with
    /// nothing on the way, the colony keeps an evening together itself. A life goal's toast waits for the next gathering
    /// (<see cref="Cup"/>). The count is saved with the play-outs (PlayOutTracker), so a load keeps the week.
    /// </summary>
    public static class Stage
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The week's allowance for the colony's own doings (the "Gatherings a week" setting).</summary>
        public static int AWeek => StageMath.AWeek(S?.gatheringsAWeek ?? StageMath.DefaultAWeek);

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The lords seen on each home last time, so each gathering counts once (not saved: after a load, those already on were counted before).</summary>
        private static readonly Dictionary<int, HashSet<int>> seen = new Dictionary<int, HashSet<int>>();
        private static Game seenGame;
        private static int lastHour = -1;

        private static StageColony Of(Map map, bool make = true)
        {
            var t = PlayOutTracker.Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return null;
            int key = Homes.KeyOf(map);
            var c = t.stage.FirstOrDefault(x => x.colony == key);
            if (c == null && make)
            {
                c = new StageColony { colony = key, last = Now };
                t.stage.Add(c);
            }
            return c;
        }

        // ---------------- Counted ----------------

        /// <summary>A gathering or an outing began on a home now (an outing, said where it starts; a gathering's lord is seen by itself).</summary>
        public static void Held(Map map)
        {
            var c = Of(map);
            if (c == null) return;
            int now = Now;
            c.held.Add(now);
            c.last = now;
            StageMath.Prune(c.held, now);
        }

        /// <summary>Room now for one of the colony's own doings on this home.</summary>
        public static bool Room(Map map, int now)
        {
            var c = Of(map);
            return c == null || StageMath.Room(c.held, now, AWeek, SinceLast(c, now));
        }

        /// <summary>Ticks since the home's last gathering of any kind (<see cref="int.MaxValue"/>: none known).</summary>
        public static int Since(Map map, int now)
        {
            var c = Of(map, false);
            return c == null ? int.MaxValue : SinceLast(c, now);
        }

        private static int SinceLast(StageColony c, int now) => c.last < 0 || c.last > now ? int.MaxValue : now - c.last;

        /// <summary>A calendar day's gathering on this home would come within two days of another: kept at supper instead.</summary>
        public static bool AtSupper(Map map, int now) => StageMath.AtSupper(Since(map, now));

        // ---------------- The customs ----------------

        /// <summary>A seasonal custom may be asked at this home this season (one a season).</summary>
        public static bool CustomMayCome(Map map)
        {
            var c = Of(map, false);
            return c == null || StageMath.CustomMayCome(c.customSeason, Season(map));
        }

        /// <summary>A seasonal custom was asked at this home.</summary>
        public static void CustomAsked(Map map)
        {
            var c = Of(map);
            if (c != null) c.customSeason = Season(map);
        }

        private static int Season(Map map) => StageMath.SeasonKey(GenLocalDate.Year(map), GenLocalDate.DayOfYear(map));

        // ---------------- A life goal's toast ----------------

        /// <summary>
        /// A life goal reached: the one who thinks best of them raises a cup to them at the colony's next gathering (or at
        /// supper, if none comes within two days). True when it waits to be raised.
        /// </summary>
        public static bool Cup(Pawn honoured, Pawn raiser, string deed, string cause)
        {
            if (honoured == null || raiser == null || !PlayOuts.On) return false;
            var c = Of(honoured.MapHeld);
            if (c == null) return false;
            c.cups.Add(new StageCup { honoured = honoured, raiser = raiser, deed = deed, cause = cause, promised = Now });
            return true;
        }

        /// <summary>A gathering of the colony's began: the toasts waiting are to be raised at it, those they're for asked to it too.</summary>
        internal static void Started(PlayOut p)
        {
            if (p == null || !Festive(p)) return;
            var c = Of(p.map, false);
            if (c == null || c.cups.Count == 0) return;
            foreach (var cup in c.cups.Where(x => x.playId < 0).ToList())
            {
                if (!Here(cup.honoured, p.map)) continue;
                cup.playId = p.id;
                var raiser = Here(cup.raiser, p.map) ? cup.raiser : null;
                if (!p.invited.Contains(cup.honoured)) p.invited.Add(cup.honoured);
                if (raiser != null && !p.invited.Contains(raiser)) p.invited.Add(raiser);
                var honoured = GoalRewards.Honoured;
                var glass = GoalRewards.Glass;
                if (honoured != null) p.moods.Add(new HeldMood { pawn = cup.honoured, thought = honoured, cause = cup.cause });
                if (raiser != null && glass != null) p.moods.Add(new HeldMood { pawn = raiser, thought = glass, about = cup.honoured, cause = cup.cause });
            }
        }

        /// <summary>What the begin message adds for the toasts to be raised there ("Harper has a cup to raise there for Barr.").</summary>
        internal static string CupsAt(PlayOut p)
        {
            var c = p == null ? null : Of(p.map, false);
            if (c == null) return null;
            var cups = c.cups.Where(x => x.playId == p.id).ToList();
            if (cups.Count == 0) return null;
            return string.Join(" ", cups.Select(x => Here(x.raiser, p.map)
                ? "CC_Stage_CupThere".Translate(x.raiser.Named("RAISER"), x.honoured.Named("A")).Resolve()
                : "CC_Stage_CupThereAll".Translate(x.honoured.Named("A")).Resolve()));
        }

        /// <summary>
        /// At the gathering, a while in, the cups go up for each toast it carries, with the toast's words from whoever
        /// raises it (GatheringTouches). Once per gathering.
        /// </summary>
        internal static void RaiseCups(PlayOut p, List<Pawn> here)
        {
            var c = p == null ? null : Of(p.map, false);
            if (c == null || here == null || here.Count < 2) return;
            foreach (var cup in c.cups.Where(x => x.playId == p.id))
            {
                // together: said at the gathering itself, to the one it's for, standing in its area with the others.
                var by = here.Contains(cup.raiser) ? cup.raiser : here.FirstOrDefault(x => x != cup.honoured);
                if (by == null || !here.Contains(cup.honoured)) continue;
                VisualTouches.RaiseCups(here);
                Speech.Say(by, ToastLine(cup), cup.honoured, ToastColor);
            }
        }

        private static readonly Color ToastColor = new Color(1f, 0.93f, 0.72f);

        /// <summary>The toast's words: what they did, more often than not; else that they said they would and did, or simply to them.</summary>
        private static string ToastLine(StageCup cup)
        {
            if (!cup.deed.NullOrEmpty() && Rand.Chance(0.6f)) return "CC_Asp_Toast_1".Translate(cup.honoured.Named("A"), cup.deed.Named("DEED")).Resolve();
            return (Rand.Bool ? "CC_Asp_Toast_2" : "CC_Asp_Toast_3").Translate(cup.honoured.Named("A")).Resolve();
        }

        /// <summary>The gathering is over (it happened, or it fell through): its toasts are raised, or wait again.</summary>
        internal static void PlayOver(PlayOut p, bool happened)
        {
            var c = p == null ? null : Of(p.map, false);
            if (c == null) return;
            if (p.id == c.floorPlay) c.floorPlay = -1;
            foreach (var cup in c.cups.Where(x => x.playId == p.id).ToList())
            {
                if (happened) c.cups.Remove(cup);
                else cup.playId = -1;
            }
        }

        /// <summary>A gathering where a cup goes up: a feast, a party, a fire, a toast; not words, not a grave.</summary>
        private static bool Festive(PlayOut p) =>
            p.kind == PlayKind.Feast || p.kind == PlayKind.Gathering || p.kind == PlayKind.Bonfire || p.kind == PlayKind.Toast && !p.words && !(p.place is Building_Grave);

        private static bool Here(Pawn p, Map map) => p != null && !p.Dead && p.Spawned && p.Map == map && PlayOuts.OurPerson(p);

        // ---------------- The clock ----------------

        /// <summary>
        /// Every few seconds (PlayOutTracker's pass): each home's gatherings seen as they start; once an hour, the toasts
        /// raised at supper or let go, and the floor.
        /// </summary>
        internal static void Tick(PlayOutTracker t, int now)
        {
            if (Current.Game != seenGame)
            {
                seenGame = Current.Game;
                seen.Clear();
                lastHour = -1;
            }
            foreach (var map in Homes.All)
            {
                try
                {
                    Watch(map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pace: gatherings seen", e);
                }
            }
            int hour = now / Sched.HourTicks;
            if (hour == lastHour) return;
            lastHour = hour;
            t.stage.RemoveAll(c => c == null || Homes.MapOf(c.colony) == null);
            foreach (var c in t.stage.ToList())
            {
                try
                {
                    var map = Homes.MapOf(c.colony);
                    StageMath.Prune(c.held, now);
                    Suppers(c, map, now);
                    Floor(t, c, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pace: the hour", e);
                }
            }
        }

        /// <summary>The home's gathering lords: one that wasn't there last time began now, and is counted.</summary>
        private static void Watch(Map map, int now)
        {
            var lords = map?.lordManager?.lords;
            if (lords == null) return;
            int key = map.uniqueID;
            bool primed = seen.TryGetValue(key, out var before);
            var current = new HashSet<int>();
            foreach (var lord in lords)
            {
                var job = lord?.LordJob;
                if (!(job is LordJob_Joinable_Gathering || job is LordJob_Joinable_MarriageCeremony || job is LordJob_FeastGuests)) continue;
                current.Add(lord.loadID);
                if (primed && !before.Contains(lord.loadID)) Held(map);
            }
            seen[key] = current;
        }

        /// <summary>
        /// A toast nobody gathered for within two days is raised at supper, when the two are together in the evening; past
        /// the colony's own wait it is let go (its moods never given: it never happened).
        /// </summary>
        private static void Suppers(StageColony c, Map map, int now)
        {
            if (c.cups.Count == 0 || map == null) return;
            c.cups.RemoveAll(x => x.honoured == null || x.honoured.Dead || x.playId < 0 && StageMath.CupLetGo(x.promised, now));
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 17 || hour > 21) return;
            foreach (var cup in c.cups.Where(x => x.playId < 0 && StageMath.CupAtSupper(x.promised, now)).ToList())
            {
                var raiser = cup.raiser != null && !cup.raiser.Dead ? cup.raiser : null;
                if (raiser == null || !Here(cup.honoured, map) || !Here(raiser, map)) continue;
                var it = cup;
                Together.When("cup|" + cup.honoured.thingIDNumber + "|" + cup.promised, raiser, cup.honoured, () => AtSupper(c, it), 4f, -1f,
                    () => GenLocalDate.HourOfDay(map) >= 17 && GenLocalDate.HourOfDay(map) <= 22);
            }
        }

        /// <summary>The cup raised at supper: the words, the cups, the moods, and a line in the evening's news.</summary>
        private static void AtSupper(StageColony c, StageCup cup)
        {
            if (!c.cups.Contains(cup) || cup.playId >= 0) return;
            c.cups.Remove(cup);
            // together: the two are together at supper; the cup is raised to the one it's for.
            VisualTouches.RaiseCups(new List<Pawn> { cup.raiser, cup.honoured });
            Speech.Say(cup.raiser, ToastLine(cup), cup.honoured, ToastColor);
            if (GoalRewards.Honoured != null) CouncilGameComponent.GiveThought(cup.honoured, GoalRewards.Honoured, null, -1f, cup.cause);
            if (GoalRewards.Glass != null) CouncilGameComponent.GiveThought(cup.raiser, GoalRewards.Glass, cup.honoured, -1f, cup.cause);
            Tell.Minor(NewsKind.People, Importance.Notable, cup.honoured.MapHeld,
                "CC_Stage_CupAtSupper".Translate(cup.raiser.Named("RAISER"), cup.honoured.Named("A")).Resolve(), new LookTargets(cup.honoured), MessageTypeDefOf.PositiveEvent);
        }

        // ---------------- The floor ----------------

        /// <summary>
        /// Eight days without a gathering and nothing of the colony's own on the way: the colony keeps an evening together
        /// itself, planned in the afternoon for that evening (a gathering at its fire, the rec room or the table), so ten
        /// days never pass without one. With answers not playing out on the map, the game's own party.
        /// </summary>
        private static void Floor(PlayOutTracker t, StageColony c, Map map, int now)
        {
            if (map == null || !StageMath.FloorEvening(SinceLast(c, now))) return;
            if (c.floorPlay >= 0 && t.Get(c.floorPlay) is PlayOut was && was.state != PlayState.Done) return;
            c.floorPlay = -1;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 14 || hour > 19) return;
            if (t.plays.Any(p => p.map == map && p.state != PlayState.Done && (p.IsGathering || p.IsGroup))) return;
            if (PlayOuts.WhyNotNow(map, true) != null) return;
            var home = map.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).ToList();
            if (home.Count < 3) return;
            if (!PlayOuts.On)
            {
                var party = GatheringDefOf.Party;
                if (party?.Worker != null && party.Worker.CanExecute(map) && party.Worker.TryExecute(map)) c.last = now;
                return;
            }
            var lead = home.OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First();
            var p = new PlayOut
            {
                kind = PlaySpots.ColonyFire(map, lead) != null ? PlayKind.Bonfire : PlayKind.Gathering,
                map = map,
                created = now,
                label = "CC_Stage_EveningLabel".Translate().Resolve(),
                invited = home,
                organizer = lead,
                spots = new List<PlaySpot> { PlaySpot.Fire, PlaySpot.Rec, PlaySpot.Dining, PlaySpot.Gather, PlaySpot.Outdoors },
                durationTicks = PlayOutMath.DurationTicks(2f),
                pool = "Council_Pool_PlayParty",
                hourFrom = 18,
                hourTo = 23,
                music = true,
                // The colony's own: let go without a word if no evening comes for it (the floor keeps room open for it).
                routine = true
            };
            p.deadline = PlayOutMath.Deadline(now, hour, p.hourFrom, p.hourTo, 3f);
            t.Add(p);
            c.floorPlay = p.id;
        }

        // ---------------- The dev log ----------------

        /// <summary>Each home's week as the count has it, for the dev log.</summary>
        public static string Describe()
        {
            var t = PlayOutTracker.Instance;
            if (t == null || t.stage.Count == 0) return "No gatherings counted yet.";
            int now = Now;
            return string.Join("\n", t.stage.Select(c =>
            {
                var map = Homes.MapOf(c.colony);
                string name = map != null ? Homes.Name(map) ?? map.ToString() : "colony " + c.colony;
                int since = SinceLast(c, now);
                return name + ": " + StageMath.InWeek(c.held, now) + " in the last 7 days, the last "
                       + (since == int.MaxValue ? "never" : (since / (float)StageMath.TicksPerDay).ToString("0.0") + " days ago")
                       + ", room for the colony's own: " + StageMath.Room(c.held, now, AWeek, since) + ", toasts waiting: " + c.cups.Count;
            }));
        }

        [LudeonTK.DebugAction("Rimfolk", "Pace: the week's gatherings", allowedGameStates = LudeonTK.AllowedGameStates.Playing)]
        private static void DebugWeek() => Log.Message("[Rimfolk] The colony's pace:\n" + Describe());
    }
}
