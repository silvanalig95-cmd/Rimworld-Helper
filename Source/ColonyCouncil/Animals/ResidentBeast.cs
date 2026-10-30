using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A wild beast that stays (HerdStoryMath): a wolf, a bear, a stag that has been on the colony's land for days is
    /// given a name by a colonist, and from then on the colony watches it: whoever sees it says so, and a few days
    /// later the council is asked what to make of it (Council_DlmA_ResidentBeast: leave food out, keep close, hunt it,
    /// tame it, let it be, drive it off), and again every so often while it stays. The story closes when it is hunted,
    /// tamed, driven off, gone, or still here after a season, part of the land. Saved on the council
    /// (CouncilGameComponent.beast); runs from AnimalLife.Daily and Hourly.
    /// </summary>
    public class ResidentBeast : IExposable
    {
        public Pawn animal;
        public Pawn namer;
        public int since = -1, namedTick = -1, fed, asks, lastAskTick = -1, lastSightTick = -1, candidateId, candidateDays, fateTick = -1;
        public bool feared;
        /// <summary>The colony's last answer: watched, fed, feared, hunted, tame, driven.</summary>
        public string stance = "watched";
        /// <summary>Beasts whose story has closed (ids), not taken up again.</summary>
        public List<int> knownIds = new List<int>();

        public const int KnownMax = 20;
        public const string Dilemma = "Council_DlmA_ResidentBeast";

        private static int Now => Find.TickManager.TicksGame;

        public bool Here => animal != null && !animal.Dead && !animal.Destroyed && animal.Spawned;

        public float DaysNamed => namedTick < 0 ? 0f : (Now - namedTick) / (float)GenDate.TicksPerDay;

        public bool Predator => animal?.RaceProps?.predator ?? false;

        public string StanceNow => HerdStoryMath.Stance(fed, feared);

        public static bool On => AnimalLife.On && ColonyCouncilMod.Settings.strangers;

        // ---------------- The days ----------------

        public void Daily(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            var map = HappeningTracker.HomeMap();
            if (map == null) return;
            if (animal != null)
            {
                Check(comp, map);
                return;
            }
            if (fateTick >= 0 && Now - fateTick < HerdStoryMath.RestDays * GenDate.TicksPerDay) return;
            var a = Candidate(map);
            if (a == null)
            {
                candidateId = 0;
                candidateDays = 0;
                return;
            }
            if (a.thingIDNumber == candidateId) candidateDays++;
            else
            {
                candidateId = a.thingIDNumber;
                candidateDays = 1;
            }
            if (candidateDays >= HerdStoryMath.ResidentAfterDays) Become(comp, map, a);
        }

        /// <summary>A plain wild animal of some size, about on the land and not a story already.</summary>
        public static bool Fits(Pawn a, Map map)
        {
            if (a?.RaceProps == null || !a.RaceProps.Animal || a.Faction != null || a.Dead || a.Downed || !a.Spawned || a.InMentalState) return false;
            if (map != null && map.fogGrid.IsFogged(a.Position)) return false;
            if (!HerdStoryMath.BeastFits(a.RaceProps.baseBodySize, a.RaceProps.predator, a.RaceProps.Insect, Strangers.IsMajestic(a))) return false;
            var tags = new HashSet<string>();
            AnimalTags.Add(a, tags);
            return !tags.Contains("pokemon");
        }

        private Pawn Candidate(Map map)
        {
            var fits = map.mapPawns.AllPawnsSpawned.Where(a => Fits(a, map) && !knownIds.Contains(a.thingIDNumber)).ToList();
            if (fits.Count == 0) return null;
            var same = fits.FirstOrDefault(a => a.thingIDNumber == candidateId);
            return same ?? fits.OrderByDescending(a => a.RaceProps.baseBodySize).First();
        }

        private void Become(CouncilGameComponent comp, Map map, Pawn a)
        {
            animal = a;
            since = namedTick = Now;
            asks = 0;
            fed = 0;
            feared = false;
            stance = "watched";
            lastAskTick = -1;
            lastSightTick = -1;
            namer = Namer(map);
            HerdStory.Name(a);
            string kind = AnimalFacts.KindLabel(a);
            try
            {
                var who = namer ?? HerdStory.Keeper(map, null);
                if (who != null)
                {
                    Find.LetterStack.ReceiveLetter("CC_Beast_NamedTitle".Translate(kind.Named("KIND")).Resolve(),
                        "CC_Beast_Named".Translate(a.Named("PET"), who.Named("PAWN"), kind.Named("KIND")).Resolve(), LetterDefOf.NeutralEvent, new LookTargets(a));
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_Beast_NamedChronicle".Translate(a.Named("PET"), who.Named("PAWN"), kind.Named("KIND")).Resolve(), who));
                    Interplay.Journal(who, "Council_Journal_Beast_Named", new[] { Predator ? "predator" : "grazer" }, a.Named("PET"), kind.Named("KIND"));
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast named", e);
            }
        }

        /// <summary>Who names it: a child, else someone with a love of animals, else anyone.</summary>
        private static Pawn Namer(Map map)
        {
            var people = IncidentWorker_Happening.Colonists(map).Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).ToList();
            if (people.Count == 0) return null;
            var child = people.Where(p => p.DevelopmentalStage.Child()).RandomElementWithFallback();
            if (child != null && Rand.Chance(0.6f)) return child;
            var lover = people.Where(p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && People.Passionate(r)).RandomElementWithFallback();
            return lover ?? people.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback() ?? people.RandomElement();
        }

        private void Check(CouncilGameComponent comp, Map map)
        {
            if (animal.Dead || animal.Destroyed)
            {
                End(comp, stance == "hunted" ? "hunted" : "died");
                return;
            }
            if (animal.Faction == Faction.OfPlayer)
            {
                End(comp, "tamed");
                return;
            }
            if (!animal.Spawned || animal.Map != map)
            {
                End(comp, stance == "driven" ? "driven" : "gone");
                return;
            }
            if (DaysNamed >= HerdStoryMath.FateAfterDays)
            {
                End(comp, "stays");
                return;
            }
            float sinceAsk = lastAskTick < 0 ? 0f : (Now - lastAskTick) / (float)GenDate.TicksPerDay;
            if (!HerdStoryMath.AskDue(DaysNamed, asks, sinceAsk)) return;
            if (comp.dilemmas.TryFire(true, d => d.defName == Dilemma))
            {
                asks++;
                lastAskTick = Now;
            }
        }

        private void End(CouncilGameComponent comp, string key)
        {
            var a = animal;
            try
            {
                var who = namer != null && !namer.Dead ? namer : null;
                string text = ("CC_Beast_End_" + key).Translate(a.Named("PET"), (who ?? a).Named("PAWN")).Resolve();
                News.Post(text, a.Spawned ? new LookTargets(a) : LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, who));
                if (key == "stays" && who != null && who.Spawned) CouncilGameComponent.GiveThought(who, HerdStoryDefOf.Council_Beast_PartOfTheLand);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast's end", e);
            }
            if (a != null)
            {
                knownIds.Add(a.thingIDNumber);
                while (knownIds.Count > KnownMax) knownIds.RemoveAt(0);
            }
            animal = null;
            namer = null;
            fateTick = Now;
            candidateId = 0;
            candidateDays = 0;
        }

        // ---------------- Sightings ----------------

        /// <summary>Every hour: someone in sight of the beast says so, now and then.</summary>
        public void Hourly(CouncilGameComponent comp)
        {
            if (!On || !Here || Now - lastSightTick < GenDate.TicksPerHour * 6 || !Rand.Chance(0.5f)) return;
            var map = animal.Map;
            if (map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned
                .Where(c => People.HasInnerLife(c) && !c.Downed && !c.Drafted && c.Awake() && c.Position.InHorDistOf(animal.Position, 16f) && GenSight.LineOfSight(c.Position, animal.Position, map))
                .RandomElementWithFallback();
            if (p == null) return;
            var tags = new List<string> { Predator ? "predator" : "grazer", StanceNow };
            if (p.DevelopmentalStage.Juvenile()) tags.Add("child");
            string line = Flavor.FromPool("Council_Pool_BeastSighting", p, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return;
            // apart: a remark on seeing the beast, said to nobody in particular; the beast is where they look.
            if (Speech.Say(p, line.Formatted(animal.Named("PET"), p.Named("PAWN")).Resolve())) lastSightTick = Now;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref animal, "animal");
            Scribe_References.Look(ref namer, "namer");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref namedTick, "namedTick", -1);
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref asks, "asks");
            Scribe_Values.Look(ref lastAskTick, "lastAskTick", -1);
            Scribe_Values.Look(ref lastSightTick, "lastSightTick", -1);
            Scribe_Values.Look(ref candidateId, "candidateId");
            Scribe_Values.Look(ref candidateDays, "candidateDays");
            Scribe_Values.Look(ref fateTick, "fateTick", -1);
            Scribe_Values.Look(ref feared, "feared");
            Scribe_Values.Look(ref stance, "stance", "watched");
            Scribe_Collections.Look(ref knownIds, "knownIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (knownIds == null) knownIds = new List<int>();
                if (stance.NullOrEmpty()) stance = "watched";
            }
        }
    }

    /// <summary>The question about the beast that stays: X the beast, A the one who named it (grown) or whoever is best with animals, B the best shot.</summary>
    public class DilemmaWorker_ResidentBeast : DilemmaWorker
    {
        private static ResidentBeast Beast => CouncilGameComponent.Instance?.beast;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var rb = Beast;
            if (rb == null || !rb.Here || rb.animal.Map != map || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var n = rb.namer;
            var a = n != null && !n.Dead && n.Spawned && n.Map == map && n.IsColonist && !n.DevelopmentalStage.Juvenile() ? n : VarietyUtil.Best(map, SkillDefOf.Animals);
            if (a == null) return false;
            var b = pawns.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = rb.animal };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var rb = Beast;
            if (rb == null) return;
            tags.Add(rb.Predator ? "predator" : "grazer");
            tags.Add(rb.StanceNow);
            if (rb.asks >= 1) tags.Add("asked" + Mathf.Min(3, rb.asks + 1));
        }
    }

    /// <summary>The colony's answer about the beast, kept for its lines and its end: fed, feared, hunted, tame, watched, driven.</summary>
    public class DilemmaEffect_BeastStance : DilemmaEffect
    {
        public string stance = "watched";

        public override void Apply(DilemmaContext ctx)
        {
            var rb = CouncilGameComponent.Instance?.beast;
            if (rb == null || rb.animal == null) return;
            rb.stance = stance;
            if (stance == "fed")
            {
                rb.fed++;
                rb.feared = false;
            }
            else if (stance == "feared") rb.feared = true;
        }
    }

    /// <summary>Food for the beast, carried out to it and left (PlayOut/): meat for a meat-eater, hay or raw food for a grazer.</summary>
    public class DilemmaEffect_FeedBeast : DilemmaEffect
    {
        public int count = 10;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map ?? x?.Map;
            if (x == null || map == null || !x.Spawned) return;
            string what = Food(x, map);
            if (what == null) return;
            // apart: a colonist carries the food out to where the beast is and leaves it (CarryOut to the stranger).
            new DilemmaEffect_CarryOut { what = what, count = count, who = PlayWho.A, to = PlaySpot.Stranger, steward = false, label = "CC_Beast_FeedLabel".Translate(x.Named("PET")).Resolve() }.Apply(ctx);
        }

        /// <summary>What it would eat, of what the colony has: the meat there is most of, hay, or any raw food.</summary>
        public static string Food(Pawn x, Map map)
        {
            bool meat = x.RaceProps?.Eats(FoodTypeFlags.Meat) ?? false;
            if (meat)
            {
                var best = map.listerThings.AllThings.Where(t => t.Spawned && t.def.IsMeat && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    .GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.stackCount)).FirstOrDefault();
                return best?.Key.defName;
            }
            var hay = DefDatabase<ThingDef>.GetNamedSilentFail("Hay");
            if (hay != null && map.listerThings.ThingsOfDef(hay).Any(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer))) return hay.defName;
            return "rawfood";
        }
    }

    /// <summary>The beast is driven off the land: it makes for the nearest edge and goes (a group of its own with LordJob_ExitMapNear).</summary>
    public class DilemmaEffect_DriveOff : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Faction != null || x.Map == null) return;
            try
            {
                x.GetLord()?.RemovePawn(x);
                LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(x.Position, LocomotionUrgency.Jog, 14f), x.Map, new List<Pawn> { x });
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast driven off", e);
            }
        }
    }
}
