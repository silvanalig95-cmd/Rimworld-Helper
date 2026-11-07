using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One record that counts toward a colony trait, with its weight (a tamed animal counts for more than a sown plant).</summary>
    public class ColonyTraitRecord
    {
        public RecordDef record;
        public float weight = 1f;
    }

    /// <summary>
    /// One of Rimfolk's own tallies that counts toward a colony trait (a camp hosted, a ritual of the faith held to its
    /// end), with its weight. The tallies are kept by <see cref="ColonyTraits.Tally"/> as the things happen.
    /// </summary>
    public class ColonyTraitTally
    {
        public string key;
        public float weight = 1f;
    }

    /// <summary>
    /// Something the colony grows into by what its people do, beside the five ways its council's choices shape:
    /// green-fingered, builders, learned. Measured from the colonists' own records as a rolling daily average per
    /// head, so it comes with steady work and fades when the work stops. Those with a passion for the work are proud
    /// of it. Some are measured from Rimfolk's own tallies instead (camps hosted, rituals kept: for the colony as a
    /// whole), or from a standing count read each day (the calendar's days kept year after year).
    /// </summary>
    public class ColonyTraitDef : Def
    {
        public List<ColonyTraitRecord> records = new List<ColonyTraitRecord>();
        /// <summary>Rimfolk's own tallies that count toward it (see <see cref="ColonyTraits.Tally"/>).</summary>
        public List<ColonyTraitTally> tallies;
        /// <summary>Counted for the colony as a whole rather than per head (a camp hosted is one camp, however many live here).</summary>
        public bool perColony;
        /// <summary>A standing count read each day instead of a rolling average (see <see cref="ColonyTraits.Level"/>): "calendar".</summary>
        public string level;
        /// <summary>What it needs: "Ideology", "Biotech", "Royalty", or a mod's package id. Without it the trait isn't shown or earned.</summary>
        public string requires;
        /// <summary>
        /// How the colony's old hands lean while it holds (a colony known for its hospitality minds turning people away):
        /// added to their nature when an answer goes with or against one of the ways (SetbackCouncil's "not my choice").
        /// </summary>
        public WayShift lean;
        /// <summary>Camps come this much sooner after the last one while the colony is known for it (1: no change).</summary>
        public float campGap = 1f;
        /// <summary>The colony is known for it once the rolling daily amount per colonist reaches this.</summary>
        public float knownAt = 10f;
        /// <summary>Colonists with a passion for this skill take pride in it.</summary>
        public SkillDef skill;
        /// <summary>A name for dilemmas and encounters to ask for (like the ways' Merciful, Thrifty...).</summary>
        public string tag;
        public int order;
        /// <summary>The letter when the colony becomes known for it.</summary>
        [MustTranslate] public string earned;
        /// <summary>The news when people stop saying it.</summary>
        [MustTranslate] public string lost;

        /// <summary>The DLC or mod it needs is there.</summary>
        public bool Available
        {
            get
            {
                if (requires.NullOrEmpty()) return true;
                switch (requires)
                {
                    case "Ideology": return ModsConfig.IdeologyActive;
                    case "Biotech": return ModsConfig.BiotechActive;
                    case "Royalty": return ModsConfig.RoyaltyActive;
                    default: return People.ModActive(requires);
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (records.NullOrEmpty() && tallies.NullOrEmpty() && level.NullOrEmpty()) yield return "colony trait has no records, tallies or level";
            if (knownAt <= 0f) yield return "knownAt must be above zero";
            if (campGap <= 0f) yield return "campGap must be above zero";
            if (!level.NullOrEmpty() && !ColonyTraits.Levels.Contains(level)) yield return "unknown level " + level;
        }
    }

    /// <summary>The colony traits as they stand: kept inside the colony's character (see <see cref="CharacterTracker"/>).</summary>
    public class ColonyTraits : IExposable
    {
        /// <summary>The share of today in the rolling average: about forty days of memory, so a winter alone doesn't undo a farming colony.</summary>
        public const float Alpha = 1f / 40f;
        /// <summary>Known at the def's level, and lost again below this share of it, so it doesn't flicker.</summary>
        public const float LoseShare = 0.6f;

        private Dictionary<string, float> rates = new Dictionary<string, float>();
        private Dictionary<string, float> last = new Dictionary<string, float>();
        private List<string> known = new List<string>();
        private int days;
        /// <summary>Rimfolk's own tallies since yesterday's count (camps hosted, rituals kept...).</summary>
        private Dictionary<string, float> today = new Dictionary<string, float>();

        /// <summary>The standing counts a trait can be read from.</summary>
        public static readonly HashSet<string> Levels = new HashSet<string> { "calendar" };

        /// <summary>The traits this game can have (those whose DLC or mod is there).</summary>
        public static IEnumerable<ColonyTraitDef> All => DefDatabase<ColonyTraitDef>.AllDefsListForReading.Where(d => d.Available).OrderBy(d => d.order);

        /// <summary>
        /// Something that counts toward a trait happened (a camp hosted, a ritual of the faith held): it is added to
        /// today's tally, and folded into the traits that count it at the end of the day.
        /// </summary>
        public static void Tally(string key, float amount = 1f, Map map = null)
        {
            if (key.NullOrEmpty() || amount == 0f) return;
            // The colony where it happened (each colony its own character, decision 13A); the seat's for no home.
            var t = (map != null ? CharacterTracker.For(map) : CouncilGameComponent.Instance?.character)?.traits;
            if (t == null) return;
            t.today[key] = (t.today.TryGetValue(key, out float n) ? n : 0f) + amount;
        }

        /// <summary>A trait read from a standing count: the count today, of the colony of <paramref name="map"/> (the seat's for none).</summary>
        public static float Level(string level, Map map = null)
        {
            switch (level)
            {
                case "calendar":
                    // Each colony's own calendar (the seat's for a map that isn't a home).
                    var days = ColonyCalendar.On ? (map != null ? ColonyCalendar.For(map) : ColonyCalendar.Seat)?.days : null;
                    return days == null ? 0f : ColonyTraitMath.CalendarLevel(days.Where(d => d != null).Select(d => (d.Personal, d.keptYears, d.tradition)));
                default: return 0f;
            }
        }

        private static Func<ThingDef, bool> wallPred;

        /// <summary>A colony's home has walls enough to be called walled (ManyColoniesMath.MayBeWalled: the calendar's walls-up line).</summary>
        public static bool HasWalls(Map map)
        {
            if (map == null) return false;
            try
            {
                wallPred ??= RoomTemplateDef.RolePredicate("wall");
                return wallPred != null && ManyColoniesMath.MayBeWalled(Avail.ColonyCount(map, wallPred));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony traits: the walls", e);
                return true;
            }
        }

        /// <summary>
        /// A colony founded from another starts with half its mother's (decision 13A): half of each rate; a name it keeps only
        /// where half still holds it by the usual rule, and "walled" never on land with no walls.
        /// </summary>
        public static ColonyTraits HalfOf(ColonyTraits mother, bool walls)
        {
            var t = new ColonyTraits();
            if (mother == null) return t;
            foreach (var kv in mother.rates) t.rates[kv.Key] = ManyColoniesMath.FoundedValue(kv.Value);
            foreach (var name in mother.known)
            {
                var d = DefDatabase<ColonyTraitDef>.GetNamedSilentFail(name);
                if (d == null || name == ManyColoniesMath.WalledTrait && !walls) continue;
                if (ColonyTraitMath.Known(true, t.Rate(d), d.knownAt, LoseShare)) t.known.Add(name);
            }
            return t;
        }

        /// <summary>How the colony's old hands lean on this way, from what it has grown into (see <see cref="ColonyTraitDef.lean"/>).</summary>
        public float Lean(Way w)
        {
            if (known.Count == 0) return 0f;
            float sum = 0f;
            foreach (var d in Known)
                if (d.lean != null) sum += d.lean.Get(w);
            return sum;
        }

        /// <summary>How much sooner camps come after the last one, from what the colony is known for (1: no change).</summary>
        public float CampGap
        {
            get
            {
                if (known.Count == 0) return 1f;
                float f = 1f;
                foreach (var d in Known) f *= d.campGap;
                return Mathf.Clamp(f, 0.4f, 2f);
            }
        }

        public bool IsKnown(ColonyTraitDef d) => d != null && known.Contains(d.defName);

        public IEnumerable<ColonyTraitDef> Known => All.Where(IsKnown);

        public float Rate(ColonyTraitDef d) => d != null && rates.TryGetValue(d.defName, out float r) ? r : 0f;

        /// <summary>How close to being known, 0..1 (1 and over: known).</summary>
        public float Progress(ColonyTraitDef d) => d == null ? 0f : Rate(d) / d.knownAt;

        public bool HasTag(string tag) => !tag.NullOrEmpty() && known.Count > 0 && Known.Any(d => d.tag == tag || d.defName == tag);

        /// <summary>Once a day: what everyone did since yesterday, by record, folded into each trait's average.</summary>
        public void Daily(CouncilGameComponent comp, CharacterTracker of = null)
        {
            var defs = All.ToList();
            var tallied = today;
            today = new Dictionary<string, float>();
            if (defs.Count == 0) return;
            // Each colony's own people (decision 13A); with one colony, everyone, as always.
            bool several = ColonyState.Several;
            var home = several ? of?.Map : null;
            var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => p.records != null && p.RaceProps.Humanlike && !p.IsQuestLodger() && !p.IsSlave
                                                                          && (home == null || Homes.Of(p) == home)).ToList();
            if (colonists.Count == 0) return;
            days++;
            DailyTallies(tallied, home);
            letterMap = home;
            walledOk = home == null || HasWalls(home);
            var recordsUsed = defs.SelectMany(d => d.records).Where(r => r?.record != null).Select(r => r.record).Distinct().ToList();
            var delta = new Dictionary<RecordDef, float>();
            var seen = new HashSet<string>();
            foreach (var p in colonists)
            {
                foreach (var r in recordsUsed)
                {
                    string key = r.defName + "|" + p.ThingID;
                    seen.Add(key);
                    float now = p.records.GetValue(r);
                    // A newcomer's past isn't the colony's: they count from the day they're first seen here.
                    if (last.TryGetValue(key, out float before) && now > before) delta[r] = (delta.TryGetValue(r, out float d) ? d : 0f) + (now - before);
                    last[key] = now;
                }
            }
            foreach (var k in last.Keys.Where(k => !seen.Contains(k)).ToList()) last.Remove(k);
            // The first day only sets the starting counts.
            if (days <= 1) return;
            foreach (var def in defs)
            {
                float rate;
                if (!def.level.NullOrEmpty()) rate = Level(def.level, home);
                else
                {
                    float amount = 0f;
                    foreach (var r in def.records)
                        if (r?.record != null && delta.TryGetValue(r.record, out float d)) amount += d * r.weight;
                    if (def.tallies != null)
                        foreach (var t in def.tallies)
                            if (t?.key != null && tallied.TryGetValue(t.key, out float n)) amount += n * t.weight;
                    rate = ColonyTraitMath.Fold(Rate(def), def.perColony ? amount : amount / colonists.Count, Alpha);
                }
                rates[def.defName] = rate;
                Check(comp, def, rate, colonists.Count);
            }
        }

        /// <summary>
        /// The tallies counted once a day rather than as they happen: guests staying (Hospitality's, a night each), and
        /// a chaplain in the seat or a moral guide in the faith's role (Ideology).
        /// </summary>
        private static void DailyTallies(Dictionary<string, float> tallied, Map home)
        {
            void Add(string key, float n)
            {
                if (n > 0f) tallied[key] = (tallied.TryGetValue(key, out float v) ? v : 0f) + n;
            }
            try
            {
                // With more than one colony, each counts its own guests, chaplain and moral guide (home); with one, as always.
                int guests = 0;
                foreach (var map in Find.Maps)
                    if (map.IsPlayerHome && (home == null || map == home))
                        guests += Strangers.On(map, StrangerKind.Visitor).Count(Strangers.IsHospitalityGuest);
                Add("guestNight", guests);
                var comp = CouncilGameComponent.Instance;
                if (ModsConfig.IdeologyActive && comp != null)
                {
                    var offices = home != null ? ColonyState.For(home)?.offices : comp.offices;
                    if (ColonyCouncilMod.Settings.offices && (offices?.Filled("Council_Office_Chaplain") ?? false)) Add("chaplainDay", 1f);
                    if (PawnsFinder.AllMaps_FreeColonists.Any(p => NameRecords.IsMoralGuide(p) && (home == null || Homes.Of(p) == home))) Add("guideDay", 1f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony traits: daily tallies", e);
            }
        }

        /// <summary>The day's colony (its letters name the settlement with more than one), and whether it has walls enough to be called walled.</summary>
        [Unsaved] private Map letterMap;
        [Unsaved] private bool walledOk = true;

        private void Check(CouncilGameComponent comp, ColonyTraitDef def, float rate, int colonists)
        {
            bool isKnown = IsKnown(def);
            // "Walled" is never said of a colony with no walls (decision 13A): kept from it, and let go once the walls are gone.
            bool needsWalls = def.defName == ManyColoniesMath.WalledTrait && !walledOk;
            if (isKnown && needsWalls)
            {
                known.Remove(def.defName);
                string gone = def.lost.NullOrEmpty() ? "CC_Ways_TraitFading".Translate(def.label).Resolve() : def.lost;
                Tell.Minor(NewsKind.Colony, Importance.Notable, letterMap, gone, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            if (!isKnown && !needsWalls && ColonyTraitMath.Known(false, rate, def.knownAt, LoseShare) && colonists >= 3 && days >= 15)
            {
                known.Add(def.defName);
                string text = def.earned.NullOrEmpty() ? def.description : def.earned;
                string label = "CC_Ways_TraitLabel".Translate(def.label.CapitalizeFirst()).Resolve();
                // Those who love the work take pride in it; a name with no one skill behind it is simply the colony's.
                if (def.skill != null) text += "\n\n" + "CC_Ways_TraitPride".Translate(def.skill.label).Resolve();
                Tell.Letter(NewsKind.Colony, Importance.Notable, letterMap, label, text, LetterDefOf.PositiveEvent);
                // The chronicle's sentence is the colony's one name's, at the end of the day (ColonyName.Look).
            }
            else if (isKnown && !ColonyTraitMath.Known(true, rate, def.knownAt, LoseShare))
            {
                known.Remove(def.defName);
                string text = def.lost.NullOrEmpty() ? "CC_Ways_TraitFading".Translate(def.label).Resolve() : def.lost;
                if (letterMap != null) Tell.Minor(NewsKind.Colony, Importance.Notable, letterMap, text, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                else News.Post(text, LookTargets.Invalid, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        /// <summary>The traits this colonist takes pride in (the colony's, matching their passions).</summary>
        public List<ColonyTraitDef> PrideOf(Pawn p)
        {
            var list = new List<ColonyTraitDef>();
            if (p?.skills == null || known.Count == 0) return list;
            foreach (var d in Known)
            {
                if (d.skill == null) continue;
                var rec = p.skills.GetSkill(d.skill);
                if (rec != null && !rec.TotallyDisabled && People.Passionate(rec)) list.Add(d);
            }
            return list;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref rates, "rates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref known, "known", LookMode.Value);
            Scribe_Values.Look(ref days, "days");
            Scribe_Collections.Look(ref today, "today", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (today == null) today = new Dictionary<string, float>();
                if (rates == null) rates = new Dictionary<string, float>();
                if (last == null) last = new Dictionary<string, float>();
                if (known == null) known = new List<string>();
                known.RemoveAll(k => DefDatabase<ColonyTraitDef>.GetNamedSilentFail(k) == null);
            }
        }
    }

    /// <summary>Pride in what the colony has become known for, for those who love that work.</summary>
    public class ThoughtWorker_ColonyTrait : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !s.colonyCharacter || !s.MoodEffects || !p.IsColonist || p.IsQuestLodger() || !People.HasInnerLife(p)) return ThoughtState.Inactive;
            // What their own colony is known for (each colony its own character, decision 13A).
            var traits = CharacterTracker.Of(p)?.traits;
            return traits != null && traits.PrideOf(p).Count > 0 ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    public class Thought_ColonyTrait : Thought_Situational
    {
        /// <summary>Like pride in the colony's ways: it lasts as long as the trait, so it shrinks for the spoiled.</summary>
        public override float MoodOffset() => base.MoodOffset() * GoodLife.RewardScale(pawn);

        public override string Description
        {
            get
            {
                var traits = CharacterTracker.Of(pawn)?.traits?.PrideOf(pawn);
                string what = traits == null || traits.Count == 0 ? null : "CC_Ways_TraitWhy".Translate(TextUtil.JoinAnd(traits.Select(t => t.label).ToList())).Resolve();
                return Causes.Append(base.Description, what);
            }
        }
    }
}
