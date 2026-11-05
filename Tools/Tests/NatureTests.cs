using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2d.2, against their nature (docs/design/stage2-diplomacy.md, item 13; Diplomacy/NatureMath.cs;
/// the player's decision 19): the switch and the setting, the heart's new source and relief at the end of their lists, the
/// scaling as the pressures' bad side, one act a day, two acts at once, who each wired act weighs on, the marks' rules, the
/// words, and the mod's side (each act where it happens, through the heart's caps, the heart's page, the council's questions,
/// the save).
/// </summary>
public static class NatureTests
{
    private const int D = NatureMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "against their nature: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        float[] Of(PersonaAxis a, float v)
        {
            var x = new float[PersonaMath.Axes];
            x[(int)a] = v;
            return x;
        }
        var none = new float[PersonaMath.Axes];

        // ================= The switch =================

        C(NatureMath.SwitchedOn, "switched on in round d");
        C(Code("CouncilSettings.cs").Contains("public bool diplomacyNature = true;") && Code("CouncilSettings.cs").Contains("Scribe_Values.Look(ref diplomacyNature, \"diplomacyNature\", true);"),
            "the setting, saved, on by default");
        C(Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyNature\", \"CC_Settings_DiplomacyNature\", \"CC_SetNote_DiplomacyNature\", \"heart\")")
          && Regex.IsMatch(Code("UI/SettingsCatalog.cs"), @"Toggle\(""diplomacyRenown""[^\n]*\n\s*Toggle\(""diplomacyNature"""), "beside the other diplomacy toggles, needing the heart");
        string nature = Code("Diplomacy/Nature.cs");
        C(nature.Contains("public static bool On => NatureMath.SwitchedOn && (S?.diplomacyNature ?? true) && Heart.On;"), "off (or the heart off): nothing new is felt");
        C(Regex.IsMatch(nature, @"public static float Felt\(Pawn p, NatureAct\[\] acts[^)]*\)\s*\{\s*if \(!On"), "the service asks the switch before anything");

        // ================= The heart's new kinds =================

        C(Enum.GetValues(typeof(StrainSource)).Cast<StrainSource>().Last() == StrainSource.Nature && Enum.GetValues(typeof(StrainRelief)).Cast<StrainRelief>().Last() == StrainRelief.Nature,
            "a new source and a new relief, each at the end of its list (saved by position)");
        C((int)StrainSource.Nature == 16 && (int)StrainRelief.Nature == 8, "the older kinds keep their places");
        C(HeartMath.SourceCap(StrainSource.Nature) == NatureMath.MaxStrain, "the source's daily cap is one act's most");
        float today = 0f, total = 0f;
        float a1 = HeartMath.Add(12f, 1f, 1f, ref today, HeartMath.SourceCap(StrainSource.Nature), ref total, HeartMath.DailyCap);
        float a2 = HeartMath.Add(12f, 1f, 1f, ref today, HeartMath.SourceCap(StrainSource.Nature), ref total, HeartMath.DailyCap);
        C(Math.Abs(a1 - 12f) < 0.01f && a2 == 0f, "within the heart's caps: a second act that day adds nothing");
        today = 0f;
        total = HeartMath.DailyCap - 4f;
        C(Math.Abs(HeartMath.Add(12f, 1f, 1f, ref today, HeartMath.SourceCap(StrainSource.Nature), ref total, HeartMath.DailyCap) - 4f) < 0.01f,
            "and within the day's cap for everything");

        // ================= Scaled as the pressures' bad side =================

        C(Math.Abs(NatureMath.Scale(12f, LifePressure.Normal, 0f) - 12f * PressureMath.Factor(LifePressure.Normal)) < 0.01f, "normal: as written, less what the setting takes");
        C(Math.Abs(NatureMath.Scale(12f, LifePressure.Gentle, 0f) - 6f) < 0.01f && Math.Abs(NatureMath.Scale(12f, LifePressure.Demanding, 0f) - 18f) < 0.01f,
            "gentle halves it, demanding makes it half again");
        C(Math.Abs(NatureMath.Scale(12f, LifePressure.Demanding, 1f) - 18f * 0.75f) < 0.01f && NatureMath.Scale(12f, LifePressure.Normal, 0.5f) < NatureMath.Scale(12f, LifePressure.Normal, 0f),
            "hard times: down to three quarters at the break line");
        C(NatureMath.Scale(0f, LifePressure.Demanding, 0f) == 0f && NatureMath.Scale(-3f, LifePressure.Normal, 0f) == 0f, "nothing scaled to something");
        C(NatureMath.Scale(NatureMath.MaxStrain, LifePressure.Demanding, 0f) < HeartMath.BandStart(StrainBand.Strained),
            "one act alone never makes a steady colonist strained, even on demanding");
        C(Regex.IsMatch(nature, @"NatureMath\.Scale\(felt, S\?\.lifePressure \?\? LifePressure\.Normal, Pressures\.Hard\(p\)\)"), "the game's side scales by the setting and how near breaking they are");

        // ================= One act a day =================

        C(NatureMath.MayFeel(-1, 0) && !NatureMath.MayFeel(5 * D, 6 * D - 1) && NatureMath.MayFeel(5 * D, 6 * D), "one act a day per colonist");
        C(NatureMath.MayFeel(5 * D, 4 * D), "a last act from a later time (a reload past it) never blocks");
        C(nature.Contains("if (!NatureMath.MayFeel(h.natureTick, now)) return 0f;") && nature.Contains("h.natureTick = now;"), "the last act kept on the heart, asked first");

        // ================= What each act brings =================

        foreach (NatureAct a in Enum.GetValues(typeof(NatureAct))) C(NatureMath.Rules.Any(r => r.act == a), $"{a} goes against someone's nature");
        C(Enum.GetValues(typeof(NatureAct)).Cast<NatureAct>().Count(a => !NatureMath.Wired(a)) == 1 && !NatureMath.Wired(NatureAct.DarkScheme), "every act wired but the dark scheme");
        string all = string.Concat(Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.EndsWith("NatureMath.cs")).Select(File.ReadAllText));
        C(!all.Contains("NatureAct.DarkScheme"), "the dark scheme waits for schemes (release 2c.3) and the player's harm pick");
        C(NatureMath.Felt(NatureAct.SworeFealty, Of(PersonaAxis.Pride, 29f)) == 0f && NatureMath.Felt(NatureAct.SworeFealty, Of(PersonaAxis.Pride, 31f)) > 0f,
            "an oath: the proud, from where pride shows");
        C(Math.Abs(NatureMath.Strain(NatureAct.SworeFealty, Of(PersonaAxis.Pride, 31f)) - NatureMath.MaxStrain * NatureMath.Floor) < 0.01f
          && Math.Abs(NatureMath.Strain(NatureAct.SworeFealty, Of(PersonaAxis.Pride, 100f)) - NatureMath.MaxStrain) < 0.01f, "about 5 just past it, 12 at the far end");
        C(NatureMath.Strain(NatureAct.BrokeTruce, Of(PersonaAxis.Trust, 60f)) > 0f && NatureMath.Strain(NatureAct.BrokeTruce, Of(PersonaAxis.Trust, -60f)) == 0f, "a broken word: the trusting");
        C(NatureMath.BreakerFeels(true, true) && !NatureMath.BreakerFeels(false, true) && !NatureMath.BreakerFeels(true, false),
            "whoever the game names for the deed that broke it, one of the colony's; nobody for the whole colony's deed");
        C(NatureMath.Strain(NatureAct.GivenHostage, Of(PersonaAxis.Nerve, -50f)) > 0f && NatureMath.Strain(NatureAct.GivenHostage, Of(PersonaAxis.Nerve, 50f)) == 0f, "a hostage: the timid");
        C(NatureMath.Strain(NatureAct.KinHostage, Of(PersonaAxis.Warmth, 50f)) > 0f && NatureMath.Strain(NatureAct.KinHostage, none) == 0f, "a hostage's kin: the warm");
        C(new[] { "Parent", "Child", "Sibling", "Spouse", "Fiance", "Lover" }.All(NatureMath.CloseKin) && !NatureMath.CloseKin("Cousin") && !NatureMath.CloseKin("Kin")
          && !NatureMath.CloseKin("ExSpouse") && !NatureMath.CloseKin(null), "close kin: parents, children, brothers and sisters, a spouse, a betrothed, a lover");
        C(NatureMath.WardParent("Parent") && !NatureMath.WardParent("Child") && !NatureMath.WardParent("Sibling"), "a ward sent away: the child's parents");
        C(NatureMath.Strain(NatureAct.WardSent, Of(PersonaAxis.Warmth, 50f)) > 0f && NatureMath.Strain(NatureAct.WardSent, none) == 0f, "and only the warm of them");
        C(NatureMath.Strain(NatureAct.MatchAgainstHeart, none) >= NatureMath.AlwaysStrain && NatureMath.Strain(NatureAct.MatchAgainstHeart, Of(PersonaAxis.Warmth, -90f)) >= NatureMath.AlwaysStrain
          && NatureMath.Strain(NatureAct.MatchAgainstHeart, Of(PersonaAxis.Warmth, 90f)) > NatureMath.Strain(NatureAct.MatchAgainstHeart, none), "a match pressed: anyone, the warm more");
        C(NatureMath.Strain(NatureAct.RefusedChallenge, Of(PersonaAxis.Temper, 60f)) > 0f && NatureMath.Strain(NatureAct.RefusedChallenge, Of(PersonaAxis.Pride, 60f)) > 0f
          && NatureMath.Strain(NatureAct.RefusedChallenge, Of(PersonaAxis.Nerve, -60f)) == 0f, "a challenge turned down: the hot-tempered and the proud");
        var both = new float[PersonaMath.Axes];
        both[(int)PersonaAxis.Temper] = 50f;
        both[(int)PersonaAxis.Pride] = 90f;
        C(NatureMath.RuleOf(NatureAct.RefusedChallenge, both, out var worse) && worse.axis == PersonaAxis.Pride
          && Math.Abs(NatureMath.Strain(NatureAct.RefusedChallenge, both) - NatureMath.Strain(NatureAct.RefusedChallenge, Of(PersonaAxis.Pride, 90f))) < 0.01f,
            "an act against two sides counts the worse, and its words name that side");
        C(NatureMath.Strain(NatureAct.AcceptedChallenge, Of(PersonaAxis.Nerve, -50f)) > 0f && NatureMath.Strain(NatureAct.AcceptedChallenge, Of(PersonaAxis.Nerve, 50f)) == 0f,
            "a challenge faced: the timid");
        C(NatureMath.Strain(NatureAct.SeasonAway, Of(PersonaAxis.Openness, -60f)) > 0f && NatureMath.Felt(NatureAct.SeasonAway, Of(PersonaAxis.Openness, 60f)) < 0f
          && NatureMath.Relief(NatureAct.SeasonAway, Of(PersonaAxis.Openness, 100f)) <= NatureMath.MaxRelief, "a season away: the set in their ways strained, the curious eased, 6 at most");
        C(NatureMath.Strain(NatureAct.HaggledForKin, Of(PersonaAxis.Warmth, 50f)) > 0f && NatureMath.Strain(NatureAct.HaggledForKin, none) == 0f, "haggling over a captive: the warm");
        C(NatureMath.Strain(NatureAct.TruceWithFoe, none) >= NatureMath.AlwaysStrain, "a truce with one's foe: the avenger, always");
        // Two acts at once: one act a day, the one that weighs most.
        var timidOpen = new float[PersonaMath.Axes];
        timidOpen[(int)PersonaAxis.Nerve] = -70f;
        timidOpen[(int)PersonaAxis.Openness] = 60f;
        var hostage = new[] { NatureAct.GivenHostage, NatureAct.SeasonAway };
        C(NatureMath.Pick(hostage, timidOpen, out var w1) > 0f && w1 == NatureAct.GivenHostage, "a timid hostage who likes the new: the hostage weighs, the season doesn't ease it");
        var closed = Of(PersonaAxis.Openness, -80f);
        C(NatureMath.Pick(hostage, closed, out var w2) > 0f && w2 == NatureAct.SeasonAway, "a hostage set in their ways: the season away weighs");
        C(NatureMath.Pick(hostage, Of(PersonaAxis.Openness, 70f), out var w3) < 0f && w3 == NatureAct.SeasonAway, "a bold, curious hostage: eased");
        C(NatureMath.Pick(hostage, none, out _) == 0f, "an ordinary hostage: nothing");

        // ================= The marks' rules =================

        C(NatureMath.MarkFamily(NatureAct.SworeFealty, true, -1, -1, 10 * D) == NatureMath.MarkOath, "an oath sworn by a proud one may humble them");
        C(NatureMath.MarkFamily(NatureAct.SworeFealty, false, -1, -1, 10 * D) == null, "only an act that went against them");
        int brk = (int)NatureAct.BrokeTruce;
        C(NatureMath.MarkFamily(NatureAct.BrokeTruce, true, -1, -1, 10 * D) == null, "a word broken once leaves no mark");
        C(NatureMath.MarkFamily(NatureAct.BrokeTruce, true, brk, 10 * D, 40 * D) == NatureMath.MarkWordBroken, "twice within a year: warier");
        C(NatureMath.MarkFamily(NatureAct.BrokeTruce, true, brk, 10 * D, 71 * D) == null, "twice, but a year apart: no mark");
        C(NatureMath.MarkFamily(NatureAct.BrokeTruce, true, (int)NatureAct.SeasonAway, 10 * D, 20 * D) == null, "another act between: not the same act twice");
        C(NatureMath.MarkFamily(NatureAct.AcceptedChallenge, true, (int)NatureAct.AcceptedChallenge, 0, 30 * D) == NatureMath.MarkNerve
          && NatureMath.MarkFamily(NatureAct.SeasonAway, true, (int)NatureAct.SeasonAway, 0, 30 * D) == NatureMath.MarkAbroad, "a challenge faced twice, a second season away");
        C(NatureMath.MarkFamily(NatureAct.HaggledForKin, true, (int)NatureAct.HaggledForKin, 0, 5 * D) == null
          && NatureMath.MarkFamily(NatureAct.MatchAgainstHeart, true, (int)NatureAct.MatchAgainstHeart, 0, 5 * D) == null, "other acts leave no mark");
        var marks = XDocument.Load(Path.Combine(root, "Defs", "PersonaDefs", "Rimfolk_PersonaMarks_Nature.xml")).Root!.Elements("ColonyCouncil.PersonaMarkDef").ToList();
        XElement Fam(string f) => marks.FirstOrDefault(m => m.Descendants("family").Any(x => x.Value == f));
        float Shift(XElement m, string axis) => float.TryParse(m?.Element("shifts")?.Element(axis)?.Value, out float v) ? v : 0f;
        float Chance(XElement m) => float.TryParse(m?.Descendants("chance").FirstOrDefault()?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 1f;
        C(new[] { NatureMath.MarkOath, NatureMath.MarkWordBroken, NatureMath.MarkNerve, NatureMath.MarkAbroad }.All(f => Fam(f) != null) && marks.Count == 4,
            "each family has its mark, and nothing else");
        C(Shift(Fam(NatureMath.MarkOath), "pride") < 0f && Shift(Fam(NatureMath.MarkWordBroken), "trust") < 0f && Shift(Fam(NatureMath.MarkNerve), "nerve") > 0f
          && Shift(Fam(NatureMath.MarkAbroad), "openness") > 0f, "each moves the side the act went against: humbled, warier, bolder, more open");
        C(marks.All(m => Chance(m) > 0f && Chance(m) < 1f) && marks.All(m => m.Descendants("when").All(w => string.IsNullOrWhiteSpace(w.Value))),
            "rare: each has its chance; the act already asked their nature");
        C(Fam(NatureMath.MarkOath)?.Element("major")?.Value == "true" && marks.All(m => !m.Descendants("news").First().Value.Contains('!')), "an oath is a turning point; each is told");
        C(nature.Contains("PersonaMarks.Give(p, family, null,") && nature.Contains("int lastAct = h.natureAct, lastTick = h.natureTick;"),
            "given through the persona's own marks, read against the act before this one");

        // ================= The words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Nature.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (NatureAct a in Enum.GetValues(typeof(NatureAct)))
            if (NatureMath.Wired(a))
                C(keyed.ContainsKey(NatureMath.ActKey(a)), $"{a} has its words");
        foreach (var role in new[] { (NatureAct.SeasonAway, "Envoy"), (NatureAct.SeasonAway, "Hostage"), (NatureAct.HaggledForKin, "Kin"), (NatureAct.WardSent, "Page") })
            C(keyed.ContainsKey(NatureMath.ActKey(role.Item1, role.Item2)), $"{role.Item1} has its words for the {role.Item2.ToLowerInvariant()}");
        foreach (var k in NatureMath.SideKeys()) C(keyed.ContainsKey(k), $"the side {k} has its words");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Nature_Side_")).All(kv => kv.Value.Contains("{PAWN_possessive}") || kv.Value.Contains("{PAWN_pronoun}")),
            "the sides say whose by the game's own pronouns, never a guessed one");
        C(!keyed.Values.Any(v => Regex.IsMatch(v, @"\b(his|her|hers|him|he|she)\b", RegexOptions.IgnoreCase)), "no pronoun guessed anywhere");
        C(keyed.ContainsKey("CC_Nature_Told_2") && keyed.ContainsKey("CC_Nature_Told_Eased_2") && nature.Contains("DipWords.T(eased ? \"CC_Nature_Told_Eased\" : \"CC_Nature_Told\""),
            "told in the news in two wordings or more, by Tell");
        C(nature.Contains("Tell.Minor(NewsKind.People") && !nature.Contains("Messages.Message("), "told through Tell only");
        C(!keyed.Values.Any(v => v.Contains('—') || v.Contains('!')), "no em dashes or exclamation marks");
        C(new[] { "CC_Settings_DiplomacyNature", "CC_SetNote_DiplomacyNature", "CC_Settings_DiplomacyNature_Tip", "CC_Heart_Source_Nature", "CC_Heart_Relief_Nature", "CC_Heart_Help_Nature",
            "CC_Heart_NatureHelp", "CC_Nature_Why", "CC_Nature_WhyEased" }.All(keyed.ContainsKey), "the setting's and the heart's words");
        C(keyed["CC_Heart_NatureHelp"].Contains("quiet evening with friends"), "what eases it, the design's own words");
        var acts = keyed.Where(kv => kv.Key.StartsWith("CC_Nature_Act_")).Select(kv => kv.Value).ToList();
        C(acts.All(v => !Regex.IsMatch(v, @"\b(knelt|kneel|sword|radio|comms)\b")), "every age: the act's words name no age's tools (the nature is the person's)");

        // ================= Where each act happens =================

        C(Code("Diplomacy/Fealty.cs").Contains("Nature.Felt(speaker, NatureAct.SworeFealty, f)"), "the oath: the speaker who swears it at the gate (Fealty.Swear, from the oath's job)");
        C(Regex.IsMatch(Code("Diplomacy/FealtyOath.cs"), @"Fealty\.Swear\(o\.people, pawn\.Map, pawn, l\)"), "the oath's job passes the one who walked out");
        C(Code("Diplomacy/Matches.cs").Contains("Nature.Felt(p.ours, NatureAct.MatchAgainstHeart, p.people"), "a match: the colonist, when the council presses it over the objection");
        string hooks = Code("Diplomacy/PactsHooks.cs");
        C(hooks.Contains("var by = MemoryHooks.Doer;") && hooks.Contains("Nature.Felt(by, NatureAct.BrokeTruce, f)") && hooks.Contains("broke |= Truces.BrokenByUs(f, how)")
          && hooks.Contains("broke |= Pacts.BrokenByUs(f, how)") && hooks.Contains("broke |= Fealty.BrokenByUs(f, how)"), "a broken truce, pact or oath: the one the game names for the deed");
        string mem = Code("World/Patch_FactionMemory.cs");
        C(mem.Contains("By(damageBy, () => FactionMemory.Note(f, \"attacked\", name))") && mem.Contains("By(capturer, () => FactionMemory.Note(f, \"captured\", name))")
          && mem.Contains("By(TradeSession.Active ? TradeSession.playerNegotiator : null") && mem.Contains("By(LookPawn(look), () => FactionMemory.Note(f, \"caravanRaided\"))")
          && mem.Contains("By(hand, () => FactionMemory.Note(f, \"killed\""), "the doer: the blow's hand, the captor, the seller, the caravan's first, the killer");
        C(mem.Contains("[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberTookDamage))]") && mem.Contains("[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]")
          && Regex.IsMatch(mem, @"Patch_MemoryDamageBy[\s\S]{0,400}Finalizer\(\)\s*\{\s*MemoryHooks\.damageBy = null;"), "kept only while the game weighs the deed");
        // An arrest is weighed as a capture in Pawn.CheckAcceptArrest, before CapturedBy (whose own weighing then breaks nothing).
        C(mem.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.CheckAcceptArrest))]")
          && Regex.IsMatch(mem, @"Patch_MemoryArrestedBy\s*\{\s*public static void Prefix\(Pawn arrester\)\s*\{\s*if \(MemoryHooks\.Listening\) MemoryHooks\.capturer = arrester;\s*\}\s*public static void Finalizer\(\)\s*\{\s*MemoryHooks\.capturer = null;"),
            "an arrest names the arrester as the captor, kept only while the game weighs it");
        C(Code("Diplomacy/CallsToArms.cs").Contains("&& !unanswered && home != null)") && Code("Diplomacy/CallsToArms.cs").Contains("Nature.Felt(TruceParley.Speaker(home), NatureAct.BrokeTruce, f)"),
            "a pact's call refused by the council's answer: the speaker who sends it; unanswered (or no home left), nobody");
        string abroad = Code("Diplomacy/WardsAbroad.cs");
        C(Regex.IsMatch(abroad, @"if \(w\.Hostage\) Nature\.HostageGoes\(p, w\.people\);\s*else Nature\.WardGoes\(p, w\.people, w\.page\);\s*try\s*\{\s*var c = CaravanExitMapUtility"),
            "a hostage and a ward: as they walk off the map");
        C(nature.Contains("Felt(hostage, new[] { NatureAct.GivenHostage, NatureAct.SeasonAway }, people, null, \"Hostage\")") && nature.Contains("Felt(kin, NatureAct.KinHostage, people, hostage.LabelShort)")
          && nature.Contains("Felt(parent, NatureAct.WardSent, people, ward.LabelShort, page ? \"Page\" : null)"), "the hostage (and a season away, one act), their close kin, a ward's parents");
        C(Regex.IsMatch(Code("Diplomacy/EnvoysAbroad.cs"), @"Nature\.Felt\(p, NatureAct\.SeasonAway, a\.people, null, ""Envoy""\);\s*try\s*\{\s*var c = CaravanExitMapUtility"), "the envoy: as they walk off the map");
        C(Regex.IsMatch(Code("Happenings/Workers_Challenges.cs"), @"private static void Contest\([\s\S]{0,1400}Nature\.Felt\(a, NatureAct\.AcceptedChallenge"), "a challenge faced: the champion, at the contest itself");
        C(Code("Strangers/StrangerEffects.cs").Contains("Nature.Felt(champion, NatureAct.RefusedChallenge, ctx.faction") && Code("Strangers/StrangerEffects.cs").Contains("h.contest ? h.a : null"),
            "a challenge turned down: the champion, when the council sends the challenger away");
        C(Regex.IsMatch(Code("Diplomacy/Ransoms.cs"), @"public static void AskMore\([\s\S]{0,700}Nature\.Felt\(speaker, NatureAct\.HaggledForKin, o\.people\);[\s\S]{0,300}Nature\.CloseKinOf\(o\.captive, map\)"),
            "asking more for a captive: the speaker, and the captive's close kin among the colony's");
        C(Code("Diplomacy/Truces.cs").Contains("Nature.TruceWithFoe(f, home);") && nature.Contains("!v.Active || v.faction != people"),
            "a truce sworn: the avengers of this home (the vendettas)");
        // Close kin: a child and a brother or sister are the game's implied relations, never in DirectRelations.
        C(nature.Contains("p.GetRelations(o).Any(r => NatureMath.CloseKin(r?.defName))") && !Regex.IsMatch(nature, @"CloseKinOf\([^)]*\)\s*\{[\s\S]{0,400}DirectRelations"),
            "close kin read through every relation the game knows, so a child and a brother or sister count");
        // The words that say they saw or heard it: only those there on the map (Nature.There).
        var seenWords = keyed.Where(kv => kv.Key.StartsWith("CC_Nature_Act_") && Regex.IsMatch(kv.Value, @"\b(saw|heard)\b")).Select(kv => kv.Key.Substring("CC_Nature_Act_".Length)).OrderBy(k => k).ToList();
        C(seenWords.SequenceEqual(new[] { "HaggledForKin_Kin", "KinHostage", "RefusedChallenge", "TruceWithFoe", "WardSent", "WardSent_Page" }), "the acts whose words say they saw or heard it: " + string.Join(", ", seenWords));
        C(nature.Contains("CloseKinOf(hostage, hostage.MapHeld)") && nature.Contains("ParentsOf(ward, ward.MapHeld)") && Regex.IsMatch(nature, @"ParentsOf\([^)]*\)\s*\{[\s\S]{0,400}!There\(o, map\)")
          && Regex.IsMatch(nature, @"CloseKinOf\([^)]*\)\s*\{[\s\S]{0,300}map\.mapPawns\.FreeColonistsSpawned") && nature.Contains("!There(v.avenger, home)")
          && nature.Contains("public static bool There(Pawn p, Map map) => p != null && map != null && p.Spawned && p.Map == map;"),
            "a hostage's kin, a ward's parents, the captive's kin and the avenger feel it only when there to see it");
        string strangers = Code("Strangers/StrangerEffects.cs");
        C(strangers.Contains("h.contest && Nature.There(h.a, ctx.map) ? h.a : null")
          && Regex.IsMatch(strangers, @"x\.Faction\.IsPlayer \|\| x\.IsPrisoner\) return;\s*(//[^\n]*\n\s*)*if \(Challenge\(ctx\) is Pawn champion\) Nature\.Felt\("),
            "a challenge turned down weighs only with the challenger still here to be sent away, as the answer's sheet says, and the champion there");
        C(Code("Diplomacy/WardsAbroad.cs").Contains("Nature.WardGoes(p, w.people, w.page)") && keyed["CC_Nature_Act_WardSent_Page"].Contains("page"),
            "a page sent to a titled household is said as a page, not a ward");

        // ================= Through the heart =================

        C(nature.Contains("HeartLogic.Build(p, StrainSource.Nature,") && nature.Contains("HeartLogic.Relieve(p, -felt, StrainRelief.Nature)"), "strain and relief through the heart, under their own kinds");
        string logic = Code("Heart/HeartLogic.cs");
        C(Regex.IsMatch(logic, @"public static float Build\(Pawn p, StrainSource source, float raw\)[\s\S]{0,900}HeartMath\.Add\(raw, speed, HeartMath\.TraitFactor\(source"),
            "built as the hour builds: the strength setting, their traits, the source's cap and the day's");
        C(Regex.IsMatch(logic, @"public static float Relieve\([\s\S]{0,500}HeartMath\.ReliefTraitFactor"), "eased by their nature's way of letting go");
        C(!nature.Contains("ThoughtMaker") && !nature.Contains("TryGainMemory"), "no mood of its own (decision 19)");
        string data = Code("Heart/HeartData.cs");
        C(new[] { "natureTick", "natureAct", "natureEased", "natureWhy" }.All(f => data.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\"")),
            "the last act kept on the heart: values, not a new list");
        string view = Code("Heart/HeartView.cs");
        C(view.Contains("SourceText(h, s.source)") && view.Contains("ReliefText(h, x.r)") && view.Contains("case StrainSource.Nature: return \"CC_Heart_Help_Nature\";")
          && view.Contains(": NatureHelp(h);"), "the heart's page says what they did, against what, and what would help");
        C(Regex.IsMatch(view, @"public static string SourcesText\([^)]*\)\s*\{[^}]*SourceLabel\(s\.source\)") && !Regex.IsMatch(view, @"public static string SourcesText\([^)]*\)\s*\{[^}]*SourceText\(h,"),
            "the mood's \"built up by\" and the episode's letter name the source, never the act's words in their sentence");

        // ================= The council's questions =================

        C(Code("Diplomacy/FealtyObjection.cs").Contains("Nature.Impact(sheet, TruceParley.Speaker(ctx.map), NatureAct.SworeFealty"), "the oath's question names the speaker's pride");
        C(Code("Diplomacy/TruceObjection.cs").Contains("Nature.Impact(sheet, c.a, NatureAct.TruceWithFoe"), "the truce's question names the avenger's vengeance");
        C(Code("Diplomacy/MatchObjection.cs").Contains("Nature.Impact(sheet, c.a, NatureAct.MatchAgainstHeart"), "the match's question names the colonist's heart");
        C(Code("Happenings/Workers_Challenges.cs").Contains("Nature.Impact(sheet, ctx.a, NatureAct.AcceptedChallenge") && Code("Strangers/StrangerEffects.cs").Contains("Nature.ImpactRefused(sheet, champion)"),
            "the challenge's answers name the champion's nature");
        string fealtyDlm = File.ReadAllText(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_Fealty.xml"));
        C(fealtyDlm.Contains("[proud] {A_nameDef} is too proud to kneel to anyone"), "the proud objection names the nature, the design's own words");

        // ================= Elsewhere =================

        C(File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Diplomacy.xml")).Contains("Against their nature."), "the Almanac's diplomacy page");
        C(File.ReadAllText(Path.Combine(root, "Tools", "DefValidator", "Program.cs")).Contains("[\"CC_Nature_Side_\"]"), "the def validator knows the words' keys");
        C(Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Balance.csproj")), "NatureMath\\.cs"), "the balance simulator models it (--nature)");
        return n;
    }
}
