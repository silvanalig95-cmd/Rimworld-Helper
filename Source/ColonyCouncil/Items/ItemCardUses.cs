using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The item card's reading of step 3's kinds (Rimfolk's look, stage 3, point 5, step 3: every item the first two steps
    /// left), read from the defs and their comps so the player's mods' items come in on their own. Here the sorting and the
    /// dispatch, and books, techprints, trainers and usable items; body parts and genes in Items/ItemCardBodies.cs, shells,
    /// furniture in a crate and anything else in Items/ItemCardThings.cs. Once a kind: what it is (a book's outcomes, a
    /// part's install recipes and hediff, a trainer's skill or psycast, a usable item's effects and targets, a shell's burst
    /// and the guns that fire it, a crate's piece). Once a card: the thing's own (a book's skills and research at its quality,
    /// a gene holder's genes). Every ten seconds while open: who at home it is for, the colony's research, doctors, guns and
    /// rooms. Rules and words in Items/ItemCardUseMath.cs.
    /// </summary>
    public static partial class ItemCardLive
    {
        /// <summary>Step 3's numbers of a kind, read once.</summary>
        private sealed class UsePart
        {
            // Books.
            public string bookKind = "";
            public bool skillBook, researchBook, anomaly, joy, breakRisk, quest;
            public float readHours = 2f;
            public readonly List<string> otherOutcomes = new List<string>();
            // Body parts (Items/ItemCardBodies.cs).
            public readonly List<RecipeDef> installs = new List<RecipeDef>();
            public HediffDef hediff;
            public string part = "";
            public bool implant, natural, better;
            public float efficiency = -1f;
            public readonly List<string> adds = new List<string>(), abilities = new List<string>();
            public int doctorLevel;
            // Techprints.
            public ResearchProjectDef project;
            // Trainers.
            public SkillDef skill;
            public float xp;
            public AbilityDef ability;
            // Genes.
            public string geneKind = "";
            // Usable items.
            public readonly List<string> effects = new List<string>(), risks = new List<string>(), doesWords = new List<string>();
            public string targets = "";
            public bool usedUp;
            // Shells (Items/ItemCardThings.cs).
            public string damage = "", launcherKind = "";
            public int amount;
            public float radius, penetration;
            public bool fire, foam, explodesItself;
            public readonly List<ThingDef> launchers = new List<ThingDef>();
            // Furniture in a crate.
            public float comfort, rest, workSpeed = 1f, power;
            public JoyKindDef joyKind;
            public bool bed, table, workTable, seat;
            public string facility = "";
            public int recipes;
            // Anything else.
            public string description = "";
            public readonly List<(string label, string value)> stats = new List<(string, string)>();
        }

        // ---------------------------------------------------------------- the sorting

        private static bool Within(ThingDef d, ThingCategoryDef c) => c != null && d.thingCategories != null && d.IsWithinCategory(c);

        /// <summary>Step 3's flags of an item (ItemCardMath.KindOf orders them): read from its class, its comps and the game's filing, so a mod's book, trainer or part comes in on its own.</summary>
        private static void StepThreeSort(ThingDef d, ItemSort s)
        {
            s.Item = true;
            var cls = d.thingClass;
            s.Book = cls != null && typeof(Book).IsAssignableFrom(cls) || d.GetCompProperties<CompProperties_Readable>() != null;
            s.Techprint = d.GetCompProperties<CompProperties_Techprint>() != null || Within(d, ThingCategoryDefOf.Techprints);
            s.Trainer = d.GetCompProperties<CompProperties_UseEffect_LearnSkill>() != null || d.GetCompProperties<CompProperties_UseEffect_GainAbility>() != null
                        || Within(d, ThingCategoryDefOf.Neurotrainers);
            s.Genes = cls != null && typeof(GeneSetHolderBase).IsAssignableFrom(cls);
            s.Usable = d.GetCompProperties<CompProperties_Usable>() != null;
            s.BodyPart = d.isTechHediff || Within(d, ThingCategoryDefOf.BodyParts);
            s.Shell = d.IsShell;
        }

        // ---------------------------------------------------------------- the dispatch

        private static void ReadUse(DefPart p)
        {
            var up = p.use = new UsePart();
            switch (p.kind)
            {
                case ItemKind.Book:
                    ReadBook(p.def, up);
                    break;
                case ItemKind.BodyPart:
                    ReadBodyPart(p.def, up);
                    break;
                case ItemKind.Techprint:
                    up.project = p.def.GetCompProperties<CompProperties_Techprint>()?.project;
                    break;
                case ItemKind.Trainer:
                    up.skill = p.def.GetCompProperties<CompProperties_UseEffect_LearnSkill>()?.skill;
                    up.xp = p.def.GetCompProperties<CompProperties_UseEffect_LearnSkill>()?.xpGainAmount ?? 0f;
                    up.ability = p.def.GetCompProperties<CompProperties_UseEffect_GainAbility>()?.ability;
                    break;
                case ItemKind.Genes:
                    up.geneKind = typeof(Xenogerm).IsAssignableFrom(p.def.thingClass) ? "xenogerm" : typeof(HumanEmbryo).IsAssignableFrom(p.def.thingClass) ? "embryo" : "genepack";
                    break;
                case ItemKind.Usable:
                    ReadUsable(p.def, up);
                    break;
                case ItemKind.Shell:
                    ReadShell(p.def, up);
                    break;
                case ItemKind.Furniture:
                    ReadFurniture(p.def, up);
                    break;
                default:
                    ReadOther(p.def, up);
                    break;
            }
        }

        /// <summary>The thing's own facts: the kind's numbers into the facts, the thing in hand's once a card (a book's skills, a gene holder's genes).</summary>
        private static void LiveUse(ItemCardModel m, DefPart p)
        {
            var u = m.facts.Use ??= new UseFacts();
            var up = p.use ?? new UsePart();
            if (!m.useRead)
            {
                m.useRead = true;
                m.use.Clear();
                m.genes.Clear();
                m.useNote = null;
                try
                {
                    switch (m.kind)
                    {
                        case ItemKind.Book:
                            LiveBook(m, up, u);
                            break;
                        case ItemKind.BodyPart:
                            LiveBodyPart(m, up, u);
                            break;
                        case ItemKind.Trainer:
                            u.Skill = up.skill?.label ?? "";
                            u.Xp = up.xp;
                            u.Psycast = up.ability?.label ?? "";
                            u.PsycastLevel = up.ability?.level ?? 0;
                            break;
                        case ItemKind.Genes:
                            LiveGenes(m, up, u);
                            break;
                        case ItemKind.Usable:
                            LiveUsable(m, up, u);
                            break;
                        case ItemKind.Shell:
                            LiveShell(up, u);
                            break;
                        case ItemKind.Furniture:
                            LiveFurniture(m, up, u);
                            break;
                        case ItemKind.Other:
                            LiveOther(m, up, u);
                            break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the item card reading " + m.def.defName, e);
                }
            }
            if (m.kind == ItemKind.Usable) u.Uses = Math.Max(1, m.thing?.stackCount ?? 1);
        }

        /// <summary>What kind of thing it is, in a few words: "Book, a textbook", "Body part, bionic", "Techprint".</summary>
        private static string UseKindWords(ItemCardModel m, DefPart p)
        {
            var up = p.use ?? new UsePart();
            var u = m.facts.Use ?? new UseFacts();
            switch (p.kind)
            {
                case ItemKind.Book:
                    return W("CC_ItemCard_Kind_Book_" + (up.bookKind.Length > 0 ? up.bookKind : "book"));
                case ItemKind.BodyPart:
                    if (u.Natural) return W("CC_ItemCard_Kind_PartNatural");
                    if (m.def.techLevel == TechLevel.Undefined) return W(u.Implant ? "CC_ItemCard_Kind_PartImplant" : "CC_ItemCard_Kind_PartPlain");
                    return W(u.Implant ? "CC_ItemCard_Kind_PartImplantTech" : "CC_ItemCard_Kind_Part", m.def.techLevel.ToStringHuman().Named("TECH"));
                case ItemKind.Techprint:
                    return W("CC_ItemCard_Kind_Techprint");
                case ItemKind.Trainer:
                    return W(up.ability != null ? "CC_ItemCard_Kind_Psytrainer" : "CC_ItemCard_Kind_Neurotrainer");
                case ItemKind.Genes:
                    return W("CC_ItemCard_Kind_" + (up.geneKind == "xenogerm" ? "Xenogerm" : up.geneKind == "embryo" ? "Embryo" : "Genepack"));
                case ItemKind.Usable:
                    return W("CC_ItemCard_Kind_Usable");
                case ItemKind.Shell:
                    return W("CC_ItemCard_Kind_Shell");
                case ItemKind.Furniture:
                    return W("CC_ItemCard_Kind_Furniture", (m.def.FirstThingCategory?.LabelCap.ToString() ?? ItemCardMath.Cap(m.def.label ?? "")).Named("WHAT"));
                default:
                    return u.Unfinished ? W("CC_ItemCard_Kind_Unfinished") : m.def.FirstThingCategory?.LabelCap.ToString() ?? W("CC_ItemCard_Kind_Item");
            }
        }

        /// <summary>The colony's side: who at home it is for, and what the colony has for it (every ten seconds).</summary>
        private static void ColonyUse(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            var u = f.Use ??= new UseFacts();
            var up = p.use ?? new UsePart();
            m.forWhom.Clear();
            m.forWhomNote = null;
            m.useRules.Clear();
            m.useGo.Clear();
            u.Who = new List<UseCandidate>();
            if (m.map == null) return;
            var people = m.map.mapPawns.FreeColonistsSpawned.Where(x => x.RaceProps.Humanlike && !x.DevelopmentalStage.Baby()).OrderBy(x => x.LabelShort).ToList();
            try
            {
                switch (m.kind)
                {
                    case ItemKind.Book:
                        ColonyBook(m, up, u, people);
                        break;
                    case ItemKind.BodyPart:
                        ColonyBodyPart(m, up, u, people);
                        break;
                    case ItemKind.Techprint:
                        ColonyTechprint(m, up, u);
                        break;
                    case ItemKind.Trainer:
                        ColonyTrainer(m, up, u, people);
                        break;
                    case ItemKind.Genes:
                        ColonyGenes(m, up, u, people);
                        break;
                    case ItemKind.Usable:
                        ColonyUsable(m, up, u, people);
                        break;
                    case ItemKind.Shell:
                        ColonyShell(m, up, u);
                        break;
                    case ItemKind.Furniture:
                        ColonyFurniture(m, up, u, people);
                        break;
                    default:
                        u.Held = HeldOf(m.map, m.def);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading the colony for " + m.def.defName, e);
            }
        }

        /// <summary>Who at home, ranked (ItemCardMath.Ranked), into the facts and the card's lines; the best in good ink, those who can't in bad.</summary>
        private static void SetWho(ItemCardModel m, List<(Pawn pawn, UseCandidate c)> list, string note)
        {
            var u = m.facts.Use;
            u.Who = ItemCardMath.Ranked(list.Select(x => x.c));
            var best = ItemCardMath.Best(u);
            foreach (var c in u.Who.Take(12))
            {
                var pawn = list.First(x => x.c == c).pawn;
                m.forWhom.Add(new ItemPawnLine { pawn = pawn, text = c.Why, tone = c == best ? 1 : c.Can ? 0 : -1 });
            }
            m.forWhomNote = note;
        }

        /// <summary>How many the colony holds: on the map and not forbidden, and in its people's packs.</summary>
        private static int HeldOf(Map map, ThingDef d)
        {
            int have = 0;
            var player = Faction.OfPlayer;
            foreach (var t in map.listerThings.ThingsOfDef(d))
                if (t != null && t.Spawned && !t.IsForbidden(player)) have += t.stackCount;
            foreach (var x in map.mapPawns.FreeColonistsSpawned)
            {
                var inv = x.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv)
                    if (t.def == d) have += t.stackCount;
            }
            return have;
        }

        /// <summary>The lines from the facts (after the colony's reading, so they follow it).</summary>
        private static void RowsUse(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            var u = f.Use;
            if (u == null) return;
            // The rows that follow the colony are rebuilt from the facts; a book's and a usable item's own rows were made once (LiveUse).
            switch (m.kind)
            {
                case ItemKind.BodyPart:
                    RowsBodyPart(m, u);
                    break;
                case ItemKind.Genes:
                    RowsGenes(m, u);
                    break;
                case ItemKind.Techprint:
                    RowsTechprint(m, u);
                    break;
                case ItemKind.Trainer:
                    RowsTrainer(m, u);
                    break;
                case ItemKind.Shell:
                    RowsShell(m, u);
                    break;
                case ItemKind.Furniture:
                    RowsFurniture(m, p.use, u);
                    break;
                case ItemKind.Other:
                    RowsOther(m, p.use, u);
                    break;
            }
        }

        // ---------------------------------------------------------------- books

        private static readonly AccessTools.FieldRef<ReadingOutcomeDoerGainResearch, Dictionary<ResearchProjectDef, float>> ResearchValues = ResearchRef();
        private static MethodInfo maxLevelOf;
        private static bool maxLevelLooked;

        private static AccessTools.FieldRef<ReadingOutcomeDoerGainResearch, Dictionary<ResearchProjectDef, float>> ResearchRef()
        {
            try
            {
                return AccessTools.FieldRefAccess<ReadingOutcomeDoerGainResearch, Dictionary<ResearchProjectDef, float>>("values");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The highest level a skill book of a quality teaches up to (the game's own rule, read by name; 0 when it can't be read).</summary>
        private static int MaxLevelFor(QualityCategory q)
        {
            if (!maxLevelLooked)
            {
                maxLevelLooked = true;
                maxLevelOf = AccessTools.Method(typeof(BookOutcomeDoerGainSkillExp), "GetMaxSkillLevel");
            }
            try
            {
                return maxLevelOf?.Invoke(null, new object[] { q }) is int n ? n : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>A book's outcomes by its reading comp (once a kind): skills, research, the unknown's research, recreation, a mental break, a quest, a mod's own.</summary>
        private static void ReadBook(ThingDef d, UsePart up)
        {
            var doers = d.GetCompProperties<CompProperties_Readable>()?.doers;
            if (doers != null)
                foreach (var o in doers)
                {
                    if (o == null) continue;
                    if (o is BookOutcomeProperties_GainSkillExp) up.skillBook = true;
                    else if (o is BookOutcomeProperties_GainAnomalyResearch) up.anomaly = true;
                    else if (o is BookOutcomeProperties_GainResearch) up.researchBook = true;
                    else if (o is BookOutcomeProperties_JoyFactorModifier) up.joy = true;
                    else if (o is BookOutcomeProperties_MentalBreak) up.breakRisk = true;
                    else if (o is BookOutcomeProperties_GiveQuest) up.quest = true;
                    else up.otherOutcomes.Add(ItemCardMath.EffectWords(o.GetType().Name));
                }
            up.bookKind = up.skillBook ? "textbook" : up.anomaly ? "tome" : up.researchBook ? "schematic" : up.quest ? "map" : up.joy ? "novel" : "book";
            try
            {
                if (AccessTools.Field(typeof(JobDriver_Reading), "ManualReadTicks")?.GetValue(null) is int ticks && ticks > 0) up.readHours = ticks / ItemCardMath.TicksPerHour;
            }
            catch
            {
                // Two hours, the game's own.
            }
        }

        /// <summary>A book in hand: its skills and the experience an hour, the level it teaches up to, its research, recreation and break risk, the game's own words for each (once a card).</summary>
        private static void LiveBook(ItemCardModel m, UsePart up, UseFacts u)
        {
            u.BookKind = up.bookKind;
            u.Quest = up.quest;
            u.ReadHours = up.readHours;
            u.JoyFactor = up.joy ? 1f : 0f;
            u.Skills.Clear();
            u.Projects.Clear();
            u.ProjectsDone = 0;
            var book = m.thing as Book;
            var notes = new List<string>();
            if (book == null)
            {
                // A kind: its outcomes are chosen when it is written, by its quality.
                if (up.skillBook)
                    m.use.Add((W("CC_ItemCard_Row_Teaches"), W("CC_ItemCard_BookSkillsKind", ItemCardMath.Whole(BookUtility.GetSkillExpForQuality(QualityCategory.Normal) * ItemCardMath.TicksPerHour).Named("XP"))));
                if (up.researchBook || up.anomaly)
                    m.use.Add((W("CC_ItemCard_Row_Research"), W("CC_ItemCard_BookResearchKind", ItemCardMath.Whole(BookUtility.GetResearchExpForQuality(QualityCategory.Normal) * ItemCardMath.TicksPerHour).Named("RESEARCH"))));
            }
            else
            {
                u.Quality = book.TryGetQuality(out var q) ? q.GetLabel() : "";
                u.JoyFactor = up.joy ? book.JoyFactor : 0f;
                u.BreakHour = up.breakRisk ? book.MentalBreakChancePerHour : 0f;
                foreach (var doer in book.BookComp?.Doers ?? Enumerable.Empty<BookOutcomeDoer>())
                {
                    try
                    {
                        if (doer is BookOutcomeDoerGainSkillExp skills && skills.Values != null)
                        {
                            foreach (var kv in skills.Values)
                            {
                                if (kv.Key == null) continue;
                                u.Skills.Add(kv.Key.label);
                                u.SkillXpHour = Mathf.Max(u.SkillXpHour, kv.Value * ItemCardMath.TicksPerHour);
                            }
                            u.MaxLevel = MaxLevelFor(doer.Quality);
                        }
                        else if (doer is ReadingOutcomeDoerGainResearch research && ResearchValues != null)
                        {
                            var values = ResearchValues(research);
                            if (values != null)
                                foreach (var kv in values)
                                {
                                    if (kv.Key == null) continue;
                                    if (kv.Key.IsFinished) u.ProjectsDone++;
                                    else u.Projects.Add(kv.Key.label);
                                    u.ResearchHour = Mathf.Max(u.ResearchHour, kv.Value * ItemCardMath.TicksPerHour);
                                }
                        }
                        string words = doer.GetBenefitsString(null);
                        if (!words.NullOrEmpty()) notes.Add(words.Trim());
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("the item card reading a book's outcome: " + m.def.defName, e);
                    }
                }
                if (u.Skills.Count > 0)
                    m.use.Add((W("CC_ItemCard_Row_Teaches"), W(u.MaxLevel > 0 ? "CC_ItemCard_BookSkills" : "CC_ItemCard_BookSkillsNoMax", ItemCardMath.AndList(u.Skills).Named("SKILLS"),
                        ItemCardMath.Whole(u.SkillXpHour).Named("XP"), u.MaxLevel.Named("MAX"))));
                if (u.Projects.Count + u.ProjectsDone > 0)
                    m.use.Add((W("CC_ItemCard_Row_Research"), u.Projects.Count > 0
                        ? W("CC_ItemCard_BookResearch", ItemCardMath.AndList(u.Projects).Named("PROJECTS"), ItemCardMath.Whole(u.ResearchHour).Named("RESEARCH"))
                          + (u.ProjectsDone > 0 ? " " + W("CC_ItemCard_BookResearchSomeDone", u.ProjectsDone.Named("N")) : "")
                        : W("CC_ItemCard_BookResearchDone")));
                if (up.joy) m.use.Add((W("CC_ItemCard_Row_Joy"), W("CC_ItemCard_BookJoy", ItemCardMath.N(u.JoyFactor).Named("FACTOR"))));
                if (u.BreakHour > 0f) m.use.Add((W("CC_ItemCard_Row_Break"), W("CC_ItemCard_BookBreak", ItemCardMath.Pct(u.BreakHour).Named("CHANCE"))));
            }
            if (up.quest) m.use.Add((W("CC_ItemCard_Row_Quest"), W("CC_ItemCard_BookQuest")));
            foreach (var o in up.otherOutcomes.Where(x => x.Length > 0)) m.use.Add((W("CC_ItemCard_Row_Outcome"), ItemCardMath.Cap(o)));
            m.use.Add((W("CC_ItemCard_Row_Read"), W("CC_ItemCard_BookRead", ItemCardMath.Days(u.ReadHours / 24f).Named("HOURS"))));
            if (notes.Count > 0) m.useNote = string.Join("\n", notes);
        }

        /// <summary>Who at home gains most: a skill book's experience by each one's skills and learning, a research book's reader by how fast they read, a novel's by who most wants recreation.</summary>
        private static void ColonyBook(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            var book = m.thing as Book;
            // A kind of skill or research book: what it teaches is chosen when it is written, so nobody is ranked.
            if (book == null && (up.skillBook || up.researchBook || up.anomaly))
            {
                m.forWhomNote = W("CC_ItemCard_WhoBookKind");
                return;
            }
            var list = new List<(Pawn, UseCandidate)>();
            var skillDoer = book?.BookComp?.Doers?.OfType<BookOutcomeDoerGainSkillExp>().FirstOrDefault();
            foreach (var x in people)
            {
                var c = new UseCandidate { Name = x.LabelShort };
                bool reads;
                try
                {
                    reads = BookUtility.CanReadEver(x);
                }
                catch
                {
                    reads = true;
                }
                float speed = 1f;
                try
                {
                    speed = BookUtility.GetReadingModifier(x);
                }
                catch
                {
                    // Read as plain.
                }
                if (!reads)
                {
                    c.Why = W("CC_ItemCard_WhoCantRead");
                    list.Add((x, c));
                    continue;
                }
                if (skillDoer?.Values != null)
                {
                    var words = new List<string>();
                    foreach (var kv in skillDoer.Values)
                    {
                        var rec = x.skills?.GetSkill(kv.Key);
                        if (rec == null) continue;
                        string passion = rec.passion != Passion.None ? Passions.Label(rec.passion) : null;
                        string who = kv.Key.LabelCap + " " + rec.Level + (passion != null ? " (" + passion + ")" : "");
                        if (rec.TotallyDisabled)
                        {
                            words.Add(W("CC_ItemCard_WhoSkillDisabled", kv.Key.label.Named("SKILL")));
                            continue;
                        }
                        float lf;
                        try
                        {
                            lf = rec.LearnRateFactor();
                        }
                        catch
                        {
                            lf = ItemCardMath.PassionFactor((int)rec.passion);
                        }
                        float gain = ItemCardMath.BookGain(kv.Value * ItemCardMath.TicksPerHour * speed, lf, rec.Level, u.MaxLevel, false);
                        c.Gain += gain;
                        words.Add(gain > 0f ? W("CC_ItemCard_WhoSkillGain", who.Named("WHO"), ItemCardMath.Whole(gain).Named("XP")) : W("CC_ItemCard_WhoSkillPast", who.Named("WHO"), u.MaxLevel.Named("MAX")));
                    }
                    c.Can = c.Gain > 0f;
                    c.Why = string.Join(", ", words);
                }
                else if (up.joy && !up.researchBook && !up.anomaly && !up.skillBook)
                {
                    float joy = x.needs?.joy?.CurLevelPercentage ?? 1f;
                    c.Can = x.needs?.joy != null;
                    c.Gain = 1f - joy;
                    c.Why = c.Can ? W("CC_ItemCard_WhoJoy", joy.ToStringPercent().Named("JOY")) : W("CC_ItemCard_WhoNoJoy");
                }
                else
                {
                    c.Can = true;
                    c.Gain = speed;
                    c.Why = W("CC_ItemCard_WhoReads", speed.ToStringPercent().Named("SPEED"));
                }
                list.Add((x, c));
            }
            SetWho(m, list, null);
        }

        // ---------------------------------------------------------------- techprints

        /// <summary>The project: how many are applied of how many, whether it can be studied yet, what it opens (every ten seconds).</summary>
        private static void ColonyTechprint(ItemCardModel m, UsePart up, UseFacts u)
        {
            var r = up.project;
            u.Held = HeldOf(m.map, m.def);
            if (r == null) return;
            u.Project = r.LabelCap.ToString();
            u.Applied = r.TechprintsApplied;
            u.Needed = r.TechprintCount;
            u.ProjectDone = r.IsFinished;
            var waits = new List<string>();
            foreach (var pre in (r.prerequisites ?? new List<ResearchProjectDef>()).Concat(r.hiddenPrerequisites ?? new List<ResearchProjectDef>()))
                if (pre != null && !pre.IsFinished) waits.Add(pre.label);
            u.Waits = ItemCardMath.AndList(waits.Take(3).ToList());
            try
            {
                u.BenchFit = r.PlayerHasAnyAppropriateResearchBench;
            }
            catch
            {
                u.BenchFit = true;
            }
            var unlocked = new List<Def>();
            try
            {
                unlocked = r.UnlockedDefs ?? new List<Def>();
            }
            catch
            {
                // Nothing named.
            }
            u.Unlocks = unlocked.Count;
            var names = unlocked.Where(x => x != null).Select(x => x.label).Where(x => !x.NullOrEmpty()).Distinct().Take(3).ToList();
            u.UnlockWords = names.Count == 0 ? "" : ItemCardMath.AndList(names) + (unlocked.Count > names.Count ? " " + W("CC_ItemCard_AndMoreThings", (unlocked.Count - names.Count).Named("N")) : "");
        }

        private static void RowsTechprint(ItemCardModel m, UseFacts u)
        {
            m.use.Clear();
            m.useNote = null;
            if (u.Project.Length == 0) return;
            int left = ItemCardMath.PrintsLeft(u.Applied, u.Needed);
            m.use.Add((W("CC_ItemCard_Row_Project"), u.Project));
            m.use.Add((W("CC_ItemCard_Row_Applied"), u.ProjectDone ? W("CC_ItemCard_PrintDone") : left == 0 ? W("CC_ItemCard_PrintAllApplied", u.Needed.Named("NEEDED"))
                : W("CC_ItemCard_PrintApplied", u.Applied.Named("APPLIED"), u.Needed.Named("NEEDED"), left.Named("LEFT"))));
            if (!u.ProjectDone)
                m.use.Add((W("CC_ItemCard_Row_CanStudy"), u.Waits.Length > 0 ? W("CC_ItemCard_PrintWaits", u.Waits.Named("WAITS"))
                    : left > 0 ? W("CC_ItemCard_PrintNeedsPrints")
                    : !u.BenchFit ? W("CC_ItemCard_PrintNeedsBench") : W("CC_ItemCard_PrintCanStudy")));
            if (u.Unlocks > 0) m.use.Add((W("CC_ItemCard_Row_Opens"), u.UnlockWords.Length > 0 ? ItemCardMath.Cap(u.UnlockWords) : u.Unlocks.ToString()));
        }

        // ---------------------------------------------------------------- trainers

        /// <summary>Who at home gains most: a neurotrainer's levels by each one's skill and passion (the game's own curve), a psytrainer's psycasters by their psylink.</summary>
        private static void ColonyTrainer(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            var list = new List<(Pawn, UseCandidate)>();
            foreach (var x in people)
            {
                var c = new UseCandidate { Name = x.LabelShort };
                if (up.skill != null)
                {
                    var rec = x.skills?.GetSkill(up.skill);
                    if (rec == null || rec.TotallyDisabled)
                        c.Why = W("CC_ItemCard_WhoSkillDisabled", up.skill.label.Named("SKILL"));
                    else
                    {
                        float factor;
                        try
                        {
                            factor = rec.LearnRateFactor(true);
                        }
                        catch
                        {
                            factor = ItemCardMath.PassionFactor((int)rec.passion);
                        }
                        float now = rec.Level + (rec.Level < ItemCardMath.MaxSkillLevel ? rec.xpSinceLastLevel / Mathf.Max(1f, SkillRecord.XpRequiredToLevelUpFrom(rec.Level)) : 0f);
                        float after = ItemCardMath.LevelAfter(rec.Level, rec.xpSinceLastLevel, up.xp * factor, SkillRecord.XpRequiredToLevelUpFrom);
                        string passion = rec.passion != Passion.None ? Passions.Label(rec.passion) : null;
                        c.Gain = Mathf.Max(0f, after - now);
                        c.Can = c.Gain > 0.01f;
                        c.Why = c.Can
                            ? W("CC_ItemCard_WhoTrainer", rec.Level.Named("LEVEL"), (passion != null ? " (" + passion + ")" : "").Named("PASSION"), ItemCardMath.N((float)Math.Round(after, 1)).Named("AFTER"))
                            : W("CC_ItemCard_WhoTrainerTop", rec.Level.Named("LEVEL"));
                    }
                }
                else if (up.ability != null)
                {
                    int level = 0;
                    bool has = false;
                    try
                    {
                        level = x.HasPsylink ? x.GetPsylinkLevel() : 0;
                        has = x.abilities?.GetAbility(up.ability) != null;
                    }
                    catch
                    {
                        // Read as none.
                    }
                    c.Can = level >= up.ability.level && !has;
                    c.Gain = c.Can ? level : 0f;
                    c.Why = has ? W("CC_ItemCard_WhoHasPsycast") : level <= 0 ? W("CC_ItemCard_WhoNoPsylink") : level < up.ability.level
                        ? W("CC_ItemCard_WhoPsylinkLow", level.Named("LEVEL"), up.ability.level.Named("NEED"))
                        : W("CC_ItemCard_WhoPsylink", level.Named("LEVEL"));
                }
                // The thing in hand: the game's own word on who may use it.
                if (c.Can && m.thing != null)
                {
                    var why = GameSaysNo(m.thing, x);
                    if (why != null)
                    {
                        c.Can = false;
                        c.Why = why;
                    }
                }
                list.Add((x, c));
            }
            SetWho(m, list, null);
        }

        private static void RowsTrainer(ItemCardModel m, UseFacts u)
        {
            m.use.Clear();
            if (u.Skill.Length > 0)
            {
                m.use.Add((W("CC_ItemCard_Row_Teaches"), W("CC_ItemCard_TrainerSkill", ItemCardMath.Whole(u.Xp).Named("XP"), u.Skill.Named("SKILL"))));
                m.use.Add((W("CC_ItemCard_Row_Passion"), W("CC_ItemCard_TrainerPassion")));
            }
            if (u.Psycast.Length > 0) m.use.Add((W("CC_ItemCard_Row_Psycast"), W("CC_ItemCard_TrainerPsycast", u.Psycast.Named("PSYCAST"), u.PsycastLevel.Named("LEVEL"))));
            m.use.Add((W("CC_ItemCard_Row_Uses"), W("CC_ItemCard_OneUse")));
        }

        /// <summary>The game's own reason this one can't use the thing in hand (its use comp and each effect's), or null.</summary>
        private static string GameSaysNo(Thing t, Pawn x)
        {
            try
            {
                var usable = t.TryGetComp<CompUsable>();
                if (usable != null)
                {
                    var r = usable.CanBeUsedBy(x, false, true);
                    if (!r.Accepted) return r.Reason.NullOrEmpty() ? W("CC_ItemCard_WhoCantUse") : r.Reason;
                }
                if (t is ThingWithComps tc)
                    foreach (var e in tc.AllComps.OfType<CompUseEffect>())
                    {
                        var r = e.CanBeUsedBy(x);
                        if (!r.Accepted) return r.Reason.NullOrEmpty() ? W("CC_ItemCard_WhoCantUse") : r.Reason;
                    }
            }
            catch
            {
                // The game's check needs more than the card has: no word from it.
            }
            return null;
        }

        // ---------------------------------------------------------------- usable items

        /// <summary>A usable item's effects and targets by their comp classes (the game's and the mods'), what it costs to use, its risks (once a kind).</summary>
        private static void ReadUsable(ThingDef d, UsePart up)
        {
            foreach (var c in d.comps ?? new List<CompProperties>())
            {
                var cls = c?.compClass?.Name ?? "";
                if (cls.Length == 0) continue;
                try
                {
                    if (cls == "CompUseEffect_DestroySelf" || c is CompProperties_UseEffectDestroySelf)
                    {
                        up.usedUp = true;
                        continue;
                    }
                    if (cls.StartsWith("CompTargetable", StringComparison.Ordinal))
                    {
                        up.targets = ItemCardMath.KnownTargets.Contains(cls) ? W("CC_ItemCard_Targets_" + cls) : W("CC_ItemCard_Targets_Other");
                        if (c is CompProperties_Targetable tp && tp.psychicSensitiveTargetsOnly) up.targets += " " + W("CC_ItemCard_TargetsSensitive");
                        continue;
                    }
                    bool effect = typeof(CompUseEffect).IsAssignableFrom(c.compClass) || typeof(CompTargetEffect).IsAssignableFrom(c.compClass);
                    if (!effect || cls == "CompUseEffect_PlaySound" || cls == "CompUseEffect_PlayWarmupSound" || cls == "CompUseEffect_Artifact") continue;
                    up.effects.Add(cls);
                    string words;
                    if (ItemCardMath.KnownEffects.Contains(cls)) words = W("CC_ItemCard_Does_" + cls, EffectThing(c).Named("WHAT"));
                    else words = W("CC_ItemCard_Does_Other", ItemCardMath.EffectWords(cls).Named("WHAT"));
                    up.doesWords.Add(words);
                    // The risks of using it, in the effect's own numbers.
                    if (c is CompProperties_TargetEffect_BrainDamageChance brain && brain.brainDamageChance > 0f)
                        up.risks.Add(W("CC_ItemCard_RiskBrain", ItemCardMath.Pct(brain.brainDamageChance).Named("CHANCE")));
                    else if (c is CompProperties_TargetEffect_GoodwillImpact goodwill && goodwill.goodwillImpact != 0)
                        up.risks.Add(W("CC_ItemCard_RiskGoodwill", goodwill.goodwillImpact.Named("N")));
                    else if (c is CompProperties_TargetEffectResurrect res && res.withSideEffects) up.risks.Add(W("CC_ItemCard_RiskResurrect"));
                    else if (ItemCardMath.RiskyEffects.Contains(cls)) up.risks.Add(W("CC_ItemCard_Risk_" + cls));
                }
                catch
                {
                    // A mod's comp that can't be read: left out.
                }
            }
            if (up.targets.Length == 0) up.targets = W("CC_ItemCard_Targets_User");
        }

        /// <summary>What an effect gives, by its own props: the hediff an implant or a use adds, else nothing.</summary>
        private static string EffectThing(CompProperties c)
        {
            if (c is CompProperties_UseEffectInstallImplant implant) return implant.hediffDef?.label ?? "";
            if (c is CompProperties_UseEffectAddHediff add) return add.hediffDef?.label ?? "";
            return "";
        }

        private static void LiveUsable(ItemCardModel m, UsePart up, UseFacts u)
        {
            u.Effects = up.effects.ToList();
            u.Does = ItemCardMath.AndList(up.doesWords.Distinct().ToList());
            u.Targets = up.targets;
            u.UsedUp = up.usedUp;
            u.Risks = up.risks.ToList();
            m.use.Add((W("CC_ItemCard_Row_Does"), u.Does.Length > 0 ? ItemCardMath.Cap(u.Does) : W("CC_ItemCard_DoesUnknown")));
            m.use.Add((W("CC_ItemCard_Row_Targets"), ItemCardMath.Cap(u.Targets)));
            m.use.Add((W("CC_ItemCard_Row_Uses"), up.usedUp ? W("CC_ItemCard_OneUse") : W("CC_ItemCard_ManyUses")));
            if (u.Risks.Count > 0) m.use.Add((W("CC_ItemCard_Row_Risks"), ItemCardMath.Cap(ItemCardMath.AndList(u.Risks))));
        }

        /// <summary>Who at home can use it: the game's own check for each on the thing in hand.</summary>
        private static void ColonyUsable(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            u.Held = HeldOf(m.map, m.def);
            if (m.thing == null) return;
            var list = new List<(Pawn, UseCandidate)>();
            foreach (var x in people)
            {
                var why = GameSaysNo(m.thing, x);
                list.Add((x, new UseCandidate { Name = x.LabelShort, Can = why == null, Gain = why == null ? 1f : 0f, Why = why ?? W("CC_ItemCard_WhoCanUse") }));
            }
            SetWho(m, list, null);
        }
    }
}
