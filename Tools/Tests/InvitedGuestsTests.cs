using ColonyCouncil;

/// <summary>
/// Invited visitors as Hospitality's guests (the player's request, 10 November 2026: "visitors that you invite with
/// diplomatic actions should also be treated as guests with hospitality"): which arrivals are invited, how long
/// Hospitality's stay is set for, who ends it, who leads where they keep, when they're handed over (InvitedGuestsMath),
/// and the seams: Hospitality's own way of making guests by name, each invited arrival through it with Rimfolk's own group
/// as the fallback, the feast and the envoy keeping their purpose on top, the arrivals that stay Rimfolk's own.
/// </summary>
public static class InvitedGuestsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "invited visitors as Hospitality's guests: " + what);
        }

        // ---------- Who is invited ----------
        var invited = new[] { ArrivalWhy.LetterInvitation, ArrivalWhy.TownInvited, ArrivalWhy.TownFair, ArrivalWhy.NotableGuest, ArrivalWhy.FeastGuests, ArrivalWhy.Envoy };
        var not = new[] { ArrivalWhy.HostingMission, ArrivalWhy.Errand, ArrivalWhy.Story, ArrivalWhy.Joining };
        C(invited.Length + not.Length == Enum.GetValues<ArrivalWhy>().Length, "every kind of arrival is one or the other");
        foreach (var a in invited) C(InvitedGuestsMath.Invited(a) && InvitedGuestsMath.WhyNot(a) == null, $"{a} is invited");
        foreach (var a in not) C(!InvitedGuestsMath.Invited(a) && !string.IsNullOrEmpty(InvitedGuestsMath.WhyNot(a)), $"{a} stays Rimfolk's own ({InvitedGuestsMath.WhyNot(a)})");

        // ---------- Who ends the stay, who steers it, how long ----------
        foreach (var a in Enum.GetValues<ArrivalWhy>())
        {
            bool ours = a == ArrivalWhy.FeastGuests || a == ArrivalWhy.Envoy;
            C(InvitedGuestsMath.RimfolkEnds(a) == ours, $"{a}: {(ours ? "Rimfolk ends the stay at its own time" : "Hospitality's stay runs out on its own")}");
            C(InvitedGuestsMath.UpsetWhenLost(a) == !ours, $"{a}: {(ours ? "Rimfolk keeps the count of losses, not Hospitality" : "Hospitality's own displeasure at losing the group")}");
            C(InvitedGuestsMath.RimfolkSteers(a) == (a == ArrivalWhy.FeastGuests), $"{a}: {(a == ArrivalWhy.FeastGuests ? "the feast keeps them where the day takes them" : "Hospitality's guest area")}");
        }
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.LetterInvitation, 10f) == 25000, "a letter's guest stays the hours Rimfolk planned (10 hours)");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.TownInvited, 9f) == 22500 && InvitedGuestsMath.StayTicks(ArrivalWhy.NotableGuest, 20f) == 50000, "a town's folk and a guest of note the same");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.LetterInvitation, 1f) == (int)(InvitedGuestsMath.MinHours * 2500) && InvitedGuestsMath.StayTicks(ArrivalWhy.TownFair, float.NaN) == (int)(InvitedGuestsMath.MinHours * 2500),
            "never shorter than a few hours, an unknown length the shortest");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.FeastGuests, 48f) == (48 + 24) * 2500, "a feast's guests: its two days and one to spare, so Hospitality never sends them first");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.Envoy, 15f * 24f) == (int)(InvitedGuestsMath.MaxHours * 2500) && InvitedGuestsMath.MaxHours == 16f * 24f,
            "an envoy: the season and a day, the longest anyone stays");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.LetterInvitation, 9999f) == (int)(InvitedGuestsMath.MaxHours * 2500), "nobody past the longest, whatever is asked");
        C(InvitedGuestsMath.StayTicks(ArrivalWhy.LetterInvitation, 10f, 1000) == 10000, "in the game's own hour");

        // ---------- When they're handed over ----------
        C(InvitedGuestsMath.HandOver(ArrivalWhy.Envoy, true, true, false, 2, 0), "invited, Hospitality there, the setting on, at peace, all people: handed over");
        C(!InvitedGuestsMath.HandOver(ArrivalWhy.Story, true, true, false, 1, 0) && !InvitedGuestsMath.HandOver(ArrivalWhy.HostingMission, true, true, false, 3, 0), "not when uninvited");
        C(!InvitedGuestsMath.HandOver(ArrivalWhy.Envoy, false, true, false, 2, 0), "not without Hospitality (or its way of making guests not found)");
        C(!InvitedGuestsMath.HandOver(ArrivalWhy.Envoy, true, false, false, 2, 0), "not with the setting or the deeper hooks off");
        C(!InvitedGuestsMath.HandOver(ArrivalWhy.FeastGuests, true, true, true, 2, 0), "not when their people turned hostile");
        C(!InvitedGuestsMath.HandOver(ArrivalWhy.FeastGuests, true, true, false, 0, 0) && !InvitedGuestsMath.HandOver(ArrivalWhy.FeastGuests, true, true, false, 2, 1),
            "not with nobody there, nor with anyone in the group who isn't one of their people on the map (an animal, someone not come)");

        // ---------- Who leads where they keep ----------
        C(InvitedGuestsMath.Who(false, true, true, true) == InvitedGuestsMath.Lead.Hospitality, "walking in or out: Hospitality's");
        C(InvitedGuestsMath.Who(true, true, true, true) == InvitedGuestsMath.Lead.Indoors && InvitedGuestsMath.Who(true, true, true, false) == InvitedGuestsMath.Lead.Indoors,
            "in danger: indoors, whatever the day");
        C(InvitedGuestsMath.Who(true, true, false, true) == InvitedGuestsMath.Lead.Focus, "in danger with no indoors place of the visit's own: still where the visit wants them");
        C(InvitedGuestsMath.Who(true, false, true, true) == InvitedGuestsMath.Lead.Focus, "otherwise where the visit wants them");
        C(InvitedGuestsMath.Who(true, false, true, false) == InvitedGuestsMath.Lead.Hospitality, "with nowhere the visit wants them (an envoy's day): Hospitality's guest area");

        // ---------- The seams: Hospitality's own way ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string R(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string ig = R("Integrations/InvitedGuests.cs");
        C(ig.Contains("GenTypes.GetTypeInAnyAssembly(\"Hospitality.IncidentWorker_VisitorGroup\")") && ig.Contains("AccessTools.Method(worker, \"CreateLord\",")
          && ig.Contains("new[] { typeof(Faction), typeof(IntVec3), typeof(List<Pawn>), typeof(Map), typeof(bool), typeof(bool), typeof(int) }"),
            "Hospitality's own way of making guests, its incident's CreateLord, by name");
        C(ig.Contains("createLord.Invoke(null, new object[] { f, spot, list, map, false, InvitedGuestsMath.UpsetWhenLost(why), InvitedGuestsMath.StayTicks(why, hours) });"),
            "handed over with no letter of Hospitality's (Rimfolk's says it), its displeasure and its stay by the rules");
        C(ig.Contains("AccessTools.Method(worker, \"GiveItems\", new[] { typeof(IEnumerable<Pawn>) })") && ig.Contains("giveItems.Invoke(null, new object[] { list });"),
            "with what its own visitors carry: money for a bed and a meal, wares to sell");
        C(ig.Contains("\"Hospitality.LordJob_VisitColony\"") && ig.Contains("\"Hospitality.LordToil_VisitPoint\"") && ig.Contains("visitJob.IsInstanceOfType(l.LordJob)"),
            "its guests' group and their stay known by type name");
        C(ig.Contains("if (Hosted(lord) && list.All(p => p.GetLord() == lord)) return lord;") && ig.Contains("Lords.Leave(p);"),
            "only a group taken whole counts; a half-made one is let go for Rimfolk's own, and ended once empty");
        C(ig.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.invitedAsGuests ?? true) && People.ModActive(Guests.Mod);"),
            "behind the setting, the deeper game hooks and Hospitality loaded");
        int tries = System.Text.RegularExpressions.Regex.Matches(ig, @"\btry\b").Count, logs = System.Text.RegularExpressions.Regex.Matches(ig, @"CouncilLog\.ErrorOnce\(").Count;
        C(tries >= 8 && logs >= tries, $"everything that touches Hospitality is caught and logged once ({tries} tries, {logs} logs)");
        C(ig.Contains("tr.triggers.Any(t => t is Trigger_TicksPassed) && tr.sources.Any(s => visitToil.IsInstanceOfType(s))") && ig.Contains("end.Execute(l);")
          && ig.Contains("l.GotoToil(end.target);"), "their stay ends as Hospitality's own does when its time is up; still walking in, straight to its way out");
        C(ig.Contains("public static void GoHome(Lord l, string memo)") && ig.Contains("if (Hosted(l))") && ig.Contains("l.ReceiveMemo(memo);"),
            "a visit's word to go home: Hospitality's stay ended, else Rimfolk's own memo");
        C(ig.Contains("joinLate = AccessTools.Method(visitToil, \"JoinLate\", new[] { typeof(Pawn) })") && ig.Contains("\"OnGuestJoinedLate\""),
            "one of them up again rejoins the way Hospitality takes a guest in late");
        C(ig.Contains("var lead = InvitedGuestsMath.Who(Staying(l), danger, indoors != null, duty != null && focus.IsValid);") && ig.Contains("p.mindState.duty = new PawnDuty(def, cell, r);")
          && ig.Contains("l.CurLordToil.UpdateAllDuties();"), "steered by the rule: Rimfolk's duty at its place, else Hospitality's own duties given back");
        C(ig.Contains("[HarmonyPatch(typeof(Lord), nameof(Lord.Notify_PawnDamaged))]") && ig.Contains("by.Faction != Faction.OfPlayer") && ig.Contains("Outsiders.InBout(victim, by as Pawn)")
          && ig.Contains("Feasts.Harmed(Homes.KeyOf(fmap));") && ig.Contains("Envoys.Harmed(Homes.KeyOf(emap));"),
            "hurt by the colony is heard as from Rimfolk's own group (an agreed bout is no harm)");

        // ---------- The seams: each invited arrival ----------
        string helpers = R("Happenings/HappeningHelpers.cs");
        C(helpers.Contains("public static Lord Invited(ArrivalWhy why, List<Pawn> pawns, Map map, Faction f, IntVec3? chill, float hours) => Visit(pawns, map, f, chill, hours, why);")
          && helpers.Contains("var hosted = invited.HasValue ? InvitedGuests.HandOver(invited.Value, pawns, map, f, spot, hours) : null;")
          && helpers.Contains("if (hosted != null) return hosted;\n            return LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand("),
            "an invited visit walks in as any visit, then is Hospitality's, else the game's own visit as before");
        C(R("World/NeighbourLetters.cs").Contains("Arrivals.Invited(ArrivalWhy.LetterInvitation, new List<Pawn> { x }, map, f, Gathering(map), Rand.Range(8f, 14f))"),
            "a letter's invitation taken up (a pen friend's visit, a gathering day)");
        string deal = R("Dealings/Dealings.cs");
        C(deal.Contains("invite ? ArrivalWhy.TownInvited : ArrivalWhy.TownFair") && deal.Contains("Arrivals.Invited(why, pawns, home, f, null, hours)"), "a town's folk asked to the colony's feast or fair");
        C(R("Happenings/Workers_Living.cs").Contains("Arrivals.Invited(ArrivalWhy.NotableGuest, new List<Pawn> { x }, map, m.faction, GatheringSpot(map), Rand.Range(18f, 26f))"),
            "a guest of note who wrote ahead and was told yes");
        string feasts = R("Diplomacy/Feasts.cs");
        C(feasts.Contains("if (InvitedGuests.HandOver(ArrivalWhy.FeastGuests, party, map, group.Key, gate, StayHours) == null)\n                        LordMaker.MakeNewLord(group.Key, new LordJob_FeastGuests(")
          && feasts.Contains("public const float StayHours = 2f * GenDate.HoursPerDay;"), "a feast's (or a wedding's) guests, their own group as the fallback");
        C(feasts.Contains("(l?.LordJob is LordJob_FeastGuests || InvitedGuests.Hosted(l)) && l.ownedPawns.Any(x => p.GuestOf(x) != null)"), "the feast finds its guests in Hospitality's care too");
        C(feasts.Contains("else if (InvitedGuests.Hosted(l)) InvitedGuests.Steer(l, FeastDefOf.Rimfolk_FeastGuest, cell, radius, FeastDefOf.Rimfolk_FeastIndoors);")
          && feasts.Contains("p.focus = cell;") && feasts.Contains("SteerHosted(map, p);\n            Watch(p, map);"),
            "the feast still takes them to the gate, the contests, the table and indoors for the night, at every look");
        C(feasts.Contains("x.GetLord()?.CurLordToil is LordToil_FeastGuests || InvitedGuests.Staying(x)") && feasts.Contains("!(p.GetLord()?.CurLordToil is LordToil_FeastGuests) && !InvitedGuests.Staying(p)"),
            "settled at the gate and fed and bedded by the feast's hosting rules as before");
        C(feasts.Contains("InvitedGuests.GoHome(l, LordJob_FeastGuests.MemoLeave);"), "they leave at first light as the feast ends, Hospitality's stay ended with it");
        C(feasts.Contains("internal static void HostedLook(DiplomacyHome h)") && feasts.Contains("Rare(Homes.KeyOf(map));"), "the feast's look every 250 ticks, as their own group gave it");
        string book = R("Diplomacy/FeastBook.cs");
        C(book.Contains("Scribe_Values.Look(ref focus, \"focus\", IntVec3.Invalid);") && book.Contains("Scribe_Values.Look(ref focusRadius, \"focusRadius\", 6f);"),
            "where the feast wants them is kept with it, so a reload steers them back");
        string envoys = R("Diplomacy/Envoys.cs");
        C(envoys.Contains("if (InvitedGuests.HandOver(ArrivalWhy.Envoy, party, map, f, spot, EnvoyMath.StayDays * GenDate.HoursPerDay) == null)\n                    LordMaker.MakeNewLord(f, new LordJob_EnvoyStay("),
            "an envoy for a season, their own group as the fallback");
        C(envoys.Contains("(l?.LordJob is LordJob_EnvoyStay || InvitedGuests.Hosted(l))") && envoys.Contains("!(p.GetLord()?.CurLordToil is LordToil_EnvoyStay) && !InvitedGuests.Staying(p)"),
            "the stay finds them in Hospitality's care, and still feeds and beds them, seats the envoy at a meeting and sends their letters home");
        C(envoys.Contains("InvitedGuests.Rejoin(lord, p)") && envoys.Contains("InvitedGuests.HandOver(ArrivalWhy.Envoy, lost, map, s.people, spot, left)"), "one of them up again goes back to the stay");
        C(System.Text.RegularExpressions.Regex.Matches(envoys, @"InvitedGuests\.GoHome\(").Count == 3 && !envoys.Contains("ReceiveMemo(LordJob_EnvoyStay.MemoLeave)"),
            "the season over, the envoy dead, or the home lost: they go, Hospitality's stay ended with it");
        C(envoys.Contains("InvitedGuests.Steer(lord, null, IntVec3.Invalid, 0f, RansomDefOf.Rimfolk_EnvoyIndoors);") && envoys.Contains("if (s.meals == 0) s.meals = 1;")
          && envoys.Contains("Night(s, map, bed == s.bed ? EnvoyNight.Own : EnvoyNight.Spare, bed);"),
            "indoors in danger; a meal or a night Hospitality gave them first still counts for the stay");
        string help = R("Integrations/GuestHelp.cs");
        C(help.Contains("bool invited = InvitedGuests.Following;") && help.Contains("InvitedGuests.Rare();"), "the look rides on the guests' 250-tick pass, only while a feast or an envoy is at a home");

        // ---------- The seams: what stays Rimfolk's own ----------
        C(R("Missions/Workers_MissionRemembered.cs").Contains("Arrivals.Visit(people, map, m.faction, spot, hours)"), "a hosting mission's guests (their people asked; the colony's own table and beds are what it counts)");
        foreach (var (file, job) in new[] { ("Diplomacy/Wards.cs", "new LordJob_WardParty("), ("Diplomacy/MatchDays.cs", "new LordJob_MatchEscort("), ("Diplomacy/TruceParley.cs", "new LordJob_Parley("),
                     ("Diplomacy/FealtyOath.cs", "new LordJob_FealtyOath("), ("Diplomacy/Ransoms.cs", "new LordJob_Messenger(") })
        {
            string s = R(file);
            C(s.Contains(job) && !s.Contains("InvitedGuests."), $"{job.Substring(4).TrimEnd('(')} stays an errand at the gate with its own ending");
        }

        // ---------- The setting ----------
        string settings = R("CouncilSettings.cs"), catalog = R("UI/SettingsCatalog.cs");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Guests.xml"));
        C(settings.Contains("public bool invitedAsGuests = true;") && settings.Contains("Scribe_Values.Look(ref invitedAsGuests, \"invitedAsGuests\", true);")
          && catalog.Contains("Mod(Toggle(\"invitedAsGuests\", \"CC_Settings_InvitedAsGuests\", \"CC_SetNote_InvitedAsGuests\", \"deepHooks\"), HospitalityId);"),
            "the setting, on, with Hospitality's others, needing the deeper game hooks");
        C(keyed.Contains("<CC_Settings_InvitedAsGuests>") && keyed.Contains("<CC_Settings_InvitedAsGuests_Tip>") && keyed.Contains("<CC_SetNote_InvitedAsGuests>"), "the setting's words");
        return n;
    }
}
