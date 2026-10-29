using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// The colony's next steps (Building/NextStepsMath): what's worth proposing, which proposals show, when a planned step
/// is done or let go, where a new step goes, ordering and moving.
/// </summary>
public static class NextStepsTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "next steps: " + what);
        }

        C(NextStepsMath.WorthProposing(0.3f, true) && !NextStepsMath.WorthProposing(0.05f, true) && !NextStepsMath.WorthProposing(0.9f, false),
            "proposed only with something to do and a score that matters");
        var scores = new List<float> { 0.1f, 0.9f, 0.5f, 0.3f, 0.7f };
        var none = new List<bool> { false, false, false, false, false };
        var picks = NextStepsMath.Propose(scores, none, none, none);
        C(picks.Count == 5 && picks[0] == 1 && picks[1] == 4 && picks[2] == 2, "proposals come most pressing first");
        var inPlan = new List<bool> { false, true, false, false, false };
        var dropped = new List<bool> { false, false, false, true, false };
        picks = NextStepsMath.Propose(scores, inPlan, none, dropped);
        C(picks.Count == 3 && !picks.Contains(1) && !picks.Contains(3), "a step already in the plan or let go isn't proposed");
        var many = new List<float>();
        var no = new List<bool>();
        for (int i = 0; i < 20; i++)
        {
            many.Add(i / 20f);
            no.Add(false);
        }
        C(NextStepsMath.Propose(many, no, no, no).Count == NextStepsMath.MaxProposed, "at most eight proposals at once");

        C(!NextStepsMath.DoneNow(true, 1) && !NextStepsMath.DoneNow(true, 2) && NextStepsMath.DoneNow(true, 3), "a sourced step is done after three looks without its source");
        C(!NextStepsMath.DoneNow(false, 10), "the player's own step is never done by itself");
        int day = 60000;
        C(NextStepsMath.StillDropped(0, 14 * day, day) && !NextStepsMath.StillDropped(0, 16 * day, day), "a let-go step stays let go a fortnight");
        C(NextStepsMath.StillShownDone(0, 4 * day, day) && !NextStepsMath.StillShownDone(0, 6 * day, day), "a done step shows five days");

        var plan = new List<float> { 0.8f, 0.5f, 0f, 0.3f };
        C(NextStepsMath.InsertAt(plan, 0.2f) == 4, "a step that isn't urgent goes to the end");
        C(NextStepsMath.InsertAt(plan, 0.6f) == 1, "an urgent step goes ahead of the first less urgent one");
        C(NextStepsMath.InsertAt(plan, 0.9f) == 0, "the most urgent of all goes first");
        C(NextStepsMath.InsertAt(new List<float>(), 0.9f) == 0, "into an empty plan, first");
        var order = NextStepsMath.ByUrgency(new List<float> { 0.2f, 0.9f, 0f, 0.9f });
        C(order[0] == 1 && order[1] == 3 && order[2] == 0 && order[3] == 2, "by urgency, equal scores keeping their order");
        C(NextStepsMath.Moved(0, 3, -1) == 0 && NextStepsMath.Moved(2, 3, 1) == 2 && NextStepsMath.Moved(1, 3, 1) == 2 && NextStepsMath.Moved(1, 3, -1) == 0,
            "moving up or down stays within the plan");
        return n;
    }
}
