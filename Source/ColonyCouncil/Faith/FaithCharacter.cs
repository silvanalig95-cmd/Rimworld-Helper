using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's faith on their character page (section 35, after their views): which faith and how firmly (the game's
    /// certainty, devout or wavering), a role they hold in it, what it asks that weighs on them now, what moved their
    /// faith lately (a talk, a crisis, with how far), and who is likeliest to sway them, or whom they could bring round.
    /// Shown with "Faith in the colony" on (Ideology, not classic mode). The Culture tab draws the same, with the faith's
    /// icon and a certainty bar in place of the first lines (<see cref="Details"/>).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FaithCharacter
    {
        public const int Order = 35;
        private const int NotesShown = 3;

        static FaithCharacter()
        {
            try
            {
                CharacterPage.Add("faith", Order, Section);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: character page", e);
            }
        }

        public static CharacterSection Section(Pawn p)
        {
            if (!Faiths.Holds(p)) return null;
            var ideo = Faiths.Of(p);
            var s = new CharacterSection { title = "CC_FaithP_PageTitle".Translate() };
            float c = Faiths.Certainty(p);
            s.Line("CC_FaithP_PageHolds".Translate(ideo.name.Named("FAITH"), c.ToStringPercent().Named("PCT"), FaithPeople.StateWord(c).Named("STATE")).Resolve(),
                CertaintyTip(p));
            var role = SafeRole(ideo, p);
            if (role != null) s.Line("CC_FaithP_PageRole".Translate(p.Named("PAWN"), role.LabelCap.Named("ROLE"), ideo.name.Named("FAITH")).Resolve());
            AddDetails(s, p, ideo);
            return s;
        }

        /// <summary>
        /// The section without its first lines (which faith, how firmly, a role): the Culture tab draws those itself, with
        /// the faith's icon and a certainty bar. Null without "Faith in the colony".
        /// </summary>
        public static CharacterSection Details(Pawn p)
        {
            if (!Faiths.Holds(p)) return null;
            var s = new CharacterSection { title = "CC_FaithP_PageTitle".Translate(), order = Order, key = "faith" };
            AddDetails(s, p, Faiths.Of(p));
            return s.Empty ? null : s;
        }

        /// <summary>What it asks of them now, the faith moments lately, and who could sway them (or whom they could).</summary>
        private static void AddDetails(CharacterSection s, Pawn p, Ideo ideo)
        {
            var asks = FaithAsks.Of(p);
            if (asks.Count > 0)
                s.Line("CC_FaithP_PageAsks".Translate(ideo.name.Named("FAITH"), p.Named("PAWN"), TextUtil.JoinAnd(asks.Take(3).Select(FaithAsks.Text).ToList()).Named("LIST")).Resolve(),
                    "CC_FaithP_AsksTip".Translate());
            foreach (var n in FaithPeople.Notes(p).Take(NotesShown))
                s.Line((PersonaMarks.DateLabel(n.tick, p).Colorize(UiKit.Dim) + "  " + NoteText(n)).Trim(), n.crisis ? "CC_FaithP_NoteCrisisTip".Translate() : "CC_FaithP_NoteTalkTip".Translate());
            if (!p.IsFreeColonist) return;
            // Who could sway them, and whom they could sway.
            var others = PawnsFinder.AllMaps_FreeColonists.Where(o => o != p && Faiths.Holds(o) && !Faiths.Same(o, p) && !o.IsQuestLodger()).ToList();
            var by = others.Where(Faiths.Devout).Select(o => (o, v: FaithPeople.Expected(p, o, FaithPeopleMath.DebateSway)))
                .Where(x => x.v >= FaithPeopleMath.ShowSwayFrom).OrderByDescending(x => x.v).Take(2).ToList();
            if (by.Count > 0)
                s.Line("CC_FaithP_PageSwayedBy".Translate(p.Named("PAWN"), TextUtil.JoinAnd(by.Select(x => x.o.LabelShort).ToList()).Named("LIST")).Resolve(),
                    "CC_FaithP_SwayTip".Translate());
            if (Faiths.Devout(p))
            {
                var could = others.Select(o => (o, v: FaithPeople.Expected(o, p, FaithPeopleMath.DebateSway)))
                    .Where(x => x.v >= FaithPeopleMath.ShowSwayFrom).OrderByDescending(x => x.v).Take(3).ToList();
                if (could.Count > 0)
                    s.Line("CC_FaithP_PageCouldSway".Translate(p.Named("PAWN"), TextUtil.JoinAnd(could.Select(x => x.o.LabelShort).ToList()).Named("LIST")).Resolve(),
                        "CC_FaithP_SwayTip".Translate());
            }
        }

        /// <summary>A faith moment as the pages say it: what it was, and how far it moved them.</summary>
        public static string NoteText(FaithNote n)
        {
            if (n == null) return "";
            int pts = Mathf.RoundToInt(n.delta * 100f);
            return pts == 0 ? n.text : "CC_FaithP_NoteMoved".Translate(n.text.Named("WHAT"), (pts > 0 ? "+" + pts : pts.ToString()).Named("PTS")).Resolve();
        }

        public static string CertaintyTip(Pawn p)
        {
            var lines = new List<string> { "CC_FaithP_CertaintyTip".Translate(FaithMath.DevoutFrom.ToStringPercent().Named("DEVOUT"), FaithMath.WaveringBelow.ToStringPercent().Named("WAVERING")).Resolve() };
            try
            {
                float day = p.ideo.CertaintyChangePerDay;
                if (Mathf.Abs(day) >= 0.0005f)
                    lines.Add("CC_FaithP_CertaintyDrift".Translate((day > 0f ? "+" : "") + day.ToStringPercent("0.#")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: certainty change per day", e);
            }
            return string.Join("\n\n", lines);
        }

        /// <summary>The role they hold in their faith (leader, moral guide, a specialist), or null.</summary>
        public static Precept_Role SafeRole(Ideo ideo, Pawn p)
        {
            try
            {
                return ideo?.GetRole(p);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
