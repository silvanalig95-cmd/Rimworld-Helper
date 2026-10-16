using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    public static partial class Interplay
    {
        // ---------------- Life goals and personal stories in who someone is ----------------

        /// <summary>
        /// A colonist's life goal and personal story as tags (PeopleTags), like their quirks: "goal:Marry",
        /// "goal:TeenLove", "story:Suitors" (their own story), "instory:Suitors" (someone else's, as its other person or
        /// third). Dreams, moments, social events, dilemma roles and flavour lines can ask for them by name.
        /// </summary>
        public static void AddLifeTags(Pawn p, HashSet<string> tags)
        {
            if (!On || p == null || tags == null) return;
            var comp = Comp;
            if (comp?.aspirations != null && S.aspirations)
            {
                var goal = comp.aspirations.Active(p);
                if (goal?.def != null) tags.Add("goal:" + GoalName(goal.def.defName));
            }
            var arcs = ArcTracker.On ? ArcTracker.Instance : null;
            if (arcs == null) return;
            var mine = arcs.Of(p);
            if (mine?.def != null) tags.Add("story:" + ShortName(mine.def.defName, "Council_Arc_"));
            foreach (var a in arcs.active)
                if (a?.def != null && a.pawn != p && (a.other == p || a.third == p))
                    tags.Add("instory:" + ShortName(a.def.defName, "Council_Arc_"));
        }

        /// <summary>A def's name without its family prefix: Council_Arc_Suitors as Suitors.</summary>
        public static string ShortName(string defName, string prefix) =>
            defName.NullOrEmpty() || !defName.StartsWith(prefix) ? defName : defName.Substring(prefix.Length);

        /// <summary>
        /// A life goal's name as its tag says it: everything up to the first underscore after Council_ goes, so
        /// Council_Asp_Marry is Marry, Council_Asp4_TeenLove is TeenLove and Council_AspH_TenLives is TenLives.
        /// </summary>
        public static string GoalName(string defName)
        {
            string rest = ShortName(defName, "Council_");
            if (rest == null || !rest.StartsWith("Asp")) return rest;
            int cut = rest.IndexOf('_');
            return cut >= 0 && cut < rest.Length - 1 ? rest.Substring(cut + 1) : rest;
        }
    }
}
