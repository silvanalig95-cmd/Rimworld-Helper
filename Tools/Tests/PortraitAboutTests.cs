using System.Text;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// "About this picture" and the variants the game wants (the player, 23 November 2026: "one of the right-click options
/// could open kind of a menu where I see the image that is currently assigned to the pawn ... which of the characteristics
/// were now relevant for why this image was chosen ... and that I perhaps also could make corrections in there", "if a
/// pawn loses an eye ... create a one-eyed ... variant of this specific image", and "it's also important that a variant is
/// then marked as done once it's created and in the game, not that it will get created over and over"). The pure parts:
/// the distance in its parts, summing to the matcher's to the last bit; the hard rules one by one; a mark variant's record
/// with its side read, joined to its picture's face and shown for whoever has the mark on that side; the variants wanted
/// (the expressions a picture lacks, the marks no version shows, done once made), their instructions (the expressions in
/// Make-Variants' own words), their files beside the picture (a deleted name taken again with -2); the list written and
/// read; the corrections' lines, applied as the scripts apply them, appended to corrections.csv and written into
/// records.xml with the rest kept as it was, the whole writing on a library of its own; a variant's files and records
/// deleted with it; the words and the seams.
/// </summary>
public static class PortraitAboutTests
{
    private static PortraitColour Hex(string h) => PortraitLibraryMath.TryColour(h, out var c) ? c : throw new Exception("bad colour " + h);

    private const string Records = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
                                   + "<!-- Rimfolk portrait library, written by Update-PortraitLibrary.ps1: one record per picture in this folder. -->\r\n"
                                   + "<Defs>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X</defName>\r\n    <texPath>characters-01/X</texPath>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#e0b898</skinColor>\r\n    <hairColor>#4a3020</hairColor>\r\n    <hair>short</hair>\r\n    <era>medieval</era>\r\n    <marks><li>scar</li></marks>\r\n    <character>characters-01_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_blink</defName>\r\n    <texPath>characters-01/X_v-blink</texPath>\r\n    <variant>blink</variant>\r\n    <variantOf>P_X</variantOf>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#e0b898</skinColor>\r\n    <hairColor>#4a3020</hairColor>\r\n    <marks><li>scar</li></marks>\r\n    <character>characters-01_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_lostEyeLeft</defName>\r\n    <texPath>characters-01/X_v-lostEyeLeft</texPath>\r\n    <variant>losteyeleft</variant>\r\n    <variantOf>P_X</variantOf>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#e0b898</skinColor>\r\n    <hairColor>#4a3020</hairColor>\r\n    <marks><li>scar</li><li>lostEye</li></marks>\r\n    <side>left</side>\r\n    <character>characters-01_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_Y</defName>\r\n    <texPath>characters-01/Y</texPath>\r\n    <sex>female</sex>\r\n    <age>young</age>\r\n    <skinColor>#e0b898</skinColor>\r\n    <hairColor>#4a3020</hairColor>\r\n    <pawn>Ape &amp; Co</pawn>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "</Defs>\r\n";

    private static PortraitLook Person(string[]? marks = null, PortraitSide side = PortraitSide.None, bool patch = false)
    {
        var l = new PortraitLook { sex = PortraitSex.Male, age = PortraitAge.Adult, skin = Hex("#e0b898"), hairColour = Hex("#4a3020"), xenotype = "baseliner", baseliner = true, eyePatch = patch };
        foreach (var m in marks ?? Array.Empty<string>()) l.AddMark(m, side);
        return l;
    }

