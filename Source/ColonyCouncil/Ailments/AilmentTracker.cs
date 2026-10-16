using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Everyday ailments and the rare serious illnesses, in a game component of its own. Once an hour it looks at each
    /// colonist (their job, where they stand, the room they sleep in) and counts the day's hours of heavy work, hand
    /// work, cold work, sun and so on; once a day's hours are enough, an ailment that fits gets one roll. It also runs
    /// the catching ailments' clock (the game's own disease incidents, fired through the storyteller), the spread of
    /// the contagious serious ones between roommates, the letters, journal lines and thoughts, and the after-effects of
    /// getting better. With the "Everyday ailments" setting off, nothing new starts; illnesses already running finish
    /// their course.
    /// </summary>
    public class AilmentTracker : GameComponent
    {
        // ---------------- Saved ----------------

        /// <summary>"ThingID|defName": when they last came down with that one; "ThingID|any": with any.</summary>
        private Dictionary<string, int> last = new Dictionary<string, int>();
        /// <summary>ThingID: the day (game day index) they last ate fruit or vegetables.</summary>
        private Dictionary<string, int> freshDay = new Dictionary<string, int>();
        /// <summary>ThingID: the last day they were looked at here (a day's rolls happen once, even across a reload).</summary>
        private Dictionary<string, int> seenDay = new Dictionary<string, int>();
        /// <summary>ThingID: sweets eaten lately (fades by a sixth a day).</summary>
        private Dictionary<string, float> sweets = new Dictionary<string, float>();
        /// <summary>Hediffs (load IDs) already announced, so each illness gets one letter or message.</summary>
        private HashSet<int> announced = new HashSet<int>();
        /// <summary>Wounds (load IDs) already rolled for tetanus.</summary>
        private HashSet<int> woundsRolled = new HashSet<int>();
        /// <summary>Bites and stings (load IDs) already rolled for what an animal or an insect can pass on.</summary>
        private HashSet<int> bitesRolled = new HashSet<int>();
        /// <summary>Hediff load ID: the ThingID of the doctor who last tended it.</summary>
        private Dictionary<int, string> doctors = new Dictionary<int, string>();
        /// <summary>"ThingID|defName": they've had it before.</summary>
        private HashSet<string> had = new HashSet<string>();
        public int lastSeriousTick = -9999999;

        private List<string> tmpKeys1, tmpKeys2, tmpKeys3, tmpKeys4, tmpDoctors;
        private List<int> tmpInts1, tmpInts2, tmpInts3, tmpIds;
        private List<float> tmpFloats;

        // ---------------- Not saved ----------------

        private class PawnDay
        {
            public int day = -1;
            public readonly float[] hours = new float[32];
            public readonly HashSet<string> rolled = new HashSet<string>();
        }

        private readonly Dictionary<Pawn, PawnDay> days = new Dictionary<Pawn, PawnDay>();
        private readonly HashSet<HediffDef> seriousNow = new HashSet<HediffDef>();
        private readonly Dictionary<HediffDef, int> casesNow = new Dictionary<HediffDef, int>();
        private int lastDay = -1;

        private struct Ended
        {
            public Pawn pawn;
            public HediffDef def;
            public float severity;
            public int loadID;
        }

        private static readonly List<Ended> ended = new List<Ended>();

        private static AilmentTracker cached;
        private static Game cachedGame;

        public static AilmentTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<AilmentTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public AilmentTracker(Game game)
        {
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.ailments;

        public static bool SeriousOn => On && S.seriousIllnesses;

        private static float Freq => AilmentMath.Frequency(S?.ailmentFrequency ?? 1f);

        /// <summary>Mean days between catching everyday ailments on a map, and between catching serious illnesses, at 100%.</summary>
        public const float EverydayCatchingDays = 20f;
        public const float SeriousCatchingDays = 300f;

        /// <summary>No new serious illness starts within this many days of the last one.</summary>
        public const float SeriousGapDays = 30f;

        /// <summary>At most this many everyday ailments that come from circumstance at once per colonist.</summary>
        public const int MaxEverydayAtOnce = 2;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            days.Clear();
            ended.Clear();
            AilmentChecks.Reset();
            defs = null;
            incidents = null;
        }

        // ---------------- The defs ----------------

        private static List<HediffDef> defs;
        private static List<IncidentDef> incidents;

        /// <summary>Every Rimfolk ailment and illness.</summary>
        public static List<HediffDef> Defs
        {
            get
            {
                if (defs == null)
                    defs = DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.CompProps<HediffCompProperties_Ailment>() != null).ToList();
                return defs;
            }
        }

        private static readonly Dictionary<HediffDef, HediffCompProperties_Ailment> infos = new Dictionary<HediffDef, HediffCompProperties_Ailment>();

        /// <summary>The ailment part of a hediff (null for any other hediff), looked up once per def.</summary>
        public static HediffCompProperties_Ailment InfoOf(HediffDef d)
        {
            if (d == null) return null;
            if (!infos.TryGetValue(d, out var info))
            {
                info = d.CompProps<HediffCompProperties_Ailment>();
                infos[d] = info;
            }
            return info;
        }

        /// <summary>The catching ailments: Rimfolk disease incidents.</summary>
        public static List<IncidentDef> Incidents
        {
            get
            {
                if (incidents == null)
                    incidents = DefDatabase<IncidentDef>.AllDefsListForReading.Where(d => d.Worker is IncidentWorker_Ailment && d.diseaseIncident != null).ToList();
                return incidents;
            }
        }

        // ---------------- Ticking ----------------

        public override void GameComponentTick()
        {
            if (ended.Count > 0) ProcessEnded();
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Ailments)) return;
            if (!On) return;
            try
            {
                Hourly(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailments", e);
            }
        }

        private static readonly int PawnErrorKey = "ColonyCouncilailments of a colonist".GetHashCode();

        private void Hourly(int now)
        {
            int day = now / GenDate.TicksPerDay;
            CountSerious();
            if (now >= GenDate.TicksPerDay) Clock();
            // The first two days of a colony are for settling in.
            bool circumstance = now >= GenDate.TicksPerDay * 2;
            // A copy: the game's list is shared and rebuilt whenever anyone asks for it.
            var everyone = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList();
            for (int i = 0; i < everyone.Count; i++)
            {
                var p = everyone[i];
                if (!Eligible(p)) continue;
                try
                {
                    Hour(p, now, day, circumstance);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Rimfolk] ailments of " + p.LabelShort + ": " + e, PawnErrorKey);
                }
            }
            if (day != lastDay)
            {
                lastDay = day;
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ailments clean-up", e);
                }
            }
        }

        /// <summary>
        /// Who ailments happen to: the colony's own people (slaves too, they work), not guests, prisoners, quest lodgers,
        /// babies or anyone without an inner life; on a map or with a caravan, not in cryptosleep or away in the world.
        /// </summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && !p.Destroyed && p.RaceProps != null && p.RaceProps.Humanlike && p.RaceProps.IsFlesh && p.Faction == Faction.OfPlayer && !p.IsPrisoner
            && !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && p.health?.hediffSet != null
            && (p.Spawned || p.GetCaravan() != null);

        private PawnDay DayOf(Pawn p, int day, out bool newDay)
        {
            if (!days.TryGetValue(p, out var d))
            {
                d = new PawnDay();
                days[p] = d;
            }
            newDay = d.day != day;
            if (newDay)
            {
                d.day = day;
                for (int i = 0; i < d.hours.Length; i++) d.hours[i] = 0f;
                d.rolled.Clear();
            }
            return d;
        }

        private void Hour(Pawn p, int now, int day, bool circumstance)
        {
            var d = DayOf(p, day, out bool newDay);
            bool awake = RestUtility.Awake(p);
            if (newDay) Daily(p, day, circumstance);
            int mask = AilmentChecks.Sample(p, awake);
            for (int t = 0; t < 31; t++)
                if ((mask & (1 << t)) != 0) d.hours[t] += 1f;
            Announce(p);
            if (!circumstance) return;
            // Counted causes: once the day holds enough hours of it, one roll a day.
            foreach (var def in Defs)
            {
                var info = InfoOf(def);
                if (info?.causes == null || (info.serious && !SeriousOn)) continue;
                foreach (var c in info.causes)
                {
                    if (c == null || !AilmentChecks.Counted(c.trigger) || d.hours[(int)c.trigger] < c.hours) continue;
                    string key = def.defName + "|" + (int)c.trigger;
                    if (!d.rolled.Add(key)) continue;
                    if (!Rand.Chance(c.chance * Freq * PersonFactor(p, info) * Boost(p, c))) continue;
                    if (!CanGet(p, def, info, false)) continue;
                    Give(p, def, info, false);
                }
            }
            // Lice go round with the everyday ailments; the serious ones only while they're on.
            if (!awake) Spread(p);
            Bites(p, d);
            if (!SeriousOn) return;
            if (!d.rolled.Contains("Wound")) Wounds(p, d);
            if (!d.rolled.Contains("AfterCold")) AfterCold(p, d);
        }

        /// <summary>A cause that's likelier for someone who already has something (typhus for someone with lice).</summary>
        private static float Boost(Pawn p, AilmentCause c) =>
            c.boostHediff != null && c.boost != 1f && p.health.hediffSet.HasHediff(c.boostHediff) ? c.boost : 1f;

        /// <summary>
        /// Once a day per colonist: the diet, sweets, and the illnesses that come from them. Done once per game day even
        /// across a reload. Days away (an outpost, cryptosleep) or while the meal hook is off count as days eaten well.
        /// </summary>
        private void Daily(Pawn p, int day, bool circumstance)
        {
            string id = p.ThingID;
            bool firstToday = !seenDay.TryGetValue(id, out int seen) || seen != day;
            if (!firstToday) return;
            bool away = seen > 0 && day - seen > 1;
            seenDay[id] = day;
            if (!DietKnown || away || !freshDay.ContainsKey(id)) freshDay[id] = day;
            if (sweets.TryGetValue(id, out float sw)) sweets[id] = sw * 0.84f;
            if (!circumstance) return;
            int without = day - freshDay[id];
            foreach (var def in Defs)
            {
                var info = InfoOf(def);
                if (info?.causes == null || (info.serious && !SeriousOn)) continue;
                var diet = info.Cause(AilmentTrigger.Diet);
                if (diet != null && DietKnown && without >= diet.hours && Rand.Chance(diet.chance * Freq) && CanGet(p, def, info, false))
                    Give(p, def, info, false);
                var sweet = info.Cause(AilmentTrigger.Sweets);
                if (sweet != null)
                {
                    float recent = sweets.TryGetValue(id, out float s) ? s : 0f;
                    float age = Flavor.HumanAge(p);
                    float chance = sweet.chance * (1f + Mathf.Min(recent, 10f)) * (age >= 60f ? 3f : age >= 40f ? 2f : 1f);
                    if (Rand.Chance(chance * Freq * PersonFactor(p, info)) && CanGet(p, def, info, false)) Give(p, def, info, false);
                }
            }
        }

        private static float PersonFactor(Pawn p, HediffCompProperties_Ailment info)
        {
            float f = 1f;
            if (p.DevelopmentalStage.Juvenile()) f *= info.childFactor;
            else if (info.oldFactor != 1f && Flavor.HumanAge(p) >= 60f) f *= info.oldFactor;
            if (info.risks != null)
                for (int i = 0; i < info.risks.Count; i++)
                    if (info.risks[i] != null && info.risks[i].Applies(p)) f *= info.risks[i].factor;
            return f;
        }

        // ---------------- Coming down with something ----------------

        /// <summary>
        /// Whether they can come down with it now: not already, not on cooldown, not resistant, not too many small ailments
        /// at once, and for a serious illness, the colony has room for one (none running, none for a while).
        /// </summary>
        private bool CanGet(Pawn p, HediffDef def, HediffCompProperties_Ailment info, bool spreading)
        {
            if (!On || (info.serious && !SeriousOn)) return false;
            if (p.health.hediffSet.HasHediff(def)) return false;
            if (p.DevelopmentalStage.Juvenile() && info.childFactor <= 0f) return false;
            if (info.childrenOnly && !p.DevelopmentalStage.Juvenile()) return false;
            if (!info.biomes.NullOrEmpty() && (p.MapHeld?.Biome == null || !info.biomes.Contains(p.MapHeld.Biome.defName))) return false;
            if (info.once && HadBefore(p, def)) return false;
            int now = Find.TickManager.TicksGame;
            if (last.TryGetValue(p.ThingID + "|" + def.defName, out int t) && t <= now && now - t < info.cooldownDays * GenDate.TicksPerDay) return false;
            try
            {
                if (p.health.immunity.DiseaseContractChanceFactor(def) <= 0f) return false;
            }
            catch
            {
                // A modded immunity system that can't answer: carry on.
            }
            if (info.serious)
            {
                if (spreading) return CasesOf(def) < info.maxCases;
                return SeriousRoom(def);
            }
            return EverydayCount(p) < MaxEverydayAtOnce;
        }

        private static int EverydayCount(Pawn p)
        {
            int n = 0;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                var info = InfoOf(h.def);
                if (info != null && !info.serious && !info.causes.NullOrEmpty()) n++;
            }
            return n;
        }

        /// <summary>Room in the colony for a new serious illness: none running now, and the last one a while ago.</summary>
        public bool SeriousRoom(HediffDef def)
        {
            if (!SeriousOn) return false;
            if (seriousNow.Count > 0) return false;
            int now = Find.TickManager.TicksGame;
            return now - lastSeriousTick >= SeriousGapDays * GenDate.TicksPerDay || lastSeriousTick > now;
        }

        private int CasesOf(HediffDef def) => casesNow.TryGetValue(def, out int n) ? n : 0;

        /// <summary>Which serious illnesses the colony has right now (hidden ones too), and how many cases of each.</summary>
        private void CountSerious()
        {
            seriousNow.Clear();
            casesNow.Clear();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
            {
                var hs = p?.health?.hediffSet?.hediffs;
                if (hs == null) continue;
                for (int i = 0; i < hs.Count; i++)
                {
                    var info = InfoOf(hs[i].def);
                    if (info == null || !info.serious) continue;
                    seriousNow.Add(hs[i].def);
                    casesNow[hs[i].def] = CasesOf(hs[i].def) + 1;
                }
            }
        }

        private void Give(Pawn p, HediffDef def, HediffCompProperties_Ailment info, bool spreading)
        {
            var h = HediffMaker.MakeHediff(def, p);
            if (h.TryGetComp<HediffComp_Immunizable>() == null) h.Severity = info.startSeverity.RandomInRange;
            p.health.AddHediff(h);
            if (!p.health.hediffSet.hediffs.Contains(h)) return;
            int now = Find.TickManager.TicksGame;
            Remember(p, def, now);
            if (info.serious)
            {
                if (!spreading) lastSeriousTick = now;
                seriousNow.Add(def);
                casesNow[def] = CasesOf(def) + 1;
            }
            if (h.Visible) Shown(p, h, info);
        }

        private void Remember(Pawn p, HediffDef def, int now)
        {
            last[p.ThingID + "|" + def.defName] = now;
            last[p.ThingID + "|any"] = now;
            had.Add(p.ThingID + "|" + def.defName);
        }

        public bool HadBefore(Pawn p, HediffDef def) => p != null && def != null && had.Contains(p.ThingID + "|" + def.defName);

        /// <summary>A Rimfolk disease incident gave them this (its letter is the incident's own).</summary>
        public void Notify_Caught(Pawn p, Hediff h)
        {
            var info = InfoOf(h.def);
            if (info == null) return;
            int now = Find.TickManager.TicksGame;
            Remember(p, h.def, now);
            announced.Add(h.loadID);
            if (info.serious)
            {
                lastSeriousTick = now;
                seriousNow.Add(h.def);
                casesNow[h.def] = CasesOf(h.def) + 1;
            }
            if (p.Faction == Faction.OfPlayer && Rand.Chance(info.serious ? 0.7f : 0.4f)) Journal(p, info.serious ? "Council_Journal_Ail_Serious" : "Council_Journal_Ail_Start", h.def, info);
        }

        /// <summary>
        /// The first time an illness shows: a message for an everyday one, a letter with a doctor's note for a serious
        /// one, and now and then a line in their journal.
        /// </summary>
        private void Shown(Pawn p, Hediff h, HediffCompProperties_Ailment info)
        {
            if (!announced.Add(h.loadID)) return;
            if (info.serious)
            {
                SeriousLetter(p, h, info);
                if (Rand.Chance(0.7f)) Journal(p, "Council_Journal_Ail_Serious", h.def, info);
                return;
            }
            if (!info.gotText.NullOrEmpty())
            {
                string text;
                try
                {
                    text = info.gotText.Formatted(p.Named("PAWN")).Resolve();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ailment message " + h.def.defName, e);
                    text = null;
                }
                if (!text.NullOrEmpty())
                    News.Post(text, new LookTargets(p), NewsKind.Health, MessageTypeDefOf.NegativeHealthEvent, info.quiet ? Importance.Flavor : Importance.Notable);
            }
            if (Rand.Chance(0.4f)) Journal(p, "Council_Journal_Ail_Start", h.def, info);
        }

        /// <summary>Illnesses that were hidden (incubating) and show now get their letter or message.</summary>
        private void Announce(Pawn p)
        {
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                var info = InfoOf(h.def);
                if (info == null || announced.Contains(h.loadID) || !h.Visible) continue;
                // Everyday ones given by hand (dev mode) or by another mod: no message, just remembered.
                if (!info.serious)
                {
                    announced.Add(h.loadID);
                    continue;
                }
                Shown(p, h, info);
            }
        }

        private static void SeriousLetter(Pawn p, Hediff h, HediffCompProperties_Ailment info)
        {
            string text;
            try
            {
                text = info.letterText.NullOrEmpty() ? h.def.description : info.letterText.Formatted(p.Named("PAWN")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailment letter " + h.def.defName, e);
                text = h.def.description;
            }
            string note = DoctorsNote(h.def);
            if (!note.NullOrEmpty()) text += "\n\n" + note;
            string label = h.def.LabelCap + ": " + p.LabelShortCap;
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent, new LookTargets(p));
        }

        /// <summary>A doctor's note on an illness, fitting the colony's era, with its heading.</summary>
        public static string DoctorsNote(HediffDef def)
        {
            var info = InfoOf(def);
            if (info == null || info.notes.NullOrEmpty()) return null;
            string note = Flavor.PickText(info.notes);
            if (note.NullOrEmpty()) return null;
            return (Avail.IsPreIndustrial ? "CC_Ail_NoteHeader_LowTech" : "CC_Ail_NoteHeader").Translate() + " " + note;
        }

        private static void Journal(Pawn p, string pool, HediffDef def, HediffCompProperties_Ailment info, Pawn other = null)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null || p == null || p.Faction != Faction.OfPlayer) return;
            var tags = info?.key.NullOrEmpty() == false ? new[] { "ail:" + info.key, info.serious ? "serious" : "everyday" } : null;
            string illness = def?.label ?? "";
            if (other != null) journals.WriteTagged(p, pool, tags, null, illness.Named("ILLNESS"), other.Named("OTHER"));
            else journals.WriteTagged(p, pool, tags, null, illness.Named("ILLNESS"));
        }

        // ---------------- The special causes ----------------

        private static readonly HashSet<string> DirtyWounds = new HashSet<string> { "Bite", "Scratch", "Stab", "Cut", "Shredded", "Gunshot" };

        /// <summary>
        /// Deep wounds left untended for hours may bring tetanus: one roll per colonist per day however many wounds there
        /// are, each wound counted once, and less likely in eras that vaccinate.
        /// </summary>
        private void Wounds(Pawn p, PawnDay d)
        {
            List<Hediff_Injury> fresh = null;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (!(h is Hediff_Injury inj) || inj.Severity < 3f || !DirtyWounds.Contains(inj.def.defName) || inj.IsPermanent()) continue;
                if (woundsRolled.Contains(inj.loadID)) continue;
                (fresh ??= new List<Hediff_Injury>()).Add(inj);
            }
            if (fresh == null) return;
            foreach (var def in Defs)
            {
                var info = InfoOf(def);
                var cause = info?.Cause(AilmentTrigger.Wound);
                if (cause == null) continue;
                bool open = false;
                foreach (var inj in fresh)
                {
                    // A wound cleaned and dressed in time is safe; only those left open this long count.
                    if (inj.IsTended())
                    {
                        woundsRolled.Add(inj.loadID);
                        continue;
                    }
                    if (inj.ageTicks < cause.hours * GenDate.TicksPerHour) continue;
                    woundsRolled.Add(inj.loadID);
                    open = true;
                }
                if (!open) continue;
                d.rolled.Add("Wound");
                if (Rand.Chance(cause.chance * EraFactor * Freq * PersonFactor(p, info)) && CanGet(p, def, info, false))
                {
                    Give(p, def, info, false);
                    return;
                }
            }
        }

        private static readonly HashSet<string> BiteWounds = new HashSet<string> { "Bite" };
        private static readonly HashSet<string> StingWounds = new HashSet<string> { "Bite", "Stab", "Scratch", "Cut" };

        /// <summary>
        /// Fresh bites from a warm-blooded animal, or bites and stings from an insectoid: one roll a day for each kind,
        /// however many wounds there are, for what such bites can pass on (rabies, hive fever). Each wound counts once,
        /// a few hours after it was made or as soon as it's dressed; animal bites that were all cleaned and dressed in
        /// that time are half as likely to pass anything on.
        /// </summary>
        private void Bites(Pawn p, PawnDay d)
        {
            bool bitten = false, stung = false, allCleaned = true;
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                if (!(hs[i] is Hediff_Injury inj) || inj.Severity < 2f || inj.IsPermanent() || bitesRolled.Contains(inj.loadID)) continue;
                var race = inj.sourceDef?.race;
                if (race == null || !race.IsFlesh || race.Humanlike) continue;
                bool insect = race.FleshType == FleshTypeDefOf.Insectoid && StingWounds.Contains(inj.def.defName);
                bool animal = !insect && race.Animal && race.FleshType == FleshTypeDefOf.Normal && BiteWounds.Contains(inj.def.defName);
                if (!insect && !animal) continue;
                // Given a few hours to be cleaned first; then counted once, whatever happens.
                if (inj.ageTicks < GenDate.TicksPerHour * 4 && !inj.IsTended()) continue;
                bitesRolled.Add(inj.loadID);
                if (insect) stung = true;
                else
                {
                    bitten = true;
                    if (!inj.IsTended()) allCleaned = false;
                }
            }
            if (bitten && d.rolled.Add("AnimalBite")) BiteRoll(p, AilmentTrigger.AnimalBite, allCleaned ? 0.5f : 1f);
            if (stung && d.rolled.Add("InsectBite")) BiteRoll(p, AilmentTrigger.InsectBite, 1f);
        }

        private void BiteRoll(Pawn p, AilmentTrigger trigger, float factor)
        {
            foreach (var def in Defs)
            {
                var info = InfoOf(def);
                var cause = info?.Cause(trigger);
                if (cause == null || (info.serious && !SeriousOn)) continue;
                if (Rand.Chance(cause.chance * factor * Freq * PersonFactor(p, info)) && CanGet(p, def, info, false))
                {
                    Give(p, def, info, false);
                    return;
                }
            }
        }

        /// <summary>Colonies with modern medicine vaccinate: tetanus is much rarer there.</summary>
        private static float EraFactor
        {
            get
            {
                var tech = Avail.EffectiveTech;
                return tech >= TechLevel.Spacer ? 0.2f : tech == TechLevel.Industrial ? 0.4f : 1f;
            }
        }

        private static HediffDef flu, malnutrition, frail, hypothermia;
        private static bool lookedVanilla;

        private static void LookVanilla()
        {
            if (lookedVanilla) return;
            lookedVanilla = true;
            flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            malnutrition = DefDatabase<HediffDef>.GetNamedSilentFail("Malnutrition");
            frail = DefDatabase<HediffDef>.GetNamedSilentFail("Frail");
            hypothermia = DefDatabase<HediffDef>.GetNamedSilentFail("Hypothermia");
        }

        private static bool HasAtLeast(Pawn p, HediffDef def, float severity)
        {
            if (def == null) return false;
            var h = p.health.hediffSet.GetFirstHediffOfDef(def);
            return h != null && h.Visible && h.Severity >= severity;
        }

        /// <summary>Old, frail, starving or chilled to the bone: pneumonia after a cold is a real danger for them.</summary>
        public static bool Vulnerable(Pawn p)
        {
            LookVanilla();
            return Flavor.HumanAge(p) >= 60f || p.DevelopmentalStage.Child() && Flavor.HumanAge(p) < 6f || HasAtLeast(p, malnutrition, 0.2f)
                   || HasAtLeast(p, frail, 0f) || HasAtLeast(p, hypothermia, 0.15f);
        }

        /// <summary>A cold, a chest cold or the flu that has taken hold: once a day, a chance of pneumonia (much more for the vulnerable).</summary>
        private void AfterCold(Pawn p, PawnDay d)
        {
            LookVanilla();
            bool sick = false;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (!h.Visible || h.Severity < 0.25f) continue;
                if (h.def == flu || InfoOf(h.def)?.key is "HeadCold" or "ChestCold")
                {
                    sick = true;
                    break;
                }
            }
            if (!sick) return;
            d.rolled.Add("AfterCold");
            float vulnerable = Vulnerable(p) ? 1f : 0.15f;
            foreach (var def in Defs)
            {
                var info = InfoOf(def);
                var cause = info?.Cause(AilmentTrigger.AfterCold);
                if (cause == null) continue;
                if (Rand.Chance(cause.chance * vulnerable * Freq) && CanGet(p, def, info, false))
                {
                    Give(p, def, info, false);
                    return;
                }
            }
        }

        /// <summary>
        /// Someone asleep with a contagious illness that shows can pass it to whoever sleeps in the same room: less while
        /// they're being treated, not at all from a room of their own. Lice (typhus) only in a dirty room or to the unwashed.
        /// </summary>
        private void Spread(Pawn p)
        {
            if (!p.Spawned) return;
            Room room = null;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                var info = InfoOf(h.def);
                if (info == null || info.spreadPerHour <= 0f || !h.Visible || (info.serious && !SeriousOn)) continue;
                if (room == null)
                {
                    var bed = RestUtility.CurrentBed(p);
                    room = bed?.GetRoom();
                    if (room == null || room.PsychologicallyOutdoors) return;
                }
                bool dirtyRoom = AilmentChecks.Dirty(room);
                float chance = info.spreadPerHour * (h.IsTended() ? info.spreadWhileTended : 1f) * Freq;
                foreach (var bed in room.ContainedBeds)
                {
                    foreach (var other in bed.CurOccupants)
                    {
                        if (other == p || !Eligible(other) || RestUtility.Awake(other)) continue;
                        if (info.spreadNeedsDirt && !dirtyRoom && !AilmentChecks.Unwashed(other) && !AilmentChecks.Unwashed(p)) continue;
                        if (!Rand.Chance(chance) || !CanGet(other, h.def, info, true)) continue;
                        Give(other, h.def, info, true);
                    }
                }
            }
        }

        // ---------------- Diet ----------------

        /// <summary>Whether the colony's meals are being watched (Rimfolk's meal hook is on and applied).</summary>
        public static bool DietKnown => RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_AilmentDiet));

        /// <summary>A colonist ate something: fruit and vegetables (raw or cooked in) and sweets are noted.</summary>
        public void Notify_Ate(Pawn p, Thing food)
        {
            var def = food?.def;
            if (def?.ingestible == null || p == null) return;
            string id = p.ThingID;
            if (IsFresh(food)) freshDay[id] = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (IsSweet(def)) sweets[id] = (sweets.TryGetValue(id, out float s) ? s : 0f) + 1f;
            if (!On || !Eligible(p) || Find.TickManager.TicksGame < GenDate.TicksPerDay * 2) return;
            bool rich = def.ingestible.preferability >= FoodPreferability.MealFine;
            bool drink = IsAlcohol(def);
            if (!rich && !drink) return;
            float age = Flavor.HumanAge(p);
            foreach (var d in Defs)
            {
                var info = InfoOf(d);
                if (info?.causes == null || info.serious) continue;
                var c = rich ? info.Cause(AilmentTrigger.RichMeal) : null;
                float chance = c != null ? c.chance * (age >= 60f ? 2f : age >= 40f ? 1.5f : 1f) : 0f;
                var dc = drink ? info.Cause(AilmentTrigger.Drink) : null;
                if (dc != null) chance = Mathf.Max(chance, dc.chance);
                if (chance <= 0f) continue;
                if (Rand.Chance(chance * Freq * PersonFactor(p, info)) && CanGet(p, d, info, false))
                {
                    Give(p, d, info, false);
                    return;
                }
            }
        }

        private static bool IsAlcohol(ThingDef d)
        {
            if (!d.IsDrug) return false;
            var drug = d.GetCompProperties<CompProperties_Drug>();
            return drug?.chemical != null && drug.chemical == ChemicalDefOf.Alcohol;
        }

        private static bool IsFresh(Thing food)
        {
            if ((food.def.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0) return true;
            var ingredients = food.TryGetComp<CompIngredients>()?.ingredients;
            if (ingredients == null) return false;
            foreach (var i in ingredients)
                if (i?.ingestible != null && (i.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0)
                    return true;
            return false;
        }

        private static readonly string[] SweetWords = { "Chocolate", "Candy", "Sweet", "Honey", "Jam", "Cake", "Dessert", "Sugar", "Pie", "Ambrosia" };

        private static bool IsSweet(ThingDef d)
        {
            if (d.IsDrug && d.ingestible.joyKind?.defName != "Gluttonous") return false;
            if (d.ingestible.joy > 0f && d.ingestible.joyKind?.defName == "Gluttonous") return true;
            foreach (var w in SweetWords)
                if (d.defName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        /// <summary>They've had fruit or vegetables within the last days (true when the diet isn't being watched).</summary>
        public bool AteFreshWithin(Pawn p, float daysBack)
        {
            if (p == null || !DietKnown) return true;
            if (!freshDay.TryGetValue(p.ThingID, out int day)) return true;
            return Find.TickManager.TicksGame / GenDate.TicksPerDay - day <= Mathf.CeilToInt(daysBack);
        }

        // ---------------- Doctors and getting better ----------------

        /// <summary>Someone tended an ailment: the first time for a serious case, the doctor takes it to heart.</summary>
        public void Notify_Tended(Pawn patient, Hediff h)
        {
            var info = InfoOf(h?.def);
            if (info == null || patient == null) return;
            var doctor = DoctorOf(patient);
            if (doctor == null) return;
            bool first = !doctors.ContainsKey(h.loadID);
            doctors[h.loadID] = doctor.ThingID;
            if (!first || doctor == patient || doctor.Faction != Faction.OfPlayer) return;
            if (info.serious)
            {
                CouncilGameComponent.GiveThought(doctor, AilmentDefOf.Council_Ail_HardCase, null, 1f);
                if (Rand.Chance(0.6f)) Journal(doctor, "Council_Journal_Ail_Doctor", h.def, info, patient);
            }
            else if (Rand.Chance(0.12f)) Journal(doctor, "Council_Journal_Ail_Doctor", h.def, info, patient);
        }

        /// <summary>The colonist tending this patient right now.</summary>
        private static Pawn DoctorOf(Pawn patient)
        {
            if (!patient.Spawned || patient.Map == null) return null;
            if (patient.CurJobDef == JobDefOf.TendPatient && patient.CurJob?.targetA.Thing == patient) return patient;
            // Slaves tend too; a mech paramedic is no doctor to credit.
            foreach (var c in patient.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                if (c.RaceProps.Humanlike && c.CurJobDef == JobDefOf.TendPatient && c.CurJob?.targetA.Thing == patient)
                    return c;
            return null;
        }

        private static Pawn ColonistById(string id)
        {
            if (id.NullOrEmpty()) return null;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
                if (p.ThingID == id)
                    return p;
            return null;
        }

        /// <summary>An ailment ended (its comp heard it go); handled on the next tick.</summary>
        public static void QueueEnded(Pawn p, Hediff h)
        {
            if (p == null || h?.def == null) return;
            ended.Add(new Ended { pawn = p, def = h.def, severity = h.Severity, loadID = h.loadID });
        }

        private void ProcessEnded()
        {
            var list = ended.ToList();
            ended.Clear();
            foreach (var e in list)
            {
                try
                {
                    Recovered(e);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("ailment ended", ex);
                }
            }
        }

        private void Recovered(Ended e)
        {
            string doctorId = doctors.TryGetValue(e.loadID, out var d) ? d : null;
            doctors.Remove(e.loadID);
            announced.Remove(e.loadID);
            var p = e.pawn;
            if (p == null || p.Dead || p.Destroyed || p.Discarded || p.health?.hediffSet == null) return;
            var info = InfoOf(e.def);
            if (info == null) return;
            // Cured outright (a healer serum, surgery, a resurrection) rather than got over: only what a cure leaves.
            if (e.severity > 0.001f)
            {
                AfterEffects(p, info.curedAfterEffects);
                return;
            }
            // Got over it: what it leaves behind for a while.
            AfterEffects(p, info.afterEffects);
            if (p.Faction != Faction.OfPlayer || !People.HasInnerLife(p)) return;
            if (info.serious)
            {
                News.Post("CC_Ail_Recovered".Translate(p.Named("PAWN"), e.def.label.Named("ILLNESS")).Resolve(), new LookTargets(p),
                    NewsKind.Health, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                CouncilGameComponent.GiveThought(p, AilmentDefOf.Council_Ail_Survived);
                Journal(p, "Council_Journal_Ail_Better", e.def, info);
                var doctor = ColonistById(doctorId);
                if (doctor != null && doctor != p)
                {
                    CouncilGameComponent.GiveThought(doctor, AilmentDefOf.Council_Ail_PulledThrough, null, -1f, "CC_Cause_About".Translate(p.LabelShort).ToString());
                    Journal(doctor, "Council_Journal_Ail_DoctorSaved", e.def, info, p);
                    // Counts toward the doctor's name (Health/HealthTracker.cs).
                    HealthTracker.Instance?.PulledThrough(doctor, p, e.def.label, 1f, true);
                }
            }
            else if (!info.lingering && Rand.Chance(0.15f)) Journal(p, "Council_Journal_Ail_Better", e.def, info);
        }

        private void AfterEffects(Pawn p, List<AilmentAfterEffect> list)
        {
            if (list == null) return;
            foreach (var ae in list)
            {
                if (ae?.hediff == null || p.health.hediffSet.HasHediff(ae.hediff) || !Rand.Chance(ae.chance)) continue;
                var after = HediffMaker.MakeHediff(ae.hediff, p);
                after.Severity = ae.severity.RandomInRange;
                p.health.AddHediff(after);
                announced.Add(after.loadID);
            }
        }

        /// <summary>An illness killed them: the doctor who fought it remembers.</summary>
        public void Notify_Died(Pawn p, Hediff h)
        {
            if (h == null) return;
            string doctorId = doctors.TryGetValue(h.loadID, out var d) ? d : null;
            doctors.Remove(h.loadID);
            var doctor = ColonistById(doctorId);
            if (doctor != null && doctor != p) Journal(doctor, "Council_Journal_Ail_DoctorLost", h.def, InfoOf(h.def), p);
        }

        // ---------------- The catching clock ----------------

        /// <summary>A sickness in the region (a world event) makes everything go round more.</summary>
        private static float SituationFactor()
        {
            var world = WorldSituations.Instance;
            if (world == null) return 1f;
            foreach (var s in world.Running)
                if (s?.def != null && s.def.kind == "sickness")
                    return 0.6f;
            return 1f;
        }

        private void Clock()
        {
            if (!DebugSettings.enableRandomDiseases) return;
            var difficulty = Find.Storyteller?.difficulty;
            float interval = difficulty?.diseaseIntervalFactor ?? 1f;
            if (interval >= Difficulty.DiseaseIntervalFactorCutoff) return;
            float situation = SituationFactor();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || map.Biome == null || map.Biome.inVacuum) continue;
                float everyday = AilmentMath.CatchingMtbDays(EverydayCatchingDays, map.Biome.diseaseMtbDays, interval, Freq, situation);
                if (Rand.MTBEventOccurs(everyday, GenDate.TicksPerDay, GenDate.TicksPerHour)) TryCatching(map, false);
                if (!SeriousOn) continue;
                float serious = AilmentMath.CatchingMtbDays(SeriousCatchingDays, map.Biome.diseaseMtbDays, interval, Freq, situation);
                if (Rand.MTBEventOccurs(serious, GenDate.TicksPerDay, GenDate.TicksPerHour)) TryCatching(map, true);
            }
        }

        /// <summary>Fires one catching ailment that fits the map now, through the storyteller (so its refire days count).</summary>
        public bool TryCatching(Map map, bool serious)
        {
            var options = new List<(IncidentDef def, IncidentParms parms, float weight)>();
            foreach (var def in Incidents)
            {
                if (!(def.Worker is IncidentWorker_Ailment worker) || (InfoOf(def.diseaseIncident)?.serious ?? false) != serious) continue;
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                if (!def.Worker.CanFireNow(parms)) continue;
                float w = worker.Weight(map);
                if (w > 0f) options.Add((def, parms, w));
            }
            if (options.Count == 0) return false;
            var pick = options.RandomElementByWeight(o => o.weight);
            return Find.Storyteller.TryFire(new FiringIncident(pick.def, null, pick.parms));
        }

        // ---------------- Saving ----------------

        private void Prune()
        {
            var ids = CouncilGameComponent.KnownPawnIds();
            int now = Find.TickManager.TicksGame;
            last.RemoveAll(kv => now - kv.Value > GenDate.TicksPerDay * 120 || kv.Value > now);
            CouncilGameComponent.PruneByPawn(last);
            foreach (var k in freshDay.Keys.Where(k => !ids.Contains(k)).ToList()) freshDay.Remove(k);
            foreach (var k in seenDay.Keys.Where(k => !ids.Contains(k)).ToList()) seenDay.Remove(k);
            foreach (var k in sweets.Keys.Where(k => !ids.Contains(k) || sweets[k] < 0.05f).ToList()) sweets.Remove(k);
            had.RemoveWhere(k =>
            {
                int i = k.IndexOf('|');
                return i <= 0 || !ids.Contains(k.Substring(0, i));
            });
            // Hediffs that are gone: the colonists' current ones are all that matter.
            var live = new HashSet<int>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
            {
                var hs = p?.health?.hediffSet?.hediffs;
                if (hs == null) continue;
                foreach (var h in hs) live.Add(h.loadID);
            }
            announced.RemoveWhere(id => !live.Contains(id));
            woundsRolled.RemoveWhere(id => !live.Contains(id));
            bitesRolled.RemoveWhere(id => !live.Contains(id));
            foreach (var k in doctors.Keys.Where(k => !live.Contains(k)).ToList()) doctors.Remove(k);
            foreach (var p in days.Keys.Where(p => p == null || p.Dead || p.Destroyed || !Eligible(p)).ToList()) days.Remove(p);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ailments clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value, ref tmpKeys1, ref tmpInts1);
            Scribe_Collections.Look(ref freshDay, "freshDay", LookMode.Value, LookMode.Value, ref tmpKeys2, ref tmpInts2);
            Scribe_Collections.Look(ref seenDay, "seenDay", LookMode.Value, LookMode.Value, ref tmpKeys3, ref tmpInts3);
            Scribe_Collections.Look(ref sweets, "sweets", LookMode.Value, LookMode.Value, ref tmpKeys4, ref tmpFloats);
            Scribe_Collections.Look(ref announced, "announced", LookMode.Value);
            Scribe_Collections.Look(ref woundsRolled, "woundsRolled", LookMode.Value);
            Scribe_Collections.Look(ref bitesRolled, "bitesRolled", LookMode.Value);
            Scribe_Collections.Look(ref doctors, "doctors", LookMode.Value, LookMode.Value, ref tmpIds, ref tmpDoctors);
            Scribe_Collections.Look(ref had, "had", LookMode.Value);
            Scribe_Values.Look(ref lastSeriousTick, "lastSeriousTick", -9999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                last ??= new Dictionary<string, int>();
                freshDay ??= new Dictionary<string, int>();
                seenDay ??= new Dictionary<string, int>();
                sweets ??= new Dictionary<string, float>();
                announced ??= new HashSet<int>();
                woundsRolled ??= new HashSet<int>();
                bitesRolled ??= new HashSet<int>();
                doctors ??= new Dictionary<int, string>();
                had ??= new HashSet<string>();
            }
        }
    }

    [DefOf]
    public static class AilmentDefOf
    {
        public static ThoughtDef Council_Ail_HardCase;
        public static ThoughtDef Council_Ail_PulledThrough;
        public static ThoughtDef Council_Ail_Survived;

        static AilmentDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AilmentDefOf));
        }
    }
}
