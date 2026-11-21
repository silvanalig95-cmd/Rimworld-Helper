using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColonyCouncil
{
    public enum PortraitSex : byte
    {
        Any,
        Male,
        Female
    }

    public enum PortraitAge : byte
    {
        Child,
        Young,
        Adult,
        Old
    }

    public enum PortraitHairStyle : byte
    {
        Bald,
        Short,
        Medium,
        Long,
        Braids,
        Bun,
        Tied
    }

    public enum PortraitBeard : byte
    {
        None,
        Stubble,
        Short,
        Full,
        Moustache
    }

    public enum PortraitBuild : byte
    {
        Average,
        Slim,
        Heavy,
        Broad
    }

    /// <summary>The age of the clothes: tribal hides and wraps, medieval tunics, industrial jackets, spacer suits; unknown matches any.</summary>
    public enum PortraitEra : byte
    {
        Unknown,
        Tribal,
        Medieval,
        Industrial,
        Spacer
    }

    /// <summary>The kind of clothes: rags, work clothes, fine clothes, robes, armour; unknown matches any.</summary>
    public enum PortraitDress : byte
    {
        Unknown,
        Rags,
        Work,
        Fine,
        Robes,
        Armour
    }

    /// <summary>The way the person in a picture looks: at the viewer, or turned to the picture's left or right (a little, or three-quarter).</summary>
    public enum PortraitFacing : byte
    {
        Front,
        Left,
        Right,
        ThreeQuarterLeft,
        ThreeQuarterRight
    }

    /// <summary>A colour, 0 to 1 a channel.</summary>
    public struct PortraitColour
    {
        public float r, g, b;

        public PortraitColour(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public override string ToString() => $"({r:0.00}, {g:0.00}, {b:0.00})";
    }

    /// <summary>
    /// What one picture of the library shows, read from its def (<c>PortraitDef</c>; the lead writes one per picture from
    /// the picture itself): its kinds, sex, age, skin and hair colours, hair, beard, build, clothes, headwear, features,
    /// marks, facing, and the character it is a variant of.
    /// </summary>
    public sealed class PortraitRecord
    {
        public string name = "";
        /// <summary>The xenotypes and races it shows (def names, small letters); both empty: a baseliner human.</summary>
        public List<string> xenotypes = new List<string>(), races = new List<string>();
        public PortraitSex sex;
        public PortraitAge age = PortraitAge.Adult;
        public PortraitColour skin, hairColour;
        public bool skinSet, hairColourSet;
        public PortraitHairStyle hair = PortraitHairStyle.Short;
        public PortraitBeard beard;
        public PortraitBuild build;
        public PortraitEra era;
        public PortraitDress dress;
        public bool headwear;
        public HashSet<string> features = new HashSet<string>(), marks = new HashSet<string>();
        public PortraitFacing facing;
        public string character = "", variant = "base";

        /// <summary>The character it belongs to: its own name where it is no variant of another.</summary>
        public string Character => string.IsNullOrEmpty(character) ? name : character;

        public override string ToString() => name;
    }

    /// <summary>A person's looks as the game shows them (read by <c>PortraitReader</c>), in the library's words.</summary>
    public sealed class PortraitLook
    {
        /// <summary>Their xenotype's and race's def names in small letters ("" for a xenotype of the player's own making), and whether they are a baseliner.</summary>
        public string xenotype = "", race = "human";
        public bool baseliner = true;
        /// <summary>A race built as a human is (the human body, drawn as the game draws humans).</summary>
        public bool humanShaped = true;
        public PortraitSex sex;
        public PortraitAge age = PortraitAge.Adult;
        public PortraitColour skin, hairColour;
        public bool unusualSkin;
        public PortraitHairStyle hair = PortraitHairStyle.Short;
        public PortraitBeard beard;
        public PortraitBuild build;
        public PortraitEra era;
        public PortraitDress dress;
        /// <summary>Wearing headgear, and the setting shows it.</summary>
        public bool headwear;
        public HashSet<string> features = new HashSet<string>(), marks = new HashSet<string>();

        /// <summary>Their kind, sex and age in words: kept with a pick, so a person who becomes another kind is matched again.</summary>
        public string Kind => (baseliner && race == "human" ? "baseliner" : race + "/" + xenotype) + "_" + PortraitLibraryMath.Word(sex);

        /// <summary>Everything the matcher reads, in words, for the log and for a person kept with no picture.</summary>
        public string Words =>
            Kind + "_" + PortraitLibraryMath.Word(age) + "_skin" + PortraitLibraryMath.Hex(skin) + "_hair" + PortraitLibraryMath.Hex(hairColour) + "_" + hair.ToString().ToLowerInvariant()
            + "_" + beard.ToString().ToLowerInvariant() + "_" + build.ToString().ToLowerInvariant() + "_" + era.ToString().ToLowerInvariant() + "_" + dress.ToString().ToLowerInvariant()
            + (headwear ? "_headwear" : "") + (features.Count > 0 ? "_" + string.Join("+", features.OrderBy(f => f, StringComparer.Ordinal)) : "")
            + (marks.Count > 0 ? "_" + string.Join("+", marks.OrderBy(f => f, StringComparer.Ordinal)) : "");

        /// <summary>
        /// Far from a human's look (a race of its own shape, a skin no human grows, fur, scales, horns, tusks, a snout): with
        /// no pictures of their own kind, the game's own face rather than a baseliner's picture.
        /// </summary>
        public bool FarFromHuman
        {
            get
            {
                if (!humanShaped || unusualSkin) return true;
                foreach (var f in PortraitLibraryMath.FarFeatures)
                    if (features.Contains(f))
                        return true;
                return false;
            }
        }
    }

    /// <summary>A pick: the picture (an index into the records), its character, and its distance; -1 for none.</summary>
    public struct PortraitPick
    {
        public int index;
        public string character;
        public float distance;

        public bool Any => index >= 0;

        public static readonly PortraitPick None = new PortraitPick { index = -1, character = "", distance = float.PositiveInfinity };
    }

    /// <summary>
    /// The portrait library's rules without game types (Tools/Tests try them; docs: DESIGN.md, "Portraits, phase 3: a
    /// library of painted portraits", and <c>docs/portrait-art/PLAN.md</c>):
    /// <list type="bullet">
    /// <item>A picture's record from its def's words, and what is wrong with one (for the game's def errors).</item>
    /// <item>A pawn's looks from the game's words: the age band, hair and beard from their defs' words or the hair
    /// texture's shape, build from the body type, the clothes' age and kind, features from the genes.</item>
    /// <item>The matcher: the kind first (a pawn of a kind with pictures gets only that kind's; a kind with none, the
    /// baseliners' where it looks near human, else the game's own face); then sex and age; then the distance on skin and
    /// hair colour (by colour), hair, beard, build, clothes and features; marks follow the body (a picture never shows a
    /// mark the person hasn't, and one that shows theirs is preferred).</item>
    /// <item>Characters: the nearest few characters, the one fewest others keep, then one by the pawn's own id; kept for
    /// good, its variant chosen anew as the body changes (a lost eye, a scar, a burn, a bionic eye, growing old).</item>
    /// </list>
    /// </summary>
    public static class PortraitLibraryMath
    {
        // ---------------- Words ----------------

        public static string Word(PortraitSex v) => v == PortraitSex.Male ? "male" : v == PortraitSex.Female ? "female" : "any";

        public static string Word(PortraitAge v) => v.ToString().ToLowerInvariant();

        /// <summary>The features a picture or a pawn may have, as the defs write them.</summary>
        public static readonly string[] Features = { "pointedEars", "longEars", "horns", "tusks", "fur", "scales", "gills", "glowingEyes", "markings", "tail", "snout" };

        /// <summary>The features that make a look far from a human's.</summary>
        public static readonly string[] FarFeatures = { "horns", "tusks", "fur", "scales", "snout" };

        /// <summary>The marks of the body a picture or a pawn may have.</summary>
        public static readonly string[] Marks = { "lostEye", "eyePatch", "scar", "burn", "bionicEye", "missingEar" };

        private static string Known(string[] words, string w)
        {
            if (string.IsNullOrEmpty(w)) return null;
            foreach (var k in words)
                if (string.Equals(k, w.Trim(), StringComparison.OrdinalIgnoreCase))
                    return k;
            return null;
        }

        /// <summary>A mark as the matcher counts it: an eye patch is a lost eye.</summary>
        public static string MarkKey(string mark) => mark == "eyePatch" ? "lostEye" : mark;

        private static bool TryEnum<T>(string text, out T value) where T : struct
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
            foreach (var name in Enum.GetNames(typeof(T)))
            {
                if (!string.Equals(name, t, StringComparison.OrdinalIgnoreCase)) continue;
                value = (T)Enum.Parse(typeof(T), name);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A colour as a def writes it: <c>#c49a6c</c>, <c>c49a6c</c>, or <c>(196, 154, 108)</c> / <c>(0.77, 0.6, 0.42)</c> as
        /// the game writes colours (0 to 255 where any channel is above 1).
        /// </summary>
        public static bool TryColour(string text, out PortraitColour c)
        {
            c = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.StartsWith("#")) t = t.Substring(1);
            if (t.Length == 6 && int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
            {
                c = new PortraitColour(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
                return true;
            }
            var parts = t.Trim('(', ')', ' ').Split(',');
            if (parts.Length < 3 || parts.Length > 4) return false;
            var v = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]) || v[i] < 0f)
                    return false;
            float scale = v[0] > 1f || v[1] > 1f || v[2] > 1f ? 255f : 1f;
            c = new PortraitColour(Math.Min(1f, v[0] / scale), Math.Min(1f, v[1] / scale), Math.Min(1f, v[2] / scale));
            return true;
        }

        public static string Hex(PortraitColour c) =>
            ((int)Math.Round(Clamp01(c.r) * 255f)).ToString("x2") + ((int)Math.Round(Clamp01(c.g) * 255f)).ToString("x2") + ((int)Math.Round(Clamp01(c.b) * 255f)).ToString("x2");

        /// <summary>
        /// A picture's record from its def's fields (each the text the def holds; lists as lists), with what is wrong with
        /// it: an unknown word, a missing colour or texture. <paramref name="field"/> gives a field's text, null when unset.
        /// </summary>
        public static PortraitRecord Record(string name, Func<string, string> field, Func<string, IList<string>> list, List<string> problems)
        {
            var r = new PortraitRecord { name = name ?? "" };
            void Bad(string what) => problems?.Add(what);
            if (string.IsNullOrWhiteSpace(field("texPath"))) Bad("no texPath: the picture's texture under Textures, without .png");
            foreach (var x in list("xenotypes") ?? new string[0])
                if (!string.IsNullOrWhiteSpace(x))
                    r.xenotypes.Add(x.Trim().ToLowerInvariant());
            foreach (var x in list("races") ?? new string[0])
                if (!string.IsNullOrWhiteSpace(x))
                    r.races.Add(x.Trim().ToLowerInvariant());
            if (r.xenotypes.Remove("baseliner") && r.xenotypes.Count > 0) Bad("xenotypes: 'baseliner' with others; a baseliner picture lists no xenotype");
            r.races.Remove("human");
            string s = field("sex");
            if (!string.IsNullOrWhiteSpace(s))
            {
                string l = s.Trim().ToLowerInvariant();
                if (l == "m" || l == "male" || l == "man") r.sex = PortraitSex.Male;
                else if (l == "f" || l == "female" || l == "woman") r.sex = PortraitSex.Female;
                else if (l != "any") Bad("sex: '" + s + "' is not male, female or any");
            }
            T Enum<T>(string f, T fallback) where T : struct
            {
                string text = field(f);
                if (string.IsNullOrWhiteSpace(text)) return fallback;
                if (TryEnum(text, out T v)) return v;
                Bad(f + ": '" + text + "' is not one of " + string.Join(", ", System.Enum.GetNames(typeof(T))).ToLowerInvariant());
                return fallback;
            }
            r.age = Enum("age", PortraitAge.Adult);
            r.hair = Enum("hair", PortraitHairStyle.Short);
            r.beard = Enum("beard", PortraitBeard.None);
            r.build = Enum("build", PortraitBuild.Average);
            r.era = Enum("era", PortraitEra.Unknown);
            r.dress = Enum("dress", PortraitDress.Unknown);
            r.facing = Enum("facing", PortraitFacing.Front);
            string hw = field("headwear");
            r.headwear = !string.IsNullOrWhiteSpace(hw) && (hw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || hw.Trim() == "1");
            r.skinSet = TryColour(field("skinColor"), out r.skin);
            if (!r.skinSet) Bad("skinColor: a colour is needed (#rrggbb or (r, g, b)), the skin's or the fur's or scales'");
            r.hairColourSet = TryColour(field("hairColor"), out r.hairColour);
            if (!r.hairColourSet && r.hair != PortraitHairStyle.Bald) Bad("hairColor: a colour is needed for any hair (#rrggbb or (r, g, b))");
            foreach (var f in list("features") ?? new string[0])
            {
                var k = Known(Features, f);
                if (k != null) r.features.Add(k);
                else if (!string.IsNullOrWhiteSpace(f)) Bad("features: '" + f + "' is not one of " + string.Join(", ", Features));
            }
            foreach (var m in list("marks") ?? new string[0])
            {
                var k = Known(Marks, m);
                if (k != null) r.marks.Add(MarkKey(k));
                else if (!string.IsNullOrWhiteSpace(m)) Bad("marks: '" + m + "' is not one of " + string.Join(", ", Marks));
            }
            r.character = (field("character") ?? "").Trim();
            string variant = field("variant");
            r.variant = string.IsNullOrWhiteSpace(variant) ? "base" : variant.Trim();
            return r;
        }

        /// <summary>What is wrong across the library: a character with no base variant, two variants of one character of different sex or kind.</summary>
        public static List<string> LibraryProblems(IList<PortraitRecord> records)
        {
            var problems = new List<string>();
            foreach (var g in records.Where(r => !string.IsNullOrEmpty(r.character)).GroupBy(r => r.character))
            {
                var list = g.ToList();
                if (list.Count > 1 && !list.Any(r => r.variant == "base" && r.marks.Count == 0))
                    problems.Add("character '" + g.Key + "': no variant 'base' without marks, for when the body has none");
                if (list.Select(r => r.sex).Distinct().Count() > 1 || list.Select(r => string.Join(",", r.xenotypes) + "/" + string.Join(",", r.races)).Distinct().Count() > 1)
                    problems.Add("character '" + g.Key + "': its variants are of different sexes or kinds");
            }
            return problems;
        }

        /// <summary>
        /// The library as a number that changes when a picture is added, taken out or renamed (FNV-1a over the names in
        /// order), kept with each pick so a grown library is noticed.
        /// </summary>
        public static int LibrarySignature(IEnumerable<string> names)
        {
            var list = names.Select(n => n ?? "").ToList();
            list.Sort(StringComparer.Ordinal);
            uint h = 2166136261u;
            foreach (var n in list)
            {
                foreach (char c in n)
                {
                    h ^= c;
                    h *= 16777619u;
                }
                h ^= '\n';
                h *= 16777619u;
            }
            return unchecked((int)h);
        }

        // ---------------- A pawn's looks ----------------

        /// <summary>No picture for the youngest: under this age (a baby, a toddler).</summary>
        public const float ChildFrom = 4f;

        /// <summary>Young until this share of their kind's life expectancy (24 for a human), old from this share (60).</summary>
        public const float YoungUntilShare = 0.3f, OldFromShare = 0.75f;

        /// <summary>A child until 13, as the game's own life stages count it.</summary>
        public const float GrownFrom = 13f;

        /// <summary>The age band for a biological age and their kind's life expectancy; false for the youngest (no picture).</summary>
        public static bool AgeOf(float years, float lifeExpectancy, out PortraitAge age)
        {
            age = PortraitAge.Adult;
            if (float.IsNaN(years) || years < ChildFrom) return false;
            float le = lifeExpectancy > 20f && lifeExpectancy < 100000f ? lifeExpectancy : 80f;
            if (years < GrownFrom) age = PortraitAge.Child;
            else if (years < Math.Max(GrownFrom + 5f, YoungUntilShare * le)) age = PortraitAge.Young;
            else if (years >= OldFromShare * le) age = PortraitAge.Old;
            else age = PortraitAge.Adult;
            return true;
        }

        /// <summary>
        /// A skin no human grows (a xenotype's green, blue, purple, deep red, orange or yellow, a waster's grey, ink black, a
        /// cold white): with no pictures of their kind, the game's own face rather than a baseliner's.
        /// </summary>
        public static bool UnusualSkin(PortraitColour c)
        {
            Hsv(c, out float h, out float s, out float v);
            if (v < 0.1f || s > 0.72f) return true;
            if (s < 0.06f && v < 0.9f) return true;
            if (h >= 180f && h <= 300f && s > 0.02f) return true;
            return s > 0.12f && (h < 5f || h > 50f);
        }

        /// <summary>
        /// The words of defs' names, labels, texture paths and tags, split where the case changes or a sign stands
        /// (<c>HalfShaved</c>: "half", "shaved"), in small letters.
        /// </summary>
        public static List<string> Tokens(params string[] texts)
        {
            var list = new List<string>();
            if (texts == null) return list;
            var sb = new StringBuilder();
            void Flush()
            {
                if (sb.Length > 0) list.Add(sb.ToString().ToLowerInvariant());
                sb.Clear();
            }
            foreach (var t in texts)
            {
                if (string.IsNullOrEmpty(t)) continue;
                for (int i = 0; i < t.Length; i++)
                {
                    char c = t[i];
                    if (!char.IsLetter(c))
                    {
                        Flush();
                        continue;
                    }
                    if (char.IsUpper(c) && i > 0 && char.IsLower(t[i - 1])) Flush();
                    sb.Append(c);
                }
                Flush();
            }
            return list;
        }

        private static bool Starts(List<string> tokens, params string[] stems)
        {
            foreach (var t in tokens)
            foreach (var s in stems)
                if (t.StartsWith(s, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>A word found inside any of the words (FauxHawk and Fauxhawk alike).</summary>
        private static bool Has(List<string> tokens, string part)
        {
            foreach (var t in tokens)
                if (t.IndexOf(part, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        private static bool Is(List<string> tokens, params string[] words)
        {
            foreach (var t in tokens)
            foreach (var w in words)
                if (t == w)
                    return true;
            return false;
        }

        /// <summary>
        /// The hair from its def's words, or null where they don't say: braids; a bun or a knot; tied (a ponytail, pigtails,
        /// bunches); long (long, dreadlocks, locks, flowing); short (a mohawk, half or side shaved, a crop, a buzz, spikes,
        /// a tuft, an afro); medium (a bob, shoulder length); bald (bald, shaved).
        /// </summary>
        public static PortraitHairStyle? HairFromWords(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return null;
            if (Starts(tokens, "braid", "plait")) return PortraitHairStyle.Braids;
            if (Starts(tokens, "pony", "pigtail", "bunches", "tied")) return PortraitHairStyle.Tied;
            if (Is(tokens, "bun", "buns", "knot", "topknot") || Starts(tokens, "bun")) return PortraitHairStyle.Bun;
            if (Starts(tokens, "long", "dread", "locks", "flowy", "mane")) return PortraitHairStyle.Long;
            if (Has(tokens, "hawk") || Starts(tokens, "undercut") || Is(tokens, "half", "side") && Starts(tokens, "shave", "cut")) return PortraitHairStyle.Short;
            if (Starts(tokens, "bald", "shave")) return PortraitHairStyle.Bald;
            if (Starts(tokens, "short", "crop", "buzz", "spike", "tuft", "bowl", "pixie", "fringe", "afro", "crew", "flattop")
                || Is(tokens, "cut", "recruit", "military", "soldier")) return PortraitHairStyle.Short;
            if (Is(tokens, "bob", "medium", "shoulder") || Starts(tokens, "bob")) return PortraitHairStyle.Medium;
            return null;
        }

        /// <summary>Where the hair reaches in its front texture, as a share of its height from the top: from here down long (past the jaw), from <see cref="MediumFrom"/> medium.</summary>
        public const float LongFrom = 0.72f, MediumFrom = 0.6f;

        /// <summary>
        /// The hair from the shape of its front texture (each row's cover summed across, from the top, the texture
        /// <paramref name="width"/> wide): hardly anything drawn is bald; reaching past the jaw long, to it medium, else
        /// short. Null when there is nothing to read.
        /// </summary>
        public static PortraitHairStyle? HairFromShape(float[] rowsFromTop, float width)
        {
            if (rowsFromTop == null || rowsFromTop.Length < 8 || !(width > 0f)) return null;
            int n = rowsFromTop.Length, lowest = -1;
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = Math.Max(0f, rowsFromTop[i]);
                total += a;
                if (a > 0.04f * width) lowest = i;
            }
            if (total < 0.02f * n * width || lowest < 0) return PortraitHairStyle.Bald;
            float reach = (lowest + 1f) / n;
            return reach >= LongFrom ? PortraitHairStyle.Long : reach >= MediumFrom ? PortraitHairStyle.Medium : PortraitHairStyle.Short;
        }

        /// <summary>The hair when neither its words nor its texture tell: long for a woman, short for anyone else.</summary>
        public static PortraitHairStyle HairFallback(PortraitSex sex) => sex == PortraitSex.Female ? PortraitHairStyle.Long : PortraitHairStyle.Short;

        /// <summary>
        /// The beard from its def's words: none where the game draws none; stubble for stubble, a shadow; a moustache for a
        /// moustache alone; short for a goatee, a soul patch, a chin beard, a circle, an anchor; else full.
        /// </summary>
        public static PortraitBeard BeardFromWords(List<string> tokens, bool none)
        {
            if (none) return PortraitBeard.None;
            if (tokens == null) return PortraitBeard.Full;
            if (Starts(tokens, "nobeard", "clean") || Is(tokens, "no") && Starts(tokens, "beard")) return PortraitBeard.None;
            if (Starts(tokens, "stubble", "shadow", "scruff")) return PortraitBeard.Stubble;
            bool stache = Starts(tokens, "moustache", "mustache", "stache", "handlebar", "walrus");
            if (stache && !Starts(tokens, "chop", "beard", "full", "goatee", "dutch", "braid", "fork", "side")) return PortraitBeard.Moustache;
            if (Starts(tokens, "goatee", "soul", "chin", "circle", "anchor", "short", "patch") || Has(tokens, "vandyke") || Is(tokens, "van") && Is(tokens, "dyke")) return PortraitBeard.Short;
            return PortraitBeard.Full;
        }

        /// <summary>The build from the body type's words: thin, slim or lean is slim; fat or heavy heavy; hulk, broad or muscular broad; else average.</summary>
        public static PortraitBuild BuildFromWords(List<string> tokens)
        {
            if (tokens == null) return PortraitBuild.Average;
            if (Starts(tokens, "thin", "slim", "lean", "skinny", "slender")) return PortraitBuild.Slim;
            if (Starts(tokens, "fat", "heavy", "stout", "obese", "plump")) return PortraitBuild.Heavy;
            if (Starts(tokens, "hulk", "broad", "muscl", "brawn", "bulk")) return PortraitBuild.Broad;
            return PortraitBuild.Average;
        }

        /// <summary>The age of clothes by the game's tech level (0 undefined, 1 animal, 2 neolithic, 3 medieval, 4 industrial, 5 spacer, 6 ultra, 7 archotech).</summary>
        public static PortraitEra EraOf(int techLevel)
        {
            if (techLevel <= 0) return PortraitEra.Unknown;
            if (techLevel <= 2) return PortraitEra.Tribal;
            if (techLevel == 3) return PortraitEra.Medieval;
            if (techLevel == 4) return PortraitEra.Industrial;
            return PortraitEra.Spacer;
        }

        /// <summary>A piece they wear: its tech level, its outermost layer's draw order, and whether it covers the torso.</summary>
        public struct WornPiece
        {
            public int tech, layer;
            public bool torso;

            public WornPiece(int tech, int layer, bool torso)
            {
                this.tech = tech;
                this.layer = layer;
                this.torso = torso;
            }
        }

        /// <summary>
        /// The age of what they wear: the outermost piece on the torso whose age the game knows (a coat over a shirt counts
        /// as the coat), else the outermost of anything they wear, else their people's age, else unknown (any age matches).
        /// </summary>
        public static PortraitEra EraOfWorn(IList<WornPiece> worn, int factionTech)
        {
            int best = -1, bestLayer = int.MinValue;
            bool bestTorso = false;
            if (worn != null)
            {
                for (int i = 0; i < worn.Count; i++)
                {
                    var w = worn[i];
                    if (EraOf(w.tech) == PortraitEra.Unknown) continue;
                    bool better = w.torso && !bestTorso || w.torso == bestTorso && w.layer > bestLayer;
                    if (best < 0 || better)
                    {
                        best = i;
                        bestLayer = w.layer;
                        bestTorso = w.torso;
                    }
                }
            }
            return best >= 0 ? EraOf(worn[best].tech) : EraOf(factionTech);
        }

        /// <summary>
        /// The kind of their clothes, from the outermost piece on the torso: nothing on the torso, or worn to tatters, rags;
        /// armour (the game rates it as armour); robes (a robe, cloak, habit, cape, cassock, vestment, toga, kimono); fine
        /// (made excellent or better, or a noble's, royal or formal piece); else work clothes.
        /// </summary>
        public static PortraitDress DressOf(List<string> tokens, bool nothing, bool tattered, bool armour, bool fine)
        {
            if (nothing || tattered) return PortraitDress.Rags;
            if (armour) return PortraitDress.Armour;
            if (tokens != null && Starts(tokens, "robe", "cloak", "habit", "cape", "cassock", "vestment", "toga", "kimono", "poncho", "shawl")) return PortraitDress.Robes;
            if (fine || tokens != null && Starts(tokens, "royal", "noble", "formal", "prestige", "regal", "suit", "gown", "corset")) return PortraitDress.Fine;
            return PortraitDress.Work;
        }

        /// <summary>
        /// The features a gene shows on the head and body, from its words (name, label, the textures it draws): pointed or
        /// elf ears; long, floppy or animal ears; horns or antlers; tusks; fur; scales; gills; glowing eyes; markings or
        /// tattoos; a tail; a snout. A gene for the plain human head (<c>Headbone_Human</c>) shows none.
        /// </summary>
        public static List<string> FeaturesOf(List<string> tokens)
        {
            var f = new List<string>();
            if (tokens == null) return f;
            bool ear = Starts(tokens, "ear");
            if (ear && Starts(tokens, "point", "elf", "elven", "fae", "pixie")) f.Add("pointedEars");
            else if (ear && Starts(tokens, "long", "flop", "droop", "bunny", "rabbit", "cat", "fox", "wolf", "dog", "pig", "animal", "lop", "furry")) f.Add("longEars");
            if (Starts(tokens, "horn", "antler")) f.Add("horns");
            if (Starts(tokens, "tusk")) f.Add("tusks");
            if (Starts(tokens, "fur") && !ear) f.Add("fur");
            if (Starts(tokens, "scale", "reptil")) f.Add("scales");
            if (Starts(tokens, "gill")) f.Add("gills");
            if (Starts(tokens, "glow") && Starts(tokens, "eye")) f.Add("glowingEyes");
            if (Starts(tokens, "marking", "tattoo", "stripe")) f.Add("markings");
            if (Starts(tokens, "tail")) f.Add("tail");
            if (Starts(tokens, "snout", "muzzle") || Is(tokens, "nose") && Starts(tokens, "pig", "boar", "animal")) f.Add("snout");
            return f;
        }

        // ---------------- The matcher ----------------

        /// <summary>The weights: the skin's colour most (for every ten points of difference past the slack), the clothes' age, the hair's colour, then the hair, beard, build, clothes' kind, features.</summary>
        public const float SkinWeight = 3f, EraWeight = 2f, HairColourWeight = 0.6f, HairColourMost = 3f, HairWeight = 1f, BeardWeight = 1f, BuildWeight = 1f, DressWeight = 1f;

        /// <summary>
        /// Colour differences up to these cost nothing: a painted skin's mid-tone and the game's own skin colour (which its
        /// textures shade, and which is yellower than a painter's) never quite agree, nor two painters' browns.
        /// </summary>
        public const float SkinSlack = 6f, HairSlack = 8f;

        /// <summary>A feature one has and the other hasn't; a mark of the person's the picture doesn't show.</summary>
        public const float FeatureCost = 1f, MarkCost = 0.5f;

        /// <summary>The next age band standing in where the kind has none of the person's own; a baseliner's picture for a kind with none, near a human.</summary>
        public const float NearAgeCost = 2.5f, NearKindCost = 1f;

        /// <summary>The furthest a picture may be and still be theirs; beyond it, the game's own face.</summary>
        public const float CloseEnough = 6f;

        /// <summary>The nearest few characters: within this of the nearest, at most this many.</summary>
        public const float FewWithin = 1f;

        public const int FewMost = 4;

        /// <summary>A grown library takes a person's character away only for one at least this much closer.</summary>
        public const float BetterBy = 1.5f;

        private static readonly float[,] HairApart =
        {
            // bald, short, medium, long, braids, bun, tied
            { 0f, 0.75f, 1.25f, 1.5f, 1.5f, 1.5f, 1.5f },
            { 0.75f, 0f, 0.5f, 1f, 1.25f, 1f, 1f },
            { 1.25f, 0.5f, 0f, 0.5f, 0.75f, 0.75f, 0.5f },
            { 1.5f, 1f, 0.5f, 0f, 0.5f, 0.75f, 0.5f },
            { 1.5f, 1.25f, 0.75f, 0.5f, 0f, 0.75f, 0.5f },
            { 1.5f, 1f, 0.75f, 0.75f, 0.75f, 0f, 0.5f },
            { 1.5f, 1f, 0.5f, 0.5f, 0.5f, 0.5f, 0f }
        };

        private static readonly float[,] BeardApart =
        {
            // none, stubble, short, full, moustache
            { 0f, 0.5f, 1f, 1.5f, 0.75f },
            { 0.5f, 0f, 0.5f, 1f, 0.75f },
            { 1f, 0.5f, 0f, 0.5f, 0.75f },
            { 1.5f, 1f, 0.5f, 0f, 1f },
            { 0.75f, 0.75f, 0.75f, 1f, 0f }
        };

        private static readonly float[,] BuildApart =
        {
            // average, slim, heavy, broad
            { 0f, 0.5f, 0.75f, 0.75f },
            { 0.5f, 0f, 1.25f, 1.25f },
            { 0.75f, 1.25f, 0f, 0.75f },
            { 0.75f, 1.25f, 0.75f, 0f }
        };

        public static float HairApartOf(PortraitHairStyle a, PortraitHairStyle b) => HairApart[(int)a, (int)b];

        public static float BeardApartOf(PortraitBeard a, PortraitBeard b) => BeardApart[(int)a, (int)b];

        public static float BuildApartOf(PortraitBuild a, PortraitBuild b) => BuildApart[(int)a, (int)b];

        /// <summary>The kinds of clothes apart: the same none, armour and fine clothes 1, any other two 0.75; unknown none.</summary>
        public static float DressApartOf(PortraitDress a, PortraitDress b)
        {
            if (a == b || a == PortraitDress.Unknown || b == PortraitDress.Unknown) return 0f;
            bool af = a == PortraitDress.Armour && b == PortraitDress.Fine || a == PortraitDress.Fine && b == PortraitDress.Armour;
            bool rf = a == PortraitDress.Rags && b == PortraitDress.Fine || a == PortraitDress.Fine && b == PortraitDress.Rags;
            return af || rf ? 1f : 0.75f;
        }

        /// <summary>Whether a picture shows the person's kind: its races (none listed: a human) and its xenotypes (none listed: a baseliner, or any xenotype of another race).</summary>
        public static bool OwnKind(PortraitRecord r, PortraitLook l)
        {
            bool race = r.races.Count == 0 ? l.race == "human" : r.races.Contains(l.race);
            bool xeno = r.xenotypes.Count == 0 ? l.baseliner || l.race != "human" : l.xenotype.Length > 0 && r.xenotypes.Contains(l.xenotype);
            return race && xeno;
        }

        /// <summary>A baseliner human's picture (no race or xenotype listed).</summary>
        public static bool Baseliner(PortraitRecord r) => r.races.Count == 0 && r.xenotypes.Count == 0;

        public static bool SexAgrees(PortraitSex person, PortraitSex picture) => person == PortraitSex.Any || picture == PortraitSex.Any || person == picture;

        /// <summary>The next age band (a child and the young, the young and adults, adults and the old).</summary>
        public static bool NearAge(PortraitAge a, PortraitAge b) => Math.Abs((int)a - (int)b) == 1;

        /// <summary>A picture may stand for the person's body: it shows no mark they haven't got, and headwear only where they wear it and the setting shows it.</summary>
        public static bool Shows(PortraitRecord r, PortraitLook l)
        {
            foreach (var m in r.marks)
                if (!l.marks.Contains(m))
                    return false;
            return !r.headwear || l.headwear;
        }

        /// <summary>
        /// How far a picture is from a person on their looks (the matcher's kind, sex and age rules aside): the skin's and
        /// hair's colours by their difference as the eye sees it (CIE76, for every ten points past the slack), the hair, beard, build and
        /// clothes by their tables, the clothes' ages apart, each feature one has and the other hasn't, and each mark of the
        /// person's the picture doesn't show.
        /// </summary>
        public static float Distance(PortraitLook l, PortraitRecord r)
        {
            float d = 0f;
            if (r.skinSet) d += SkinWeight * Math.Max(0f, SkinApart(l.skin, r.skin) - SkinSlack) / 10f;
            if (r.hairColourSet)
            {
                float hc = Math.Min(HairColourMost, HairColourWeight * Math.Max(0f, ColourApart(l.hairColour, r.hairColour) - HairSlack) / 10f);
                // Bald on both: the colour hardly shows (a beard's, the brows').
                d += l.hair == PortraitHairStyle.Bald && r.hair == PortraitHairStyle.Bald ? hc * 0.5f : hc;
            }
            d += HairWeight * HairApart[(int)l.hair, (int)r.hair];
            d += BeardWeight * BeardApart[(int)l.beard, (int)r.beard];
            d += BuildWeight * BuildApart[(int)l.build, (int)r.build];
            if (l.era != PortraitEra.Unknown && r.era != PortraitEra.Unknown) d += EraWeight * Math.Abs((int)l.era - (int)r.era);
            d += DressWeight * DressApartOf(l.dress, r.dress);
            foreach (var f in l.features)
                if (!r.features.Contains(f))
                    d += FeatureCost;
            foreach (var f in r.features)
                if (!l.features.Contains(f))
                    d += FeatureCost;
            foreach (var m in l.marks)
                if (!r.marks.Contains(m))
                    d += MarkCost;
            return d;
        }

        /// <summary>
        /// The pictures that may stand for a person, with their distances (infinity for those that may not): the kind first
        /// (their own kind's where the library has any; else the baseliners', a little further, where they look near a
        /// human; else none), then sex, then the age band (their own where the kind has it for their sex, else the next
        /// band, further), then the body (<see cref="Shows"/>).
        /// </summary>
        public static float[] Distances(PortraitLook l, IList<PortraitRecord> records)
        {
            int n = records?.Count ?? 0;
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = float.PositiveInfinity;
            if (n == 0 || l == null) return d;
            bool own = false;
            for (int i = 0; i < n && !own; i++) own = OwnKind(records[i], l);
            if (!own && (l.FarFromHuman || l.baseliner && l.race == "human")) return d;
            float kindCost = own ? 0f : NearKindCost;
            bool Pool(PortraitRecord r) => own ? OwnKind(r, l) : Baseliner(r);
            bool sameAge = false;
            for (int i = 0; i < n && !sameAge; i++)
                sameAge = Pool(records[i]) && SexAgrees(l.sex, records[i].sex) && records[i].age == l.age;
            for (int i = 0; i < n; i++)
            {
                var r = records[i];
                if (!Pool(r) || !SexAgrees(l.sex, r.sex) || !Shows(r, l)) continue;
                if (r.age != l.age && (sameAge || !NearAge(r.age, l.age))) continue;
                d[i] = kindCost + Distance(l, r) + (r.age == l.age ? 0f : NearAgeCost);
            }
            return d;
        }

        /// <summary>A stable number from the pawn's id, for picking among equals.</summary>
        public static uint Mix(int seed) => unchecked(((uint)seed * 2654435761u) ^ ((uint)seed >> 15) * 40503u);

        /// <summary>
        /// A new pick for a person: of the nearest few characters (each by its nearest picture that may stand for them),
        /// those fewest others keep (<paramref name="usesOf"/>, by character), the nearest of them, and among equally near
        /// ones one by their own id; none beyond <see cref="CloseEnough"/>. So the first person of a look gets the closest
        /// face, and the next alike person the next closest.
        /// </summary>
        public static PortraitPick Pick(PortraitLook l, IList<PortraitRecord> records, int seed, Func<string, int> usesOf)
        {
            var d = Distances(l, records);
            var best = new Dictionary<string, int>();
            for (int i = 0; i < d.Length; i++)
            {
                if (float.IsInfinity(d[i])) continue;
                string c = records[i].Character;
                if (!best.TryGetValue(c, out int j) || d[i] < d[j] || d[i] == d[j] && string.CompareOrdinal(records[i].name, records[j].name) < 0) best[c] = i;
            }
            if (best.Count == 0) return PortraitPick.None;
            float nearest = best.Values.Min(i => d[i]);
            if (nearest > CloseEnough) return new PortraitPick { index = -1, character = "", distance = nearest };
            var few = best.Values.Where(i => d[i] <= nearest + FewWithin && d[i] <= CloseEnough)
                .OrderBy(i => d[i]).ThenBy(i => records[i].Character, StringComparer.Ordinal).Take(FewMost).ToList();
            int least = few.Min(i => usesOf?.Invoke(records[i].Character) ?? 0);
            // Of those fewest others keep, the nearest; among equally near, one by the pawn's own id.
            var free = few.Where(i => (usesOf?.Invoke(records[i].Character) ?? 0) == least).ToList();
            float closest = free.Min(i => d[i]);
            free.RemoveAll(i => d[i] > closest + 0.001f);
            int pick = free[(int)(Mix(seed) % (uint)free.Count)];
            return new PortraitPick { index = pick, character = records[pick].Character, distance = d[pick] };
        }

        /// <summary>
        /// The picture of a kept character that stands for the person as their body is now: of its variants that may (no
        /// mark they haven't got), the nearest (so a lost eye takes the variant with it, old age the older one), any age
        /// band apart allowed but further; -1 where none may.
        /// </summary>
        public static PortraitPick Variant(PortraitLook l, IList<PortraitRecord> records, string character)
        {
            var pick = PortraitPick.None;
            if (records == null || string.IsNullOrEmpty(character)) return pick;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r.Character != character || !SexAgrees(l.sex, r.sex) || !Shows(r, l)) continue;
                float d = Distance(l, r) + NearAgeCost * Math.Abs((int)r.age - (int)l.age);
                if (d < pick.distance || d == pick.distance && pick.index >= 0 && string.CompareOrdinal(r.name, records[pick.index].name) < 0)
                    pick = new PortraitPick { index = i, character = character, distance = d };
            }
            return pick;
        }

        /// <summary>
        /// Whether a person's kept character gives way to a new pick: nothing kept; they are another kind or sex now; the
        /// character is gone from the library or has no picture that may stand for them; or the library changed and has a
        /// character at least <see cref="BetterBy"/> closer. A person kept with no picture is looked at again whenever the
        /// library or their looks change.
        /// </summary>
        public static bool PickAgain(bool kept, bool keptHasCharacter, string keptKind, string kind, string keptWords, string words, bool variantThere,
            int keptLibrary, int library, float keptNow, float bestNow)
        {
            if (!kept) return true;
            if (!keptHasCharacter) return keptLibrary != library || keptWords != words;
            if (keptKind != kind || !variantThere) return true;
            return keptLibrary != library && bestNow + BetterBy <= keptNow;
        }

        // ---------------- Colours ----------------

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>CIE L*a*b* of an sRGB colour.</summary>
        public static void Lab(PortraitColour c, out double l, out double a, out double b)
        {
            double Lin(float v)
            {
                v = Clamp01(v);
                return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            double r = Lin(c.r), g = Lin(c.g), bl = Lin(c.b);
            double x = (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047, y = 0.2126 * r + 0.7152 * g + 0.0722 * bl, z = (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883;
            double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
            double fx = F(x), fy = F(y), fz = F(z);
            l = 116.0 * fy - 16.0;
            a = 500.0 * (fx - fy);
            b = 200.0 * (fy - fz);
        }

        /// <summary>How far apart two skins look: their lightness in full, their warmth (a* and b*) at half (CIE76 otherwise).</summary>
        public static float SkinApart(PortraitColour x, PortraitColour y)
        {
            // Skins tell apart by how light they are; their warmth counts half (the game's skin colours are yellower than
            // a painter's mid-tones of the same skin).
            Lab(x, out double l1, out double a1, out double b1);
            Lab(y, out double l2, out double a2, out double b2);
            return (float)Math.Sqrt((l1 - l2) * (l1 - l2) + 0.25 * ((a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2)));
        }

        /// <summary>How far apart two colours look (CIE76: 0 the same, about 2 just noticeable, 100 black and white).</summary>
        public static float ColourApart(PortraitColour x, PortraitColour y)
        {
            Lab(x, out double l1, out double a1, out double b1);
            Lab(y, out double l2, out double a2, out double b2);
            return (float)Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
        }

        /// <summary>Hue in degrees (0 to 360), saturation and value (0 to 1).</summary>
        public static void Hsv(PortraitColour c, out float h, out float s, out float v)
        {
            float r = Clamp01(c.r), g = Clamp01(c.g), b = Clamp01(c.b);
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            v = max;
            s = max > 0f ? d / max : 0f;
            if (d <= 1e-6f) h = 0f;
            else if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            if (h < 0f) h += 360f;
        }
    }
}