    private static PortraitRecord Rec(string name, string[]? marks = null, PortraitSide side = PortraitSide.None, string variant = "base", string variantOf = "")
    {
        var r = new PortraitRecord
        {
            name = name, sex = PortraitSex.Male, age = PortraitAge.Adult, skin = Hex("#e0b898"), skinSet = true, hairColour = Hex("#4a3020"), hairColourSet = true, side = side, variant = variant,
            variantOf = variantOf
        };
        foreach (var m in marks ?? Array.Empty<string>())
        {
            r.marks.Add(PortraitLibraryMath.MarkKey(m));
            if (m == "eyePatch") r.patch = true;
        }
        return r;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "about this picture and the variants wanted: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string art = Path.Combine(root, "Tools", "PortraitArt");

        // ---------------- The distance in its parts ----------------
        {
            var rng = new Random(23112026);
            string RandomHex() => "#" + rng.Next(0x1000000).ToString("x6");
            int same = 0, tried = 0;
            var feats = PortraitLibraryMath.Features;
            for (int k = 0; k < 400; k++)
            {
                var l = new PortraitLook
                {
                    sex = PortraitSex.Male, skin = Hex(RandomHex()), hairColour = Hex(RandomHex()), hair = (PortraitHairStyle)rng.Next(7), beard = (PortraitBeard)rng.Next(5),
                    build = (PortraitBuild)rng.Next(4), era = (PortraitEra)rng.Next(5), dress = (PortraitDress)rng.Next(6), role = rng.Next(3) == 0 ? "cook" : ""
                };
                var r = new PortraitRecord
                {
                    skin = Hex(RandomHex()), skinSet = rng.Next(5) > 0, hairColour = Hex(RandomHex()), hairColourSet = rng.Next(5) > 0, hair = (PortraitHairStyle)rng.Next(7),
                    beard = (PortraitBeard)rng.Next(5), build = (PortraitBuild)rng.Next(4), era = (PortraitEra)rng.Next(5), dress = (PortraitDress)rng.Next(6)
                };
                for (int f = 0; f < 3; f++)
                {
                    if (rng.Next(2) == 0) l.features.Add(feats[rng.Next(feats.Length)]);
                    if (rng.Next(2) == 0) r.features.Add(feats[rng.Next(feats.Length)]);
                }
                if (rng.Next(3) == 0) l.AddMark("scar");
                if (rng.Next(3) == 0) l.AddMark("lostEye", PortraitSide.Left);
                if (rng.Next(4) == 0) l.AddLook("drinker", false);
                var p = PortraitLibraryMath.Parts(l, r);
                tried++;
                float sum = p.skin + p.hairColour + p.hair + p.beard + p.build + p.era + p.dress + p.features + p.marks + p.looks;
                if (p.Total == PortraitLibraryMath.Distance(l, r) && Math.Abs(sum - p.Total) < 1e-4f) same++;
            }
            C(same == tried, $"the parts sum to the matcher's distance, to the last bit, for {tried} random people and pictures ({same})");
            var me = Person(new[] { "scar" });
            var x = Rec("X");
            x.hair = PortraitHairStyle.Long;
            x.beard = PortraitBeard.Full;
            var px = PortraitLibraryMath.Parts(me, x);
            C(px.hair == PortraitLibraryMath.HairApartOf(PortraitHairStyle.Short, PortraitHairStyle.Long) && px.beard == PortraitLibraryMath.BeardApartOf(PortraitBeard.None, PortraitBeard.Full)
                                                                                                         && px.marks == PortraitLibraryMath.MarkCost && px.skin == 0f && px.hairColour == 0f,
                "each part where it belongs: the hair's and the beard's by their tables, a mark of theirs it doesn't show, nothing for the same colours");
            var rules = PortraitLibraryMath.Rules(Rec("Y", new[] { "lostEye" }, PortraitSide.Right), Person(new[] { "lostEye" }, PortraitSide.Left));
            C(rules.kind && rules.sex && rules.age && rules.skin && !rules.marks && rules.looks && rules.specials && rules.headwear && !rules.All,
                "the hard rules one by one: a lost right eye is not met for someone who lost the left");
            var woman = Person();
            woman.sex = PortraitSex.Female;
            woman.age = PortraitAge.Old;
            var rw = PortraitLibraryMath.Rules(Rec("Z"), woman);
            C(!rw.sex && !rw.age && rw.kind && rw.marks, "another sex and another age band: those two not met, the rest met");
        }

        // ---------------- A mark variant's record, its side, and whom it is shown for ----------------
        {
            var people = new List<FolderPortrait>();
            var probs = new List<string>();
            PortraitFolderMath.Parse(Records, people, null, probs);
            var rec = people.Select(p => p.record).ToList();
            var v = rec.First(r => r.name == "P_X_lostEyeLeft");
            C(probs.Count == 0 && v.variant == "lostEyeLeft" && v.IsMarkVariant && !v.IsExpression && v.side == PortraitSide.Left && v.marks.Contains("lostEye") && v.marks.Contains("scar"),
                $"a mark variant read: its word as Rimfolk writes it (the scripts may write it small), its side, its marks ({string.Join("; ", probs)})");
            C(rec.First(r => r.name == "P_X_blink").IsExpression && !rec.First(r => r.name == "P_X").IsMarkVariant, "an expression stays an expression, the picture a picture");
            var faces = new Dictionary<string, Dictionary<string, int>>();
            var own = PortraitLibraryMath.SplitExpressions(rec, faces, null);
            var variants = new Dictionary<int, List<int>>();
            var kept = PortraitLibraryMath.SplitVariants(rec, own, variants, probs);
            int xi = rec.FindIndex(r => r.name == "P_X"), vi = rec.FindIndex(r => r.name == "P_X_lostEyeLeft");
            C(kept.Count == 3 && variants.Count == 1 && variants[xi].SequenceEqual(new[] { vi }) && faces["P_X"].ContainsKey("blink") && v.Character == rec[xi].Character,
                "the variant joins its picture's face, kept among the pictures; the expression belongs to the picture");
            var orphan = new List<PortraitRecord> { Rec("Q_v", new[] { "burn" }, variant: "burn", variantOf: "Q") };
            var orphanProbs = new List<string>();
            C(PortraitLibraryMath.SplitVariants(orphan, new List<int> { 0 }, new Dictionary<int, List<int>>(), orphanProbs).Count == 0 && orphanProbs.Count == 1,
                "a mark variant of a picture not in the library: left out and told");
            var probs2 = new List<string>();
            var both = PortraitLibraryMath.Record("B", f => f switch { "texPath" => "a/B", "sex" => "male", "skinColor" => "#e0b898", "hairColor" => "#4a3020", "side" => "right", "variant" => "bionicEyeLeft", "variantOf" => "A", _ => null },
                f => f == "marks" ? new[] { "eyePatch" } : null, probs2);
            C(both.side == PortraitSide.Left && both.marks.Contains("bionicEye") && both.patch && probs2.Count == 1,
                "the variant's own side wins over a side written against it (told), its mark added to the marks written");
            var bad = new List<string>();
            PortraitLibraryMath.Record("S", f => f == "side" ? "up" : f == "skinColor" ? "#e0b898" : f == "hairColor" ? "#4a3020" : f == "texPath" ? "a/S" : null, f => null, bad);
            C(bad.Count == 1 && bad[0].StartsWith("side:"), "a side that is neither left nor right: told");

            // Whom each version is shown for.
            int Shown(PortraitLook l) => PortraitLibraryMath.Version(l, rec, xi, variants[xi]);
            C(Shown(Person(new[] { "scar", "lostEye" }, PortraitSide.Left)) == vi, "someone holding the picture who lost their left eye: the variant made for it is shown for them");
            C(Shown(Person(new[] { "scar", "lostEye" }, PortraitSide.Right)) == xi, "someone who lost their right eye: not the left eye's variant, the picture itself");
            C(Shown(Person(new[] { "scar", "lostEye" })) == vi, "a lost eye on a side not told: the variant shows it");
            C(Shown(Person(new[] { "scar" })) == xi && Shown(Person()) == xi && PortraitLibraryMath.Version(null, rec, xi, variants[xi]) == xi,
                "nobody else: the picture itself (nothing known of them: the picture too)");
            var bothEyes = Person(new[] { "scar", "lostEye" }, PortraitSide.Left);
            bothEyes.AddMark("lostEye", PortraitSide.Right);
            C(Shown(bothEyes) == vi && bothEyes.SidesOf("lostEye") == (PortraitSide.Left | PortraitSide.Right), "both eyes lost: the one eye's variant, both sides kept");
            var lib = new List<PortraitRecord> { Rec("A"), Rec("A_lostEyeLeft", new[] { "lostEye" }, PortraitSide.Left, "lostEyeLeft", "A"), Rec("A_patchLeft", new[] { "eyePatch" }, PortraitSide.Left, "eyePatchLeft", "A") };
            var list = new List<int> { 1, 2 };
            C(PortraitLibraryMath.Version(Person(new[] { "lostEye" }, PortraitSide.Left), lib, 0, list) == 1 && PortraitLibraryMath.Version(Person(new[] { "lostEye" }, PortraitSide.Left, true), lib, 0, list) == 2,
                "a lost eye: the plain one; with an eye patch worn: the patch");
            C(PortraitLibraryMath.ShowsMarks(lib[1], Person(new[] { "lostEye" }, PortraitSide.Left)) && !PortraitLibraryMath.ShowsMarks(lib[1], Person(new[] { "lostEye" }, PortraitSide.Right))
                                                                                                  && !PortraitLibraryMath.Shows(lib[1], Person())
                                                                                                  && PortraitLibraryMath.ShowsMarks(Rec("Z", new[] { "lostEye" }), Person(new[] { "lostEye" }, PortraitSide.Right)),
                "the matcher's mark rule with the side: only the person's own side; a picture with no side for either");
            // The first pick goes by the face: of its pictures, the variant showing the mark is the nearer (the library keeps the picture and shows the variant).
            var pick = PortraitLibraryMath.Pick(Person(new[] { "scar", "lostEye" }, PortraitSide.Left), rec, 7, c => 0);
            C(pick.Any && pick.index == vi && pick.character == rec[xi].Character, "a new pick of someone with the mark: the face's variant with it is the nearest of that face");
            var libSrc = Read("UI/Faces/PortraitLibrary.cs");
            C(libSrc.Contains("if (pick.Any) pick.index = RootOf(pick.index);") && libSrc.Contains("if (i >= 0) i = VersionFor(p, i);") && libSrc.Contains("PortraitLibraryMath.Version(PortraitReader.MarksOf(p), records, root, list)"),
                "the library keeps the picture itself and shows the version for the body now, read every few seconds while shown");
            C(PortraitLibraryMath.HeadUnchanged("artificialArmLeft") && !PortraitLibraryMath.HeadUnchanged("lostEyeLeft") && libSrc.Contains("PortraitLibraryMath.HeadUnchanged(records[i].variant)"),
                "an artificial arm's variant shows its picture's expressions until it has its own; a lost eye's never");
        }

        // ---------------- The variants wanted ----------------
        {
            C(PortraitVariantsMath.ExpressionsWanted(new[] { "smile" }).SequenceEqual(new[] { "blink", "frown", "pain" }) && PortraitVariantsMath.ExpressionsWanted(PortraitLifeMath.Faces).Count == 0,
                "the expressions a picture lacks, in order; none where it has all four");
            var pic = Rec("A", new[] { "scar" });
            var lostLeft = Person(new[] { "scar", "lostEye" }, PortraitSide.Left);
            C(PortraitVariantsMath.MarksWanted(lostLeft, pic, null).SequenceEqual(new[] { "lostEyeLeft" }), "a lost left eye the picture doesn't show: lostEyeLeft (the scar it shows: nothing)");
            C(PortraitVariantsMath.MarksWanted(lostLeft, pic, new[] { Rec("A_v", new[] { "scar", "lostEye" }, PortraitSide.Left, "lostEyeLeft", "A") }).Count == 0,
                "done: a variant of that word made, nothing wanted");
            C(PortraitVariantsMath.MarksWanted(lostLeft, pic, new[] { Rec("A_v", new[] { "scar", "lostEye" }, PortraitSide.Right, "lostEyeRight", "A") }).SequenceEqual(new[] { "lostEyeLeft" }),
                "the other side's variant doesn't count");
            var all = Person(new[] { "lostEye", "bionicEye", "burn", "missingEar", "artificialArm" }, PortraitSide.Right, true);
            all.AddMark("scar");
            var words = PortraitVariantsMath.MarksWanted(all, Rec("B"), null);
            C(words.SequenceEqual(new[] { "eyePatchRight", "bionicEyeRight", "scar", "burn", "missingEarRight", "artificialArmRight" }),
                $"every mark on its side, an eye patch worn over the lost eye, in the list's order ({string.Join(", ", words)})");
            C(PortraitVariantsMath.MarksWanted(Person(new[] { "lostEye" }), Rec("C"), null).SequenceEqual(new[] { "lostEyeLeft" }), "a side not told: the left");
            C(PortraitVariantsMath.MarksWanted(Person(new[] { "lostEye" }, PortraitSide.Left), Rec("D", new[] { "eyePatch" }, PortraitSide.Left), null).SequenceEqual(new[] { "lostEyeLeft" })
              && PortraitVariantsMath.MarksWanted(Person(new[] { "lostEye" }, PortraitSide.Left, true), Rec("D", new[] { "eyePatch" }, PortraitSide.Left), null).Count == 0,
                "a picture with an eye patch: enough for someone wearing one, the plain lost eye still wanted for someone who doesn't");
            C(PortraitVariantsMath.MarksWanted(Person(), pic, null).Count == 0 && PortraitVariantsMath.MarksWanted(null, pic, null).Count == 0, "no marks: nothing wanted");

            // The instructions.
            string make = File.Exists(Path.Combine(art, "Make-Variants.ps1")) ? File.ReadAllText(Path.Combine(art, "Make-Variants.ps1")) : "";
            var keep = Regex.Match(make, @"\$Keep = '([^']*)'");
            bool sameWords = keep.Success && keep.Groups[1].Value == PortraitVariantsMath.KeepFace;
            foreach (var f in PortraitLifeMath.Faces)
            {
                var m = Regex.Match(make, "'" + f + @"'\s*=\s*'((?:[^']|'')*)'\s*\+\s*\$Keep");
                sameWords &= m.Success && m.Groups[1].Value.Replace("''", "'") + PortraitVariantsMath.KeepFace == PortraitVariantsMath.Instruction(f);
            }
            C(sameWords, "the expressions' instructions word for word as Make-Variants.ps1 has them");
            string lostL = PortraitVariantsMath.Instruction("lostEyeLeft") ?? "", lostR = PortraitVariantsMath.Instruction("lostEyeRight") ?? "";
            C(lostL.Contains("lost their left eye (the eye on the right of the picture)") && lostR.Contains("right eye (the eye on the left of the picture)") && lostL.EndsWith(PortraitVariantsMath.KeepMark),
                "a mark's side as seen from the person, and where that is in the picture; then what stays");
            C(PortraitLibraryMath.MarkVariantWords.All(w => !string.IsNullOrEmpty(PortraitVariantsMath.Instruction(w)))
              && PortraitVariantsMath.Words.All(w => { string t = PortraitVariantsMath.Instruction(w) ?? ""; return !t.Contains("—") && !t.Contains("!") && t.Length < 400; }),
                "every word has its instruction, plain, no dash of that kind, no exclamation mark");
            C(PortraitVariantsMath.Instruction("artificialArmLeft", PortraitEra.Medieval)!.Contains("wood") && PortraitVariantsMath.Instruction("artificialArmLeft", PortraitEra.Spacer)!.Contains("bionic")
                                                                                                               && PortraitVariantsMath.Instruction("nothing") == null,
                "an artificial arm by the clothes' age: wood and iron for the medieval, bionic otherwise; an unknown word none");

            // The files beside the picture.
            C(PortraitVariantsMath.VariantFile("characters-01/X.png", "lostEyeLeft", null) == "characters-01/X_v-lostEyeLeft.png"
              && PortraitVariantsMath.VariantFile("characters-01\\X", "blink", new HashSet<string>()) == "characters-01/X_v-blink.png", "a variant's file: beside its picture, _v- and the word");
            var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x_v-losteyeleft", "x_v-losteyeleft-2" };
            C(PortraitVariantsMath.VariantFile("characters-01/X.png", "lostEyeLeft", deleted) == "characters-01/X_v-lostEyeLeft-3.png"
              && PortraitVariantsMath.VariantFile("X", "scar", deleted) == "X_v-scar.png", "a name deleted for good is taken again with the next free number");

            // The list: rows, priorities, done, the first date kept.
            VariantWanted W(string picture, string word, string who, int priority, int rank, string record) =>
                new VariantWanted { picture = picture, word = word, instruction = PortraitVariantsMath.Instruction(word) ?? "", who = who, priority = priority, rank = rank, record = record };
            var fresh = new List<VariantWanted>
            {
                W("characters-01/N.png", "blink", "Noble (notable)", 2, 3, "P_N"),
                W("characters-01/P.png", "smile", "Pris (prisoner)", 1, 2, "P_P"),
                W("characters-01/X.png", "lostEyeLeft", "Ape (colonist)", 1, 0, "P_X"),
                W("characters-01/X.png", "blink", "Ape (colonist)", 1, 0, "P_X"),
                W("characters-01/X.png", "smile", "Ape (colonist)", 1, 0, "P_X"),
                W("characters-01/X.png", "blink", "Ape (colonist)", 1, 0, "P_X"),
                W("characters-01/S.png", "frown", "Sal (slave)", 1, 1, "P_S")
            };
            var old = new List<VariantWanted> { new VariantWanted { picture = "Characters-01/X.png", word = "lostEyeLeft", first = "2026-11-20" }, new VariantWanted { picture = "gone.png", word = "blink", first = "2026-01-01" } };
            var made = new HashSet<string> { PortraitVariantsMath.MadeKey("P_X", "smile") };
            var rows = PortraitVariantsMath.Rows(fresh, old, "2026-11-23", made);
            C(rows.Select(r => r.word + "@" + PortraitDeleteMath.StemOf(r.picture)).SequenceEqual(new[] { "blink@X.png", "lostEyeLeft@X.png", "frown@S.png", "smile@P.png", "blink@N.png" }),
                $"one row a picture and word, colonists first, then slaves, prisoners, the notables' at priority 2; done left out ({string.Join(", ", rows.Select(r => r.word + "@" + r.picture))})");
            C(rows[1].first == "2026-11-20" && rows[0].first == "2026-11-23" && rows.All(r => r.picture != "gone.png"), "the date first wanted kept from the file (the case of its path aside), today for a new one; what is no longer wanted gone");
            var p = new PortraitRecord { name = "P_X_smile2", variant = "smile", variantOf = "P_X" };
            C(PortraitVariantsMath.Made(new[] { p, Rec("P_X") }).SetEquals(new[] { PortraitVariantsMath.MadeKey("P_X", "smile") }), "what the library has: every version by its picture and word");
            string csv = PortraitVariantsMath.Csv(rows);
            C(csv.StartsWith("picture,word,instruction,who,priority,first\r\n") && csv.Contains("characters-01/X.png,lostEyeLeft,\"The person has lost their left eye")
                                                                         && csv.Contains(",Ape (colonist),1,2026-11-20\r\n") && !csv.Contains("\n\n"),
                "the file: the contract's columns, the picture's path with forward slashes, the instruction quoted, Windows' line endings");
            var back = PortraitVariantsMath.Read("﻿" + csv);
            C(back.Count == rows.Count && back[1].instruction == rows[1].instruction && back[1].priority == 1 && back[4].priority == 2 && back[1].first == "2026-11-20", "read back as written, a mark at its start too");
            var semi = PortraitVariantsMath.Read("picture;word;priority;first\r\na/X.png;blink;1;2026-11-01\r\n");
            C(semi.Count == 1 && semi[0].word == "blink" && semi[0].first == "2026-11-01", "a list saved again by Excel with semicolons reads too");
        }

        // ---------------- Corrections ----------------
        {
            var before = Rec("P_X", new[] { "scar" });
            var after = PortraitCorrectMath.Copy(before);
            after.sex = PortraitSex.Female;
            after.age = PortraitAge.Young;
            after.features.Add("pointedEars");
            after.marks.Remove("scar");
            after.marks.Add("lostEye");
            after.patch = true;
            after.side = PortraitSide.Left;
            after.role = "cook";
            after.headwear = true;
            var fixes = PortraitCorrectMath.Fixes(before, after);
            C(string.Join("; ", fixes) == "sex female; age young; features add:pointedEars; marks remove:scar; marks add:eyePatch; side left; role cook; headwear true",
                $"the lines a change makes, in corrections.csv's words ({string.Join("; ", fixes)})");
            C(before.sex == PortraitSex.Male && before.marks.Contains("scar") && !before.features.Contains("pointedEars"), "the copy changed, the record not");
            var applied = PortraitCorrectMath.Copy(before);
            bool all = fixes.All(f => PortraitCorrectMath.Apply(applied, f));
            C(all && PortraitCorrectMath.Fixes(applied, after).Count == 0, "the lines applied give the change back, each one known");
            var undo = PortraitCorrectMath.Fixes(after, before);
            C(undo.Any(f => f.ToString() == "side clear") && undo.Any(f => f.ToString() == "role clear") && undo.Any(f => f.ToString() == "headwear false"),
                "taking a side, a work or headwear away: clear, clear, false");
            var r = Rec("R");
            C(!PortraitCorrectMath.Apply(r, new PortraitFix("age", "ancient")) && !PortraitCorrectMath.Apply(r, new PortraitFix("marks", "add:wings")) && !PortraitCorrectMath.Apply(r, new PortraitFix("colour", "red"))
              && PortraitCorrectMath.Apply(r, new PortraitFix("age", "middle-aged")) && r.age == PortraitAge.Middle, "an unknown word changes nothing; the records' other words read as theirs");
            C(PortraitCorrectMath.PictureCell("characters-01/X") == "characters-01\\X.png", "a picture named as the scripts name it in corrections.csv");
            string fresh = PortraitCorrectMath.Append("", "characters-01\\X.png", fixes.Take(2));
            C(fresh == "picture,field,value,apply,why\r\ncharacters-01\\X.png,sex,female,yes,set in the game\r\ncharacters-01\\X.png,age,young,yes,set in the game\r\n",
                "a new corrections.csv: the scripts' columns, the lines marked yes, why \"set in the game\"");
            string had = "﻿picture;field;value;apply;why\r\nx.png;age;old;ask;the model saw an old man\r\n";
            string more = PortraitCorrectMath.Append(had, "characters-01\\X.png", new[] { new PortraitFix("looks", "add:drinker") });
            C(more == "picture;field;value;apply;why\r\nx.png;age;old;ask;the model saw an old man\r\ncharacters-01\\X.png;looks;add:drinker;yes;set in the game\r\n",
                "added at the end of a file saved by Excel, its own separator, the lines already there as they were");
            var lines = PortraitCorrectMath.Read(more);
            C(lines.Count == 2 && lines[1][1] == "looks" && lines[1][3] == "yes" && lines[1][4] == PortraitCorrectMath.Why, "read back by its columns");

            // The game's own lines always hold (the player: "the corrections i make through the game client are always prioritized").
            string withGame = "picture,field,value,apply,why\r\n"
                              + "characters-01\\X.png,sex,female,ask,the model saw a woman\r\n"
                              + "Library\\Characters-01\\x.png,sex,female,yes,set in the game\r\n"
                              + "characters-01\\X.png,marks,add:scar,yes,set in the game\r\n"
                              + "characters-01\\Y.png,sex,female,yes,set in the game\r\n";
            string redone = PortraitCorrectMath.Append(withGame, "characters-01\\X.png",
                new[] { new PortraitFix("sex", "male"), new PortraitFix("marks", "remove:scar"), new PortraitFix("marks", "add:lostEye") });
            C(redone == "picture,field,value,apply,why\r\n"
                        + "characters-01\\X.png,sex,female,ask,the model saw a woman\r\n"
                        + "characters-01\\X.png,sex,male,yes,set in the game\r\n"
                        + "characters-01\\X.png,marks,remove:scar,yes,set in the game\r\n"
                        + "characters-01\\Y.png,sex,female,yes,set in the game\r\n"
                        + "characters-01\\X.png,marks,add:lostEye,yes,set in the game\r\n",
                "a field corrected in the game again: its own line (the picture however spelled) takes the new value, a list's by its word, a new word a new line; the check's and another picture's lines as they were");

            // Taken again on top of records.xml when the library is read.
            var folderPeople = new List<FolderPortrait>();
            PortraitFolderMath.Parse(Records, folderPeople, null, null);
            string gameLines = "picture,field,value,apply,why\r\n"
                               + "characters-01\\X.png,age,old,ask,the model saw an old man\r\n"
                               + "characters-01\\X.png,age,old,yes,the model saw an old man\r\n"
                               + "characters-01\\X.png,sex,female,yes,set in the game\r\n"
                               + "X.png,marks,remove:scar,yes,set in the game\r\n"
                               + "library\\characters-01\\X.png,side,right,yes,set in the game\r\n"
                               + "characters-01\\X.png,headwear,true,no,set in the game\r\n"
                               + "characters-01\\Gone.png,sex,female,yes,set in the game\r\n";
            int took = PortraitCorrectMath.ApplyGameLines(folderPeople, gameLines);
            var byName = folderPeople.ToDictionary(p => p.record.name, p => p.record);
            var rx = byName["P_X"];
            var rv = byName["P_X_lostEyeLeft"];
            C(took == 3 && rx.sex == PortraitSex.Female && byName["P_X_blink"].sex == PortraitSex.Female && rv.sex == PortraitSex.Female && rx.marks.Count == 0
              && !byName["P_X_blink"].marks.Contains("scar") && rv.marks.SetEquals(new[] { "lostEye" }),
                $"the game's own yes lines taken again on top of records.xml, for the picture and its versions ({took} records)");
            C(rx.side == PortraitSide.Right && rv.side == PortraitSide.Left && rx.age == PortraitAge.Adult && !rx.headwear && byName["P_Y"].sex == PortraitSex.Female && byName["P_Y"].marks.Count == 0,
                "a mark variant keeps its own side; the check's lines (asked or yes) and a game line marked no left to Update-PortraitLibrary; another picture untouched");
            C(PortraitCorrectMath.ApplyGameLines(folderPeople, gameLines) == 0 && PortraitCorrectMath.ApplyGameLines(folderPeople, "") == 0, "taken again: nothing more changes");
            string folderSrc = Read("UI/Faces/PortraitFolder.cs");
            C(folderSrc.Contains("got.corrected = PortraitCorrectMath.ApplyGameLines(got.people,") && folderSrc.IndexOf("ApplyGameLines", StringComparison.Ordinal) > folderSrc.IndexOf("PortraitFolderMath.Parse(File.ReadAllText(records)", StringComparison.Ordinal),
                "the library folder's reading takes them on its worker, after records.xml, at the start and at \"Reload the library\"");

            // records.xml written with the corrected records, the rest as it was.
            var people = new List<FolderPortrait>();
            PortraitFolderMath.Parse(Records, people, null, null);
            var x = PortraitCorrectMath.Copy(people[0].record);
            x.sex = PortraitSex.Female;
            x.marks.Remove("scar");
            x.features.Add("horns");
            x.side = PortraitSide.Right;
            x.headwear = true;
            var patched = new List<string>();
            string xml = PortraitCorrectMath.WithRecords(Records, new Dictionary<string, PortraitRecord> { ["P_X"] = x }, patched);
            var again = new List<FolderPortrait>();
            var probs = new List<string>();
            PortraitFolderMath.Parse(xml, again, null, probs);
            var x2 = again[0].record;
            C(patched.SequenceEqual(new[] { "P_X" }) && probs.Count == 0 && x2.sex == PortraitSex.Female && x2.marks.Count == 0 && x2.features.Contains("horns") && x2.side == PortraitSide.Right && x2.headwear
              && x2.era == PortraitEra.Medieval && x2.hair == PortraitHairStyle.Short && again[0].texPath == "characters-01/X",
                $"the record's corrected fields written, its other fields as they were ({string.Join("; ", probs)})");
            int a = Records.IndexOf("  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_blink", StringComparison.Ordinal);
            C(xml.Substring(xml.IndexOf("  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_blink", StringComparison.Ordinal)) == Records.Substring(a)
              && xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<!-- Rimfolk") && !Regex.IsMatch(xml, "(?<!\r)\n"),
                "every other record exactly as it was, the declaration, the comment and the line endings kept");
            C(xml.Contains("    <sex>female</sex>\r\n") && xml.Contains("    <features><li>horns</li></features>\r\n    <side>right</side>\r\n    <headwear>true</headwear>\r\n  </ColonyCouncil.PortraitDef>")
                                                     && !xml.Substring(0, a).Contains("<marks>"),
                "a field set in place, new ones at the record's end as the scripts write them, an emptied list taken out");
            C(ReferenceEquals(PortraitCorrectMath.WithRecords(Records, new Dictionary<string, PortraitRecord> { ["P_Gone"] = x }, null), Records), "a record records.xml doesn't have: the text unchanged");

            // The whole writing, on a library of its own.
            string dir = Path.Combine(Path.GetTempPath(), "rimfolk-correct-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "records.xml"), Records);
                var job = new CorrectJob { folder = dir, records = new Dictionary<string, PortraitRecord> { ["P_X"] = x } };
                job.lines.Add(new KeyValuePair<string, List<PortraitFix>>("characters-01\\X.png", new List<PortraitFix> { new PortraitFix("sex", "female") }));
                var res = PortraitCorrectMath.Run(job, ms => { });
                string list = File.ReadAllText(Path.Combine(dir, "corrections.csv"));
                C(res.listed && res.recorded && res.recordsPatched == 1 && list.EndsWith("characters-01\\X.png,sex,female,yes,set in the game\r\n")
                  && File.ReadAllText(Path.Combine(dir, "records.xml")).Contains("<sex>female</sex>") && !File.Exists(Path.Combine(dir, "records.xml.tmp")),
                    "written on a library of its own: the line in corrections.csv, the record in records.xml, no file left behind");
                job.lines[0].Value[0].value = "male";
                var res2 = PortraitCorrectMath.Run(job, ms => { });
                string list2 = File.ReadAllText(Path.Combine(dir, "corrections.csv"));
                C(res2.listed && list2.Split('\n').Length == 3 && list2.EndsWith("characters-01\\X.png,sex,male,yes,set in the game\r\n"),
                    "corrected again in the game: its own line takes the new value, no second line");
                Directory.CreateDirectory(Path.Combine(dir, "corrections.csv.tmp"));
                job.tries = 2;
                var res3 = PortraitCorrectMath.Run(job, ms => { });
                C(!res3.listed && !res3.recorded && res3.error.Length > 0, "a list that can't be written: nothing written, the error told");
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception)
                {
                    // A temporary folder left behind.
                }
            }
        }

