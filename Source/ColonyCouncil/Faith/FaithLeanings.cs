using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How strongly a colonist is drawn to an ideoligion other than their own (Faith/FaithLeanings.cs).</summary>
    public class FaithLeaning : IExposable
    {
        public Ideo ideo;
        /// <summary>0 to 1: passing their certainty, they take it up.</summary>
        public float pull;
        /// <summary>Which way it went on the last day: 1 growing, -1 fading, 0 about the same.</summary>
        public int trend;
        /// <summary>Its pull has been told once (it passed <see cref="LeaningMath.TellAt"/>).</summary>
        public bool told;

        public void ExposeData()
        {
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref pull, "pull");
            Scribe_Values.Look(ref trend, "trend");
            Scribe_Values.Look(ref told, "told");
        }
    }

    /// <summary>
    /// Leanings (the arithmetic is <see cref="LeaningMath"/>): beside the ideoligion they hold, each colonist leans toward
    /// the others around them. Once a day (FaithCrisis.Daily) each leaning fades a little and is fed by that ideoligion's
    /// believers in the colony, the more the closer they are (friends, lovers, family), and by the council's decree
    /// naming it; the strongest wears on their certainty, against the game's own daily recovery for the content. A sway
    /// toward an ideoligion (a talk, a crisis, a council answer) feeds the leaning too. When a leaning passes their
    /// certainty they take that ideoligion up, through the game's own conversion, as firmly as they had leaned, and
    /// settle into it for a year (LeaningMath.SettleDays); the one they leave stays a small leaning of its own, so they
    /// may drift back if it grows again. An ideoligion the game has let go of is forgotten. The game only ever sees one ideoligion and a
    /// certainty, as always. Nothing here without Ideology, in classic mode or with "Faith in the colony" off.
    /// </summary>
    public static class FaithLeanings
    {
        private static readonly List<FaithLeaning> None = new List<FaithLeaning>();

        /// <summary>Their leanings (none recorded: an empty list).</summary>
        public static List<FaithLeaning> Of(Pawn p) => FaithPeople.Tracker?.Of(p, false)?.leanings ?? None;

        /// <summary>Their leanings worth showing, strongest first, at most three.</summary>
        public static List<FaithLeaning> Shown(Pawn p) =>
            Of(p).Where(l => Alive(l?.ideo) && l.ideo != Faiths.Of(p) && l.pull >= 0.05f).OrderByDescending(l => l.pull).Take(3).ToList();

        /// <summary>An ideoligion the game still has (one whose last believers are gone is removed from the game).</summary>
        public static bool Alive(Ideo ideo) => ideo != null && Find.IdeoManager?.IdeosListForReading != null && Find.IdeoManager.IdeosListForReading.Contains(ideo);

        /// <summary>A sway toward an ideoligion (Faiths.SwayToward) draws them toward it as well: the same amount on their leaning.</summary>
        public static void Feed(Pawn p, Ideo toward, float amount)
        {
            if (!Faiths.Holds(p) || !Alive(toward) || toward == Faiths.Of(p) || amount <= 0f) return;
            var r = FaithPeople.Tracker?.Of(p, true);
            if (r == null) return;
            var l = r.leanings.FirstOrDefault(x => x.ideo == toward);
            if (l == null) r.leanings.Add(l = new FaithLeaning { ideo = toward });
            l.pull = Mathf.Clamp01(l.pull + amount);
            Trim(r);
        }

        /// <summary>Only the strongest <see cref="LeaningMath.MaxLeanings"/> are kept.</summary>
        private static void Trim(FaithPerson r)
        {
            if (r.leanings.Count <= LeaningMath.MaxLeanings) return;
            r.leanings.Sort((a, b) => b.pull.CompareTo(a.pull));
            r.leanings.RemoveRange(LeaningMath.MaxLeanings, r.leanings.Count - LeaningMath.MaxLeanings);
        }

        /// <summary>What the day's pass needs once: who believes what, and the ideoligion the council has named.</summary>
        public sealed class DayLook
        {
            public List<Faiths.Faith> census;
            public Ideo decree;
        }

        public static DayLook Begin() => new DayLook { census = Faiths.Census().ToList(), decree = DecreeFaith.FaithOfColony };

        /// <summary>One colonist's day: leanings fed and faded, certainty worn by the strongest, and a switch if it overtook.</summary>
        public static void Day(Pawn p, DayLook look)
        {
            var own = Faiths.Of(p);
            var r = FaithPeople.Tracker?.Of(p, true);
            if (own == null || r == null || look == null || p.DevelopmentalStage.Baby()) return;
            bool devout = Faiths.Devout(p);
            float ready = LeaningMath.Readiness(Persona.Get(p, PersonaAxis.Openness));
            bool dislikes = Faiths.DislikesPreaching(p);
            r.leanings.RemoveAll(l => !Alive(l?.ideo));
            var ideos = new HashSet<Ideo>(look.census.Select(f => f.ideo).Where(Alive));
            if (Alive(look.decree)) ideos.Add(look.decree);
            foreach (var l in r.leanings) ideos.Add(l.ideo);
            ideos.Remove(own);
            foreach (var ideo in ideos)
            {
                float closeness = 0f;
                var faith = look.census.FirstOrDefault(f => f.ideo == ideo);
                if (faith != null)
                    foreach (var b in faith.believers)
                        if (b != p && !b.Dead) closeness += LeaningMath.Closeness(p.relations?.OpinionOf(b) ?? 0, Close(p, b));
                float feed = LeaningMath.Feed(closeness, ideo == look.decree, ready, dislikes);
                var l = r.leanings.FirstOrDefault(x => x.ideo == ideo);
                if (l == null)
                {
                    if (feed <= 0f) continue;
                    r.leanings.Add(l = new FaithLeaning { ideo = ideo });
                }
                float before = l.pull;
                l.pull = LeaningMath.Day(l.pull, feed);
                l.trend = LeaningMath.Trend(before, l.pull);
                if (!l.told && l.pull >= LeaningMath.TellAt)
                {
                    l.told = true;
                    Notify.Message("CC_Leaning_Drawn".Translate(p.Named("PAWN"), ideo.name.Named("FAITH")).Resolve(), p, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                }
            }
            r.leanings.RemoveAll(l => l?.ideo == null || l.ideo == own || l.pull < LeaningMath.Forget);
            Trim(r);
            var strongest = r.leanings.OrderByDescending(l => l.pull).FirstOrDefault();
            if (strongest == null) return;
            // Just taken up an ideoligion: they settle into it a year before anything wears on them again.
            if (!LeaningMath.Settled(r.lastSwitch, Find.TickManager.TicksGame, GenDate.TicksPerDay)) return;
            Faiths.Shake(p, LeaningMath.Erosion(strongest.pull, devout));
            if (LeaningMath.Overtakes(strongest.pull, p.ideo.Certainty, strongest.trend)) TakeUp(p, r, strongest);
        }

        /// <summary>A lover or family by blood: their faith pulls half again as hard.</summary>
        private static bool Close(Pawn p, Pawn b)
        {
            try
            {
                return LovePartnerRelationUtility.LovePartnerRelationExists(p, b) || (p.relations?.FamilyByBlood?.Contains(b) ?? false);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The leaning overtook: they take that ideoligion up, as firmly as they had leaned, and the one they leave becomes a small leaning.</summary>
        private static void TakeUp(Pawn p, FaithPerson r, FaithLeaning l)
        {
            var old = Faiths.Of(p);
            float left = p.ideo.Certainty;
            var to = l.ideo;
            if (!Alive(to)) return;
            float firm = Mathf.Clamp(l.pull, LeaningMath.MinSwitch, 1f);
            string cause = to == DecreeFaith.FaithOfColony
                ? "CC_FaithDecree_Cause".Translate(to.name.Named("FAITH")).Resolve()
                : "CC_Leaning_Cause".Translate(p.Named("PAWN")).Resolve();
            if (!Faiths.TakeUp(p, to, cause)) return;
            p.ideo.OffsetCertainty(firm - p.ideo.Certainty);
            r.leanings.Remove(l);
            r.lastSwitch = Find.TickManager.TicksGame;
            // The one they left stays with them a little; it has to grow again to draw them back.
            if (Alive(old)) r.leanings.Add(new FaithLeaning { ideo = old, pull = LeaningMath.ReturnPull(left), told = true });
            FaithPeople.Note(p, "CC_Leaning_Note".Translate(to.name.Named("FAITH")).Resolve(), 0f);
        }
    }
}
