using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>Why the council may name one of its fallen a saint (SaintsMath.Merit): the strongest reason their death gives.</summary>
    public enum SaintReason
    {
        None,
        /// <summary>Died carrying, rescuing or tending someone while the colony was under attack.</summary>
        Rescuer,
        /// <summary>Died holding the line against an attack, a year or more with the colony.</summary>
        Defender,
        /// <summary>The colony's healer: years of tending the sick and the hurt.</summary>
        Healer,
        /// <summary>Kept a god's ways devotedly (or the colony's priest) for two years or more.</summary>
        Keeper,
        /// <summary>Died with a vow still being kept.</summary>
        Vow
    }

    /// <summary>What a colonist's death was, as SaintsMath.Merit reads it (from the game at the moment they died: Gods/Saints/SaintsPatches.cs).</summary>
    public struct SaintDeath
    {
        /// <summary>A free colonist of the colony's own, grown, with an inner life (never a guest, a prisoner, a slave or a child).</summary>
        public bool ours;
        public bool scoffer;
        /// <summary>The god they held first is the Hunger: never a saint.</summary>
        public bool hunger;
        /// <summary>Days with the colony.</summary>
        public int days;
        /// <summary>Killed by an enemy's hand while the colony was under attack.</summary>
        public bool byEnemy;
        public bool drafted;
        /// <summary>Carrying, rescuing or tending someone when they fell.</summary>
        public bool rescuing;
        public int medicine;
        public bool medicinePassion;
        /// <summary>How many times they tended someone else (the game's own record).</summary>
        public int tended;
        /// <summary>Devoted (or more) to the god they held first.</summary>
        public bool devoted;
        public bool priest;
        /// <summary>A vow still being kept.</summary>
        public bool vow;
        /// <summary>A god's presence on the land when they died.</summary>
        public bool godNear;
    }

    /// <summary>
    /// The pure rules of the saints (docs/gods-next-proposals.md, section F, option b; the player, 14 November 2026: "perhaps
    /// instead of these being the gods that are currently registered to an ideology, this could, for example, be some sort of
    /// saints or important characters ... kind of a middle ground between God and the pawns", then "let's go with saints then").
    /// Ideology's deities are read as the faith's saints, or its damned, each tied to one of the gods by its own words
    /// (GodsMath.DeityDomains, then <see cref="GodFor"/>); a fallen colonist's death may merit a saint (<see cref="Merit"/>);
    /// the words by age and culture; the feast day; statues; the lean a saint gives its god. Free of game types so Tools/Tests
    /// can check them. Never a dynasty: a saint is a person honoured, never their kin.
    /// </summary>
    public static class SaintsMath
    {
        // ================= A death that merits a saint =================

        /// <summary>The score a death needs: a rescue under attack alone, else a deed and a year with the colony (or a vow kept, or a god near).</summary>
        public const float MeritFrom = 2.5f;

        /// <summary>Days with the colony before anyone could call them one of its own saints (a rescue under attack excepted).</summary>
        public const int MinDays = 15;

        /// <summary>A year with the colony, which counts for a little.</summary>
        public const int YearDays = 60;

        /// <summary>The keeper's long years: two years devoted (or as priest).</summary>
        public const int KeeperDays = 120;

        /// <summary>The healer: a skilled hand that has tended many, or a passion and many more.</summary>
        public const int HealerSkill = 12, HealerTended = 80, HealerPassionSkill = 8, HealerPassionTended = 150;

        /// <summary>
        /// Whether a death merits a saint, and why: the strongest of its reasons, with <paramref name="score"/> their sum. A
        /// rescue under attack (3) merits it alone; holding the line (2), the healer's years (2) and the keeper's (2, 2.5 for a
        /// devoted priest) with a year with the colony (+0.5), a vow kept to the end (+1) or a god's presence on the land (+0.5).
        /// Never a scoffer, a child, a slave, a guest, one who held the Hunger first, or one new to the colony.
        /// </summary>
        public static SaintReason Merit(SaintDeath d, out float score)
        {
            score = 0f;
            if (!d.ours || d.scoffer || d.hunger || d.days < 0) return SaintReason.None;
            var reason = SaintReason.None;
            float best = 0f, sum = 0f;
            void Reason(SaintReason r, float s)
            {
                sum += s;
                if (s > best)
                {
                    best = s;
                    reason = r;
                }
            }
            if (d.byEnemy && d.rescuing) Reason(SaintReason.Rescuer, 3f);
            else if (d.byEnemy && d.drafted) Reason(SaintReason.Defender, 2f);
            if (d.medicine >= HealerSkill && d.tended >= HealerTended || d.medicinePassion && d.medicine >= HealerPassionSkill && d.tended >= HealerPassionTended)
                Reason(SaintReason.Healer, 2f);
            if ((d.devoted || d.priest) && d.days >= KeeperDays) Reason(SaintReason.Keeper, d.devoted && d.priest ? 2.5f : 2f);
            if (d.vow) Reason(SaintReason.Vow, 1f);
            if (d.days >= YearDays) sum += 0.5f;
            if (d.godNear) sum += 0.5f;
            score = sum;
            if (reason == SaintReason.None) return SaintReason.None;
            if (reason != SaintReason.Rescuer && d.days < MinDays) return SaintReason.None;
            return score >= MeritFrom ? reason : SaintReason.None;
        }

        /// <summary>The domain a reason speaks of, for the god of one who held none: the hearth's for a rescue, war's for the line, healing's for the healer.</summary>
        public static GodDomain ReasonDomain(SaintReason r) => r switch
        {
            SaintReason.Rescuer => GodDomain.Hearth,
            SaintReason.Defender => GodDomain.War,
            SaintReason.Healer => GodDomain.Healing,
            _ => GodDomain.None
        };

        /// <summary>A waiting death is asked about within this many days, or let go.</summary>
        public const int CandidateDays = 12;

        /// <summary>At most this many deaths wait for the council at once (the newest kept).</summary>
        public const int MaxWaiting = 4;

        // ================= A faith's deity read as a saint =================

        /// <summary>Memes whose deity the colony reads as one of the damned (the game's and the player's mods', by defName).</summary>
        public static readonly HashSet<string> DarkMemes = new HashSet<string>
        {
            "Raider", "Cannibal", "PainIsVirtue", "Bloodfeeding", "Inhuman", "Ritualist", "VFEP_PirateMeme",
            "AM_BiologicalCorruptors", "AM_BiologicalDefilers", "AM_Madness", "AM_PainIsDivine", "AM_PsychicVampirism", "AM_Sadist",
            "AM_Structure_FleshCult", "AM_Structure_Horaxian", "VME_BloodCourt", "VME_ViolentConversion", "VME_Structure_ChthonianCult"
        };

        /// <summary>Words in a deity's name or title (or its meme's label) that mark one of the damned, at a word's start.</summary>
        public static readonly string[] DarkWords =
        {
            "devour", "blood", "torment", "slaughter", "carnage", "cruel", "agony", "madness", "abyss", "void", "hunger", "flesh", "murder",
            "plunder", "pillag", "corrupt", "defile", "eldritch", "raid", "cannibal"
        };

        /// <summary>A word of <see cref="DarkWords"/> among these words.</summary>
        public static bool DarkWordsIn(string words)
        {
            if (string.IsNullOrEmpty(words)) return false;
            foreach (var w in Split(words))
                foreach (var d in DarkWords)
                    if (w.StartsWith(d, StringComparison.Ordinal))
                        return true;
            return false;
        }

        private static IEnumerable<string> Split(string s) =>
            s.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-', '\'', '(', ')', ':', ';', '!', '?', '"' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// One of the damned, not a saint: its meme is a dark one (<see cref="DarkMemes"/>), its own words or its meme's label
        /// are dark, or its domain is the void.
        /// </summary>
        public static bool Damned(string relatedMeme, string relatedMemeLabel, string name, string type, GodDomain domain)
        {
            if (domain == GodDomain.Void) return true;
            if (!string.IsNullOrEmpty(relatedMeme) && DarkMemes.Contains(relatedMeme)) return true;
            return DarkWordsIn(type) || DarkWordsIn(name) || DarkWordsIn(relatedMemeLabel);
        }

        /// <summary>The gods the damned are tied to: the Hunger, and the darker of the rest (the long white, the coil under the mountain).</summary>
        public static readonly HashSet<string> DarkGods = new HashSet<string> { "Undertow", "Hush", "Smoulder" };

        /// <summary>Domains no god holds first, and the gods nearest them: war to the coil and the storm, healing to the soft mother.</summary>
        private static readonly (GodDomain d, string god, float w)[] Near =
        {
            (GodDomain.War, "Smoulder", 1.5f), (GodDomain.War, "Cirrus", 1f), (GodDomain.Healing, "Morel", 1f)
        };

        /// <summary>
        /// The god a deity is tied to, by the domains its words give (GodsMath.DeityDomains): a god whose own domain is the
        /// deity's scores 3, whose second is 2; the deity's second domain 1 and 0.5; a domain no god holds first goes to the
        /// nearest (<see cref="Near"/>). A saint is never tied to the Hunger; one of the damned only to the dark gods
        /// (<see cref="DarkGods"/>, the Hunger kind among them), the Hunger when nothing else fits. Ties go by the seed (the
        /// deity's name), so a deity keeps its god. Null with no god to tie it to.
        /// </summary>
        public static string GodFor(GodDomain domain, GodDomain second, bool damned, IList<(string key, GodDomain d, GodDomain s, bool hunger)> gods, int seed)
        {
            if (gods == null || gods.Count == 0) return null;
            var pool = gods.Where(g => !string.IsNullOrEmpty(g.key) && (damned ? g.hunger || DarkGods.Contains(g.key) : !g.hunger)).ToList();
            if (pool.Count == 0) return null;
            float Score((string key, GodDomain d, GodDomain s, bool hunger) g)
            {
                float v = 0f;
                if (domain != GodDomain.None)
                {
                    if (g.d == domain) v += 3f;
                    if (g.s == domain) v += 2f;
                }
                if (second != GodDomain.None)
                {
                    if (g.d == second) v += 1f;
                    if (g.s == second) v += 0.5f;
                }
                foreach (var (d, god, w) in Near)
                {
                    if (god != g.key) continue;
                    if (d == domain) v += w;
                    else if (d == second) v += w * 0.3f;
                }
                // The Hunger takes the damned nothing else holds.
                if (damned && g.hunger) v += 0.5f;
                return v;
            }
            var scored = pool.Select(g => (g.key, v: Score(g))).ToList();
            float top = scored.Max(x => x.v);
            if (top <= 0f) return damned ? pool.FirstOrDefault(g => g.hunger).key ?? pool[0].key : null;
            var best = scored.Where(x => x.v >= top - 0.001f).Select(x => x.key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            return best[(int)((uint)seed % (uint)best.Count)];
        }

        /// <summary>A stable seed from a deity's name (the same deity always the same god among equals).</summary>
        public static int Seed(string name)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in name ?? "") h = h * 31 + c;
                return h & 0x7FFFFFFF;
            }
        }

        // ================= Words by age and culture =================

        /// <summary>
        /// The word a faith's saints are called by (a key's ending: CC_Saint_Word_, _Title_, _A_, _AOf_): by the faith's age
        /// (the ancestors of the tribes, the saints of the medieval and industrial ages, the exemplars of spacers), else its
        /// culture's where it has its own (the Corunan's venerated, the Kriminul's martyrs).
        /// </summary>
        public static string WordSet(int age, string culture)
        {
            if (!string.IsNullOrEmpty(culture) && Cultures.TryGetValue(culture, out var own)) return own;
            return Math.Max(0, Math.Min(3, age)) switch
            {
                0 => "Ancestors",
                3 => "Exemplars",
                _ => "Saints"
            };
        }

        /// <summary>The damned's word by age: the forsaken of the tribes, the damned, the fallen of spacers.</summary>
        public static string DamnedSet(int age) => Math.Max(0, Math.Min(3, age)) switch
        {
            0 => "Forsaken",
            3 => "Fallen",
            _ => "Damned"
        };

        /// <summary>Cultures with a word of their own for their holy dead (the game's, by CultureDef defName).</summary>
        public static readonly Dictionary<string, string> Cultures = new Dictionary<string, string>
        {
            ["Corunan"] = "Venerated",
            ["Kriminul"] = "Martyrs"
        };

        /// <summary>Every word set, for the keyed strings each needs.</summary>
        public static readonly string[] Sets = { "Ancestors", "Saints", "Exemplars", "Venerated", "Martyrs", "Forsaken", "Damned", "Fallen" };

        /// <summary>The faith's word on the ideoligion screen: its saints', or its damned's when every one of them is damned.</summary>
        public static string HeaderSet(int age, string culture, int holy, int damned) => holy == 0 && damned > 0 ? DamnedSet(age) : WordSet(age, culture);

        /// <summary>An age from a tech level (the game's TechLevel as a number: 1 animal, 2 neolithic, 3 medieval, 4 industrial, 5 spacer up).</summary>
        public static int AgeOfTech(int tech) => tech <= 2 ? 0 : tech == 3 ? 1 : tech == 4 ? 2 : 3;

        // ================= What a saint gives their god =================

        /// <summary>How much a saint of the faith adds to its god in what the faith draws its believers toward (a share of the strongest, 0 to 1).</summary>
        public const float SaintLean = 0.35f;

        /// <summary>
        /// A faith's lean toward the gods (PawnGodsMath.FaithLean) with its saints' gods: each saint's god a little more
        /// (<see cref="SaintLean"/>, at most the strongest's whole), never the damned's.
        /// </summary>
        public static Dictionary<string, float> WithSaints(Dictionary<string, float> faith, IEnumerable<string> saintGods)
        {
            var lean = faith == null ? new Dictionary<string, float>() : new Dictionary<string, float>(faith);
            if (saintGods == null) return lean;
            foreach (var g in saintGods.Where(x => !string.IsNullOrEmpty(x)).Distinct())
                lean[g] = Math.Min(1f, (lean.TryGetValue(g, out float v) ? v : 0f) + SaintLean);
            return lean;
        }

        /// <summary>The dread a faith's damned leave in its believers, once each: enough to be uneasy about the god it is tied to.</summary>
        public const float DamnedDread = 20f;

        // ================= The feast day =================

        /// <summary>The hours a saint's day is kept in: the evening.</summary>
        public const int FeastFrom = 17, FeastTo = 21;

        /// <summary>
        /// A colony saint's day has come: the day of the year they died, from the year after they were named, once a year,
        /// in the evening.
        /// </summary>
        public static bool FeastDue(int dayOfYear, int hour, int year, int feastDay, int namedYear, int lastKept) =>
            dayOfYear == feastDay && year > namedYear && lastKept < year && hour >= FeastFrom && hour <= FeastTo;

        // ================= Prayers at a saint's place =================

        /// <summary>The chance an hour that someone at a home goes to pray at one of its saints' places: more with more places, never much.</summary>
        public static float PrayChance(int places) => places <= 0 ? 0f : Math.Min(0.1f, 0.02f + 0.015f * (places - 1));

        /// <summary>Days between one person's prayers at a saint's place.</summary>
        public const float PrayGapDays = 2f;

        /// <summary>How long a prayer at a saint's place lasts, in ticks.</summary>
        public const int PrayTicks = 600, RaiseTicks = 450;

        // ================= Statues =================

        /// <summary>Statues by name from the player's mods (Alpha Memes', Vanilla Furniture Expanded - Art's and the rest), and Odyssey's own.</summary>
        public static readonly HashSet<string> Statues = new HashSet<string>
        {
            "AM_GrayStatueDecorative", "AM_Sphynx", "AM_Megalith", "AM_Obelisk", "AM_FuturisticObelisk", "AM_FuturisticStele",
            "VFE_StatueSmall", "VFE_StatueLarge", "VFE_StatueGrand", "VFEE_RoyalSculptureSmall", "VFEE_RoyalSculptureLarge", "VFEE_RoyalSculptureGrand",
            "VME_Statue", "VFEPD_Statue", "Statue"
        };

        /// <summary>Things called a sculpture or a statue that aren't one to raise to a saint: a workbench, an unfinished piece, a pod, a cube of an obsession, a void's or a gray menace's, a terror.</summary>
        private static readonly string[] NotStatues = { "Biosculpter", "TableSculpting", "Unfinished", "Cube", "Void", "Deadlife", "Teleporter", "Terror", "Fake", "Snow" };

        /// <summary>A building a saint's statue can be: one of <see cref="Statues"/>, or a work of art called a sculpture or a statue (not one of <see cref="NotStatues"/>).</summary>
        public static bool IsStatue(string defName, bool isArt)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            if (Statues.Contains(defName)) return true;
            if (NotStatues.Any(n => defName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) return false;
            bool named = defName.IndexOf("Sculpture", StringComparison.OrdinalIgnoreCase) >= 0 || defName.IndexOf("Statue", StringComparison.OrdinalIgnoreCase) >= 0;
            return named && (isArt || defName.IndexOf("Statue", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ================= Saved, and small =================

        /// <summary>The colony's own saints kept (the oldest let go past it).</summary>
        public const int MaxSaints = 40;

        /// <summary>Statues raised to a saint kept (gone when the statue is).</summary>
        public const int MaxPlaces = 60;

        /// <summary>The faith's saints seen at the colony, for the book's "kept no more" (the oldest gone let go past it).</summary>
        public const int MaxSeen = 40;

        /// <summary>Who already took their faith's damned to heart (person and god), the oldest let go past it.</summary>
        public const int MaxFeared = 200;
    }
}
