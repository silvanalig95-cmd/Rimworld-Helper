using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The decree "one faith for the colony" (a DecreeDef with oneFaith, Defs/DecreeDefs/Rimfolk_Decrees_Faith.xml), with
    /// Ideology. The player names the faith as it's declared (the largest by default). While it holds, everyone of
    /// another faith leans toward it a little more each day (Faith/FaithLeanings.cs, LeaningMath.Decree), and trusts
    /// the council a little less (the open-minded least, the devout and a faith that frowns
    /// on being preached to most), and council meetings open with a word from one of its believers. Believers of the
    /// faith also start talks about it more often: the faith talks read <see cref="FaithOfColony"/> and
    /// <see cref="TalkFactor"/>. The devout of other faiths hate it, so the decree system's own petition to lift it
    /// comes from them. Lifting it ends all of it at once. Never in play without Ideology, in classic mode or with "Faith
    /// in the colony" off: it isn't listed, and one in force when that changes is lifted the next morning.
    /// </summary>
    public static class DecreeFaith
    {
        private static FaithCouncilTracker Tracker => FaithCouncilTracker.Instance;

        private static readonly Color MeetingColor = new Color(0.86f, 0.83f, 1f);

        private static DecreeDef def;
        private static int defGeneration = -1;

        /// <summary>The decree itself (the first def with oneFaith), or null.</summary>
        public static DecreeDef Def
        {
            get
            {
                if (defGeneration != DefRefs.Generation)
                {
                    def = DefDatabase<DecreeDef>.AllDefsListForReading.FirstOrDefault(d => d.oneFaith);
                    defGeneration = DefRefs.Generation;
                }
                return def;
            }
        }

        // ---------------- Who reads it ----------------

        /// <summary>
        /// The faith the council has named the colony's by decree, while the decree holds; null otherwise (and always
        /// without Ideology, in classic mode or with "Faith in the colony" off). For the faith talks and anything else
        /// that wants to know.
        /// </summary>
        public static Ideo FaithOfColony
        {
            get
            {
                if (!Faiths.On) return null;
                var d = Def;
                var comp = CouncilGameComponent.Instance;
                if (d == null || comp?.decrees == null || !comp.decrees.IsActive(d)) return null;
                return Tracker?.decreeFaith;
            }
        }

        /// <summary>A believer of the colony's named faith while the decree holds: they speak of it more often.</summary>
        public static bool Preaches(Pawn p) => p != null && FaithOfColony is Ideo f && Faiths.Of(p) == f;

        /// <summary>How much more often this colonist starts a talk about their faith while the decree holds (1 when it doesn't apply).</summary>
        public static float TalkFactor(Pawn p) => Preaches(p) ? FaithCouncilMath.DecreeTalkFactor : 1f;

        /// <summary>The faith the decree speaks of on its card and in its reactions: the one in force, else the one last chosen, else the largest.</summary>
        public static Ideo Faith
        {
            get
            {
                if (!Faiths.On) return null;
                var chosen = Tracker?.decreeFaith;
                var census = Faiths.Census();
                if (chosen != null && census.Any(f => f.ideo == chosen)) return chosen;
                return census.FirstOrDefault()?.ideo;
            }
        }

        /// <summary>Of the decree's faith (for its reactions).</summary>
        public static bool Believer(Pawn p) => Faiths.Holds(p) && Faith is Ideo f && Faiths.Of(p) == f;

        /// <summary>Of another faith than the decree's (for its reactions).</summary>
        public static bool Outsider(Pawn p) => Faiths.Holds(p) && Faith is Ideo f && Faiths.Of(p) != f;

        /// <summary>Not for this game as it stands: no faith life, or a single faith among the colony's people.</summary>
        public static bool Hidden => !Faiths.On || Faiths.Census().Count < 2;

        // ---------------- Declaring it ----------------

        /// <summary>
        /// The decrees page's enact (or force) button: for this decree, the player first picks the faith from a menu (the
        /// largest first); any other decree is enacted at once.
        /// </summary>
        public static void Enact(DecreeDef d, Action enact)
        {
            if (enact == null) return;
            if (d == null || !d.oneFaith || !Faiths.On)
            {
                enact();
                return;
            }
            var census = Faiths.Census().Where(f => f?.ideo != null).ToList();
            if (census.Count == 0)
            {
                enact();
                return;
            }
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < census.Count; i++)
            {
                var f = census[i];
                string label = (i == 0 ? "CC_FaithDecree_PickLargest" : "CC_FaithDecree_Pick").Translate(f.ideo.name.Named("FAITH"), f.believers.Count.Named("COUNT")).Resolve();
                Texture2D icon = null;
                try
                {
                    icon = f.ideo.Icon;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faith decree icon", e);
                }
                var ideo = f.ideo;
                options.Add(new FloatMenuOption(label, () =>
                {
                    Choose(ideo);
                    enact();
                }, icon, ideo.Color));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>The faith the next declaration names.</summary>
        public static void Choose(Ideo faith)
        {
            var tr = Tracker;
            if (tr != null && faith != null) tr.decreeFaith = faith;
        }

        /// <summary>A line for the decree's card: the faith it names (in force), or the one it will name.</summary>
        public static string CardLine(DecreeDef d)
        {
            if (d == null || !d.oneFaith) return null;
            var inForce = FaithOfColony;
            if (inForce != null) return "CC_FaithDecree_CardInForce".Translate(inForce.name.Named("FAITH")).Resolve();
            var largest = Faiths.Census().FirstOrDefault()?.ideo;
            return largest == null ? null : "CC_FaithDecree_CardChoose".Translate(largest.name.Named("FAITH")).Resolve();
        }

        /// <summary>Declared (DecreeWork.Enacted): the faith is named, the colony hears of it, and it goes in the chronicle.</summary>
        public static void Enacted(DecreeDef d)
        {
            var tr = Tracker;
            if (!Faiths.On || tr == null) return;
            var faith = Faith;
            if (faith == null) return;
            tr.decreeFaith = faith;
            string text = "CC_FaithDecree_Named".Translate(faith.name.Named("FAITH")).Resolve();
            // apart: the council's decree, heard of by everyone wherever they are; its word is said at a gathering (onEnact).
            News.Post(text, null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Important);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, text, null));
        }

        /// <summary>Lifted, or run out (DecreeWork.Ended): nothing of it is left.</summary>
        public static void Ended(DecreeDef d)
        {
            var tr = Tracker;
            var faith = tr?.decreeFaith;
            if (tr != null) tr.decreeFaith = null;
            if (faith == null || !Faiths.On) return;
            string text = "CC_FaithDecree_Lifted".Translate(faith.name.Named("FAITH")).Resolve();
            News.Post(text, null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, text, null));
        }

        // ---------------- Each day while it holds ----------------

        /// <summary>
        /// The decree's morning (DecreeWork.Daily): those of other faiths trust the council a little less (its pull on them
        /// is their leaning toward the named faith, fed each day by FaithLeanings). With faith life gone, or the faith
        /// with nobody left to hold it, the decree is lifted.
        /// </summary>
        public static void Daily(DecreeTracker t, DecreeDef d)
        {
            var comp = CouncilGameComponent.Instance;
            var tr = Tracker;
            if (comp == null || tr == null || t == null || d == null) return;
            var faith = tr.decreeFaith;
            if (!Faiths.On || faith == null || !Faiths.Census().Any(f => f.ideo == faith))
            {
                t.Repeal(d, comp, false);
                return;
            }
            var a = t.Get(d);
            int day = a == null ? 1 : Mathf.Max(1, (Find.TickManager.TicksGame - a.startTick) / GenDate.TicksPerDay + 1);
            // The trust log says why on the first morning and every fifth after, rather than every day.
            string why = day % 5 == 1 ? "CC_Log_FaithDecree".Translate(faith.name.Named("FAITH")).Resolve() : null;
            var people = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            // apart: a decree is felt by each colonist wherever they are; nobody here does anything to anyone. Its pull on
            // everyone of another faith is their leaning toward it, fed each day (Faith/FaithLeanings.cs).
            foreach (var p in people)
            {
                if (!Faiths.Holds(p) || Faiths.Of(p) == faith) continue;
                float openness = Persona.Get(p, PersonaAxis.Openness);
                comp.standings.Change(p, FaithCouncilMath.DecreeTrustPerDay(openness, Faiths.Certainty(p), Faiths.DislikesPreaching(p)), why);
            }
        }

        // ---------------- At the council's meetings ----------------

        private static readonly HashSet<int> worded = new HashSet<int>();

        /// <summary>
        /// A meeting opened while the decree holds (CouncilMeeting.Talk): one of the named faith's believers who came,
        /// the devout first, says a word for it before the business starts. Once a meeting. True when something was said.
        /// </summary>
        public static bool MeetingWord(CouncilMeeting m, List<Pawn> here)
        {
            // together: only those at the meeting's gathering (CouncilMeeting.Talk passes who is there).
            var faith = FaithOfColony;
            if (faith == null || m == null || here == null || worded.Contains(m.calledTick)) return false;
            worded.Add(m.calledTick);
            if (worded.Count > 50) worded.Clear();
            try
            {
                var speaker = here.Where(p => Faiths.Of(p) == faith).OrderByDescending(Faiths.Devout)
                    .ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
                if (speaker == null) return false;
                string line = Flavor.FromPool("Council_Pool_FaithDecree_MeetingWord", speaker);
                if (line.NullOrEmpty()) return false;
                line = line.Formatted(speaker.Named("PAWN"), faith.name.Named("FAITH")).Resolve();
                CouncilGameComponent.Speak(speaker, line, MeetingColor);
                if (m.remarks.Count < 5) m.remarks.Add("CC_Meeting_Remark".Translate(speaker.LabelShort, line).Resolve());
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith decree meeting word", e);
                return false;
            }
        }
    }
}
