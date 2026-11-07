namespace ColonyCouncil
{
    /// <summary>
    /// The colony's age, as World/Ledger/LedgerMath.cs has it (the harm rules name it: Harm/HarmMath.cs, HarmTwoMath.cs).
    /// Copied here because LedgerMath.cs brings the whole ledger with it; Tools/Tests links the real one, so the two can't
    /// drift without a test noticing (HarmTwoTests checks the four ages).
    /// </summary>
    public enum WorldAge
    {
        Tribal,
        Medieval,
        Industrial,
        Spacer
    }
}
