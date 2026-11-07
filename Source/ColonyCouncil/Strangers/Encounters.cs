using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small moment between a colonist (A) and someone or something that isn't one of us (X): news from a visitor,
    /// haggling with a trader, words through a prisoner's door, a fallen raider's plea, a pet's mischief, a stag at
    /// dawn. Effects reuse the dilemma effects (target X for the stranger); {A_...}, {X_...} and {FACTION} in texts.
    /// </summary>
    public class EncounterDef : Def
    {
        public StrangerRole stranger;
        /// <summary>Who the colonist must be. Without one: any adult (children too, with <see cref="children"/>).</summary>
        public DilemmaRole colonist;
        public bool children;
        public float weight = 1f;
        public float cooldownDays = 5f;
        public List<Season> seasons;
        public bool nightOnly;
        public bool dayOnly;
        /// <summary>Only on these lands, any of them (see <see cref="Lands"/>: desert, tundra, boreal, jungle, swamp, forest,
        /// grassland, mountains, hills, coast, lake, river, valley, canyon, island, caves, cliffs, oasis, crater). Empty: anywhere.</summary>
        public List<string> lands;
        /// <summary>
        /// Only when the moment fits, as A lives it: a line's tags without the brackets ("rain|snow", "storm,outside",
        /// "clear,night"). See <see cref="Flavor"/> and <see cref="LetterTags"/>.
        /// </summary>
        public string moment;
        [MustTranslate] public List<string> texts = new List<string>();
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>A flavor pool for A's journal (lines may use {X_...}).</summary>
        public string journal;
        /// <summary>Worth a message at the "notable" level and a chronicle line.</summary>
        public bool notable;
        /// <summary>Only for a colony known this way (see DilemmaDef.colonyIs).</summary>
        public List<string> colonyIs;
        public List<string> colonyIsNot;
        /// <summary>How this moment shapes the colony's character (small: it's one colonist's doing).</summary>
        public WayShift ways;
        // ---- Other mods (see Integrations/ModStrangers.cs) ----
        /// <summary>Only when one of these mods or DLCs is active (package ids).</summary>
        public List<string> mods;
        /// <summary>Only when the colony has one of these (ThingDef names: a building it built, or goods in its stores).</summary>
        public List<string> colonyHas;
        /// <summary>Only on one of these biomes (BiomeDef names).</summary>
        public List<string> biomes;
        /// <summary>
        /// Only while one of these is over the map or the whole world (GameConditionDef names: Aurora, Eclipse). A name the
        /// game doesn't have (a DLC or mod that isn't loaded) is never on, so the rare skies stay rare.
        /// </summary>
        public List<string> conditions;
        /// <summary>
        /// Only when A's ideoligion holds one of these precepts (PreceptDef names, Ideology): a kind word to a slave where
        /// slavery is frowned on, a hard one where it's honourable. A name the game doesn't have never fits.
        /// </summary>
        public List<string> precepts;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (stranger == null || stranger.kind == StrangerKind.None) yield return "encounter needs a stranger kind";
            if (texts.NullOrEmpty()) yield return "encounter has no texts";
        }
    }

    /// <summary>
    /// One home's encounters (stage 0, step 7b): when the last came, how many today, when one fell due, and each kind's
    /// own cooldown. One per colony, kept by the council's rules (ColonyBook); an old save's are the seat's.
    /// </summary>
    public class EncounterHome : IExposable, IColonyEntry
    {
        /// <summary>The colony's key (<see cref="Homes.KeyOf"/>), or none while it has no home (an old save's, not yet placed).</summary>
        public int home = HomesMath.None;
        /// <summary>When its home stopped being ours; -1 while it is.</summary>
        public int lostAt = ColonyStateMath.NotLost;

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        public Dictionary<string, int> lastTick = new Dictionary<string, int>();
        public int lastFired = -999999;
        public int day = -1;
        public int today;
        [Unsaved] public int dueSince = -1;

        /// <summary>Saved under the names the encounters always used, so an old save's are read straight into the seat's.</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
            Scribe_Collections.Look(ref lastTick, "lastTick", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastFired, "lastFired", -999999);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref today, "today");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastTick == null) lastTick = new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Strangers met on the map: each home on its own (EncounterHome), a few a day at most, after a stranger is present
    /// (a visitor, a trader, a lodger, a prisoner, a pet, a wild animal...), sometimes one of the council's questions instead.
    /// </summary>
    public class EncounterTracker : IExposable
    {
        /// <summary>One per colony; an old save's are the seat's.</summary>
        private List<EncounterHome> homes = new List<EncounterHome>();
        /// <summary>While an old save loads: its one record, read from where it always was, for the seat.</summary>
        [Unsaved] private EncounterHome oldSave;
        public NuisanceTracker nuisances = new NuisanceTracker();
        private const int DueHours = 6;

        /// <summary>A home's encounters (the seat's for a map that isn't a home).</summary>
        private EncounterHome HomeOf(Map map)
        {
            homes ??= new List<EncounterHome>();
            return ColonyBook.Entry(homes, map, () => new EncounterHome());
        }

        /// <summary>Homes lost (Homes.Changed): each one's encounters kept a while, as the council's.</summary>
        internal void HomesLost(IReadOnlyList<int> lost) => ColonyBook.Lost(homes, lost, null);

        /// <summary>Once a day: encounters of homes lost long enough ago are let go.</summary>
        internal void Daily() => ColonyBook.Daily(homes, null);

        private static readonly Dictionary<StrangerKind, float> HourlyChance = new Dictionary<StrangerKind, float>
        {
            { StrangerKind.Visitor, 0.10f },
            { StrangerKind.Trader, 0.10f },
            { StrangerKind.Lodger, 0.05f },
            { StrangerKind.Prisoner, 0.03f },
            { StrangerKind.Fallen, 0.25f },
            { StrangerKind.Pet, 0.02f },
            { StrangerKind.Wild, 0.015f },
            { StrangerKind.Mech, 0.015f },
            { StrangerKind.Slave, 0.025f },
            { StrangerKind.Entity, 0.015f }
        };

        /// <summary>
        /// How likely, each hour, that someone of this kind brings a question to the council. Visitors and traders ask
        /// about one stay in three or four (Tools/Balance: questions stay near one every two days).
        /// </summary>
        private static readonly Dictionary<StrangerKind, float> DilemmaChance = new Dictionary<StrangerKind, float>
        {
            { StrangerKind.Visitor, 0.02f },
            { StrangerKind.Trader, 0.02f },
            { StrangerKind.Lodger, 0.02f },
            { StrangerKind.Prisoner, 0.004f },
            { StrangerKind.Fallen, 0.08f },
            { StrangerKind.Pet, 0.002f },
            { StrangerKind.Wild, 0.002f }
        };

        private const int MaxPerDay = 3;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public void Hourly(CouncilGameComponent comp)
        {
            try
            {
                nuisances.Hourly();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nuisances", e);
            }
            if (!S.strangers) return;
            int now = Find.TickManager.TicksGame;
            if (now < GenDate.TicksPerDay * 2) return;
            // Each home on its own, in the order they were founded.
            foreach (var map in Homes.All)
            {
                try
                {
                    HourAt(comp, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("encounters", e);
                }
            }
        }

        private void HourAt(CouncilGameComponent comp, Map map, int now)
        {
            var h = HomeOf(map);
            if (now / GenDate.TicksPerDay != h.day)
            {
                h.day = now / GenDate.TicksPerDay;
                h.today = 0;
            }
            if (h.today >= Mathf.CeilToInt(MaxPerDay * Mathf.Max(1f, S.encounterFrequency)) || now - h.lastFired < GenDate.TicksPerHour * 3) return;
            if (map == null || comp.social.BattleOn(map) || MapFacts.ThreatOn(map)) return;
            var present = Strangers.Present(map);
            if (present.Count == 0) return;
            // Strangers only pass through, so their questions can't wait for the council's usual rhythm: asked of this colony.
            var council = DilemmaTracker.For(map) ?? comp.dilemmas;
            if (S.enableDilemmas && now - council.lastFiredTick > GenDate.TicksPerDay * 3)
            {
                float quiet = 1f;
                foreach (var k in present)
                    if (DilemmaChance.TryGetValue(k, out float c)) quiet *= 1f - c;
                if (Rand.Chance(1f - quiet)
                    && council.TryFire(false, d => d.Worker is DilemmaWorker_Stranger && d.stranger != null && present.Contains(d.stranger.kind)))
                    return;
            }
            if (h.dueSince >= 0 && now - h.dueSince >= GenDate.TicksPerHour * DueHours) h.dueSince = -1;
            if (h.dueSince < 0)
            {
                float none = 1f;
                float freq = Mathf.Clamp(S.encounterFrequency, 0.1f, 4f);
                foreach (var k in present)
                    if (HourlyChance.TryGetValue(k, out float c)) none *= 1f - Mathf.Min(0.9f, c * freq);
                if (!Rand.Chance(1f - none)) return;
                h.dueSince = now;
            }
            if (TryOne(comp, map, present)) h.dueSince = -1;
        }

        public bool TryOne(CouncilGameComponent comp, Map map, HashSet<StrangerKind> present, Func<EncounterDef, bool> only = null)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && !p.DevelopmentalStage.Baby())
                .ToList();
            if (colonists.Count == 0) return false;
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            bool night = hour >= 21 || hour < 5;
            var season = GenLocalDate.Season(map);
            // Each kind's cooldown at this home.
            var h = HomeOf(map);
            var lastTick = h.lastTick;
            var pool = DefDatabase<EncounterDef>.AllDefsListForReading.Where(d =>
                d.stranger != null && present.Contains(d.stranger.kind)
                && (only == null || only(d))
                && (!lastTick.TryGetValue(d.defName, out int t) || now - t > d.cooldownDays * GenDate.TicksPerDay)
                && (d.seasons.NullOrEmpty() || d.seasons.Contains(season))
                && (!d.nightOnly || night) && (!d.dayOnly || !night)
                && Lands.Fits(map, d.lands)
                && ConditionOn(map, d.conditions)
                && ModContent.Fits(d, map)
                && ((d.colonyIs.NullOrEmpty() && d.colonyIsNot.NullOrEmpty()) || (CharacterTracker.For(map)?.Fits(d.colonyIs, d.colonyIsNot) ?? false))).ToList();
            foreach (var def in pool.InRandomOrder().OrderByDescending(d => d.weight * Rand.Value))
            {
                Pawn a = null, x = null;
                try
                {
                    if (!Cast(def, map, colonists, out a, out x)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("encounter " + def.defName, e);
                    continue;
                }
                Fire(def, comp, map, a, x);
                lastTick[def.defName] = now;
                h.lastFired = now;
                h.today++;
                return true;
            }
            return false;
        }

        /// <summary>One of these game conditions over the map or the world right now (none given: always).</summary>
        public static bool ConditionOn(Map map, List<string> names)
        {
            if (names.NullOrEmpty()) return true;
            try
            {
                foreach (var list in new[] { map?.gameConditionManager?.ActiveConditions, Find.World?.gameConditionManager?.ActiveConditions })
                {
                    if (list == null) continue;
                    foreach (var c in list)
                        if (c?.def != null && names.Contains(c.def.defName))
                            return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("encounter conditions", e);
            }
            return false;
        }

        private static bool Cast(EncounterDef def, Map map, List<Pawn> colonists, out Pawn a, out Pawn x)
        {
            a = x = null;
            var strangers = Strangers.On(map, def.stranger.kind)
                .Where(s => !s.RaceProps.Humanlike || def.stranger.kind == StrangerKind.Fallen || def.stranger.kind == StrangerKind.Entity || s.Awake()).ToList();
            if (strangers.Count == 0) return false;
            foreach (var p in colonists.InRandomOrder())
            {
                if (def.colonist != null ? !def.colonist.Allows(p, null) : !def.children && p.DevelopmentalStage.Juvenile()) continue;
                if (!def.precepts.NullOrEmpty() && !def.precepts.Any(n => Beliefs.HasPrecept(p, n))) continue;
                // Only a stranger they are really with: within talking range for people, in sight not far off for animals.
                var met = strangers.Where(x => Meets(p, x)).ToList();
                if (met.Count == 0) continue;
                var s = Strangers.Pick(map, def.stranger, p, met);
                if (s == null) continue;
                if (!def.moment.NullOrEmpty() && Flavor.Match("[" + def.moment + "]", LetterTags.For(new DilemmaContext { map = map, a = p, stranger = s })) < 0) continue;
                a = p;
                x = s;
                return true;
            }
            return false;
        }

        /// <summary>
        /// A colonist and a stranger together: people within talking range (SocialLog.Near), animals and mechs in sight a
        /// little way off, an entity on its holding platform in sight close by.
        /// </summary>
        private static bool Meets(Pawn p, Pawn x)
        {
            if (Together.Able(p) && x != null && !x.Spawned && x.ParentHolder is Thing holder && holder.Spawned && holder.Map == p.Map)
                return p.Position.InHorDistOf(holder.Position, 10f) && (p.GetRoom() == holder.GetRoom() || GenSight.LineOfSight(p.Position, holder.Position, p.Map, true));
            if (!Together.Able(p) || x == null || !x.Spawned || x.Map != p.Map) return false;
            return x.RaceProps != null && x.RaceProps.Humanlike ? SocialLog.Near(p, x) : Together.Hears(p, x, 16f);
        }

        private static void Fire(EncounterDef def, CouncilGameComponent comp, Map map, Pawn a, Pawn x)
        {
            // together: Cast picks a colonist who is with the stranger (Meets).
            var ctx = new DilemmaContext { map = map, a = a, stranger = x, faction = Strangers.FactionOf(x), why = def.label };
            // Tagged lines ([rain], [winter], [trait:Kind], [x:child]) only when they fit the moment and the people.
            string text = ctx.Format(Flavor.Pick(def.texts, LetterTags.For(ctx)) ?? Flavor.PickText(def.texts));
            ctx.cause = text;
            foreach (var e in def.effects)
            {
                try
                {
                    e.Apply(ctx);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("encounter effect " + def.defName, ex);
                }
            }
            CharacterTracker.For(map)?.Shift(def.ways, text);
            Deeds.Encounter(def, a, x);
            // An entity is met where its holding platform stands.
            News.Post(text, new LookTargets(a, x.Spawned ? x : x.SpawnedParentOrMe), NewsKind.World, MessageTypeDefOf.SilentInput, def.notable ? Importance.Notable : Importance.Flavor);
            // Into the social log too, where Interaction Bubbles picks it up (Integrations/SocialLog.cs).
            SocialLog.Add(a, x, text);
            if (def.notable) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, a));
            if (!def.journal.NullOrEmpty()) comp.journals.Write(a, def.journal, null, x.Named("X"));
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) homes?.RemoveAll(h => h == null);
            Scribe_Collections.Look(ref homes, "homes", LookMode.Deep);
            // A save from before there were encounters per home: what it kept, where it always was, is the seat's.
            if (Scribe.mode == LoadSaveMode.LoadingVars && homes == null)
            {
                oldSave = new EncounterHome();
                homes = new List<EncounterHome> { oldSave };
            }
            if (oldSave != null && Scribe.mode != LoadSaveMode.Saving) oldSave.ExposeData();
            Scribe_Deep.Look(ref nuisances, "nuisances");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                oldSave = null;
                homes ??= new List<EncounterHome>();
                homes.RemoveAll(h => h == null);
                if (nuisances == null) nuisances = new NuisanceTracker();
            }
        }
    }
}
