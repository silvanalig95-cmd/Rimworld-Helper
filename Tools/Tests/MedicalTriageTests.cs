using System;
using ColonyCouncil;

/// <summary>
/// Medical triage with Move the Patient (Integrations/MedicalTriageMath): the best bed (the player's priority, then the
/// game's medical quality, then the nearest), a surgery bed first for an operation and open to others only while nobody
/// waits for one, who outranks whom for a bed, who may be moved out of one, and the four hours before anyone is moved
/// again. The player's case: a hospital bed at High and marked for surgery, two ordinary beds at Normal.
/// </summary>
public static class MedicalTriageTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "medical triage: " + what);
        }

        const int Low = MedicalTriageMath.Low, Normal = MedicalTriageMath.Normal, High = MedicalTriageMath.High, Critical = MedicalTriageMath.Critical;
        C(Low < Normal && Normal < High && High < Critical && Low == 1 && Critical == 4, "the mod's four priorities, in its own numbers");

        // ---------- The game's medical quality ----------
        float hospitalClean = MedicalTriageMath.Quality(0.10f, 1.11f, 0.7f);
        float bedClean = MedicalTriageMath.Quality(0f, 1.07f, 0.7f);
        float hospitalDirty = MedicalTriageMath.Quality(0.10f, 1.11f, 1.3f);
        C(Math.Abs(hospitalClean - 0.51f) < 0.0001f, $"a hospital bed in a clean room: tend 0.10, immunity 0.11, infection 0.3 off ({hospitalClean})");
        C(hospitalClean > bedClean, "in the same room a hospital bed treats better than a plain bed");
        C(bedClean > hospitalDirty, "a plain bed in a clean room treats better than a hospital bed in a dirty one");

        // ---------- The best bed ----------
        var rank = new Func<int, float, float, MedicalTriageMath.Rank>((p, q, d) => new MedicalTriageMath.Rank(p, q, d));
        C(MedicalTriageMath.Better(rank(High, 0f, 900f), rank(Normal, 0.5f, 1f)), "the player's priority comes first: a High bed over a better Normal one");
        C(MedicalTriageMath.Better(rank(Normal, 0.51f, 900f), rank(Normal, 0.37f, 1f)), "the same priority: the better treated wins, however far");
        C(MedicalTriageMath.Same(rank(Normal, 0.370f, 900f), rank(Normal, 0.371f, 1f)), "near the same quality: equally good");
        C(MedicalTriageMath.Compare(rank(Normal, 0.370f, 1f), rank(Normal, 0.371f, 900f)) < 0, "equally good: the nearer first");
        C(MedicalTriageMath.Compare(rank(Low, 0.9f, 1f), rank(Critical, 0f, 900f)) > 0, "a Low bed after a Critical one");
        C(MedicalTriageMath.Compare(rank(High, 0.3f, 4f), rank(High, 0.3f, 4f)) == 0, "a bed is as good as itself");
        // The steps keep the order whole: no three beds go round in a circle.
        var a = rank(Normal, 0.3700f, 1f);
        var b = rank(Normal, 0.3724f, 2f);
        var c = rank(Normal, 0.3749f, 3f);
        bool ab = MedicalTriageMath.Compare(a, b) < 0, bc = MedicalTriageMath.Compare(b, c) < 0, ac = MedicalTriageMath.Compare(a, c) < 0;
        C(!(ab && bc) || ac, "the order is transitive across nearly equal beds");

        // ---------- An operation and the surgery bed ----------
        C(MedicalTriageMath.CompareFor(true, true, rank(Normal, 0f, 900f), false, rank(Critical, 0.5f, 1f)) < 0,
            "waiting for an operation: the surgery bed first, whatever the other's rank");
        C(MedicalTriageMath.CompareFor(false, true, rank(Normal, 0f, 1f), false, rank(High, 0f, 1f)) > 0, "anyone else: the surgery bed by its rank");
        C(MedicalTriageMath.BetterFor(true, true, rank(Low, 0f, 1f), false, rank(High, 0.5f, 1f)), "an operation is better off in the surgery bed");
        C(!MedicalTriageMath.BetterFor(true, false, rank(High, 0.5f, 1f), true, rank(Low, 0f, 1f)), "and never moved out of it for a better ordinary bed");
        C(MedicalTriageMath.SurgeryBedOpen(true, true), "the surgery bed is open to one waiting for an operation");
        C(MedicalTriageMath.SurgeryBedOpen(false, false), "and to anyone while nobody waits for one");
        C(!MedicalTriageMath.SurgeryBedOpen(false, true), "but not to an ordinary patient while someone waits for one");

        // ---------- Who outranks whom ----------
        C(MedicalTriageMath.Outranks(Critical, false, High, false, false), "Critical over High");
        C(MedicalTriageMath.Outranks(High, false, Normal, false, false), "High over Normal");
        C(MedicalTriageMath.Outranks(Normal, false, Low, false, false), "Normal over Low");
        C(!MedicalTriageMath.Outranks(High, false, High, false, false), "never the same: nobody is carried out for an equal");
        C(!MedicalTriageMath.Outranks(Normal, false, High, false, false), "never the lighter over the worse");
        C(!MedicalTriageMath.Outranks(High, true, Normal, false, false), "an operation claims only a surgery bed: elsewhere by severity, like anyone");
        C(MedicalTriageMath.Outranks(Low, true, Critical, false, true), "an operation claims the surgery bed from an ordinary patient, however serious");
        C(!MedicalTriageMath.Outranks(Critical, false, Low, true, true), "nobody takes the surgery bed from one waiting for their operation");
        C(!MedicalTriageMath.Outranks(Critical, true, Low, true, true), "not even another operation");
        C(MedicalTriageMath.Outranks(Critical, false, Normal, false, true), "two ordinary patients in a surgery bed: by severity");

        // ---------- Who may be moved ----------
        const int Hour = 2500;
        var none = MedicalTriageMath.Hold.None;
        C(MedicalTriageMath.MayMove(false, false, false, false, false, true, true, -1, 10 * Hour) == none, "a patient lying quietly, another bed free: may be moved");
        C(MedicalTriageMath.MayMove(true, false, false, false, false, true, true, -1, 0) == MedicalTriageMath.Hold.Operating, "never mid-operation");
        C(MedicalTriageMath.MayMove(false, true, false, false, false, true, true, -1, 0) == MedicalTriageMath.Hold.BeingTended, "never while being tended");
        C(MedicalTriageMath.MayMove(false, false, true, false, false, true, true, -1, 0) == MedicalTriageMath.Hold.MentalState, "never in a mental state");
        C(MedicalTriageMath.MayMove(false, false, false, true, false, true, true, -1, 0) == MedicalTriageMath.Hold.InLabor, "never in labour");
        C(MedicalTriageMath.MayMove(false, false, false, false, true, true, true, -1, 0) == MedicalTriageMath.Hold.Carried, "never someone already being carried");
        C(MedicalTriageMath.MayMove(false, false, false, false, false, false, false, -1, 0) == MedicalTriageMath.Hold.NoOtherBed, "never when no other medical bed is free");
        C(MedicalTriageMath.MayMove(false, false, false, false, false, true, false, -1, 0) == MedicalTriageMath.Hold.OtherKind, "never when the only free bed is of the other kind");
        C(MedicalTriageMath.MayMove(true, true, true, true, true, false, false, 0, 1) == MedicalTriageMath.Hold.Operating, "the operation is named first");
        C(MedicalTriageMath.SameKind(false, false, false, false), "a colonist into a colonist's bed");
        C(!MedicalTriageMath.SameKind(false, false, true, false), "never a colonist into a prisoner's bed");
        C(!MedicalTriageMath.SameKind(true, false, false, false), "never a prisoner into a colonist's bed");
        C(MedicalTriageMath.SameKind(true, false, true, false), "a prisoner into a prisoner's bed");
        C(!MedicalTriageMath.SameKind(false, true, false, false) && MedicalTriageMath.SameKind(false, true, false, true), "a slave only into a slave's bed");

        // ---------- The cooldown ----------
        C(MedicalTriageMath.CooldownTicks == 4 * Hour, "four hours before someone is moved again");
        C(!MedicalTriageMath.Recently(-1, 100), "never moved: free to move");
        C(MedicalTriageMath.Recently(1000, 1000 + 4 * Hour - 1), "within four hours: held");
        C(!MedicalTriageMath.Recently(1000, 1000 + 4 * Hour), "after four hours: free again");
        C(!MedicalTriageMath.Recently(50000, 10), "a clock that went back (another game) holds nobody");
        C(MedicalTriageMath.MayMove(false, false, false, false, false, true, true, 1000, 1000 + Hour) == MedicalTriageMath.Hold.Recently,
            "moved an hour ago: held where they are");
        C(!MedicalTriageMath.GiveUp(1000, 1000 + 4 * Hour - 1) && MedicalTriageMath.GiveUp(1000, 1000 + 4 * Hour), "a move not done in four hours is given up");
        C(MedicalTriageMath.GiveUp(1000, 10), "and one from another game's clock at once");
        C(MedicalTriageMath.LineTicks > MedicalTriageMath.CooldownTicks / 2 && MedicalTriageMath.SoonTicks == Hour, "the moved patient's line shows for hours, the beds looked at for an hour");

        // ---------- The player's case: a hospital bed at High and marked for surgery, two plain beds at Normal ----------
        var hospital = rank(High, 0.51f, 100f);
        var plain1 = rank(Normal, 0.37f, 4f);
        var plain2 = rank(Normal, 0.37f, 9f);
        C(MedicalTriageMath.Compare(hospital, plain1) < 0 && MedicalTriageMath.Compare(hospital, plain2) < 0, "the hospital bed is the best, though further");
        C(MedicalTriageMath.SurgeryBedOpen(false, false), "with the surgery mark and no operation waiting, an injured colonist may take it");
        C(MedicalTriageMath.Outranks(High, false, Normal, false, true), "a tended patient in it (Normal) is moved for a newcomer with open wounds (High)");
        C(MedicalTriageMath.Outranks(Low, true, High, false, true), "and anyone ordinary in it for an operation");
        C(MedicalTriageMath.Outranks(High, false, Normal, false, false), "without the mark: the same by severity");
        C(!MedicalTriageMath.Outranks(Low, true, High, false, false), "without the mark an operation has no claim of its own");
        C(MedicalTriageMath.MayMove(false, false, false, false, false, false, false, -1, 0) == MedicalTriageMath.Hold.NoOtherBed,
            "both plain beds taken: nobody is moved");
        return n;
    }
}
