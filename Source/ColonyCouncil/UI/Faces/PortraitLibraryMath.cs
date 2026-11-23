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

    /// <summary>
    /// The nine age bands of <c>docs/portrait-art/FRAMEWORK.md</c> (pillar 2), youngest first: a baby (0 to 1), a toddler
    /// (1 to 3), a child (4 to 12), a teen (13 to 17), young (18 to 29), adult (30 to 44), middle-aged (45 to 59), old (60
    /// to 74), an elder (75 and over); a human's years, the grown-ups' scaled by their kind's life expectancy against 80.
    /// </summary>
    public enum PortraitAge : byte
    {
        Baby,
        Toddler,
        Child,
        Teen,
        Young,
        Adult,
        Middle,
        Old,
        Elder
    }

    /// <summary>
    /// The stages a picture is kept through (a kept picture changes only when a person moves to the next stage, the
    /// player's rules of 22 November 2026): a baby, a toddler, a child, a teen, a grown-up (young, adult and middle-aged
    /// alike), old (old and elder alike).
    /// </summary>
    public enum PortraitStage : byte
    {
        Baby,
        Toddler,
        Child,
        Teen,
        Grown,
        Old
    }

    /// <summary>The game's own developmental stage, where known: a baby (a newborn too), a child, a grown-up.</summary>
    public enum GameStage : byte
    {
        Unknown,
        Baby,
        Child,
        /// <summary>The game's teenage stage (a human's 13 to 18).</summary>
        Teen,
        /// <summary>Grown by the game's stages, with no teenage stage told apart (a teen by years under 18).</summary>
        Adult,
        /// <summary>Past the game's teenage stage (a human's adult stage, from 18).</summary>
        Grown
    }

    /// <summary>"Portraits from the library": the closest picture for everyone it may stand for, or only pictures close to them (phase 3's way).</summary>
    public enum PortraitPickMode
    {
        Closest,
        OnlyClose
    }

    /// <summary>"Strangers may share a picture": never, when not seen together (the player's choice), or always (for a small library).</summary>
    public enum PortraitShareMode
    {
        Never,
        NotTogether,
        Always
    }

    /// <summary>
    /// Whose a picture may be (the player, 22 November 2026): a stranger's (raiders, traders, visitors, guests), a notable's
    /// (faction leaders, titled nobles, the colony's named contacts) held by them alone while they live, or one of the
    /// player's own people's (colonists, slaves, prisoners) held by them alone for good; the higher rank wins.
    /// </summary>
    public enum PortraitRank : byte
    {
        Stranger,
        Notable,
        Own
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

    /// <summary>
    /// The side of the body a mark is on, as seen from the person (their own left eye is on the picture's right as they
    /// face it): not told, left, right; a person's lost eyes may be on both.
    /// </summary>
    [Flags]
    public enum PortraitSide : byte
    {
        None = 0,
        Left = 1,
        Right = 2
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
        /// <summary>
        /// The side its sided marks are on (a lost or bionic eye, an eye patch, a missing ear, an artificial arm; the record's
        /// <c>side</c>, left or right as seen from the person), or none told: it then stands for the mark on either side.
        /// </summary>
        public PortraitSide side;
        /// <summary>Its lost eye is covered by an eye patch (its marks named eyePatch, which the matcher counts as a lost eye).</summary>
        public bool patch;
        /// <summary>The looks it shows (the looks' words, <see cref="PortraitLookMath"/>), and those of them that are special.</summary>
        public HashSet<string> looks = new HashSet<string>(StringComparer.OrdinalIgnoreCase), specials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The work it shows (one of <see cref="PortraitLookMath.Roles"/>), or "" for any.</summary>
        public string role = "";
        public PortraitFacing facing;
        /// <summary>
        /// The character it is a variant of and the variant's word (phase 3: the same face with a mark or older; "base" for
        /// the face itself). For an expression of another picture (<see cref="variantOf"/> set) the word is the expression.
        /// </summary>
        public string character = "", variant = "base";
        /// <summary>
        /// The picture this one is a version of (its def's name), or "". Phase 6: an expression, the same picture painted
        /// again with the eyes closed, a smile, a frown or in pain (<see cref="PortraitLifeMath.Faces"/>, the word in
        /// <see cref="variant"/>): never picked on its own and never counted in the matching or the missing list; it belongs
        /// to its picture and only the large portrait shows it. A mark variant (<see cref="PortraitLibraryMath.MarkVariantWords"/>
        /// in <see cref="variant"/>: the same picture with a lost eye, a scar ...): one of its picture's face, shown instead of
        /// it for whoever holds the picture and has the mark (<see cref="PortraitLibraryMath.Version"/>).
        /// </summary>
        public string variantOf = "";
        /// <summary>The colonist it is pinned to (their nickname, first, last, shown or full name), or "": used for them only, never for anyone else.</summary>
        public string pawn = "";

        public bool Pinned => pawn.Length > 0;

        /// <summary>A mark variant of another picture: the same picture with a mark it lacks, a version of it, not a face of its own.</summary>
        public bool IsMarkVariant => variantOf.Length > 0 && PortraitLibraryMath.MarkVariantWord(variant) != null;

        /// <summary>An expression of another picture (phase 6), not a picture of its own (a version with a word Rimfolk doesn't know yet too).</summary>
        public bool IsExpression => variantOf.Length > 0 && !IsMarkVariant;

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
        public PortraitSex sex;
        public PortraitAge age = PortraitAge.Adult;
        public PortraitColour skin, hairColour;
        public PortraitHairStyle hair = PortraitHairStyle.Short;
        public PortraitBeard beard;
        public PortraitBuild build;
        public PortraitEra era;
        public PortraitDress dress;
        /// <summary>Wearing headgear, and the setting shows it.</summary>
        public bool headwear;
        public HashSet<string> features = new HashSet<string>(), marks = new HashSet<string>();
        /// <summary>The sides their sided marks are on (by the mark as the matcher counts it, <see cref="PortraitLibraryMath.MarkKey"/>); a mark not here, or with none, on a side not told.</summary>
        public Dictionary<string, PortraitSide> sides = new Dictionary<string, PortraitSide>();
        /// <summary>They wear an eye patch (an apparel or a part of that name).</summary>
        public bool eyePatch;
        /// <summary>Their looks (from their traits, genes, hediffs and body, <see cref="LookBook.Of"/>), and those of them that are special.</summary>
        public HashSet<string> looks = new HashSet<string>(StringComparer.OrdinalIgnoreCase), specials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Their work (one of <see cref="PortraitLookMath.Roles"/>), or "" (a child, or nothing tells).</summary>
        public string role = "";

        /// <summary>A look they have, with whether it is special.</summary>
        public void AddLook(string word, bool special)
        {
            if (string.IsNullOrEmpty(word)) return;
            looks.Add(word);
            if (special) specials.Add(word);
        }

        /// <summary>A mark of their body (as the matcher counts it), on a side where it has one and it is told.</summary>
        public void AddMark(string mark, PortraitSide side = PortraitSide.None)
        {
            if (string.IsNullOrEmpty(mark)) return;
            string k = PortraitLibraryMath.MarkKey(mark);
            marks.Add(k);
            if (side == PortraitSide.None || !PortraitLibraryMath.Sided(k)) return;
            sides.TryGetValue(k, out var had);
            sides[k] = had | side;
        }

        /// <summary>The sides a mark of theirs is on (none where it isn't told).</summary>
        public PortraitSide SidesOf(string mark) => sides.TryGetValue(PortraitLibraryMath.MarkKey(mark), out var s) ? s : PortraitSide.None;

        /// <summary>Their kind, sex and age in words: kept with a pick, so a person who becomes another kind is matched again.</summary>
        public string Kind => (baseliner && race == "human" ? "baseliner" : race + "/" + xenotype) + "_" + PortraitLibraryMath.Word(sex);

        /// <summary>Everything the matcher reads, in words, for the log and for a person kept with no picture.</summary>
        public string Words =>
            Kind + "_" + PortraitLibraryMath.Word(age) + "_skin" + PortraitLibraryMath.Hex(skin) + "_hair" + PortraitLibraryMath.Hex(hairColour) + "_" + hair.ToString().ToLowerInvariant()
            + "_" + beard.ToString().ToLowerInvariant() + "_" + build.ToString().ToLowerInvariant() + "_" + era.ToString().ToLowerInvariant() + "_" + dress.ToString().ToLowerInvariant()
            + (headwear ? "_headwear" : "") + (features.Count > 0 ? "_" + string.Join("+", features.OrderBy(f => f, StringComparer.Ordinal)) : "")
            + (marks.Count > 0 ? "_" + string.Join("+", marks.OrderBy(f => f, StringComparer.Ordinal)) : "")
            + (looks.Count > 0 ? "_" + string.Join("+", looks.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) : "") + (role.Length > 0 ? "_" + role : "");

    }

    /// <summary>A pick: the picture (an index into the records), its character, and its distance; -1 for none.</summary>
    public struct PortraitPick
    {
        public int index;
        public string character;
        public float distance;
        /// <summary>No picture given though some would fit them: every fitting one is held by someone else (for the missing list's "no free picture").</summary>
        public bool crowded;

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
    /// <item>The matcher: the kind first (only a picture of their own race and xenotype, else the game's own face); then
    /// sex, their own age band and their skin (the same tone or the next; a skin no human grows a near colour), all
    /// exactly, else no picture; then the distance: the hair's colour most, then the skin's colour, hair, beard, build,
    /// clothes and features; marks follow the body (a picture never shows a mark the person hasn't, and one that shows
    /// theirs is preferred).</item>
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

        /// <summary>
        /// The marks of the body a picture or a pawn may have; artificialArm is any artificial arm or hand (bionic, archotech,
        /// prosthetic, a mod's added part) on an arm, a shoulder or a hand.
        /// </summary>
        public static readonly string[] Marks = { "lostEye", "eyePatch", "scar", "burn", "bionicEye", "missingEar", "artificialArm" };

        /// <summary>The game's tags for the limbs that handle things (a shoulder, an arm, a hand, the fingers), a mod's arms and hands among them.</summary>
        public static readonly string[] ArmTags = { "ManipulationLimbCore", "ManipulationLimbSegment", "ManipulationLimbDigit" };

        /// <summary>
        /// Whether a body part is an arm, a shoulder or a hand, by its def's words (name and label: "arm", "forearm",
        /// "shoulder", "hand") or by one of the game's tags for the limbs that handle things (<see cref="ArmTags"/>), so a
        /// mod's body with arms of its own names counts too. A leg, a foot, the head or the torso never.
        /// </summary>
        public static bool ArmPart(List<string> words, IEnumerable<string> tags)
        {
            if (tags != null)
                foreach (var t in tags)
                    if (t != null && Array.IndexOf(ArmTags, t) >= 0)
                        return true;
            return words != null && Is(words, "arm", "arms", "forearm", "forearms", "shoulder", "shoulders", "hand", "hands");
        }

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

        /// <summary>The marks that are on one side of the body (an eye patch counts as the lost eye under it): a lost or bionic eye, a missing ear, an artificial arm.</summary>
        public static bool Sided(string mark)
        {
            switch (MarkKey(mark))
            {
                case "lostEye":
                case "bionicEye":
                case "missingEar":
                case "artificialArm":
                    return true;
                default:
                    return false;
            }
        }

        // ---------------- Mark variants ----------------

        /// <summary>
        /// The mark variants a picture may have beside it (the player, 23 November 2026: "if a pawn loses an eye ... create a
        /// one-eyed ... variant of this specific image ... the variants are then always assigned to this specific image file"):
        /// the same picture with one mark it lacks, the side as seen from the person, by their words in records.xml
        /// (<c>&lt;variant&gt;</c> of a picture with <c>&lt;variantOf&gt;</c>) and in variants-wanted.csv.
        /// </summary>
        public static readonly string[] MarkVariantWords =
        {
            "lostEyeLeft", "lostEyeRight", "eyePatchLeft", "eyePatchRight", "bionicEyeLeft", "bionicEyeRight", "scar", "burn", "missingEarLeft", "missingEarRight",
            "artificialArmLeft", "artificialArmRight"
        };

        /// <summary>A mark variant's word as Rimfolk writes it (the case aside: the scripts may write it in small letters), or null for none of them.</summary>
        public static string MarkVariantWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim();
            foreach (var w in MarkVariantWords)
                if (string.Equals(w, t, StringComparison.OrdinalIgnoreCase))
                    return w;
            return null;
        }

        /// <summary>The mark a mark variant's word shows (lostEye, eyePatch, bionicEye, scar, burn, missingEar, artificialArm) and its side; null for no such word.</summary>
        public static string MarkOfVariant(string word, out PortraitSide side)
        {
            side = PortraitSide.None;
            string w = MarkVariantWord(word);
            if (w == null) return null;
            if (w.EndsWith("Left", StringComparison.Ordinal))
            {
                side = PortraitSide.Left;
                return w.Substring(0, w.Length - 4);
            }
            if (w.EndsWith("Right", StringComparison.Ordinal))
            {
                side = PortraitSide.Right;
                return w.Substring(0, w.Length - 5);
            }
            return w;
        }

        /// <summary>The mark variant's word for a mark on a side (a sided mark on no side told: the left), or null for a mark with no variant.</summary>
        public static string VariantWordFor(string mark, PortraitSide side)
        {
            if (string.IsNullOrEmpty(mark) || Array.IndexOf(Marks, mark) < 0) return null;
            if (!Sided(mark)) return mark;
            return mark + ((side & PortraitSide.Right) != 0 && (side & PortraitSide.Left) == 0 ? "Right" : "Left");
        }

        /// <summary>A mark variant that leaves the head as it was (an artificial arm): its picture's own expressions may be shown over it.</summary>
        public static bool HeadUnchanged(string word)
        {
            string m = MarkOfVariant(word, out _);
            return m == "artificialArm";
        }

        /// <summary>A side as a record writes it: left, right, or "" for none told.</summary>
        public static string SideWord(PortraitSide side) => side == PortraitSide.Left ? "left" : side == PortraitSide.Right ? "right" : "";

        /// <summary>A record's side from its text: left or right (the case aside, l and r too); none for nothing; null for anything else.</summary>
        public static PortraitSide? SideOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return PortraitSide.None;
            string t = text.Trim().ToLowerInvariant();
            if (t == "left" || t == "l") return PortraitSide.Left;
            if (t == "right" || t == "r") return PortraitSide.Right;
            if (t == "none" || t == "-" || t == "both") return PortraitSide.None;
            return null;
        }

        /// <summary>The side a body part's words name (its label as the game's body def writes it, "left eye", "RightShoulder"), or none.</summary>
        public static PortraitSide SideFromWords(List<string> tokens)
        {
            if (tokens == null) return PortraitSide.None;
            bool left = Is(tokens, "left", "l"), right = Is(tokens, "right", "r");
            return left && !right ? PortraitSide.Left : right && !left ? PortraitSide.Right : PortraitSide.None;
        }

        /// <summary>Whether a worn thing's or an added part's words name an eye patch ("Eyepatch", "eye patch").</summary>
        public static bool EyePatchWords(List<string> tokens) => tokens != null && (Starts(tokens, "eyepatch") || Is(tokens, "eye") && Starts(tokens, "patch"));

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
        /// Other words for the same thing (the framework's, <c>docs/portrait-art/FRAMEWORK.md</c>, pillar 2, and common
        /// ones), so a def written with them reads as the matcher's own: a teenager a teen, middle-aged middle, elderly an
        /// elder; buzzed hair or a mohawk short, a topknot a bun, dreadlocks long, curly medium; a short or full beard, a
        /// goatee short; muscular broad.
        /// </summary>
        public static string Synonym(string field, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string t = text.Trim().Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
            switch (field)
            {
                case "age":
                    if (t == "teenager") return "teen";
                    if (t == "middleaged") return "middle";
                    if (t == "elderly") return "elder";
                    if (t == "infant" || t == "newborn") return "baby";
                    break;
                case "hair":
                    if (t == "buzzed" || t == "buzz" || t == "buzzcut" || t == "mohawk" || t == "crop" || t == "cropped") return "short";
                    if (t == "topknot" || t == "knot") return "bun";
                    if (t == "dreadlocks" || t == "dreads" || t == "locks") return "long";
                    if (t == "curly") return "medium";
                    if (t == "ponytail" || t == "pigtails") return "tied";
                    if (t == "shaved") return "bald";
                    break;
                case "beard":
                    if (t == "shortbeard" || t == "goatee") return "short";
                    if (t == "fullbeard") return "full";
                    if (t == "mustache") return "moustache";
                    if (t == "clean" || t == "cleanshaven" || t == "shaven") return "none";
                    break;
                case "build":
                    if (t == "muscular" || t == "stocky") return "broad";
                    if (t == "thin" || t == "lean") return "slim";
                    if (t == "fat" || t == "stout") return "heavy";
                    break;
            }
            return text;
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
                if (TryEnum(Synonym(f, text), out T v)) return v;
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
                if (k != null)
                {
                    r.marks.Add(MarkKey(k));
                    if (k == "eyePatch") r.patch = true;
                }
                else if (!string.IsNullOrWhiteSpace(m)) Bad("marks: '" + m + "' is not one of " + string.Join(", ", Marks));
            }
            string sideText = field("side");
            var side = SideOf(sideText);
            if (side == null) Bad("side: '" + sideText.Trim() + "' is not left or right");
            else r.side = side.Value;
            foreach (var w in list("looks") ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(w)) continue;
                var look = PortraitLookMath.Book.Find(w);
                if (look == null)
                {
                    Bad("looks: '" + w.Trim() + "' is not the word of any look (ColonyCouncil.PortraitLookDef)");
                    continue;
                }
                r.looks.Add(look.word);
                if (look.special) r.specials.Add(look.word);
            }
            string role = field("role");
            if (!string.IsNullOrWhiteSpace(role))
            {
                r.role = PortraitLookMath.RoleWord(role) ?? "";
                if (r.role.Length == 0) Bad("role: '" + role.Trim() + "' is not one of " + string.Join(", ", PortraitLookMath.Roles));
            }
            r.character = (field("character") ?? "").Trim();
            r.pawn = (field("pawn") ?? "").Trim();
            string variant = field("variant");
            r.variant = string.IsNullOrWhiteSpace(variant) ? "base" : variant.Trim();
            // Phase 6: an expression of another picture (variantOf) names its expression in variant; phase 3's meaning (a
            // character's variant with a mark or older) stays for every picture without variantOf.
            r.variantOf = (field("variantOf") ?? "").Trim();
            if (r.variantOf.Length > 0)
            {
                // Its word, the case aside; a word Rimfolk doesn't know yet (the tools may paint more later) is kept and simply not shown.
                r.variant = PortraitLifeMath.FaceWord(variant) ?? MarkVariantWord(variant) ?? (variant ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(variant)) Bad("variant: an expression (a picture with variantOf) names its expression: " + string.Join(", ", PortraitLifeMath.Faces));
                if (r.variantOf == name) Bad("variantOf: a picture can't be an expression of itself");
                // A mark variant shows its mark on its side, whatever its marks and side say.
                string mark = MarkOfVariant(r.variant, out var markSide);
                if (mark != null)
                {
                    r.marks.Add(MarkKey(mark));
                    if (mark == "eyePatch") r.patch = true;
                    if (markSide != PortraitSide.None)
                    {
                        if (r.side != PortraitSide.None && r.side != markSide) Bad("side: '" + SideWord(r.side) + "' against its variant " + r.variant + "; the variant's side is kept");
                        r.side = markSide;
                    }
                }
            }
            return r;
        }

        /// <summary>
        /// The mark variants split off (after <see cref="SplitExpressions"/>, whose pictures of their own they are among): of
        /// <paramref name="own"/>, those that stand on their own (in order), and each picture's mark variants by its index
        /// (<paramref name="variants"/>), each joined to its picture's face (its character set to the picture's), so it is one
        /// of that face for the matcher and whoever holds the face. A mark variant of a picture not in the library, or of
        /// another variant, is left out and told; a second of the same word kept (the first in order is used).
        /// </summary>
        public static List<int> SplitVariants(IList<PortraitRecord> records, IList<int> own, Dictionary<int, List<int>> variants, List<string> problems)
        {
            var kept = new List<int>();
            if (records == null || own == null) return kept;
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int i in own)
                if (!records[i].IsMarkVariant && !byName.ContainsKey(records[i].name))
                    byName[records[i].name] = i;
            foreach (int i in own)
            {
                var r = records[i];
                if (!r.IsMarkVariant)
                {
                    kept.Add(i);
                    continue;
                }
                if (!byName.TryGetValue(r.variantOf, out int root))
                {
                    problems?.Add(r.name + ": a mark variant of " + r.variantOf + ", which isn't a picture of the library, so it is left out");
                    continue;
                }
                r.character = records[root].Character;
                if (!variants.TryGetValue(root, out var list)) variants[root] = list = new List<int>();
                list.Add(i);
                kept.Add(i);
            }
            return kept;
        }

        /// <summary>
        /// The library's pictures split (phase 6): the indices of those that stand on their own (in order), and the expressions
        /// of another picture (<see cref="PortraitRecord.variantOf"/>) by the picture they belong to and their word. An
        /// expression whose picture isn't in the library (or is itself an expression), or a second of the same word, is left
        /// out and told in <paramref name="problems"/>; one with a word Rimfolk doesn't know yet is left out quietly (the
        /// player's tools may paint more expressions later), never a picture of its own.
        /// </summary>
        public static List<int> SplitExpressions(IList<PortraitRecord> records, Dictionary<string, Dictionary<string, int>> expressions, List<string> problems)
        {
            var own = new List<int>();
            if (records == null) return own;
            var bases = new HashSet<string>(records.Where(r => r != null && !r.IsExpression).Select(r => r.name), StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null) continue;
                if (!r.IsExpression)
                {
                    own.Add(i);
                    continue;
                }
                if (PortraitLifeMath.FaceWord(r.variant) == null) continue;
                if (!bases.Contains(r.variantOf))
                {
                    problems?.Add(r.name + ": an expression of " + r.variantOf + ", which isn't a picture of the library, so it is left out");
                    continue;
                }
                if (!expressions.TryGetValue(r.variantOf, out var faces)) expressions[r.variantOf] = faces = new Dictionary<string, int>(StringComparer.Ordinal);
                if (faces.ContainsKey(r.variant)) problems?.Add(r.name + ": a second " + r.variant + " of " + r.variantOf + ", left out");
                else faces[r.variant] = i;
            }
            return own;
        }

        /// <summary>
        /// What is wrong across the library: a character with no base variant, two variants of one character of different sex
        /// or kind. Expressions and mark variants of another picture (versions of that picture) are left out.
        /// </summary>
        public static List<string> LibraryProblems(IList<PortraitRecord> records)
        {
            var problems = new List<string>();
            foreach (var g in records.Where(r => !string.IsNullOrEmpty(r.character) && r.variantOf.Length == 0).GroupBy(r => r.character))
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

        /// <summary>A toddler from one year, a child from four, a teen from 13, grown from 18: a human's years for the young ones, where the game's own life stages don't tell (<c>FRAMEWORK.md</c>'s years).</summary>
        public const float ToddlerFrom = 1f, ChildFrom = 4f, GrownFrom = 13f, TeenUntil = 18f;

        /// <summary>A human's years at which each grown-up band ends (young, adult, middle, old; an elder after), scaled by the kind's life span against <see cref="HumanLife"/>.</summary>
        public static readonly float[] BandEnds = { 30f, 45f, 60f, 75f };

        public const float HumanLife = 80f;

        /// <summary>
        /// For someone whose ageing has stopped (an ageless gene): their look follows their years since birth as if their
        /// kind lived ten times as long (the player, 22 November 2026: "ageless pawns can also look ... old, middle-aged, or
        /// even elderly over time if they are like really super old. And they don't necessarily always have to be looking
        /// like a young adult"). For a kind living 80 years: young until 300, adult until 450, middle-aged until 600, old
        /// until 750, then an elder.
        /// </summary>
        public const float AgelessLifeScale = 10f;

        /// <summary>The age band for a biological age and their kind's life expectancy (the game's stage not known); false for no age.</summary>
        public static bool AgeOf(float years, float lifeExpectancy, out PortraitAge age) => AgeOf(years, years, lifeExpectancy, 1f, GameStage.Unknown, false, out age);

        /// <summary>The same with the game's stage of life known.</summary>
        public static bool AgeOf(float years, float lifeExpectancy, GameStage stage, out PortraitAge age) => AgeOf(years, years, lifeExpectancy, 1f, stage, false, out age);

        /// <summary>
        /// How old a person looks, as an age band (the player, 22 November 2026: "the age range should always be
        /// corresponding to the xenotype ... some even can live forever technically ... Not that an ... elf that's still
        /// young with a certain age is shown as an old person"):
        /// <list type="bullet">
        /// <item>from their biological age (<paramref name="years"/>; genes that slow or speed up ageing count by themselves);</item>
        /// <item>the young ones by the game's own stages of life where it tells them (a baby under one, else a toddler; a
        /// child; a teen), else by a human's years (a toddler from one, a child from four, a teen from 13 until 18);</item>
        /// <item>a grown-up by a human's years (young until 30, adult until 45, middle-aged until 60, old until 75, then an
        /// elder) scaled by their kind's life expectancy times their own life span factor (the genes for a long or short
        /// life) against 80: a kind that lives 400 years is young until 150;</item>
        /// <item>someone whose ageing has stopped (<paramref name="ageless"/>) by their years since birth
        /// (<paramref name="born"/>) on a scale <see cref="AgelessLifeScale"/> times as long, never younger than their
        /// biological age reads.</item>
        /// </list>
        /// False for no age at all.
        /// </summary>
        public static bool AgeOf(float years, float born, float lifeExpectancy, float lifespanFactor, GameStage stage, bool ageless, out PortraitAge age)
        {
            age = PortraitAge.Adult;
            if (float.IsNaN(years) || float.IsInfinity(years)) return false;
            if (years < 0f) years = 0f;
            float life = lifeExpectancy > 20f && lifeExpectancy < 100000f ? lifeExpectancy : HumanLife;
            if (lifespanFactor > 0.05f && lifespanFactor < 1000f) life *= lifespanFactor;
            float k = life / HumanLife;
            bool little = stage == GameStage.Baby || stage == GameStage.Unknown && years < ChildFrom;
            if (little)
                age = years < ToddlerFrom ? PortraitAge.Baby : PortraitAge.Toddler;
            else if (stage == GameStage.Child || stage == GameStage.Unknown && years < GrownFrom)
                age = PortraitAge.Child;
            else if (stage == GameStage.Teen || stage != GameStage.Grown && years < TeenUntil)
                age = PortraitAge.Teen;
            else
            {
                age = GrownBand(years, k);
                if (ageless && !float.IsNaN(born) && born > years)
                {
                    var byYears = GrownBand(born, k * AgelessLifeScale);
                    if (byYears > age) age = byYears;
                }
            }
            return true;
        }

        /// <summary>A grown-up's band for their years, the bands' human ends scaled by <paramref name="k"/>.</summary>
        private static PortraitAge GrownBand(float years, float k)
        {
            if (years < BandEnds[0] * k) return PortraitAge.Young;
            if (years < BandEnds[1] * k) return PortraitAge.Adult;
            if (years < BandEnds[2] * k) return PortraitAge.Middle;
            if (years < BandEnds[3] * k) return PortraitAge.Old;
            return PortraitAge.Elder;
        }

        /// <summary>The stage an age band belongs to: young, adult and middle-aged are one (a grown-up), old and elder one.</summary>
        public static PortraitStage StageOf(PortraitAge a)
        {
            switch (a)
            {
                case PortraitAge.Baby: return PortraitStage.Baby;
                case PortraitAge.Toddler: return PortraitStage.Toddler;
                case PortraitAge.Child: return PortraitStage.Child;
                case PortraitAge.Teen: return PortraitStage.Teen;
                case PortraitAge.Old:
                case PortraitAge.Elder: return PortraitStage.Old;
                default: return PortraitStage.Grown;
            }
        }

        /// <summary>
        /// A skin no human grows (a xenotype's green, blue, purple, deep red, orange or yellow, a waster's grey, ink black, a
        /// cold white): it matches only another such skin of a near colour (<see cref="SkinAgrees"/>).
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

        /// <summary>
        /// The weights: the hair's colour most (<see cref="HairColourCost"/>), then the skin's colour (for every ten points of
        /// difference past the slack) and the clothes' age, then the hair, beard, build, clothes' kind, features.
        /// </summary>
        public const float SkinWeight = 3f, EraWeight = 2f, HairWeight = 1f, BeardWeight = 1f, BuildWeight = 1f, DressWeight = 1f;

        /// <summary>
        /// The hair's colour, a strong preference but never a hard rule (the player, 23 November 2026: "the image selection
        /// should also lean more heavily towards the right hair color. If a pawn ... has blonde hair, it should, of course,
        /// prioritize blonde hair first over other hair colors ... It shouldn't be a deal breaker, if there's no matching hair
        /// color, but everything else fits. It should still take it, but it should prioritize the same hair color"): its
        /// difference past the slack, in tens of points, squared, times 2.5, at most 12. So a neighbouring shade costs little
        /// (dark brown and black, 14 apart: 0.85, less than short against long hair), a clearly different colour (30 apart
        /// or more: blond and brown, black and red, brown and grey) the most, which is more than a picture can cost on its
        /// hair, beard, build and clothes together (at most 11.25), so a picture of their hair colour ranks before it
        /// whatever those show.
        /// </summary>
        public const float HairColourWeight = 2.5f, HairColourMost = 12f;

        /// <summary>
        /// Colour differences up to these cost nothing: a painted skin's mid-tone and the game's own skin colour (which its
        /// textures shade, and which is yellower than a painter's) never quite agree, nor two painters' browns.
        /// </summary>
        public const float SkinSlack = 6f, HairSlack = 8f;

        /// <summary>A feature one has and the other hasn't; a mark of the person's the picture doesn't show.</summary>
        public const float FeatureCost = 1f, MarkCost = 0.5f;

        /// <summary>
        /// An ordinary look of the person's the picture doesn't show (a plain face for a drinker, a plain picture for a fire
        /// mage where none shows the flame), and the person's work where the picture shows none: 0.75 each, a little less
        /// than a feature, so a picture showing them is preferred and a plain one still stands in. A picture showing a look
        /// or a work the person hasn't got never stands for them at all (<see cref="LooksAgree"/>).
        /// </summary>
        public const float LookMissingCost = 0.75f, RoleMissingCost = 0.75f;

        /// <summary>
        /// The human skin tones by lightness (CIE L*), lightest first: very fair from 85, fair from 75, light from 65,
        /// medium from 55, tan from 45, brown from 33, dark from 22, very dark below (eight tones; the first seven are the
        /// missing list's skin words, <c>PortraitFolderMath.SkinWord</c>, with the darkest split in two, as the game's two
        /// darkest melanin skins lie 13 apart). A skin and a picture's agree within the same tone or the next
        /// (<see cref="SkinAgrees"/>): the game's own skin and the painter's colour of the same skin (which never quite agree,
        /// <see cref="SkinSlack"/>) always land in the same tone or next to each other.
        /// </summary>
        public static readonly float[] SkinToneFrom = { 85f, 75f, 65f, 55f, 45f, 33f, 22f };

        /// <summary>
        /// Skins no human grows, beside the lightness: a grey (its colour weaker than this, CIE chroma: the game's slate grey
        /// 5, light grey 0, sheer white 7, ink black 1) agrees with another grey; a coloured one with another whose hue is at
        /// most <see cref="HueMost"/> degrees apart. The game's own colour and the painter's of the same skin are at most 13
        /// degrees apart (the game's blue and the missing list's blue); a red and an orange 21 or more, a blue and a purple
        /// 35, a green and a pink far more (the game's deep and pale yellow are 10 apart, so they agree).
        /// </summary>
        public const float GreyChroma = 12f, HueMost = 20f;

        /// <summary>
        /// Any other two skins (a human skin and one no human grows, a grey and a coloured one) agree only up to this far
        /// apart (CIE76): nearly the same colour, as a near-white on either side of the line (the game's sheer white and a
        /// painted white are 7 apart). So a human skin never gets a green, blue, red, orange, yellow, purple, grey or ink
        /// black one, nor those a human one, and a grey never a pink.
        /// </summary>
        public const float SkinLineMost = 10f;

        /// <summary>"Only close ones": the furthest a picture may be and still be shown; beyond it, the game's own face.</summary>
        public const float CloseEnough = 6f;

        /// <summary>
        /// The nearest few characters: within this of the nearest, at most this many; of them, the one fewest others keep
        /// (the player, 22 November 2026: "as few repeated images as possible").
        /// </summary>
        public const float FewWithin = 1.5f;

        public const int FewMost = 6;

        /// <summary>"Choose another picture" steps through at most this many: the person's own pinned pictures, then the nearest faces.</summary>
        public const int ChoicesMost = 8;

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

        /// <summary>
        /// Whether a picture shows the person's own kind, exactly: its races (none listed: a human) and its xenotypes (none
        /// listed: a baseliner; a xenotype of the player's own making by its name). A hybrid or a xenotype with no name
        /// matches none.
        /// </summary>
        public static bool OwnKind(PortraitRecord r, PortraitLook l)
        {
            bool race = r.races.Count == 0 ? l.race == "human" : r.races.Contains(l.race);
            bool xeno = r.xenotypes.Count == 0 ? l.baseliner : l.xenotype.Length > 0 && r.xenotypes.Contains(l.xenotype);
            return race && xeno;
        }


        public static bool SexAgrees(PortraitSex person, PortraitSex picture) => person == PortraitSex.Any || picture == PortraitSex.Any || person == picture;

        /// <summary>
        /// Whether a picture of one age band may stand for a person of another: only their own band (the player, 23
        /// November 2026: "the race, xenotype, the gender, also I would say the skin color and age ... always have to match
        /// the pawn for sure. And if there's no image with this combination available for the pawn, then it should just
        /// take no image"). A new pick and the choices of "Choose another picture" only; a picture already kept stays
        /// through its stage (<see cref="PickAgain"/>), and a picture pinned to a colonist fits by stage (<see cref="PinFit"/>).
        /// </summary>
        public static bool AgeMayStand(PortraitAge person, PortraitAge picture) => person == picture;

        /// <summary>What a picture of age band <paramref name="picture"/> costs for a person of band <paramref name="person"/>: nothing for their own, infinity for any other (<see cref="AgeMayStand"/>).</summary>
        public static float AgeCost(PortraitAge person, PortraitAge picture) => AgeMayStand(person, picture) ? 0f : float.PositiveInfinity;

        /// <summary>A skin's tone (<see cref="SkinToneFrom"/>) by its lightness (CIE L*): 0 very fair to 7 very dark.</summary>
        public static int SkinToneOf(double lightness)
        {
            int t = 0;
            foreach (var from in SkinToneFrom)
                if (lightness < from)
                    t++;
            return t;
        }

        /// <summary>A skin's tone by its colour: 0 very fair to 7 very dark.</summary>
        public static int SkinTone(PortraitColour c)
        {
            Lab(c, out double l, out _, out _);
            return SkinToneOf(l);
        }

        /// <summary>
        /// Whether a picture's skin may stand for a person's (the player, 23 November 2026, above): the same tone of
        /// lightness or the next (<see cref="SkinToneFrom"/>), for every skin; two human skins need nothing else. Two skins
        /// no human grows (<see cref="UnusualSkin"/>) also by their colour: two greys agree, two coloured ones of a near hue
        /// (<see cref="HueMost"/>). Any other two (a human skin and an unusual one, a grey and a coloured one) only where
        /// they are nearly the same colour (<see cref="SkinLineMost"/>): a green-skinned person never gets a pink picture, a
        /// person of a human skin never a green one.
        /// </summary>
        public static bool SkinAgrees(PortraitColour person, PortraitColour picture)
        {
            if (Math.Abs(SkinTone(person) - SkinTone(picture)) > 1) return false;
            bool odd = UnusualSkin(person), oddPicture = UnusualSkin(picture);
            if (!odd && !oddPicture) return true;
            if (odd && oddPicture)
            {
                Lch(person, out _, out double c1, out double h1);
                Lch(picture, out _, out double c2, out double h2);
                bool grey = c1 < GreyChroma, greyPicture = c2 < GreyChroma;
                if (grey && greyPicture) return true;
                if (!grey && !greyPicture && AngleApart(h1, h2) <= HueMost) return true;
            }
            return ColourApart(person, picture) <= SkinLineMost;
        }

        /// <summary>A picture may stand for the person's body: it shows no mark they haven't got (no eye patch on someone with both eyes, no bionic arm on someone with their own arms), and headwear only where they wear it and the setting shows it.</summary>
        public static bool Shows(PortraitRecord r, PortraitLook l) => ShowsMarks(r, l) && (!r.headwear || l.headwear);

        /// <summary>
        /// Every mark a picture shows is the person's, and a sided one on their side: a picture of a lost left eye stands for
        /// someone who has lost their left eye (or whose side isn't told), never for someone who has lost only the right.
        /// </summary>
        public static bool ShowsMarks(PortraitRecord r, PortraitLook l)
        {
            foreach (var m in r.marks)
            {
                if (!l.marks.Contains(m)) return false;
                if (r.side == PortraitSide.None || !Sided(m)) continue;
                var theirs = l.SidesOf(m);
                if (theirs != PortraitSide.None && (theirs & r.side) == 0) return false;
            }
            return true;
        }

        /// <summary>
        /// A picture's distance from a person in its parts, as the matcher counts them (<see cref="Distance"/> is their sum,
        /// <see cref="Total"/>): the skin's colour, the hair's colour, the hair, the beard, the build, the clothes' age and kind,
        /// the features, the marks, and the looks and work.
        /// </summary>
        public struct DistanceParts
        {
            public float skin, hairColour, hair, beard, build, era, dress, features, marks, looks;

            /// <summary>The sum, in the matcher's order: what <see cref="Distance"/> gives, to the last bit.</summary>
            public float Total
            {
                get
                {
                    float d = 0f;
                    d += skin;
                    d += hairColour;
                    d += hair;
                    d += beard;
                    d += build;
                    d += era;
                    d += dress;
                    d += features;
                    d += marks;
                    d += looks;
                    return d;
                }
            }
        }

        /// <summary>
        /// How far a picture is from a person on their looks (the matcher's kind, sex, age and skin tone rules aside): the hair's
        /// colour most (<see cref="HairColourCost"/>), the skin's colour by its difference as the eye sees it (for every ten
        /// points past the slack), the hair, beard, build and clothes by their tables, the clothes' ages apart, each feature
        /// one has and the other hasn't, each mark of the person's the picture doesn't show, and their looks and work it
        /// doesn't show (<see cref="LookCost"/>). The sum of <see cref="Parts"/>.
        /// </summary>
        public static float Distance(PortraitLook l, PortraitRecord r) => Parts(l, r).Total;

        /// <summary>Each part of a picture's distance from a person (<see cref="Distance"/>), for the log and the window "About this picture".</summary>
        public static DistanceParts Parts(PortraitLook l, PortraitRecord r)
        {
            var p = new DistanceParts();
            if (r.skinSet) p.skin = SkinWeight * Math.Max(0f, SkinApart(l.skin, r.skin) - SkinSlack) / 10f;
            if (r.hairColourSet)
            {
                float hc = HairColourCost(l.hairColour, r.hairColour);
                // Bald on both: the colour hardly shows (a beard's, the brows').
                p.hairColour = l.hair == PortraitHairStyle.Bald && r.hair == PortraitHairStyle.Bald ? hc * 0.5f : hc;
            }
            p.hair = HairWeight * HairApart[(int)l.hair, (int)r.hair];
            p.beard = BeardWeight * BeardApart[(int)l.beard, (int)r.beard];
            p.build = BuildWeight * BuildApart[(int)l.build, (int)r.build];
            if (l.era != PortraitEra.Unknown && r.era != PortraitEra.Unknown) p.era = EraWeight * Math.Abs((int)l.era - (int)r.era);
            p.dress = DressWeight * DressApartOf(l.dress, r.dress);
            foreach (var f in l.features)
                if (!r.features.Contains(f))
                    p.features += FeatureCost;
            foreach (var f in r.features)
                if (!l.features.Contains(f))
                    p.features += FeatureCost;
            foreach (var m in l.marks)
                if (!r.marks.Contains(m))
                    p.marks += MarkCost;
            p.looks = LookCost(l, r);
            return p;
        }

        /// <summary>
        /// The matcher's hard rules for one picture and one person, each met or not (for the window "About this picture"):
        /// their own kind, the sex, their own age band, the skin's tone, the marks (with their side), its looks and work theirs,
        /// the same special looks, headwear only where they wear it.
        /// </summary>
        public struct HardRules
        {
            public bool kind, sex, age, skin, marks, looks, specials, headwear;

            /// <summary>Every rule met: the picture may stand for them as a new pick.</summary>
            public bool All => kind && sex && age && skin && marks && looks && specials && headwear;
        }

        public static HardRules Rules(PortraitRecord r, PortraitLook l)
        {
            var h = new HardRules();
            if (r == null || l == null) return h;
            h.kind = OwnKind(r, l);
            h.sex = SexAgrees(l.sex, r.sex);
            h.age = AgeMayStand(l.age, r.age);
            h.skin = SkinFits(r, l);
            h.marks = ShowsMarks(r, l);
            h.specials = SpecialsAgree(r, l);
            bool looks = r.role.Length == 0 || r.role == l.role;
            foreach (var w in r.looks)
                if (!l.looks.Contains(w))
                    looks = false;
            h.looks = looks;
            h.headwear = !r.headwear || l.headwear;
            return h;
        }

        /// <summary>
        /// Whether a picture someone keeps still may stand for them after its record was corrected in the game: a picture pinned
        /// to them by its stage and special looks (<see cref="PinFits"/>, <see cref="SpecialsAgree"/>), any other by every hard
        /// rule of a new pick (<see cref="Rules"/>). Where it no longer does, they are picked again by the usual rules.
        /// </summary>
        public static bool StillFits(PortraitRecord r, PortraitLook l) => r != null && l != null && (r.Pinned ? PinFits(r, l) && SpecialsAgree(r, l) : Rules(r, l).All);

        /// <summary>
        /// The version of a picture to show for a person now (the player, 23 November 2026: "if a pawn loses an eye ... a
        /// one-eyed ... variant of this specific image"): of the picture (<paramref name="picture"/>) and its mark variants
        /// (<paramref name="variants"/>), those that show only marks of theirs on their side (<see cref="ShowsMarks"/>); the one
        /// showing the most of their marks; of equals, one with an eye patch for someone wearing one and without for anyone
        /// else; then the picture itself; then by name. The picture itself where none fits, or nothing is known of them.
        /// </summary>
        public static int Version(PortraitLook l, IList<PortraitRecord> records, int picture, IList<int> variants)
        {
            if (l == null || records == null || picture < 0 || variants == null || variants.Count == 0) return picture;
            int best = picture, bestShown = -1;
            bool bestPatch = false;
            void Try(int i)
            {
                var r = records[i];
                if (i != picture && !ShowsMarks(r, l)) return;
                int shown = 0;
                foreach (var m in r.marks)
                    if (l.marks.Contains(m))
                        shown++;
                bool patch = r.patch == l.eyePatch;
                bool better = shown > bestShown || shown == bestShown && (patch && !bestPatch || patch == bestPatch && best != picture && (i == picture || string.CompareOrdinal(r.name, records[best].name) < 0));
                if (!better) return;
                best = i;
                bestShown = shown;
                bestPatch = patch;
            }
            Try(picture);
            foreach (int i in variants)
                if (i >= 0 && i < records.Count && i != picture)
                    Try(i);
            return best;
        }

        /// <summary>What a picture's hair colour costs for a person's (<see cref="HairColourWeight"/>): nothing within the slack, a little for a neighbouring shade, at most <see cref="HairColourMost"/>.</summary>
        public static float HairColourCost(PortraitColour person, PortraitColour picture)
        {
            float past = Math.Max(0f, ColourApart(person, picture) - HairSlack) / 10f;
            return Math.Min(HairColourMost, HairColourWeight * past * past);
        }

        /// <summary>
        /// The person's ordinary looks the picture doesn't show (<see cref="LookMissingCost"/> each), and their work where
        /// the picture shows none (<see cref="RoleMissingCost"/>). Special looks and what the picture shows cost nothing
        /// here: a picture that may stand for the person shows only looks and work of theirs (<see cref="LooksAgree"/>).
        /// </summary>
        public static float LookCost(PortraitLook l, PortraitRecord r)
        {
            float d = 0f;
            foreach (var w in l.looks)
                if (!l.specials.Contains(w) && !r.looks.Contains(w))
                    d += LookMissingCost;
            if (l.role.Length > 0 && r.role.Length == 0) d += RoleMissingCost;
            return d;
        }

        /// <summary>
        /// Whether a picture's looks and work may stand for the person (the player, 22 November 2026: "it should always be
        /// also based on an actual pawn's characteristic, and only then also this image is loaded. Otherwise, it should ...
        /// rather choose a default one that doesn't have special things like that"): every look it shows is one of theirs,
        /// it shows every special look of theirs (<see cref="SpecialsAgree"/>), and its work, where it shows one, is theirs.
        /// So a plain picture (no looks, no work) stands for anyone of the kind, sex and age.
        /// </summary>
        public static bool LooksAgree(PortraitRecord r, PortraitLook l)
        {
            foreach (var w in r.looks)
                if (!l.looks.Contains(w))
                    return false;
            if (r.role.Length > 0 && r.role != l.role) return false;
            return SpecialsAgree(r, l);
        }

        /// <summary>
        /// Special looks (the player, 22 November 2026: "rather shows no image than ... a really not fitting one"): a picture
        /// stands for a person with a special look only if it shows it, and a picture showing one only for people who have
        /// it; so the two have the same special looks, or none.
        /// </summary>
        public static bool SpecialsAgree(PortraitRecord r, PortraitLook l)
        {
            if (r.specials.Count != l.specials.Count) return false;
            foreach (var w in l.specials)
                if (!r.specials.Contains(w))
                    return false;
            return true;
        }

        /// <summary>
        /// Whether a kept picture no longer fits someone who has gained a special look it doesn't show (the game's own
        /// drawing then, until a picture with it comes); one who has lost a special look keeps the picture.
        /// </summary>
        public static bool LacksSpecial(PortraitRecord kept, ICollection<string> specialsNow)
        {
            if (kept == null || specialsNow == null) return false;
            foreach (var w in specialsNow)
                if (!kept.specials.Contains(w))
                    return true;
            return false;
        }

        /// <summary>
        /// The person's looks no picture shows that may stand for them by kind, sex, age and skin (one of their own kind, its
        /// sex agreeing, of their age band, its skin agreeing with theirs; a pinned picture only where it is theirs,
        /// <paramref name="theirs"/>), for the missing list: special and ordinary looks alike.
        /// </summary>
        public static List<string> LooksLacking(PortraitLook l, IList<PortraitRecord> records, Func<PortraitRecord, bool> theirs = null)
        {
            var lacking = new List<string>();
            if (l == null || l.looks.Count == 0) return lacking;
            foreach (var w in l.looks.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                bool shown = false;
                if (records != null)
                    foreach (var r in records)
                    {
                        if (r.Pinned && (theirs == null || !theirs(r))) continue;
                        if (!r.looks.Contains(w) || !OwnKind(r, l) || !SexAgrees(l.sex, r.sex) || !AgeMayStand(l.age, r.age) || !SkinFits(r, l)) continue;
                        shown = true;
                        break;
                    }
                if (!shown) lacking.Add(w);
            }
            return lacking;
        }

        /// <summary>
        /// The pictures that may stand for a person, with their distances (infinity for those that may not): never a
        /// picture pinned to a colonist (that is theirs alone); the kind first, exactly (the player, 22 November 2026: "if
        /// there is a xenotype appearing in game and there is no image for it ... it should just show no image ... not take
        /// another xenotype's image": a picture of their own race and xenotype, a baseliner's only for a baseliner, else
        /// none), then sex, then their own age band (<see cref="AgeMayStand"/>), then the skin (<see cref="SkinAgrees"/>:
        /// the same tone or the next; a skin no human grows only one of a near colour), then the body (<see cref="Shows"/>),
        /// then its looks and work (<see cref="LooksAgree"/>: only looks and work of theirs, and every special look of
        /// theirs). Where none is left, no picture (the game's own drawing). No cut-off by distance here: "only close ones"
        /// is applied to what is shown (<see cref="Shown"/>).
        /// </summary>
        public static float[] Distances(PortraitLook l, IList<PortraitRecord> records)
        {
            int n = records?.Count ?? 0;
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = float.PositiveInfinity;
            if (n == 0 || l == null) return d;
            for (int i = 0; i < n; i++)
            {
                var r = records[i];
                if (r.Pinned || !OwnKind(r, l) || !SexAgrees(l.sex, r.sex) || !AgeMayStand(l.age, r.age) || !SkinFits(r, l) || !Shows(r, l) || !LooksAgree(r, l)) continue;
                d[i] = Distance(l, r);
            }
            return d;
        }

        /// <summary>A picture's skin may stand for the person's (<see cref="SkinAgrees"/>); a picture whose record has no skin colour (a def error at start-up) isn't held to it.</summary>
        public static bool SkinFits(PortraitRecord r, PortraitLook l) => !r.skinSet || SkinAgrees(l.skin, r.skin);

        /// <summary>Whether the library has any picture of the person's own kind (not counting pictures pinned to colonists), for the missing list.</summary>
        public static bool HasOwnKind(PortraitLook l, IList<PortraitRecord> records)
        {
            if (l == null || records == null) return false;
            for (int i = 0; i < records.Count; i++)
                if (!records[i].Pinned && OwnKind(records[i], l))
                    return true;
            return false;
        }

        /// <summary>A stable number from the pawn's id, for picking among equals.</summary>
        public static uint Mix(int seed) => unchecked(((uint)seed * 2654435761u) ^ ((uint)seed >> 15) * 40503u);

        /// <summary>
        /// A new pick for a person (the player, 22 November 2026: "it should not be that the same image comes over and over
        /// again frequently ... Rather have a pawn assign no image and we build up the library further in the meanwhile"):
        /// a face nobody else keeps (<paramref name="usesOf"/>, by character) wherever there is one that may stand for them;
        /// else a face others keep where it may be shared (<paramref name="mayShare"/>, with its nearest picture; none given:
        /// any); else none (the game's own drawing, <see cref="PortraitPick.crowded"/> where faces fit but none was free).
        /// Of those, the nearest few characters (each by its nearest picture), the fewest others keep, the nearest of them,
        /// and among equally near ones one by their own id. The closest is picked however far (the setting decides whether
        /// it is shown), and where the nearest is close enough the few are all close enough. So the first person of a look
        /// gets the closest face, and the next alike person the next closest free one. A face <paramref name="taken"/> by
        /// another (a colonist's or a notable's own) is never picked.
        /// </summary>
        public static PortraitPick Pick(PortraitLook l, IList<PortraitRecord> records, int seed, Func<string, int> usesOf, Func<string, bool> taken = null,
            Func<string, PortraitRecord, bool> mayShare = null)
        {
            var d = Distances(l, records);
            var best = new Dictionary<string, int>();
            bool fits = false;
            for (int i = 0; i < d.Length; i++)
            {
                if (float.IsInfinity(d[i])) continue;
                fits = true;
                string c = records[i].Character;
                if (taken != null && taken(c)) continue;
                if (!best.TryGetValue(c, out int j) || d[i] < d[j] || d[i] == d[j] && string.CompareOrdinal(records[i].name, records[j].name) < 0) best[c] = i;
            }
            // A face nobody else keeps first; else one that may be shared; else none.
            var pool = best.Values.Where(i => (usesOf?.Invoke(records[i].Character) ?? 0) == 0).ToList();
            if (pool.Count == 0) pool = best.Values.Where(i => mayShare == null || mayShare(records[i].Character, records[i])).ToList();
            if (pool.Count == 0)
            {
                var none = PortraitPick.None;
                none.crowded = fits;
                return none;
            }
            float nearest = pool.Min(i => d[i]);
            var few = pool.Where(i => d[i] <= nearest + FewWithin && (nearest > CloseEnough || d[i] <= CloseEnough))
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
        /// Whether a kept picture is shown by the setting "Portraits from the library": always for "the closest"; for "only
        /// close ones" a pinned picture or one chosen by hand, or one that was close enough when it was picked.
        /// </summary>
        public static bool Shown(PortraitPickMode mode, bool pinnedOrChosen, float distance) =>
            mode == PortraitPickMode.Closest || pinnedOrChosen || distance <= CloseEnough;

        // ---------------- Pinned pictures ----------------

        /// <summary>A colonist's names a pin is read against: the nickname, the name on the colonist bar, the first, the last and the full name.</summary>
        public struct PinNames
        {
            public string nick, shown, first, last, full;

            public PinNames(string nick, string shown, string first, string last, string full)
            {
                this.nick = nick;
                this.shown = shown;
                this.first = first;
                this.last = last;
                this.full = full;
            }
        }

        private static bool SameName(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// How a pin names a colonist, the most telling first: 0 their nickname, 1 the name on the colonist bar, 2 their
        /// first name, 3 their last name, 4 their full name; -1 not at all. The letters' case and spaces at the ends aside.
        /// </summary>
        public static int PinLevel(string pin, PinNames n)
        {
            if (string.IsNullOrWhiteSpace(pin)) return -1;
            if (SameName(pin, n.nick)) return 0;
            if (SameName(pin, n.shown)) return 1;
            if (SameName(pin, n.first)) return 2;
            if (SameName(pin, n.last)) return 3;
            if (SameName(pin, n.full)) return 4;
            return -1;
        }

        /// <summary>
        /// Whose a pin is among the colony's own people (by id and names): the one it names most tellingly, and of two
        /// named alike the one with the lower id (the longer in the game); -1 for no one, so the picture stays unused.
        /// </summary>
        public static int PinOwner(string pin, IList<KeyValuePair<int, PinNames>> colonists)
        {
            int owner = -1, level = int.MaxValue;
            if (colonists == null) return owner;
            foreach (var c in colonists)
            {
                int l = PinLevel(pin, c.Value);
                if (l < 0) continue;
                if (l < level || l == level && c.Key < owner)
                {
                    level = l;
                    owner = c.Key;
                }
            }
            return owner;
        }

        /// <summary>
        /// How well a pinned picture fits the person now (the player, 22 November 2026): 2 where its stage is theirs (a baby's
        /// for a baby, a toddler's for a toddler, a child's for a child, a teen's for a teen, a young, adult or middle-aged
        /// picture for any of those, an old or elder one for the old); 1 for a picture of the next stage among teens,
        /// grown-ups and the old (a colonist grown old with only a younger picture of their own keeps their own face rather
        /// than a stranger's); 0 where it doesn't fit (or isn't pinned).
        /// </summary>
        public static int PinFit(PortraitRecord r, PortraitLook l)
        {
            if (r == null || l == null || !r.Pinned) return 0;
            PortraitStage pic = StageOf(r.age), now = StageOf(l.age);
            if (pic == now) return 2;
            bool grownSide = pic >= PortraitStage.Teen && now >= PortraitStage.Teen;
            return grownSide && Math.Abs((int)pic - (int)now) == 1 ? 1 : 0;
        }

        public static bool PinFits(PortraitRecord r, PortraitLook l) => PinFit(r, l) > 0;

        /// <summary>
        /// The person's own pinned picture for their stage now (none: -1): of the pictures pinned to them
        /// (<paramref name="theirs"/>) that fit them and show their special looks (<see cref="SpecialsAgree"/>; a pin is
        /// made from the person, so its ordinary looks and work are not held against them), the best fitting, then the
        /// nearest to their looks, then by name.
        /// </summary>
        public static PortraitPick PinFor(PortraitLook l, IList<PortraitRecord> records, Func<PortraitRecord, bool> theirs)
        {
            var pick = PortraitPick.None;
            int bestFit = 0;
            if (l == null || records == null || theirs == null) return pick;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                int fit = PinFit(r, l);
                if (fit == 0 || fit < bestFit || !SpecialsAgree(r, l) || !theirs(r)) continue;
                float d = Distance(l, r);
                if (fit > bestFit || d < pick.distance || d == pick.distance && pick.index >= 0 && string.CompareOrdinal(r.name, records[pick.index].name) < 0)
                {
                    pick = new PortraitPick { index = i, character = r.Character, distance = d };
                    bestFit = fit;
                }
            }
            return pick;
        }

        // ---------------- Kept for good ----------------

        /// <summary>
        /// Whether a person's kept picture gives way to a new pick (the player, 22 November 2026: "it should really select an
        /// image once the pawn, so to say, enters the game the first time based on how the pawn looks like at that stage. And
        /// if a pawn loses an arm later on, I guess it should just for now continue using the same image ... But exceptions
        /// ... if a kid is getting older, enters a new life stage"):
        /// <list type="bullet">
        /// <item>nothing kept yet: picked;</item>
        /// <item>they moved to the next stage (a baby to a toddler, a toddler to a child, a child to a teen, a teen to young,
        /// into old; the player: "eventually, yeah, we should update the image"): picked again (their pin for the new stage,
        /// else the matcher); young, adult and middle-aged keep it, and old to elder;</item>
        /// <item>kept with no picture (none fitted): looked at again when the library or their looks change;</item>
        /// <item>the picture gone from the library: picked again;</item>
        /// <item>its face now belongs to a colonist of the player's (<paramref name="takenByAnother"/>: a colonist's picture is
        /// theirs alone, and of two colonists the one who had it first keeps it): picked again;</item>
        /// <item>a picture pinned to them that fits them better than what they keep (<see cref="PinFit"/>: a pin made for
        /// them where the matcher gave one, an old picture of their own once they are old): the pin (the player's own
        /// choice); one they chose by hand stays;</item>
        /// <item>they have gained a special look their picture doesn't show (<paramref name="lacksSpecial"/>,
        /// <see cref="LacksSpecial"/>): picked again, the game's own drawing until a picture with it comes (even one chosen
        /// by hand); one who has lost a special look keeps theirs.</item>
        /// </list>
        /// Nothing else changes it: no injury, scar or lost eye, no new hair, beard or clothes, no new work, no grown library.
        /// </summary>
        public static bool PickAgain(bool kept, PortraitStage keptStage, PortraitStage stageNow, string keptPicture, bool keptPictureThere, int keptPinFit,
            bool keptByHand, int keptLibrary, int library, string keptWords, string words, int pinFitNow, bool takenByAnother = false, bool lacksSpecial = false)
        {
            if (!kept) return true;
            if (keptStage != stageNow) return true;
            if (string.IsNullOrEmpty(keptPicture)) return keptLibrary != library || keptWords != words;
            if (!keptPictureThere || takenByAnother || lacksSpecial) return true;
            return pinFitNow > keptPinFit && !keptByHand;
        }

        /// <summary>Whose each reserved face is, all of the player's own: <see cref="Owners(IEnumerable{ValueTuple{int, string, int, PortraitRank}}, IEnumerable{KeyValuePair{string, int}})"/>.</summary>
        public static Dictionary<string, int> Owners(IEnumerable<(int id, string character, int since)> reserved, IEnumerable<KeyValuePair<string, int>> pinned) =>
            Owners(reserved?.Select(r => (r.id, r.character, r.since, PortraitRank.Own)), pinned);

        /// <summary>
        /// Whose each reserved face is (the player, 22 November 2026: "once a pawn is part of my colony, their image should no
        /// longer be used for anyone else", and of the colony's contacts "maybe they also get not that their images are
        /// repeated"): of those keeping a face reserved (<paramref name="holders"/>: id, face, since when, rank: the player's
        /// own or a notable; a stranger's holds nothing), the highest rank, then the one who had it first, then the lower
        /// id. A face pinned to a colonist is reserved for them from the start, above everyone (<paramref name="pinned"/>:
        /// face, owner; -1 for a pin naming no one, so no one else gets it either).
        /// </summary>
        public static Dictionary<string, int> Owners(IEnumerable<(int id, string character, int since, PortraitRank rank)> holders, IEnumerable<KeyValuePair<string, int>> pinned)
        {
            var owners = new Dictionary<string, int>(StringComparer.Ordinal);
            var first = new Dictionary<string, (PortraitRank rank, int since, int id)>(StringComparer.Ordinal);
            if (pinned != null)
                foreach (var kv in pinned)
                    if (!string.IsNullOrEmpty(kv.Key) && (!owners.TryGetValue(kv.Key, out int o) || o < 0))
                        owners[kv.Key] = kv.Value;
            if (holders != null)
                foreach (var r in holders)
                {
                    if (string.IsNullOrEmpty(r.character) || r.rank == PortraitRank.Stranger || owners.ContainsKey(r.character) && !first.ContainsKey(r.character)) continue;
                    if (!first.TryGetValue(r.character, out var f) || r.rank > f.rank || r.rank == f.rank && (r.since < f.since || r.since == f.since && r.id < f.id))
                    {
                        first[r.character] = (r.rank, r.since, r.id);
                        owners[r.character] = r.id;
                    }
                }
            return owners;
        }

        // ---------------- Strangers sharing a face ----------------

        /// <summary>
        /// Whether a face other strangers keep may be given to a stranger too (the player, 22 November 2026: "with things
        /// like raiders or guests, then I'm of course more lenient when it comes to repeating certain images, if it makes
        /// sense", and of special looks: "you can reuse that more frequently also, because these characters are also more
        /// rare to come up"): a picture with a special look always; else as the setting says: never, only while no one
        /// keeping it is on a map (<paramref name="holderSeen"/>), or always.
        /// </summary>
        public static bool MayShare(PortraitShareMode mode, bool specialPicture, bool holderSeen) =>
            specialPicture || mode == PortraitShareMode.Always || mode == PortraitShareMode.NotTogether && !holderSeen;

        /// <summary>
        /// Whether a stranger gives up a face another stranger keeps too (the player: "not that you have this character and
        /// you have another random character come up and they look exactly the same"): never for a picture with a special
        /// look or with "always"; with "never", whenever someone who had it first (<paramref name="others"/>: id, since when,
        /// on a map now) keeps it too; with "when not seen together", when they are on a map (<paramref name="seen"/>) and
        /// someone who had it first is on one too. The one who came later gives way: a new pick, or the game's own drawing.
        /// </summary>
        public static bool GivesWay(PortraitShareMode mode, bool specialPicture, int id, int since, bool seen, IEnumerable<(int id, int since, bool seen)> others)
        {
            if (specialPicture || mode == PortraitShareMode.Always || others == null) return false;
            foreach (var o in others)
            {
                if (o.id == id) continue;
                bool earlier = o.since < since || o.since == since && o.id < id;
                if (!earlier) continue;
                if (mode == PortraitShareMode.Never || seen && o.seen) return true;
            }
            return false;
        }

        // ---------------- Choose another picture ----------------

        /// <summary>
        /// The pictures "Choose another picture" steps through for a person, in order (indices): their own pinned pictures
        /// that fit their stage, nearest first; then each face the matcher may give them, by its nearest picture, nearest
        /// first (for "only close ones", those close enough only); at most <see cref="ChoicesMost"/>.
        /// </summary>
        public static List<int> Choices(PortraitLook l, IList<PortraitRecord> records, Func<PortraitRecord, bool> theirs, PortraitPickMode mode, Func<string, bool> taken = null)
        {
            var list = new List<int>();
            if (l == null || records == null) return list;
            var pins = new List<KeyValuePair<int, float>>();
            for (int i = 0; i < records.Count; i++)
                if (records[i].Pinned && theirs != null && theirs(records[i]) && PinFits(records[i], l) && SpecialsAgree(records[i], l))
                    pins.Add(new KeyValuePair<int, float>(i, Distance(l, records[i])));
            list.AddRange(pins.OrderByDescending(x => PinFit(records[x.Key], l)).ThenBy(x => x.Value).ThenBy(x => records[x.Key].name, StringComparer.Ordinal).Select(x => x.Key));
            var d = Distances(l, records);
            var best = new Dictionary<string, int>();
            for (int i = 0; i < d.Length; i++)
            {
                if (float.IsInfinity(d[i]) || mode == PortraitPickMode.OnlyClose && d[i] > CloseEnough) continue;
                string c = records[i].Character;
                if (taken != null && taken(c)) continue;
                if (!best.TryGetValue(c, out int j) || d[i] < d[j] || d[i] == d[j] && string.CompareOrdinal(records[i].name, records[j].name) < 0) best[c] = i;
            }
            list.AddRange(best.Values.OrderBy(i => d[i]).ThenBy(i => records[i].Character, StringComparer.Ordinal));
            if (list.Count > ChoicesMost) list.RemoveRange(ChoicesMost, list.Count - ChoicesMost);
            return list;
        }

        /// <summary>The choice after the picture shown now (by name; the first where it is none of them), or -1 where there is no other.</summary>
        public static int NextChoice(IList<int> choices, IList<PortraitRecord> records, string shownNow)
        {
            if (choices == null || choices.Count == 0) return -1;
            int at = -1;
            for (int k = 0; k < choices.Count; k++)
                if (records[choices[k]].name == shownNow)
                    at = k;
            if (at < 0) return choices[0];
            return choices.Count == 1 ? -1 : choices[(at + 1) % choices.Count];
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

        /// <summary>A colour's lightness, chroma (how strong its colour is: 0 a grey) and hue in degrees (0 to 360), CIE LCh.</summary>
        public static void Lch(PortraitColour c, out double l, out double chroma, out double hue)
        {
            Lab(c, out l, out double a, out double b);
            chroma = Math.Sqrt(a * a + b * b);
            hue = Math.Atan2(b, a) * 180.0 / Math.PI;
            if (hue < 0) hue += 360.0;
        }

        /// <summary>How far apart two hues are, in degrees (0 to 180), the short way round.</summary>
        public static double AngleApart(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
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
