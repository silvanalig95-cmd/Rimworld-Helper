using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Faith in the colony (Faith/), with Ideology: who holds which faith and how firmly, and the one way Rimfolk moves
    /// it. The game keeps each colonist's certainty and converts them when it runs out; everything Rimfolk does (a talk
    /// about faith, an evening of faith, a crisis, a decree) goes through <see cref="Sway"/>, <see cref="Shake"/> and
    /// <see cref="Steady"/>, so a conversion is always the game's own, and is told the same way whatever led to it.
    /// Nothing here without Ideology, in classic mode, or with "Faith in the colony" off.
    /// </summary>
    public static class Faiths
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => ModsConfig.IdeologyActive && S != null && S.faithLife && Find.IdeoManager != null && !Find.IdeoManager.classicMode;

        public static Ideo Of(Pawn p) => p?.ideo?.Ideo;

        /// <summary>Someone with a faith of their own that Rimfolk can speak about.</summary>
        public static bool Holds(Pawn p) => On && Of(p) != null;

        /// <summary>The game's certainty, 0 to 1 (1 for anyone without a faith).</summary>
        public static float Certainty(Pawn p) => Of(p) == null ? 1f : p.ideo.Certainty;

        public static bool Wavering(Pawn p) => Holds(p) && FaithMath.Wavering(p.ideo.Certainty);

        public static bool Devout(Pawn p) => Holds(p) && FaithMath.Devout(p.ideo.Certainty);

        public static bool Same(Pawn a, Pawn b) => Of(a) != null && Of(a) == Of(b);

        /// <summary>Their faith frowns on being preached to (a precept about proselytism that dislikes it, from any mod).</summary>
        public static bool DislikesPreaching(Pawn p)
        {
            var ideo = Of(p);
            if (ideo == null) return false;
            foreach (var pr in ideo.PreceptsListForReading)
            {
                string n = pr?.def?.defName;
                if (n != null && n.IndexOf("Proselyt", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && (n.Contains("Disliked") || n.Contains("Abhorrent") || n.Contains("Horrible") || n.Contains("Despised")))
                    return true;
            }
            return false;
        }

        // ---------------- Who believes what ----------------

        /// <summary>One faith in the colony: who holds it, and how firmly on average.</summary>
        public class Faith
        {
            public Ideo ideo;
            public readonly List<Pawn> believers = new List<Pawn>();
            public float certainty;
        }

        private static List<Faith> census;
        private static int censusTick = -99999;

        /// <summary>The colony's own people (free colonists at home or away) by faith, most believers first; worked out at most once an hour.</summary>
        public static List<Faith> Census()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (census != null && now >= censusTick && now - censusTick < GenDate.TicksPerHour) return census;
            var list = new List<Faith>();
            if (On)
            {
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep)
                {
                    var ideo = Of(p);
                    if (ideo == null || p.IsQuestLodger()) continue;
                    var f = list.FirstOrDefault(x => x.ideo == ideo);
                    if (f == null) list.Add(f = new Faith { ideo = ideo });
                    f.believers.Add(p);
                }
                foreach (var f in list) f.certainty = f.believers.Average(b => b.ideo.Certainty);
                list.Sort((a, b) => b.believers.Count != a.believers.Count ? b.believers.Count.CompareTo(a.believers.Count) : b.certainty.CompareTo(a.certainty));
            }
            census = list;
            censusTick = now;
            return census;
        }

        /// <summary>Forget the census (a conversion, a new game).</summary>
        public static void Reset() => census = null;

        /// <summary>How split the colony is, 0 (one faith) to 1 (everyone their own).</summary>
        public static float Split()
        {
            var c = Census();
            return FaithMath.Split(c.Count, c.Sum(f => f.believers.Count));
        }

        // ---------------- Moving faith ----------------

        public enum SwayResult
        {
            None,
            Moved,
            Converted
        }

        /// <summary>
        /// A word from <paramref name="speaker"/> about their faith takes some of <paramref name="listener"/>'s certainty
        /// (<see cref="FaithMath.Sway"/>: the speaker's conversion power, the listener's openness, pride and opinion of
        /// them). At none left the listener takes the speaker's faith, through the game's own conversion, and it's told.
        /// </summary>
        public static SwayResult Sway(Pawn listener, Pawn speaker, float baseAmount, string cause = null)
        {
            if (!Holds(listener) || !Holds(speaker) || listener == speaker || Same(listener, speaker)) return SwayResult.None;
            float power = speaker.GetStatValue(StatDefOf.ConversionPower);
            float opinion = listener.relations?.OpinionOf(speaker) ?? 0f;
            float amount = FaithMath.Sway(baseAmount, power, Persona.Get(listener, PersonaAxis.Openness), Persona.Get(listener, PersonaAxis.Pride),
                opinion, DislikesPreaching(listener));
            return SwayToward(listener, Of(speaker), amount, cause, speaker);
        }

        /// <summary>The same toward a faith with no one speaker (a rite, a decree), by an amount already worked out.</summary>
        public static SwayResult SwayToward(Pawn listener, Ideo toward, float amount, string cause = null, Pawn by = null)
        {
            if (!Holds(listener) || toward == null || Of(listener) == toward || amount <= 0f) return SwayResult.None;
            var old = Of(listener);
            bool converted;
            try
            {
                converted = listener.ideo.IdeoConversionAttempt(amount, toward);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("faith sway", e);
                return SwayResult.None;
            }
            if (!converted || Of(listener) != toward)
            {
                // A sway toward a faith draws them toward it too (Faith/FaithLeanings.cs).
                FaithLeanings.Feed(listener, toward, amount);
                return SwayResult.Moved;
            }
            Converted(listener, old, toward, cause, by);
            return SwayResult.Converted;
        }

        /// <summary>
        /// They take another faith up outright (a leaning that overtook their certainty): the game's own conversion, or
        /// its plain change of faith should a stat keep the conversion from landing, then told as any conversion is.
        /// </summary>
        internal static bool TakeUp(Pawn p, Ideo toward, string cause)
        {
            if (!Holds(p) || toward == null || Of(p) == toward) return false;
            var old = Of(p);
            try
            {
                p.ideo.IdeoConversionAttempt(10f, toward);
                if (Of(p) != toward) p.ideo.SetIdeo(toward);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("faith take up", e);
            }
            if (Of(p) != toward) return false;
            Converted(p, old, toward, cause, null);
            return true;
        }

        /// <summary>Certainty taken without pointing anywhere (a crisis of faith): no one converts from this alone.</summary>
        public static void Shake(Pawn p, float amount)
        {
            if (!Holds(p) || amount <= 0f) return;
            p.ideo.OffsetCertainty(-System.Math.Min(amount, p.ideo.Certainty - 0.01f));
        }

        /// <summary>Certainty given back (a good rite of their own, a believer's comfort).</summary>
        public static void Steady(Pawn p, float amount)
        {
            if (Holds(p) && amount > 0f) p.ideo.OffsetCertainty(amount);
        }

        /// <summary>Someone took a new faith through Rimfolk: a message and a line in the chronicle (the mark and journal follow from Marks).</summary>
        private static void Converted(Pawn p, Ideo old, Ideo now, string cause, Pawn by)
        {
            Reset();
            PeopleTags.Forget(p);
            string text = (by != null
                ? "CC_Faith_ConvertedBy".Translate(p.Named("PAWN"), now.name.Named("FAITH"), (old?.name ?? "").Named("OLD"), by.Named("OTHER"))
                : "CC_Faith_Converted".Translate(p.Named("PAWN"), now.name.Named("FAITH"), (old?.name ?? "").Named("OLD"))).Resolve();
            if (!cause.NullOrEmpty()) text += " " + cause;
            Notify.Message(text, p, MessageTypeDefOf.NeutralEvent, Importance.Important);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
        }

        // ---------------- Tags ----------------

        /// <summary>faith:devout, faith:wavering, faith:majority (the colony's largest faith), faith:minority, faith:alone (the only one of theirs).</summary>
        public static void AddTags(Pawn p, HashSet<string> tags)
        {
            if (!Holds(p) || !p.IsFreeColonist) return;
            float c = p.ideo.Certainty;
            if (FaithMath.Devout(c)) tags.Add("faith:devout");
            else if (FaithMath.Wavering(c)) tags.Add("faith:wavering");
            var census = Census();
            if (census.Count < 2) return;
            var mine = census.FirstOrDefault(f => f.ideo == Of(p));
            if (mine == null) return;
            if (mine == census[0] && mine.believers.Count > census[1].believers.Count) tags.Add("faith:majority");
            else tags.Add("faith:minority");
            if (mine.believers.Count == 1) tags.Add("faith:alone");
        }
    }
}
