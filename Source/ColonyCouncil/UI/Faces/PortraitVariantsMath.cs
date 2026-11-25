using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// One line of the library folder's variants-wanted.csv: a version of a picture the game wants painted, for the
    /// portrait scripts (Make-Variants takes these first). The picture's path from the library folder (forward slashes,
    /// with .png), the word (an expression or a mark variant), the exact instruction for the edit model, who wants it,
    /// its priority (1 the colony's own, 2 a notable's), and the date it was first wanted.
    /// </summary>
    public sealed class VariantWanted
    {
        public string picture = "", word = "", instruction = "", who = "", first = "";
        public int priority = 2;
        /// <summary>The record of the picture it is a version of (not written: the game leaves out what the library already has by it).</summary>
        public string record = "";
        /// <summary>Who wants it, the colony's order: 0 a colonist, 1 a slave, 2 a prisoner, 3 a notable (not written: the rows' order).</summary>
        public int rank;

        /// <summary>A row's key: its picture (the letters' case aside, as on Windows) and its word.</summary>
        public string Key => (picture ?? "").ToLowerInvariant() + "|" + (word ?? "");
    }

    /// <summary>
    /// The versions of a picture the game wants painted (docs: DESIGN.md, "Portraits: about this picture, and the variants
    /// the game wants"; the player, 23 November 2026: "variants are first and foremost relevant for my actual colonists ...
    /// if a pawn loses an eye ... it then creates an entry, create a one-eyed ... variant of this specific image that is
    /// currently used for the pawn", and "it's also important that a variant is then marked as done once it's created and
    /// in the game, not that it will get created over and over"), without game types (Tools/Tests try it):
    /// <list type="bullet">
    /// <item>which expressions a picture shown for one of the colony's own lacks (<see cref="ExpressionsWanted"/>), and which
    /// mark variants a person needs of the picture they hold (<see cref="MarksWanted"/>): a mark of theirs (on its side)
    /// that neither the picture nor any variant of it shows;</item>
    /// <item>each word's instruction for the edit model (<see cref="Instruction"/>), the expressions in Make-Variants' own
    /// words;</item>
    /// <item>a variant's file beside its picture (<see cref="VariantFile"/>): <c>&lt;stem&gt;_v-&lt;word&gt;.png</c>, or
    /// <c>-2</c>, <c>-3</c> ... where deleted.csv has that name;</item>
    /// <item>the list (<see cref="Rows"/>, <see cref="Csv"/>, <see cref="Read"/>): one row per picture and word, never one the
    /// library has (done), the date first wanted kept from the file as it was.</item>
    /// </list>
    /// </summary>
    public static class PortraitVariantsMath
    {
        public const string ListFile = "variants-wanted.csv";

        /// <summary>variants-wanted.csv's columns, in the order written.</summary>
        public static readonly string[] Columns = { "picture", "word", "instruction", "who", "priority", "first" };

        /// <summary>
        /// Every word, in the list's order: the expressions, then the mark variants, then the conditions
        /// (<see cref="PortraitLifeMath.Conditions"/>, a list of their own: Rimfolk Studio treats every word that is neither an
        /// expression nor a mark as a condition).
        /// </summary>
        public static readonly string[] Words = PortraitLifeMath.Faces.Concat(PortraitLibraryMath.MarkVariantWords).Concat(PortraitLifeMath.Conditions).ToArray();

        /// <summary>A word's place in the list's order (an unknown word last).</summary>
        public static int Order(string word)
        {
            int i = Array.IndexOf(Words, word);
            return i >= 0 ? i : Words.Length;
        }

        // ---------------- The instructions ----------------

        /// <summary>What stays the same in an expression, as Make-Variants.ps1 says it.</summary>
        public const string KeepFace = "Keep everything else exactly the same: the same person and face, hair, clothes, pose, position, framing, lighting and painting style.";

        /// <summary>What stays the same in a mark variant: the face changes only where the mark is.</summary>
        public const string KeepMark = "Keep everything else exactly the same: the same person, the rest of the face, hair, clothes, pose, position, framing, lighting and painting style.";

        /// <summary>
        /// The instruction for the edit model (FLUX Kontext) for a word: the expressions word for word as Make-Variants.ps1
        /// has them; a mark variant plainly what changes and on which side (the person's own left is on the picture's right as
        /// they face it), then what stays. An artificial arm by the clothes' age: a simple one of wood and iron for a tribal or
        /// medieval picture, a bionic one otherwise. Null for a word it doesn't know, and for the conditions
        /// (<see cref="PortraitLifeMath.Conditions"/>): Rimfolk Studio words those itself (the player, 25 November 2026: "the
        /// instruction can stay empty for these words, the Studio's script uses its own wording").
        /// </summary>
        public static string Instruction(string word, PortraitEra era = PortraitEra.Unknown)
        {
            switch (word)
            {
                case "blink": return "Close the person's eyes, as in the middle of a blink, the eyelids fully shut and relaxed. " + KeepFace;
                case "smile": return "Give the person a small, warm smile with the mouth closed. " + KeepFace;
                case "frown": return "Make the person frown, the brows drawn together and down, the mouth set in a slight scowl. " + KeepFace;
                case "pain": return "Make the person wince in pain, the eyes squeezed half shut, the brows drawn up and together, the teeth clenched. " + KeepFace;
                case "scar": return "Give the person an old, healed scar across the face: a pale, slightly raised line from the brow down over the cheek, the eye itself unharmed. " + KeepMark;
                case "burn": return "Give the person an old, healed burn scar on one cheek and the jaw below it: rough, uneven, shiny skin, a little paler and pinker than the rest. " + KeepMark;
            }
            string mark = PortraitLibraryMath.MarkOfVariant(word, out var side);
            if (mark == null || side == PortraitSide.None) return null;
            string own = side == PortraitSide.Left ? "left" : "right", seen = side == PortraitSide.Left ? "right" : "left";
            switch (mark)
            {
                case "lostEye":
                    return "The person has lost their " + own + " eye (the eye on the " + seen + " of the picture): the eyelids closed and a little sunken over the empty socket, with an old, healed scar across them. " + KeepMark;
                case "eyePatch":
                    return "The person has lost their " + own + " eye (the eye on the " + seen + " of the picture) and wears a plain dark eye patch over it, held by a thin strap around the head. " + KeepMark;
                case "bionicEye":
                    return "The person's " + own + " eye (the eye on the " + seen + " of the picture) is replaced by a bionic eye: a small round implant of metal and dark glass set into the socket, with a faint light in it. " + KeepMark;
                case "missingEar":
                    return "The person's " + own + " ear (on the " + seen + " of the picture) is missing: only a healed, scarred stump where the ear was. " + KeepMark;
                case "artificialArm":
                    string arm = era == PortraitEra.Tribal || era == PortraitEra.Medieval
                        ? "a simple artificial arm of carved wood with iron fittings, strapped on with leather"
                        : "a bionic arm of metal plates and joints";
                    return "The person's " + own + " arm (on the " + seen + " of the picture) is " + arm + ", in place of the arm of flesh from the shoulder to the hand. " + KeepMark;
            }
            return null;
        }

        // ---------------- What is wanted ----------------

        /// <summary>
        /// The conditions a picture lacks (<paramref name="had"/>: the words it has), in the list's order (the first six first:
        /// asleep, tired, hurt, wounded, ill, freezing), for the colony's own people as their expressions are.
        /// </summary>
        public static List<string> ConditionsWanted(ICollection<string> had)
        {
            var list = new List<string>();
            foreach (var c in PortraitLifeMath.Conditions)
                if (had == null || !had.Contains(c))
                    list.Add(c);
            return list;
        }

        /// <summary>The expressions a picture lacks (<paramref name="had"/>: the words it has), in the list's order: blink, smile, frown, pain.</summary>
        public static List<string> ExpressionsWanted(ICollection<string> had)
        {
            var list = new List<string>();
            foreach (var f in PortraitLifeMath.Faces)
                if (had == null || !had.Contains(f))
                    list.Add(f);
            return list;
        }

        /// <summary>
        /// The mark variants a person needs of the picture they hold: each mark of theirs (a lost eye, covered by an eye patch
        /// where they wear one; a bionic eye, a scar, a burn, a missing ear, an artificial arm), a sided one for each side it is
        /// on (the left where the side isn't told), that neither the picture nor any of its mark variants
        /// (<paramref name="variants"/>) shows on that side (a lost eye with an eye patch, or without, as they have it). Each a
        /// word of <see cref="PortraitLibraryMath.MarkVariantWords"/>, in the list's order.
        /// </summary>
        public static List<string> MarksWanted(PortraitLook l, PortraitRecord picture, IEnumerable<PortraitRecord> variants)
        {
            var list = new List<string>();
            if (l == null || picture == null || l.marks.Count == 0) return list;
            var versions = new List<PortraitRecord> { picture };
            if (variants != null) versions.AddRange(variants.Where(v => v != null));
            foreach (var key in l.marks)
            {
                string mark = key == "lostEye" && l.eyePatch ? "eyePatch" : key;
                if (Array.IndexOf(PortraitLibraryMath.Marks, mark) < 0) continue;
                var sides = new List<PortraitSide>();
                if (PortraitLibraryMath.Sided(mark))
                {
                    var on = l.SidesOf(key);
                    if ((on & PortraitSide.Left) != 0 || on == PortraitSide.None) sides.Add(PortraitSide.Left);
                    if ((on & PortraitSide.Right) != 0) sides.Add(PortraitSide.Right);
                }
                else sides.Add(PortraitSide.None);
                foreach (var side in sides)
                {
                    bool shown = versions.Any(v => v.marks.Contains(key) && (key != "lostEye" || v.patch == l.eyePatch)
                                                   && (side == PortraitSide.None || v.side == PortraitSide.None || v.side == side));
                    if (shown) continue;
                    string word = PortraitLibraryMath.VariantWordFor(mark, side);
                    if (word != null && !list.Contains(word)) list.Add(word);
                }
            }
            return list.OrderBy(Order).ToList();
        }

        // ---------------- A variant's file ----------------

        /// <summary>
        /// The file a variant of a picture is made as, its path from the library folder with forward slashes: beside the
        /// picture, its name with <c>_v-</c> and the word (<c>colonists/X_v-lostEyeLeft.png</c>); where deleted.csv has that
        /// name (<paramref name="deleted"/>: names without .png, the case aside) the next free of <c>-2</c>, <c>-3</c> ...
        /// (a variant deleted for good may be made again). <paramref name="picture"/> is the picture's path, with or without .png.
        /// </summary>
        public static string VariantFile(string picture, string word, ICollection<string> deleted)
        {
            string t = (picture ?? "").Replace('\\', '/').TrimStart('/');
            if (t.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 4);
            string dir = PortraitDeleteMath.BatchOf(t), stem = PortraitDeleteMath.StemOf(t);
            string name = stem + "_v-" + word;
            bool Taken(string n) => deleted != null && (deleted.Contains(n) || deleted.Contains(n.ToLowerInvariant()));
            if (Taken(name))
                for (int k = 2; k < 1000; k++)
                {
                    string next = name + "-" + k.ToString(CultureInfo.InvariantCulture);
                    if (Taken(next)) continue;
                    name = next;
                    break;
                }
            return (dir.Length > 0 ? dir + "/" : "") + name + ".png";
        }

        // ---------------- The list ----------------

        /// <summary>
        /// The list to write: one row per picture and word (the wishes of several people for the same one together: their names
        /// joined, the most urgent priority and rank), never one the library has (<paramref name="made"/>: the record of the
        /// picture and the word, as records.xml has its variants; done), the date first wanted from the file as it was
        /// (<paramref name="old"/>) or <paramref name="today"/>; ordered by priority, then the colony's own before others
        /// (colonists, slaves, prisoners), then who, then the picture, then the word.
        /// </summary>
        public static List<VariantWanted> Rows(IEnumerable<VariantWanted> fresh, IEnumerable<VariantWanted> old, string today, ICollection<string> made)
        {
            var firsts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var o in old ?? Enumerable.Empty<VariantWanted>())
                if (o != null && !string.IsNullOrEmpty(o.first) && (!firsts.TryGetValue(o.Key, out var f) || string.CompareOrdinal(o.first, f) < 0))
                    firsts[o.Key] = o.first;
            var byKey = new Dictionary<string, VariantWanted>(StringComparer.Ordinal);
            var rows = new List<VariantWanted>();
            foreach (var n in fresh ?? Enumerable.Empty<VariantWanted>())
            {
                if (n == null || string.IsNullOrEmpty(n.picture) || string.IsNullOrEmpty(n.word)) continue;
                if (made != null && made.Contains(MadeKey(n.record, n.word))) continue;
                if (byKey.TryGetValue(n.Key, out var had))
                {
                    if (!string.IsNullOrEmpty(n.who) && !had.who.Split(new[] { "; " }, StringSplitOptions.None).Contains(n.who)) had.who = had.who.Length > 0 ? had.who + "; " + n.who : n.who;
                    had.priority = Math.Min(had.priority, n.priority);
                    had.rank = Math.Min(had.rank, n.rank);
                    continue;
                }
                var row = new VariantWanted
                {
                    picture = n.picture, word = n.word, instruction = n.instruction ?? "", who = n.who ?? "", priority = n.priority, record = n.record ?? "", rank = n.rank,
                    first = firsts.TryGetValue(n.Key, out var first) ? first : today ?? ""
                };
                byKey[n.Key] = row;
                rows.Add(row);
            }
            return rows.OrderBy(r => r.priority).ThenBy(r => r.rank).ThenBy(r => r.who, StringComparer.Ordinal).ThenBy(r => r.picture, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => Order(r.word)).ToList();
        }

        /// <summary>The key of a version the library has: its picture's record and its word.</summary>
        public static string MadeKey(string record, string word) => (record ?? "") + "|" + (word ?? "");

        /// <summary>The versions the library has, by <see cref="MadeKey"/>: every expression and mark variant of its records (<c>variantOf</c> and the word).</summary>
        public static HashSet<string> Made(IEnumerable<PortraitRecord> records)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in records ?? Enumerable.Empty<PortraitRecord>())
                if (r != null && r.variantOf.Length > 0)
                    set.Add(MadeKey(r.variantOf, r.variant));
            return set;
        }

        /// <summary>One row as a CSV line in the columns' order.</summary>
        public static string Line(VariantWanted r) =>
            string.Join(",", new[] { r.picture, r.word, r.instruction, r.who, r.priority.ToString(CultureInfo.InvariantCulture), r.first }.Select(v => PortraitDeleteMath.Quote(v, ',')));

        /// <summary>The list's text: the columns, then a row each, Windows' line endings (Excel and the scripts read it as it is).</summary>
        public static string Csv(IEnumerable<VariantWanted> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Columns)).Append("\r\n");
            foreach (var r in rows ?? Enumerable.Empty<VariantWanted>()) sb.Append(Line(r)).Append("\r\n");
            return sb.ToString();
        }

        /// <summary>The rows a variants-wanted.csv's text holds, each read by its columns' names (a file saved again by Excel too); lines without a picture or a word passed over.</summary>
        public static List<VariantWanted> Read(string text)
        {
            var list = new List<VariantWanted>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char delim = PortraitDeleteMath.DelimiterOf(text);
            List<string> head = null;
            foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
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
                var r = new VariantWanted
                {
                    picture = Cell("picture"), word = Cell("word"), instruction = Cell("instruction"), who = Cell("who"), first = Cell("first"),
                    priority = int.TryParse(Cell("priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 2
                };
                if (r.picture.Length > 0 && r.word.Length > 0) list.Add(r);
            }
            return list;
        }
    }
}
