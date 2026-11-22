using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// One look a picture can show (a <c>PortraitLookDef</c>, read into plain words): its word, whether it is special, and
    /// what gives it, by def name: traits (a trait's name, or its name and one degree), genes, hediffs, and the race's body.
    /// Any one of them gives the look.
    /// </summary>
    public sealed class LookRule
    {
        public string word = "";
        public bool special;
        /// <summary>The traits that give it: the trait's def name and, where the line names one, its degree (null: any degree).</summary>
        public List<KeyValuePair<string, int?>> traits = new List<KeyValuePair<string, int?>>();
        public HashSet<string> genes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> hediffs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> bodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool Empty => traits.Count == 0 && genes.Count == 0 && hediffs.Count == 0 && bodies.Count == 0;

        public override string ToString() => word + (special ? " (special)" : "");
    }

    /// <summary>A look a person has, and what gave it to them: "trait", "gene", "hediff" or "body", and that def's name.</summary>
    public struct LookFound
    {
        public string word;
        public bool special;
        public string from, def;

        public LookFound(string word, bool special, string from, string def)
        {
            this.word = word;
            this.special = special;
            this.from = from;
            this.def = def;
        }
    }

    /// <summary>
    /// Every look the game knows (the looks' defs, read once at start-up), by word and by what gives each, so a person's
    /// looks are found by a few lookups: their traits, active genes, hediffs and body each looked up once.
    /// </summary>
    public sealed class LookBook
    {
        private readonly Dictionary<string, LookRule> byWord = new Dictionary<string, LookRule>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<KeyValuePair<LookRule, int?>>> byTrait = new Dictionary<string, List<KeyValuePair<LookRule, int?>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<LookRule>> byGene = new Dictionary<string, List<LookRule>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<LookRule>> byHediff = new Dictionary<string, List<LookRule>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<LookRule>> byBody = new Dictionary<string, List<LookRule>>(StringComparer.OrdinalIgnoreCase);

        public readonly List<LookRule> rules = new List<LookRule>();

        /// <summary>Whether any look is special (else a person's special looks need not be read at all).</summary>
        public bool AnySpecial { get; private set; }

        public int Count => rules.Count;

        /// <summary>
        /// The book of these looks; what is wrong across them in <paramref name="problems"/>: a word given twice (the
        /// first kept).
        /// </summary>
        public LookBook(IEnumerable<LookRule> looks, List<string> problems = null)
        {
            if (looks == null) return;
            foreach (var r in looks)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.word)) continue;
                if (byWord.ContainsKey(r.word))
                {
                    problems?.Add("the look '" + r.word + "' is given twice; the first is kept");
                    continue;
                }
                byWord[r.word] = r;
                rules.Add(r);
                if (r.special) AnySpecial = true;
                foreach (var t in r.traits) Add(byTrait, t.Key, new KeyValuePair<LookRule, int?>(r, t.Value));
                foreach (var g in r.genes) Add(byGene, g, r);
                foreach (var h in r.hediffs) Add(byHediff, h, r);
                foreach (var b in r.bodies) Add(byBody, b, r);
            }
        }

        private static void Add<T>(Dictionary<string, List<T>> d, string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!d.TryGetValue(key, out var list))
            {
                list = new List<T>();
                d[key] = list;
            }
            list.Add(value);
        }

        /// <summary>The look of a word (the letters' case aside), or null.</summary>
        public LookRule Find(string word) => !string.IsNullOrWhiteSpace(word) && byWord.TryGetValue(word.Trim(), out var r) ? r : null;

        /// <summary>
        /// The looks a person has, each once, with what gave it: their traits (name and degree; a line with a degree only
        /// for that degree), their active genes, their hediffs and their race's body. With <paramref name="specialOnly"/>,
        /// only the special looks (read every few seconds for a person whose picture is kept).
        /// </summary>
        public List<LookFound> Of(IEnumerable<KeyValuePair<string, int>> traits, IEnumerable<string> genes, IEnumerable<string> hediffs, string body, bool specialOnly = false)
        {
            var found = new List<LookFound>();
            if (rules.Count == 0 || specialOnly && !AnySpecial) return found;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Give(LookRule r, string from, string def)
            {
                if (specialOnly && !r.special || !seen.Add(r.word)) return;
                found.Add(new LookFound(r.word, r.special, from, def));
            }
            if (!string.IsNullOrEmpty(body) && byBody.TryGetValue(body, out var bs))
                foreach (var r in bs)
                    Give(r, "body", body);
            if (traits != null)
                foreach (var t in traits)
                    if (t.Key != null && byTrait.TryGetValue(t.Key, out var ts))
                        foreach (var kv in ts)
                            if (kv.Value == null || kv.Value.Value == t.Value)
                                Give(kv.Key, "trait", t.Key);
            if (genes != null)
                foreach (var g in genes)
                    if (g != null && byGene.TryGetValue(g, out var gs))
                        foreach (var r in gs)
                            Give(r, "gene", g);
            if (hediffs != null)
                foreach (var h in hediffs)
                    if (h != null && byHediff.TryGetValue(h, out var hs))
                        foreach (var r in hs)
                            Give(r, "hediff", h);
            return found;
        }
    }

    /// <summary>
    /// Looks (docs: DESIGN.md, "Portraits, phase 5: looks, special looks and what the library lacks"; the player, 22
    /// November 2026: "if like a trait appears where they could be visually displayed ... any other potential special
    /// things that a pawn could have that would require a dedicated image ... co-join twins ... That should then, of course,
    /// also be visible. Or if there are certain traits like this ... that the game then rather shows no image than ... a
    /// really not fitting one"), without game types (Tools/Tests try them):
    /// <list type="bullet">
    /// <item>A look's def read into a rule (<see cref="Rule"/>): its word, special or not, and the lines that give it; a
    /// trait line "DrugDesire:2" for one degree, "Pyromaniac" for any. Every name is soft: one naming a def that isn't
    /// loaded never matches.</item>
    /// <item>The book of every look (<see cref="Book"/>), set once at start-up by the game, read by the records (an unknown
    /// word in a record's <c>looks</c> is a problem as any unknown word is) and by the reader of a pawn's looks.</item>
    /// </list>
    /// The matcher's part (a picture showing a look or a work stands only for people who have it; a look of theirs a
    /// picture doesn't show costs a little, a special look of theirs it doesn't show rules it out) is in
    /// <see cref="PortraitLibraryMath.LooksAgree"/> and <see cref="PortraitLibraryMath.LookCost"/>.
    /// </summary>
    public static class PortraitLookMath
    {
        /// <summary>The looks the game knows now; empty until the game has read the looks' defs (and in tests, until a test sets it).</summary>
        public static LookBook Book { get; private set; } = new LookBook(null);

        /// <summary>The book set (the game, once its defs are read; the tests, with their own looks).</summary>
        public static void Use(LookBook book) => Book = book ?? new LookBook(null);

        /// <summary>
        /// A trait line: "Pyromaniac" (any degree) or "Beauty:-1" (that degree; a sign allowed). False where the degree is
        /// no whole number or the name is empty.
        /// </summary>
        public static bool TraitLine(string text, out string name, out int? degree)
        {
            name = null;
            degree = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            int at = t.LastIndexOf(':');
            if (at < 0)
            {
                name = t;
                return true;
            }
            name = t.Substring(0, at).Trim();
            string d = t.Substring(at + 1).Trim();
            if (name.Length == 0 || !int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return false;
            degree = v;
            return true;
        }

        /// <summary>Whether a look's word is one the records and the lead's scripts can carry: letters, digits, '-' or '_', no spaces, commas or semicolons.</summary>
        public static bool GoodWord(string word) =>
            !string.IsNullOrWhiteSpace(word) && word.Trim().All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

        /// <summary>
        /// A look's rule from its def's fields (each list as the def holds it), with what is wrong with it: no word, a word
        /// with spaces or commas, a trait line whose degree is no number, nothing that gives the look.
        /// </summary>
        public static LookRule Rule(string word, bool special, IEnumerable<string> traits, IEnumerable<string> genes, IEnumerable<string> hediffs, IEnumerable<string> bodies,
            List<string> problems)
        {
            var r = new LookRule { word = (word ?? "").Trim(), special = special };
            if (r.word.Length == 0) problems?.Add("no word: the word records use in <looks>");
            else if (!GoodWord(r.word)) problems?.Add("word '" + r.word + "': letters and digits only (no spaces, commas or semicolons), as records write it");
            foreach (var t in traits ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (TraitLine(t, out string name, out int? degree)) r.traits.Add(new KeyValuePair<string, int?>(name, degree));
                else problems?.Add("traits: '" + t + "' is not a trait's name, or its name and a degree (Beauty:-1)");
            }
            foreach (var g in genes ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(g))
                    r.genes.Add(g.Trim());
            foreach (var h in hediffs ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(h))
                    r.hediffs.Add(h.Trim());
            foreach (var b in bodies ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(b))
                    r.bodies.Add(b.Trim());
            if (r.Empty) problems?.Add("no traits, genes, hediffs or bodies: nobody would have this look");
            return r;
        }

        // ---------------- Work ----------------

        /// <summary>
        /// The work a picture can show (the player, 22 November 2026: "the profession can and should also be reflected in
        /// how they look like ... if someone is doing a lot of science, has high intelligence ... look like a scientist ...
        /// soldiers being soldiers"): a record's <c>role</c>, one of these words, or none for any.
        /// </summary>
        public static readonly string[] Roles =
            { "scholar", "doctor", "soldier", "warrior", "hunter", "scout", "builder", "miner", "crafter", "smith", "farmer", "herder", "cook", "artist", "trader", "leader", "priest", "labourer" };

        /// <summary>A role as the records write it (the letters' case aside; "laborer" the same as "labourer"), or null for none of them.</summary>
        public static string RoleWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim().ToLowerInvariant();
            if (t == "laborer") t = "labourer";
            return Roles.Contains(t) ? t : null;
        }

        /// <summary>
        /// The words a backstory's name, title or spawn categories use for a work, and the work each means (the game's,
        /// Vanilla Backstories Expanded's and Vanilla Factions Expanded - Medieval 2's backstories in <c>docs/player-mods</c>
        /// among them: "InfantryMedic", "VFEM_Blacksmith", "VBE_StewCooker").
        /// </summary>
        private static readonly Dictionary<string, string> RoleWords = BuildRoleWords();

        private static Dictionary<string, string> BuildRoleWords()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            void W(string role, string words)
            {
                foreach (var w in words.Split(' '))
                    if (w.Length > 0)
                        d[w] = role;
            }
            W("soldier", "soldier mercenary merc guard guards guardsman marine marines trooper infantry infanteer militia militiaman sergeant veteran rifleman sniper sharpshooter "
                         + "gunner artilleryman legionary legionnaire commando conscript cadet officer footman pikeman spearman halberdier landsknecht janissary housecarl watchman "
                         + "defender security counterterrorist cannonfodder deserter bodyguard fighter gunslinger");
            W("warrior", "warrior gladiator knight berserker brawler swordsman swordswoman duelist champion marauder blackguard crusader squire shieldbearer raider barbarian "
                         + "bandit pitfighter wrestler");
            W("hunter", "hunter huntress trapper archer poacher tracker falconer bowman");
            W("scout", "scout ranger explorer spy pathfinder courier messenger runner infiltrator wanderer drifter traveller traveler nomad navigator adventurer agent");
            W("scholar", "scientist researcher scholar professor student teacher academic inventor librarian scribe philosopher astronomer physicist alchemist archivist "
                         + "tutor analyst chemist archaeologist programmer hacker mathematician biologist geneticist theorist sage intellectual translator critic historian");
            W("doctor", "doctor surgeon medic nurse physician healer paramedic herbalist apothecary midwife caregiver pharmacist therapist");
            W("builder", "builder architect mason carpenter bricklayer brickmaker construction constructor foreman demolitionist roofer structural");
            W("miner", "miner digger prospector quarryman driller drill excavator");
            W("crafter", "crafter craftsman craftswoman artisan tailor seamster seamstress weaver tinker tinkerer mechanic technician tech engineer machinist assembler "
                         + "jeweler jeweller locksmith cooper maker artificer fabricator repairman maintainer");
            W("smith", "smith blacksmith armorer armourer gunsmith weaponsmith bladesmith metalworker");
            W("farmer", "farmer farmhand farmstead farm peasant gardener botanist grower landworker miller field fields fisher fisherman");
            W("herder", "herder herds herd shepherd rancher cowboy tamer beastmaster zookeeper stablehand kennel menagerie herdsman animal");
            W("cook", "cook chef baker butcher brewer cooker sommelier dietetician");
            W("artist", "artist painter sculptor musician bard singer poet actor dancer performer writer entertainer minstrel idol rocker designer playwright novelist "
                        + "jester juggler acrobat storyteller photographer composer arts");
            W("trader", "trader merchant salesman saleswoman dealer peddler shopkeeper smuggler negotiator diplomat broker banker financier accountant auditor fence "
                        + "supplier bureaucrat clerk");
            W("leader", "leader chief chieftain noble lord lordling king queen prince princess baron baroness countess duke duchess governor mayor commander warlord "
                        + "captain boss executive regent ruler aristocrat castellan thane guildmaster politician commissar emperor empress magistrate consul senator heir");
            W("priest", "priest priestess monk nun cleric preacher missionary shaman prophet acolyte oracle druid cultist bishop abbot chaplain inquisitor zealot church");
            W("labourer", "laborer labourer worker slave servant janitor porter hauler dockworker menial drudge butler maid scullion gravedigger cleaner handler drone "
                          + "ratcatcher page");
            return d;
        }

        /// <summary>
        /// The work a backstory clearly names, from its words in this order: its title, its name, its spawn categories; in
        /// each, the last word that names a work (the head of the phrase: "infantry medic" a doctor, "army cook" a cook,
        /// "military chaplain" a priest). Null where none does.
        /// </summary>
        public static string RoleFromWords(IList<string> titleTokens, IList<string> nameTokens, IList<string> categoryTokens)
        {
            foreach (var tokens in new[] { titleTokens, nameTokens, categoryTokens })
            {
                if (tokens == null) continue;
                for (int i = tokens.Count - 1; i >= 0; i--)
                    if (tokens[i] != null && RoleWords.TryGetValue(tokens[i], out var role))
                        return role;
            }
            return null;
        }

        /// <summary>One of a person's skills: its def name, level, passion (0 none, 1 minor, 2 major) and whether it is disabled.</summary>
        public struct SkillSeen
        {
            public string skill;
            public int level, passion;
            public bool disabled;

            public SkillSeen(string skill, int level, int passion, bool disabled = false)
            {
                this.skill = skill;
                this.level = level;
                this.passion = passion;
                this.disabled = disabled;
            }
        }

        /// <summary>The work each of the game's skills shows (shooting a soldier's, or a hunter's among people of the bow).</summary>
        private static readonly string[] SkillOrder = { "Intellectual", "Medicine", "Shooting", "Melee", "Construction", "Mining", "Crafting", "Plants", "Animals", "Cooking", "Artistic", "Social" };

        private static string RoleOfSkill(string skill, bool tribal)
        {
            switch (skill)
            {
                case "Intellectual": return "scholar";
                case "Medicine": return "doctor";
                case "Shooting": return tribal ? "hunter" : "soldier";
                case "Melee": return "warrior";
                case "Construction": return "builder";
                case "Mining": return "miner";
                case "Crafting": return "crafter";
                case "Plants": return "farmer";
                case "Animals": return "herder";
                case "Cooking": return "cook";
                case "Artistic": return "artist";
                case "Social": return "trader";
                default: return null;
            }
        }

        /// <summary>
        /// The work their best skill with a passion shows: of the game's twelve skills they have a passion for (not
        /// disabled), the highest level, then the burning passion, then the skills' order; null for none.
        /// <paramref name="tribal"/>: people of the bow (their people's age neolithic or before), whose shooting is a hunter's.
        /// </summary>
        public static string RoleFromSkills(IEnumerable<SkillSeen> skills, bool tribal)
        {
            SkillSeen? best = null;
            int bestAt = int.MaxValue;
            if (skills == null) return null;
            foreach (var s in skills)
            {
                int at = Array.IndexOf(SkillOrder, s.skill);
                if (at < 0 || s.disabled || s.passion <= 0) continue;
                if (best == null || s.level > best.Value.level || s.level == best.Value.level && (s.passion > best.Value.passion || s.passion == best.Value.passion && at < bestAt))
                {
                    best = s;
                    bestAt = at;
                }
            }
            return best == null ? null : RoleOfSkill(best.Value.skill, tribal);
        }
    }
}
