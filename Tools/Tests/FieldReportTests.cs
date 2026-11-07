using ColonyCouncil;

/// <summary>
/// Bugs the player reported from a game (7 November 2026) that live in game code, checked by what the code says:
/// a beast named a nemesis while it lay downed and dying (Strangers/Nemeses.cs), and the food left out for the beast that
/// stays, which went to the gate and was taken by a courier instead of being set down in front of the animal
/// (PlayOut/PlayOutGroupEffects.cs, PlayOut/PlayGroups.cs); and a pet's temperament that RimHUD's panel didn't show.
/// </summary>
public static class FieldReportTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the player's reports: " + what);
        }

        string nemeses = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Strangers/Nemeses.cs"));
        C(nemeses.Contains("if (!x.Downed && !x.InMentalState && !comp.social.BattleOn(x.Map)"), "a downed beast isn't said to have slunk off into the wild");

        string carry = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/PlayOut/PlayOutGroupEffects.cs"));
        C(carry.Contains("p.other = Receiver(x);"), "an errand's receiver can be an animal");
        C(carry.Contains("(((x.RaceProps?.Humanlike ?? false) && x.Faction != Faction.OfPlayer) || IsBeast(x))"), "a stranger of another people, or any animal");
        // "events/dilemmas where you provide food to an animal shouldn't have some courier pick up the food": the orphaned
        // young one is the colony's by the time its food is carried out, so the colony's own animals count too.
        C(carry.Contains("public static bool IsBeast(Pawn x) => x != null && x.RaceProps != null && x.RaceProps.Animal && !x.RaceProps.Humanlike;"),
            "every animal is fed in person, the colony's own included");
        string groupsCode = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/PlayOut/PlayGroups.cs"));
        C(groupsCode.Contains("|| DilemmaEffect_CarryOut.IsBeast(x))"), "the carrier walks to the colony's own animal too, not the gate");
        string orphan = File.ReadAllText(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_New.xml"));
        int raise = orphan.IndexOf("DilemmaEffect_Recruit");
        C(raise > 0 && orphan.IndexOf("DilemmaEffect_FeedBeast", raise) > raise, "the orphaned young: taken in first, then fed (the case the player met)");
        C(carry.Contains("&& !IsBeast(ctx.stranger))"), "food for an animal never calls a courier to the gate");
        C(carry.Contains(": Receiver(x) != null ? x.LabelShort"), "the answer says it is carried to the animal");
        string groups = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/PlayOut/PlayGroups.cs"));
        int feed = groups.IndexOf("An animal is fed by setting the food down in front of it");
        C(feed > 0 && groups.IndexOf("dropped.SetForbidden(true, false);", feed) > feed && groups.IndexOf("Feed(to, dropped);", feed) > groups.IndexOf("dropped.SetForbidden(true, false);", feed),
            "what the animal leaves isn't hauled back or taken by anyone else");
        string beast = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Animals/ResidentBeast.cs"));
        C(beast.Contains("new DilemmaEffect_CarryOut { what = what, count = count, who = PlayWho.A, to = PlaySpot.Stranger"), "the beast's food goes to the beast (PlaySpot.Stranger)");

        // "i don't see anything for my pet yet": RimHUD's panel stands in for the inspect text, so the line has a widget too.
        string hud = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Integrations/RimHudMore.cs"));
        string hudDefs = File.ReadAllText(Path.Combine(root, "Defs/Misc/Council_RimHUD_More.xml"));
        C(hud.Contains("public static class RimHud_AnimalNature") && hud.Contains("AnimalTempers.Of(pawn)") && hud.Contains("Patch_AnimalNatureInspect.Text(pawn)"),
            "RimHUD's panel shows an animal's temperament, with the inspect line's text as its tooltip");
        C(hudDefs.Contains("<defClass>ColonyCouncil.RimHud_AnimalNature</defClass>") && hudDefs.Contains("MayRequire=\"jaxe.rimhud\""), "its widget def, loaded only with RimHUD");
        return n;
    }
}
