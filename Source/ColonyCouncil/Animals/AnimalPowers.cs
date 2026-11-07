using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's animals with powers, the player's pick "magic and powers" of 7 November 2026, in four parts that share
    /// one pass (Sched.RarePowers, every 250 ticks; its hourly look on the pass at tick 227 of the hour, its daily one in
    /// the first hour of the day) and one saved book (<see cref="AnimalPowersBook"/>):
    /// <list type="bullet">
    /// <item>PokéWorld: the colony's own Pokémon do a little at home by their type (Integrations/PokeWorldTypes.cs).</item>
    /// <item>A RimWorld of Magic: a mage's familiar (Integrations/MagicFamiliars.cs).</item>
    /// <item>Omen animals: one born under a sign (Animals/OmenAnimals.cs).</item>
    /// <item>Ideology: the animals the colony's faith holds sacred (Animals/SacredAnimals.cs).</item>
    /// </list>
    /// The numbers are in Animals/AnimalPowersMath.cs. Without the mods, or with every part off, the pass returns at once.
    /// </summary>
    public static class AnimalPowers
    {
        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        /// <summary>Any part could do something in this game.</summary>
        public static bool AnyOn => PokeWorldTypes.On || MagicFamiliars.On || OmenAnimals.On || SacredAnimals.On;

        /// <summary>Every 250 ticks, at Sched.RarePowers.</summary>
        public static void Pass(AnimalPowersBook book, int now)
        {
            bool hour = Sched.Hourly(now, Sched.RarePowers);
            if (hour)
            {
                Safe("animal powers: the Pokémon types' hour", () => PokeWorldTypes.Hourly(now));
                Safe("animal powers: familiars' hour", () => MagicFamiliars.Hourly(book, now));
                Safe("animal powers: omens' hour", () => OmenAnimals.Hourly(book, now));
                Safe("animal powers: sacred animals' hour", () => SacredAnimals.Hourly(now));
                Safe("animal powers: cooldowns", () => book.PruneCalm(now));
                if (Sched.FirstHourOfDay(now)) Safe("animal powers: familiars' day", () => MagicFamiliars.Daily(book, now));
            }
            Safe("animal powers: the Pokémon types", () => PokeWorldTypes.Rare(book, now));
            Safe("animal powers: familiars", () => MagicFamiliars.Rare(book, now));
        }

        /// <summary>A faint glow (the game's lightning glow, tinted and dimmed) at a spot on the map: a familiar's, a Pokémon at work.</summary>
        public static void Glow(Map map, Vector3 at, Color color, float scale, float solid = 0.4f)
        {
            if (map == null) return;
            try
            {
                var data = FleckMaker.GetDataStatic(at, map, FleckDefOf.LightningGlow, scale);
                data.instanceColor = color;
                data.solidTimeOverride = solid;
                map.flecks.CreateFleck(data);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal powers: a glow", e);
            }
        }

        /// <summary>In view on the map being looked at: only then is a glow made.</summary>
        public static bool InView(Map map, IntVec3 cell)
        {
            if (map == null || map != Find.CurrentMap || !cell.IsValid) return false;
            try
            {
                return Find.CameraDriver.CurrentViewRect.ExpandedBy(2).Contains(cell);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>A mage and the animal bound to them as a familiar (Integrations/MagicFamiliars.cs).</summary>
    public class FamiliarBond : IExposable
    {
        public Pawn mage;
        public Pawn animal;
        public int since = -1;
        /// <summary>Its name and kind when bound, for the words when it's gone.</summary>
        public string name;
        public string kind;

        public void ExposeData()
        {
            Scribe_References.Look(ref mage, "mage");
            Scribe_References.Look(ref animal, "animal");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref kind, "kind");
        }
    }

    /// <summary>A binding waiting to be played out: the mage goes to the animal and stays with it a while.</summary>
    public class FamiliarBinding : IExposable
    {
        public Pawn mage;
        public Pawn animal;
        public int since = -1;
        /// <summary>The council's answer to the mage's wish; otherwise the mage's own choice.</summary>
        public bool byWish;
        /// <summary>Times it began and was broken off.</summary>
        public int tries;

        public void ExposeData()
        {
            Scribe_References.Look(ref mage, "mage");
            Scribe_References.Look(ref animal, "animal");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref byWish, "byWish");
            Scribe_Values.Look(ref tries, "tries");
        }
    }

    /// <summary>An omen animal of the colony, while it lives (Animals/OmenAnimals.cs).</summary>
    public class OmenRecord : IExposable
    {
        public Pawn animal;
        public OmenSign sign;
        /// <summary>The day it was born on, for a kept day's omen ("the founding day").</summary>
        public string about;
        public int born = -1;
        public string name;
        public string kind;
        /// <summary>The home it was born to (Homes.KeyOf).</summary>
        public int home = HomesMath.None;

        public void ExposeData()
        {
            Scribe_References.Look(ref animal, "animal");
            Scribe_Values.Look(ref sign, "sign");
            Scribe_Values.Look(ref about, "about");
            Scribe_Values.Look(ref born, "born", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
        }
    }

    /// <summary>
    /// What the colony's animals with powers keep between loads: the familiars and the bindings waiting, the omen animals
    /// alive, when the last omen came, and the Psychic types' cooldowns (by ThingID, a colonist calmed or a Pokémon that
    /// calmed someone). An existing save gets an empty book. Each list is capped (AnimalPowersMath).
    /// </summary>
    public class AnimalPowersBook : GameComponent
    {
        public List<FamiliarBond> familiars = new List<FamiliarBond>();
        public List<FamiliarBinding> bindings = new List<FamiliarBinding>();
        public List<OmenRecord> omens = new List<OmenRecord>();
        public int lastOmenTick = -1;
        /// <summary>When someone was last calmed by a Psychic type, and when each Psychic type last calmed someone.</summary>
        public Dictionary<string, int> calmTicks = new Dictionary<string, int>();

        private static AnimalPowersBook cached;
        private static Game cachedGame;

        public AnimalPowersBook(Game game)
        {
        }

        public static AnimalPowersBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<AnimalPowersBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (!Sched.Every(t, Sched.RareTicks, Sched.RarePowers) || !AnimalPowers.AnyOn) return;
            using var clock = PassClock.Time(Pass.RarePowers);
            AnimalPowers.Pass(this, t);
        }

        // ---------------- Familiars ----------------

        public FamiliarBond FamiliarOf(Pawn mage) => mage == null ? null : familiars.FirstOrDefault(f => f.mage == mage);

        public FamiliarBond BondOfAnimal(Pawn animal) => animal == null ? null : familiars.FirstOrDefault(f => f.animal == animal);

        public FamiliarBinding BindingOf(Pawn mage) => mage == null ? null : bindings.FirstOrDefault(b => b.mage == mage);

        public void AddBinding(FamiliarBinding b)
        {
            if (b?.mage == null || b.animal == null) return;
            bindings.RemoveAll(x => x.mage == b.mage || x.animal == b.animal);
            bindings.Add(b);
            Caps.TrimOldest(bindings, AnimalPowersMath.MaxBindings);
        }

        public void AddFamiliar(FamiliarBond f)
        {
            if (f?.mage == null || f.animal == null) return;
            familiars.RemoveAll(x => x.mage == f.mage || x.animal == f.animal);
            familiars.Add(f);
            Caps.TrimOldest(familiars, AnimalPowersMath.MaxFamiliars);
        }

        // ---------------- Omens ----------------

        public OmenRecord OmenOf(Pawn animal) => animal == null ? null : omens.FirstOrDefault(o => o.animal == animal);

        public int LivingOmens(int home) => omens.Count(o => o.home == home && o.animal != null && !o.animal.Dead);

        public void AddOmen(OmenRecord o, int now)
        {
            if (o?.animal == null) return;
            omens.RemoveAll(x => x.animal == o.animal);
            omens.Add(o);
            lastOmenTick = now;
            Caps.TrimOldest(omens, AnimalPowersMath.MaxOmenRecords);
        }

        // ---------------- Calming ----------------

        public int CalmedAt(Thing t) => t != null && calmTicks.TryGetValue(t.ThingID, out int at) ? at : -1;

        public void NoteCalm(Thing person, Thing pokemon, int now)
        {
            if (person != null) calmTicks[person.ThingID] = now;
            if (pokemon != null) calmTicks[pokemon.ThingID] = now;
        }

        /// <summary>Cooldowns long past let go (once an hour).</summary>
        public void PruneCalm(int now)
        {
            if (calmTicks.Count == 0) return;
            foreach (var key in calmTicks.Where(kv => AnimalPowersMath.Ready(kv.Value, now, AnimalPowersMath.CalmPersonCooldown)).Select(kv => kv.Key).ToList())
                calmTicks.Remove(key);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                familiars.RemoveAll(f => f?.mage == null || f.animal == null || f.mage.Discarded || f.animal.Discarded);
                bindings.RemoveAll(b => b?.mage == null || b.animal == null || b.mage.Discarded || b.animal.Discarded);
                omens.RemoveAll(o => o?.animal == null || o.animal.Discarded);
            }
            Scribe_Collections.Look(ref familiars, "familiars", LookMode.Deep);
            Scribe_Collections.Look(ref bindings, "bindings", LookMode.Deep);
            Scribe_Collections.Look(ref omens, "omens", LookMode.Deep);
            Scribe_Values.Look(ref lastOmenTick, "lastOmenTick", -1);
            Scribe_Collections.Look(ref calmTicks, "calmTicks", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                familiars ??= new List<FamiliarBond>();
                bindings ??= new List<FamiliarBinding>();
                omens ??= new List<OmenRecord>();
                calmTicks ??= new Dictionary<string, int>();
                familiars.RemoveAll(f => f?.mage == null || f.animal == null);
                bindings.RemoveAll(b => b?.mage == null || b.animal == null);
                omens.RemoveAll(o => o?.animal == null);
            }
        }
    }
}
