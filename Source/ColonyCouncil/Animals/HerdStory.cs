using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The herd's year (HerdStoryMath): the colony's livestock has a leader, the eldest of the many, named by its keeper or
    /// the herdmaster; its line is counted generation by generation as its blood is born into the herd; the first calf of
    /// each spring is a moment the keeper goes down to the pens for; when the old leader grows old its last season is
    /// told, and when it dies the herd follows one of its own. Saved on the council (CouncilGameComponent.herd); runs
    /// from AnimalLife.Daily and AnimalLife.Born.
    /// </summary>
    public class HerdStory : IExposable
    {
        public Pawn leader;
        public int leaderSince = -1;
        /// <summary>The leader's generation in its line (1: the line began with it), and the name of the one the line began with.</summary>
        public int generation = 1;
        public string lineName;
        /// <summary>The highest generation told of, the tick the old leader's last season was told (-1: not yet), the year whose first spring calf was marked, how many leaders there have been.</summary>
        public int toldGeneration = 1, lastSeasonTick = -1, springYear = -1, leaders;
        /// <summary>Animals of the line (ids) and their generations, the newest <see cref="LineMax"/>.</summary>
        public List<int> lineIds = new List<int>();
        public List<int> lineGens = new List<int>();

        public const int LineMax = 60;

        private static int Now => Find.TickManager.TicksGame;

        public static bool On => AnimalLife.On;

        /// <summary>Once a day: the leader lost, chosen, or grown old.</summary>
        public void Daily(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            var map = HappeningTracker.HomeMap();
            if (map == null) return;
            if (leader != null && (leader.Dead || leader.Destroyed || !AnimalFacts.Ours(leader))) Lost(comp, map);
            if (leader == null)
            {
                var herd = AnimalFacts.Herd(map);
                if (herd.Count >= HerdStoryMath.MinHerd) Choose(comp, map, herd);
                return;
            }
            if (lastSeasonTick < 0 && AnimalFacts.Old(leader)) LastSeason(comp, map);
        }

        // ---------------- The line ----------------

        private int GenerationOf(Pawn a)
        {
            if (a == null) return 0;
            int i = lineIds.IndexOf(a.thingIDNumber);
            return i >= 0 && i < lineGens.Count ? lineGens[i] : 0;
        }

        /// <summary>Of the leader's line: itself, or born to one of it.</summary>
        private bool OfTheLine(Pawn a)
        {
            if (a == null) return false;
            if (lineIds.Contains(a.thingIDNumber)) return true;
            var m = a.GetMother();
            var f = a.GetFather();
            return (m != null && lineIds.Contains(m.thingIDNumber)) || (f != null && lineIds.Contains(f.thingIDNumber));
        }

        private int LineGeneration(Pawn a)
        {
            int own = GenerationOf(a);
            if (own > 0) return own;
            int m = GenerationOf(a?.GetMother()), f = GenerationOf(a?.GetFather());
            int parent = Mathf.Max(m, f);
            return parent > 0 ? HerdStoryMath.Generation(parent) : 0;
        }

        private void AddToLine(Pawn a, int gen)
        {
            if (a == null || lineIds.Contains(a.thingIDNumber)) return;
            lineIds.Add(a.thingIDNumber);
            lineGens.Add(gen);
            while (lineIds.Count > LineMax)
            {
                lineIds.RemoveAt(0);
                if (lineGens.Count > 0) lineGens.RemoveAt(0);
            }
        }

        // ---------------- The leader ----------------

        private void Choose(CouncilGameComponent comp, Map map, List<Pawn> herd)
        {
            var adults = herd.Where(a => a.ageTracker != null && a.ageTracker.Adult && !a.Dead).ToList();
            if (adults.Count == 0) return;
            var ofKind = new Dictionary<ThingDef, int>();
            foreach (var a in adults) ofKind[a.def] = (ofKind.TryGetValue(a.def, out int c) ? c : 0) + 1;
            Pawn best = null;
            float bestScore = -1f;
            foreach (var a in adults)
            {
                float score = HerdStoryMath.LeaderScore(a.ageTracker.AgeBiologicalYearsFloat, ofKind[a.def], adults.Count, lineName != null && OfTheLine(a));
                if (score > bestScore)
                {
                    best = a;
                    bestScore = score;
                }
            }
            if (best == null) return;
            bool first = leaders == 0;
            bool ofLine = lineName != null && OfTheLine(best);
            leaders++;
            if (ofLine) generation = Mathf.Max(2, LineGeneration(best));
            else
            {
                generation = 1;
                toldGeneration = 1;
                lineIds.Clear();
                lineGens.Clear();
                lineName = null;
            }
            leader = best;
            leaderSince = Now;
            lastSeasonTick = -1;
            Name(best);
            if (lineName == null) lineName = best.LabelShort;
            AddToLine(best, generation);
            var keeper = Keeper(map, best);
            string kind = AnimalFacts.KindLabel(best);
            var tags = new[] { first ? "first" : ofLine ? "line" : "new" };
            try
            {
                if (keeper != null)
                {
                    string title = (first ? "CC_Herd_LeaderTitle" : "CC_Herd_NewLeaderTitle").Translate(best.Named("PET")).Resolve();
                    string text = first
                        ? "CC_Herd_Leader".Translate(best.Named("PET"), keeper.Named("PAWN"), kind.Named("KIND")).Resolve()
                        : ofLine
                            ? "CC_Herd_NewLeader_Line".Translate(best.Named("PET"), keeper.Named("PAWN"), lineName.Named("LINE"), Ordinal(generation).Named("COUNT")).Resolve()
                            : "CC_Herd_NewLeader_New".Translate(best.Named("PET"), keeper.Named("PAWN"), kind.Named("KIND")).Resolve();
                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new LookTargets(best));
                    Interplay.Journal(keeper, "Council_Journal_Herd_Leader", tags, best.Named("PET"), kind.Named("KIND"));
                    var k = keeper;
                    var l = best;
                    Together.When("herdleader|" + best.thingIDNumber, keeper, best, () =>
                    {
                        if (l.Dead || !AnimalFacts.Ours(l)) return;
                        string line = Interplay.Line("Council_Pool_Herd_SayLeader", k, tags, l.Named("PET"));
                        // together: the keeper is at the leader's side (Together.When), and speaks to it.
                        if (!line.NullOrEmpty()) Speech.Say(k, line, null);
                    }, 24f, 2f);
                }
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Herd_LeaderChronicle".Translate(best.Named("PET")).Resolve(), keeper));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("herd leader", e);
            }
        }

        private void LastSeason(CouncilGameComponent comp, Map map)
        {
            lastSeasonTick = Now;
            var keeper = Keeper(map, leader);
            if (keeper == null) return;
            try
            {
                Find.LetterStack.ReceiveLetter("CC_Herd_LastSeasonTitle".Translate(leader.Named("PET")).Resolve(),
                    "CC_Herd_LastSeason".Translate(leader.Named("PET"), keeper.Named("PAWN")).Resolve(), LetterDefOf.NeutralEvent, new LookTargets(leader));
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Herd_LastSeasonChronicle".Translate(leader.Named("PET")).Resolve(), keeper));
                Interplay.Journal(keeper, "Council_Journal_Herd_LastSeason", new string[0], leader.Named("PET"), AnimalFacts.KindLabel(leader).Named("KIND"));
                // The keeper sits with the old leader at the pens this evening (PlayOut/).
                new DilemmaEffect_Visit { label = "CC_Herd_SitWith".Translate(leader.Named("PET")).Resolve(), whom = PlayWho.Pet, hours = new IntRange(17, 21), duration = new FloatRange(0.75f, 1f) }
                    .Apply(new DilemmaContext { map = map, a = keeper, pet = leader });
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("herd leader's last season", e);
            }
        }

        private void Lost(CouncilGameComponent comp, Map map)
        {
            var gone = leader;
            leader = null;
            lastSeasonTick = -1;
            if (gone == null) return;
            var keeper = Keeper(map, gone);
            try
            {
                if (keeper != null)
                {
                    Find.LetterStack.ReceiveLetter("CC_Herd_GoneTitle".Translate(gone.Named("PET")).Resolve(),
                        "CC_Herd_Gone".Translate(gone.Named("PET"), keeper.Named("PAWN")).Resolve(), LetterDefOf.NeutralEvent, LookTargets.Invalid);
                    CouncilGameComponent.GiveThought(keeper, HerdStoryDefOf.Council_Herd_LeaderGone);
                    Interplay.Journal(keeper, "Council_Journal_Herd_Gone", new string[0], gone.Named("PET"), AnimalFacts.KindLabel(gone).Named("KIND"));
                }
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, "CC_Herd_GoneChronicle".Translate(gone.Named("PET")).Resolve(), keeper));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("herd leader lost", e);
            }
        }

        // ---------------- Births ----------------

        /// <summary>A newborn in the herd (AnimalLife.Born): the first calf of spring, and a generation of the leader's line.</summary>
        public void Notify_Newborn(CouncilGameComponent comp, Pawn baby, Pawn mother)
        {
            if (!On || comp == null || baby == null || mother == null) return;
            var map = baby.MapHeld ?? HappeningTracker.HomeMap();
            if (map == null) return;
            try
            {
                if (HerdStoryMath.SpringCalf(GenLocalDate.Season(map) == Season.Spring, GenLocalDate.Year(map), springYear))
                {
                    springYear = GenLocalDate.Year(map);
                    SpringCalf(comp, map, baby, mother);
                }
                int mg = GenerationOf(mother);
                if (mg <= 0) return;
                int gen = HerdStoryMath.Generation(mg);
                AddToLine(baby, gen);
                if (!HerdStoryMath.GenerationNews(gen, toldGeneration)) return;
                toldGeneration = gen;
                var keeper = Keeper(map, mother);
                string ordinal = Ordinal(gen);
                News.Post("CC_Herd_Generation".Translate(baby.Named("PET"), mother.Named("OTHER"), (lineName ?? "").Named("LINE"), ordinal.Named("COUNT")).Resolve(),
                    new LookTargets(baby), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Herd_GenerationChronicle".Translate(baby.Named("PET"), (lineName ?? "").Named("LINE"), ordinal.Named("COUNT")).Resolve(), keeper));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("herd newborn", e);
            }
        }

        /// <summary>The first calf of spring: the keeper goes down to the pens and stays until it stands; the colony is glad of it.</summary>
        private void SpringCalf(CouncilGameComponent comp, Map map, Pawn baby, Pawn mother)
        {
            var keeper = Keeper(map, mother);
            if (keeper == null) return;
            string kind = AnimalFacts.KindLabel(mother);
            Find.LetterStack.ReceiveLetter("CC_Herd_SpringCalfTitle".Translate().Resolve(),
                "CC_Herd_SpringCalf".Translate(baby.Named("PET"), mother.Named("OTHER"), keeper.Named("PAWN")).Resolve(), LetterDefOf.PositiveEvent, new LookTargets(baby));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Herd_SpringCalfChronicle".Translate(baby.Named("PET")).Resolve(), keeper));
            foreach (var p in IncidentWorker_Happening.Colonists(map))
                if (People.HasInnerLife(p)) CouncilGameComponent.GiveThought(p, HerdStoryDefOf.Council_Herd_FirstSpringCalf);
            Interplay.Journal(keeper, "Council_Journal_Herd_SpringCalf", new string[0], baby.Named("PET"), mother.Named("OTHER"), kind.Named("KIND"));
            // apart: the keeper walks down to the newborn's side (PlayOut/) and stays a while.
            new DilemmaEffect_Visit { label = "CC_Herd_SpringVisit".Translate().Resolve(), whom = PlayWho.Pet, hours = new IntRange(6, 18), duration = new FloatRange(0.5f, 0.75f) }
                .Apply(new DilemmaContext { map = map, a = keeper, pet = baby });
        }

        // ---------------- Helpers ----------------

        /// <summary>Who looks after an animal of the herd: its own person, the herdmaster, else whoever is best with animals.</summary>
        public static Pawn Keeper(Map map, Pawn a)
        {
            var person = PetTracker.PersonOf(a);
            if (person != null && !person.Dead && person.Spawned && person.Map == map) return person;
            var hm = AnimalFacts.Herdmaster;
            if (hm != null && !hm.Dead && hm.Spawned && hm.Map == map) return hm;
            return IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
        }

        public static void Name(Pawn a)
        {
            if (a == null || (a.Name != null && !a.Name.Numerical)) return;
            try
            {
                a.Name = PawnBioAndNameGenerator.GeneratePawnName(a, NameStyle.Full);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("naming an animal", e);
            }
        }

        public static string Ordinal(int n)
        {
            string key = "CC_Herd_Ordinal_" + n;
            return key.CanTranslate() ? key.Translate().ToString() : "CC_Herd_Ordinal_More".Translate(n).ToString();
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref leader, "leader");
            Scribe_Values.Look(ref leaderSince, "leaderSince", -1);
            Scribe_Values.Look(ref generation, "generation", 1);
            Scribe_Values.Look(ref lineName, "lineName");
            Scribe_Values.Look(ref toldGeneration, "toldGeneration", 1);
            Scribe_Values.Look(ref lastSeasonTick, "lastSeasonTick", -1);
            Scribe_Values.Look(ref springYear, "springYear", -1);
            Scribe_Values.Look(ref leaders, "leaders");
            Scribe_Collections.Look(ref lineIds, "lineIds", LookMode.Value);
            Scribe_Collections.Look(ref lineGens, "lineGens", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lineIds == null) lineIds = new List<int>();
                if (lineGens == null) lineGens = new List<int>();
                while (lineGens.Count > lineIds.Count) lineGens.RemoveAt(lineGens.Count - 1);
                while (lineGens.Count < lineIds.Count) lineGens.Add(1);
            }
        }
    }

    [DefOf]
    public static class HerdStoryDefOf
    {
        public static ThoughtDef Council_Herd_FirstSpringCalf;
        public static ThoughtDef Council_Herd_LeaderGone;
        public static ThoughtDef Council_Beast_PartOfTheLand;

        static HerdStoryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HerdStoryDefOf));
        }
    }
}
