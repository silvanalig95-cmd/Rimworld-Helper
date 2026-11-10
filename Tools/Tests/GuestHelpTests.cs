using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;
using W = ColonyCouncil.GuestHelpMath.Work;

/// <summary>
/// Hospitality's guests helping out (Integrations/GuestHelpMath.cs, GuestHelp.cs): every fit and refusal, each trouble's
/// start and end and who pitches in to it, the verdict on a work giver (narrowing only in everyday times, the trouble's
/// work alone in trouble, a thanks day's work beyond the cap), the mood line opened, the cap and overworking, thanks, the
/// regard both ways, every name against the player's mods, and the seams (the patches by class, the setting, the switch,
/// the think tree's step, the clock's slot, the saved book, the tab, the words).
/// </summary>
public static class GuestHelpTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "guests helping out: " + what);
        }

        var none = new List<string>();

        // ---------------- Work they love ----------------

        C(GuestHelpMath.Fit(W.Cooking, 2, false, null, none) == HelpFit.BurningPassion, "a burning passion for cooking: cooking");
        C(GuestHelpMath.Fit(W.Cooking, 1, false, null, none) == HelpFit.Passion, "a passion for cooking: cooking");
        C(GuestHelpMath.Fit(W.Cooking, 0, false, null, none) == HelpFit.None, "no passion, nothing else: no everyday cooking");
        C(GuestHelpMath.Fit(W.Hauling, 0, true, null, none) == HelpFit.HardWorker && GuestHelpMath.Fit(W.Cleaning, 0, true, null, none) == HelpFit.HardWorker,
            "a hard worker hauls and cleans");
        C(GuestHelpMath.Fit(W.Cooking, 0, true, null, none) == HelpFit.None, "a hard worker doesn't cook for that alone");
        C(GuestHelpMath.Fit(W.Handling, 0, false, PeoplesMath.Herders, none) == HelpFit.Calling, "herders tend animals");
        C(GuestHelpMath.Fit(W.Doctor, 0, false, PeoplesMath.Healers, none) == HelpFit.Calling, "healers tend the sick");
        C(GuestHelpMath.Fit(W.Mining, 0, false, PeoplesMath.Herders, none) == HelpFit.None, "herders don't mine for their calling");
        C(GuestHelpMath.Fit(W.Research, 0, false, null, new List<string> { "Research_Fast" }) == HelpFit.Faith, "a faith that prizes learning studies");
        C(GuestHelpMath.Fit(W.Hauling, 0, false, null, new List<string> { "VME_DumbLabor_Exalted" }) == HelpFit.Faith, "a faith exalting dumb labour hauls");
        C(GuestHelpMath.Fit(W.Cooking, 2, true, PeoplesMath.Pilgrims, new List<string>()) == HelpFit.BurningPassion, "a burning passion is the strongest reason");
        C(GuestHelpMath.Fit(W.Cooking, 1, false, PeoplesMath.Pilgrims, none) == HelpFit.Calling, "their people's calling before a minor passion");
        C(GuestHelpMath.Fit(W.Patient, 2, true, null, none) == HelpFit.None && GuestHelpMath.Fit(W.Warden, 2, true, null, none) == HelpFit.None,
            "own care and work never for a guest are no fit");
        foreach (var calling in PeoplesMath.Callings)
        {
            C(GuestHelpMath.CallingWork.ContainsKey(calling), $"the {calling} have their work (or none, for hire)");
            if (GuestHelpMath.CallingWork.TryGetValue(calling, out var works))
                foreach (var w in works)
                    C(GuestHelpMath.Fit(w, 0, false, calling, none) == HelpFit.Calling && !GuestHelpMath.Never(w), $"the {calling} take up {w}");
        }
        C(GuestHelpMath.CallingWork[PeoplesMath.Mercenaries].Length == 0, "the Banner has no everyday work of its own");
        C(!GuestHelpMath.CallingWork[PeoplesMath.Sextons].Any(w => w == W.Hauling), "the Quiet Bell sweeps, never carries the dead");
        C(!GuestHelpMath.CallingWork[PeoplesMath.Grovekeepers].Contains(W.PlantCutting) && GuestHelpMath.NoTrees(PeoplesMath.Grovekeepers, none),
            "the Mossborn grow and never fell a tree");
        C(GuestHelpMath.NoTrees(null, new List<string> { "TreeCutting_Horrible" }) && !GuestHelpMath.NoTrees(null, none), "a faith against felling: no trees");

        // ---------------- Some guests won't ----------------

        C(GuestHelpMath.Refuse(W.Hauling, true, false, false, null, none, true) == HelpRefusal.Titled, "titled: no hauling");
        C(GuestHelpMath.Refuse(W.Cleaning, false, true, false, null, none, true) == HelpRefusal.Notable, "a town's head or heir: no cleaning");
        C(GuestHelpMath.Refuse(W.PlantCutting, false, false, true, null, none, true) == HelpRefusal.Proud, "proud: no plant cutting");
        C(GuestHelpMath.Refuse("HaulingUrgent", true, false, false, null, none, true) == HelpRefusal.Titled, "titled: no urgent hauling either");
        C(GuestHelpMath.Refuse(W.Cooking, true, true, true, null, none, true) == HelpRefusal.None, "the titled may cook (cooking isn't menial)");
        C(GuestHelpMath.Refuse(W.Mining, false, false, false, null, new List<string> { "Mining_Prohibited" }, true) == HelpRefusal.Faith, "a faith against mining");
        C(GuestHelpMath.Refuse(W.Research, false, false, false, null, new List<string> { "Research_None" }, true) == HelpRefusal.Faith, "a faith against study");
        C(GuestHelpMath.Refuse(W.Hunting, false, false, false, null, new List<string> { "KillingInnocentAnimals_Horrible" }, true) == HelpRefusal.Faith,
            "a faith against killing animals: no hunting");
        C(GuestHelpMath.Refuse(W.Cooking, false, false, false, PeoplesMath.Mercenaries, none, true) == HelpRefusal.Hire, "the Banner: no everyday work");
        C(GuestHelpMath.Refuse(W.Firefighter, false, false, false, PeoplesMath.Mercenaries, none, false) == HelpRefusal.None, "the Banner pitches in in trouble");
        foreach (var w in GuestHelpMath.NeverWork) C(GuestHelpMath.Refuse(w, false, false, false, null, none, true) == HelpRefusal.Never, $"{w}: never for a guest");
        C(GuestHelpMath.RefuseGiver("HaulCorpses", false, none) == HelpRefusal.Never && GuestHelpMath.RefuseGiver("DoBillsCremate", false, none) == HelpRefusal.Never
          && GuestHelpMath.RefuseGiver("Strip", false, none) == HelpRefusal.Never && GuestHelpMath.RefuseGiver("VFE_DigGrave", false, none) == HelpRefusal.Never,
            "the dead are never a guest's: corpses, stripping, cremation, graves");
        C(GuestHelpMath.RefuseGiver("DoBillsMedicalHumanOperation", true, none) == HelpRefusal.Never, "surgery: never");
        C(GuestHelpMath.RefuseGiver("DoctorTendToEntities", false, none) == HelpRefusal.Never, "an entity kept: never");
        C(GuestHelpMath.RefuseGiver("DoBillsButcherFlesh", false, new List<string> { "MeatEating_Abhorrent" }) == HelpRefusal.Faith, "a faith against meat: no butchering");
        C(GuestHelpMath.RefuseGiver("Slaughter", false, new List<string> { "AnimalSlaughter_Prohibited" }) == HelpRefusal.Faith, "a faith against slaughter: none");
        C(GuestHelpMath.RefuseGiver("DoBillsCook", false, new List<string> { "MeatEating_Abhorrent" }) == HelpRefusal.None, "cooking stays theirs");
        C(GuestHelpMath.LifeOnly(true, false, false) && GuestHelpMath.LifeOnly(false, true, false) && GuestHelpMath.LifeOnly(false, false, true)
          && !GuestHelpMath.LifeOnly(false, false, false), "the titled, the high-standing and the proud pitch in only to save a life");
        C(GuestHelpMath.ProudAt == NatureMath.Threshold, "proud from the persona's line, as diplomacy reads it");
        C(GuestHelpMath.PrivateRoom("Bedroom") && GuestHelpMath.PrivateRoom("Barracks") && GuestHelpMath.PrivateRoom("PrisonCell") && GuestHelpMath.PrivateRoom("PrisonBarracks")
          && !GuestHelpMath.PrivateRoom("Kitchen") && !GuestHelpMath.PrivateRoom("GuestRoom") && !GuestHelpMath.PrivateRoom(null),
            "never into a bedroom, barracks or cell; kitchens and guest rooms are fine");

        // ---------------- The colony in trouble: each start and end ----------------

        C(GuestHelpMath.FireIsNear(0f) && GuestHelpMath.FireIsNear(GuestHelpMath.FireNear) && !GuestHelpMath.FireIsNear(GuestHelpMath.FireNear + 0.5f)
          && !GuestHelpMath.FireIsNear(-1f), "a fire within 20 cells of them, none when there's no fire");
        C(GuestHelpMath.RescueNeeded(1) && !GuestHelpMath.RescueNeeded(0), "a rescue while someone is down outside a bed, over once they're all in");
        C(GuestHelpMath.TendNeeded(false, 100, 2), "after a raid, with wounded waiting: tend");
        C(!GuestHelpMath.TendNeeded(true, 100, 2), "not while the enemy is still about");
        C(!GuestHelpMath.TendNeeded(false, GuestHelpMath.TendWindowTicks + 1, 2), "over a day after the raid");
        C(!GuestHelpMath.TendNeeded(false, 100, 0), "over once they're all tended");
        C(!GuestHelpMath.TendNeeded(false, -1, 2), "no raid, no tending crisis");
        C(GuestHelpMath.FrostHarvest(3) && !GuestHelpMath.FrostHarvest(0), "the frost harvest while its crops stand, over once they're in");
        C(GuestHelpMath.FoodShort(2.5f, false, 5) && !GuestHelpMath.FoodShort(3.5f, false, 5), "the food runs out under three days");
        C(GuestHelpMath.FoodShort(3.5f, true, 5) && !GuestHelpMath.FoodShort(4f, true, 5), "and lasts until there are four again");
        C(!GuestHelpMath.FoodShort(0f, false, 0), "no eaters, no hunger");
        var all = HelpCrisis.Fire | HelpCrisis.Rescue | HelpCrisis.Tend | HelpCrisis.Frost | HelpCrisis.Food;
        C(GuestHelpMath.ForGuest(all, true, true, true, true, false, false, false) == all, "a capable guest pitches in to everything");
        C(GuestHelpMath.ForGuest(all, false, true, true, true, false, false, false) == (all & ~HelpCrisis.Fire), "a fire far off isn't theirs");
        C((GuestHelpMath.ForGuest(all, true, false, true, true, false, false, false) & HelpCrisis.Tend) == 0, "the wounded need Medicine enough");
        C((GuestHelpMath.ForGuest(all, true, true, false, true, false, false, false) & HelpCrisis.Food) == 0, "the hungry need Cooking enough");
        C((GuestHelpMath.ForGuest(all, true, true, true, false, false, false, false) & HelpCrisis.Frost) == 0, "the harvest needs someone who can work plants");
        C(GuestHelpMath.ForGuest(all, true, true, true, true, true, false, false) == GuestHelpMath.LifeSaving, "the titled only to save a life");
        C(GuestHelpMath.ForGuest(all, true, true, true, true, false, true, false) == GuestHelpMath.LifeSaving, "the overworked only to save a life");
        C(GuestHelpMath.ForGuest(all, true, true, true, true, false, false, true) == (HelpCrisis.Fire | HelpCrisis.Rescue),
            "Hospitality's helping off: fires and rescues only (documented choice)");
        C(GuestHelpMath.ForGuest(HelpCrisis.None, true, true, true, true, false, false, false) == HelpCrisis.None, "no trouble, nothing");
        C(GuestHelpMath.TendMedicine == 4, "tending from Medicine 4, below Hospitality's own minimum");
        // The player: "i'd rather not have visitor tend the wounds if they're not good doctors. or they should only tend bruises
        // and minor injuries otherwise, not bigger or serious ones".
        C(GuestHelpMath.TendAnyMedicine == 8 && GuestHelpMath.MayTend(8, true) && GuestHelpMath.MayTend(12, true), "a good doctor (Medicine 8) tends anything");
        C(GuestHelpMath.MayTend(4, false) && GuestHelpMath.MayTend(7, false) && !GuestHelpMath.MayTend(7, true) && !GuestHelpMath.MayTend(4, true),
            "Medicine 4 to 7 tends only patients with nothing serious to tend");
        C(!GuestHelpMath.MayTend(3, false) && !GuestHelpMath.MayTend(0, false), "below Medicine 4, no tending at all");
        C(GuestHelpMath.MayTakePatient(5, true, true) && !GuestHelpMath.MayTakePatient(5, false, true) && GuestHelpMath.MayTakePatient(9, false, true)
          && !GuestHelpMath.MayTakePatient(3, true, false),
            "a middling doctor takes up a patient with anything minor, even beside serious wounds; never one with only serious ones");

        C(GuestHelpMath.CrisisOf(W.Firefighter, "FightFires", "WorkGiver_FightFires") == HelpCrisis.Fire, "fighting fires: the fire");
        C(GuestHelpMath.CrisisOf(W.Firefighter, "VEF_FightCustomFire", "X") == HelpCrisis.Fire, "a mod's own fires too");
        C(GuestHelpMath.CrisisOf(W.Doctor, "DoctorRescue", "WorkGiver_RescueDowned") == HelpCrisis.Rescue, "the game's rescue: the rescue");
        C(GuestHelpMath.CrisisOf(W.Doctor, "DoctorTendToHumanlikes", "WorkGiver_TendOther") == HelpCrisis.Tend
          && GuestHelpMath.CrisisOf(W.Doctor, "DoctorTendEmergency", "WorkGiver_TendOtherUrgent") == HelpCrisis.Tend, "tending the colony's people: the wounded");
        C(GuestHelpMath.CrisisOf(W.Doctor, "DoctorTendToAnimals", "WorkGiver_TendOther") == HelpCrisis.None
          && GuestHelpMath.CrisisOf(W.Doctor, "DoctorTendToSelf", "WorkGiver_TendSelf") == HelpCrisis.None, "never an animal's or their own tending");
        C(GuestHelpMath.CrisisOf(W.Doctor, "DoBillsMedicalHumanOperation", "WorkGiver_DoBill") == HelpCrisis.None, "tending only, never surgery");
        C(GuestHelpMath.CrisisOf(W.Growing, "GrowerHarvest", "WorkGiver_GrowerHarvest") == HelpCrisis.Frost
          && GuestHelpMath.CrisisOf(W.PlantCutting, "PlantsCut", "WorkGiver_PlantsCut") == HelpCrisis.Frost, "the field's harvest and the marked crops: the frost");
        C(GuestHelpMath.CrisisOf(W.Growing, "GrowerSow", "WorkGiver_GrowerSow") == HelpCrisis.None, "sowing isn't the frost's");
        C(GuestHelpMath.CrisisOf(W.Cooking, "DoBillsCook", "WorkGiver_DoBill") == HelpCrisis.Food
          && GuestHelpMath.CrisisOf(W.Cooking, "DoBillsButcherFlesh", "WorkGiver_DoBill") == HelpCrisis.Food, "cooking and butchering: the food");
        C(GuestHelpMath.CrisisOf(W.Cooking, "DoBillsBrew", "WorkGiver_DoBill") == HelpCrisis.None && GuestHelpMath.CrisisOf(W.Cooking, "FillFermentingBarrel", "X") == HelpCrisis.None
          && GuestHelpMath.CrisisOf(W.Cooking, "VBE_DoBillsBar", "X") == HelpCrisis.None, "never brewing for the food");
        C(GuestHelpMath.CrisisOf(W.Hauling, "HaulGeneral", "WorkGiver_HaulGeneral") == HelpCrisis.None, "hauling answers no trouble");

        // ---------------- The verdict on each work giver: the seams ----------------

        var everyday = new GuestHelpMath.Ask { everydayOn = true, pitchInOn = true, letHelp = true, fits = true, capOpen = true };
        bool A(GuestHelpMath.Ask a) => GuestHelpMath.Allow(a);
        C(A(Set(everyday, a => a.hospitality = true)), "everyday: Hospitality's yes kept for work they love");
        C(!A(Set(everyday, a => a.hospitality = false)), "everyday: Hospitality's no stays no");
        C(!A(Set(everyday, a => { a.hospitality = true; a.fits = false; })), "no fit, no everyday work, even when Hospitality would allow it");
        C(!A(Set(everyday, a => { a.hospitality = true; a.capOpen = false; })), "past the day's hours: no");
        C(!A(Set(everyday, a => { a.hospitality = true; a.overworked = true; })), "overworked: no");
        C(!A(Set(everyday, a => { a.hospitality = true; a.refused = HelpRefusal.Titled; })), "refused: no");
        C(!A(Set(everyday, a => { a.hospitality = true; a.letHelp = false; })), "\"Let them help\" off: no");
        C(A(Set(everyday, a => { a.hospitality = true; a.ownCare = true; a.letHelp = false; a.fits = false; })) && !A(Set(everyday, a => { a.hospitality = false; a.ownCare = true; })),
            "their own care stays Hospitality's, switch or not");
        C(A(Set(everyday, a => { a.hospitality = true; a.everydayOn = false; a.pitchInOn = false; a.fits = false; a.letHelp = false; })),
            "both settings off: Hospitality's own answer, untouched");
        C(A(Set(everyday, a => { a.hospitality = true; a.everydayOn = false; a.fits = false; })), "pitching in alone on: everyday stays Hospitality's");
        // Exhaustive: outside trouble and a thanks day, it only narrows Hospitality's answer.
        int combos = 0, widened = 0;
        foreach (bool hosp in new[] { false, true })
        foreach (bool ev in new[] { false, true })
        foreach (bool pi in new[] { false, true })
        foreach (bool let in new[] { false, true })
        foreach (bool own in new[] { false, true })
        foreach (bool fits in new[] { false, true })
        foreach (bool cap in new[] { false, true })
        foreach (bool over in new[] { false, true })
        foreach (bool can in new[] { false, true })
        foreach (var refused in Enum.GetValues(typeof(HelpRefusal)).Cast<HelpRefusal>())
        foreach (var giverCrisis in new[] { HelpCrisis.None, HelpCrisis.Fire, HelpCrisis.Food })
        {
            combos++;
            var a = new GuestHelpMath.Ask
            {
                hospitality = hosp, everydayOn = ev, pitchInOn = pi, letHelp = let, ownCare = own, fits = fits, capOpen = cap, overworked = over, refused = refused,
                giverCrisis = giverCrisis, crisisCan = can, inCrisis = HelpCrisis.None, thanksWork = false, thanksCan = can
            };
            if (A(a) && !hosp) widened++;
        }
        C(combos > 1000 && widened == 0, $"everyday: never more than Hospitality allows ({combos} cases, {widened} widened)");
        // In trouble: only that trouble's work, whatever Hospitality's roll said.
        var trouble = Set(everyday, a => { a.inCrisis = HelpCrisis.Fire; a.giverCrisis = HelpCrisis.Fire; a.crisisCan = true; a.hospitality = false; a.fits = false; });
        C(A(trouble), "a fire near them: firefighting, whatever Hospitality's roll and mood");
        C(!A(Set(trouble, a => a.crisisCan = false)), "only what they can do (the game's own checks)");
        C(!A(Set(trouble, a => { a.giverCrisis = HelpCrisis.None; a.hospitality = true; a.fits = true; })), "in trouble, only the trouble's work: no everyday fit then");
        C(!A(Set(trouble, a => a.giverCrisis = HelpCrisis.Food)), "another trouble's work isn't theirs");
        C(!A(Set(trouble, a => a.pitchInOn = false)), "with pitching in off, Hospitality's own answer");
        C(!A(Set(trouble, a => a.letHelp = false)), "\"Let them help\" off: not even in trouble");
        C(!A(Set(trouble, a => a.refused = HelpRefusal.Never)), "never work stays never, trouble or not");
        C(A(Set(trouble, a => a.refused = HelpRefusal.Titled)), "the titled fight a fire (a life at stake)");
        C(A(Set(trouble, a => a.refused = HelpRefusal.Hire)), "the Banner pitches in");
        C(!A(Set(trouble, a => { a.inCrisis = HelpCrisis.Food; a.giverCrisis = HelpCrisis.Food; a.refused = HelpRefusal.Faith; })),
            "a faith's custom holds in a trouble where no life is at stake (no butchering for the food)");
        C(A(Set(trouble, a => a.capOpen = false)), "trouble's work is beyond the day's cap");
        // A thanks day.
        var thanks = Set(everyday, a => { a.thanksWork = true; a.thanksCan = true; a.hospitality = false; a.capOpen = false; a.fits = true; });
        C(A(thanks), "their thanks day: its work beyond the cap and Hospitality's roll");
        C(!A(Set(thanks, a => a.thanksCan = false)), "Hospitality's own minimum and exclusions kept on the thanks day");
        C(!A(Set(thanks, a => a.everydayOn = false)), "the thanks day is the everyday setting's");
        C(!A(Set(thanks, a => a.refused = HelpRefusal.Faith)), "nothing refused on the thanks day either");

        // Hospitality's mood line, and Rimfolk's own step.
        C(GuestHelpMath.Gate(false, true, true, true, HelpCrisis.Fire, false), "in trouble the mood line opens");
        C(GuestHelpMath.Gate(false, true, true, true, HelpCrisis.None, true), "on the thanks day it opens");
        C(!GuestHelpMath.Gate(false, true, false, true, HelpCrisis.Fire, false), "with pitching in off, it doesn't for trouble");
        C(!GuestHelpMath.Gate(false, false, true, true, HelpCrisis.None, true), "with the everyday setting off, not for thanks");
        C(!GuestHelpMath.Gate(false, true, true, false, HelpCrisis.Fire, true) && GuestHelpMath.Gate(true, true, true, false, HelpCrisis.None, false),
            "switched off: Hospitality's own line");
        C(GuestHelpMath.Gate(true, true, true, true, HelpCrisis.None, false) && !GuestHelpMath.Gate(false, true, true, true, HelpCrisis.None, false),
            "otherwise Hospitality's own line");
        C(GuestHelpMath.PitchingIn(true, true, HelpCrisis.Rescue, false, false), "Rimfolk's step: in trouble, before their needs");
        C(!GuestHelpMath.PitchingIn(true, true, HelpCrisis.Rescue, true, false) && !GuestHelpMath.PitchingIn(true, true, HelpCrisis.Rescue, false, true),
            "not when starving or dropping");
        C(!GuestHelpMath.PitchingIn(true, true, HelpCrisis.None, false, false) && !GuestHelpMath.PitchingIn(false, true, HelpCrisis.Fire, false, false)
          && !GuestHelpMath.PitchingIn(true, false, HelpCrisis.Fire, false, false), "no trouble, the setting off or the switch off: no step");

        // ---------------- Limits ----------------

        C(GuestHelpMath.CapTicks == 3 * 2500 && GuestHelpMath.CapOpen(7499) && !GuestHelpMath.CapOpen(7500), "about three hours of everyday work a day");
        C(!GuestHelpMath.Overworked(7500, 0) && GuestHelpMath.Overworked(7500, 7500) && GuestHelpMath.Overworked(2500, 12500),
            "overworked past six hours of work in all (the cap and a trouble on top)");
        C(GuestHelpMath.Hours(0) == 0f && GuestHelpMath.Hours(2500) == 1f && GuestHelpMath.Hours(3749) == 1f && GuestHelpMath.Hours(3750) == 1.5f, "hours shown to the half hour");
        C(GuestHelpMath.TooUnhappy(0.7f) && !GuestHelpMath.TooUnhappy(0.8f) && GuestHelpMath.HappyEnough == 0.75f, "unhappy under Hospitality's own line, 75%");

        // ---------------- Thanks paid in work ----------------

        C(GuestHelpMath.Thanks(true, true, true, true, 1f) == HelpThanks.Rescued, "carried in comes first");
        C(GuestHelpMath.Thanks(false, true, true, false, 0f) == HelpThanks.Freed, "then set free once");
        C(GuestHelpMath.Thanks(false, false, true, false, 0f) == HelpThanks.Tended, "then tended");
        C(GuestHelpMath.Thanks(false, false, false, true, 0f) == HelpThanks.Feast, "then a feast");
        C(GuestHelpMath.Thanks(false, false, false, false, 0.95f) == HelpThanks.Stay && GuestHelpMath.Thanks(false, false, false, false, 0.85f) == HelpThanks.None,
            "then a stay going very well (Hospitality's score 0.9), else nothing");
        C(GuestHelpMath.Thanks(false, false, false, false, -1f) == HelpThanks.None, "no score read: no thanks for the stay");
        C(GuestHelpMath.ThanksHour(6) && GuestHelpMath.ThanksHour(10) && !GuestHelpMath.ThanksHour(11) && !GuestHelpMath.ThanksHour(3), "offered in the morning");
        C(GuestHelpMath.ThanksTicks == 60000, "a day's work");
        C(GuestHelpMath.BestLoved(new[] { ("Hauling", 0, 0), ("Cooking", 2, 6), ("Mining", 1, 12) }) == "Cooking", "the best-loved: the highest passion");
        C(GuestHelpMath.BestLoved(new[] { ("Mining", 1, 9), ("Cooking", 1, 12) }) == "Cooking", "then the best skill");
        C(GuestHelpMath.BestLoved(new[] { ("Mining", 1, 9), ("Art", 1, 9) }) == "Art", "then by name");
        C(GuestHelpMath.BestLoved(new (string, int, int)[0]) == null && GuestHelpMath.BestLoved(null) == null, "nothing to offer");

        // ---------------- It counts both ways ----------------

        C(GuestHelpMath.SideBySide(3f, W.Cooking, W.Cooking) && !GuestHelpMath.SideBySide(3f, W.Cooking, W.Hauling) && !GuestHelpMath.SideBySide(9f, W.Cooking, W.Cooking)
          && !GuestHelpMath.SideBySide(3f, W.Cooking, null), "side by side: close, at the same work");
        C(GuestHelpMath.SpeakWell(0, 0, false) == 0f, "no help, no good word");
        C(GuestHelpMath.SpeakWell(2500, 0, false) == 1f && GuestHelpMath.SpeakWell(10000, 0, false) == 2f, "a little for a few hours, more for long help");
        C(GuestHelpMath.SpeakWell(0, 1, false) == 2f && GuestHelpMath.SpeakWell(0, 0, true) == 1f, "a trouble and a thanks day count");
        C(GuestHelpMath.SpeakWell(100000, 5, true) == GuestHelpMath.MaxSpeakWell, "capped");
        C(GuestHelpMath.HurtRegard > 0f && GuestHelpMath.HurtRegard <= 5f && GuestHelpMath.OverworkRegard > 0f && GuestHelpMath.MaxNotablesTold > 0, "small regard either way");
        C(GuestHelpMath.MaxGuests >= 10 && GuestHelpMath.MaxTold >= 8 && GuestHelpMath.MaxEnabled >= 5, "the saved bounds");

        // ---------------- Every name against the player's mods ----------------

        var active = new Dictionary<string, HashSet<string>>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var rd = new StreamReader(gz))
        {
            string line;
            while ((line = rd.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length < 4 || cols[3] != "yes") continue;
                if (!active.TryGetValue(cols[0], out var set)) active[cols[0]] = set = new HashSet<string>();
                set.Add(cols[1]);
            }
        }
        HashSet<string> Of(string type) => active.TryGetValue(type, out var s) ? s : new HashSet<string>();
        foreach (var p in GuestHelpMath.PreceptsNamed) C(Of("PreceptDef").Contains(p), $"precept {p} is one of the player's active defs");
        var workTypes = GuestHelpMath.CallingWork.Values.SelectMany(x => x).Concat(GuestHelpMath.FaithWork.Values.SelectMany(x => x))
            .Concat(GuestHelpMath.FaithForbids.Values.SelectMany(x => x)).Concat(GuestHelpMath.NeverWork).Concat(GuestHelpMath.Menial).Distinct();
        foreach (var w in workTypes) C(Of("WorkTypeDef").Contains(w), $"work type {w} is one of the player's active defs");
        var givers = GuestHelpMath.NeverGivers.Concat(GuestHelpMath.TendGivers).Concat(GuestHelpMath.FrostGivers).Concat(GuestHelpMath.FaithForbidsGiver.Values.SelectMany(x => x))
            .Concat(new[] { "DoctorRescue", "FightFires", "DoBillsCook", "DoBillsButcherFlesh" }).Distinct();
        foreach (var g in givers) C(Of("WorkGiverDef").Contains(g), $"work giver {g} is one of the player's active defs");

        // ---------------- The seams ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string help = Code("Integrations/GuestHelp.cs"), tab = Code("Integrations/GuestHelpTab.cs"), checkCode = Code("Integrations/GuestCheck.cs");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        foreach (var cls in new[] { "Patch_GuestHelpWorkGiver", "Patch_GuestHelpJob", "Patch_GuestHelpWantsToHelp", "Patch_GuestHelpTendFit" })
        {
            var m = Regex.Match(help, @"\[HarmonyPatch[^\]]*\]\s*public static class " + cls + @"\b(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{cls} is a patch class of its own, applied by RimfolkPatches.ApplyAll");
            C(m.Success && m.Groups[1].Value.Contains("public static bool Prepare()") && m.Groups[1].Value.Contains("StandsDownWhy") && m.Groups[1].Value.Contains("Hospitality isn't loaded"),
                $"{cls} stands down without Hospitality, saying so");
            C(m.Success && m.Groups[1].Value.Contains("Postfix") && !m.Groups[1].Value.Contains("Prefix") && m.Groups[1].Value.Contains("catch (Exception e)"),
                $"{cls} is a postfix, its errors caught");
        }
        C(help.Contains("[HarmonyPatch(typeof(JobGiver_Work), \"PawnCanUseWorkGiver\")]") && help.Contains("__result = GuestHelp.CanUse(pawn, giver, __result);"),
            "the work givers' verdict is the rule's, after Hospitality's own prefix");
        C(help.Contains("[HarmonyPatch(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.HasJobOnThing))]") && help.Contains("if (!GuestHelpMath.MayTakePatient(GuestHelp.MedicineOf(pawn), minor, serious)) __result = false;"),
            "a guest passes over a patient with nothing their skill is fit for, on the tending givers' base check");
        C(help.Contains("[HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]") && help.Contains("[HarmonyPatch(typeof(TendUtility), nameof(TendUtility.GetOptimalHediffsToTendWithSingleTreatment))]")
          && help.Contains("MedicineChoice.Judge(h, out _) != MedicineChoiceMath.Threat.Serious") && help.Contains("tendableHediffsInTendPriorityOrder = minor;")
          && help.Contains("if (!minor) return false;") && help.Contains("public static void Postfix() => GuestHelp.minorOnly = null;"),
            "a middling guest's treatment covers the minor conditions only, the serious left for a better doctor, nothing used when nothing minor is left");
        C(help.Contains("\"CC_GuestHelp_Tab_MinorOnly\""), "the guest tab says a middling doctor tends only minor ailments");
        C(help.Contains("if (f == null || f.IsPlayer || !GuestHelp.Any) return;"), "the colony's own pawns never pass through it");
        C(help.Contains("GenTypes.GetTypeInAnyAssembly(\"Hospitality.ThinkNode_ConditionalWantsToHelp\")") && help.Contains("AccessTools.Method(t, \"Satisfied\""),
            "Hospitality's mood line patched by name, softly");
        C(help.Contains("\"Hospitality.ModSettings_Hospitality\"") && help.Contains("\"disableWork\"") && help.Contains("\"minGuestWorkSkill\""),
            "Hospitality's own switch and minimum skill read by name and kept");
        C(help.Contains("public static bool Everyday => RimfolkPatches.On && (S?.guestHelp ?? true)") && help.Contains("public static bool PitchIn => RimfolkPatches.On && (S?.guestsPitchIn ?? true)"),
            "each part behind its setting and the deeper game hooks");
        C(settings.Contains("public bool guestHelp = true;") && settings.Contains("Scribe_Values.Look(ref guestHelp, \"guestHelp\", true);")
          && settings.Contains("public bool guestsPitchIn = true;") && settings.Contains("Scribe_Values.Look(ref guestsPitchIn, \"guestsPitchIn\", true);"),
            "the two settings, on by default, saved");
        C(catalog.Contains("Mod(Toggle(\"guestHelp\", \"CC_Settings_GuestHelp\", \"CC_SetNote_GuestHelp\", \"deepHooks\"), HospitalityId);")
          && catalog.Contains("Mod(Toggle(\"guestsPitchIn\", \"CC_Settings_GuestsPitchIn\", \"CC_SetNote_GuestsPitchIn\", \"deepHooks\"), HospitalityId);"),
            "on Your other mods, under Hospitality, behind the deeper hooks");
        C(help.Contains("letHelp = !r.off") && help.Contains("public static void SetLetHelp(Pawn g, bool on)") && tab.Contains("Widgets.CheckboxLabeled(box, \"CC_GuestHelp_Let\".Translate(), ref let);"),
            "the guest tab's \"Let them help\", on by default (off is saved)");
        C(checkCode.Contains("if (help) GuestHelpTab.Draw(listing, pawn);") && checkCode.Contains("GuestCheck.Grown(listing, pawn);") && checkCode.Contains("(On || GuestHelpTab.On)"),
            "the tab's section drawn with the recruiting checklist, the tab grown for either");
        // The think tree's step, on the game's hook Hospitality's guest duty offers.
        var tree = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_GuestHelp.xml")).Root!.Element("ThinkTreeDef");
        C(tree?.Element("insertTag")?.Value == "Humanlike_PreMain" && (string)tree.Element("thinkRoot")?.Attribute("Class") == "ColonyCouncil.ThinkNode_GuestPitchIn"
          && tree.Element("thinkRoot")!.Element("subNodes")!.Elements("li").Select(e => (string)e.Attribute("Class")).SequenceEqual(new[] { "ColonyCouncil.JobGiver_GuestRescue", "JobGiver_Work" }),
            "Rimfolk's step on Humanlike_PreMain: the rescue, then the trouble's work");
        C(help.Contains("if (g?.Faction == null || g.Faction.IsPlayer || !PitchIn"), "the step says no at once for the colony's own");
        // The clock.
        C(PassTable.All.Any(p => p.pass == Pass.RareGuestHelp && p.offset == Sched.RareGuestHelp && p.period == Sched.RareTicks && p.waiting), "its pass is timed, only while it waits");
        C(help.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareGuestHelp)") && help.Contains("PassClock.Time(Pass.RareGuestHelp)"), "its pass runs on its own slot");
        var taken = Sched.RareOffsets.Concat(new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers, Sched.RareVisitors, Sched.RareTimed,
            Sched.RareRearrange, Sched.RareNeighbours, Sched.RareOutsiders, Sched.RareJourneys, Sched.RareOutsiderTalk, Sched.RareTriage, Sched.RareHauling }).Concat(Sched.HourlyOnCycle);
        C(!taken.Contains(Sched.RareGuestHelp) && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareGuestHelp), "a tick of the cycle of its own");
        // What's saved: bounded, let go when the guest leaves, modelled.
        C(help.Contains("while (book.guests.Count > GuestHelpMath.MaxGuests)") && help.Contains("while (r.told.Count > GuestHelpMath.MaxTold)")
          && help.Contains("r.enabled.Count >= GuestHelpMath.MaxEnabled"), "each saved list capped");
        C(help.Contains("guests.RemoveAll(r => r?.pawn == null || r.pawn.Destroyed || r.pawn.Dead || !r.pawn.SpawnedOrAnyParentSpawned)") && help.Contains("Farewell(r);"),
            "a guest gone is let go (their switch, hours and thanks with them)");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var k in new[] { "Integrations/GuestHelp.cs:guests", "Integrations/GuestHelp.cs:told", "Integrations/GuestHelp.cs:enabled" })
            C(model.Contains("\"" + k + "\""), $"{k} is modelled in the save estimate");
        // Words: every key, and the pools said over a head.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GuestHelp.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        foreach (Match m in Regex.Matches(help + tab, "\"(CC_GuestHelp_[A-Za-z_]+)\""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_")) continue;
            C(keyed.Contains(key), $"{key} exists");
        }
        foreach (var c in GuestHelpMath.Crises) C(keyed.Contains("CC_GuestHelp_Crisis_" + c), $"the trouble {c} in words");
        foreach (var t in Enum.GetValues(typeof(HelpThanks)).Cast<HelpThanks>().Where(t => t != HelpThanks.None))
            C(keyed.Contains("CC_GuestHelp_Msg_Thanks_" + t) && keyed.Contains("CC_GuestHelp_Why_" + t), $"thanks for {t} in words");
        foreach (var r in new[] { HelpRefusal.Notable, HelpRefusal.Proud }) C(keyed.Contains("CC_GuestHelp_No_" + r), $"refusal {r} in words");
        foreach (var f in new[] { HelpFit.Passion, HelpFit.BurningPassion, HelpFit.HardWorker, HelpFit.Calling, HelpFit.Faith }) C(keyed.Contains("CC_GuestHelp_Fit_" + f), $"fit {f} in words");
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_GuestHelp_Lines.xml")).Root!.Elements()
            .ToDictionary(e => e.Element("defName")!.Value, e => e.Element("lines")!.Descendants("li").Select(li => li.Value).ToList());
        List<string> Tagged(string pool, string tag) => pools[pool].Where(l => Tags(l).Any(t => t.Split('|').Contains(tag))).ToList();
        foreach (var f in new[] { "burning", "passion", "calling", "faith", "hard" })
            C(Tagged("Rimfolk_Pool_GuestHelpFit", "why:" + f).Count >= 3, $"why they take up work: {f} has lines");
        foreach (var calling in PeoplesMath.Callings.Where(c => GuestHelpMath.CallingWork[c].Length > 0))
            C(Tagged("Rimfolk_Pool_GuestHelpFit", "their:" + calling).Count >= 1, $"the {calling} say it their own way");
        foreach (var c in GuestHelpMath.Crises) C(Tagged("Rimfolk_Pool_GuestHelpCrisis", "c:" + c.ToString().ToLowerInvariant()).Count >= 3, $"pitching in to the {c}: lines");
        foreach (var t in Enum.GetValues(typeof(HelpThanks)).Cast<HelpThanks>().Where(t => t != HelpThanks.None))
            C(Tagged("Rimfolk_Pool_GuestHelpThanks", "t:" + t.ToString().ToLowerInvariant()).Count >= 2, $"thanks for {t}: lines");
        foreach (var s in new[] { "enough", "unhappy", "overworked" }) C(Tagged("Rimfolk_Pool_GuestHelpStop", "s:" + s).Count >= 3, $"putting work down, {s}: lines");
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
            C(pools.Values.SelectMany(x => x).Any(l => Tags(l).Any(t => t.Split('|').Contains("their:" + age))), $"lines in the {age} people's ways (every age)");
        C(pools.Values.SelectMany(x => x).Any(l => Tags(l).Any(t => t.Split('|').Contains("their:empire"))), "the Empire's own");
        C(pools.Values.SelectMany(x => x).All(l => Tags(l).Length > 0), "every line carries its moment's tag (only that moment's lines are drawn)");
        C(!pools.Values.SelectMany(x => x).Any(l => Tags(l).Any(t => t is "tribal" or "medieval" or "industrial" or "spacer" or "lowtech")),
            "never the colony's age: the speaker's own people's (their:...)");
        string immersion = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "ImmersionTests.cs"));
        foreach (var pool in pools.Keys) C(immersion.Contains("\"" + pool + "\""), $"{pool} is checked as said over a head (one sentence)");
        C(help.Contains("Speech.Say(g, text);") && !help.Contains("Messages.Message("), "speech through Speech.Say, messages through the teller");
        return n;
    }

    private static GuestHelpMath.Ask Set(GuestHelpMath.Ask a, Action<AskBox> change)
    {
        var box = new AskBox { a = a };
        change(box);
        return box.a;
    }

    /// <summary>An Ask to change in a lambda (it's a struct), field by field.</summary>
    private sealed class AskBox
    {
        public GuestHelpMath.Ask a;
        public bool hospitality { get => a.hospitality; set => a.hospitality = value; }
        public bool everydayOn { get => a.everydayOn; set => a.everydayOn = value; }
        public bool pitchInOn { get => a.pitchInOn; set => a.pitchInOn = value; }
        public bool letHelp { get => a.letHelp; set => a.letHelp = value; }
        public bool ownCare { get => a.ownCare; set => a.ownCare = value; }
        public HelpRefusal refused { get => a.refused; set => a.refused = value; }
        public bool fits { get => a.fits; set => a.fits = value; }
        public bool capOpen { get => a.capOpen; set => a.capOpen = value; }
        public bool overworked { get => a.overworked; set => a.overworked = value; }
        public HelpCrisis inCrisis { get => a.inCrisis; set => a.inCrisis = value; }
        public HelpCrisis giverCrisis { get => a.giverCrisis; set => a.giverCrisis = value; }
        public bool crisisCan { get => a.crisisCan; set => a.crisisCan = value; }
        public bool thanksWork { get => a.thanksWork; set => a.thanksWork = value; }
        public bool thanksCan { get => a.thanksCan; set => a.thanksCan = value; }
    }

    private static string[] Tags(string line)
    {
        var m = Regex.Match(line, @"^\s*\[([^\]]*)\]");
        return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray() : new string[0];
    }
}
