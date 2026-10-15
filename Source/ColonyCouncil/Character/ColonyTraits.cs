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
    /// Something the colony grows into by what its people do, beside the five ways its council's choices shape:
    /// green-fingered, builders, learned. Measured from the colonists' own records as a rolling daily average per
    /// head, so it comes with steady work and fades when the work stops. Those with a passion for the work are proud
    /// of it.
    /// </summary>
    public class ColonyTraitDef : Def
    {
        public List<ColonyTraitRecord> records = new List<ColonyTraitRecord>();
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

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (records.NullOrEmpty()) yield return "colony trait has no records";
            if (knownAt <= 0f) yield return "knownAt must be above zero";
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

        public static IEnumerable<ColonyTraitDef> All => DefDatabase<ColonyTraitDef>.AllDefsListForReading.OrderBy(d => d.order);

        public bool IsKnown(ColonyTraitDef d) => d != null && known.Contains(d.defName);

        public IEnumerable<ColonyTraitDef> Known => All.Where(IsKnown);

        public float Rate(ColonyTraitDef d) => d != null && rates.TryGetValue(d.defName, out float r) ? r : 0f;

        /// <summary>How close to being known, 0..1 (1 and over: known).</summary>
        public float Progress(ColonyTraitDef d) => d == null ? 0f : Rate(d) / d.knownAt;

        public bool HasTag(string tag) => !tag.NullOrEmpty() && Known.Any(d => d.tag == tag || d.defName == tag);

        /// <summary>Once a day: what everyone did since yesterday, by record, folded into each trait's average.</summary>
        public void Daily(CouncilGameComponent comp)
        {
            var defs = DefDatabase<ColonyTraitDef>.AllDefsListForReading;
            if (defs.Count == 0) return;
            var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => p.records != null && p.RaceProps.Humanlike && !p.IsQuestLodger() && !p.IsSlave).ToList();
            if (colonists.Count == 0) return;
            days++;
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
                float amount = 0f;
                foreach (var r in def.records)
                    if (r?.record != null && delta.TryGetValue(r.record, out float d)) amount += d * r.weight;
                float perHead = amount / colonists.Count;
                float rate = Rate(def) * (1f - Alpha) + perHead * Alpha;
                rates[def.defName] = rate;
                Check(comp, def, rate, colonists.Count);
            }
        }

        private void Check(CouncilGameComponent comp, ColonyTraitDef def, float rate, int colonists)
        {
            bool isKnown = IsKnown(def);
            if (!isKnown && rate >= def.knownAt && colonists >= 3 && days >= 15)
            {
                known.Add(def.defName);
                string text = def.earned.NullOrEmpty() ? def.description : def.earned;
                string label = "CC_Ways_TraitLabel".Translate(def.label.CapitalizeFirst()).Resolve();
                Find.LetterStack.ReceiveLetter(label, text + "\n\n" + "CC_Ways_TraitPride".Translate(def.skill?.label ?? "").Resolve(), LetterDefOf.PositiveEvent);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, label, null));
            }
            else if (isKnown && rate < def.knownAt * LoseShare)
            {
                known.Remove(def.defName);
                string text = def.lost.NullOrEmpty() ? "CC_Ways_TraitFading".Translate(def.label).Resolve() : def.lost;
                News.Post(text, LookTargets.Invalid, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, text, null));
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
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
            if (comp?.character == null || !s.colonyCharacter || !s.MoodEffects || !p.IsColonist || p.IsQuestLodger() || !People.HasInnerLife(p)) return ThoughtState.Inactive;
            return comp.character.traits.PrideOf(p).Count > 0 ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    public class Thought_ColonyTrait : Thought_Situational
    {
        /// <summary>Like pride in the colony's ways: it lasts as long as the trait, so it shrinks for the spoiled.</summary>
        public override float MoodOffset() => base.MoodOffset() * SmallJoys.LiveScale(pawn);

        public override string Description
        {
            get
            {
                var traits = CouncilGameComponent.Instance?.character?.traits.PrideOf(pawn);
                string what = traits == null || traits.Count == 0 ? null : "CC_Ways_TraitWhy".Translate(TextUtil.JoinAnd(traits.Select(t => t.label).ToList())).Resolve();
                return Causes.Append(base.Description, what);
            }
        }
    }
}
