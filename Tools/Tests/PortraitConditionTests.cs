using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The painted conditions and the people list (the player, 25 November 2026): a picture's versions of what a person goes
/// through (asleep, tired, hurt, wounded, ill, freezing, then overheated, starving, drunk, breaking, unconscious; the eleven
/// words Rimfolk Studio 0.13.0 makes), shown in its place while one holds, the most serious first, no blinking or
/// expression over it, its drawn effect toned down; wanted for the colony's own people, the instruction left to the Studio. And people.csv: what the game sees in each
/// person, what their picture does not show, and what changed since a save before.
/// </summary>
public static class PortraitConditionTests
{
    private static PortraitRecord Picture(string name, string? variant = null, string? variantOf = null, string age = "adult", string? looks = null, string? role = null,
                                          string? marks = null)
    {
        var f = new Dictionary<string, string?>
        {
            ["texPath"] = "x/" + name, ["sex"] = "male", ["age"] = age, ["skinColor"] = "#c9a27e", ["hairColor"] = "#2a2a2a", ["variant"] = variant, ["variantOf"] = variantOf
        };
        var lists = new Dictionary<string, List<string>?> { ["marks"] = marks?.Split(',').ToList() };
        var r = PortraitLibraryMath.Record(name, k => f.TryGetValue(k, out var v) ? v : null, k => lists.TryGetValue(k, out var v) ? v : null, new List<string>());
        // The looks and the work as the records' words would give them (the look book and the roles aren't read here).
        foreach (var w in looks?.Split(',') ?? new string[0]) r.looks.Add(w);
        r.role = role ?? "";
        return r;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "painted conditions and the people list: " + what);
        }
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        // ---------------- The words ----------------
        var conds = PortraitLifeMath.Conditions;
        C(conds.SequenceEqual(new[] { "asleep", "tired", "hurt", "wounded", "ill", "freezing", "overheated", "starving", "drunk", "breaking", "unconscious" }),
            "the eleven words exactly as Rimfolk Studio names its files and records, the first six first");
        C(PortraitLifeMath.ConditionOrder.OrderBy(x => x).SequenceEqual(conds.OrderBy(x => x)) && PortraitLifeMath.ConditionOrder[0] == "unconscious" && PortraitLifeMath.ConditionOrder.Last() == "tired",
            "the order when several hold has every condition once, unconscious first and tired last");
        C(!conds.Any(c => PortraitLifeMath.Faces.Contains(c) || PortraitLibraryMath.MarkVariantWords.Contains(c)), "a condition is neither an expression nor a mark (the Studio tells them apart so)");
        var words = PortraitVariantsMath.Words.ToList();
        C(conds.All(words.Contains) && words.IndexOf("asleep") > words.IndexOf(PortraitLibraryMath.MarkVariantWords.Last()) && words.Skip(words.Count - conds.Length).SequenceEqual(conds),
            "the conditions are a list of their own in the words, after the expressions and the marks");
        C(conds.All(c => PortraitVariantsMath.Instruction(c) == null) && PortraitVariantsMath.Instruction("blink") != null,
            "no instruction for a condition: the Studio's script words them itself");
        C(PortraitLifeMath.ConditionWord("Asleep") == "asleep" && PortraitLifeMath.ConditionWord(" WOUNDED ") == "wounded" && PortraitLifeMath.ConditionWord("smile") == null,
            "a condition's word read the case aside; an expression is none");

        // ---------------- When each holds ----------------
        var calm = PortraitCondition.Calm;
        C(PortraitLifeMath.ConditionsNow(calm).Count == 0, "someone whole and at ease: none");
        var sleep = calm;
        sleep.asleep = true;
        C(PortraitLifeMath.ConditionsNow(sleep).SequenceEqual(new[] { "asleep" }), "asleep while sleeping");
        var down = calm;
        down.unconscious = true;
        down.asleep = true;
        C(PortraitLifeMath.ConditionsNow(down).SequenceEqual(new[] { "unconscious" }), "unconscious, not asleep");
        var downed = calm;
        downed.downed = true;
        C(PortraitLifeMath.ConditionsNow(downed).SequenceEqual(new[] { "unconscious" }), "unconscious when downed");
        var cut = calm;
        cut.bleed = 0.05f;
        C(PortraitLifeMath.ConditionHolds("hurt", cut) && !PortraitLifeMath.ConditionHolds("wounded", cut), "bleeding a little: hurt, not wounded");
        cut.bloodLoss = 0.25f;
        C(PortraitLifeMath.ConditionHolds("wounded", cut), "a fair amount of blood lost: wounded");
        var tired = calm;
        tired.rest = 0.1f;
        C(PortraitLifeMath.ConditionHolds("tired", tired) && !PortraitLifeMath.ConditionHolds("tired", sleep), "very tired while awake; asleep is no longer tired");
        var cold = calm;
        cold.cold = 0.3f;
        var dead = cold;
        dead.dead = true;
        C(PortraitLifeMath.ConditionHolds("freezing", cold) && PortraitLifeMath.ConditionsNow(dead).Count == 0, "freezing by hypothermia; the dead none");
        var high = calm;
        high.high = true;
        var drunk = calm;
        drunk.drunk = 0.5f;
        C(PortraitLifeMath.ConditionHolds("drunk", high) && PortraitLifeMath.ConditionHolds("drunk", drunk) && !PortraitLifeMath.ConditionHolds("drunk", calm), "drunk by alcohol or a drug's high");
        var low = calm;
        low.mood = 0.15f;
        var edge = calm;
        edge.mood = 0.3f;
        var broken = calm;
        broken.breaking = true;
        C(PortraitLifeMath.ConditionHolds("breaking", low) && !PortraitLifeMath.ConditionHolds("breaking", edge) && PortraitLifeMath.ConditionHolds("breaking", broken),
            "breaking near a break (under the middle of the break lines) and in one; only on edge, not yet");

        // ---------------- Which shows ----------------
        var both = sleep;
        both.bloodLoss = 0.3f;
        C(PortraitLifeMath.ConditionWanted(both, _ => true) == "wounded", "wounded and asleep in a hospital bed: wounded (the most serious)");
        C(PortraitLifeMath.ConditionWanted(both, w => w == "asleep") == "asleep", "wounded not painted yet: asleep, which is");
        C(PortraitLifeMath.ConditionWanted(both, _ => false) == "", "none painted: the picture itself");
        C(PortraitLifeMath.ConditionStep("", "asleep", 0f, 0.1f) == "asleep" && PortraitLifeMath.ConditionStep("asleep", "wounded", 0f, 0.1f) == "wounded",
            "the first one and a more serious one at once");
        C(PortraitLifeMath.ConditionStep("wounded", "tired", 10f, 11f) == "wounded" && PortraitLifeMath.ConditionStep("wounded", "", 10f, 10f + PortraitLifeMath.ConditionHold) == "",
            "a lighter one or none only after the one shown has stayed a few seconds");

        // ---------------- The drawn effect over a painted one ----------------
        var lv = new PortraitEffectLevels { bleed = 1f, hurt = 1f, cold = 1f };
        var damped = PortraitEffectsMath.Damp(lv, "wounded");
        C(Math.Abs(damped.bleed - PortraitEffectsMath.PaintedShare) < 0.001f && Math.Abs(damped.hurt - PortraitEffectsMath.PaintedShare) < 0.001f && damped.cold == 1f,
            "over a painted wound, the bleeding and the hurt drawn faint; the frost as it is");
        C(Math.Abs(PortraitEffectsMath.Damp(lv, "freezing").cold - PortraitEffectsMath.PaintedShare) < 0.001f && PortraitEffectsMath.Damp(lv, "").bleed == 1f,
            "over painted frost, the frost faint; nothing painted, nothing changed");

        // ---------------- The records and the wanted list ----------------
        var asleep = Picture("P_01_v-asleep", "asleep", "P_01");
        var eyeAsleep = Picture("P_01_v-lostEyeLeft_v-asleep", "Asleep", "P_01_v-lostEyeLeft");
        C(asleep.IsExpression && !asleep.IsMarkVariant && asleep.variant == "asleep" && eyeAsleep.variant == "asleep", "a condition's record: a version of its picture by its word, the case aside");
        var records = new List<PortraitRecord> { Picture("P_01"), asleep, Picture("P_01_v-blink", "blink", "P_01"), Picture("P_01_v-lostEyeLeft", "lostEyeLeft", "P_01"), eyeAsleep };
        var faces = new Dictionary<string, Dictionary<string, int>>();
        var own = PortraitLibraryMath.SplitExpressions(records, faces, null);
        C(own.Select(i => records[i].name).SequenceEqual(new[] { "P_01", "P_01_v-lostEyeLeft" }) && faces["P_01"].ContainsKey("asleep") && faces["P_01"].ContainsKey("blink")
          && faces["P_01_v-lostEyeLeft"].ContainsKey("asleep"), "a condition belongs to its picture, a mark variant's to the variant: never picked on its own");
        C(PortraitVariantsMath.ConditionsWanted(new[] { "asleep", "blink" }).SequenceEqual(conds.Where(c => c != "asleep")), "the conditions a picture lacks, in the list's order");
        string lib = Code("UI/Faces/PortraitLibrary.cs");
        C(lib.Contains("public PortraitPicture ConditionPicture(string picture, string word)") && lib.Contains("PortraitLifeMath.ConditionWord(x.Key) == null")
          && lib.Contains("public IReadOnlyDictionary<string, PictureFile> ConditionsOf(string picture)"), "the library keeps the conditions apart from the expressions");
        string variants = Code("UI/Faces/PortraitVariants.cs");
        int notableAt = variants.IndexOf("if (rank == Notable) return list;", StringComparison.Ordinal), condAt = variants.IndexOf("PortraitVariantsMath.ConditionsWanted(", StringComparison.Ordinal);
        C(notableAt > 0 && condAt > notableAt, "the conditions are wanted for the colony's own people (not the notables)");
        string engine = Code("UI/Faces/FaceEngine.cs"), stage = Code("UI/Faces/FaceEngineStage.cs"), sources = Code("UI/Faces/PortraitSources.cs");
        C(stage.Contains("PortraitSources.Shown(p, own, true)") && stage.Contains("PortraitSources.Shown(p, PortraitSources.ForStage(p), true)")
          && engine.Contains("PortraitSources.Shown(p, PortraitSources.For(p), moving)") && engine.Contains("PortraitSources.Shown(p, PortraitSources.For(p), false)"),
            "the large portrait, the colonist card and the round portraits draw the condition in its place");
        C(sources.Contains("!PortraitEffects.On(large)") && Code("UI/Faces/PortraitDelete.cs").Contains("PortraitSources.ForStage(p)") && !Code("UI/Faces/PortraitDelete.cs").Contains("PortraitSources.Shown("),
            "only where the setting shows what they go through; deleting takes the picture itself");
        C(Code("UI/Faces/FaceEngineAlive.cs").Contains("PortraitLibrary.Source.ExpressionsOf(s.picture)"),
            "the expressions are looked up by the picture drawn: a condition's has none, so no blink and no expression over it");
        string effects = Code("UI/Faces/PortraitEffects.cs");
        C(effects.Contains("PortraitEffectsMath.Damp(e, held.painted)") && effects.Contains("c.downed = health.Downed;"), "the drawn effect toned down over a painted one; downed read from the game");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        C(conds.All(c => keys.Contains("<CC_PortraitAbout_Cond_" + c + ">") && keys.Contains("<CC_PortraitAbout_CondWhat_" + c + ">") && keys.Contains("<CC_PortraitAbout_CondWhen_" + c + ">")),
            "each condition has its name, what it shows and when, for the window about a picture");

        // ---------------- The people list: what the picture does not show ----------------
        var look = new PortraitLook { sex = PortraitSex.Male, age = PortraitAge.Middle, role = "mage" };
        look.AddLook("drinker", false);
        look.AddLook("albino", true);
        look.AddMark("lostEye", PortraitSide.Left);
        look.AddMark("scar");
        var held = Picture("P_01", age: "adult", looks: "drinker", role: "soldier");
        var heldEye = Picture("P_01_v-lostEyeLeft", "lostEyeLeft", "P_01", marks: "lostEye");
        var lacks = PortraitPeopleMath.Lacks(look, held, heldEye);
        C(lacks.SequenceEqual(new[] { "age:middle", "look:albino", "role:mage", "mark:scar" }),
            "what the picture does not show: another age band, a look (the special first), their work, a mark neither version shows (" + string.Join("; ", lacks) + ")");
        C(PortraitPeopleMath.Lacks(look, held, held).Contains("mark:lostEye-left"), "a mark on its side where no version shows it");
        C(PortraitPeopleMath.Lacks(look, null, null).Count == 6, "no picture: everything");
        C(PortraitPeopleMath.MarkWord("eyePatch", PortraitSide.Right) == "lostEye-right" && PortraitPeopleMath.MarkWord("scar", PortraitSide.Left) == "scar",
            "a mark as the list writes it: its side only where it has one");

        // ---------------- What changed ----------------
        PersonSeen Seen(string age, string looks, string marks, string role, string title, string lacking, string changed = "") => new PersonSeen
        {
            id = "42", name = "Myst", age = age, looks = looks, marks = marks, role = role, title = title, lacks = lacking, changed = changed
        };
        var before = Seen("adult", "drinker", "", "soldier", "", "");
        var now = Seen("middle", "drinker; albino", "scar", "mage", "knight", "age:middle; look:albino; mark:scar; role:mage");
        var ch = PortraitPeopleMath.Changes(before, now, "2026-11-25");
        C(ch.SequenceEqual(new[] { "age:middle@2026-11-25", "look:albino@2026-11-25", "mark:scar@2026-11-25", "role:mage@2026-11-25", "title:knight@2026-11-25" }),
            "a new age band, look, mark, role and title, dated the day first seen (" + string.Join("; ", ch) + ")");
        C(PortraitPeopleMath.Changes(null, now, "2026-11-25").Count == 0, "someone listed for the first time: nothing changed yet");
        now.changed = string.Join("; ", ch);
        var later = Seen("middle", "drinker; albino", "scar", "mage", "knight", "look:albino; role:mage", string.Join("; ", ch));
        var kept = PortraitPeopleMath.Changes(now, later, "2026-12-01");
        C(kept.SequenceEqual(new[] { "look:albino@2026-11-25", "role:mage@2026-11-25", "title:knight@2026-11-25" }),
            "kept with their first date while they hold and the picture does not show them; dropped once it does (a new picture of their age, with the scar)");
        var gone = Seen("middle", "drinker", "scar", "mage", "", "role:mage", string.Join("; ", kept));
        C(PortraitPeopleMath.Changes(later, gone, "2026-12-02").SequenceEqual(new[] { "role:mage@2026-11-25" }), "a look or a title no longer theirs: dropped");

        // ---------------- The list ----------------
        var rows = PortraitPeopleMath.Rows(new[]
        {
            new PersonSeen { id = "7", name = "Zed", standing = "notable", rank = 3 }, new PersonSeen { id = "42", name = "Myst", standing = "colonist", rank = 0, age = "middle" },
            new PersonSeen { id = "9", name = "Ana", standing = "prisoner", rank = 2 }, new PersonSeen { id = "42", name = "Twice", rank = 0 }
        }, new[] { new PersonSeen { id = "42", first = "2026-11-20", age = "adult" } }, "2026-11-25");
        C(rows.Select(r => r.name).SequenceEqual(new[] { "Myst", "Ana", "Zed" }) && rows[0].first == "2026-11-20" && rows[1].first == "2026-11-25",
            "one line each, the colony's order, the date first listed kept");
        var odd = new PersonSeen
        {
            id = "1", name = "O'Brien, Jr", fullName = "Sam \"Red\" O'Brien", standing = "slave", picture = "people/p01.png", words = "baseliner_male_adult", lacks = "look:drinker; mark:scar",
            changed = "mark:scar@2026-11-25", marks = "scar", first = "2026-11-25"
        };
        var back = PortraitPeopleMath.Read(PortraitPeopleMath.Csv(new[] { odd }));
        C(back.Count == 1 && back[0].name == odd.name && back[0].fullName == odd.fullName && back[0].lacks == odd.lacks && back[0].changed == odd.changed && back[0].rank == 1,
            "written and read back: commas, quotes and the separators kept, the standing's order read");
        C(PortraitPeopleMath.Csv(new PersonSeen[0]).TrimEnd() == string.Join(",", PortraitPeopleMath.Columns), "the columns' line");
        string handover = File.ReadAllText(Path.Combine(root, "docs", "portrait-art", "STUDIO-HANDOVER.md"));
        C(handover.Contains(PortraitPeopleMath.ListFile) && PortraitPeopleMath.Columns.All(c => handover.Contains("`" + c + "`")), "people.csv and each of its columns are told in the handover");
        C(conds.All(c => handover.Contains(c)), "the condition words are told in the handover");
        C(Code("UI/Faces/PortraitKeeper.cs").Contains("PortraitPeople.Write();"), "written at every save");

        // ---------------- A variant dismissed in the game ----------------
        string delete = Code("UI/Faces/PortraitDelete.cs");
        C(delete.Contains("public static List<(string title, string name)> VariantsShown(Pawn p, PortraitPicture pic)") && delete.Contains("() => ConfirmVersion(record)")
          && delete.Contains("PortraitEffects.PaintedNow(p)") && delete.Contains("FaceEngine.FaceNow(p)") && delete.Contains("if (rec.IsMarkVariant) list.Add("),
            "the large portrait's menu dismisses the variant shown now (a condition, the expression held, a mark variant) the way a deleted variant goes");
        C(keys.Contains("<CC_Portrait_Dismiss>") && keys.Contains("lists it again in variants-wanted.csv"), "its entry, and the dialog saying it is asked for anew");
        C(PortraitVariantsMath.VariantFile("people/p01.png", "asleep", new HashSet<string> { "p01_v-asleep" }).EndsWith("_v-asleep-2.png"),
            "made anew past the dismissed one's name: _v-asleep-2");

        // ---------------- A picture made for a name, in other saves ----------------
        var mine = Picture("P_Myst");
        mine.pawn = "Myst";
        var plain = Picture("P_Other");
        C(mine.Pinned && mine.MadeFor && !plain.MadeFor, "a picture made for a name is pinned until the names are read");
        C(PortraitLibraryMath.FreePins(new[] { mine, plain }, _ => -1) && !mine.Pinned && mine.MadeFor && mine.pinFree && !plain.pinFree,
            "no one of that name in this save: an ordinary picture, still made for them");
        var stranger = new PortraitLook { sex = PortraitSex.Male, age = PortraitAge.Adult, skin = mine.skin, hairColour = mine.hairColour, xenotype = "baseliner", race = "human" };
        C(PortraitLibraryMath.Pick(stranger, new List<PortraitRecord> { mine }, 9, _ => 0, _ => false, (_, _) => true).Any, "free for whoever fits it best");
        C(PortraitLibraryMath.FreePins(new[] { mine }, _ => 4) && mine.Pinned && !PortraitLibraryMath.Pick(stranger, new List<PortraitRecord> { mine }, 9, _ => 0, _ => false, (_, _) => true).Any,
            "someone of that name in the colony: theirs alone again");
        C(!PortraitLibraryMath.FreePins(new[] { mine }, _ => 4), "nothing changed: told so");
        string library = Code("UI/Faces/PortraitLibrary.cs");
        C(library.Contains("PortraitLibraryMath.FreePins(records,") && library.Contains("var pinned = records.Where(r => r.Pinned)") && library.Contains("NamesAgainFor(p);"),
            "the names read for each save, a free picture's face not held for anyone, a newcomer of that name gets it at once");
        return n;
    }
}
