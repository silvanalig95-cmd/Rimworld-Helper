using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Medicine by what is treated (Health/MedicineChoiceMath.cs): the threat rule on clear cases, with the game's own numbers
/// for each wound kind (its bleed rate and infection chance, the part's health and bleeding), the care each gets under
/// every medical care setting (never above it, the serious always at it), what a tending does with the medicine in hand,
/// how many of one medicine are fetched, the order of tending, when a job ends for other medicine; and the seams in the mod.
/// </summary>
public static class MedicineChoiceTests
{
    private const int NoCare = MedicineChoiceMath.NoCare, NoMeds = MedicineChoiceMath.NoMeds, Herbal = MedicineChoiceMath.Herbal,
        Normal = MedicineChoiceMath.Normal, Best = MedicineChoiceMath.Best;

    private static readonly MedicineChoiceMath.Threat Harmless = MedicineChoiceMath.Threat.Harmless, Minor = MedicineChoiceMath.Threat.Minor,
        Serious = MedicineChoiceMath.Threat.Serious;

    /// <summary>A wound as the game makes it: its kind's bleed rate and infection chance, its size, the part's health and bleed factor.</summary>
    private static MedicineChoiceMath.Facts Wound(float kindBleed, float kindInfection, float severity, float partHealth, float partBleed = 1f,
        bool vital = false, bool organ = false, bool solid = false, bool animal = false)
    {
        return new MedicineChoiceMath.Facts
        {
            injury = true,
            severity = severity,
            partHealth = partHealth,
            bleedPerDay = solid ? 0f : severity * kindBleed * partBleed,
            infectionChance = solid ? 0f : kindInfection,
            vitalPart = vital,
            organ = organ,
            animal = animal
        };
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "medicine by need: " + what);
        }

        MedicineChoiceMath.Threat J(MedicineChoiceMath.Facts f, out MedicineChoiceMath.Why why) => MedicineChoiceMath.Judge(f, out why);
        bool Is(MedicineChoiceMath.Facts f, MedicineChoiceMath.Threat t, MedicineChoiceMath.Why w) => J(f, out var why) == t && why == w;

        // The game's wounds (Hediffs_Local_Injuries.xml): bleed rate a point a day, infection chance; parts' health (BodyParts_*).
        const float CutBleed = 0.06f, CutInf = 0.15f, BiteInf = 0.30f, BurnInf = 0.30f, CrushBleed = 0.01f, FrostInf = 0.25f, StumpBleed = 0.12f;
        const float Arm = 30f, Leg = 30f, Hand = 20f, Finger = 8f, Torso = 40f, Lung = 15f, Brain = 10f, Ribs = 30f, Femur = 25f, Neck = 25f;

        // ---------- The game's infection chance by a wound's size ----------
        C(Math.Abs(MedicineChoiceMath.SeverityFactor(1f) - 0.1f) < 1e-5 && Math.Abs(MedicineChoiceMath.SeverityFactor(12f) - 1f) < 1e-5
          && Math.Abs(MedicineChoiceMath.SeverityFactor(6.5f) - 0.55f) < 1e-4, "the size curve: a tenth at 1 point, the whole at 12, evenly between");
        C(Math.Abs(MedicineChoiceMath.WoundInfectionChance(0.3f, 12f, true) - 0.03f) < 1e-5, "an animal's wound: a tenth of the chance");
        C(MedicineChoiceMath.WoundInfectionChance(0f, 20f, false) == 0f, "a wound kind that never gets infected never does");

        // ---------- Harmless: no medicine, only bandaging ----------
        C(Is(Wound(0f, 0f, 4f, Leg), Harmless, MedicineChoiceMath.Why.Clean), "a bruise on the leg: harmless, it neither bleeds nor can get infected");
        C(Is(Wound(CutBleed, CutInf, 1f, Finger), Harmless, MedicineChoiceMath.Why.Small), "the smallest scratch (1 on a finger): harmless, it barely bleeds, 1.5% infection");
        C(Is(Wound(0f, 0f, 8f, Ribs, solid: true), Harmless, MedicineChoiceMath.Why.Clean), "a crack in the ribs (8 of 30): harmless, bone neither bleeds nor gets infected");
        C(Is(Wound(CrushBleed, CutInf, 2f, Leg), Harmless, MedicineChoiceMath.Why.Small), "a small crush (2): harmless, it barely bleeds, 2.7% infection");
        C(Is(Wound(0f, 0f, 6f, Leg, animal: true), Harmless, MedicineChoiceMath.Why.Clean), "an animal's bruise: harmless");

        // ---------- Minor: herbal at most ----------
        C(Is(Wound(CutBleed, CutInf, 2f, Arm), Minor, MedicineChoiceMath.Why.Bleeds), "a small cut (2 on the arm): minor, it bleeds 12% a day");
        C(Is(Wound(CutBleed, CutInf, 4f, Hand), Minor, MedicineChoiceMath.Why.Bleeds), "a scratch that bleeds (4 on the hand): minor, 24% a day");
        C(Is(Wound(0f, BurnInf, 3f, Arm), Minor, MedicineChoiceMath.Why.MayInfect), "a small burn (3 on the arm): minor, it can get infected (7.9%)");
        C(Is(Wound(CrushBleed, CutInf, 5f, Leg), Minor, MedicineChoiceMath.Why.MayInfect), "a crush (5): minor, it can get infected (6.4%)");
        C(Is(Wound(0f, FrostInf, 3f, Finger), Minor, MedicineChoiceMath.Why.MayInfect), "frostbite on a finger (3): minor, it can get infected");
        C(Is(Wound(CutBleed, BiteInf, 5f, Leg, animal: true), Minor, MedicineChoiceMath.Why.Bleeds), "a dog's bite (5): minor, it bleeds, though an animal's rarely gets infected");
        C(Is(Wound(CutBleed, BiteInf, 3f, Leg), Minor, MedicineChoiceMath.Why.Bleeds), "a small bite (3): minor");

        // ---------- Serious: the best the medical care allows ----------
        C(Is(Wound(CutBleed, CutInf, 10f, Torso), Serious, MedicineChoiceMath.Why.BleedsHeavily), "a gunshot in the torso (10): serious, it bleeds 60% a day");
        C(Is(Wound(CutBleed, CutInf, 2f, Neck, partBleed: 4f, vital: true), Serious, MedicineChoiceMath.Why.BleedsHeavily), "a small cut on the neck bleeds heavily (48% a day)");
        C(Is(Wound(CutBleed, CutInf, 2f, Lung, vital: true, organ: true), Serious, MedicineChoiceMath.Why.VitalPart), "a small cut on a lung: serious, a part they can't live without");
        C(Is(Wound(0f, 0f, 2f, Brain, vital: true, organ: true), Serious, MedicineChoiceMath.Why.VitalPart), "a bruise on the brain: serious, though it neither bleeds nor gets infected");
        C(Is(Wound(0f, 0f, 3f, 20f, organ: true), Serious, MedicineChoiceMath.Why.Organ), "a wound on an organ the game doesn't count vital: serious");
        C(Is(Wound(0f, 0f, 20f, Femur, solid: true), Serious, MedicineChoiceMath.Why.Deep), "a crack through most of the femur (20 of 25): serious, deep");
        C(Is(Wound(0f, BurnInf, 14f, Torso), Serious, MedicineChoiceMath.Why.Deep), "a large burn (14 on the torso): serious, deep (it gets infected one time in three)");
        C(J(Wound(0f, 0f, 12f, Torso), out _) == Harmless, "a big bruise on the torso (12 of 40) is still harmless: deep counts for a wound that can go bad, or half a part");
        C(Is(new MedicineChoiceMath.Facts { missingPart = true, severity = 1f, partHealth = Arm, bleedPerDay = Arm * StumpBleed }, Serious, MedicineChoiceMath.Why.BleedsHeavily),
            "a lost arm's fresh stump: serious, it bleeds 360% a day");
        C(Is(new MedicineChoiceMath.Facts { missingPart = true, severity = 1f, partHealth = Finger, bleedPerDay = Finger * StumpBleed }, Serious, MedicineChoiceMath.Why.BleedsHeavily),
            "a lost finger's stump bleeds heavily too (96% a day)");
        C(Is(new MedicineChoiceMath.Facts { missingPart = true, severity = 1f, partHealth = 2f, bleedPerDay = 2f * StumpBleed }, Minor, MedicineChoiceMath.Why.Bleeds),
            "a small creature's lost toe: minor, it bleeds a little");

        // A patient bleeding out: every wound that bleeds is serious; one that doesn't stays as it is.
        var smallCut = Wound(CutBleed, CutInf, 2f, Arm);
        smallCut.bleedingOut = true;
        C(Is(smallCut, Serious, MedicineChoiceMath.Why.BleedingOut), "bleeding out: even a small cut gets the best");
        var bruiseOut = Wound(0f, 0f, 4f, Leg);
        bruiseOut.bleedingOut = true;
        C(J(bruiseOut, out _) == Harmless, "bleeding out: a bruise is still harmless");
        var answered = Wound(0f, 0f, 4f, Leg);
        answered.bestByAnswer = true;
        C(Is(answered, Serious, MedicineChoiceMath.Why.CouncilAnswer), "the council's answer (the best medicine for the doctor): even a bruise gets the best");

        // ---------- Illnesses ----------
        C(Is(new MedicineChoiceMath.Facts { infection = true, lethal = true }, Serious, MedicineChoiceMath.Why.Infection), "an infection: serious");
        C(Is(new MedicineChoiceMath.Facts { lethal = true }, Serious, MedicineChoiceMath.Why.Lethal), "the flu (it can kill): serious");
        C(Is(new MedicineChoiceMath.Facts { lethal = true, lifeThreatening = true }, Serious, MedicineChoiceMath.Why.LifeThreatening), "the plague at its worst: serious, a threat to life now");
        C(Is(new MedicineChoiceMath.Facts { chronic = true }, Serious, MedicineChoiceMath.Why.Chronic), "a chronic condition (asthma): serious");
        C(Is(new MedicineChoiceMath.Facts { chronic = true, lethal = true }, Serious, MedicineChoiceMath.Why.Chronic), "a carcinoma: serious, a lasting condition");
        C(Is(new MedicineChoiceMath.Facts(), Serious, MedicineChoiceMath.Why.Illness), "any other illness a mod adds (gut worms): serious");
        C(Is(new MedicineChoiceMath.Facts { rimfolkAilment = true }, Minor, MedicineChoiceMath.Why.EverydayAilment), "Rimfolk's head cold: minor, an everyday ailment");
        C(Is(new MedicineChoiceMath.Facts { rimfolkAilment = true, rimfolkSerious = true }, Serious, MedicineChoiceMath.Why.SeriousIllness), "Rimfolk's tuberculosis: serious");
        C(J(new MedicineChoiceMath.Facts { rimfolkAilment = true, lethal = true }, out _) == Serious, "one of Rimfolk's ailments that can kill is never minor");
        var tb = new MedicineChoiceMath.Facts { rimfolkAilment = true, rimfolkSerious = true };
        tb.bleedingOut = true;
        C(J(tb, out _) == Serious, "an illness stays serious whatever else");

        // ---------- The care each gets: never above the setting ----------
        int[] ceilings = { NoCare, NoMeds, Herbal, Normal, Best };
        foreach (var t in new[] { Harmless, Minor, Serious })
            foreach (bool herbal in new[] { false, true })
                foreach (int c in ceilings)
                {
                    int got = MedicineChoiceMath.CareFor(t, c, herbal);
                    C(got <= c, $"{t}, setting {c}, harmless {(herbal ? "herbal" : "none")}: {got}, never above the setting");
                    if (c > NoCare) C(got >= NoMeds, $"{t} under setting {c}: still tended");
                }
        foreach (int c in ceilings) C(MedicineChoiceMath.CareFor(Serious, c, false) == c, $"the serious get the setting itself ({c})");
        C(MedicineChoiceMath.CareFor(Minor, Best, false) == Herbal && MedicineChoiceMath.CareFor(Minor, Normal, false) == Herbal, "the minor get herbal at most");
        C(MedicineChoiceMath.CareFor(Minor, NoMeds, false) == NoMeds, "and none where the setting says none");
        C(MedicineChoiceMath.CareFor(Harmless, Best, false) == NoMeds, "the harmless get no medicine");
        C(MedicineChoiceMath.CareFor(Harmless, Best, true) == Herbal && MedicineChoiceMath.CareFor(Harmless, NoMeds, true) == NoMeds, "or herbal at most by the setting");
        C(MedicineChoiceMath.CareFor(Serious, NoCare, false) == NoCare && MedicineChoiceMath.CareFor(Harmless, NoCare, true) == NoCare, "no care stays no care");

        // ---------- Which care allows which medicine ----------
        const float HerbalP = 0.6f, IndustrialP = 1f, GlitterP = 1.6f;
        int herbalNeeds = MedicineChoiceMath.NeedsCare(HerbalP, HerbalP, IndustrialP), industrialNeeds = MedicineChoiceMath.NeedsCare(IndustrialP, HerbalP, IndustrialP),
            glitterNeeds = MedicineChoiceMath.NeedsCare(GlitterP, HerbalP, IndustrialP);
        C(herbalNeeds == Herbal && industrialNeeds == Normal && glitterNeeds == Best, "herbal needs herbal or better, industrial normal, glitterworld the best");
        C(MedicineChoiceMath.NeedsCare(0.5f, HerbalP, IndustrialP) == Herbal, "a weaker mod's medicine (a poultice at 0.5) counts as herbal");
        C(MedicineChoiceMath.Allows(Best, glitterNeeds) && !MedicineChoiceMath.Allows(Herbal, industrialNeeds) && MedicineChoiceMath.Allows(Normal, herbalNeeds),
            "the game's own rule: a care allows medicine up to its kind");
        C(!MedicineChoiceMath.Allows(NoMeds, herbalNeeds) && !MedicineChoiceMath.Allows(NoCare, herbalNeeds), "no medicine allows none");

        // ---------- The player's case: the plague, two scratches and a bruise, medical care at the best ----------
        int plague = MedicineChoiceMath.CareFor(J(new MedicineChoiceMath.Facts { lethal = true }, out _), Best, false);
        int scratch = MedicineChoiceMath.CareFor(J(Wound(CutBleed, CutInf, 3f, Arm), out _), Best, false);
        int bruise = MedicineChoiceMath.CareFor(J(Wound(0f, 0f, 4f, Leg), out _), Best, false);
        C(plague == Best && scratch == Herbal && bruise == NoMeds, "the plague the best, the scratches herbal at most, the bruise none");
        // The game tends the plague alone (an illness), then the wounds in one tending; the most any gets is the tending's.
        var tendings = new List<int> { plague, Math.Max(scratch, bruise) };
        C(MedicineChoiceMath.SameMedicineCount(tendings) == 1, "one glitterworld medicine is fetched, for the plague alone");
        C(MedicineChoiceMath.WithCarried(plague, glitterNeeds) == MedicineChoiceMath.AtTend.UseIt, "the plague is tended with it");
        C(MedicineChoiceMath.WithCarried(tendings[1], glitterNeeds) == MedicineChoiceMath.AtTend.FetchOther, "the scratches never with glitterworld: the doctor goes for herbal");
        C(MedicineChoiceMath.WithCarried(bruise, glitterNeeds) == MedicineChoiceMath.AtTend.WithoutIt, "a bruise on its own is tended without medicine");
        C(MedicineChoiceMath.WithCarried(scratch, herbalNeeds) == MedicineChoiceMath.AtTend.UseIt, "the scratches with herbal");
        C(MedicineChoiceMath.SameMedicineCount(new List<int> { Best, Best, Herbal }) == 2, "two serious tendings: two of the best");
        C(MedicineChoiceMath.SameMedicineCount(new List<int> { Herbal, Herbal, NoMeds }) == 2, "two tendings at herbal, one without: two herbal");
        C(MedicineChoiceMath.SameMedicineCount(new List<int> { NoMeds, NoMeds }) == 0 && MedicineChoiceMath.SameMedicineCount(new List<int>()) == 0,
            "only harmless wounds: no medicine fetched at all");

        // ---------- The order of tending: the serious first, then the minor, then the harmless ----------
        float[] seriousP = { 0f, 0.025f, 1f, 4.5f }, lightP = { 0f, 0.025f, 0.09f, 0.59f, 1f };
        bool order = true;
        foreach (float s in seriousP)
            foreach (float m in lightP)
                foreach (float h in lightP)
                    order &= s + MedicineChoiceMath.PriorityShift(Serious) > m + MedicineChoiceMath.PriorityShift(Minor)
                             && m + MedicineChoiceMath.PriorityShift(Minor) > h + MedicineChoiceMath.PriorityShift(Harmless);
        C(order, "the shifted priorities keep the kinds apart, whatever the game's own (bleeding, a threat to life)");
        C(MedicineChoiceMath.PriorityShift(Serious) == 0f, "the serious keep the game's own priority");

        // ---------- When a tend job ends after a tending, so the next one fetches other medicine ----------
        const int Unknown = int.MaxValue;
        C(!MedicineChoiceMath.EndAfter(Best, true, glitterNeeds, Best, true), "glitterworld in hand, the next gets the best: go on");
        C(MedicineChoiceMath.EndAfter(Herbal, true, glitterNeeds, Best, true), "glitterworld in hand, the next gets herbal: go for herbal");
        C(!MedicineChoiceMath.EndAfter(NoMeds, true, glitterNeeds, Best, true), "glitterworld in hand, the next gets none: tended without, the medicine kept");
        C(MedicineChoiceMath.EndAfter(NoMeds, true, glitterNeeds, Best, false), "with Smart Medicine, which tends with what is in hand: the job ends instead");
        C(MedicineChoiceMath.EndAfter(Best, true, herbalNeeds, Herbal, true), "herbal in hand for the scratches, the plague comes due: go for the best");
        C(!MedicineChoiceMath.EndAfter(Best, true, herbalNeeds, Best, true), "herbal was the best there was for the plague: go on");
        C(!MedicineChoiceMath.EndAfter(Best, true, herbalNeeds, Unknown, true), "not knowing what it was fetched for (after a load): go on");
        C(MedicineChoiceMath.EndAfter(Best, false, 0, NoMeds, true), "none in hand for a bruise, the plague comes due: go for medicine");
        C(!MedicineChoiceMath.EndAfter(Best, false, 0, Best, true) && !MedicineChoiceMath.EndAfter(NoMeds, false, 0, NoMeds, true),
            "none in hand because there is none, or none wanted: go on");
        C(!MedicineChoiceMath.EndAfter(-1, true, glitterNeeds, Best, true), "nothing left to tend: the game ends the job itself");
        C(MedicineChoiceMath.SoonTicks == 2500, "an illness coming due within the hour counts for the medicine fetched");

        // ---------- The seams ----------
        string root = Root();
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string choice = Code("Health/MedicineChoice.cs"), patches = Code("Health/MedicineChoicePatches.cs"), bridge = Code("Integrations/SmartMedicine.cs");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool medicineByNeed = true;") && settings.Contains("Scribe_Values.Look(ref medicineByNeed, \"medicineByNeed\", true);")
          && catalog.Contains("Toggle(\"medicineByNeed\", \"CC_Settings_MedicineByNeed\", \"CC_SetNote_MedicineByNeed\", \"deepHooks\")"),
            "the setting \"Medicine by what is treated\", on by default, saved, behind the deeper hooks");
        C(settings.Contains("public HarmlessMedicine harmlessMedicine = HarmlessMedicine.None;") && catalog.Contains("Choice(\"harmlessMedicine\"")
          && catalog.Contains("new[] { \"None\", \"Herbal\" }, \"medicineByNeed\")"), "what harmless wounds get: none by default, or herbal at most");
        C(choice.Contains("public static bool SettingOn => RimfolkPatches.On && (ColonyCouncilMod.Settings?.medicineByNeed ?? true);"), "off with the setting or the deeper hooks");
        foreach (var cls in new[] { "Patch_MedicineTendOrder", "Patch_MedicineFirstTending", "Patch_MedicineCount", "Patch_MedicineAtTend" })
        {
            int at = patches.IndexOf("public static class " + cls, StringComparison.Ordinal);
            string body = at >= 0 ? patches.Substring(at, Math.Min(900, patches.Length - at)) : "";
            C(body.Contains("public static bool Prepare() => !SmartMedicineBridge.Present;") && body.Contains("StandsDownWhy"), $"{cls}: Rimfolk's own way, standing down with Smart Medicine");
        }
        foreach (var cls in new[] { "Patch_SmartMedicineJob", "Patch_SmartMedicineSearch", "Patch_SmartMedicineTend", "Patch_SmartMedicineHealthTab" })
        {
            int at = patches.IndexOf("public static class " + cls, StringComparison.Ordinal);
            string body = at >= 0 ? patches.Substring(at, Math.Min(500, patches.Length - at)) : "";
            C(body.Contains("public static bool Prepare() => SmartMedicineBridge.Ready;") && body.Contains("SmartMedicineBridge.Refresh("), $"{cls}: fills in Smart Medicine's choice, only with it");
        }
        C(patches.Contains("[HarmonyBefore(SmartMedicineBridge.HarmonyId)]") && bridge.Contains("public const string HarmonyId = \"uuugggg.rimworld.SmartMedicine.main\";"),
            "before Smart Medicine's own prefixes, by its Harmony id");
        C(bridge.Contains("\"SmartMedicine.PriorityCareSettingsComp\"") && bridge.Contains("\"SmartMedicine.PriorityCareComp\"") && bridge.Contains("\"hediffCare\"")
          && bridge.Contains("typeof(Dictionary<Hediff, MedicalCareCategory>)"), "Smart Medicine read by name: the continued version's comp, the original's, the dictionary checked");
        C(bridge.Contains("\"SmartMedicine.DefaultCareFix\"") && bridge.Contains("\"GetOriginalCare\""), "its short-lived raise of a patient's care read through");
        C(bridge.Contains("if (has && !mine) continue;") && bridge.Contains("book.HandsOff(id);") && bridge.Contains("if (mine && (int)cur != written)"),
            "a choice set by hand, changed by hand or taken back to the default is the player's");
        C(choice.Contains("Scribe_Collections.Look(ref filled, \"filled\", LookMode.Value, LookMode.Value, ref filledKeys, ref filledValues);")
          && choice.Contains("Scribe_Collections.Look(ref handsOff, \"handsOff\", LookMode.Value);") && choice.Contains("Scribe_Collections.Look(ref bestByAnswer, \"bestByAnswer\", LookMode.Value);")
          && choice.Contains("MaxBest = 40, MaxFilled = 600, MaxHandsOff = 200"), "saved: the answer's patients, Rimfolk's choices in Smart Medicine and those taken back, capped and pruned");
        C(choice.Contains("settings.medCare") == false && patches.Contains("settings.medCare = own;"), "the medical care setting is only ever lowered for the game's own search, and put back");
        C(patches.Contains("MedicineChoice.SoonCare(patient, MedicineChoiceMath.SoonTicks)") && choice.Contains("td.TProps.TendIsPermanent || td.tendTicksLeft - td.TProps.TendTicksOverlap > ticks"),
            "a job made for a bruise brings the medicine of an illness coming due on the way");
        C(patches.Contains("doctor.CurJob.def != JobDefOf.TendPatient) return;"), "another mod's way of tending (a warcasket's dressing) is left as it is");
        C(!choice.Contains("Messages.Message") && !choice.Contains("LetterStack") && !patches.Contains("Messages.Message") && !patches.Contains("LetterStack")
          && !bridge.Contains("Messages.Message"), "no letters and no messages");
        C(Code("Dilemmas/DilemmaWorkers_Variety.cs").Contains("MedicineChoice.GaveBest(p);"), "the council's answer for the doctor who is the patient keeps everything of theirs on the best");
        C(patches.Contains("[HarmonyPatch(typeof(Hediff), nameof(Hediff.GetTooltip))]") && choice.Contains("CC_Med_Why_"), "shown: each condition's tooltip says what it gets and why");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_MedicineChoice.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (MedicineChoiceMath.Why w in Enum.GetValues(typeof(MedicineChoiceMath.Why)))
            C(keyed.ContainsKey("CC_Med_Why_" + w), $"the reason {w} has its words");
        foreach (var k in new[] { "CC_Med_Line", "CC_Med_Line_ByHand", "CC_Med_Care_Best", "CC_Med_Care_Setting", "CC_Med_Care_None", "CC_Med_Care_Herbal",
                     "CC_Settings_MedicineByNeed", "CC_Settings_MedicineByNeed_Tip", "CC_SetNote_MedicineByNeed", "CC_Settings_HarmlessMedicine",
                     "CC_Settings_HarmlessMedicine_Tip", "CC_HarmlessMed_None", "CC_HarmlessMed_Herbal", "CC_SetNote_HarmlessMed_None", "CC_SetNote_HarmlessMed_Herbal" })
            C(keyed.ContainsKey(k), $"{k} exists");
        C(keyed.Values.All(v => !v.Contains('—') && !v.Contains('!')), "no em dashes or exclamation marks");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
