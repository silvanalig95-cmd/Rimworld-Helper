using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// PokéWorld in the colony's life, read by name like the rest of the integration (Integrations/PokeWorld.cs):
    /// <list type="bullet">
    /// <item>An evolution is a colony moment: the trainer's reaction (a line, a memory that gives practice instead of
    /// mood, a journal entry), a word from whoever is beside them, the news and, for a named Pokémon, the chronicle.
    /// PokéWorld evolves a Pokémon by making a new one and copying the old one's age, name, master and records over, so an
    /// evolution is seen once an hour as the same birth tick under a new kind, the old one gone without dying.</item>
    /// <item>Levels 25, 50, 75 and 100, and a friendship that grows into a bond with its trainer.</item>
    /// <item>A Pokémon that faints (goes down) in a fight worries its trainer; one that dies is mourned like any pet
    /// (Strangers/Pets.cs, with lines of its own).</item>
    /// <item>The Pokédex counted from PokéWorld's own record, for a council project and a life goal.</item>
    /// <item>A rare Pokémon seen on the colony's land (<see cref="HappeningWorker_PokemonSighting"/>).</item>
    /// </list>
    /// What is read: the comp's levelTracker.level, friendshipTracker.friendship and shinyTracker.isShiny fields, and the
    /// PokedexManager world component (TotalCaught, or its pokedex dictionary). The original mod, eth0net's 1.5 update and
    /// ports of it share these names; anything that can't be read is skipped quietly after one warning in the log. Nothing
    /// is saved: after a load the first hour only looks.
    /// </summary>
    public static class PokeWorldLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ===================== Reading a Pokémon (by name) =====================

        private sealed class Reader
        {
            public FieldInfo tracker;
            public FieldInfo value;
        }

        private static readonly Dictionary<(Type, string), Reader> readers = new Dictionary<(Type, string), Reader>();
        private static readonly HashSet<string> failed = new HashSet<string>();

        private static ThingComp CompOf(Pawn p)
        {
            if (p?.AllComps == null || !PokeWorldLink.IsPokemon(p)) return null;
            foreach (var c in p.AllComps)
                if (c != null && c.GetType().Name == "CompPokemon") return c;
            return null;
        }

        /// <summary>A value on one of the comp's trackers (levelTracker.level...), or null when it can't be read.</summary>
        private static object Read(Pawn p, string trackerName, string valueName)
        {
            string what = trackerName + "." + valueName;
            if (failed.Contains(what)) return null;
            var comp = CompOf(p);
            if (comp == null) return null;
            try
            {
                var key = (comp.GetType(), what);
                if (!readers.TryGetValue(key, out var r))
                {
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    var tf = comp.GetType().GetField(trackerName, flags);
                    var vf = tf?.FieldType.GetField(valueName, flags);
                    r = tf != null && vf != null ? new Reader { tracker = tf, value = vf } : null;
                    readers[key] = r;
                    if (r == null)
                    {
                        failed.Add(what);
                        CouncilLog.WarnOnce("PokéWorld: couldn't find " + what + " on its Pokémon; Rimfolk does without it.");
                        return null;
                    }
                }
                if (r == null) return null;
                var t = r.tracker.GetValue(comp);
                return t == null ? null : r.value.GetValue(t);
            }
            catch (Exception e)
            {
                failed.Add(what);
                CouncilLog.ErrorOnce("PokéWorld: reading " + what, e);
                return null;
            }
        }

        /// <summary>Its level (1 to 100), or 0 when it can't be read.</summary>
        public static int Level(Pawn p) => Read(p, "levelTracker", "level") is int n ? n : 0;

        /// <summary>Its friendship with the colony (0 to 255), or -1 when it can't be read.</summary>
        public static int Friendship(Pawn p) => Read(p, "friendshipTracker", "friendship") is int n ? n : -1;

        public static bool Shiny(Pawn p) => Read(p, "shinyTracker", "isShiny") is bool b && b;

        // ===================== The Pokédex =====================

        private static bool dexFailed;

        /// <summary>Species the colony has caught, as PokéWorld's Pokédex counts them; -1 when it can't be read.</summary>
        public static int Caught()
        {
            if (!PokeWorldLink.Active || dexFailed || Find.World?.components == null) return -1;
            try
            {
                var dex = Find.World.components.FirstOrDefault(c => c != null && c.GetType().Name == "PokedexManager");
                if (dex == null) return -1;
                var total = dex.GetType().GetMethod("TotalCaught", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (total != null && total.ReturnType == typeof(int)) return (int)total.Invoke(dex, null);
                var f = dex.GetType().GetField("pokedex", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (!(f?.GetValue(dex) is IDictionary entries)) return -1;
                int n = 0;
                foreach (DictionaryEntry e in entries)
                    if (e.Value?.ToString() == "Caught") n++;
                return n;
            }
            catch (Exception e)
            {
                dexFailed = true;
                CouncilLog.ErrorOnce("PokéWorld: reading the Pokédex", e);
                return -1;
            }
        }

        // ===================== The colony's Pokémon, once an hour =====================

        private sealed class Seen
        {
            public Pawn pawn;
            public string kind;
            public string kindLabel;
            public int level;
            public int friendship;
            public bool down;
        }

        /// <summary>The colony's Pokémon at the last look, by birth tick (an evolution keeps it).</summary>
        private static readonly Dictionary<long, Seen> seen = new Dictionary<long, Seen>();
        private static Game seenGame;
        private static bool looked;

        /// <summary>Colony Pokémon that went down in the fight going on, and whose they are.</summary>
        private static readonly Dictionary<Pawn, Pawn> fainted = new Dictionary<Pawn, Pawn>();

        public static void Hourly(CouncilGameComponent comp)
        {
            if (!PokeWorldLink.Active || comp == null) return;
            if (seenGame != Current.Game)
            {
                seenGame = Current.Game;
                seen.Clear();
                fainted.Clear();
                looked = false;
                rareOne = null;
            }
            var now = new Dictionary<long, Seen>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList())
                {
                    if (p.Dead || !PokeWorldLink.IsPokemon(p) || p.ageTracker == null) continue;
                    long key = p.ageTracker.BirthAbsTicks;
                    if (now.ContainsKey(key)) continue;
                    now[key] = new Seen
                    {
                        pawn = p, kind = p.kindDef?.defName, kindLabel = AnimalFacts.KindLabel(p), level = Level(p), friendship = Friendship(p),
                        down = p.Spawned && p.Downed
                    };
                }
            }
            if (looked)
            {
                foreach (var kv in now)
                {
                    if (!seen.TryGetValue(kv.Key, out var before)) continue;
                    var p = kv.Value.pawn;
                    try
                    {
                        if (before.pawn != p && AnimalMath.Evolved(before.kind, kv.Value.kind, before.pawn != null && !before.pawn.Dead && before.pawn.Destroyed))
                            Evolved(comp, p, before.kindLabel);
                        else if (before.pawn == p)
                        {
                            int m = AnimalMath.MilestonePassed(before.level, kv.Value.level);
                            if (m > 0) Milestone(comp, p, m);
                            if (before.friendship >= 0 && before.friendship < AnimalMath.BondFriendship && kv.Value.friendship >= AnimalMath.BondFriendship) Friends(comp, p);
                            if (!before.down && kv.Value.down) Fainted(p);
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("PokéWorld: the colony's Pokémon", e);
                    }
                }
            }
            seen.Clear();
            foreach (var kv in now) seen[kv.Key] = kv.Value;
            looked = true;
            AfterFight(comp);
            RareOneWatch(comp);
        }

        /// <summary>Whose it is: the colonist it's bonded to, else its master (People with an inner life).</summary>
        public static Pawn TrainerOf(Pawn pokemon) => PetTracker.PersonOf(pokemon);

        private static void Evolved(CouncilGameComponent comp, Pawn p, string oldKind)
        {
            var trainer = TrainerOf(p);
            string newKind = AnimalFacts.KindLabel(p);
            bool named = p.Name != null && !p.Name.Numerical;
            string text = trainer != null
                ? "CC_PW_Evolved".Translate(p.Named("PET"), trainer.Named("PAWN"), oldKind.Named("OLD"), newKind.Named("NEW")).Resolve()
                : "CC_PW_EvolvedNobody".Translate(p.Named("PET"), oldKind.Named("OLD"), newKind.Named("NEW")).Resolve();
            using (Causes.Push("CC_PW_Cause_Evolved".Translate(oldKind.Named("OLD"), newKind.Named("NEW")).Resolve()))
            {
                News.Post(text, new LookTargets(p), NewsKind.Land, MessageTypeDefOf.PositiveEvent, named ? Importance.Notable : Importance.Flavor);
                if (named) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, trainer));
                if (trainer == null) return;
                CouncilGameComponent.GiveThought(trainer, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_PW_Evolved"));
                comp.journals.WriteTagged(trainer, "Council_Journal_PW_Evolved", Tags(p), null, p.Named("PET"), oldKind.Named("OLD"), newKind.Named("NEW"));
                comp.aspirations.Notify(trainer, "pw_evolved");
                Say(trainer, "Council_Pool_PW_SayEvolved", p, oldKind, newKind);
                // Whoever is beside the trainer when it happens says so, face to face.
                var with = Together.Company(trainer);
                if (with != null && S.colonistMoments)
                {
                    string said = Interplay.Line("Council_Pool_PW_Congratulate", with, Tags(p), trainer.Named("OTHER"), p.Named("PET"), oldKind.Named("OLD"), newKind.Named("NEW"));
                    if (!said.NullOrEmpty()) Speech.Logged(with, trainer, said);
                }
            }
        }

        private static void Milestone(CouncilGameComponent comp, Pawn p, int level)
        {
            var trainer = TrainerOf(p);
            if (trainer == null) return;
            using (Causes.Push("CC_PW_Cause_Level".Translate(p.Named("PET"), level.Named("LEVEL")).Resolve()))
            {
                News.Post("CC_PW_Level".Translate(p.Named("PET"), trainer.Named("PAWN"), level.Named("LEVEL")).Resolve(), new LookTargets(p), NewsKind.Land,
                    MessageTypeDefOf.PositiveEvent, level >= 50 ? Importance.Notable : Importance.Flavor);
                CouncilGameComponent.GiveThought(trainer, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_PW_LevelMilestone"));
                var tags = Tags(p).Concat(new[] { "level:" + level }).ToArray();
                comp.journals.WriteTagged(trainer, "Council_Journal_PW_Level", tags, null, p.Named("PET"), level.Named("LEVEL"));
                if (level >= 100) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_PW_Level".Translate(p.Named("PET"), trainer.Named("PAWN"), level.Named("LEVEL")).Resolve(), trainer));
            }
        }

        /// <summary>Its friendship has grown as far as it goes with most: its trainer and it may bond, the game's own way.</summary>
        private static void Friends(CouncilGameComponent comp, Pawn p)
        {
            var master = p.playerSettings?.Master;
            if (master == null || master.Dead || !People.HasInnerLife(master) || AnimalFacts.Bonded(p, master)) return;
            if (master.relations?.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && AnimalFacts.Ours(r.otherPawn)) == true) return;
            if (!RelationsUtility.TryDevelopBondRelation(master, p, 1f)) return;
            using (Causes.Push("CC_PW_Cause_Bond".Translate(p.Named("PET")).Resolve()))
            {
                News.Post("CC_PW_Bonded".Translate(p.Named("PET"), master.Named("PAWN")).Resolve(), new LookTargets(p, master), NewsKind.People,
                    MessageTypeDefOf.PositiveEvent, Importance.Notable);
                comp.journals.WriteTagged(master, "Council_Journal_PW_Bonded", Tags(p), null, p.Named("PET"));
            }
        }

        /// <summary>Gone down in a fight: its trainer will be beside it as soon as it's over.</summary>
        private static void Fainted(Pawn p)
        {
            var trainer = TrainerOf(p);
            if (trainer == null || fainted.ContainsKey(p)) return;
            fainted[p] = trainer;
            using (Causes.Push("CC_PW_Cause_Fainted".Translate(p.Named("PET")).Resolve()))
                CouncilGameComponent.GiveThought(trainer, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_PW_Fainted"));
        }

        /// <summary>
        /// Once nothing threatens the map any more: each Pokémon that fainted and lived gets a line in its trainer's
        /// journal (one that died is mourned like any pet, by the pets' own farewell).
        /// </summary>
        private static void AfterFight(CouncilGameComponent comp)
        {
            if (fainted.Count == 0) return;
            foreach (var kv in fainted.ToList())
            {
                var p = kv.Key;
                var trainer = kv.Value;
                if (p == null || trainer == null || p.Dead || trainer.Dead)
                {
                    fainted.Remove(p);
                    continue;
                }
                var map = p.MapHeld;
                if (map != null && (MapFacts.ThreatOn(map) || (comp.social?.battleActive ?? false))) continue;
                fainted.Remove(p);
                comp.journals.WriteTagged(trainer, "Council_Journal_PW_Fainted", Tags(p), null, p.Named("PET"));
            }
        }

        private static string[] Tags(Pawn p)
        {
            var t = new List<string> { "pokemon" };
            if (Shiny(p)) t.Add("shiny");
            if (p.Name != null && !p.Name.Numerical) t.Add("named");
            return t.ToArray();
        }

        /// <summary>A line the trainer says to their Pokémon, if they're in view (the ambient rate every such line shares).</summary>
        private static void Say(Pawn trainer, string pool, Pawn p, string oldKind, string newKind)
        {
            if (!Speech.On || !trainer.Spawned || trainer.Map != Find.CurrentMap) return;
            string line = Interplay.Line(pool, trainer, Tags(p), p.Named("PET"), oldKind.Named("OLD"), newKind.Named("NEW"));
            if (line.NullOrEmpty()) return;
            // apart: said to the Pokémon, not to another colonist.
            Speech.Say(trainer, line, null, new Color(0.95f, 0.92f, 0.75f));
        }

        // ===================== A rare one on the colony's land =====================

        private static Pawn rareOne;
        private static string rareName;

        /// <summary>The rare Pokémon a sighting brought (HappeningWorker_PokemonSighting), watched until it's caught or gone.</summary>
        public static void Notify_RareOne(Pawn p)
        {
            rareOne = p;
            rareName = p != null ? AnimalFacts.KindLabel(p) : null;
        }

        private static void RareOneWatch(CouncilGameComponent comp)
        {
            var p = rareOne;
            if (p == null) return;
            if (p.Dead || p.Discarded)
            {
                rareOne = null;
                return;
            }
            if (p.Faction == Faction.OfPlayer)
            {
                rareOne = null;
                var who = TrainerOf(p);
                string text = who != null
                    ? "CC_PW_RareCaught".Translate(rareName.Named("SPECIES"), who.Named("PAWN")).Resolve()
                    : "CC_PW_RareCaughtNobody".Translate(rareName.Named("SPECIES")).Resolve();
                News.Post(text, new LookTargets(p), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, who));
                if (who != null) comp.journals.Write(who, "Council_Journal_PW_RareCaught", null, rareName.Named("SPECIES"));
                return;
            }
            if (!p.Spawned)
            {
                rareOne = null;
                News.Post("CC_PW_RareGone".Translate(rareName.Named("SPECIES")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
            }
        }
    }

    // ===================== Wishes =====================

    /// <summary>
    /// PokéWorld: a colonist with no Pokémon of their own wants one: one of the colony's that has no trainer yet (on the
    /// map or in the PC) to be theirs, else a wild one caught for them while the colony has a ball belt or can make one.
    /// Promised, a Pokémon at home without a trainer is given to them at once.
    /// </summary>
    public class WishWorker_OwnPokemon : WishWorker
    {
        private static bool HasOne(Pawn p) => AnimalFacts.AnimalsOf(p).Any(PokeWorldLink.IsPokemon);

        private static Pawn Unclaimed(Map map) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => PokeWorldLink.IsPokemon(a) && a.playerSettings?.Master == null && !AnimalFacts.Young(a)
                                                        && (a.relations == null || !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)))
                .OrderByDescending(a => PokeWorldLife.Level(a)).FirstOrDefault();

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!PokeWorldLink.Active) yield break;
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Baby() || HasOne(pawn) || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) yield break;
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            bool fond = (animals != null && People.Passionate(animals)) || PeopleTags.Has(pawn, "animallover") || pawn.DevelopmentalStage.Child()
                        || MoreHabits.Feeling(pawn, "Loneliness") != null;
            if (!fond) yield break;
            var free = Unclaimed(map);
            Pawn example = free;
            if (example == null)
            {
                if (!PokeWorldLink.CanCatch(map)) yield break;
                example = PokeWorldLink.WildPokemon(map).RandomElementWithFallback();
                if (example == null) yield break;
            }
            var c = Candidate(free != null ? 1.1f : 0.6f, (free != null ? "CC_PW_Reason_OwnFree" : "CC_PW_Reason_OwnWild").Translate(pawn.Named("PAWN"), AnimalFacts.KindLabel(example).Named("SPECIES")));
            c.subjectLabel = AnimalFacts.KindLabel(example);
            if (free != null) c.other = free;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasOne(wish.Beneficiary);

        public override bool StillValid(Wish wish) => PokeWorldLink.Active;

        /// <summary>Promised: the Pokémon at home without a trainer is theirs now.</summary>
        public override string OnPromised(Wish wish)
        {
            var p = wish.Beneficiary;
            var mon = wish.other;
            if (p == null || mon == null || !AnimalFacts.Ours(mon) || mon.playerSettings == null || mon.playerSettings.Master != null || mon.MapHeld != p.MapHeld) return null;
            try
            {
                mon.playerSettings.Master = p;
                return "CC_PW_Promise_Own";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: giving a Pokémon a trainer", e);
                return null;
            }
        }

        public override void OnGranted(Wish wish) => wish.other = AnimalFacts.AnimalsOf(wish.Beneficiary).FirstOrDefault(PokeWorldLink.IsPokemon) ?? wish.other;

        public override KeptCheck Holds(Wish wish) => PetKept(wish.other);

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "pet";
    }

    /// <summary>
    /// PokéWorld: a trainer, or someone with a love of animals, wants the colony to keep more ball belts on hand: there are
    /// wild Pokémon about, the colony has at most one belt, and one can be made now. Fulfilled when two more are there.
    /// </summary>
    public class WishWorker_BallSupply : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!PokeWorldLink.Active) yield break;
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile()) yield break;
            bool trainer = PeopleTags.Has(pawn, "trainer");
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            if (!trainer && (animals == null || animals.TotallyDisabled || !People.Passionate(animals))) yield break;
            int belts = PokeWorldLink.BeltCount(map);
            if (belts > 1 || !PokeWorldLink.WildPokemon(map).Any()) yield break;
            var belt = PokeWorldLink.MakeableBelt(map);
            if (belt == null) yield break;
            var c = Candidate(trainer ? 1f : 0.6f, "CC_PW_Reason_Balls".Translate(pawn.Named("PAWN")));
            c.thingDef = belt;
            c.baseline = belts;
            c.targetValue = belts + 2;
            c.subjectLabel = belt.label;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => PokeWorldLink.BeltCount(wish.Beneficiary?.MapHeld) >= wish.targetValue;

        public override bool StillValid(Wish wish) => PokeWorldLink.Active;

        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && PokeWorldLink.MakeableBelt(wish.Beneficiary?.MapHeld) != null;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Count".Translate(PokeWorldLink.BeltCount(wish.Beneficiary?.MapHeld), wish.targetValue.ToString("0"));
    }

    // ===================== Life goals and the council's project =====================

    /// <summary>"Catch them all", as far as one life goes: this many more species in PokéWorld's Pokédex than when they set out.</summary>
    public class AspirationWorker_Pokedex : AspirationWorker
    {
        public override bool CanHave(Pawn p)
        {
            if (!PokeWorldLink.Active || p.MapHeld == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return false;
            int caught = PokeWorldLife.Caught();
            if (caught < 0 || caught >= PokeWorldLink.SpeciesCount) return false;
            return PeopleTags.Has(p, "trainer") || People.Passionate(p.skills?.GetSkill(SkillDefOf.Animals));
        }

        public override bool Init(Aspiration a)
        {
            int caught = PokeWorldLife.Caught();
            if (caught < 0) return false;
            a.baseline = caught;
            a.target = AnimalMath.DexGoal(caught, PokeWorldLink.SpeciesCount, (int)def.amount);
            return a.target > a.baseline;
        }

        private static float Now(Aspiration a)
        {
            int caught = PokeWorldLife.Caught();
            return caught < 0 ? a.baseline : caught;
        }

        public override float Progress(Aspiration a) => (Now(a) - a.baseline) / Mathf.Max(1f, a.target - a.baseline);

        public override bool StillPossible(Aspiration a) => PokeWorldLink.Active && PokeWorldLife.Caught() >= 0;

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate((Now(a) - a.baseline).ToString("0"), (a.target - a.baseline).ToString("0"));
    }

    /// <summary>Raise a Pokémon of their own to a level (50, or a good way past where their best one is now, at most 100).</summary>
    public class AspirationWorker_PokemonLevel : AspirationWorker
    {
        private static int Best(Pawn p) => AnimalFacts.AnimalsOf(p).Where(PokeWorldLink.IsPokemon).Select(PokeWorldLife.Level).DefaultIfEmpty(0).Max();

        public override bool CanHave(Pawn p) => PokeWorldLink.Active && PeopleTags.Has(p, "trainer") && Best(p) > 0 && Best(p) < 90;

        public override bool Init(Aspiration a)
        {
            int best = Best(a.pawn);
            if (best <= 0) return false;
            a.baseline = best;
            a.target = Mathf.Min(100, Mathf.Max(def.amount, best + 15));
            return true;
        }

        public override float Progress(Aspiration a) => (Best(a.pawn) - a.baseline) / Mathf.Max(1f, a.target - a.baseline);

        public override bool Done(Aspiration a) => Best(a.pawn) >= a.target;

        public override bool StillPossible(Aspiration a) => PokeWorldLink.Active && PeopleTags.Has(a.pawn, "trainer");

        public override string ProgressText(Aspiration a) => "CC_PW_Asp_Level".Translate(Best(a.pawn), a.target.ToString("0"));
    }

    /// <summary>The council's project: this many more species in the Pokédex.</summary>
    public class Goal_Pokedex : AmbitionGoal
    {
        public int count = 5;

        public override bool Possible(Map map) => PokeWorldLink.Active && PokeWorldLife.Caught() >= 0 && PokeWorldLink.CanCatch(map) && PokeWorldLink.WildPokemon(map).Any();

        public override float Baseline(Map map) => Mathf.Max(0, PokeWorldLife.Caught());

        private static int Now(float baseline) => Mathf.Max((int)baseline, PokeWorldLife.Caught());

        public override float Progress(Map map, float baseline) => Ratio(Now(baseline) - baseline, count);

        public override string Describe(Map map, float baseline) => "CC_PW_Goal_Pokedex".Translate(count, Mathf.Max(0, Now(baseline) - (int)baseline));
    }

    // ===================== A rare sighting =====================

    /// <summary>
    /// PokéWorld: a rare Pokémon (PokéWorld's own rarity 6 to 9, never a legendary or a fossil, of a generation switched
    /// on) has been seen on the colony's land, and will move on in a couple of days. Ones the colony hasn't caught come
    /// first. The colony may go and look at it, send its best trainer after it, or let it pass; catching it is the
    /// player's own (a ball belt), and is news when it happens.
    /// </summary>
    public class HappeningWorker_PokemonSighting : IncidentWorker_Happening
    {
        private static IEnumerable<PawnKindDef> Kinds(Map map) =>
            PokeWorldLink.Roaming().Where(PokeWorldLink.Rare).Select(r => r.race?.AnyPawnKind ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == r))
                .Where(k => k != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PokeWorldLink.Active && PokeWorldLink.PokemonShare() > 0f && Kinds(map).Any() && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var owned = PokeWorldLink.OwnedSpecies(ctx.map);
            var kind = Kinds(ctx.map).RandomElementByWeightWithFallback(k => owned.Contains(k.race) ? 0.3f : 1f);
            if (kind == null) return false;
            var mon = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            ctx.stranger = mon;
            ctx.thing = kind.race;
            var people = Colonists(ctx.map);
            ctx.a = people.Where(p => PeopleTags.Has(p, "trainer")).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault()
                    ?? people.Where(p => !p.DevelopmentalStage.Baby()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var mon = ctx.stranger;
            if (mon == null || !RCellFinder.TryFindRandomPawnEntryCell(out var cell, ctx.map, CellFinder.EdgeRoadChance_Animal, false, null)) return false;
            GenSpawn.Spawn(mon, cell, ctx.map);
            mon.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Mathf.RoundToInt(ext.days.RandomInRange * GenDate.TicksPerDay);
            PokeWorldLife.Notify_RareOne(mon);
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.stranger != null && !ctx.stranger.Spawned && !ctx.stranger.Discarded) Find.WorldPawns.PassToWorld(ctx.stranger, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.stranger != null && PokeWorldLink.OwnedSpecies(ctx.map).Contains(ctx.stranger.def)) tags.Add("known");
            else tags.Add("new");
            if (ctx.a != null && PeopleTags.Has(ctx.a, "trainer")) tags.Add("trainer");
            if (ctx.stranger != null && PokeWorldLife.Shiny(ctx.stranger)) tags.Add("shiny");
        }
    }
}
