using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Bed rest only when it makes sense (Health/BedRestMath.cs): the thresholds, each family's verdict (the game's wounds by
/// medicine by what is treated's judgement, the game's and mods' illnesses, what the game makes urgent, the player's order,
/// the council's decree, another mod's reason), sleeping a little more but never all day, and each of Rimfolk's ailments
/// stage by stage from its def; and the seams in the mod.
/// </summary>
public static class BedRestTests
{
    private static readonly BedRestMath.Why None = BedRestMath.Why.None;

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "bed rest: " + what);
        }

        // ---------- The thresholds ----------
        C(BedRestMath.StrongPain == 0.15f, "strong pain is the game's own line past minor pain, 15%");
        C(BedRestMath.SeriousDrop == 0.3f && BedRestMath.PoorLevel == 0.7f, "a serious stage leaves a capacity poor (the health tab's under 70%)");
        C(Math.Abs(BedRestMath.Drop(-0.2f, 1f, 999f) - 0.2f) < 1e-5 && Math.Abs(BedRestMath.Drop(0f, 0.6f, 999f) - 0.4f) < 1e-5
          && Math.Abs(BedRestMath.Drop(0f, 1f, 0.5f) - 0.5f) < 1e-5 && BedRestMath.Drop(0.1f, 1f, 999f) == 0f,
            "what a capacity modifier takes off: its offset, its factor, a cap, nothing for a gain");
        C(Math.Abs(BedRestMath.SleepHours(1f) - 7f) < 0.2f, $"the game's own sleep is about 7 hours a day ({BedRestMath.SleepHours(1f):0.0})");
        C(BedRestMath.SleepHours(BedRestMath.MaxRestFallWorking) <= 9.5f && BedRestMath.SleepHours(BedRestMath.MaxRestFallWorking) - BedRestMath.SleepHours(1f) <= 2.5f,
            $"the most a condition they work with tires them: about two hours more sleep, never all day ({BedRestMath.SleepHours(BedRestMath.MaxRestFallWorking):0.0} hours)");
        C(BedRestMath.SleepHours(1.1f) > BedRestMath.SleepHours(1f) && BedRestMath.SleepHours(1.3f) > BedRestMath.SleepHours(1.1f), "the more a stage tires, the more they sleep");
        C(BedRestMath.Sleeps(new BedRestMath.Stage { restFall = 1.05f }) && !BedRestMath.Sleeps(new BedRestMath.Stage { restFall = 1f }),
            "a stage that makes rest fall faster has them sleep a little more");
        C(BedRestMath.LosingBlood == MedicineChoiceMath.BleedsPerDay, "losing blood is the game's own line, a tenth of the blood a day");

        // ---------- Wounds: medicine by what is treated's judgement, then the pain and the limbs together ----------
        const float CutBleed = 0.06f, CutInf = 0.15f, BruiseInf = 0f, BulletBleed = 0.06f;
        const float Arm = 30f, Leg = 30f, Torso = 40f, Hand = 20f;
        MedicineChoiceMath.Facts W(float bleed, float inf, float sev, float partHealth, bool vital = false) => new MedicineChoiceMath.Facts
        {
            injury = true, severity = sev, partHealth = partHealth, bleedPerDay = sev * bleed, infectionChance = inf, vitalPart = vital
        };
        BedRestMath.Condition Wound(MedicineChoiceMath.Facts f, bool tended = true, bool legs = false, bool arms = false)
        {
            var t = MedicineChoiceMath.Judge(f, out var why);
            return new BedRestMath.Condition { wound = true, tended = tended, threat = t, threatWhy = why, onLegs = legs, onArms = arms, severity = f.severity };
        }
        var fine = new BedRestMath.Body { woundPain = 0.05f, moving = 1f, manipulation = 1f };
        var bruise = Wound(W(0f, BruiseInf, 4f, Leg), legs: true);
        var scratch = Wound(W(CutBleed, CutInf, 2f, Arm), arms: true);
        var cut = Wound(W(CutBleed, CutInf, 4f, Hand), arms: true);
        var gunshot = Wound(W(BulletBleed, CutInf, 10f, Torso));
        var deepLeg = Wound(W(CutBleed, CutInf, 16f, Leg), legs: true);
        var neck = Wound(W(CutBleed, CutInf, 3f, 25f, vital: true));
        C(BedRestMath.Sends(bruise, fine) == None && BedRestMath.Sends(scratch, fine) == None && BedRestMath.Sends(cut, fine) == None,
            "a tended bruise, scratch or small cut keeps them working (the game would keep them in bed until healed)");
        C(BedRestMath.GameCounts(bruise) && BedRestMath.GameCounts(cut), "the game's own bed rest counts every tended wound still healing");
        C(BedRestMath.Sends(gunshot, fine) == BedRestMath.Why.SeriousWound && gunshot.threatWhy == MedicineChoiceMath.Why.BleedsHeavily,
            "a gunshot in the torso that bleeds heavily: in bed until it heals");
        C(BedRestMath.Sends(deepLeg, fine) == BedRestMath.Why.SeriousWound && BedRestMath.Sends(neck, fine) == BedRestMath.Why.SeriousWound,
            "a deep wound, a wound on a vital part: in bed until it heals");
        C(BedRestMath.Sends(Wound(W(CutBleed, CutInf, 10f, Torso), tended: false), fine) == None && BedRestMath.Own(Wound(W(CutBleed, CutInf, 10f, Torso), tended: false), fine) == BedRestMath.Why.SeriousWound,
            "an untended serious wound: the game's urgent tending has them in bed, and the line already says it rests until it heals");
        var hurts = new BedRestMath.Body { woundPain = 0.15f, moving = 1f, manipulation = 1f };
        var hurtsLess = new BedRestMath.Body { woundPain = 0.14f, moving = 1f, manipulation = 1f };
        C(BedRestMath.Sends(scratch, hurts) == BedRestMath.Why.WoundsHurt && BedRestMath.Sends(scratch, hurtsLess) == None,
            "many small wounds that together bring strong pain (15%) need bed; under it they work through it");
        var lame = new BedRestMath.Body { woundPain = 0.1f, moving = 0.7f, manipulation = 1f };
        var limping = new BedRestMath.Body { woundPain = 0.1f, moving = 0.8f, manipulation = 1f };
        C(BedRestMath.Sends(bruise, lame) == BedRestMath.Why.WoundsLame && BedRestMath.Sends(bruise, limping) == None,
            "wounds that leave moving poor (70%) with one on a leg need bed; limping at 80% they keep working");
        C(BedRestMath.Sends(cut, lame) == None, "a lame leg from something else doesn't send a cut hand to bed");
        var clumsy = new BedRestMath.Body { woundPain = 0.1f, moving = 1f, manipulation = 0.65f };
        C(BedRestMath.Sends(cut, clumsy) == BedRestMath.Why.WoundsClumsy && BedRestMath.Sends(bruise, clumsy) == None,
            "wounds on the arms that leave manipulation poor need bed; a bruised leg doesn't");
        var smallBleed = Wound(W(CutBleed, CutInf, 3f, Arm), arms: true);
        smallBleed.ignored = true;
        var gunIgnored = gunshot;
        gunIgnored.ignored = true;
        C(BedRestMath.Sends(gunIgnored, fine) == None && BedRestMath.Own(smallBleed, hurts) == None,
            "a condition taken off bed rest by hand (Smart Medicine) never sends them, however serious");

        // ---------- Illnesses: the game's and the mods' ----------
        BedRestMath.Condition Ill(bool immunizable, bool lethal, float pain = 0f, float cons = 0f, float moving = 0f, float breathing = 0f, float restFall = 1f, bool immune = false) =>
            new BedRestMath.Condition
            {
                illness = true, immunizable = immunizable, immune = immune, lethal = lethal,
                stage = new BedRestMath.Stage { pain = pain, consciousness = cons, moving = moving, breathing = breathing, restFall = restFall }
            };
        var fluMinor = Ill(true, true, 0f, 0.05f, 0f, 0.1f);
        var fluExtreme = Ill(true, true, 0.05f, 0.15f, 0f, 0.2f);
        C(BedRestMath.Sends(fluMinor, fine) == BedRestMath.Why.Lethal, "the flu, even at its mildest: it can kill, so bed while they build immunity");
        C(BedRestMath.Sends(Ill(true, true, 0.85f, 0.3f, 0f, 0.15f), fine) == BedRestMath.Why.Lethal, "the plague: bed");
        C(BedRestMath.Sends(Ill(true, true, 0.3f, 0.9f), fine) == BedRestMath.Why.Lethal, "malaria, sleeping sickness, a wound infection: bed");
        var fluImmune = fluExtreme;
        fluImmune.immune = true;
        var fluTired = fluMinor;
        fluTired.stage.restFall = 1.3f;
        C(BedRestMath.Sends(fluImmune, fine) == None && !BedRestMath.GameCounts(fluImmune),
            "once immune, the flu passes on its feet, as the game has it");
        C(BedRestMath.Sends(Ill(false, false, 0.2f), fine) == None && BedRestMath.Own(Ill(false, false, 0.2f), fine) == BedRestMath.Why.Pain,
            "an illness the game doesn't rest for (gut worms, a carcinoma, a bad back) is left as the game has it, whatever its stage");
        C(BedRestMath.Sends(Ill(true, false, 0.05f, 0.15f, 0f, 0.6f), fine) == BedRestMath.Why.Breathless && BedRestMath.Sends(Ill(true, false, 0.05f, 0.15f, 0f, 0.15f), fine) == None,
            "a mod's allergy: in bed at the stage that leaves breathing poor, on its feet at the mild one");
        C(BedRestMath.Sends(Ill(true, false, 0.5f), fine) == BedRestMath.Why.Pain, "a stage in strong pain (a mod's parasites at their worst): bed");
        C(BedRestMath.Sends(Ill(true, false, 0f, 0.3f), fine) == BedRestMath.Why.Dazed && BedRestMath.Sends(Ill(true, false, 0f, 0f, 0.3f), fine) == BedRestMath.Why.Unsteady
          && BedRestMath.Sends(Ill(true, false, 0f, 0.29f, 0.29f, 0.29f), fine) == None,
            "a stage that takes 30% off consciousness or moving needs bed; just under, they keep working");
        C(BedRestMath.Sends(Ill(true, false, 0.14f, 0.15f, 0.2f, 0.2f), fine) == None,
            "a cold or a stomach bug's worst day (pain under 15%, capacities a fifth down) is a minor problem: they keep working");
        C(BedRestMath.SleepsMore(Ill(true, false, 0f, 0.08f, 0f, 0.08f, 1.2f), fine) && !BedRestMath.SleepsMore(Ill(true, false, 0f, 0.08f), fine)
          && !BedRestMath.SleepsMore(fluTired, fine),
            "a stage they work with that tires them has them sleep a little more; one that sends them to bed doesn't need saying");

        // ---------- The patient: urgent cases, tending, orders, the decree, another mod ----------
        var none = new BedRestMath.Patient();
        BedRestMath.InBed D(Func<BedRestMath.Patient, BedRestMath.Patient> f) => BedRestMath.Decide(f(none));
        C(D(p => p) == BedRestMath.InBed.No && !BedRestMath.Rests(none), "nothing at all: no rest");
        C(D(p => { p.gameRest = true; p.gameOwnReasons = true; return p; }) == BedRestMath.InBed.No,
            "the game's rest for a tended scratch or a cold, and nothing the rule counts: they keep working");
        C(D(p => { p.exempt = true; p.gameRest = true; p.gameOwnReasons = true; return p; }) == BedRestMath.InBed.GameOwn
          && D(p => { p.exempt = true; p.conditionRests = true; return p; }) == BedRestMath.InBed.No, "a baby or a creature not of flesh: the game's own rest, the rule never");
        C(D(p => { p.downed = true; return p; }) == BedRestMath.InBed.Downed && D(p => { p.surgery = true; return p; }) == BedRestMath.InBed.Surgery
          && D(p => { p.labor = true; return p; }) == BedRestMath.InBed.Labor, "down, an operation due, labour: in bed, as the game has it");
        C(D(p => { p.conditionRests = true; p.needsTending = true; return p; }) == BedRestMath.InBed.Condition, "a condition that needs bed is named before the tending");
        C(D(p => { p.needsTending = true; p.doctorFree = true; return p; }) == BedRestMath.InBed.Tending, "something small to tend and a doctor free: lies down to be tended");
        C(D(p => { p.needsTending = true; p.gameRest = true; return p; }) == BedRestMath.InBed.No, "something small to tend and no doctor free: keeps working until one is");
        C(D(p => { p.needsTending = true; p.bleeding = true; return p; }) == BedRestMath.InBed.Tending && D(p => { p.needsTending = true; p.seriousToTend = true; return p; }) == BedRestMath.InBed.Tending,
            "bleeding, or something serious to tend: in bed to be tended, doctor free or not");
        C(D(p => { p.ordered = true; p.gameRest = true; p.gameOwnReasons = true; return p; }) == BedRestMath.InBed.Ordered && D(p => { p.ordered = true; return p; }) == BedRestMath.InBed.No,
            "the player's order to bed keeps the game's rest until the game's own rest would end");
        C(D(p => { p.decree = true; p.gameRest = true; p.gameOwnReasons = true; return p; }) == BedRestMath.InBed.Decree, "the council's rest when ill decree: the game's rest for anyone ill or hurt");
        C(D(p => { p.gameRest = true; return p; }) == BedRestMath.InBed.GameOwn, "another mod's reason for rest, not one the rule covers, is left as it is");

        // ---------- Rimfolk's ailments, one by one, from their defs ----------
        string root = Root();
        var ailments = LoadAilments(Path.Combine(root, "Defs", "HediffDefs"));
        C(ailments.Count >= 80, $"found Rimfolk's ailments ({ailments.Count})");
        // Where each rests from (the severity, at the start of a stage); every one not named keeps them working at every stage.
        var restsFrom = new Dictionary<string, float>
        {
            // Serious illnesses that kill fast, in bed from their first signs.
            ["Council_Ail_Cholera"] = 0.05f, ["Council_Ail_Meningitis"] = 0f, ["Council_Ail_Pneumonia"] = 0f,
            // Serious illnesses from the stage they are really underway, their first fever worked through (restFrom).
            ["Council_Ail_Appendicitis"] = 0.25f, ["Council_Ail_BirdFever"] = 0.3f, ["Council_Ail_BlackBane"] = 0.3f, ["Council_Ail_Glassblood"] = 0.3f,
            ["Council_Ail_HiveFever"] = 0.3f, ["Council_Ail_SwampFever"] = 0.3f, ["Council_Ail_Tetanus"] = 0.3f, ["Council_Ail_Typhus"] = 0.3f,
            ["Council_Ail_ValleyFever"] = 0.3f, ["Council_Ail_Rabies"] = 0.5f, ["Council_Ail_Tuberculosis"] = 0.6f, ["Council_Ail_Scurvy"] = 0.75f,
            ["Council_Ail_Measles"] = 0.6f, ["Council_Ail_Shingles"] = 0.5f,
            // Strong pain at their worst.
            ["Council_Ail_KidneyStone"] = 0.25f, ["Council_Ail_Migraine"] = 0.35f, ["Council_Ail_TornBack"] = 0.35f
        };
        foreach (var name in restsFrom.Keys) C(ailments.Any(a => a.name == name), $"{name} is one of Rimfolk's ailments");
        var shouldSleep = new HashSet<string>
        {
            "Council_Ail_HeadCold", "Council_Ail_ChestCold", "Council_Ail_SprainedAnkle", "Council_Ail_DrowsySpells", "Council_Ail_Convalescent",
            "Council_Ail_Dreamfever", "Council_Ail_Insomnia", "Council_Ail_BirdFever", "Council_Ail_BlackBane", "Council_Ail_Glassblood",
            "Council_Ail_HiveFever", "Council_Ail_SwampFever", "Council_Ail_Typhus", "Council_Ail_ValleyFever", "Council_Ail_Tetanus",
            "Council_Ail_Tuberculosis", "Council_Ail_Measles", "Council_Ail_Scurvy", "Council_Ail_HuntersFever", "Council_Ail_MechaniteFever",
            "Council_Ail_StomachBug", "Council_Ail_StrainedBack", "Council_Ail_TornBack"
        };
        foreach (var a in ailments)
        {
            float? from = restsFrom.TryGetValue(a.name, out float f0) ? f0 : (float?)null;
            bool ok = true, slept = false;
            string got = "";
            foreach (var st in a.stages.Where(s => s.visible))
            {
                float sev = Math.Max(st.min, 0.001f);
                var c = new BedRestMath.Condition
                {
                    illness = true, immunizable = a.immunizable, lethal = a.lethal, rimfolk = true, restFrom = a.restFrom, severity = sev, stage = st.stage
                };
                bool rests = BedRestMath.Sends(c, fine) != None;
                bool want = from.HasValue && st.min >= from.Value - 1e-4f;
                if (rests != want) ok = false;
                got += $" {st.min:0.##}:{(rests ? "bed" : "works")}";
                if (!rests)
                {
                    C(st.stage.restFall <= BedRestMath.MaxRestFallWorking + 1e-4f,
                        $"{a.name} at {st.label}: they keep working and sleep at most a little more (rest falls x{st.stage.restFall:0.##}, never all day)");
                    if (BedRestMath.SleepsMore(c, fine)) slept = true;
                }
            }
            C(ok, $"{a.name} rests {(from.HasValue ? "from " + from.Value.ToString("0.##") : "never")} (by its def:{got})");
            if (shouldSleep.Contains(a.name)) C(slept, $"{a.name} sleeps a little more at a stage they work through");
            if (a.restFrom > 0f)
                C(a.stages.Any(s => Math.Abs(s.min - a.restFrom) < 1e-4f) && a.restFrom <= a.maxSeverity,
                    $"{a.name}'s restFrom ({a.restFrom:0.##}) is where one of its stages starts, so the line names the stage");
        }
        // The everyday ones the game kept in bed until immune now keep them working.
        foreach (var name in new[] { "Council_Ail_HeadCold", "Council_Ail_ChestCold", "Council_Ail_StomachBug", "Council_Ail_Dreamfever" })
        {
            var a = ailments.FirstOrDefault(x => x.name == name);
            C(a != null && a.immunizable && a.stages.Where(s => s.visible).All(s => BedRestMath.GameCounts(new BedRestMath.Condition { illness = true, immunizable = true })
                && BedRestMath.Sends(new BedRestMath.Condition { illness = true, immunizable = true, rimfolk = true, severity = Math.Max(s.min, 0.001f), stage = s.stage }, fine) == None),
                $"{name}: the game kept them in bed until immune; now they keep working at every stage");
        }
        // Once immune, the killers without a restFrom pass on their feet unless the stage itself needs bed.
        var pneumonia = ailments.FirstOrDefault(x => x.name == "Council_Ail_Pneumonia");
        if (pneumonia != null)
        {
            var chest = pneumonia.stages.First(s => s.label == "fever and chest pain");
            var labored = pneumonia.stages.First(s => s.label == "labored breathing");
            C(BedRestMath.Sends(new BedRestMath.Condition { illness = true, immunizable = true, immune = true, lethal = true, rimfolk = true, stage = chest.stage }, fine) == None
              && BedRestMath.Sends(new BedRestMath.Condition { illness = true, immunizable = true, immune = true, lethal = true, rimfolk = true, stage = labored.stage }, fine) == BedRestMath.Why.Breathless,
                "pneumonia once immune: up at fever and chest pain, still in bed while the breathing is labored");
        }

        // ---------- The seams ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string rest = Code("Health/BedRest.cs"), patches = Code("Health/BedRestPatches.cs"), bridge = Code("Integrations/SmartMedicine.cs");
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs"), defsCode = Code("Ailments/AilmentDefs.cs");
        C(settings.Contains("public bool restByNeed = true;") && settings.Contains("Scribe_Values.Look(ref restByNeed, \"restByNeed\", true);")
          && catalog.Contains("Toggle(\"restByNeed\", \"CC_Settings_RestByNeed\", \"CC_SetNote_RestByNeed\", \"deepHooks\")"),
            "the setting \"Bed rest only when it makes sense\", on by default, saved, behind the deeper hooks");
        int med = catalog.IndexOf("Toggle(\"medicineByNeed\"", StringComparison.Ordinal), mine = catalog.IndexOf("Toggle(\"restByNeed\"", StringComparison.Ordinal),
            bonds = catalog.IndexOf("Section(\"CC_Settings_Bonds\")", StringComparison.Ordinal);
        C(med > 0 && mine > med && mine < bonds, "it sits with the health settings, after medicine by what is treated");
        C(rest.Contains("public static bool On => NoticeKept && RimfolkPatches.On && (ColonyCouncilMod.Settings?.restByNeed ?? true);"),
            "off with the setting, the deeper hooks, or the recovery notice not found");
        C(patches.Contains("[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest))]") && patches.Contains("__result = BedRest.Verdict(pawn, __result);"),
            "the game's answer to who seeks medical rest is the rule's");
        C(!patches.Contains("typeof(HediffSet)"), "the HediffSet checks stay the game's (they also decide whether tended wounds heal, and a caravan's bedrolls)");
        C(patches.Contains("[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]") && patches.Contains("nameof(BedRest.GameRest)")
          && patches.Contains("BedRest.NoticeKept = found > 0;"), "the recovery notice keeps the game's own answer, or the rule stays off");
        C(rest.Contains("if (gameView > 0 || working ||") && rest.Contains("gameView++;"), "the game's own answer, when asked for, is the game's");
        C(patches.Contains("nameof(Building_Bed.GetBedRestFloatMenuOption)") && patches.Contains("Finalizer") && rest.Contains("p.CurJob?.restUntilHealed"),
            "the player's order to a medical bed: offered and kept for whatever the game would rest for");
        C(rest.Contains("\"Council_Decree_SickBay\"") && File.ReadAllText(Path.Combine(root, "Defs", "DecreeDefs", "Council_Decrees3.xml")).Contains("<defName>Council_Decree_SickBay</defName>"),
            "the council's rest when ill decree is read by its name");
        C(patches.Contains("[HarmonyPriority(Priority.Low)]") && patches.Contains("MedicineChoice.LineFor(__instance)"), "the line sits after medicine by what is treated's");
        C(patches.Contains("nameof(JobDriver_LayDown.GetReport)") && rest.Contains("\"ReportResting\".Translate()"), "a resting colonist's activity names what they rest for, only where the game says \"resting.\"");
        C(bridge.Contains("\"ignoredHediffs\"") && bridge.Contains("typeof(HashSet<Hediff>)") && rest.Contains("SmartMedicineBridge.OffBedRest(h)"),
            "Smart Medicine's conditions taken off bed rest by hand stay off");
        C(rest.Contains("WorkGiver_PatientGoToBedTreatment.AnyAvailableDoctorFor(p)") && rest.Contains("ShouldEverReceiveMedicalCareFromPlayer(p)"),
            "a doctor free to tend is the game's own look for one");
        C(rest.Contains("ConditionTicks = 60") && rest.Contains("s.tick != now || s.count != count"), "cheap: the patient looked at once a tick, their conditions once a second or when one comes or goes");
        C(defsCode.Contains("public float restFrom;") && defsCode.Contains("restFrom above the ailment's highest severity never comes"), "Rimfolk's ailments say where rest starts, checked at load");
        C(!rest.Contains("Messages.Message") && !rest.Contains("LetterStack") && !patches.Contains("Messages.Message"), "no letters and no messages");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_BedRest.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (BedRestMath.Why w in Enum.GetValues(typeof(BedRestMath.Why)))
        {
            if (w == None || w == BedRestMath.Why.SeriousWound) continue;
            C(keyed.ContainsKey("CC_Rest_Why_" + w), $"the reason {w} has its words");
        }
        foreach (BedRestMath.InBed s in Enum.GetValues(typeof(BedRestMath.InBed)))
            if (s != BedRestMath.InBed.No) C(keyed.ContainsKey("CC_Rest_InBed_" + s), $"in bed {s} has its words");
        foreach (var w in new[] { "BleedingOut", "BleedsHeavily", "VitalPart", "Organ", "Deep", "LifeThreatening", "Other" })
            C(keyed.ContainsKey("CC_Rest_Wound_" + w), $"a serious wound's reason {w} has its words");
        foreach (var k in new[] { "CC_Rest_Line_Rests", "CC_Rest_Line_Heals", "CC_Rest_Line_Stage", "CC_Rest_Line_Works", "CC_Rest_Line_WorksSleeps", "CC_Rest_Line_WorksTend",
                     "CC_Rest_Line_WorksTendSleeps", "CC_Rest_Line_WorksTendLater", "CC_Rest_Line_WorksTendLaterSleeps", "CC_Rest_Line_WorksBut", "CC_Rest_Line_ByHand",
                     "CC_Rest_InBed_Other", "CC_Rest_Report", "CC_Settings_RestByNeed", "CC_Settings_RestByNeed_Tip", "CC_SetNote_RestByNeed" })
            C(keyed.ContainsKey(k), $"{k} exists");
        C(keyed["CC_Rest_Line_Works"] == "Keeps working." && keyed["CC_Rest_Line_WorksSleeps"] == "Keeps working, and sleeps a little more.", "the line says plainly that they keep working");
        C(keyed.Values.All(v => !v.Contains('—') && !v.Contains('!')), "no em dashes or exclamation marks");
        return n;
    }

    private sealed class Ailment
    {
        public string name;
        public bool immunizable, lethal;
        public float restFrom, maxSeverity = 999f;
        public List<StageRow> stages = new List<StageRow>();
    }

    private sealed class StageRow
    {
        public float min;
        public string label;
        public bool visible = true;
        public BedRestMath.Stage stage;
    }

    private static float F(XElement e, string name, float def)
    {
        var x = e?.Element(name);
        return x != null && float.TryParse(x.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;
    }

    /// <summary>Every HediffDef with Rimfolk's ailment comp, as the rule reads its stages.</summary>
    private static List<Ailment> LoadAilments(string dir)
    {
        var list = new List<Ailment>();
        foreach (var file in Directory.GetFiles(dir, "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("HediffDef"))
            {
                var comps = d.Element("comps")?.Elements("li").ToList() ?? new List<XElement>();
                var ail = comps.FirstOrDefault(li => ((string)li.Attribute("Class") ?? "").EndsWith("HediffCompProperties_Ailment"));
                if (ail == null) continue;
                var a = new Ailment { name = d.Element("defName")?.Value ?? "", restFrom = F(ail, "restFrom", 0f) };
                a.maxSeverity = F(d, "maxSeverity", (string)d.Attribute("ParentName") == "Council_AilmentBase" ? 1f : 999f);
                a.immunizable = comps.Any(li => ((string)li.Attribute("Class") ?? "").EndsWith("Immunizable") && (F(li, "immunityPerDaySick", 0f) > 0f || F(li, "immunityPerDayNotSick", 0f) > 0f));
                var stages = d.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
                a.lethal = F(d, "lethalSeverity", -1f) > 0f || stages.Any(s => (s.Element("lifeThreatening")?.Value ?? "") == "true");
                foreach (var s in stages)
                {
                    var row = new StageRow
                    {
                        min = F(s, "minSeverity", 0f), label = s.Element("label")?.Value ?? "",
                        visible = (s.Element("becomeVisible")?.Value ?? "true") != "false"
                    };
                    var st = new BedRestMath.Stage { pain = F(s, "painOffset", 0f), restFall = F(s, "restFallFactor", 1f) + F(s, "restFallFactorOffset", 0f) };
                    foreach (var m in s.Element("capMods")?.Elements("li") ?? Enumerable.Empty<XElement>())
                    {
                        float drop = BedRestMath.Drop(F(m, "offset", 0f), F(m, "postFactor", 1f), F(m, "setMax", 999f));
                        switch (m.Element("capacity")?.Value)
                        {
                            case "Consciousness": st.consciousness = Math.Max(st.consciousness, drop); break;
                            case "Moving": st.moving = Math.Max(st.moving, drop); break;
                            case "Breathing": st.breathing = Math.Max(st.breathing, drop); break;
                        }
                    }
                    row.stage = st;
                    a.stages.Add(row);
                }
                list.Add(a);
            }
        return list;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
