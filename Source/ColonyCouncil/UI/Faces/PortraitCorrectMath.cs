using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ColonyCouncil
{
    /// <summary>One correction to a picture's record: its field and value as corrections.csv writes them (age young, sex female, marks add:lostEye, side left ...).</summary>
    public sealed class PortraitFix
    {
        public string field = "", value = "";

        public PortraitFix()
        {
        }

        public PortraitFix(string field, string value)
        {
            this.field = field ?? "";
            this.value = value ?? "";
        }

        public override string ToString() => field + " " + value;
    }

    /// <summary>A correction to write, as read on the game's thread: everything the worker needs.</summary>
    public sealed class CorrectJob
    {
        /// <summary>The library folder (its corrections.csv and records.xml).</summary>
        public string folder = "";
        /// <summary>Each picture's lines for corrections.csv: its name there (<c>batch\file.png</c>) and its corrections.</summary>
        public List<KeyValuePair<string, List<PortraitFix>>> lines = new List<KeyValuePair<string, List<PortraitFix>>>();
        /// <summary>The records as they are now in the game, by name, to write into records.xml (the picture's and its versions').</summary>
        public Dictionary<string, PortraitRecord> records = new Dictionary<string, PortraitRecord>(StringComparer.Ordinal);
        public int tries = PortraitDeleteMath.Tries;
    }

    /// <summary>What came of writing a correction.</summary>
    public sealed class CorrectResult
    {
        /// <summary>corrections.csv was written (the change stands after Update-PortraitLibrary too).</summary>
        public bool listed;
        /// <summary>records.xml was written (the change stands when the game reads the folder again).</summary>
        public bool recorded;
        public int recordsPatched;
        public string error = "";
    }

    /// <summary>
    /// Correcting a picture's record in the game (docs: DESIGN.md, "Portraits: about this picture, and the variants the
    /// game wants"; the player, 23 November 2026: "if I see, for example, the sex is incorrect or the age is actually kind
    /// of a different age group, that I could immediately change and update this through the game"), without game types
    /// (Tools/Tests try it on a folder of its own):
    /// <list type="bullet">
    /// <item>the lines a change makes (<see cref="Fixes"/>), in corrections.csv's words as the portrait scripts read them
    /// (Check-Pictures writes them, Update-PortraitLibrary applies the yes lines): sex, age, role (or clear), headwear (true
    /// or false), looks, marks and features (add:word or remove:word), side (left, right or clear);</item>
    /// <item>a line applied to a record in the game (<see cref="Apply"/>), as Update-PortraitLibrary applies it;</item>
    /// <item>corrections.csv with the lines added (<see cref="Append"/>), each marked yes, why "set in the game";</item>
    /// <item>records.xml with the corrected records' fields written as they are now (<see cref="WithRecords"/>), the rest as
    /// it was;</item>
    /// <item>the whole writing (<see cref="Run"/>), on a worker thread: the list, then the records, each written safely and
    /// tried again a few times (a cloud drive may hold a file a moment).</item>
    /// </list>
    /// </summary>
    public static class PortraitCorrectMath
    {
        public const string ListFile = "corrections.csv", Why = "set in the game";

        /// <summary>corrections.csv's columns, in the order the portrait scripts write them.</summary>
        public static readonly string[] Columns = { "picture", "field", "value", "apply", "why" };

        /// <summary>The fields a correction in the game may change, in the window's order.</summary>
        public static readonly string[] Fields =
            { "race", "xenotype", "sex", "age", "skin", "hairColour", "hair", "beard", "build", "era", "dress", "features", "marks", "side", "looks", "role", "headwear" };

        /// <summary>
        /// The skins the window offers besides the person's own: a sample of each of the eight tones the matcher tells apart
        /// (<see cref="PortraitLibraryMath.SkinToneFrom"/>, very fair to very dark, each in the middle of its tone), then the
        /// unusual skins some kinds have.
        /// </summary>
        public static readonly (string word, string hex)[] SkinChoices =
        {
            ("very fair", "f9dec8"), ("fair", "e5bfa5"), ("light", "d0a284"), ("medium", "b88665"), ("tan", "9c6c4b"), ("brown", "7c5339"), ("dark", "583a29"),
            ("very dark", "36241a"), ("green", "6f9a5a"), ("blue", "5a7fb0"), ("grey", "9a9a9a"), ("red", "b03a3a"), ("purple", "7a5aa0"), ("ink black", "1e1e26")
        };

        /// <summary>The hair colours the window offers besides the person's own: the natural ones, dark to light, then dyed ones.</summary>
        public static readonly (string word, string hex)[] HairChoices =
        {
            ("black", "1a1716"), ("dark brown", "3a2619"), ("brown", "5c3b24"), ("light brown", "8b6a47"), ("auburn", "7b3a22"), ("red", "b0441c"),
            ("strawberry blond", "c88a55"), ("blond", "d9b871"), ("platinum", "e6dbbf"), ("grey", "8f8d8a"), ("white", "e9e7e3"), ("blue", "3f5fbf"),
            ("green", "3f9a55"), ("pink", "d06aa0"), ("purple", "7a4fb0")
        };

        /// <summary>A picture as corrections.csv names it: its batch's folder and file, Windows' way (<c>colonists\X.png</c>).</summary>
        public static string PictureCell(string texPath) => PortraitFolderMath.FileKey(texPath).Replace('/', '\\');

        /// <summary>The marks as the records write them: an eye patch named so (the matcher counts it as a lost eye), in the marks' own order.</summary>
        public static List<string> MarkWords(PortraitRecord r)
        {
            var list = new List<string>();
            if (r == null) return list;
            foreach (var m in PortraitLibraryMath.Marks)
            {
                if (m == "eyePatch") continue;
                if (!r.marks.Contains(m)) continue;
                list.Add(m == "lostEye" && r.patch ? "eyePatch" : m);
            }
            foreach (var m in r.marks.OrderBy(x => x, StringComparer.Ordinal))
                if (Array.IndexOf(PortraitLibraryMath.Marks, m) < 0)
                    list.Add(m);
            return list;
        }

        /// <summary>A record's copy, every field its own (the lists too), for a change made on the side before it is taken.</summary>
        public static PortraitRecord Copy(PortraitRecord r)
        {
            if (r == null) return null;
            return new PortraitRecord
            {
                name = r.name, xenotypes = new List<string>(r.xenotypes), races = new List<string>(r.races), sex = r.sex, age = r.age, skin = r.skin, hairColour = r.hairColour,
                skinSet = r.skinSet, hairColourSet = r.hairColourSet, hair = r.hair, beard = r.beard, build = r.build, era = r.era, dress = r.dress, headwear = r.headwear,
                features = new HashSet<string>(r.features), marks = new HashSet<string>(r.marks), side = r.side, patch = r.patch,
                looks = new HashSet<string>(r.looks, StringComparer.OrdinalIgnoreCase), specials = new HashSet<string>(r.specials, StringComparer.OrdinalIgnoreCase),
                role = r.role, facing = r.facing, character = r.character, variant = r.variant, variantOf = r.variantOf, pawn = r.pawn
            };
        }

        /// <summary>
        /// The lines that turn <paramref name="before"/> into <paramref name="after"/>: one for the race (human for none) and
        /// the xenotype (baseliner for none), the sex, the age band, the skin's and the hair's colour (#rrggbb; none for no
        /// hair colour), the hair, beard, build, the clothes' age and kind (any for not told), the work (clear for none),
        /// headwear (true or false) and the side (clear for none) where each changed; add: and remove: lines for each feature,
        /// mark and look added or taken away. In the window's order.
        /// </summary>
        public static List<PortraitFix> Fixes(PortraitRecord before, PortraitRecord after)
        {
            var list = new List<PortraitFix>();
            if (before == null || after == null) return list;
            if (!SameKinds(before.races, after.races)) list.Add(new PortraitFix("race", KindsValue(after.races, "human")));
            if (!SameKinds(before.xenotypes, after.xenotypes)) list.Add(new PortraitFix("xenotype", KindsValue(after.xenotypes, "baseliner")));
            if (before.sex != after.sex) list.Add(new PortraitFix("sex", PortraitLibraryMath.Word(after.sex)));
            if (before.age != after.age) list.Add(new PortraitFix("age", PortraitLibraryMath.Word(after.age)));
            if (after.skinSet && (!before.skinSet || PortraitLibraryMath.Hex(before.skin) != PortraitLibraryMath.Hex(after.skin)))
                list.Add(new PortraitFix("skin", "#" + PortraitLibraryMath.Hex(after.skin)));
            if (before.hairColourSet != after.hairColourSet || after.hairColourSet && PortraitLibraryMath.Hex(before.hairColour) != PortraitLibraryMath.Hex(after.hairColour))
                list.Add(new PortraitFix("hairColour", after.hairColourSet ? "#" + PortraitLibraryMath.Hex(after.hairColour) : "none"));
            if (before.hair != after.hair) list.Add(new PortraitFix("hair", EnumWord(after.hair)));
            if (before.beard != after.beard) list.Add(new PortraitFix("beard", EnumWord(after.beard)));
            if (before.build != after.build) list.Add(new PortraitFix("build", EnumWord(after.build)));
            if (before.era != after.era) list.Add(new PortraitFix("era", after.era == PortraitEra.Unknown ? "any" : EnumWord(after.era)));
            if (before.dress != after.dress) list.Add(new PortraitFix("dress", after.dress == PortraitDress.Unknown ? "any" : EnumWord(after.dress)));
            Changes(list, "features", before.features, after.features, StringComparer.Ordinal);
            Changes(list, "marks", MarkWords(before), MarkWords(after), StringComparer.Ordinal);
            if (before.side != after.side) list.Add(new PortraitFix("side", after.side == PortraitSide.None ? "clear" : PortraitLibraryMath.SideWord(after.side)));
            Changes(list, "looks", before.looks, after.looks, StringComparer.OrdinalIgnoreCase);
            if (!string.Equals(before.role ?? "", after.role ?? "", StringComparison.Ordinal)) list.Add(new PortraitFix("role", string.IsNullOrEmpty(after.role) ? "clear" : after.role));
            if (before.headwear != after.headwear) list.Add(new PortraitFix("headwear", after.headwear ? "true" : "false"));
            return list;
        }

        /// <summary>A word of the records' own (short, medium, broad ...): the value's name in small letters.</summary>
        public static string EnumWord<T>(T v) where T : struct => v.ToString().ToLowerInvariant();

        private static bool SameKinds(List<string> a, List<string> b) =>
            new HashSet<string>(a ?? new List<string>(), StringComparer.OrdinalIgnoreCase).SetEquals(b ?? new List<string>());

        /// <summary>A race's or xenotype's line value: its def names joined by ";", or the word for none (human, baseliner).</summary>
        private static string KindsValue(List<string> kinds, string none) => kinds == null || kinds.Count == 0 ? none : string.Join(";", kinds);

        /// <summary>The def names of a race's or xenotype's line ("Yttakin", "Highmate;Hussar"), small letters, without the word for none.</summary>
        private static List<string> KindsOf(string v, string none)
        {
            var list = new List<string>();
            foreach (var part in (v ?? "").Split(';', ',', '|'))
            {
                string w = part.Trim().ToLowerInvariant();
                if (w.Length == 0 || w.Contains(" ") || list.Contains(w)) continue;
                list.Add(w);
            }
            list.Remove(none);
            return list;
        }

        private static bool SetWord<T>(string field, string v, ref T into) where T : struct
        {
            if (!PortraitLibraryMath.TryEnum(PortraitLibraryMath.Synonym(field, v), out T w)) return false;
            into = w;
            return true;
        }

        private static void Changes(List<PortraitFix> list, string field, IEnumerable<string> before, IEnumerable<string> after, StringComparer cmp)
        {
            var b = new HashSet<string>(before ?? Enumerable.Empty<string>(), cmp);
            var a = new HashSet<string>(after ?? Enumerable.Empty<string>(), cmp);
            foreach (var w in b.Where(w => !a.Contains(w)).OrderBy(w => w, StringComparer.Ordinal)) list.Add(new PortraitFix(field, "remove:" + w));
            foreach (var w in a.Where(w => !b.Contains(w)).OrderBy(w => w, StringComparer.Ordinal)) list.Add(new PortraitFix(field, "add:" + w));
        }

        /// <summary>
        /// One line applied to a record, as Update-PortraitLibrary applies a yes line (and features and side the same way): the
        /// sex, the age band (the records' words), the work (clear for none), headwear, a look, mark or feature added or taken
        /// away (a special look marked so), the side. False where the line is none it knows (nothing changed).
        /// </summary>
        public static bool Apply(PortraitRecord r, PortraitFix f)
        {
            if (r == null || f == null) return false;
            string v = (f.value ?? "").Trim();
            switch (FieldKey(f.field))
            {
                case "race":
                {
                    if (v.Length == 0) return false;
                    r.races = KindsOf(v, "human");
                    return true;
                }
                case "xenotype":
                {
                    if (v.Length == 0) return false;
                    r.xenotypes = KindsOf(v, "baseliner");
                    return true;
                }
                case "skin":
                {
                    if (!PortraitLibraryMath.TryColour(v, out var c)) return false;
                    r.skin = c;
                    r.skinSet = true;
                    return true;
                }
                case "haircolour":
                {
                    string t = v.ToLowerInvariant();
                    if (t == "none" || t == "clear" || t == "-")
                    {
                        r.hairColourSet = false;
                        return true;
                    }
                    if (!PortraitLibraryMath.TryColour(v, out var c)) return false;
                    r.hairColour = c;
                    r.hairColourSet = true;
                    return true;
                }
                case "hair":
                    return SetWord("hair", v, ref r.hair);
                case "beard":
                    return SetWord("beard", v, ref r.beard);
                case "build":
                    return SetWord("build", v, ref r.build);
                case "era":
                    if (Any(v))
                    {
                        r.era = PortraitEra.Unknown;
                        return true;
                    }
                    return SetWord("era", v, ref r.era);
                case "dress":
                    if (Any(v))
                    {
                        r.dress = PortraitDress.Unknown;
                        return true;
                    }
                    return SetWord("dress", v, ref r.dress);
                case "sex":
                {
                    string t = v.ToLowerInvariant();
                    if (t == "m" || t == "male" || t == "man") r.sex = PortraitSex.Male;
                    else if (t == "f" || t == "female" || t == "woman") r.sex = PortraitSex.Female;
                    else if (t == "any") r.sex = PortraitSex.Any;
                    else return false;
                    return true;
                }
                case "age":
                {
                    string t = PortraitLibraryMath.Synonym("age", v);
                    foreach (PortraitAge a in Enum.GetValues(typeof(PortraitAge)))
                        if (string.Equals(PortraitLibraryMath.Word(a), (t ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            r.age = a;
                            return true;
                        }
                    return false;
                }
                case "role":
                {
                    string t = v.ToLowerInvariant();
                    if (t == "clear" || t == "none" || t == "-")
                    {
                        r.role = "";
                        return true;
                    }
                    string role = PortraitLookMath.RoleWord(v);
                    if (role == null) return false;
                    r.role = role;
                    return true;
                }
                case "headwear":
                {
                    string t = v.ToLowerInvariant();
                    if (t == "true" || t == "yes" || t == "1") r.headwear = true;
                    else if (t == "false" || t == "no" || t == "0") r.headwear = false;
                    else return false;
                    return true;
                }
                case "side":
                {
                    if (v.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        r.side = PortraitSide.None;
                        return true;
                    }
                    var s = PortraitLibraryMath.SideOf(v);
                    if (s == null || v.Length == 0) return false;
                    r.side = s.Value;
                    return true;
                }
                case "looks":
                    return Change(v, out bool addLook, out string look) && ChangeLook(r, addLook, look);
                case "marks":
                    return Change(v, out bool addMark, out string mark) && ChangeMark(r, addMark, mark);
                case "features":
                {
                    if (!Change(v, out bool add, out string w)) return false;
                    string k = PortraitLibraryMath.Features.FirstOrDefault(x => string.Equals(x, w, StringComparison.OrdinalIgnoreCase));
                    if (k == null) return false;
                    if (add) r.features.Add(k);
                    else r.features.Remove(k);
                    return true;
                }
            }
            return false;
        }

        /// <summary>A line's field as <see cref="Apply"/> knows it: small letters, spaces and underscores taken out (hair colour and haircolor are haircolour; kind is xenotype).</summary>
        public static string FieldKey(string field)
        {
            string t = (field ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (t == "haircolor") return "haircolour";
            if (t == "skincolor" || t == "skincolour") return "skin";
            if (t == "kind" || t == "xenotypes") return "xenotype";
            if (t == "races") return "race";
            return t;
        }

        private static bool Any(string v)
        {
            string t = (v ?? "").Trim().ToLowerInvariant();
            return t == "any" || t == "unknown" || t == "clear" || t == "-";
        }

        /// <summary>"add:word" or "remove:word" (spaces round the colon allowed), as the scripts read it.</summary>
        private static bool Change(string v, out bool add, out string word)
        {
            add = false;
            word = null;
            int at = (v ?? "").IndexOf(':');
            if (at <= 0) return false;
            string how = v.Substring(0, at).Trim().ToLowerInvariant();
            word = v.Substring(at + 1).Trim();
            if (word.Length == 0 || word.Contains(" ")) return false;
            if (how == "add") add = true;
            else if (how != "remove") return false;
            return true;
        }

        private static bool ChangeLook(PortraitRecord r, bool add, string word)
        {
            var look = PortraitLookMath.Book.Find(word);
            string w = look?.word ?? word;
            if (!add)
            {
                r.looks.Remove(w);
                r.specials.Remove(w);
                return true;
            }
            if (look == null) return false;
            r.looks.Add(look.word);
            if (look.special) r.specials.Add(look.word);
            return true;
        }

        private static bool ChangeMark(PortraitRecord r, bool add, string word)
        {
            string m = PortraitLibraryMath.Marks.FirstOrDefault(x => string.Equals(x, word, StringComparison.OrdinalIgnoreCase));
            if (m == null) return false;
            string k = PortraitLibraryMath.MarkKey(m);
            if (add)
            {
                r.marks.Add(k);
                if (m == "eyePatch") r.patch = true;
                else if (m == "lostEye") r.patch = false;
                return true;
            }
            if (m == "eyePatch" && !r.patch) return true;
            r.marks.Remove(k);
            if (k == "lostEye") r.patch = false;
            return true;
        }

        // ---------------- corrections.csv ----------------

        /// <summary>One line of corrections.csv, its cells split by <paramref name="delim"/>.</summary>
        public static string Line(string picture, PortraitFix f, string apply, string why, char delim = ',') =>
            string.Join(delim.ToString(), new[] { picture, f.field, f.value, apply, why }.Select(v => PortraitDeleteMath.Quote(v, delim)));

        /// <summary>
        /// corrections.csv's text with a picture's corrections made in the game, each marked yes with the reason "set in the
        /// game" (the player, 23 November 2026: "the corrections i make through the game client are always prioritized"):
        /// where the file already has a line set in the game for the same picture and field (and for a list field, marks,
        /// looks or features, the same word), that line takes the new value; otherwise the line goes at the file's end. A new
        /// file is the columns and the lines; an old one keeps its own separator and line endings, every other line as it was
        /// (the picture check's, the player's own).
        /// </summary>
        public static string Append(string text, string picture, IEnumerable<PortraitFix> fixes, string why = Why)
        {
            var list = (fixes ?? Enumerable.Empty<PortraitFix>()).Where(f => f != null).ToList();
            string t = (text ?? "").TrimStart('﻿');
            if (string.IsNullOrWhiteSpace(t))
                return string.Join(",", Columns) + "\r\n" + string.Concat(list.Select(f => Line(picture, f, "yes", why) + "\r\n"));
            string nl = t.Contains("\r\n") || !t.Contains("\n") ? "\r\n" : "\n";
            char delim = PortraitDeleteMath.DelimiterOf(t);
            var lines = t.Replace("\r\n", "\n").Split('\n').ToList();
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            // The columns by the file's first line, to find the game's own lines.
            int head = lines.FindIndex(l => l.Trim().Length > 0);
            var cols = head >= 0 ? PortraitDeleteMath.Cells(lines[head].Trim(), delim).Select(c => c.Trim().ToLowerInvariant()).ToList() : new List<string>();
            int cPic = cols.IndexOf("picture"), cField = cols.IndexOf("field"), cValue = cols.IndexOf("value"), cWhy = cols.IndexOf("why");
            string Cell(List<string> cells, int at) => at >= 0 && at < cells.Count ? cells[at].Trim() : "";
            string pic = PictureKey(picture);
            foreach (var f in list)
            {
                string line = Line(picture, f, "yes", why, delim);
                int found = -1;
                if (head >= 0 && cPic >= 0 && cField >= 0)
                    for (int i = head + 1; i < lines.Count; i++)
                    {
                        var cells = PortraitDeleteMath.Cells(lines[i].Trim(), delim);
                        if (PictureKey(Cell(cells, cPic)) != pic || !string.Equals(Cell(cells, cField), f.field, StringComparison.OrdinalIgnoreCase)
                                                             || !string.Equals(Cell(cells, cWhy), Why, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ListField(f.field) && !string.Equals(WordOf(Cell(cells, cValue)), WordOf(f.value), StringComparison.OrdinalIgnoreCase)) continue;
                        found = i;
                    }
                if (found >= 0) lines[found] = line;
                else lines.Add(line);
            }
            return string.Join(nl, lines) + nl;
        }

        /// <summary>A field whose lines are add:word or remove:word, one a word.</summary>
        public static bool ListField(string field) =>
            string.Equals(field, "marks", StringComparison.OrdinalIgnoreCase) || string.Equals(field, "looks", StringComparison.OrdinalIgnoreCase)
                                                                           || string.Equals(field, "features", StringComparison.OrdinalIgnoreCase);

        /// <summary>The word of an add: or remove: line's value ("" for none).</summary>
        private static string WordOf(string value)
        {
            int at = (value ?? "").IndexOf(':');
            return at >= 0 ? value.Substring(at + 1).Trim() : "";
        }

        /// <summary>
        /// A picture as corrections.csv may name it, as one key: its batch's folder and file or the file alone, either slash,
        /// "library\" before it or not, the case aside (as Update-PortraitLibrary reads it).
        /// </summary>
        public static string PictureKey(string picture)
        {
            string p = (picture ?? "").Trim().Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            if (p.StartsWith("library/", StringComparison.Ordinal)) p = p.Substring(8);
            return p;
        }

        /// <summary>
        /// Whether a version of a picture takes a correction of the picture too: every version (its expressions, made from
        /// it) but a mark variant's own mark and side.
        /// </summary>
        public static bool Carries(PortraitRecord version, PortraitFix f)
        {
            if (version == null || f == null) return false;
            if (!version.IsMarkVariant) return true;
            string own = PortraitLibraryMath.MarkOfVariant(version.variant, out var side);
            if (f.field == "side") return side == PortraitSide.None;
            if (f.field != "marks") return true;
            int at = f.value.IndexOf(':');
            return at < 0 || PortraitLibraryMath.MarkKey(f.value.Substring(at + 1).Trim()) != PortraitLibraryMath.MarkKey(own);
        }

        /// <summary>
        /// The corrections made in the game, taken again when the library is read (the player, 23 November 2026: "the
        /// corrections i make through the game client are always prioritized ... the library import or other scripts should
        /// not override that again"): each line of corrections.csv marked yes and "set in the game", in the file's order,
        /// applied to its picture's record (by its batch's folder and file, or the file alone) and its versions' (as
        /// <see cref="Carries"/> says), so a records.xml written without them (an Update run that missed one) still shows them
        /// in the game. Returns how many records it changed.
        /// </summary>
        public static int ApplyGameLines(IList<FolderPortrait> people, string correctionsText)
        {
            if (people == null || people.Count == 0 || string.IsNullOrWhiteSpace(correctionsText)) return 0;
            var lines = Read(correctionsText).Where(c => string.Equals(c[3], "yes", StringComparison.OrdinalIgnoreCase) && string.Equals(c[4], Why, StringComparison.OrdinalIgnoreCase)).ToList();
            if (lines.Count == 0) return 0;
            var byPath = new Dictionary<string, FolderPortrait>(StringComparer.Ordinal);
            var byFile = new Dictionary<string, FolderPortrait>(StringComparer.Ordinal);
            foreach (var p in people)
            {
                if (p?.record == null || p.record.variantOf.Length > 0) continue;
                string key = PictureKey(PortraitFolderMath.FileKey(p.texPath));
                byPath[key] = p;
                string file = key.Substring(key.LastIndexOf('/') + 1);
                if (!byFile.ContainsKey(file)) byFile[file] = p;
            }
            var pairs = people.Where(p => p?.record != null).Select(p => new KeyValuePair<string, string>(p.record.name, p.record.variantOf)).ToList();
            var byName = new Dictionary<string, PortraitRecord>(StringComparer.Ordinal);
            foreach (var p in people)
                if (p?.record != null && !byName.ContainsKey(p.record.name))
                    byName[p.record.name] = p.record;
            var changed = new HashSet<PortraitRecord>();
            foreach (var c in lines)
            {
                string key = PictureKey(c[0]);
                if (!byPath.TryGetValue(key, out var root) && !byFile.TryGetValue(key.Substring(key.LastIndexOf('/') + 1), out root)) continue;
                var fix = new PortraitFix(c[1].ToLowerInvariant(), c[2]);
                foreach (var name in PortraitDeleteMath.RecordsOf(root.record.name, pairs))
                {
                    if (!byName.TryGetValue(name, out var r) || r != root.record && !Carries(r, fix)) continue;
                    var before = Copy(r);
                    if (Apply(r, fix) && Fixes(before, r).Count > 0) changed.Add(r);
                }
            }
            return changed.Count;
        }

        /// <summary>corrections.csv's lines as (picture, field, value, apply, why), each read by its columns' names (a file saved again by Excel too).</summary>
        public static List<string[]> Read(string text)
        {
            var list = new List<string[]>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char delim = PortraitDeleteMath.DelimiterOf(text);
            List<string> head = null;
            foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var cells = PortraitDeleteMath.Cells(line, delim);
                if (head == null)
                {
                    head = cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
                    continue;
                }
                string Cell(string col)
                {
                    int at = head.IndexOf(col);
                    return at >= 0 && at < cells.Count ? cells[at].Trim() : "";
                }
                list.Add(new[] { Cell("picture"), Cell("field"), Cell("value"), Cell("apply"), Cell("why") });
            }
            return list;
        }

        // ---------------- records.xml ----------------

        /// <summary>
        /// records.xml's text with the named records' corrected fields written as the records are now (sex, age, features,
        /// marks, side, looks, role, headwear: a field set where it has a value, its element taken out where it has none, a
        /// new one added at the record's end), everything else as it was (the other fields and records, the declaration, the
        /// comment, the line endings). The names written are added to <paramref name="patched"/>. Throws where the text is no
        /// XML at all.
        /// </summary>
        public static string WithRecords(string xml, IDictionary<string, PortraitRecord> records, List<string> patched)
        {
            var doc = XDocument.Parse(xml ?? "", LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null || records == null || records.Count == 0) return xml;
            int n = 0, done = 0;
            foreach (var e in root.Elements().ToList())
            {
                n++;
                if (e.Name.LocalName != "ColonyCouncil.PortraitDef") continue;
                string name = e.Element("defName")?.Value?.Trim() ?? "";
                if (name.Length == 0) name = "Portrait_" + n;
                if (!records.TryGetValue(name, out var r) || r == null) continue;
                SetKinds(e, "races", r.races, "human");
                SetKinds(e, "xenotypes", r.xenotypes, "baseliner");
                SetOne(e, "sex", PortraitLibraryMath.Word(r.sex));
                SetOne(e, "age", PortraitLibraryMath.Word(r.age));
                SetColour(e, "skinColor", r.skinSet, r.skin);
                SetColour(e, "hairColor", r.hairColourSet, r.hairColour);
                SetWord(e, "hair", r.hair, PortraitHairStyle.Short, true);
                SetWord(e, "beard", r.beard, PortraitBeard.None, true);
                SetWord(e, "build", r.build, PortraitBuild.Average, true);
                SetWord(e, "era", r.era, PortraitEra.Unknown, false);
                SetWord(e, "dress", r.dress, PortraitDress.Unknown, false);
                SetList(e, "features", PortraitLibraryMath.Features.Where(r.features.Contains));
                SetList(e, "marks", MarkWords(r));
                SetOne(e, "side", PortraitLibraryMath.SideWord(r.side));
                SetList(e, "looks", r.looks.OrderBy(w => w, StringComparer.OrdinalIgnoreCase));
                SetOne(e, "role", r.role);
                SetOne(e, "headwear", r.headwear ? "true" : "");
                patched?.Add(name);
                done++;
            }
            return done == 0 ? xml : PortraitDeleteMath.Written(doc, xml);
        }

        /// <summary>The whitespace a record's fields stand after ("\r\n    "), or a new line and four spaces.</summary>
        private static string Indent(XElement e)
        {
            foreach (var node in e.Nodes())
                if (node is XText t && string.IsNullOrWhiteSpace(t.Value))
                    return t.Value;
                else if (node is XElement)
                    break;
            return "\n    ";
        }

        private static void Remove(XElement x)
        {
            if (x.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value)) t.Remove();
            x.Remove();
        }

        /// <summary>A new field at the record's end, before the whitespace its closing tag stands after.</summary>
        private static void Add(XElement e, XElement field)
        {
            string indent = Indent(e);
            if (e.LastNode is XText last && string.IsNullOrWhiteSpace(last.Value))
            {
                last.AddBeforeSelf(new XText(indent));
                last.AddBeforeSelf(field);
            }
            else
            {
                e.Add(new XText(indent));
                e.Add(field);
            }
        }

        private static void SetOne(XElement e, string tag, string value)
        {
            var x = e.Element(tag);
            if (string.IsNullOrEmpty(value))
            {
                if (x != null) Remove(x);
                return;
            }
            if (x != null)
            {
                if (x.HasElements || x.Value != value)
                {
                    x.RemoveNodes();
                    x.Value = value;
                }
                return;
            }
            Add(e, new XElement(tag, value));
        }

        /// <summary>
        /// A record's races or xenotypes, written only where they differ from what the record's text says (the case and the
        /// word for none aside), so a picture whose kind was not corrected keeps its lines as the scripts wrote them.
        /// </summary>
        private static void SetKinds(XElement e, string tag, List<string> kinds, string none)
        {
            var had = new HashSet<string>(e.Element(tag)?.Elements("li").Select(li => li.Value.Trim().ToLowerInvariant()).Where(w => w.Length > 0) ?? Enumerable.Empty<string>());
            had.Remove(none);
            if (had.SetEquals(kinds ?? new List<string>())) return;
            SetList(e, tag, kinds);
        }

        /// <summary>A colour, written (#rrggbb) only where it differs from the one the record's text gives; taken out where unset.</summary>
        private static void SetColour(XElement e, string tag, bool set, PortraitColour c)
        {
            bool had = PortraitLibraryMath.TryColour(e.Element(tag)?.Value, out var was);
            if (had == set && (!set || PortraitLibraryMath.Hex(was) == PortraitLibraryMath.Hex(c))) return;
            SetOne(e, tag, set ? "#" + PortraitLibraryMath.Hex(c) : "");
        }

        /// <summary>A word field (hair, beard, build, era, dress), written only where it differs from the one the record's text gives (unset: <paramref name="unset"/>); the value for not told taken out where <paramref name="write"/> is false.</summary>
        private static void SetWord<T>(XElement e, string tag, T value, T unset, bool write) where T : struct
        {
            string text = e.Element(tag)?.Value;
            T was = string.IsNullOrWhiteSpace(text) || !PortraitLibraryMath.TryEnum(PortraitLibraryMath.Synonym(tag, text), out T w) ? unset : w;
            if (EqualityComparer<T>.Default.Equals(was, value)) return;
            SetOne(e, tag, !write && EqualityComparer<T>.Default.Equals(value, unset) ? "" : EnumWord(value));
        }

        /// <summary>A list field as the scripts write it, on one line (<c>&lt;marks&gt;&lt;li&gt;scar&lt;/li&gt;&lt;/marks&gt;</c>); taken out where empty.</summary>
        private static void SetList(XElement e, string tag, IEnumerable<string> items)
        {
            var list = (items ?? Enumerable.Empty<string>()).Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
            var x = e.Element(tag);
            if (list.Count == 0)
            {
                if (x != null) Remove(x);
                return;
            }
            var had = x?.Elements("li").Select(li => li.Value.Trim()).ToList();
            if (had != null && had.SequenceEqual(list)) return;
            var fresh = new XElement(tag, list.Select(w => new XElement("li", w)));
            if (x != null) x.ReplaceWith(fresh);
            else Add(e, fresh);
        }

        // ---------------- The writing ----------------

        /// <summary>
        /// The whole writing, on a worker thread (<paramref name="wait"/> sleeps it between tries): corrections.csv read, the
        /// lines added and written safely; then records.xml read, the records' fields written and written safely; each tried
        /// again a few times. The list first, so a change written there stands after Update-PortraitLibrary even where
        /// records.xml can't be written now.
        /// </summary>
        public static CorrectResult Run(CorrectJob job, Action<int> wait)
        {
            var r = new CorrectResult();
            string listPath = Path.Combine(job.folder, ListFile), recordsPath = Path.Combine(job.folder, PortraitDeleteMath.RecordsFile);
            bool listed = PortraitDeleteMath.Attempt(() =>
            {
                string text = PortraitDeleteMath.TextOf(File.Exists(listPath) ? File.ReadAllBytes(listPath) : null) ?? "";
                foreach (var kv in job.lines) text = Append(text, kv.Key, kv.Value);
                PortraitDeleteMath.WriteSafely(listPath, text, true);
            }, job.tries, wait, out var listErr);
            if (!listed)
            {
                r.error = listErr?.Message ?? "";
                return r;
            }
            r.listed = true;
            if (!File.Exists(recordsPath)) return r;
            var patched = new List<string>();
            bool written = PortraitDeleteMath.Attempt(() =>
            {
                patched.Clear();
                string xml = File.ReadAllText(recordsPath);
                string after = WithRecords(xml, job.records, patched);
                if (!ReferenceEquals(after, xml) && after != xml) PortraitDeleteMath.WriteSafely(recordsPath, after, false);
            }, job.tries, wait, out var recErr);
            if (!written)
            {
                r.error = recErr?.Message ?? "";
                return r;
            }
            r.recorded = true;
            r.recordsPatched = patched.Count;
            return r;
        }
    }
}
