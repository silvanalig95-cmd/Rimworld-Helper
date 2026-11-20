using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A piece of news the bees are to be told: a death or someone new, whose, at which home, since when.</summary>
    public class BeeNews : IExposable
    {
        public bool death;
        public Pawn who;
        public string name;
        public Map map;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref death, "death");
            Scribe_References.Look(ref who, "who");
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>The news waiting to be told to the bees, and the newcomers already told of (Immersion/TellingBees.cs).</summary>
    public class TellingBeesBook : GameComponent
    {
        public List<BeeNews> waiting = new List<BeeNews>();
        public List<int> told = new List<int>();

        public TellingBeesBook(Game game)
        {
        }

        public static TellingBeesBook Instance => Current.Game?.GetComponent<TellingBeesBook>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            Scribe_Collections.Look(ref told, "told", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                waiting ??= new List<BeeNews>();
                waiting.RemoveAll(n => n == null || n.map == null);
                told ??= new List<int>();
            }
        }

        public void Trim()
        {
            while (waiting.Count > TellingBeesMath.MaxWaiting) waiting.RemoveAt(0);
            while (told.Count > TellingBeesMath.ToldKept) told.RemoveAt(0);
        }
    }

    [DefOf]
    public static class TellingBeesDefOf
    {
        public static JobDef Rimfolk_TellBees;
        public static ThoughtDef Rimfolk_Bees_Told;

        static TellingBeesDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(TellingBeesDefOf));
    }

    /// <summary>
    /// Telling the bees (C23 of docs/content-update-proposals.md; the player's go of 20 November 2026): when one of the colony
    /// dies, or someone new joins, the one who tends the hives most (Alpha Bees' own beekeeping work first, else the best with
    /// animals) walks round the colony's beehouses (Alpha Bees' RB_Beehouse and RB_AdvancedBeehouse, up to four), stands a
    /// moment at each and tells them, one sentence at the first. After a death the teller's grief eases a little
    /// (Rimfolk_Bees_Told), and their journal keeps a line; a plain line in the news says it was done. No penalty if nobody goes:
    /// the news waits three days and is let go. Never comes without Alpha Bees, under the "Small habits" setting. No harm.
    /// </summary>
    public static class TellingBees
    {
        public const string Pool = "Rimfolk_Pool_TellingBees";

        private static List<ThingDef> hives;

        public static List<ThingDef> HiveDefs => hives ??= TellingBeesMath.Hives.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        public static bool On => SmallHabits.On && HiveDefs.Count > 0 && TellingBeesDefOf.Rimfolk_TellBees != null;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's beehouses at a home.</summary>
        public static List<Thing> Hives(Map map) =>
            map?.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && HiveDefs.Contains(b.def)).Cast<Thing>().ToList() ?? new List<Thing>();

        /// <summary>One of the colony died: the bees are to be told, at the home with hives.</summary>
        public static void Died(Pawn p)
        {
            var b = TellingBeesBook.Instance;
            if (b == null || !On || p?.RaceProps?.Humanlike != true || p.Faction != Faction.OfPlayer) return;
            var map = p.MapHeld != null && Homes.IsHome(p.MapHeld) ? p.MapHeld : Homes.Seat;
            if (map == null || Hives(map).Count == 0) return;
            b.waiting.Add(new BeeNews { death = true, who = p, name = p.LabelShort, map = map, tick = Now });
            b.Trim();
        }

        /// <summary>The hour: newcomers noted, and the oldest news told if someone can go now.</summary>
        public static void Hourly()
        {
            var b = TellingBeesBook.Instance;
            if (b == null || !On) return;
            foreach (var map in Homes.All.Where(m => m != null && m.IsPlayerHome).ToList())
            {
                if (Hives(map).Count == 0) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && !b.told.Contains(x.thingIDNumber)
                                                                              && (x.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 99999999f) < GenDate.TicksPerDay))
                {
                    b.told.Add(p.thingIDNumber);
                    b.waiting.Add(new BeeNews { death = false, who = p, name = p.LabelShort, map = map, tick = Now });
                }
            }
            b.waiting.RemoveAll(n => n == null || n.map == null || !TellingBeesMath.StillWaiting((Now - n.tick) / (float)GenDate.TicksPerDay));
            b.Trim();
            var news = b.waiting.FirstOrDefault();
            if (news == null || !TellingBeesMath.Hour(GenLocalDate.HourOfDay(news.map)) || MapFacts.ThreatOn(news.map)) return;
            if (TrySend(news, out _)) b.waiting.Remove(news);
        }

        /// <summary>The one who tends the hives most and is free now, or null.</summary>
        public static Pawn Keeper(Map map, Pawn not)
        {
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => p != not && OwnHappenings.Ours(p) && GodOmens.Awake(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(TellingBeesMath.BeeWork);
            int Skill(Pawn p) => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
            int i = work == null ? -1 : TellingBeesMath.Keeper(people.Select(p => p.workSettings != null && !p.WorkTypeIsDisabled(work) ? p.workSettings.GetPriority(work) : 0).ToList(),
                people.Select(Skill).ToList());
            var keeper = i >= 0 ? people[i] : people.OrderByDescending(Skill).FirstOrDefault();
            return keeper != null && OwnHappenings.Free(keeper) ? keeper : null;
        }

        /// <summary>The keeper sets out round the hives with the news; false (and why) when nobody can go now.</summary>
        public static bool TrySend(BeeNews news, out string why)
        {
            why = null;
            var map = news.map;
            var hivesHere = Hives(map);
            var keeper = map == null ? null : Keeper(map, news.death ? null : news.who);
            if (keeper == null)
            {
                why = "nobody free who tends the hives.";
                return false;
            }
            var route = hivesHere.Where(h => keeper.CanReach(h, PathEndMode.Touch, Danger.Some)).OrderBy(h => h.Position.DistanceToSquared(keeper.Position)).Take(TellingBeesMath.MostHives).ToList();
            if (route.Count == 0)
            {
                why = "no beehouse they can reach.";
                return false;
            }
            var job = JobMaker.MakeJob(TellingBeesDefOf.Rimfolk_TellBees, route[0]);
            job.targetQueueA = route.Select(h => new LocalTargetInfo(h)).ToList();
            job.count = news.death ? 1 : 0;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            telling[keeper.thingIDNumber] = news;
            if (!OwnHappenings.Job(keeper, job)) return false;
            return true;
        }

        /// <summary>The news each teller is carrying (this session; the job ends with it).</summary>
        private static readonly Dictionary<int, BeeNews> telling = new Dictionary<int, BeeNews>();

        /// <summary>At a hive: a moment there, and the telling at the first (one sentence over their head).</summary>
        internal static void AtHive(Pawn p, Thing hive, bool first)
        {
            if (!first || p == null || !telling.TryGetValue(p.thingIDNumber, out var news)) return;
            string line = Flavor.FromPool(Pool, p, null, null, news.death ? "dead" : "new");
            // apart: a word to the bees, said to nobody else.
            if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted((news.name ?? "").Named("NAME"), p.Named("PAWN")).Resolve().CapitalizeFirst());
        }

        /// <summary>The round done: the grief eased a little after a death, a line in the journal and the news.</summary>
        internal static void Done(Pawn p, int hivesTold)
        {
            if (p == null || !telling.TryGetValue(p.thingIDNumber, out var news)) return;
            telling.Remove(p.thingIDNumber);
            if (hivesTold <= 0) return;
            if (news.death && TellingBeesDefOf.Rimfolk_Bees_Told != null && (ColonyCouncilMod.Settings?.MoodEffects ?? true))
                CouncilGameComponent.GiveThought(p, TellingBeesDefOf.Rimfolk_Bees_Told);
            var name = (news.name ?? "").Named("NAME");
            CouncilGameComponent.Instance?.journals?.WriteText(p, (news.death ? "CC_Bees_Journal_Dead" : "CC_Bees_Journal_New").Translate(name).Resolve());
            Tell.Minor(NewsKind.People, Importance.Flavor, p.Map, TextVariants.Vary(news.death ? "CC_Bees_Told_Dead" : "CC_Bees_Told_New").Translate(p.Named("PAWN"), name).Resolve(),
                new LookTargets(p));
        }
    }

    /// <summary>The round of the hives: to each beehouse in turn, a moment at each facing it, the telling at the first.</summary>
    public class JobDriver_TellBees : JobDriver
    {
        private int told;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref told, "told");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            AddFinishAction(_ =>
            {
                try
                {
                    TellingBees.Done(pawn, told);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("telling the bees: done", e);
                }
            });
            var next = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return next;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNull(TargetIndex.A, next);
            var stay = ToilMaker.MakeToil("TellBees");
            stay.initAction = () =>
            {
                told++;
                try
                {
                    TellingBees.AtHive(pawn, job.targetA.Thing, told == 1);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("telling the bees: at a hive", e);
                }
            };
            stay.tickIntervalAction = _ => pawn.rotationTracker.FaceTarget(job.targetA);
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = TellingBeesMath.StandTicks;
            stay.handlingFacing = true;
            stay.socialMode = RandomSocialMode.Off;
            yield return stay;
            yield return Toils_Jump.JumpIf(next, () => !job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
        }

        public override string GetReport() => "CC_Bees_Report".Translate();
    }

    /// <summary>One of the colony died: the bees are to be told (Immersion/TellingBees.cs). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_TellingBeesDied
    {
        public static void Postfix(Pawn __instance)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.Dead || __instance.RaceProps?.Humanlike != true) return;
            try
            {
                TellingBees.Died(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("telling the bees: a death", e);
            }
        }
    }

    /// <summary>Dev mode: the bees told now (Debug actions, "Rimfolk", "Bees: ...").</summary>
    public static class TellingBeesDebug
    {
        [DebugAction("Rimfolk", "Bees: tell the bees of a death now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeath() => Tell(true);

        [DebugAction("Rimfolk", "Bees: tell the bees of someone new now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNew() => Tell(false);

        private static void Tell(bool death)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            if (!TellingBees.On)
            {
                Messages.Message("Bees: no Alpha Bees, or small habits are off.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var who = map.mapPawns.FreeColonistsSpawned.LastOrDefault();
            var news = new BeeNews { death = death, who = who, name = who?.LabelShort ?? "someone", map = map, tick = Find.TickManager.TicksGame };
            Messages.Message("Bees: " + (TellingBees.TrySend(news, out string why) ? "the one who tends the hives goes round them with the news." : why), MessageTypeDefOf.SilentInput, false);
        }
    }
}
