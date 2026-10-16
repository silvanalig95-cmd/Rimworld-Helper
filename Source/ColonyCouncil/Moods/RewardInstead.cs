using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Not every good moment has to be a good mood. Put on one of Rimfolk's memory ThoughtDefs whose stages give no mood:
    /// when the memory is gained, the colonist gets this instead. A joke or a game gives a little recreation, a quiet
    /// moment some rest or comfort, talking shop or a lesson practice in a skill, a stirring sight a small chance of an
    /// inspiration, a civic moment trust in the council, a traveler's knowledge a little research, and the smallest
    /// touches only a line in the journal. A memory about someone keeps its opinion either way, so a warm talk still
    /// changes how two people see each other.
    /// <para>The memory itself stays, with no mood: whatever looks for it (moments, the journal, dreams, quirks) still
    /// finds it. The heart reads it as a good moment of <see cref="felt"/> (its feelings, belonging, strain eased among
    /// friends), and <see cref="feelings"/> names feelings it feeds besides those that already list it.</para>
    /// <para>Scaled like the mood it replaces: the reward strength setting and the colonist's standards (dreams in full,
    /// as their moods are); nothing at all on "Advisor only". Applied once per memory gained, through the memory classes
    /// (Thought_CouncilMemory, Thought_CouncilSocial, Thought_Dream), so it needs no game hook.</para>
    /// </summary>
    public class RewardInstead : DefModExtension
    {
        /// <summary>Recreation: this share of the bar (a joke 0.05, an evening of games 0.15), of <see cref="joyKind"/> (social when empty).</summary>
        public float joy;
        public JoyKindDef joyKind;
        /// <summary>Rest and comfort: this share of the bar.</summary>
        public float rest;
        public float comfort;
        /// <summary>
        /// Practice: <see cref="xp"/> experience in this skill, or in one picked by <see cref="skillFrom"/>: "shared" (a
        /// passion they share with the other person), "theirs" (what the other person is clearly better at: watching a
        /// master) or "mine" (one of their own passions). A skill they can't use is skipped.
        /// </summary>
        public SkillDef skill;
        public string skillFrom;
        public float xp;
        /// <summary>A small chance of an inspiration: one of these (InspirationDef names, the game's or a mod's), or any that can come.</summary>
        public float inspiration;
        public List<string> inspirations;
        /// <summary>Trust in the council.</summary>
        public float trust;
        /// <summary>Research points toward the project under way (never finishing it).</summary>
        public float research;
        /// <summary>Written in their journal, in their own words (the memory's description).</summary>
        public bool journal;
        /// <summary>How big a good moment the heart takes it for, though it adds no mood (usually the mood it used to give).</summary>
        public float felt;
        /// <summary>Feelings it feeds (keys or defNames), on top of those that list the thought already.</summary>
        public List<string> feelings;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (xp > 0f && skill == null && skillFrom.NullOrEmpty()) yield return "rewardInstead gives xp without a skill or skillFrom";
            if (!skillFrom.NullOrEmpty() && skillFrom != "shared" && skillFrom != "theirs" && skillFrom != "mine") yield return "skillFrom must be shared, theirs or mine";
            if (joy < 0f || rest < 0f || comfort < 0f || xp < 0f || research < 0f || felt < 0f) yield return "rewardInstead amounts can't be negative";
            if (inspiration > 0.25f) yield return "an inspiration instead of mood should stay rare (0.25 at most)";
        }

        // ---------------- Given ----------------

        /// <summary>
        /// The same reward to the same person at most once an hour: a memory given twice in a row renews, it isn't a
        /// second moment. Not saved (after a load, a time in the future is simply over).
        /// </summary>
        private static readonly Dictionary<(int, ushort), int> lastGiven = new Dictionary<(int, ushort), int>();

        private static readonly Color MoteColor = new Color(0.78f, 0.9f, 0.78f);

        public static bool On(ThoughtDef def) => def?.GetModExtension<RewardInstead>() != null;

        /// <summary>
        /// A memory has just been gained (its class calls this from TryMergeWithExistingMemory, the one step every memory
        /// passes once the game has decided the colonist gets it).
        /// </summary>
        public static void Gained(Thought_Memory t)
        {
            var x = t?.def?.GetModExtension<RewardInstead>();
            if (x == null) return;
            try
            {
                x.Give(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reward instead of mood " + t.def.defName, e);
            }
        }

        /// <summary>The council's scale, as it would have been on the mood: reward strength and standards (dreams in full).</summary>
        private static float ScaleOf(Thought_Memory t, Pawn p)
        {
            float s;
            switch (t)
            {
                case Thought_CouncilMemory m:
                    s = m.scaled ? m.moodPowerFactor : SmallJoys.LiveScale(p);
                    break;
                case Thought_CouncilSocial m:
                    s = m.scaled ? m.moodPowerFactor : SmallJoys.LiveScale(p);
                    break;
                default:
                    s = 1f;
                    break;
            }
            return Mathf.Clamp(s, 0f, 2f);
        }

        private void Give(Thought_Memory t)
        {
            var p = t.pawn;
            var s = ColonyCouncilMod.Settings;
            if (p == null || p.Dead || s == null || !s.MoodEffects || Find.TickManager == null) return;
            int now = Find.TickManager.TicksGame;
            var key = (p.thingIDNumber, t.def.shortHash);
            if (lastGiven.TryGetValue(key, out int when) && when <= now && now - when < GenDate.TicksPerHour) return;
            if (lastGiven.Count > 600) lastGiven.Clear();
            lastGiven[key] = now;
            float scale = ScaleOf(t, p);
            if (scale <= 0f) return;

            var parts = new List<string>(3);
            var needs = p.needs;
            if (joy > 0f && needs?.joy != null)
            {
                needs.joy.GainJoy(joy * scale, joyKind ?? JoyKindDefOf.Social);
                parts.Add("CC_Instead_Joy".Translate());
            }
            if (rest > 0f && needs?.rest != null)
            {
                needs.rest.CurLevel += rest * scale;
                parts.Add("CC_Instead_Rest".Translate());
            }
            if (comfort > 0f && needs?.comfort != null)
            {
                needs.comfort.CurLevel += comfort * scale;
                parts.Add("CC_Instead_Comfort".Translate());
            }
            if (xp > 0f)
            {
                var rec = PickSkill(p, t.otherPawn);
                if (rec != null)
                {
                    rec.Learn(xp * scale);
                    parts.Add(rec.def.skillLabel ?? rec.def.label);
                }
            }
            var comp = CouncilGameComponent.Instance;
            if (trust != 0f && comp != null && p.IsColonist)
            {
                comp.standings.Change(p, trust * scale, t.CurStage?.label?.CapitalizeFirst());
                parts.Add("CC_Instead_Trust".Translate());
            }
            if (research > 0f && Research(research * scale)) parts.Add("CC_Instead_Research".Translate());
            if (inspiration > 0f && Rand.Chance(inspiration * scale)) Inspire(p, t);
            if (journal && comp != null)
            {
                string line = Line(t);
                if (!line.NullOrEmpty()) comp.journals.WriteText(p, line);
            }
            Show(p, parts);
        }

        /// <summary>The skill this practice goes to, or null (none fits, or they can't learn it).</summary>
        private SkillRecord PickSkill(Pawn p, Pawn other)
        {
            var skills = p.skills;
            if (skills == null) return null;
            if (skill != null)
            {
                var rec = skills.GetSkill(skill);
                return rec != null && !rec.TotallyDisabled ? rec : null;
            }
            SkillRecord best = null;
            float bestScore = float.MinValue;
            if (skillFrom != "mine" && other?.skills != null)
                foreach (var s in skills.skills)
                {
                    if (s == null || s.TotallyDisabled) continue;
                    var o = other.skills.GetSkill(s.def);
                    if (o == null || o.TotallyDisabled) continue;
                    float score;
                    if (skillFrom == "shared")
                    {
                        if (!People.Passionate(s) || !People.Passionate(o)) continue;
                        score = Rand.Value;
                    }
                    else
                    {
                        if (o.Level < s.Level + 3) continue;
                        score = o.Level - s.Level + Rand.Value;
                    }
                    if (score > bestScore)
                    {
                        best = s;
                        bestScore = score;
                    }
                }
            if (best != null) return best;
            // Their own passion, else what they're best at: practice all the same.
            foreach (var s in skills.skills)
            {
                if (s == null || s.TotallyDisabled) continue;
                float score = (People.Passionate(s) ? 100f : 0f) + s.Level + Rand.Value;
                if (score > bestScore)
                {
                    best = s;
                    bestScore = score;
                }
            }
            return best;
        }

        private static bool Research(float amount)
        {
            var rm = Find.ResearchManager;
            var proj = rm?.GetProject();
            if (proj == null || proj.IsFinished) return false;
            float left = proj.Cost - rm.GetProgress(proj);
            amount = Mathf.Min(amount, left - 1f);
            if (amount < 1f) return false;
            rm.AddProgress(proj, amount);
            return true;
        }

        private void Inspire(Pawn p, Thought_Memory t)
        {
            var handler = p.mindState?.inspirationHandler;
            if (handler == null || handler.Inspired || p.Downed || p.InMentalState) return;
            InspirationDef pick = null;
            if (!inspirations.NullOrEmpty())
            {
                var fits = new List<InspirationDef>();
                foreach (var n in inspirations)
                {
                    var d = DefDatabase<InspirationDef>.GetNamedSilentFail(n);
                    if (d != null && d.Worker.InspirationCanOccur(p)) fits.Add(d);
                }
                if (fits.Count > 0) pick = fits.RandomElement();
            }
            pick ??= handler.GetRandomAvailableInspirationDef();
            if (pick == null) return;
            string why = "CC_Instead_Inspired".Translate(p.Named("PAWN"), (t.LabelCap ?? "").ToString().UncapitalizeFirst().Named("MOMENT")).Resolve();
            handler.TryStartInspiration(pick, why);
        }

        /// <summary>The journal line: the memory's description, in their own words.</summary>
        private static string Line(Thought_Memory t)
        {
            string raw = t.CurStage?.description;
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(t.pawn.Named("PAWN")).Resolve();
        }

        /// <summary>
        /// A small line over their head saying what they got: always floating text, since it isn't something they say,
        /// and shown while speech shows at all (Immersion/Speech.cs).
        /// </summary>
        private static void Show(Pawn p, List<string> parts)
        {
            if (parts.Count == 0 || !Speech.On || !p.Spawned || p.Map != Find.CurrentMap) return;
            MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.6f), p.Map, "CC_Instead_Mote".Translate(string.Join(", ", parts)), MoteColor, 3.5f);
        }

        // ---------------- The heart ----------------

        /// <summary>How big a good moment the heart takes this memory for, when it gives no mood itself (0 if it isn't one).</summary>
        public static float Felt(ThoughtDef def) => def?.GetModExtension<RewardInstead>()?.felt ?? 0f;
    }
}
