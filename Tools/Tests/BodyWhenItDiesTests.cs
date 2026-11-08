using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Take a bleeding animal's body when it dies (the player's request, 8 November 2026): when the button shows, when the order
/// is dropped and what the player is told, the blood-loss outlook by the game's rules, the haul order at the death, and the
/// seams (the kill's prefix and postfix carrying the mark, the designation, the setting, the short pass).
/// </summary>
public static class BodyWhenItDiesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the body when it dies: " + what);
        }

        // ---------- The button ----------
        bool Shows(bool on = true, bool animal = true, bool ours = false, bool dead = false, bool spawned = true, float bleed = 0.4f, bool marked = false) =>
            BodyWhenItDiesMath.ButtonShows(on, animal, ours, dead, spawned, bleed, marked);
        C(Shows(), "a wild animal bleeding on the map gets the button");
        C(!Shows(ours: true), "never one of the colony's own");
        C(!Shows(dead: true) && !Shows(spawned: false), "never a dead one, or one not on the map");
        C(!Shows(bleed: 0f) && !Shows(bleed: 0.00005f) && Shows(bleed: 0.0001f), "only while it bleeds, at the game's own floor (0.0001)");
        C(!Shows(marked: true), "not again once marked (the game's Cancel button shows instead)");
        C(!Shows(on: false) && !Shows(animal: false), "not with the setting or the hooks off, and only on animals");

        // ---------- When the order is dropped ----------
        BodyMarkEnd J(bool on = true, bool marked = true, bool dead = false, bool onMap = true, bool ours = false, bool wild = true, float bleed = 0.4f) =>
            BodyWhenItDiesMath.Judge(on, marked, dead, onMap, ours, wild, bleed);
        C(J() == BodyMarkEnd.None && !BodyWhenItDiesMath.Drop(J()), "alive, bleeding, still marked, on its map: it stands, and nothing is done");
        C(J(bleed: 0f) == BodyMarkEnd.StoppedBleeding && J(bleed: 0.00009f) == BodyMarkEnd.StoppedBleeding, "it stopped bleeding (aged out, tended, healed): dropped");
        C(J(ours: true, marked: false) == BodyMarkEnd.Tamed && J(ours: true, wild: false, marked: false) == BodyMarkEnd.Joined,
            "tamed, or another people's animal come over: dropped, though the game took every order off it as it changed hands");
        C(J(onMap: false) == BodyMarkEnd.LeftMap && J(onMap: false, marked: false) == BodyMarkEnd.LeftMap, "it left the map, with its order on it or not: dropped");
        C(J(marked: false) == BodyMarkEnd.Cancelled, "the order gone while it is still here: someone cancelled or replaced it");
        C(J(dead: true) == BodyMarkEnd.Died && J(dead: true, marked: false, onMap: false) == BodyMarkEnd.Died, "dead: the kill carried it over");
        C(J(on: false) == BodyMarkEnd.Off && J(on: false, ours: true) == BodyMarkEnd.Off && J(on: false, bleed: 0f) == BodyMarkEnd.Off,
            "the setting or the hooks off: every order dropped without a word");
        C(J(bleed: 0f, onMap: false) == BodyMarkEnd.LeftMap && J(bleed: 0f, ours: true) == BodyMarkEnd.Tamed, "one reason told: leaving or taming before the bleeding");
        foreach (BodyMarkEnd e in Enum.GetValues(typeof(BodyMarkEnd)))
            C(BodyWhenItDiesMath.Drop(e) == (e != BodyMarkEnd.None), $"{e}: {(e == BodyMarkEnd.None ? "kept" : "dropped")}");
        var told = Enum.GetValues(typeof(BodyMarkEnd)).Cast<BodyMarkEnd>().Where(BodyWhenItDiesMath.Told).ToList();
        C(told.SequenceEqual(new[] { BodyMarkEnd.StoppedBleeding, BodyMarkEnd.Tamed, BodyMarkEnd.Joined, BodyMarkEnd.LeftMap }),
            "told for stopped bleeding, tamed, joined and left the map only (never the player's own cancel, a death or the setting)");
        C(BodyWhenItDiesMath.MessageKey(BodyMarkEnd.Cancelled) == null && BodyWhenItDiesMath.MessageKey(BodyMarkEnd.Died) == null
          && BodyWhenItDiesMath.MessageKey(BodyMarkEnd.StoppedBleeding) == "CC_BodyMark_Off_StoppedBleeding", "the message's key by the reason");

        // ---------- The words ----------
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_BodyWhenItDies.xml"));
        string Text(string key)
        {
            var m = Regex.Match(keyed, "<" + key + ">(.*?)</" + key + ">", RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : null;
        }
        foreach (var e in told)
        {
            string t = Text(BodyWhenItDiesMath.MessageKey(e));
            C(t != null && t.StartsWith("{ANIMAL_nameDef}") && t.Contains("cancelled") && t.Count(ch => ch == '.') == 1,
                $"{e}: one short sentence naming the animal and why ({t})");
        }
        C(Text("CC_BodyMark_Off_StoppedBleeding").Contains("stopped bleeding"), "the bleeding one says \"stopped bleeding\"");
        foreach (var k in new[] { "CC_BodyMark_Gizmo", "CC_BodyMark_GizmoDesc", "CC_BodyMark_Bleeding", "CC_BodyMark_Outlook_Dies", "CC_BodyMark_Outlook_Lives",
                     "CC_BodyMark_Outlook_LivesSlow", "CC_BodyMark_NoStorage", "CC_Settings_BodyWhenItDies", "CC_Settings_BodyWhenItDies_Tip", "CC_SetNote_BodyWhenItDies" })
        {
            string t = Text(k);
            C(t != null && !t.Contains("—") && !t.Contains("!"), $"{k} is there, without dashes or exclamations");
        }
        C(Text("CC_BodyMark_Gizmo") == "Take the body when it dies", "the button says what it does");
        C(Text("CC_BodyMark_GizmoDesc").Contains("nobody goes near it while it lives") && Text("CC_BodyMark_GizmoDesc").Contains("unforbidden and marked for hauling"),
            "its tooltip says what it does");
        C(Text("CC_BodyMark_Outlook_Dies").Contains("{0}") && Text("CC_BodyMark_Outlook_Lives").Contains("{0}") && !Text("CC_BodyMark_Outlook_LivesSlow").Contains("{0}"),
            "and whether it bleeds to death and about when");

        // ---------- The outlook, by the game's rules ----------
        C(BodyWhenItDiesMath.InjuryStopsAt(1f) == 90000 && BodyWhenItDiesMath.InjuryStopsAt(0.5f) == 90000 && BodyWhenItDiesMath.InjuryStopsAt(30f) == 180000
          && BodyWhenItDiesMath.InjuryStopsAt(100f) == 180000 && BodyWhenItDiesMath.InjuryStopsAt(15.5f) == 135000,
            "a wound stops bleeding by its age: 1.5 days for a scratch, up to 3 days for a wound of 30 (Hediff_Injury)");
        BleedOutlook O(float loss, float total, float[] rates, int[] left) => BodyWhenItDiesMath.Outlook(loss, total, rates, left);
        var none = O(0f, 0f, new float[0], new int[0]);
        C(none.gameTicks == int.MaxValue && !none.Dies, "not bleeding: nothing to bleed out");
        var deep = O(0f, 0.5f, new[] { 0.5f }, new[] { 150000 });
        C(deep.gameTicks == 120000 && deep.Dies && deep.diesIn == 120000 && deep.closesIn == 150000, "50% a day, the wound open 2.5 days: dead in two days, the health tab's figure");
        var closes = O(0f, 0.5f, new[] { 0.5f }, new[] { 60000 });
        C(!closes.Dies && closes.closesIn == 60000 && closes.gameTicks == 120000, "the same wound closing in a day: half its blood gone, then it lives");
        var slow = O(0.3f, 0.05f, new[] { 0.05f }, new[] { 40000 });
        C(!slow.Dies && slow.closesIn == 40000, "under 10% a day the blood comes back: it lives (HediffGiver_Bleeding)");
        bool Near(int a, int b) => Math.Abs(a - b) <= 2;
        var two = O(0f, 0.6f, new[] { 0.3f, 0.3f }, new[] { 30000, 200000 });
        C(Near(two.gameTicks, 100000) && two.Dies && Near(two.diesIn, 170000), $"two wounds, one closing at half a day: dead later than the tab says ({two.gameTicks}, {two.diesIn})");
        var scaled = O(0f, 1.2f, new[] { 0.3f, 0.3f }, new[] { 30000, 200000 });
        C(Near(scaled.gameTicks, 50000) && scaled.Dies && Near(scaled.diesIn, 70000),
            $"the game's factors scale every wound alike (a total of twice the wounds' sum: {scaled.gameTicks}, {scaled.diesIn})");
        var lost = O(0.8f, 0.4f, new[] { 0.4f }, new[] { 100000 });
        C(Near(lost.gameTicks, 30000) && Near(lost.diesIn, 30000), "blood already lost counts: 20% left at 40% a day is half a day");
        var never = O(0f, 0.3f, new[] { 0.3f }, new[] { int.MaxValue });
        C(never.Dies && Near(never.diesIn, never.gameTicks) && never.closesIn == int.MaxValue, "a wound that never stops by age: dead at the health tab's figure");
        var unknown = O(0f, 0.3f, new float[0], new int[0]);
        C(unknown.Dies && unknown.diesIn == unknown.gameTicks && !O(0f, 0.05f, new float[0], new int[0]).Dies, "nothing known of the wounds: by the rate now");
        var mixed = O(0f, 0.35f, new[] { 0.3f, 0.05f }, new[] { 20000, int.MaxValue });
        C(!mixed.Dies && mixed.closesIn == int.MaxValue, "the big wound closes, the trickle left comes back faster than it runs: it lives");
        C(BodyWhenItDiesMath.OutlookKey(deep) == "CC_BodyMark_Outlook_Dies" && BodyWhenItDiesMath.OutlookKey(closes) == "CC_BodyMark_Outlook_Lives"
          && BodyWhenItDiesMath.OutlookKey(slow) == "CC_BodyMark_Outlook_Lives" && BodyWhenItDiesMath.OutlookKey(mixed) == "CC_BodyMark_Outlook_LivesSlow",
            "the tooltip's line: it dies at the tab's figure, its wounds close first, or it makes its blood up");
        C(BodyWhenItDiesMath.SoonTicks == 60000 && BodyWhenItDiesMath.BloodLossGrows == 0.1f && BodyWhenItDiesMath.MissingPartFreshTicks == 90000,
            "the game's numbers: a day for \"soon\", 10% a day for blood loss to grow, 1.5 days for a fresh missing part");

        // ---------- At the death ----------
        C(BodyWhenItDiesMath.HaulOrder(false, false) && !BodyWhenItDiesMath.HaulOrder(true, false) && !BodyWhenItDiesMath.HaulOrder(false, true),
            "the body gets the game's haul order, unless it already lies in storage or has one");
        C(BodyWhenItDiesMath.TellNoStorage(false, false) && !BodyWhenItDiesMath.TellNoStorage(false, true) && !BodyWhenItDiesMath.TellNoStorage(true, false),
            "told once when no storage takes it, never when it does or it is stored already");

        // ---------- The seams ----------
        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string code = src("Animals/BodyWhenItDies.cs"), patches = src("Animals/BodyWhenItDiesPatches.cs");
        string settings = src("CouncilSettings.cs"), catalog = src("UI/SettingsCatalog.cs");
        C(patches.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]") && patches.Contains("public static void Prefix(Pawn __instance, out Map __state)")
          && patches.Contains("__state = BodyWhenItDies.MarkedMap(__instance);") && patches.Contains("public static void Postfix(Pawn __instance, Map __state)")
          && patches.Contains("!__instance.Dead) return;") && patches.Contains("BodyWhenItDies.Died(__instance, __state);"),
            "the kill carries the mark: read in the prefix before the game removes the orders, used in the postfix once it is really dead");
        C(code.Contains("map.designationManager.DesignationOn(a, def) != null ? map : null"), "the prefix reads the mark on the map that holds the animal");
        C(code.Contains("corpse.SetForbidden(false, false);") && code.Contains("new Designation(corpse, DesignationDefOf.Haul)") && code.Contains("corpse.IsInValidStorage()"),
            "its body unforbidden and given the game's own haul order");
        C(code.Contains("StoreUtility.TryFindBestBetterStorageFor(corpse, null, at,") && code.Contains("Notify.Message(\"CC_BodyMark_NoStorage\"") && code.Contains("new LookTargets(corpse)"),
            "no storage for it: told once, through Notify, the body the look target");
        C(!code.Contains("Messages.Message(") && !patches.Contains("Messages.Message(") && !code.Contains("LetterStack"), "no direct message and no letter");
        C(code.Contains("dm.RemoveAllDesignationsOn(a);") && code.IndexOf("dm.RemoveAllDesignationsOn(a);", StringComparison.Ordinal) < code.IndexOf("dm.AddDesignation(new Designation(a, def));", StringComparison.Ordinal),
            "the mark replaces a hunting or taming order, as the game's own orders on an animal do");
        C(code.Contains("designationsByDef[def]") && code.Contains("BodyWhenItDiesMath.Judge(") && code.Contains("dm.RemoveDesignation(des)"),
            "the short pass finds every mark (after a load too) and drops the ones the rules say");
        C(patches.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]") && code.Contains("new Command_Action") && code.Contains("\"UI/Designators/Haul\""),
            "the button on the animal, with the game's haul icon");
        C(src("Beasts/BeastBook.cs").Contains("BodyWhenItDies.Rare(now)"), "no clock of its own: the wild's short pass, every 250 ticks");
        C(src("Animals/PetHunts.cs").Contains("markedForBody = BodyWhenItDies.Marked(x)") && src("Animals/HuntMath.cs").Contains("if (p.markedForBody) return"),
            "the colony's pets leave a marked animal alone");
        C(code.Contains("RimfolkPatches.On") && code.Contains("bodyWhenItDies"), "off with the setting or the deeper hooks");
        C(settings.Contains("public bool bodyWhenItDies = true;") && settings.Contains("Scribe_Values.Look(ref bodyWhenItDies, \"bodyWhenItDies\", true);")
          && catalog.Contains("Toggle(\"bodyWhenItDies\", \"CC_Settings_BodyWhenItDies\", \"CC_SetNote_BodyWhenItDies\", \"deepHooks\")")
          && catalog.IndexOf("Toggle(\"bodyWhenItDies\"", StringComparison.Ordinal) > catalog.IndexOf("Section(\"CC_Settings_Strangers\")", StringComparison.Ordinal)
          && catalog.IndexOf("Toggle(\"bodyWhenItDies\"", StringComparison.Ordinal) < catalog.IndexOf("Section(\"CC_Settings_WorldEventsHeader\")", StringComparison.Ordinal),
            "its setting, on by default and saved, with the animals");
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_BodyWhenItDies.xml"));
        C(defs.Contains("<DesignationDef>") && defs.Contains("<defName>Rimfolk_TakeBodyWhenDead</defName>") && defs.Contains("<texturePath>UI/Designators/Haul</texturePath>")
          && defs.Contains("<targetType>Thing</targetType>") && !defs.Contains("designateCancelable"),
            "the mark is a designation of Rimfolk's: shown over the animal with the game's haul icon, saved, and the Cancel tool takes it off");
        C(code.Contains("public static DesignationDef Rimfolk_TakeBodyWhenDead;"), "found by its DefOf");
        return n;
    }
}
