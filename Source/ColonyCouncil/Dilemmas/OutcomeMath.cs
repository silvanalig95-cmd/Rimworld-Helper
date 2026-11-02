using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Which lines an answer's outcome is told from, for dilemmas, Rimfolk events and world situations alike: the risk's
    /// when the risk came true and it has words of its own, else the plain ones; the single text first, then its
    /// variants (DilemmaChoices.Outcome picks among them by tags). Pure (Tools/Tests: OutcomeTests).
    /// </summary>
    public static class OutcomeMath
    {
        /// <summary>
        /// The lines to pick from (empty: nothing to say). <paramref name="plain"/> is the single text told,
        /// <paramref name="more"/> its variants (null or empty: <paramref name="plain"/> is told as it is).
        /// </summary>
        public static List<string> Pool(string outcome, List<string> outcomes, string riskOutcome, List<string> riskOutcomes, bool risk,
            out string plain, out List<string> more)
        {
            bool riskText = risk && (!string.IsNullOrEmpty(riskOutcome) || (riskOutcomes != null && riskOutcomes.Count > 0));
            plain = riskText ? riskOutcome : outcome;
            more = riskText ? riskOutcomes : outcomes;
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(plain)) lines.Add(plain);
            if (more != null)
                foreach (var l in more)
                    if (!string.IsNullOrEmpty(l))
                        lines.Add(l);
            return lines;
        }
    }
}
