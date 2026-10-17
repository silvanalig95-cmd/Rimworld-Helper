using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The conversation system's two sections of a colonist's character page (Persona/CharacterPage.cs): the views they
    /// hold most strongly, in words, with what they rest on and the last talk that moved them; and what they went
    /// through with others, the in-jokes among it.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TalkPage
    {
        public const int MaxViews = 5, MaxMemories = 6;

        static TalkPage()
        {
            CharacterPage.Add("talk.views", 30, Views);
            CharacterPage.Add("talk.memories", 40, Memories);
        }

        // ---------------- Views ----------------

        public static CharacterSection Views(Pawn p)
        {
            var s = new CharacterSection { title = "CC_Talk_Views".Translate() };
            var r = Persona.Of(p);
            if (!Persona.On || r == null || r.stances.Count == 0) return s;
            var shown = new List<(TopicDef topic, string key, float v, TalkSubject subject)>();
            foreach (var kv in r.stances)
            {
                if (Mathf.Abs(kv.Value) < TalkMath.SideAt) continue;
                int colon = kv.Key.IndexOf(':');
                var topic = DefRefs.Named<TopicDef>(colon >= 0 ? kv.Key.Substring(0, colon) : kv.Key);
                if (topic == null) continue;
                TalkSubject subject = null;
                if (colon >= 0)
                {
                    int id = int.TryParse(kv.Key.Substring(colon + 1), out int x) ? x : -1;
                    var who = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.FirstOrDefault(o => o.thingIDNumber == id);
                    if (who == null) continue;
                    subject = TalkSubject.Of(who);
                }
                shown.Add((topic, kv.Key, kv.Value, subject));
            }
            foreach (var (topic, key, v, subject) in shown.OrderByDescending(x => Mathf.Abs(x.v)).ThenBy(x => x.key).Take(MaxViews))
            {
                string view = (v > 0f ? topic.viewFor : topic.viewAgainst).Formatted((subject?.name ?? "").Named("SUBJECT")).Resolve();
                string line = view.CapitalizeFirst();
                if (Mathf.Abs(v) >= TalkMath.StrongAt) line += " " + "CC_Talk_Strongly".Translate();
                s.Line(line, Tip(p, topic, key, v, subject));
            }
            return s;
        }

        private static string Tip(Pawn p, TopicDef topic, string key, float v, TalkSubject subject)
        {
            var parts = new List<string>
            {
                "CC_Talk_ViewOn".Translate(topic.about.Formatted((subject?.name ?? "").Named("SUBJECT")).Resolve().CapitalizeFirst()).Resolve()
            };
            var why = Reasons(p, topic, v);
            if (why.Count > 0) parts.Add("CC_Talk_ViewWhy".Translate(TextUtil.JoinAnd(why)).Resolve());
            var tr = TalkTracker.Instance;
            if (tr != null && tr.movedBy.TryGetValue(p.thingIDNumber + "|" + key, out var moved))
                parts.Add("CC_Talk_ViewMoved".Translate(moved.with, TextUtil.DaysString(Math.Max(1, Find.TickManager.TicksGame - moved.tick))).Resolve());
            if (tr != null && tr.HotFor(topic) >= 0.3f) parts.Add("CC_Talk_ViewNews".Translate().Resolve());
            return string.Join("\n", parts);
        }

        /// <summary>What a view rests on: the sides of them that lean it this way, and the traits and passions that do.</summary>
        private static List<string> Reasons(Pawn p, TopicDef topic, float v)
        {
            var reasons = new List<string>();
            float sign = v >= 0f ? 1f : -1f;
            var values = Persona.Values(p);
            var w = topic.SeedWeights;
            var push = new List<(float amount, string word)>();
            for (int i = 0; i < values.Length && i < w.Length; i++)
            {
                float amount = values[i] / 100f * w[i] / 100f * sign;
                if (amount < 0.08f) continue;
                string word = Persona.Word((PersonaAxis)i, values[i]);
                if (word != null) push.Add((amount, word));
            }
            foreach (var x in push.OrderByDescending(x => x.amount).Take(2)) reasons.Add(x.word);
            if (topic.leans != null)
            {
                foreach (var l in topic.leans)
                {
                    if (l == null || l.lean * sign <= 0f || !l.Fits(p) || reasons.Count >= 3) continue;
                    string label = LeanLabel(p, l);
                    if (!label.NullOrEmpty() && !reasons.Contains(label)) reasons.Add(label);
                }
            }
            return reasons;
        }

        private static string LeanLabel(Pawn p, TopicLean l)
        {
            if (!l.trait.NullOrEmpty())
            {
                var def = DefDatabase<TraitDef>.GetNamedSilentFail(l.trait);
                var t = def != null ? p.story?.traits?.GetTrait(def) : null;
                return t?.Label;
            }
            if (!l.skill.NullOrEmpty())
            {
                var def = DefDatabase<SkillDef>.GetNamedSilentFail(l.skill);
                return def != null ? "CC_Talk_ReasonPassion".Translate(def.label).Resolve() : null;
            }
            if (!l.meme.NullOrEmpty())
            {
                var def = DefDatabase<MemeDef>.GetNamedSilentFail(l.meme);
                return def != null ? "CC_Talk_ReasonFaith".Translate(def.label).Resolve() : null;
            }
            if (!l.precept.NullOrEmpty())
            {
                var def = DefDatabase<PreceptDef>.GetNamedSilentFail(l.precept);
                return def != null ? "CC_Talk_ReasonFaith".Translate(def.LabelCap).Resolve() : null;
            }
            return null;
        }

        // ---------------- Shared memories ----------------

        public static CharacterSection Memories(Pawn p)
        {
            var s = new CharacterSection { title = "CC_Talk_Memories".Translate() };
            var tr = PersonaTracker.Instance;
            if (!Persona.On || tr == null || p == null) return s;
            int now = Find.TickManager.TicksGame;
            int id = p.thingIDNumber;
            foreach (var m in tr.memories.Where(m => m?.def != null && m.Has(id)).OrderByDescending(m => m.WeightNow(now)).Take(MaxMemories))
            {
                string other = m.NameOf(m.Other(id)) ?? "?";
                s.Line("CC_Talk_MemoryLine".Translate(other, SharedMemories.Text(m)).Resolve(), MemoryTip(m, now));
            }
            return s;
        }

        private static string MemoryTip(SharedMemory m, int now)
        {
            var parts = new List<string> { "CC_Talk_MemoryWhen".Translate(TextUtil.DaysString(Math.Max(GenDate.TicksPerHour, now - m.tick))).Resolve() };
            if (m.def.joke)
                parts.Add(TalkMath.Worn(m.recalls) ? "CC_Talk_MemoryWorn".Translate().Resolve() : "CC_Talk_MemoryJoke".Translate(m.recalls).Resolve());
            else if (m.recalls > 0) parts.Add("CC_Talk_MemoryRecalled".Translate(m.recalls).Resolve());
            if (!m.def.description.NullOrEmpty()) parts.Add(m.def.description);
            return string.Join("\n", parts);
        }
    }
}