        // ---------------- A variant deleted with its own ----------------
        {
            var pairs = new[] { new KeyValuePair<string, string>("P_X", ""), new KeyValuePair<string, string>("P_X_blink", "P_X"), new KeyValuePair<string, string>("P_X_lostEyeLeft", "P_X"),
                new KeyValuePair<string, string>("P_X_lostEyeLeft_blink", "P_X_lostEyeLeft") };
            C(PortraitDeleteMath.RecordsOf("P_X", pairs).SequenceEqual(new[] { "P_X", "P_X_blink", "P_X_lostEyeLeft", "P_X_lostEyeLeft_blink" })
              && PortraitDeleteMath.RecordsOf("P_X_lostEyeLeft", pairs).SequenceEqual(new[] { "P_X_lostEyeLeft", "P_X_lostEyeLeft_blink" }),
                "a picture's records: its versions and theirs; a variant's: its own expressions, never its picture");
            var listing = new[] { "c/X.png", "c/X_v-blink.png", "c/X_v-lostEyeLeft.png", "c/X_v-lostEyeLeft-2.png", "c/X_v-lostEyeLeft_v-blink.png", "c/X2.png", "c/X_v-lostEyeLeft-2_v-smile.png" };
            var files = PortraitDeleteMath.FilesOf("c/X", false, null, null, listing);
            C(files.Count == 6 && !files.Contains("c/X2.png"), $"a picture's files beside it: its expressions, its variants (made again too) and theirs, never another picture's ({string.Join(", ", files)})");
            var vfiles = PortraitDeleteMath.FilesOf("c/X_v-lostEyeLeft", false, null, null, listing);
            C(vfiles.SequenceEqual(new[] { "c/X_v-lostEyeLeft.png", "c/X_v-lostEyeLeft_v-blink.png" }), $"a variant's files: its own and its expressions' ({string.Join(", ", vfiles)})");
        }

