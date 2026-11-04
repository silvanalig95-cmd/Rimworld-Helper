using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The stores that moved into the world ledger (stage 0 step 3b; World/Ledger/StoreMath.cs and LedgerMath.cs): who and
/// which towns the ledger keeps, when a tie that lapses "after so many days" lapses, and the dead a people's grudge holds.
/// Each check says what the old store did and that the ledger's record does the same.
/// </summary>
public static class LedgerMovesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "ledger moves: " + what);
        }

        const int Day = TieRules.DayTicks;

        // ---------------- Persons and towns ----------------

        Check(LedgerMath.KeepPerson(true, false, false), "a notable stays a person");
        Check(LedgerMath.KeepPerson(false, true, false), "someone a tie names stays a person");
        Check(LedgerMath.KeepPerson(false, false, true), "someone kept in the world stays a person");
        Check(!LedgerMath.KeepPerson(false, false, false), "someone nothing needs goes");
        Check(LedgerMath.KeepTown(true, true, -1, 0) && LedgerMath.KeepTown(true, false, 0, 999 * Day), "a town a notable lives in is kept, standing or gone");
        Check(!LedgerMath.KeepTown(false, true, -1, 5 * Day), "a standing town nobody lives in isn't kept (the world has it)");
        Check(LedgerMath.KeepTown(false, false, 10 * Day, 10 * Day + LedgerMath.GoneTownDays * Day - 1), "a gone town is remembered two years");
        Check(!LedgerMath.KeepTown(false, false, 10 * Day, 10 * Day + LedgerMath.GoneTownDays * Day), "and then forgotten");
        Check(!LedgerMath.KeepTown(false, false, -1, 0), "a gone town never missed isn't kept");
        Check(LedgerMath.GoneTownDays == 120, "two sixty-day years");

        return n;
    }
}
