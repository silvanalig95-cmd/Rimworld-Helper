using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>A camp's stay as a story and its coming back (Camps/CampStoryMath): what's remembered, who returns and when, the beats of a stay, a war party's size.</summary>
public static class CampStoryTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp story: " + what);
        }

        // ---------------- Remembered ----------------
        C(CampStoryMath.Remembered("time", true, 10f), "a camp that stayed its time and left is remembered");
        C(CampStoryMath.Remembered("raid", true, 30f), "one that fled a raid is remembered too");
        C(!CampStoryMath.Remembered("forced", true, 10f), "one driven off isn't");
        C(!CampStoryMath.Remembered("fight", true, 10f) && !CampStoryMath.Remembered("harmed", true, 0f), "nor one fought or harmed");
        C(!CampStoryMath.Remembered("time", true, 70f), "nor one whose stay went sour");
        C(!CampStoryMath.Remembered("time", false, 0f), "nor one turned back before it made camp");

        // ---------------- Coming back ----------------
        C(CampStoryMath.ReturnChance(10f, 0, 0, false, 0) == 0f, "no return within a month");
        C(CampStoryMath.ReturnChance(300f, 3, 3, true, 0) == 0f, "nor after they've been forgotten");
        C(CampStoryMath.ReturnChance(40f, 0, 0, false, CampStoryMath.MaxReturns) == 0f, "nor past the third return");
        float plain = CampStoryMath.ReturnChance(40f, 0, 0, false, 0), warm = CampStoryMath.ReturnChance(40f, 3, 2, false, 0), asked = CampStoryMath.ReturnChance(40f, 0, 0, true, 0);
        C(plain > 0.2f && plain < 0.4f, $"a plain stay comes back now and then ({plain:0.00})");
        C(warm > plain && asked > plain, "a warm stay, or an invitation, brings them back more surely");
        C(CampStoryMath.ReturnChance(40f, 0, 0, false, 1) < plain, "each return makes the next less likely");
        C(CampStoryMath.ReturnChance(40f, 9, 9, true, 0) <= 0.75f, "and never a certainty");

        var kept = CampStoryMath.Kept(6, 0, 3, new List<float> { 0f, 5f, 1f, 0f, 9f, 2f });
        C(kept.Count == CampStoryMath.KeepPeople, "four of six are kept");
        C(kept[0] == 0 && kept[1] == 3, "the one who speaks for them and their trader first");
        C(kept[2] == 4 && kept[3] == 1, "then those the colony liked best");
        C(CampStoryMath.Kept(2, 0, -1, null).Count == 2, "a pair is kept whole");
        C(CampStoryMath.Kept(0, 0, 0, null).Count == 0, "nobody from nobody");

        // ---------------- The stay's shape ----------------
        C(!CampStoryMath.AskDue(1f, 5f) && CampStoryMath.AskDue(1.5f, 5f), "the ask comes a day and a half in");
        C(!CampStoryMath.AskDue(1.5f, 1.8f), "but not when they're about to leave");
        C(!CampStoryMath.HasTrouble(2f) && CampStoryMath.HasTrouble(4f), "a short stay has no trouble; a longer one does");
        C(!CampStoryMath.TroubleDue(1.9f, 4f) && CampStoryMath.TroubleDue(2f, 4f), "trouble comes midway");
        C(!CampStoryMath.TroubleDue(3.6f, 4f), "and not in the last half day");
        C(!CampStoryMath.PartingDue(2f, 4f) && CampStoryMath.PartingDue(3.2f, 4f) && !CampStoryMath.PartingDue(4.1f, 4f), "the parting word comes in the last day, before they go");
        C(!CampStoryMath.PartingDue(1f, 1.5f), "a camp of a night and a day says no parting word");
        C(CampStoryMath.Provisions(4) == 8 && CampStoryMath.Provisions(1) == 2, "provisions are two meals a head");

        // ---------------- Neighbours for good ----------------
        C(CampStoryMath.SettleOffered(true, 10f, 4f, true), "a family that could settle, after a good long stay, asks to");
        C(!CampStoryMath.SettleOffered(false, 10f, 4f, true), "a kind that doesn't settle never asks");
        C(!CampStoryMath.SettleOffered(true, 50f, 4f, true), "nor one whose stay went badly");
        C(!CampStoryMath.SettleOffered(true, 10f, 2f, true), "nor after a short stay");
        C(!CampStoryMath.SettleOffered(true, 10f, 4f, false), "nor with neighbours settled on the land already");
        C(CampStoryMath.HoldingProvisions(1) == 3 && CampStoryMath.HoldingProvisions(5) == 15, "settled neighbours find three meals a head a day");
        C(CampStoryMath.HoldingTensionADay < 0f, "their unease eases with time");

        // ---------------- A war party ----------------
        C(CampStoryMath.Fighters(100f) == 3 && CampStoryMath.Fighters(600f) == 5 && CampStoryMath.Fighters(5000f) == 7, "three to seven fighters, by the threat");
        C(CampStoryMath.WarPoints(100f) == 150f && CampStoryMath.WarPoints(1000f) == 550f && CampStoryMath.WarPoints(9000f) == 1400f, "they come about half as strong as the threat, within bounds");
        C(CampStoryMath.WarChance(0, false) > 0.15f && CampStoryMath.WarChance(100, true) <= 0.6f, "help is likelier from close friends with an old quarrel, never certain");
        C(CampStoryMath.WarChance(-50, false) >= 0.1f, "and never quite impossible");
        C(CampStoryMath.HelperWeight(50, true) == 3f * CampStoryMath.HelperWeight(50, false), "a people at odds with the attacker is three times as likely to come");
        return n;
    }
}
