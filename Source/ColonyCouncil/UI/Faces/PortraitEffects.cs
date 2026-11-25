using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a person goes through, laid over their portraits (<see cref="PortraitEffectsMath"/> says what each effect is):
    /// the large portrait at the inspect pane, the colonist card's, and the round portraits (from
    /// <see cref="PortraitEffectsMath.RoundFrom"/> across), as the setting "What they go through, on their portraits" has it
    /// (off, the large ones, or every portrait; the player: "should be toggleable in case of performance issues").
    /// <list type="bullet">
    /// <item>Light on the game: what they go through is read from the game at most twice a second for each person shown,
    /// and someone who is whole and at ease draws nothing more. Each effect is one small texture, painted once (its colour
    /// in its pixels: the picture's paint, not the interface's), drawn over the portrait at its strength.</item>
    /// <item>An effect comes and goes softly over a second or so; the bleeding pulses and the heat shimmers; the tremble
    /// and the wobble move the person in a moving portrait (with the setting "Portraits move a little").</item>
    /// <item>Where their picture has a painted version of what they go through (asleep, wounded, ill ...:
    /// <see cref="PortraitLifeMath.Conditions"/>), that picture shows in its place (<see cref="PortraitSources.Shown"/>),
    /// the most serious first, and the drawn effect for the same thing is toned down over it
    /// (<see cref="PortraitEffectsMath.Damp"/>).</item>
    /// </list>
    /// </summary>
    public static class PortraitEffects
    {
        private sealed class Entry
        {
            public float readAt = -100f, shownAt, usedAt;
            public PortraitEffectLevels want, shown;
            /// <summary>What they go through, as last read.</summary>
            public PortraitCondition condition = PortraitCondition.Calm;
            /// <summary>The painted condition shown in place of their picture ("" none), since when, and when it was last drawn.</summary>
            public string painted = "";
            public float paintedSince, paintedDrawnAt = -100f;
        }

        private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private static int prunedAt = -1000;

        /// <summary>What they go through is read again this often (seconds).</summary>
        private const float ReadEvery = 0.5f;

        private static PortraitEffectsMode Mode => ColonyCouncilMod.Settings?.portraitEffects ?? PortraitEffectsMode.Everywhere;

        /// <summary>Whether a portrait of this kind shows the effects: <paramref name="large"/> the large ones (the inspect pane's, the colonist card's).</summary>
        public static bool On(bool large)
        {
            var m = Mode;
            return m == PortraitEffectsMode.Everywhere || m == PortraitEffectsMode.Large && large;
        }

        // ---------------- What they go through ----------------

        /// <summary>What the person goes through now, from the game's own health, needs and mind.</summary>
        public static PortraitCondition ConditionOf(Pawn p)
        {
            var c = PortraitCondition.Calm;
            if (p == null) return c;
            if (p.Dead)
            {
                c.dead = true;
                return c;
            }
            var health = p.health;
            var hs = health?.hediffSet;
            if (hs != null)
            {
                c.bleed = hs.BleedRateTotal;
                c.pain = hs.PainTotal;
                foreach (var h in hs.hediffs)
                {
                    var d = h?.def;
                    if (d == null) continue;
                    if (d == HediffDefOf.BloodLoss) c.bloodLoss = Mathf.Max(c.bloodLoss, h.Severity);
                    else if (d == HediffDefOf.Hypothermia) c.cold = Mathf.Max(c.cold, h.Severity);
                    else if (d == HediffDefOf.Heatstroke) c.heat = Mathf.Max(c.heat, h.Severity);
                    else if (d == HediffDefOf.Malnutrition) c.hunger = Mathf.Max(c.hunger, h.Severity);
                    else if (h is Hediff_Alcohol) c.drunk = Mathf.Max(c.drunk, h.Severity);
                    else if (h is Hediff_High || !d.isBad && d.defName.EndsWith("High", StringComparison.OrdinalIgnoreCase)) c.high = true;
                    else if (d.makesSickThought || d == HediffDefOf.WoundInfection)
                    {
                        // Its share of the way to death (an illness without a deadly severity, against its highest).
                        float most = d.lethalSeverity > 0f ? d.lethalSeverity : d.maxSeverity > 0f && d.maxSeverity < 100f ? d.maxSeverity : 1f;
                        c.sick = Mathf.Max(c.sick, Mathf.Clamp01(h.Severity / most));
                    }
                }
            }
            if (health != null)
            {
                c.health = health.summaryHealth?.SummaryHealthPercent ?? 1f;
                c.unconscious = health.capacities != null && !health.capacities.CanBeAwake;
                c.downed = health.Downed;
            }
            c.asleep = !c.unconscious && !p.Awake();
            c.breaking = p.InMentalState;
            var mood = p.needs?.mood;
            var breaker = p.mindState?.mentalBreaker;
            if (mood != null && breaker != null)
            {
                c.mood = mood.CurLevel;
                c.minor = breaker.BreakThresholdMinor;
                c.extreme = breaker.BreakThresholdExtreme;
            }
            if (p.needs?.rest != null) c.rest = p.needs.rest.CurLevel;
            return c;
        }

        /// <summary>The person's entry, what they go through read again twice a second.</summary>
        private static Entry EntryOf(Pawn p, float now)
        {
            int id = p.thingIDNumber;
            if (!entries.TryGetValue(id, out var e))
            {
                var c = ConditionOf(p);
                e = new Entry { condition = c, want = PortraitEffectsMath.Levels(c), readAt = now, shownAt = now };
                e.shown = e.want;
                entries[id] = e;
            }
            if (now - e.readAt >= ReadEvery)
            {
                e.readAt = now;
                try
                {
                    e.condition = ConditionOf(p);
                    e.want = PortraitEffectsMath.Levels(e.condition);
                }
                catch (Exception ex)
                {
                    e.condition = PortraitCondition.Calm;
                    e.want = default;
                    CouncilLog.ErrorOnce("portraits: what a person goes through, for their portrait's effects", ex);
                }
            }
            e.usedAt = now;
            Prune(now);
            return e;
        }

        /// <summary>The effects' strengths shown now for the person: read again twice a second, faded toward softly, kept for the next frame.</summary>
        private static PortraitEffectLevels LevelsOf(Pawn p)
        {
            float now = Time.realtimeSinceStartup;
            var e = EntryOf(p, now);
            e.shown = PortraitEffectsMath.Toward(e.shown, e.want, Mathf.Max(0f, now - e.shownAt) * PortraitEffectsMath.FadePerSecond);
            e.shownAt = now;
            return e.shown;
        }

        // ---------------- The painted conditions ----------------

        /// <summary>
        /// The condition whose painted picture shows for the person now (<paramref name="has"/>: their picture has it, made),
        /// or "": the most serious that holds (<see cref="PortraitLifeMath.ConditionWanted"/>), kept a few seconds before a
        /// lighter one or none takes its place (<see cref="PortraitLifeMath.ConditionStep"/>).
        /// </summary>
        public static string PaintedFor(Pawn p, Func<string, bool> has)
        {
            if (p == null) return "";
            float now = Time.realtimeSinceStartup;
            var e = EntryOf(p, now);
            string want = PortraitLifeMath.ConditionWanted(e.condition, has);
            string next = PortraitLifeMath.ConditionStep(e.painted, want, e.paintedSince, now);
            if (next != e.painted)
            {
                e.painted = next;
                e.paintedSince = now;
            }
            return e.painted;
        }

        /// <summary>The painted condition shown for the person now ("" none), for the window about a picture.</summary>
        public static string PaintedNow(Pawn p) =>
            p != null && entries.TryGetValue(p.thingIDNumber, out var e) && Time.realtimeSinceStartup - e.paintedDrawnAt < 1f ? e.painted : "";

        /// <summary>The painted condition was drawn for the person this frame: its drawn effect is toned down over it.</summary>
        public static void PaintedShown(Pawn p)
        {
            if (p != null && entries.TryGetValue(p.thingIDNumber, out var e)) e.paintedDrawnAt = Time.realtimeSinceStartup;
        }


        /// <summary>Every few hundred frames, the people not shown for a minute let go.</summary>
        private static void Prune(float now)
        {
            if (Time.frameCount - prunedAt < 600) return;
            prunedAt = Time.frameCount;
            var gone = new List<int>();
            foreach (var kv in entries)
                if (now - kv.Value.usedAt > 60f)
                    gone.Add(kv.Key);
            foreach (int id in gone) entries.Remove(id);
        }

        // ---------------- Drawing ----------------

        /// <summary>
        /// The effects over a portrait drawn in <paramref name="r"/>: <paramref name="large"/> the inspect pane's or the
        /// colonist card's, <paramref name="round"/> a round one (the effects kept inside its circle).
        /// </summary>
        public static void Draw(Rect r, Pawn p, bool large, bool round, float alpha = 1f)
        {
            if (p == null || Event.current.type != EventType.Repaint || !On(large)) return;
            if (round && r.width < PortraitEffectsMath.RoundFrom) return;
            var e = LevelsOf(p);
            // Over a painted condition, its own drawn effect only a little (the picture already shows it).
            if (entries.TryGetValue(p.thingIDNumber, out var held) && held.painted.Length > 0 && Time.realtimeSinceStartup - held.paintedDrawnAt < 0.5f)
                e = PortraitEffectsMath.Damp(e, held.painted);
            if (!e.Any) return;
            float clock = Time.realtimeSinceStartup;
            try
            {
                Layer(r, Paint.Pale, round, 0.32f * e.hunger, alpha);
                Layer(r, Paint.Drain, round, 0.22f * e.hurt, alpha);
                Layer(r, Paint.Sick, round, 0.2f * e.sick, alpha);
                Layer(r, Paint.Heat, round, 0.16f * e.heat * PortraitEffectsMath.Shimmer(clock), alpha);
                if (e.high > 0f)
                {
                    float d = PortraitEffectsMath.Drift(clock, p.thingIDNumber);
                    Layer(r, Paint.HighA, round, 0.15f * e.high * (1f - d), alpha);
                    Layer(r, Paint.HighB, round, 0.15f * e.high * d, alpha);
                }
                Layer(r, Paint.TiredBands, round, 0.6f * e.tired, alpha);
                Layer(r, Paint.DarkEdge, round, 0.75f * e.stress, alpha);
                Layer(r, Paint.BreakEdge, round, 0.6f * e.breaking, alpha);
                Layer(r, Paint.Frost, round, 0.95f * e.cold, alpha);
                Layer(r, Paint.RedRim, round, 0.55f * e.hurt, alpha);
                Layer(r, Paint.RedEdge, round, 0.85f * e.bleed * PortraitEffectsMath.Pulse(clock, e.bleed), alpha);
                Layer(r, Paint.Dim, round, 0.45f * e.dim, alpha);
            }
            finally
            {
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// The movement the effects add to a moving portrait: <paramref name="lean"/> in degrees about its foot and
        /// <paramref name="dx"/> as a share of its width (the tremble near a break, the wobble of the drunk or high).
        /// </summary>
        public static void Shake(Pawn p, out float lean, out float dx)
        {
            lean = dx = 0f;
            if (p == null || !On(true)) return;
            var e = LevelsOf(p);
            if (e.stress < 0.5f && e.high <= 0f) return;
            PortraitEffectsMath.Shake(e, Time.realtimeSinceStartup, p.thingIDNumber, out lean, out dx);
        }

        private static void Layer(Rect r, Paint what, bool round, float strength, float alpha)
        {
            if (strength <= 0.003f) return;
            var tex = TextureOf(what, round);
            if (tex == null) return;
            GUI.color = UiKit.WithAlpha(UiKit.Plain, Mathf.Clamp01(strength * alpha));
            GUI.DrawTexture(r, tex);
        }

        // ---------------- The textures, painted once ----------------

        private enum Paint
        {
            RedEdge,
            DarkEdge,
            BreakEdge,
            TiredBands,
            RedRim,
            Frost,
            Pale,
            Drain,
            Sick,
            Heat,
            HighA,
            HighB,
            Dim
        }

        /// <summary>Each effect's paint (the picture's, not the interface's).</summary>
        private static Color32 ColourOf(Paint what)
        {
            switch (what)
            {
                case Paint.RedEdge: return new Color32(150, 8, 10, 255);
                case Paint.DarkEdge: return new Color32(10, 9, 13, 255);
                case Paint.BreakEdge: return new Color32(80, 6, 16, 255);
                case Paint.TiredBands: return new Color32(14, 12, 18, 255);
                case Paint.RedRim: return new Color32(175, 22, 20, 255);
                case Paint.Frost: return new Color32(226, 240, 255, 255);
                case Paint.Pale: return new Color32(236, 229, 212, 255);
                case Paint.Drain: return new Color32(124, 124, 128, 255);
                case Paint.Sick: return new Color32(150, 172, 58, 255);
                case Paint.Heat: return new Color32(255, 138, 40, 255);
                case Paint.HighA: return new Color32(255, 92, 200, 255);
                case Paint.HighB: return new Color32(60, 200, 222, 255);
                default: return new Color32(8, 8, 12, 255);
            }
        }

        private static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();

        private static Texture2D TextureOf(Paint what, bool round)
        {
            int key = (int)what * 2 + (round ? 1 : 0);
            if (textures.TryGetValue(key, out var tex) && tex != null) return tex;
            try
            {
                tex = Make(what, round);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: painting an effect's texture", e);
                tex = null;
            }
            textures[key] = tex;
            return tex;
        }

        private static Texture2D Make(Paint what, bool round)
        {
            bool solid = what == Paint.Pale || what == Paint.Drain || what == Paint.Sick || what == Paint.Heat || what == Paint.HighA || what == Paint.HighB || what == Paint.Dim;
            int size = solid ? round ? 64 : 4 : what == Paint.Frost ? 192 : 128;
            var colour = ColourOf(what);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float a;
                switch (what)
                {
                    case Paint.RedEdge:
                    case Paint.DarkEdge:
                    case Paint.BreakEdge:
                        a = PortraitEffectsMath.Edge(u, v, round);
                        break;
                    case Paint.RedRim:
                        a = PortraitEffectsMath.Rim(u, v, round);
                        break;
                    case Paint.TiredBands:
                        a = PortraitEffectsMath.Bands(u, v, round);
                        break;
                    case Paint.Frost:
                        a = PortraitEffectsMath.Frost(u, v, round, 17);
                        break;
                    default:
                        a = 1f;
                        break;
                }
                a *= PortraitEffectsMath.Inside(u, v, round, size);
                var c = colour;
                c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = c;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RimfolkPortraitEffect_" + what + (round ? "_Round" : ""), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
