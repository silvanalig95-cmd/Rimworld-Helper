namespace ColonyCouncil
{
    /// <summary>
    /// When Rimfolk clears Alpha Skills' forbidden passion (Integrations/ForbiddenPassions.cs), without game types
    /// (Tools/Tests try it): only that passion, only on a skill some work trains (its main work: the base game's work types
    /// whose first skill it is, else a mod's, else any that use it), and only where the colonist can do none of that work.
    /// </summary>
    public static class ForbiddenPassionsMath
    {
        /// <summary>The passion is the forbidden one and the colonist can never do the skill's main work: it can only weigh on them.</summary>
        public static bool Clears(bool forbiddenPassion, int mainWork, int mainWorkDisabled) =>
            forbiddenPassion && mainWork > 0 && mainWorkDisabled >= mainWork;
    }
}
