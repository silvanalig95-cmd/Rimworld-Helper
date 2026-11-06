using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The five ways a colony can be known by. Each runs from one pole (negative) to the other (positive).</summary>
    public enum Way
    {
        /// <summary>harsh (-) to merciful (+)</summary>
        Mercy,
        /// <summary>thrifty (-) to generous (+)</summary>
        Generosity,
        /// <summary>restless (-) to traditional (+)</summary>
        Tradition,
        /// <summary>guarded (-) to open (+)</summary>
        Openness,
        /// <summary>easygoing (-) to hard-working (+)</summary>
        Toil
    }

    /// <summary>A nudge to the colony's ways, in XML: &lt;ways&gt;&lt;mercy&gt;8&lt;/mercy&gt;&lt;openness&gt;-4&lt;/openness&gt;&lt;/ways&gt;.</summary>
    public class WayShift
    {
        public float mercy;
        public float generosity;
        public float tradition;
        public float openness;
        public float toil;

        public float Get(Way w)
        {
            switch (w)
            {
                case Way.Mercy: return mercy;
                case Way.Generosity: return generosity;
                case Way.Tradition: return tradition;
                case Way.Openness: return openness;
                default: return toil;
            }
        }

        public bool Any => mercy != 0f || generosity != 0f || tradition != 0f || openness != 0f || toil != 0f;

        public WayShift Scaled(float f) => new WayShift { mercy = mercy * f, generosity = generosity * f, tradition = tradition * f, openness = openness * f, toil = toil * f };

        /// <summary>"more merciful, a little more guarded".</summary>
        public string Summary()
        {
            var parts = new List<string>();
            foreach (Way w in Enum.GetValues(typeof(Way)))
            {
                float v = Get(w);
                if (Mathf.Abs(v) < 0.5f) continue;
                string pole = CharacterTracker.PoleLabel(w, v > 0f);
                parts.Add((Mathf.Abs(v) >= 6f ? "CC_Ways_More" : "CC_Ways_Bit").Translate(pole).Resolve());
            }
            return string.Join(", ", parts);
        }
    }

    public class CharacterEvent : IExposable
    {
        public string what;
        public string shift;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref shift, "shift");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// The colony's character: what its council's choices, decrees and treatment of strangers have made it known
    /// for. Old deeds fade (about half in a year), so a colony can change its name. Colonists approve or chafe
    /// by nature; visitors and some dilemmas react to the colony's reputation.
    /// </summary>
    public class CharacterTracker : IExposable
    {
        /// <summary>Known for it at 25, renowned at 60 (and a little lower to lose it again).</summary>
        public const float Known = 25f;
        public const float Renowned = 60f;
        private const float Hysteresis = 5f;

        private List<float> values = new List<float> { 0f, 0f, 0f, 0f, 0f };
        private List<int> tiers = new List<int> { 0, 0, 0, 0, 0 };
        public List<CharacterEvent> recent = new List<CharacterEvent>();
        private int lastWordTick = -999999;

        // Not saved: counts up whenever the ways move, so approval kept for the mood (ApprovalKept) is worked out again.
        private int version;
        private readonly KeptAnswers<float> approvals = new KeptAnswers<float>(Sched.RareTicks);
        /// <summary>What the colony grows into by what its people do (green-fingered, builders...).</summary>
        public ColonyTraits traits = new ColonyTraits();
        /// <summary>
        /// The colony's one name as last looked at (ColonyName.Key: what it is called, what it is known for beyond its walls),
        /// so a change is told once, in one sentence. Null until the first look (a new game, an older save): set without a word.
        /// </summary>
        public string nameKey;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static readonly Way[] All = (Way[])Enum.GetValues(typeof(Way));

        public float Value(Way w) => values[(int)w];

        /// <summary>-2 (renowned for the negative pole) .. 2.</summary>
        public int Tier(Way w) => tiers[(int)w];

        public static string PoleLabel(Way w, bool positive) => ("CC_Way_" + w + (positive ? "_Pos" : "_Neg")).Translate();

        /// <summary>Tags used by dilemmas and encounters: Merciful, Harsh, Generous, Thrifty, Traditional, Restless, Open, Guarded, Hardworking, Easygoing.</summary>
        private static readonly Dictionary<string, (Way way, int sign)> TagMap = new Dictionary<string, (Way, int)>
        {
            { "Merciful", (Way.Mercy, 1) }, { "Harsh", (Way.Mercy, -1) },
            { "Generous", (Way.Generosity, 1) }, { "Thrifty", (Way.Generosity, -1) },
            { "Traditional", (Way.Tradition, 1) }, { "Restless", (Way.Tradition, -1) },
            { "Open", (Way.Openness, 1) }, { "Guarded", (Way.Openness, -1) },
            { "Hardworking", (Way.Toil, 1) }, { "Easygoing", (Way.Toil, -1) }
        };

        /// <summary>
        /// Every name a letter's lines can ask for as [colony:Name]: the ten of the ways, and what the colony has grown
        /// into (GreenFingered, Hospitable, Devout...).
        /// </summary>
        public static IEnumerable<string> TagNames
        {
            get
            {
                foreach (var k in TagMap.Keys) yield return k;
                foreach (var d in ColonyTraits.All)
                    if (!d.tag.NullOrEmpty()) yield return d.tag;
            }
        }

        public bool Is(string tag) => S.colonyCharacter && (TagMap.TryGetValue(tag, out var t) ? Math.Sign(Tier(t.way)) == t.sign : traits.HasTag(tag));

        public bool Fits(List<string> isAll, List<string> isNot)
        {
            if (!isAll.NullOrEmpty() && !isAll.All(Is)) return false;
            if (!isNot.NullOrEmpty() && isNot.Any(Is)) return false;
            return true;
        }

        /// <summary>"merciful and generous", or null while the colony has no particular name.</summary>
        public string Describe()
        {
            var parts = All.Where(w => Tier(w) != 0).OrderByDescending(w => Mathf.Abs(Value(w))).Select(w => PoleLabel(w, Tier(w) > 0)).ToList();
            return parts.Count == 0 ? null : TextUtil.JoinAnd(parts);
        }

        public void Shift(WayShift s, string why)
        {
            if (!S.colonyCharacter || s == null || !s.Any) return;
            // The name as it stood before this, for a game or an older save not yet looked at (kept without a word).
            if (nameKey == null) ColonyName.Look(CouncilGameComponent.Instance);
            foreach (var w in All)
            {
                float amt = s.Get(w);
                if (amt == 0f) continue;
                int i = (int)w;
                float v = values[i];
                // Further along a road is harder to go: deeds count for less the more known the colony already is.
                if (Mathf.Sign(amt) == Mathf.Sign(v)) amt *= 1f - Mathf.Abs(v) / 130f;
                values[i] = Mathf.Clamp(v + amt, -100f, 100f);
            }
            version++;
            recent.Add(new CharacterEvent { what = why, shift = s.Summary(), tick = Find.TickManager.TicksGame });
            if (recent.Count > 30) recent.RemoveAt(0);
            var comp = CouncilGameComponent.Instance;
            if (Check(comp)) ColonyName.Look(comp);
        }

        public void Daily(CouncilGameComponent comp)
        {
            if (!S.colonyCharacter) return;
            if (nameKey == null) ColonyName.Look(comp);
            for (int i = 0; i < values.Count; i++) values[i] *= 0.988f;
            version++;
            Check(comp);
            Word(comp);
            try
            {
                traits.Daily(comp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony traits", e);
            }
            // The day's changes to the colony's name, its ways and what the work made it, in one sentence.
            ColonyName.Look(comp);
        }

        private int TierFor(float v, int current)
        {
            float a = Mathf.Abs(v);
            int sign = v >= 0f ? 1 : -1;
            // A little slack before losing a reputation, so it doesn't flicker at the line.
            float known = Math.Sign(current) == sign && Math.Abs(current) >= 1 ? Known - Hysteresis : Known;
            float renowned = Math.Sign(current) == sign && Math.Abs(current) >= 2 ? Renowned - Hysteresis : Renowned;
            if (a >= renowned) return 2 * sign;
            if (a >= known) return sign;
            return 0;
        }

        /// <summary>
        /// The ways' tiers against their values: a way gained is told in a letter, one faded in the news. Whether any moved.
        /// The chronicle's sentence is the colony's one name's (ColonyName.Look), with what the work made it, never a line here.
        /// </summary>
        private bool Check(CouncilGameComponent comp)
        {
            if (comp == null) return false;
            bool moved = false;
            foreach (var w in All)
            {
                int i = (int)w;
                int was = tiers[i];
                int now = TierFor(values[i], was);
                if (now == was) continue;
                tiers[i] = now;
                moved = true;
                bool gained = now != 0 && (was == 0 || Math.Sign(now) != Math.Sign(was) || Math.Abs(now) > Math.Abs(was));
                string pole = PoleLabel(w, (now != 0 ? now : was) > 0);
                if (gained)
                {
                    string label = (Math.Abs(now) == 2 ? "CC_Ways_RenownedLabel" : "CC_Ways_KnownLabel").Translate(pole.CapitalizeFirst());
                    string text = (Math.Abs(now) == 2 ? "CC_Ways_Renowned" : "CC_Ways_Known").Translate(pole) + "\n\n" + ("CC_Ways_Meaning_" + w + (now > 0 ? "_Pos" : "_Neg")).Translate();
                    Tell.Letter(NewsKind.Colony, Importance.Notable, null, label, text, LetterDefOf.NeutralEvent);
                }
                else
                {
                    // Renowned to known: the neighbors still call it that, only not far and wide ("people have stopped calling
                    // the colony merciful" wasn't true of it).
                    string text = (now != 0 ? "CC_Name_Ways_Narrower" : "CC_Ways_Fading").Translate(pole).Resolve();
                    News.Post(text, LookTargets.Invalid, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                }
            }
            return moved;
        }

        /// <summary>Word gets around: a generous or open colony's neighbors warm to it a little; a thrifty or guarded one's cool.</summary>
        private void Word(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastWordTick < GenDate.TicksPerDay * 10) return;
            float g = Value(Way.Generosity), o = Value(Way.Openness);
            int amount = g >= 40f || o >= 40f ? 2 : g <= -40f || o <= -40f ? -1 : 0;
            if (amount == 0) return;
            var faction = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer) && !f.temporary)
                .RandomElementWithFallback();
            if (faction == null) return;
            lastWordTick = now;
            faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, false, false);
            string key = amount > 0 ? "CC_Ways_WordGood" : "CC_Ways_WordBad";
            string what = PoleLabel(Mathf.Abs(g) >= Mathf.Abs(o) ? Way.Generosity : Way.Openness, amount > 0);
            News.Post(key.Translate(faction.Name, what, amount.ToStringWithSign()).Resolve(), LookTargets.Invalid,
                NewsKind.World, amount > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, Importance.Flavor);
        }

        // ---------------- Who approves ----------------

        /// <summary>
        /// How much this colonist cares about each way (-1 = dislikes it, +1 = likes it), read from their character
        /// (Persona/: the eight sides the game's traits, skills and age set and what happened to them moves,
        /// PersonaMath.WayLean), the one reading of who someone is; then their kind and their beliefs (ModPeopleDef: a
        /// people's ways, a pacifist creed), which aren't nature. A mod's trait reaches it through character too.
        /// </summary>
        public static float Affinity(Pawn p, Way w) => Affinity(p, w, p?.story == null ? null : Persona.Values(p));

        /// <summary>The same with their character's sides already read (the mood asks every way in a row).</summary>
        public static float Affinity(Pawn p, Way w, float[] sides)
        {
            if (p?.story == null) return 0f;
            float a = PersonaMath.WayLean((int)w, sides);
            a += ModPeople.WayAffinity(p, w);
            return Mathf.Clamp(a, -PersonaMath.WayMost, PersonaMath.WayMost);
        }

        /// <summary>
        /// <see cref="Approval"/> as the mood asks for it (every colonist, every few seconds): kept per colonist until the
        /// ways move, and for at most 250 ticks, so a trait gained or a birthday shows within a few seconds.
        /// </summary>
        public float ApprovalKept(Pawn p)
        {
            if (p == null) return 0f;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (approvals.TryGet(p.thingIDNumber, now, version, out float a)) return a;
            a = Approval(p);
            approvals.Set(p.thingIDNumber, now, version, a);
            return a;
        }

        /// <summary>Overall approval of the colony's ways (around -100..100), and the reasons.</summary>
        public float Approval(Pawn p, List<string> reasons = null)
        {
            float score = 0f;
            float[] sides = null;
            foreach (var w in All)
            {
                float v = Value(w);
                if (Mathf.Abs(v) < 10f) continue;
                sides ??= p?.story == null ? null : Persona.Values(p);
                float a = Affinity(p, w, sides);
                if (a == 0f) continue;
                float part = v * a;
                score += part;
                if (reasons != null && Mathf.Abs(part) >= 5f)
                    reasons.Add((part > 0f ? "CC_Ways_Likes" : "CC_Ways_Dislikes").Translate(PoleLabel(w, v > 0f)).Resolve());
            }
            return score;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref values, "values", LookMode.Value);
            Scribe_Collections.Look(ref tiers, "tiers", LookMode.Value);
            Scribe_Collections.Look(ref recent, "recent", LookMode.Deep);
            Scribe_Values.Look(ref lastWordTick, "lastWordTick", -999999);
            Scribe_Deep.Look(ref traits, "traits");
            Scribe_Values.Look(ref nameKey, "nameKey");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (traits == null) traits = new ColonyTraits();
                if (values == null || values.Count != All.Length) values = new List<float> { 0f, 0f, 0f, 0f, 0f };
                if (tiers == null || tiers.Count != All.Length) tiers = new List<int> { 0, 0, 0, 0, 0 };
                if (recent == null) recent = new List<CharacterEvent>();
                recent.RemoveAll(e => e == null);
            }
        }
    }

    /// <summary>How a colonist feels about the colony's ways: pride, or chafing.</summary>
    public class ThoughtWorker_ColonyWays : ThoughtWorker
    {
        /// <summary>How strongly a colonist must approve (or disapprove) before it shows in their mood.</summary>
        public const float Mild = 18f;
        public const float Strong = 45f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            var comp = CouncilGameComponent.Instance;
            if (comp?.character == null || !s.colonyCharacter || !s.MoodEffects || !p.IsColonist || p.IsQuestLodger() || !People.HasInnerLife(p) || p.DevelopmentalStage.Baby()) return ThoughtState.Inactive;
            float a = comp.character.ApprovalKept(p);
            if (a >= Strong) return ThoughtState.ActiveAtStage(3);
            if (a >= Mild) return ThoughtState.ActiveAtStage(2);
            if (!s.Negatives) return ThoughtState.Inactive;
            if (a <= -Strong) return ThoughtState.ActiveAtStage(0);
            if (a <= -Mild) return ThoughtState.ActiveAtStage(1);
            return ThoughtState.Inactive;
        }
    }

    public class Thought_ColonyWays : Thought_Situational, IExplainedThought
    {
        /// <summary>
        /// Pride in the colony's ways lasts as long as the ways do, so like the council's other lasting rewards it
        /// follows the reward strength and shrinks for the spoiled, who come to take it for granted.
        /// </summary>
        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            return m < 0f ? m * ColonyCouncilMod.Settings.PenaltyScale : m * GoodLife.RewardScale(pawn);
        }

        public string Why
        {
            get
            {
                var reasons = new List<string>();
                CouncilGameComponent.Instance?.character.Approval(pawn, reasons);
                return reasons.Count > 0 ? string.Join("; ", reasons) + "." : null;
            }
        }

        public override string Description => Causes.Append(base.Description, Why);
    }
}
