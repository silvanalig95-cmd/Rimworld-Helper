using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
        [MustTranslate] public List<string> texts = new List<string>();
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>A flavor pool for A's journal (lines may use {X_...}).</summary>
        public string journal;
        /// <summary>Worth a message at the "notable" level and a chronicle line.</summary>
        public bool notable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (stranger == null || stranger.kind == StrangerKind.None) yield return "encounter needs a stranger kind";
            if (texts.NullOrEmpty()) yield return "encounter has no texts";
        }
    }

    public class EncounterTracker : IExposable
    {
        private Dictionary<string, int> lastTick = new Dictionary<string, int>();
        private int lastFired = -999999;
        private int day = -1;
        private int today;

        /// <summary>How likely an encounter is each hour while someone of this kind is around.</summary>
        private static readonly Dictionary<StrangerKind, float> HourlyChance = new Dictionary<StrangerKind, float>
        {
            { StrangerKind.Visitor, 0.10f },
            { StrangerKind.Trader, 0.10f },
            { StrangerKind.Lodger, 0.05f },
            { StrangerKind.Prisoner, 0.03f },
            { StrangerKind.Fallen, 0.25f },
            { StrangerKind.Pet, 0.02f },
            { StrangerKind.Wild, 0.015f }
        };

        /// <summary>How likely, each hour, that someone of this kind brings a question to the council.</summary>
        private static readonly Dictionary<StrangerKind, float> DilemmaChance = new Dictionary<StrangerKind, float>
        {
            { StrangerKind.Visitor, 0.03f },
            { StrangerKind.Trader, 0.03f },
            { StrangerKind.Lodger, 0.02f },
            { StrangerKind.Prisoner, 0.004f },
            { StrangerKind.Fallen, 0.08f },
            { StrangerKind.Pet, 0.002f },
            { StrangerKind.Wild, 0.003f }
        };

        private const int MaxPerDay = 3;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public void Hourly(CouncilGameComponent comp)
        {
            if (!S.strangers) return;
            int now = Find.TickManager.TicksGame;
            if (now < GenDate.TicksPerDay * 2) return;
            if (now / GenDate.TicksPerDay != day)
            {
                day = now / GenDate.TicksPerDay;
                today = 0;
            }
            if (today >= MaxPerDay || now - lastFired < GenDate.TicksPerHour * 3) return;
            var map = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (map == null || comp.social.battleActive || GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            var present = Strangers.Present(map);
            if (present.Count == 0) return;
            // Strangers only pass through, so their questions can't wait for the council's usual rhythm.
            if (S.enableDilemmas && now - comp.dilemmas.lastFiredTick > GenDate.TicksPerDay * 3)
            {
                float quiet = 1f;
                foreach (var k in present)
                    if (DilemmaChance.TryGetValue(k, out float c)) quiet *= 1f - c;
                if (Rand.Chance(1f - quiet)
                    && comp.dilemmas.TryFire(false, d => d.Worker is DilemmaWorker_Stranger && d.stranger != null && present.Contains(d.stranger.kind)))
                    return;
            }
            float none = 1f;
            foreach (var k in present)
                if (HourlyChance.TryGetValue(k, out float c)) none *= 1f - c;
            if (!Rand.Chance(1f - none)) return;
            TryOne(comp, map, present);
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
            var pool = DefDatabase<EncounterDef>.AllDefsListForReading.Where(d =>
                d.stranger != null && present.Contains(d.stranger.kind)
                && (only == null || only(d))
                && (!lastTick.TryGetValue(d.defName, out int t) || now - t > d.cooldownDays * GenDate.TicksPerDay)
                && (d.seasons.NullOrEmpty() || d.seasons.Contains(season))
                && (!d.nightOnly || night) && (!d.dayOnly || !night)).ToList();
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
                lastFired = now;
                today++;
                return true;
            }
            return false;
        }

        private static bool Cast(EncounterDef def, Map map, List<Pawn> colonists, out Pawn a, out Pawn x)
        {
            a = x = null;
            var strangers = Strangers.On(map, def.stranger.kind)
                .Where(s => !s.RaceProps.Humanlike || def.stranger.kind == StrangerKind.Fallen || s.Awake()).ToList();
            if (strangers.Count == 0) return false;
            foreach (var p in colonists.InRandomOrder())
            {
                if (def.colonist != null ? !def.colonist.Allows(p, null) : !def.children && p.DevelopmentalStage.Juvenile()) continue;
                var s = Strangers.Pick(map, def.stranger, p, strangers);
                if (s == null) continue;
                a = p;
                x = s;
                return true;
            }
            return false;
        }

        private static void Fire(EncounterDef def, CouncilGameComponent comp, Map map, Pawn a, Pawn x)
        {
            var ctx = new DilemmaContext { map = map, a = a, stranger = x, faction = Strangers.FactionOf(x), why = def.label };
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
            string text = ctx.Format(Flavor.PickText(def.texts));
            Notify.Message(text, new LookTargets(a, x), MessageTypeDefOf.SilentInput, def.notable ? Importance.Notable : Importance.Flavor);
            if (def.notable) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, a));
            if (!def.journal.NullOrEmpty()) comp.journals.Write(a, def.journal, null, x.Named("X"));
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref lastTick, "lastTick", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastFired, "lastFired", -999999);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref today, "today");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastTick == null) lastTick = new Dictionary<string, int>();
        }
    }
}
