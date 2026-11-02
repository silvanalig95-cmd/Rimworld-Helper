using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The advisor's cards as the shared suggestion core sees them: each card's key ("adv:" its check and what it is
    /// about, the same key the Plan page proposes it by), so "Not this" on the Priorities page or the Plan page sets it
    /// aside everywhere at once; and a card whose own button gives orders on the map (plans lamps, chairs, a firebreak)
    /// as a step done through the core, with Undo and a look a day or two later whether its reason has gone.
    /// </summary>
    public static class AdvisorSteps
    {
        public const string Source = "adv";

        /// <summary>The card's key: "adv:Council_Adv_BedsDark:beds-dark".</summary>
        public static string KeyOf(AdvisorSignal s) => s == null ? null : Source + ":" + (s.def?.defName ?? "?") + (s.key.NullOrEmpty() ? "" : ":" + s.key);

        /// <summary>The card's own action gives orders on the map (else it only opens something: a letter, a quest, a tab).</summary>
        public static bool ActIsStep(AdvisorSignal s) => s?.act != null && !s.actOpens && !s.actLabel.NullOrEmpty();

        /// <summary>The card as a step: its words, its level, and when its action gives orders, the doing of it with its undo and check.</summary>
        public static Suggestion StepOf(AdvisorSignal s, Map map)
        {
            if (s == null) return null;
            var step = new Suggestion
            {
                key = KeyOf(s), source = Source, area = s.def?.category.ToString(), title = s.title, reason = s.detail, level = (int)s.Level, map = map, look = s.look
            };
            if (ActIsStep(s))
            {
                var act = s.act;
                step.doLabel = s.actLabel;
                step.doTip = s.actTip.NullOrEmpty() ? s.actLabel : s.actTip;
                step.apply = () => SameRules.Orders(map, act, 2 * SuggestMath.TicksPerDay);
            }
            return step;
        }
    }
}
