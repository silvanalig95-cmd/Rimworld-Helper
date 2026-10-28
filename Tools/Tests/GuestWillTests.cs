using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// Whether a Hospitality guest would stay of their own will (Integrations/GuestWillMath): what they want of a home by
/// their passions and nature, the friends here that what ties them to home asks for, what taking them costs their
/// people, and how likely their family comes to ask for them back.
/// </summary>
public static class GuestWillTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "guests who'd stay: " + what);
        }

        var none = new List<string>();
        var cook = new List<(string, int, int)> { ("Cooking", 2, 9), ("Plants", 1, 12), ("Shooting", 1, 4) };
        C(GuestWillMath.Wants(cook, none, false)[0] == GuestWillMath.Want.Kitchen, "a burning passion for cooking wants a good kitchen first");
        C(GuestWillMath.Wants(cook, none, false).Count == GuestWillMath.MaxWants && GuestWillMath.Wants(cook, none, false)[1] == GuestWillMath.Want.Fields,
            "then the minor passion they're best at, and no more than two");
        C(GuestWillMath.Wants(cook, new List<string> { "Gourmand" }, false)[0] == GuestWillMath.Want.Food
          && GuestWillMath.Wants(cook, new List<string> { "Gourmand" }, false)[1] == GuestWillMath.Want.Kitchen, "a gourmand wants good food before all else");
        C(GuestWillMath.Wants(cook, new List<string> { "Ascetic" }, false).Count == 0, "an ascetic asks for nothing");
        C(GuestWillMath.Wants(cook, none, true).Count == 0, "a child asks for nothing");
        C(GuestWillMath.Wants(new List<(string, int, int)> { ("Shooting", 2, 10), ("Melee", 2, 8) }, none, false).Count == 1,
            "a shooter and a brawler both want people who can fight beside them, once");
        C(GuestWillMath.Wants(new List<(string, int, int)> { ("Mining", 2, 10) }, none, false).Count == 0, "a passion with no want of a home brings none");
        C(GuestWillMath.Wants(new List<(string, int, int)> { ("Cooking", 0, 15) }, none, false).Count == 0, "skill without passion wants nothing");

        C(GuestWillMath.WantsOwnRoom(new List<string> { "Greedy" }, false) && GuestWillMath.WantsOwnRoom(new List<string> { "Jealous" }, false)
          && GuestWillMath.WantsOwnRoom(none, true), "the greedy, the jealous and the titled want a room of their own");
        C(!GuestWillMath.WantsOwnRoom(new List<string> { "Ascetic" }, true) && !GuestWillMath.WantsOwnRoom(none, false), "an ascetic never does, nor does anyone else");

        C(GuestWillMath.ParentsHold(19f, false) && !GuestWillMath.ParentsHold(19f, true) && !GuestWillMath.ParentsHold(30f, false),
            "parents hold the young, unless they've fallen in love here");
        C(Math.Abs(GuestWillMath.DaysOfFood(64f, 10) - 4f) < 0.01f && GuestWillMath.DaysOfFood(10f, 0) > 100f, "food in store counted in days for everyone");

        C(GuestWillMath.Willing(1, true, 0), "a reason, room and what they want: they'd stay");
        C(!GuestWillMath.Willing(0, true, 0), "with no reason to stay, they won't");
        C(!GuestWillMath.Willing(1, false, 0), "with no room for one more, they won't");
        C(!GuestWillMath.Willing(1, true, 1), "one want not met is enough to keep them a guest");

        int partner = GuestWillMath.TieWeight(GuestWillMath.Tie.Partner), parent = GuestWillMath.TieWeight(GuestWillMath.Tie.Parent);
        C(partner == 2 && GuestWillMath.TieWeight(GuestWillMath.Tie.Children) == 2 && parent == 1 && GuestWillMath.TieWeight(GuestWillMath.Tie.Faith) == 1,
            "a partner or young children at home ask for two more friends here, parents or a faith for one");
        C(GuestWillMath.ExtraFriends(0, 1) == 0, "nothing tying them to home asks for no more friends");
        C(GuestWillMath.ExtraFriends(partner, 1) == 2 && GuestWillMath.ExtraFriends(partner + parent, 1) == 3, "ties at home add up in friends, never bar them");
        C(GuestWillMath.ExtraFriends(partner, 2) == 1 && GuestWillMath.ExtraFriends(partner, 3) == 0, "each reason to stay beyond the first makes up for one");
        C(GuestWillMath.ExtraFriends(partner, 0) == 2, "with no reason at all, nothing is made up for");

        C(GuestWillMath.CostFactor(false, false, false) < 1f && GuestWillMath.CostFactor(true, false, false) > 1f,
            "taking someone with no family among their people costs less, with family more");
        C(GuestWillMath.CostFactor(true, true, false) > GuestWillMath.CostFactor(true, false, false) * 1.9f, "their leader's family costs twice");
        C(GuestWillMath.CostFactor(true, true, true) <= 2.5f && GuestWillMath.CostFactor(false, false, false) >= 0.5f, "the factor stays between a half and two and a half");

        C(GuestWillMath.AskChance("partner", 50f, false) > GuestWillMath.AskChance("parent", 50f, false)
          && GuestWillMath.AskChance("parent", 50f, false) > GuestWillMath.AskChance("sibling", 50f, false), "a partner comes before a parent, a parent before a sibling");
        C(GuestWillMath.AskChance("parent", 80f, false) > GuestWillMath.AskChance("parent", 0f, false), "the more they think of them, the likelier");
        C(GuestWillMath.AskChance("sibling", 40f, true) > GuestWillMath.AskChance("sibling", 40f, false), "taken by force, likelier still");
        C(GuestWillMath.AskChance("cousin", 100f, true) == 0f && GuestWillMath.AskChance("partner", 100f, true) <= 1f, "only close family comes, and the chance is a chance");
        C(GuestWillMath.KinRank("partner") < GuestWillMath.KinRank("parent") && GuestWillMath.KinRank("child") < GuestWillMath.KinRank("sibling"),
            "family comes in order: partner, parent, child, sibling");
        C(GuestWillMath.Longing("partner", 50f) > GuestWillMath.Longing("sibling", 50f), "kept from a partner weighs more than from a sibling");
        C(GuestWillMath.AskMinDays < GuestWillMath.AskMaxDays && GuestWillMath.KeptGoodwill > GuestWillMath.HomeGoodwill, "family comes after weeks, and keeping them costs more than letting go gains");
        return n;
    }
}
