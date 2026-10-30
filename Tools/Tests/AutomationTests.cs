using System;
using ColonyCouncil;

/// <summary>The council's automations (Building/AutomationMath.cs): when it may act again, what the player removed, how many trees.</summary>
public static class AutomationTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "automations: " + what);
        }
        int day = AutomationMath.TicksPerDay;
        C(AutomationMath.Due(-1, 1000, day), "never done is due");
        C(!AutomationMath.Due(1000, 1000 + day - 1, day) && AutomationMath.Due(1000, 1000 + day, day), "done a day ago is due again at the day");
        C(AutomationMath.StillDropped(0, 14 * day) && !AutomationMath.StillDropped(0, 15 * day), "a bill the player removed stays unset a fortnight");
        C(AutomationMath.TreesToMark(0) == 5 && AutomationMath.TreesToMark(2) == 7 && AutomationMath.TreesToMark(3) == 10 && AutomationMath.TreesToMark(4) == 12, "more trees the louder the advice");
        C(AutomationMath.AutoKind("meals", false) && AutomationMath.AutoKind("medicine", false) && AutomationMath.AutoKind("components", false), "meals, medicine and components are set on their own");
        C(!AutomationMath.AutoKind("weapons", false) && !AutomationMath.AutoKind("blocks", false) && AutomationMath.AutoKind("blocks", true), "anything else only when the building or a bill wants it");
        C(AutomationMath.BillsAnHour == 2 && AutomationMath.PlanLevel == 3, "two bills an hour; high steps go into the plan");
        C(AutomationMath.Allowed(0, 8, 20) == 8 && AutomationMath.Allowed(6, 8, 20) == 2 && AutomationMath.Allowed(8, 8, 20) == 0 && AutomationMath.Allowed(3, 8, 2) == 2, "a day's cap bounds what is wanted");
        C(AutomationMath.BudgetLeft(0f, 200f) && AutomationMath.BudgetLeft(199f, 200f) && !AutomationMath.BudgetLeft(200f, 200f), "the budget lasts until it is spent");
        C(AutomationMath.ActionsKept >= 6 && AutomationMath.GrowDays >= 5, "a handful of actions kept to undo; sowing waits days between times");
        return n;
    }
}