        // ---------------- The words and the seams ----------------
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        string window = Read("UI/Faces/PortraitAboutWindow.cs"), correct = Read("UI/Faces/PortraitCorrect.cs"), wanted = Read("UI/Faces/PortraitVariants.cs"), delete = Read("UI/Faces/PortraitDelete.cs");
        var used = Regex.Matches(window + correct + wanted + delete, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        // Keys made of a stem and a word: each word's.
        foreach (var f in PortraitLifeMath.Faces) used.AddRange(new[] { "CC_PortraitAbout_Face_" + f, "CC_PortraitAbout_FaceWhat_" + f, "CC_PortraitAbout_FaceWhen_" + f });
        foreach (var mk in PortraitLibraryMath.Marks.Distinct()) used.AddRange(new[] { "CC_PortraitAbout_Mark_" + mk, "CC_PortraitAbout_MarkWhat_" + mk, "CC_PortraitAbout_MarkWhen_" + mk });
        foreach (var k in new[] { "Kind", "Sex", "Age", "Skin", "HairColour", "Hair", "Beard", "Build", "Clothes", "Features", "Marks", "Looks", "Role", "Headwear", "Facing", "File", "Character", "Pinned", "Version", "Side" })
            used.Add("CC_PortraitAbout_" + k);
        // The stems the window joins a word to are no keys of their own.
        used = used.Where(k => !k.EndsWith("_")).Distinct().ToList();
        var missing = used.Where(k => !keyed.Contains("<" + k + ">")).ToList();
        C(used.Count >= 90 && missing.Count == 0, $"every word the window, the corrections and the list use is in the language file ({used.Count}; missing: {string.Join(", ", missing)})");
        var texts = Regex.Matches(keyed, @"<(CC_PortraitAbout_\w+|CC_Portrait_Correct\w*|CC_Portrait_DeleteVariant\w*|CC_PortraitWanted_\w+)>([^<]*)<").Select(m => m.Groups[2].Value).ToList();
        C(texts.Count >= 100 && texts.All(t => !t.Contains("—") && !t.Contains("!")), "plain words: no dash of that kind, no exclamation marks");
        C(keyed.Contains("<CC_PortraitAbout_Menu>About this picture...<") && keyed.Contains("<CC_PortraitAbout_DeleteVariant>Delete this variant for good...<"),
            "the menu's entry and the variant's delete by the names the proposal gave them");
        string inspect = Read("UI/Faces/InspectPortrait.cs");
        int aboutAt = inspect.IndexOf("Window_PortraitAbout.Open(p)", StringComparison.Ordinal), trainAt = inspect.IndexOf("PortraitTraining.AddOption(p, options);", StringComparison.Ordinal);
        C(aboutAt > 0 && trainAt > aboutAt, "on the large portrait's right-click menu, after choosing another picture and before keeping one for training");
        C(Read("UI/FramedWindowsMath.cs").Contains("new FrameKey(\"PortraitAbout\"") && window.Contains(": base(\"PortraitAbout\")"), "in Rimfolk's frame, with its least size");
        C(Read("UI/Faces/PortraitKeeper.cs").Contains("PortraitVariants.Write();") && wanted.Contains("PortraitStore.Run(") && wanted.Contains("PortraitDeleteMath.WriteSafely(path, text, true)")
          && wanted.Contains("PortraitVariantsMath.Made(") && !wanted.Contains("Scribe"),
            "the list written on saving the game, on a worker, safely, what the library has read again just before; nothing saved in the game");
        C(correct.Contains("PortraitLibrary.Source.Corrected(") && correct.Contains("PortraitStore.Run(() => PortraitCorrectMath.Run(job") && Read("UI/Faces/PortraitFolder.cs").Contains("PortraitDelete.Running || PortraitCorrect.Running"),
            "a correction taken at once (whoever it no longer fits picked again), written on a worker, the folder read only after it");
        C(delete.Contains("public static void ConfirmVersion(string name)") && delete.Contains("PortraitCorrect.RootOf(pic.name)?.record.name"),
            "a variant deleted from the window with the delete's own dialog; the menu's delete is the picture itself's");
        C(window.Contains("PortraitLibraryMath.Parts(look, n.rec)") && window.Contains("PortraitLibraryMath.Rules(n.rec, look)") && window.Contains("lib.NextNearest(pawn, look, n.root, 5)"),
            "the window shows the matcher's own parts, rules and next pictures");
        string reader = Read("UI/Faces/PortraitReader.cs");
        C(reader.Contains("l.AddMark(\"lostEye\", SideOf(part))") && reader.Contains("at.untranslatedCustomLabel, at.woundAnchorTag") && reader.Contains("l.eyePatch = true"),
            "a person's marks read with their side (the body part's label and wound anchor), an eye patch worn");
        C(PortraitLibraryMath.SideFromWords(PortraitLibraryMath.Tokens("left eye", "LeftEye")) == PortraitSide.Left && PortraitLibraryMath.SideFromWords(PortraitLibraryMath.Tokens("RightShoulder")) == PortraitSide.Right
                                                                                                                  && PortraitLibraryMath.SideFromWords(PortraitLibraryMath.Tokens("head")) == PortraitSide.None
                                                                                                                  && PortraitLibraryMath.EyePatchWords(PortraitLibraryMath.Tokens("AM_Apparel_Eyepatch", "eyepatch"))
                                                                                                                  && PortraitLibraryMath.EyePatchWords(PortraitLibraryMath.Tokens("EyePatch", "eye patch"))
                                                                                                                  && !PortraitLibraryMath.EyePatchWords(PortraitLibraryMath.Tokens("BionicEye", "bionic eye")),
            "the side from a part's words; an eye patch by its name (Alpha Memes' too), a bionic eye not");
        return n;
    }
}
