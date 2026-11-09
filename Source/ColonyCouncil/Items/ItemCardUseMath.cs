using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// One colonist's reckoning with a thing to use (a book to read, a part to install, a trainer, a xenogerm, a usable
    /// item): whether they can, how much they would gain (in the kind's own measure: experience an hour, levels, a part's
    /// efficiency gained), and why in a few words.
    /// </summary>
    public sealed class UseCandidate
    {
        public string Name = "";
        public bool Can;
        public float Gain;
        public string Why = "";
    }

    /// <summary>
    /// What the item card knows of step 3's kinds (Items/ItemCardUses.cs and Items/ItemCardThings.cs from the game, by hand
    /// in the tests): books, body parts, techprints, trainers, genepacks and xenogerms, usable items, shells, furniture in a
    /// crate, and anything else. One class with a part for each kind, as <see cref="ItemFacts"/> has; only the kind's own
    /// part is filled. Who at home it is for is in <see cref="Who"/>, the best first.
    /// </summary>
    public sealed class UseFacts
    {
        // ---------------- Books ----------------

        /// <summary>What reading it gives: "textbook" (skills), "schematic" (research), "tome" (the unknown's research), "novel" (recreation), "map" (a place revealed), else "book".</summary>
        public string BookKind = "";
        /// <summary>A thing in hand: its quality's word ("excellent", empty for a kind).</summary>
        public string Quality = "";
        /// <summary>The skills it teaches, the experience an hour of reading gives each (the most of them), and the level it raises a skill to at most.</summary>
        public List<string> Skills = new List<string>();
        public float SkillXpHour;
        public int MaxLevel;
        /// <summary>The research it gives: projects not done yet, how many of its projects are done, and research an hour.</summary>
        public List<string> Projects = new List<string>();
        public int ProjectsDone;
        public float ResearchHour;
        /// <summary>Recreation: the factor reading it gives (1 a plain read; 0 none known).</summary>
        public float JoyFactor;
        /// <summary>The chance of a mental break each hour of reading (a tome), 0 to 1.</summary>
        public float BreakHour;
        /// <summary>Reading it reveals a place on the world (a quest).</summary>
        public bool Quest;
        /// <summary>The length of a read when ordered, in hours.</summary>
        public float ReadHours;

        // ---------------- Body parts ----------------

        /// <summary>The part it goes in ("arm", "kidney"); it adds an implant rather than replacing the part.</summary>
        public string Part = "";
        public bool Implant;
        /// <summary>A natural part (an organ): it works as one's own.</summary>
        public bool Natural;
        /// <summary>Its efficiency against a natural part's 100% (-1 unknown, an implant without one).</summary>
        public float Efficiency = -1f;
        /// <summary>The game says it is better than natural.</summary>
        public bool BetterThanNatural;
        /// <summary>What it adds or changes besides the part ("Moving +10%"), and the abilities it gives.</summary>
        public List<string> Adds = new List<string>();
        public List<string> Abilities = new List<string>();
        /// <summary>A recipe installs it; the doctor's Medicine it asks; the research it waits on (empty when open).</summary>
        public bool Installable;
        public int DoctorLevel;
        public string SurgeryResearch = "";
        /// <summary>The colony's best doctor for it and their chance (-1 none).</summary>
        public string BestDoctor = "";
        public float BestChance = -1f;
        /// <summary>Who at home is missing the part it replaces (names, a few).</summary>
        public string MissingWho = "";
        public int Missing;
        /// <summary>Faiths at home that frown on it, and that approve of it (their names and the precept).</summary>
        public string FaithAgainst = "", FaithFor = "";

        // ---------------- Techprints ----------------

        public string Project = "";
        public int Applied, Needed;
        public bool ProjectDone;
        /// <summary>The research it waits on before it can be studied (labels), and whether a research bench fit for it is built.</summary>
        public string Waits = "";
        public bool BenchFit = true;
        /// <summary>How many things the project opens, and a few of them.</summary>
        public int Unlocks;
        public string UnlockWords = "";

        // ---------------- Trainers ----------------

        /// <summary>A neurotrainer: its skill and the experience it gives at once.</summary>
        public string Skill = "";
        public float Xp;
        /// <summary>A psytrainer: the psycast it teaches and its level.</summary>
        public string Psycast = "";
        public int PsycastLevel;

        // ---------------- Genes ----------------

        /// <summary>"genepack", "xenogerm" or "embryo".</summary>
        public string GeneKind = "";
        /// <summary>How many genes, the first few by name, and their complexity, metabolism and archite capsules in all; its xenotype's name.</summary>
        public int GeneCount, Complexity, Metabolism, Archites;
        public string GeneNames = "", Xenotype = "";
        /// <summary>The genes are known (a thing in hand); a kind's are chosen when it is made.</summary>
        public bool GenesKnown;
        /// <summary>The colony's gene assembler: built at all, and the most complexity it takes now.</summary>
        public bool Assembler;
        public int AssemblerMax;
        /// <summary>Hunger by the metabolism (the game's curve: 1 as a baseliner).</summary>
        public float Hunger = 1f;

        // ---------------- Usable items ----------------

        /// <summary>What a use does (the effects' ids, Rimfolk's words for each), to what, and how many uses the stack has.</summary>
        public List<string> Effects = new List<string>();
        public string Does = "", Targets = "";
        public int Uses = 1;
        /// <summary>A use uses it up.</summary>
        public bool UsedUp;
        /// <summary>The risks of using it, in words (brain damage, goodwill, a boss group, a manhunter pack).</summary>
        public List<string> Risks = new List<string>();

        // ---------------- Shells ----------------

        /// <summary>Its damage's name, the damage and its radius, armour piercing.</summary>
        public string Damage = "";
        public int DamageAmount;
        public float Radius, Penetration;
        /// <summary>It sets fires (an explosion that starts them), or puts them out.</summary>
        public bool Fire, Foam;
        /// <summary>The colony's buildings that fire it (how many and their names); some building in the game fires it.</summary>
        public int Launchers;
        public string LauncherWords = "", LauncherKind = "";
        public bool AnyLauncher;
        /// <summary>The shell itself explodes when it burns or is hit (the game's explosive comp).</summary>
        public bool ExplodesItself;

        // ---------------- Furniture in a crate ----------------

        /// <summary>Comfort to sit or lie on (0 none), beauty, rest, work speed (1 none), power (used above 0, made below 0), joy kind.</summary>
        public float Comfort, Beauty, Rest, WorkSpeed = 1f, Power;
        public string JoyKind = "";
        /// <summary>The colony has none of its joy kind yet.</summary>
        public bool JoyNew;
        public bool Bed, Table, WorkTable, Seat;
        /// <summary>A facility's offsets ("research speed +10%") and what it links to.</summary>
        public string Facility = "";
        /// <summary>The colony's room it would lift most and the stages (as a work of art's).</summary>
        public string BestRoom = "", BestStageNow = "", BestStageWith = "";
        public int BestIndexNow, BestIndexWith;
        /// <summary>How many of its kind stand at home (placed); the crates are <see cref="Held"/>.</summary>
        public int Placed;
        /// <summary>Beds: colonists without one of their own.</summary>
        public int Bedless;
        public string BedlessWho = "";

        // ---------------- Anything else ----------------

        /// <summary>An unfinished thing: what it will be, the work left, at which bench.</summary>
        public bool Unfinished;
        public string Product = "", Bench = "";
        public float WorkLeft;
        /// <summary>The colony holds so many of it (-1 unknown).</summary>
        public int Held = -1;

        // ---------------- Who at home ----------------

        /// <summary>Each colonist's reckoning, the best first (ItemCardMath.Ranked).</summary>
        public List<UseCandidate> Who = new List<UseCandidate>();
    }

    /// <summary>
    /// The item card's rules for step 3's kinds, pure: the game's experience curve and learning factors (a neurotrainer's
    /// levels), a book's experience an hour, a xenogerm's hunger, a surgery's chance, who at home gains most, the tags in the
    /// shared six categories and the line in plain words for each kind.
    /// </summary>
    public static partial class ItemCardMath
    {
        // ---------------- The kinds ----------------

        /// <summary>Step 3's kinds: everything after art.</summary>
        public static bool StepThree(ItemKind k) => k >= ItemKind.Book;

        /// <summary>The kind for step 3's flags, in their order (a trainer and a xenogerm before a usable item, a usable item before a body part, anything else last).</summary>
        private static ItemKind StepThreeKind(ItemSort s)
        {
            if (s.Book) return ItemKind.Book;
            if (s.Techprint) return ItemKind.Techprint;
            if (s.Trainer) return ItemKind.Trainer;
            if (s.Genes) return ItemKind.Genes;
            if (s.Usable) return ItemKind.Usable;
            if (s.BodyPart) return ItemKind.BodyPart;
            if (s.Shell) return ItemKind.Shell;
            if (s.Item) return ItemKind.Other;
            return ItemKind.None;
        }

        // ---------------- Small sums ----------------

        /// <summary>Ticks in an hour (the game's 2,500).</summary>
        public const float TicksPerHour = 2500f;

        /// <summary>The game's learning factors by passion (SkillRecord: none 0.35, minor 1, major 1.5).</summary>
        public static float PassionFactor(int passion) => passion <= 0 ? 0.35f : passion == 1 ? 1f : 1.5f;

        /// <summary>The highest skill level (SkillRecord.MaxLevel).</summary>
        public const int MaxSkillLevel = 20;

        /// <summary>The experience a level up asks from a level (the game's curve: 1,000 at 0, 10,000 at 9, 30,000 at 19), when the game's own can't be read.</summary>
        public static float XpToLevelUp(int level)
        {
            if (level <= 0) return 1000f;
            if (level <= 9) return 1000f + level * 1000f;
            if (level <= 19) return 10000f + (level - 9) * 2000f;
            return 30000f;
        }

        /// <summary>
        /// The level after so much experience from a level and the experience already toward the next, with its fraction
        /// toward the one after (held at 20). The curve is the game's (passed in where it can be read).
        /// </summary>
        public static float LevelAfter(int level, float xpSince, float xp, Func<int, float> toLevelUp = null)
        {
            var need = toLevelUp ?? XpToLevelUp;
            int l = Math.Max(0, level);
            float have = Math.Max(0f, xpSince) + Math.Max(0f, xp);
            while (l < MaxSkillLevel)
            {
                float n = Math.Max(1f, need(l));
                if (have < n) return l + have / n;
                have -= n;
                l++;
            }
            return MaxSkillLevel;
        }

        /// <summary>A book's experience an hour for a reader: none for a skill they can't learn or are past the book's level in, else the book's rate by their learning factor.</summary>
        public static float BookGain(float xpHour, float learnFactor, int level, int maxLevel, bool disabled) =>
            disabled || maxLevel > 0 && level >= maxLevel ? 0f : Math.Max(0f, xpHour) * Math.Max(0f, learnFactor);

        /// <summary>Hunger by total metabolism (the game's GeneTuning curve: 2.25 at -5, 1 at 0, 0.5 at 5), when the game's own can't be read.</summary>
        public static float HungerFor(int metabolism)
        {
            float m = Math.Max(-5, Math.Min(5, metabolism));
            return m <= 0f ? 1f - m * 0.25f : 1f - m * 0.1f;
        }

        /// <summary>The gene assembler's least complexity, before gene processors (GeneTuning.BaseMaxComplexity).</summary>
        public const int BaseAssemblerComplexity = 6;

        /// <summary>A surgery's chance before the bed, the room and the medicine: the surgeon's success chance by the operation's own factor, held between none and certain.</summary>
        public static float SurgeryChance(float surgeon, float recipeFactor) => Math.Max(0f, Math.Min(1f, surgeon * (recipeFactor > 0f ? recipeFactor : 1f)));

        /// <summary>Techprints still needed for a project.</summary>
        public static int PrintsLeft(int applied, int needed) => Math.Max(0, needed - applied);

        /// <summary>Who at home, the best first: those who can before those who can't, the most gain first, then by name.</summary>
        public static List<UseCandidate> Ranked(IEnumerable<UseCandidate> who) =>
            (who ?? Enumerable.Empty<UseCandidate>()).Where(c => c != null).OrderByDescending(c => c.Can).ThenByDescending(c => c.Gain).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();

        /// <summary>The one who gains most (able, and gaining something), or null.</summary>
        public static UseCandidate Best(UseFacts u) => u?.Who?.Where(c => c != null && c.Can && c.Gain > 0f).OrderByDescending(c => c.Gain).ThenBy(c => c.Name, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>How many at home could use it.</summary>
        public static int Able(UseFacts u) => u?.Who?.Count(c => c != null && c.Can) ?? 0;

        /// <summary>An efficiency in words against a natural part: "125% of a natural one".</summary>
        public static string EfficiencyWords(float e) => Math.Round(e * 100f).ToString("0", Inv) + "% of a natural one";

        /// <summary>A count with its noun ("1 gene", "3 genes").</summary>
        public static string Count(int n, string one, string many) => n.ToString("#,0", Inv) + " " + (n == 1 ? one : many);

        /// <summary>A large number in words with thousands marked ("50,000").</summary>
        public static string Whole(float v) => Math.Round(v).ToString("#,0", Inv);

        // ---------------- Usable items: their effects ----------------

        /// <summary>
        /// The base game's use and target effects by their comp class (CompUseEffect_, CompTargetEffect_, CompTargetable_),
        /// each with Rimfolk's words under CC_ItemCard_Does_ and the class. Effects that harm the user or the colony, or put
        /// others against it, are risks.
        /// </summary>
        public static readonly string[] KnownEffects =
        {
            "CompUseEffect_InstallImplant", "CompUseEffect_InstallImplantMechlink", "CompUseEffect_AddHediff", "CompUseEffect_AddHediffAndEnableDisappearsComp",
            "CompUseEffect_FixWorstHealthCondition", "CompUseEffect_FinishRandomResearchProject", "CompUseEffect_GiveQuest", "CompUseEffect_CallBossgroup",
            "CompUseEffect_OffsetDeathrestCapacity", "CompUseEffect_StartWick", "CompUseEffect_SpawnFleshbeastFromTargetPawns", "CompUseEffect_LearnSkill",
            "CompUseEffect_GainAbility", "CompTargetEffect_Resurrect", "CompTargetEffect_PsychicShock", "CompTargetEffect_Berserk", "CompTargetEffect_Manhunter",
            "CompTargetEffect_MoodBoost", "CompTargetEffect_BrainDamageChance", "CompTargetEffect_GoodwillImpact", "CompTargetEffect_MutateIntoFleshbeast",
            "CompTargetEffect_InstallImplantInOtherPawn"
        };

        /// <summary>The base game's targets by their comp class (CC_ItemCard_Targets_ and the class).</summary>
        public static readonly string[] KnownTargets =
        {
            "CompTargetable_SinglePawn", "CompTargetable_SingleAnimal", "CompTargetable_SingleCorpse", "CompTargetable_AllPawnsOnTheMap", "CompTargetable_AllAnimalsOnTheMap"
        };

        /// <summary>Effects that are a risk to use: they call a fight, set off a fuse, turn animals or people on everyone, spoil a mind or a people's goodwill, or make a monster.</summary>
        public static readonly string[] RiskyEffects =
        {
            "CompUseEffect_CallBossgroup", "CompUseEffect_StartWick", "CompUseEffect_SpawnFleshbeastFromTargetPawns", "CompTargetEffect_Berserk", "CompTargetEffect_Manhunter",
            "CompTargetEffect_BrainDamageChance", "CompTargetEffect_GoodwillImpact", "CompTargetEffect_MutateIntoFleshbeast"
        };

        /// <summary>Effects that heal or bring back.</summary>
        public static bool Heals(UseFacts u) => u != null && u.Effects.Contains("CompUseEffect_FixWorstHealthCondition");

        public static bool Resurrects(UseFacts u) => u != null && u.Effects.Contains("CompTargetEffect_Resurrect");

        /// <summary>A mod's effect class in words: its name's words after Comp, UseEffect and TargetEffect ("CompUseEffect_YouthSerum": "youth serum").</summary>
        public static string EffectWords(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return "";
            var words = Words(cls).Where(w => w != "Use" && w != "Effect" && w != "Target" && w != "Targetable" && !w.StartsWith("Comp", StringComparison.Ordinal))
                .Select(w => w.ToLowerInvariant()).ToList();
            return string.Join(" ", words);
        }

        // ---------------- Step 3's tags ----------------

        /// <summary>The tags step 3 adds to the one vocabulary. The tags it shares (needed now, spare to sell, trade good, recreation, beautiful, lifts the room, a room, explodes in a fire, research) keep their place.</summary>
        private static IEnumerable<TagDef> StepThreeTags() => new[]
        {
            D("mental-break", "Mental break risk", R, 94, "each hour of reading it may bring on a mental break (the game's chance)"),
            D("risky-use", "Risky to use", R, 92, "using it may call a fight, spoil a mind, turn animals or people on everyone, or cost a people's goodwill"),
            D("faith-against", "Against a faith", R, 91, "a faith at home frowns on it: its believers think worse of it, or won't have it"),
            D("useful-here", "Useful here", O, 89, "someone at home would gain from it: the one who gains most is named"),
            D("better-than-natural", "Better than natural", S, 89, "a body part that works better than a natural one"),
            D("heals", "Heals", S, 88, "using it cures the user's worst health condition"),
            D("resurrects", "Brings back the dead", S, 88, "using it on a corpse brings the person back"),
            D("teaches", "Teaches", S, 87, "reading or using it gives experience in a skill"),
            D("gives-research", "Gives research", S, 86, "reading it gives research toward a project"),
            D("psycast", "Teaches a psycast", S, 86, "using it teaches a psycaster a psycast"),
            D("worse-than-natural", "Worse than natural", W, 85, "a body part that works worse than a natural one"),
            D("unlocks", "Opens new things", S, 85, "its research opens new buildings, recipes or plants"),
            D("adds-ability", "Gives an ability", S, 84, "installed, it gives an ability or changes what its bearer can do"),
            D("research-done", "Already known", W, 83, "the research it gives or is for is done, or every techprint the project needs is applied"),
            D("useless-here", "No use here", W, 83, "nobody at home can gain from it now"),
            D("blast", "Blast", S, 83, "fired, it bursts over an area"),
            D("power-make", "Makes power", S, 83, "a building that makes power"),
            D("work-speed", "Faster work", S, 81, "it speeds the work at a bench or the research it is linked to"),
            D("comfortable", "Comfortable", S, 80, "a seat or bed with comfort 75% or more"),
            D("eats-less", "Eats less", S, 79, "its genes' metabolism is above nothing: the bearer is less hungry"),
            D("hungry", "Hungry", W, 78, "its genes' metabolism is below nothing: the bearer is hungrier"),
            D("one-use", "One use", W, 77, "a use uses it up"),
            D("for-launcher", "For the colony's guns", O, 74, "the colony's mortars or other guns that take shells fire it"),
            D("power-use", "Needs power", W, 74, "a building that runs on power"),
            D("natural", "Natural part", X, 67, "a natural organ or limb: it works as one's own"),
            D("archite", "Archite", X, 67, "its genes need archite capsules to assemble"),
            D("reveals-place", "Reveals a place", X, 65, "reading it reveals a place on the world to travel to"),
            D("faith-for", "Approved by a faith", X, 63, "a faith at home approves of it: its believers are glad of it"),
            D("surgery", "Surgery", Nd, 61, "a doctor installs it in an operation"),
            D("prerequisites", "Earlier research", Nd, 60, "its project needs other research done first"),
            D("needs-psylink", "A psylink", Nd, 60, "only a psycaster with a high enough psylink can learn it"),
            D("gene-assembler", "Gene assembler", Nd, 58, "its genes are made into a xenogerm at a gene assembler, within its complexity"),
            D("no-launcher", "A gun to fire it", Nd, 57, "nothing at home fires it"),
            D("unfinished", "Work left", Nd, 55, "an unfinished thing: work at its bench finishes it"),
        };

        /// <summary>Step 3's tags for a thing, each from its own numbers or Rimfolk's rules, with why.</summary>
        private static void UseTags(ItemFacts f, List<ThingTag> tags)
        {
            var u = f.Use ?? new UseFacts();
            var best = Best(u);
            int able = Able(u);
            bool asked = f.HasColony && u.Who.Count > 0;
            switch (f.Kind)
            {
                case ItemKind.Book:
                    if (u.BreakHour > 0f) tags.Add(T("mental-break", Pct(u.BreakHour) + " chance of a mental break each hour of reading"));
                    if (u.Skills.Count > 0)
                        tags.Add(T("teaches", "about " + Whole(u.SkillXpHour) + " experience an hour in " + AndList(u.Skills) + (u.MaxLevel > 0 ? ", up to level " + u.MaxLevel : "")));
                    if (u.Projects.Count > 0) tags.Add(T("gives-research", "research toward " + AndList(u.Projects) + (u.ResearchHour > 0f ? ", about " + Whole(u.ResearchHour) + " an hour" : "")));
                    else if (u.ProjectsDone > 0) tags.Add(T("research-done", "the research it gives is done already: reading it gives nothing more"));
                    if (u.JoyFactor > 0f && u.Skills.Count == 0 && u.Projects.Count == 0 && u.ProjectsDone == 0)
                        tags.Add(T("recreation", "reading it is recreation" + (Math.Abs(u.JoyFactor - 1f) > 0.01f ? ", x" + N(u.JoyFactor) + " a plain read's" : "")));
                    if (u.Quest) tags.Add(T("reveals-place", "reading it reveals a place on the world to travel to"));
                    break;
                case ItemKind.BodyPart:
                    if (u.Natural) tags.Add(T("natural", "a natural " + (u.Part.Length > 0 ? u.Part : "part") + ": it works as one's own"));
                    else if (u.BetterThanNatural || u.Efficiency > 1.0001f)
                        tags.Add(T("better-than-natural", u.Efficiency > 0f ? EfficiencyWords(u.Efficiency) : "better than a natural " + (u.Part.Length > 0 ? u.Part : "part")));
                    else if (u.Efficiency >= 0f && u.Efficiency < 0.9999f) tags.Add(T("worse-than-natural", EfficiencyWords(u.Efficiency) + ": better than none, worse than a natural one"));
                    if (u.Abilities.Count > 0) tags.Add(T("adds-ability", "it gives " + AndList(u.Abilities)));
                    else if (u.Adds.Count > 0 && u.Implant) tags.Add(T("adds-ability", "it changes " + AndList(u.Adds.Take(3).ToList())));
                    if (u.Installable)
                        tags.Add(T("surgery", "a doctor installs it" + (u.DoctorLevel > 0 ? ", Medicine " + u.DoctorLevel + " or more" : "") + (u.SurgeryResearch.Length > 0 ? ", once " + u.SurgeryResearch + " is done" : "")
                                              + (f.HasColony && u.BestDoctor.Length > 0 && u.BestChance >= 0f ? "; " + u.BestDoctor + " succeeds about " + Pct(u.BestChance) + " of the time" : "")));
                    if (f.HasColony && u.Missing > 0) tags.Add(T("needed", u.MissingWho.Length > 0 ? u.MissingWho : u.Missing + " at home are missing the part it replaces"));
                    if (u.FaithAgainst.Length > 0) tags.Add(T("faith-against", u.FaithAgainst));
                    if (u.FaithFor.Length > 0) tags.Add(T("faith-for", u.FaithFor));
                    break;
                case ItemKind.Techprint:
                {
                    int left = PrintsLeft(u.Applied, u.Needed);
                    if (u.ProjectDone || u.Needed > 0 && left == 0)
                    {
                        tags.Add(T("research-done", u.ProjectDone ? u.Project + " is researched already" : "all " + u.Needed + " techprints " + u.Project + " needs are applied"));
                        if (f.HasColony) tags.Add(T("spare", "the project needs no more: worth " + Money(f.Value) + " to a trader"));
                    }
                    else if (f.HasColony && left > 0)
                        tags.Add(T("needed", u.Applied + " of " + u.Needed + " applied: " + u.Project + " needs " + left + " more before it can be studied"));
                    if (u.Unlocks > 0 && !u.ProjectDone) tags.Add(T("unlocks", u.Project + " opens " + (u.UnlockWords.Length > 0 ? u.UnlockWords : Count(u.Unlocks, "thing", "things"))));
                    if (!u.ProjectDone && u.Waits.Length > 0) tags.Add(T("prerequisites", u.Project + " needs " + u.Waits + " first"));
                    break;
                }
                case ItemKind.Trainer:
                    tags.Add(T("one-use", "used once, then gone"));
                    if (u.Skill.Length > 0) tags.Add(T("teaches", Whole(u.Xp) + " experience in " + u.Skill + " at once, by the user's passion"));
                    if (u.Psycast.Length > 0)
                    {
                        tags.Add(T("psycast", u.Psycast + ", a level " + u.PsycastLevel + " psycast"));
                        if (asked && able == 0) tags.Add(T("needs-psylink", "a psylink of level " + u.PsycastLevel + " or more; nobody at home has one"));
                        else if (!asked) tags.Add(T("needs-psylink", "a psylink of level " + u.PsycastLevel + " or more"));
                    }
                    break;
                case ItemKind.Genes:
                    if (u.GenesKnown)
                    {
                        if (u.Archites > 0) tags.Add(T("archite", Count(u.Archites, "archite capsule", "archite capsules") + " to assemble"));
                        if (u.Metabolism > 0) tags.Add(T("eats-less", "metabolism " + Signed(u.Metabolism) + ": hunger x" + N(u.Hunger)));
                        else if (u.Metabolism < 0) tags.Add(T("hungry", "metabolism " + Signed(u.Metabolism) + ": hunger x" + N(u.Hunger)));
                        if (u.GeneKind == "genepack" && f.HasColony)
                        {
                            if (!u.Assembler) tags.Add(T("gene-assembler", "the colony has no gene assembler to make a xenogerm of it"));
                            else if (u.Complexity > u.AssemblerMax) tags.Add(T("gene-assembler", "complexity " + u.Complexity + ", more than the colony's assembler takes (" + u.AssemblerMax + ")"));
                        }
                    }
                    if (u.FaithAgainst.Length > 0) tags.Add(T("faith-against", u.FaithAgainst));
                    break;
                case ItemKind.Usable:
                    if (Heals(u)) tags.Add(T("heals", "using it cures the user's worst health condition"));
                    if (Resurrects(u)) tags.Add(T("resurrects", "used on a corpse, it brings the person back"));
                    if (u.Risks.Count > 0) tags.Add(T("risky-use", AndList(u.Risks)));
                    if (u.UsedUp) tags.Add(T("one-use", u.Uses > 1 ? "a use uses one up; " + u.Uses + " in the stack" : "used once, then gone"));
                    break;
                case ItemKind.Shell:
                    if (u.ExplodesItself) tags.Add(T("explosive", "a stack explodes when fire or a blow reaches it: store it away from fire and fights"));
                    if (u.Radius > 0f) tags.Add(T("blast", BlastWords(u)));
                    if (f.HasColony && u.Launchers > 0) tags.Add(T("for-launcher", "the colony's " + u.LauncherWords + " fire it"));
                    else if (f.HasColony && u.AnyLauncher) tags.Add(T("no-launcher", "nothing at home fires it" + (u.LauncherKind.Length > 0 ? "; " + A(u.LauncherKind) + " would" : "")));
                    break;
                case ItemKind.Furniture:
                    if (u.Comfort >= 0.75f) tags.Add(T("comfortable", "comfort " + Pct(u.Comfort)));
                    if (u.Power < -0.5f) tags.Add(T("power-make", "makes " + Whole(-u.Power) + " W"));
                    else if (u.Power > 0.5f) tags.Add(T("power-use", "uses " + Whole(u.Power) + " W"));
                    if (u.WorkSpeed > 1.0001f || u.Facility.Length > 0) tags.Add(T("work-speed", u.Facility.Length > 0 ? u.Facility : "work at it goes x" + N(u.WorkSpeed)));
                    if (u.JoyKind.Length > 0) tags.Add(T("recreation", u.JoyKind + " recreation" + (f.HasColony && u.JoyNew ? ", a kind the colony has none of yet" : "")));
                    if (f.HasColony && u.BestRoom.Length > 0 && u.BestIndexWith > u.BestIndexNow)
                        tags.Add(T("lifts-room", "placed in the " + u.BestRoom + " it would make it " + u.BestStageWith + ", from " + u.BestStageNow));
                    else if (u.Beauty >= 10f) tags.Add(T("beautiful", "beauty " + N((float)Math.Round(u.Beauty))));
                    if (f.HasColony && u.Bed && u.Bedless > 0) tags.Add(T("needed", u.Bedless + " at home sleep without a bed of their own" + (u.BedlessWho.Length > 0 ? ": " + u.BedlessWho : "")));
                    if (f.HasColony) tags.Add(T("a-room", "in a crate it does nothing: placed, it does its work" + (u.BestRoom.Length > 0 && u.BestIndexWith > u.BestIndexNow ? "; the " + u.BestRoom + " would gain most" : "")));
                    break;
                case ItemKind.Other:
                    if (u.Unfinished) tags.Add(T("unfinished", (u.Product.Length > 0 ? u.Product + ": " : "") + Whole(u.WorkLeft) + " work left" + (u.Bench.Length > 0 ? " at " + A(u.Bench) : "")));
                    break;
            }
            if (asked && best != null && f.Kind != ItemKind.Furniture && f.Kind != ItemKind.Usable) tags.Add(T("useful-here", best.Name + ": " + best.Why));
            else if (asked && able == 0 && f.Kind != ItemKind.Usable && f.Kind != ItemKind.Furniture)
            {
                var first = u.Who[0];
                tags.Add(T("useless-here", "nobody at home can gain from it now" + (first.Why.Length > 0 ? "; " + first.Name + ": " + first.Why : "")));
            }
            float perKg = ValuePerKg(f);
            if (f.Tradeable && perKg >= TradeValuePerKg && f.Kind != ItemKind.Techprint)
                tags.Add(T("trade-good", "worth " + Math.Round(perKg).ToString("0", Inv) + " silver a kilogram: much worth in a pack"));
        }

        /// <summary>A shell's burst in words: "bomb, 50 in a radius of 2.9".</summary>
        public static string BlastWords(UseFacts u) =>
            (u.Damage.Length > 0 ? u.Damage : "a burst") + (u.DamageAmount > 0 ? ", " + u.DamageAmount : "") + " in a radius of " + N((float)Math.Round(u.Radius, 1));

        // ---------------- Step 3's lines in plain words ----------------

        /// <summary>What it is good for here, in one line, by its kind.</summary>
        private static string UsePlain(ItemFacts f)
        {
            var u = f.Use ?? new UseFacts();
            var best = Best(u);
            bool asked = f.HasColony && u.Who.Count > 0;
            string label = Cap(f.Label) + (u.Quality.Length > 0 && f.Kind == ItemKind.Book ? ", " + u.Quality : "");
            var lead = new List<string>();
            string tail = "";
            switch (f.Kind)
            {
                case ItemKind.Book:
                {
                    if (u.Skills.Count > 0) lead.Add("teaches " + AndList(u.Skills) + (u.MaxLevel > 0 ? " up to level " + u.MaxLevel : "") + (u.SkillXpHour > 0f ? ", about " + Whole(u.SkillXpHour) + " experience an hour" : ""));
                    else if (u.BookKind == "textbook") lead.Add("teaches the skills it was written on");
                    if (u.Projects.Count > 0) lead.Add("research toward " + AndList(u.Projects));
                    else if (u.ProjectsDone > 0) lead.Add("its research is known already");
                    else if (u.BookKind == "schematic" || u.BookKind == "tome") lead.Add("research toward the project it was written on");
                    if (u.Quest) lead.Add("reading it reveals a place on the world");
                    if (lead.Count == 0 && u.JoyFactor > 0f) lead.Add("recreation to read" + (Math.Abs(u.JoyFactor - 1f) > 0.01f ? " (x" + N(u.JoyFactor) + ")" : ""));
                    if (lead.Count == 0) lead.Add("a book");
                    if (u.BreakHour > 0f) lead.Add("but a mental break risk");
                    if (best != null) tail = best.Name + (u.Skills.Count > 0 ? " would gain most" : u.BookKind == "novel" ? " would enjoy it most" : " would read it best");
                    else if (asked && Able(u) == 0) tail = "nobody at home would gain from it";
                    break;
                }
                case ItemKind.BodyPart:
                {
                    string part = u.Part.Length > 0 ? u.Part : "part";
                    if (u.Natural) lead.Add("a natural " + part + ", as good as one's own");
                    else if (u.Implant) lead.Add("an implant in the " + part + (u.Adds.Count > 0 ? ": " + AndList(u.Adds.Take(2).ToList()) : u.Abilities.Count > 0 ? ": " + AndList(u.Abilities) : ""));
                    else if (u.Efficiency >= 0f) lead.Add(u.Efficiency > 1.0001f ? "better than a natural one (" + Pct(u.Efficiency) + ")" : u.Efficiency < 0.9999f ? "works at " + Pct(u.Efficiency) + " of a natural one" : "as good as a natural one");
                    else lead.Add("fitted at the " + part);
                    if (!u.Implant && u.Abilities.Count > 0) lead.Add("gives " + AndList(u.Abilities));
                    if (f.HasColony && u.Missing > 0) tail = u.Missing == 1 ? "someone at home is missing one" : u.Missing + " at home are missing one";
                    else if (best != null) tail = best.Name + " would gain most";
                    else if (asked) tail = "nobody at home needs one";
                    break;
                }
                case ItemKind.Techprint:
                {
                    int left = PrintsLeft(u.Applied, u.Needed);
                    // Its label names the project already in the game's words ("charged shot techprint").
                    bool named = u.Project.Length > 0 && f.Label.IndexOf(u.Project, StringComparison.OrdinalIgnoreCase) >= 0;
                    string applied = u.Needed > 0 ? u.Applied + " of " + u.Needed + " applied" : "";
                    lead.Add(named ? (applied.Length > 0 ? applied : "its project's") : "for " + (u.Project.Length > 0 ? u.Project : "a project") + (applied.Length > 0 ? ", " + applied : ""));
                    if (u.ProjectDone) lead.Add("the research is done");
                    else if (u.Needed > 0 && left == 0) lead.Add("all applied, this one spare");
                    else if (left > 0) lead.Add(left + " more needed");
                    if (u.Unlocks > 0 && !u.ProjectDone) lead.Add("it opens " + Count(u.Unlocks, "thing", "things"));
                    break;
                }
                case ItemKind.Trainer:
                    if (u.Skill.Length > 0) lead.Add(Whole(u.Xp) + " " + u.Skill + " experience at once");
                    if (u.Psycast.Length > 0) lead.Add("teaches " + u.Psycast + ", a level " + u.PsycastLevel + " psycast");
                    lead.Add("one use");
                    if (best != null) tail = best.Name + (u.Skill.Length > 0 ? " gains most" : " can learn it");
                    else if (asked) tail = u.Psycast.Length > 0 ? "nobody at home has the psylink for it" : "nobody at home can use it";
                    break;
                case ItemKind.Genes:
                    if (!u.GenesKnown)
                    {
                        lead.Add(u.GeneKind == "embryo" ? "an embryo, its genes its parents'" : "its genes are chosen when it is made");
                        break;
                    }
                    lead.Add((u.GeneKind == "embryo" && u.Xenotype.Length > 0 ? "an embryo of " + u.Xenotype + ", " : u.GeneKind == "xenogerm" && u.Xenotype.Length > 0 ? u.Xenotype + ", " : "")
                             + Count(u.GeneCount, "gene", "genes") + (u.GeneNames.Length > 0 ? " (" + u.GeneNames + ")" : ""));
                    lead.Add("complexity " + u.Complexity + ", metabolism " + Signed(u.Metabolism) + (u.Archites > 0 ? ", " + Count(u.Archites, "archite capsule", "archite capsules") : ""));
                    if (u.GeneKind == "xenogerm" && best != null) tail = "suits " + best.Name + " best";
                    else if (u.GeneKind == "xenogerm" && asked) tail = "nobody at home is a good fit";
                    else if (u.GeneKind == "genepack" && f.HasColony && !u.Assembler) tail = "the colony has no gene assembler";
                    break;
                case ItemKind.Usable:
                    lead.Add((u.Does.Length > 0 ? u.Does : "something to use") + (u.Targets.Length > 0 ? " (" + u.Targets + ")" : ""));
                    if (u.UsedUp) lead.Add("one use");
                    if (u.Risks.Count > 0) lead.Add("but " + u.Risks[0]);
                    break;
                case ItemKind.Shell:
                    lead.Add(u.Radius > 0f ? BlastWords(u) : u.Damage.Length > 0 ? u.Damage + (u.DamageAmount > 0 ? " " + u.DamageAmount : "") : "a shell");
                    if (f.HasColony && u.Launchers > 0) tail = "the colony's " + u.LauncherWords + " fire it";
                    else if (f.HasColony && u.AnyLauncher) tail = "nothing at home fires it";
                    break;
                case ItemKind.Furniture:
                    if (u.Bed) lead.Add("a bed" + (u.Rest > 0f ? ", rest " + Pct(u.Rest) : ""));
                    if (u.Comfort > 0f) lead.Add("comfort " + Pct(u.Comfort));
                    if (u.JoyKind.Length > 0) lead.Add(u.JoyKind + " recreation");
                    if (u.WorkSpeed > 1.0001f) lead.Add("work x" + N(u.WorkSpeed));
                    if (u.Facility.Length > 0) lead.Add(u.Facility);
                    if (u.Power < -0.5f) lead.Add("makes " + Whole(-u.Power) + " W");
                    else if (u.Power > 0.5f) lead.Add("uses " + Whole(u.Power) + " W");
                    if (Math.Abs(u.Beauty) >= 0.5f) lead.Add("beauty " + N((float)Math.Round(u.Beauty)));
                    if (lead.Count == 0) lead.Add("furniture to place");
                    if (f.HasColony && u.Bed && u.Bedless > 0) tail = u.Bedless == 1 ? "someone at home sleeps without a bed" : u.Bedless + " at home sleep without a bed";
                    else if (f.HasColony && u.BestRoom.Length > 0 && u.BestIndexWith > u.BestIndexNow) tail = "placed in the " + u.BestRoom + " it would make it " + u.BestStageWith;
                    else if (f.HasColony && u.JoyNew) tail = "a recreation the colony has none of";
                    break;
                default:
                    if (u.Unfinished) lead.Add((u.Product.Length > 0 ? "an unfinished " + u.Product : "unfinished") + ", " + Whole(u.WorkLeft) + " work left");
                    else lead.Add("worth " + Money(f.Value) + (f.Mass > 0f ? ", " + Kg(f.Mass) : ""));
                    if (f.HasColony && u.Held >= 0) tail = (u.Held == 0 ? "none" : u.Held.ToString("#,0", Inv)) + " in store";
                    break;
            }
            if (lead.Count > 3) lead = lead.Take(3).ToList();
            string head = label + ": " + string.Join(", ", lead);
            return head + (tail.Length > 0 ? "; " + tail : "") + ".";
        }
    }
}
