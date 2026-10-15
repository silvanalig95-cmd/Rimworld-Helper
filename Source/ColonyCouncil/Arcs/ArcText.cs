using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of a personal story: a line in someone's own voice (their tone, the moment's tags and the story's), a
    /// chronicle line, and the named values every text may use.
    /// </summary>
    public static class ArcText
    {
        /// <summary>
        /// A line from these, in <paramref name="speaker"/>'s voice, about <paramref name="about"/> ({OTHER}). Tags: the
        /// moment's (Flavor), the story's own, how the two are related (partner, kin, animal), and any extra.
        /// </summary>
        public static string Line(ToneLines lines, Pawn speaker, Arc arc, Pawn about, params string[] extra)
        {
            if (lines == null || !lines.Any || speaker == null) return null;
            try
            {
                var tags = Tags(speaker, arc, about, extra);
                string raw = lines.Pick(ToneUtility.ToneFor(speaker), tags);
                return raw.NullOrEmpty() ? null : raw.Formatted(Args(arc, speaker, about)).Resolve();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("personal story line " + arc?.def?.defName, e);
                return null;
            }
        }

        /// <summary>A chronicle line from these (third person), fitting the story's tags; null if there are none.</summary>
        public static string Chronicle(List<string> lines, Arc arc, params string[] extra)
        {
            if (lines.NullOrEmpty() || arc?.pawn == null) return null;
            try
            {
                var tags = Tags(arc.pawn, arc, arc.other, extra);
                string raw = Flavor.PickFresh(lines, tags, lines) ?? Flavor.Strip(lines.FirstOrDefault(l => Flavor.Match(l, tags) >= 0) ?? lines[0]);
                return raw.NullOrEmpty() ? null : raw.Formatted(Args(arc, arc.pawn, arc.other)).Resolve();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("personal story chronicle " + arc.def?.defName, e);
                return null;
            }
        }

        public static HashSet<string> Tags(Pawn speaker, Arc arc, Pawn about, IEnumerable<string> extra)
        {
            var tags = Flavor.TagsFor(speaker);
            if (arc != null && !arc.tag.NullOrEmpty()) tags.Add(arc.tag);
            if (about != null && about != speaker)
            {
                if (about.RaceProps?.Animal == true) tags.Add("animal");
                else if (ArcUtil.Partners(speaker, about)) tags.Add("partner");
                else if (ArcUtil.Kin(speaker, about)) tags.Add("kin");
            }
            if (extra != null)
                foreach (var t in extra)
                    if (!t.NullOrEmpty()) tags.Add(t);
            return tags;
        }

        /// <summary>
        /// The named values: {PAWN} the one speaking (or the story's colonist), {OTHER} whom it's about, {PET} the animal,
        /// {SKILL}, {LOST}, {NAME}, {FACTION}, {GOAL}, {QUIRK} and {DAYS}.
        /// </summary>
        public static NamedArgument[] Args(Arc arc, Pawn speaker, Pawn about)
        {
            var other = about ?? arc?.talkedWith ?? speaker;
            var pet = arc != null && arc.OtherIsAnimal ? arc.other : other;
            string quirk = arc?.quirk != null ? DefDatabase<QuirkDef>.GetNamedSilentFail(arc.quirk)?.label : null;
            return new[]
            {
                speaker.Named("PAWN"),
                other.Named("OTHER"),
                pet.Named("PET"),
                (arc?.skill?.label ?? "").Named("SKILL"),
                (arc?.lostName ?? "").Named("LOST"),
                (arc?.name ?? "").Named("NAME"),
                (arc?.faction?.Name ?? arc?.factionName ?? "").Named("FACTION"),
                (arc?.goalText ?? "").Named("GOAL"),
                (quirk ?? "").Named("QUIRK"),
                (arc?.awayDays ?? 0).Named("DAYS")
            };
        }

        /// <summary>What the story waits for now, with how far along it is, for the Story tab.</summary>
        public static string Waiting(Arc arc)
        {
            var st = arc?.Stage;
            var g = st?.gate;
            if (g == null) return null;
            string text = st.waiting.NullOrEmpty() ? null : st.waiting.Formatted(Args(arc, arc.pawn, arc.other)).Resolve();
            string progress = Progress(arc, g);
            if (text == null) return progress;
            return progress == null ? text : text + " (" + progress + ")";
        }

        private static string Progress(Arc arc, ArcGate g)
        {
            switch (g.kind)
            {
                case ArcGateKind.Hold:
                    return "CC_Arc_Progress_Hours".Translate(((int)arc.held).ToString(), g.hours.ToString("0")).Resolve();
                case ArcGateKind.Skill:
                    return "CC_Arc_Progress_Level".Translate(ArcUtil.Level(g.onOther ? arc.other : arc.pawn, arc.skill), ArcTracker.SkillTarget(arc, g)).Resolve();
                case ArcGateKind.Practice:
                    return ((ArcTracker.PracticeDone(arc, g) / g.amount).ToStringPercent()).ToString();
                case ArcGateKind.Time:
                {
                    float left = g.days - ArcTracker.DaysInStage(arc);
                    return left <= 0f ? null : "CC_Arc_Progress_Days".Translate(left.ToString("0.#")).Resolve();
                }
            }
            return null;
        }
    }
}
