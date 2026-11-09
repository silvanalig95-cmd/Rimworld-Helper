using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Role clothes the colony can reach (the player, 9 November 2026, of VFE Classical's toga that every specialist role
    /// may ask for: "it's not really a standard research or anything worth spending 1200 research points into... across
    /// several playthroughs and ideologies already"). The game reads here into the rule of Faith/RoleClothesMath.cs:
    /// <list type="bullet">
    /// <item>when the game chooses a role's clothes for one of the colony's ideoligions (a new ideoligion, a reform, a role
    /// added, the editor's randomize), its first draw leaves out clothes out of reach (Faith/RoleClothesPatch.cs; the
    /// setting "Role clothes the colony can reach"); the factions' own ideoligions are left as the game makes them;</item>
    /// <item>the Mood tab says what a role's clothes need ("Toga: needs the research Togas (1200), not started") and offers
    /// to choose other clothes for the role (Faith/RoleClothesWindow.cs), for the colony's own ideoligions only.</item>
    /// </list>
    /// </summary>
    public static class RoleClothes
    {
        /// <summary>The first draw of a role's clothes leaves out what is out of reach (the setting, with the deeper hooks).</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.roleClothesReach ?? true);

        /// <summary>
        /// The colony's ideoligion whose role is choosing its clothes right now (set around the game's own choice by
        /// Patch_RoleClothesChoose), or null. The game makes ideoligions on a long event's thread too, so each thread has its own.
        /// </summary>
        [ThreadStatic] internal static Ideo choosingFor;

        private static List<ThingDef> apparel;
        private static Dictionary<ThingDef, List<RecipeDef>> recipesFor;
        private static Dictionary<int, float> medians;
        private static HashSet<ResearchProjectTagDef> gameStarts, modStarts;

        public static void ResetCache()
        {
            apparel = null;
            recipesFor = null;
            medians = null;
            gameStarts = modStarts = null;
        }

        // ---------------- Whose ideoligion ----------------

        /// <summary>One of the colony's own ideoligions: the player's faction holds it (its main one, or one of its people's).</summary>
        public static bool ColonyIdeo(Ideo ideo)
        {
            if (ideo == null) return false;
            try
            {
                return Faction.OfPlayerSilentFail?.ideos?.Has(ideo) == true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether a role's clothes being chosen are the colony's: made for the player's faction (the game names the faction
        /// it makes an ideoligion for: the player's in the ideoligion pages, a reform and a role added; a faction's own at
        /// world generation), or, with no faction given (the editor's randomize), an ideoligion the colony holds.
        /// </summary>
        public static bool ForColony(Precept_Role role, FactionDef generatingFor)
        {
            if (role?.ideo == null) return false;
            if (generatingFor != null) return generatingFor.isPlayer;
            return ColonyIdeo(role.ideo);
        }

        // ---------------- The game's facts, once ----------------

        private static List<ThingDef> AllApparel() =>
            apparel ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d?.apparel != null && d.IsApparel).ToList();

        /// <summary>The recipes that make a thing (its own recipe maker's and any other).</summary>
        private static List<RecipeDef> RecipesFor(ThingDef def)
        {
            if (recipesFor == null)
            {
                var map = new Dictionary<ThingDef, List<RecipeDef>>();
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (r?.products == null || r.IsSurgery) continue;
                    foreach (var p in r.products)
                    {
                        if (p?.thingDef?.apparel == null) continue;
                        if (!map.TryGetValue(p.thingDef, out var list)) map[p.thingDef] = list = new List<RecipeDef>();
                        if (!list.Contains(r)) list.Add(r);
                    }
                }
                recipesFor = map;
            }
            return def != null && recipesFor.TryGetValue(def, out var found) ? found : new List<RecipeDef>();
        }

        /// <summary>Each age's usual cost: the median of the main tab's projects (those bought with research points).</summary>
        public static Dictionary<int, float> Medians()
        {
            if (medians == null)
            {
                var main = new List<(int, float)>();
                foreach (var p in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                    if (p != null && MainTab(p) && p.knowledgeCategory == null && p.baseCost > 0f)
                        main.Add(((int)p.techLevel, p.baseCost));
                medians = RoleClothesMath.Medians(main);
            }
            return medians;
        }

        private static bool MainTab(ResearchProjectDef p) => p.tab == null || p.tab == ResearchTabDefOf.Main;

        /// <summary>The start tags of the game's own starts, and of the starts from mods (the factions a player can start as).</summary>
        private static void Starts()
        {
            if (gameStarts != null) return;
            var game = new HashSet<ResearchProjectTagDef>();
            var mods = new HashSet<ResearchProjectTagDef>();
            foreach (var f in DefDatabase<FactionDef>.AllDefsListForReading)
            {
                if (f == null || !f.isPlayer || f.startingResearchTags == null) continue;
                bool official = f.modContentPack?.IsOfficialMod ?? true;
                foreach (var t in f.startingResearchTags)
                    if (t != null)
                        (official ? game : mods).Add(t);
            }
            mods.ExceptWith(game);
            gameStarts = game;
            modStarts = mods;
        }

        // ---------------- Which pieces meet a requirement ----------------

        /// <summary>Every piece of clothing that meets a requirement: its own pieces, and those its tags let in, on the body it names.</summary>
        public static List<ThingDef> Pieces(ApparelRequirement r)
        {
            var list = new List<ThingDef>();
            if (r == null) return list;
            if (r.requiredDefs != null)
                foreach (var d in r.requiredDefs)
                    if (d?.apparel != null && OnTheBody(r, d) && !list.Contains(d))
                        list.Add(d);
            if (!r.requiredTags.NullOrEmpty() || !r.allowedTags.NullOrEmpty())
                foreach (var d in AllApparel())
                    if (d.apparel.tags != null && OnTheBody(r, d) && (AnyTag(r.requiredTags, d) || AnyTag(r.allowedTags, d)) && !list.Contains(d))
                        list.Add(d);
            return list;
        }

        private static bool OnTheBody(ApparelRequirement r, ThingDef d)
        {
            if (r.bodyPartGroupsMatchAny.NullOrEmpty() || d.apparel?.bodyPartGroups == null) return true;
            foreach (var g in r.bodyPartGroupsMatchAny)
                if (d.apparel.bodyPartGroups.Contains(g))
                    return true;
            return false;
        }

        private static bool AnyTag(List<string> tags, ThingDef d)
        {
            if (tags == null || d.apparel?.tags == null) return false;
            foreach (var t in tags)
                if (d.apparel.tags.Contains(t))
                    return true;
            return false;
        }

        /// <summary>The same clothes asked for: the same pieces and tags (a role's saved copy and the pool's own).</summary>
        public static bool SameAs(PreceptApparelRequirement a, PreceptApparelRequirement b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b) || ReferenceEquals(a.requirement, b.requirement)) return true;
            var x = a.requirement;
            var y = b.requirement;
            if (x == null || y == null) return false;
            return SameSet(x.requiredDefs, y.requiredDefs) && SameSet(x.requiredTags, y.requiredTags) && SameSet(x.allowedTags, y.allowedTags);
        }

        private static bool SameSet<T>(List<T> a, List<T> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na == 0 || nb == 0) return na == nb;
            return a.All(b.Contains) && b.All(a.Contains);
        }

        /// <summary>A requirement's name as the game's editor shows it: its group's label, else its first piece's (capitalized, or as in a sentence).</summary>
        public static string LabelOf(PreceptApparelRequirement r, bool cap = true)
        {
            var req = r?.requirement;
            if (req == null) return "?";
            string label = !req.groupLabel.NullOrEmpty() ? req.groupLabel : Pieces(req).FirstOrDefault()?.label;
            if (label.NullOrEmpty()) return "?";
            return cap ? label.CapitalizeFirst() : label;
        }

        // ---------------- The reach of one ideoligion's role ----------------

        /// <summary>
        /// The reach rule read for one ideoligion's role at one moment: the colony's research and what it has, the starts, and
        /// what the ideoligion calls for. Short-lived: made for one choice of clothes, one mood line or one dialog.
        /// </summary>
        public sealed class Context
        {
            public readonly Ideo ideo;
            public readonly PreceptDef roleDef;
            private readonly bool playing;
            private readonly HashSet<ResearchProjectTagDef> ownStarts = new HashSet<ResearchProjectTagDef>();
            private readonly HashSet<ResearchProjectDef> memeStarts = new HashSet<ResearchProjectDef>();
            private readonly Dictionary<ResearchProjectDef, RoleClothesMath.Research> research = new Dictionary<ResearchProjectDef, RoleClothesMath.Research>();
            private readonly Dictionary<ThingDef, RoleClothesMath.Item> items = new Dictionary<ThingDef, RoleClothesMath.Item>();
            private readonly Dictionary<ThingDef, bool> ideoCalls = new Dictionary<ThingDef, bool>();
            private readonly Dictionary<ThingDef, bool> benches = new Dictionary<ThingDef, bool>();
            public readonly Dictionary<int, float> ages;

            public Context(Ideo ideo, PreceptDef roleDef)
            {
                this.ideo = ideo;
                this.roleDef = roleDef;
                playing = Current.ProgramState == ProgramState.Playing && Find.ResearchManager != null;
                ages = Medians();
                Starts();
                var own = Faction.OfPlayerSilentFail?.def?.startingResearchTags;
                if (own != null) ownStarts.UnionWith(own.Where(t => t != null));
                if (ideo?.memes != null)
                    foreach (var m in ideo.memes)
                        if (m?.startingResearchProjects != null)
                            memeStarts.UnionWith(m.startingResearchProjects.Where(p => p != null));
            }

            /// <summary>One project as the rule reads it (before the game begins, finished means the colony's start gives it).</summary>
            public RoleClothesMath.Research ResearchOf(ResearchProjectDef p)
            {
                if (research.TryGetValue(p, out var r)) return r;
                bool own = p.tags != null && p.tags.Any(ownStarts.Contains);
                r = new RoleClothesMath.Research
                {
                    name = p.LabelCap.Resolve(),
                    age = (int)p.techLevel,
                    cost = p.baseCost,
                    shownCost = p.baseCost,
                    mainTab = MainTab(p),
                    ownStart = own,
                    gameStart = p.tags != null && p.tags.Any(gameStarts.Contains),
                    modStart = p.tags != null && p.tags.Any(modStarts.Contains)
                };
                if (playing)
                {
                    r.finished = p.IsFinished;
                    float cost = p.Cost;
                    r.progress = cost > 0f ? Find.ResearchManager.GetProgress(p) / cost : 0f;
                    try
                    {
                        r.shownCost = p.CostApparent;
                    }
                    catch
                    {
                        r.shownCost = p.baseCost;
                    }
                }
                else r.finished = own || memeStarts.Contains(p);
                research[p] = r;
                return r;
            }

            /// <summary>Whether the colony has this bench or spot built on one of its homes (never before the game begins).</summary>
            private bool Built(ThingDef bench)
            {
                if (!playing || bench == null) return false;
                if (benches.TryGetValue(bench, out bool b)) return b;
                b = Find.Maps.Any(m => m != null && m.IsPlayerHome && m.listerBuildings.ColonistsHaveBuilding(bench));
                benches[bench] = b;
                return b;
            }

            /// <summary>One piece as the rule reads it: every way to make it, how many the colony has, whether the ideoligion calls for it.</summary>
            public RoleClothesMath.Item ItemOf(ThingDef def)
            {
                if (items.TryGetValue(def, out var item)) return item;
                item = new RoleClothesMath.Item { name = def.LabelCap.Resolve(), has = Has(def), calledFor = IdeoCalls(def) };
                foreach (var recipe in RecipesFor(def))
                {
                    var recipeResearch = new List<ResearchProjectDef>();
                    if (recipe.researchPrerequisite != null) recipeResearch.Add(recipe.researchPrerequisite);
                    if (recipe.researchPrerequisites != null) recipeResearch.AddRange(recipe.researchPrerequisites.Where(p => p != null));
                    var users = recipe.AllRecipeUsers?.Where(u => u != null).Distinct().ToList() ?? new List<ThingDef>();
                    var benchUsers = users.Where(u => u.category == ThingCategory.Building).ToList();
                    // Made by hand by a kind of person (a mod's own) rather than at a bench: one way with only the recipe's research.
                    if (benchUsers.Count == 0 && users.Count > 0) benchUsers.Add(null);
                    foreach (var bench in benchUsers)
                    {
                        var way = new RoleClothesMath.Way { bench = bench?.label, benchBuilt = bench == null || Built(bench) };
                        var all = new List<ResearchProjectDef>(recipeResearch);
                        if (bench?.researchPrerequisites != null) all.AddRange(bench.researchPrerequisites.Where(p => p != null));
                        foreach (var p in all.Distinct()) way.research.Add(ResearchOf(p));
                        item.ways.Add(way);
                    }
                }
                items[def] = item;
                return item;
            }

            /// <summary>How many the colony has: worn by its people anywhere, or lying on its homes not forbidden.</summary>
            private int Has(ThingDef def)
            {
                if (!playing) return 0;
                int n = 0;
                try
                {
                    foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                    {
                        var worn = p?.apparel?.WornApparel;
                        if (worn == null) continue;
                        foreach (var a in worn)
                            if (a?.def == def)
                                n++;
                    }
                    foreach (var map in Find.Maps)
                    {
                        if (map == null || !map.IsPlayerHome) continue;
                        foreach (var t in map.listerThings.ThingsOfDef(def))
                            if (t != null && t.Spawned && !t.IsForbidden(Faction.OfPlayer))
                                n += Math.Max(1, t.stackCount);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("role clothes: what the colony has", e);
                }
                return n;
            }

            /// <summary>
            /// The ideoligion calls for a piece: a desired-apparel precept names it, one of its styles has its own look for it,
            /// or its culture, a meme, a style or the role itself comes from the mod that adds it (a roman culture keeps the
            /// toga of VFE Classical; never by the game's own content, which every ideoligion has).
            /// </summary>
            private bool IdeoCalls(ThingDef def)
            {
                if (ideo == null || def == null) return false;
                if (ideoCalls.TryGetValue(def, out bool calls)) return calls;
                try
                {
                    calls = ideo.PreceptsListForReading.Any(p => p is Precept_Apparel pa && pa.apparelDef == def)
                            || ideo.thingStyleCategories != null && ideo.thingStyleCategories.Any(s => s?.category?.thingDefStyles != null && s.category.thingDefStyles.Any(t => t?.ThingDef == def));
                    var pack = def.modContentPack;
                    if (!calls && pack != null && !pack.IsOfficialMod)
                        calls = ideo.culture?.modContentPack == pack
                                || ideo.memes != null && ideo.memes.Any(m => m?.modContentPack == pack)
                                || ideo.thingStyleCategories != null && ideo.thingStyleCategories.Any(s => s?.category?.modContentPack == pack)
                                || roleDef?.modContentPack == pack;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("role clothes: what the ideoligion calls for", e);
                    calls = false;
                }
                ideoCalls[def] = calls;
                return calls;
            }

            /// <summary>A meme of the ideoligion asks for these clothes itself.</summary>
            private bool FromMeme(PreceptApparelRequirement req)
            {
                if (ideo?.memes == null || req == null) return false;
                foreach (var m in ideo.memes)
                    if (m?.apparelRequirements != null && m.apparelRequirements.Any(x => SameAs(x, req)))
                        return true;
                return false;
            }

            /// <summary>The reach of each piece that meets a requirement, with the requirement's own call (a meme's) on each.</summary>
            public List<(ThingDef def, RoleClothesMath.Item item, RoleClothesMath.Reach reach)> PiecesOf(PreceptApparelRequirement req)
            {
                var list = new List<(ThingDef, RoleClothesMath.Item, RoleClothesMath.Reach)>();
                if (req?.requirement == null) return list;
                bool meme = FromMeme(req);
                foreach (var d in Pieces(req.requirement))
                {
                    var it = ItemOf(d);
                    bool was = it.calledFor;
                    if (meme) it.calledFor = true;
                    var reach = RoleClothesMath.Of(it, ages);
                    it.calledFor = was;
                    list.Add((d, it, reach));
                }
                return list;
            }

            /// <summary>A requirement's best piece and its reach (in reach when any piece is); null piece for none.</summary>
            public (ThingDef def, RoleClothesMath.Item item, RoleClothesMath.Reach reach) Best(PreceptApparelRequirement req)
            {
                var pieces = PiecesOf(req);
                if (pieces.Count == 0) return (null, null, RoleClothesMath.Reach.Unmade);
                var best = pieces[0];
                foreach (var p in pieces)
                    if (p.reach < best.reach || p.reach == best.reach && Left(p.item) < Left(best.item))
                        best = p;
                return best;
            }

            private float Left(RoleClothesMath.Item item)
            {
                int w = RoleClothesMath.BestWay(item, ages);
                return w < 0 ? float.MaxValue : RoleClothesMath.Left(item.ways[w]);
            }

            /// <summary>
            /// How to come by a piece, in a few words: "the colony has 2", "can be made now at a crafting spot", "needs the
            /// research Togas (1200), not started", "nothing the colony knows makes it; a trader may sell one".
            /// </summary>
            public string HowToGet(RoleClothesMath.Item item, RoleClothesMath.Reach reach)
            {
                if (item == null) return "";
                switch (reach)
                {
                    case RoleClothesMath.Reach.Has:
                        return "CC_RoleClothes_How_Has".Translate(item.has.Named("COUNT")).Resolve();
                    case RoleClothesMath.Reach.Unmade:
                        return "CC_RoleClothes_How_Unmade".Translate().Resolve();
                }
                int w = RoleClothesMath.BestWay(item, ages);
                if (w < 0) return "CC_RoleClothes_How_Unmade".Translate().Resolve();
                var way = item.ways[w];
                if (reach == RoleClothesMath.Reach.Ready || RoleClothesMath.Open(way))
                {
                    if (way.bench.NullOrEmpty()) return "CC_RoleClothes_How_MakeByHand".Translate().Resolve();
                    return "CC_RoleClothes_How_Make".Translate(Find.ActiveLanguageWorker.WithIndefiniteArticle(way.bench).Named("BENCH")).Resolve();
                }
                var missing = RoleClothesMath.Missing(way);
                string list = missing.Count == 0 ? ""
                    : missing.Count == 1 ? ResearchPart(missing[0])
                    : missing.Count == 2 ? "CC_RoleClothes_TwoResearch".Translate(ResearchPart(missing[0]).Named("A"), ResearchPart(missing[1]).Named("B")).Resolve()
                    : "CC_RoleClothes_ManyResearch".Translate(ResearchPart(missing[0]).Named("A"), ResearchPart(missing[1]).Named("B"), (missing.Count - 2).Named("COUNT")).Resolve();
                return "CC_RoleClothes_How_Research".Translate(list.Named("RESEARCH")).Resolve();
            }

            /// <summary>Why a piece is out of reach, for its tooltip: each project that puts it there, with its age's usual cost.</summary>
            public string WhyOut(RoleClothesMath.Item item)
            {
                var lines = new List<string>();
                foreach (var r in RoleClothesMath.Blockers(item, ages))
                {
                    string age = ((TechLevel)r.age).ToStringHuman();
                    lines.Add("CC_RoleClothes_WhyOut".Translate(r.name.Named("RESEARCH"), r.cost.ToString("0").Named("COST"), age.Named("AGE"),
                        RoleClothesMath.Typical(r.age, ages).ToString("0").Named("USUAL")).Resolve());
                }
                return string.Join("\n", lines);
            }
        }

        /// <summary>"Togas (1200), not started" / "Smithing (700), 40% done".</summary>
        public static string ResearchPart(RoleClothesMath.Research r)
        {
            int pct = RoleClothesMath.Percent(r.progress, r.finished);
            string state = pct <= 0 ? "CC_RoleClothes_NotStarted".Translate().Resolve() : "CC_RoleClothes_PercentDone".Translate(pct.Named("PCT")).Resolve();
            return "CC_RoleClothes_ResearchPart".Translate(r.name.Named("RESEARCH"), r.shownCost.ToString("0").Named("COST"), state.Named("STATE")).Resolve();
        }

        // ---------------- The game's own choice ----------------

        /// <summary>The game's first draw of a role's clothes for the colony, with what is out of reach left out (in place).</summary>
        public static void FilterPool(Ideo ideo, PreceptDef roleDef, List<PreceptApparelRequirement> pool)
        {
            if (pool == null || pool.Count == 0) return;
            var ctx = new Context(ideo, roleDef);
            var reaches = pool.Select(r => ctx.Best(r).reach).ToList();
            var kept = RoleClothesMath.Kept(reaches);
            if (kept.Count == pool.Count) return;
            var keep = kept.Select(i => pool[i]).ToList();
            pool.Clear();
            pool.AddRange(keep);
        }

        // ---------------- The mood ----------------

        /// <summary>The role of a "wants the role's clothes" mood, when it is one of the colony's own ideoligions.</summary>
        public static Precept_Role RoleOf(Thought t)
        {
            try
            {
                var role = (t as Thought_IdeoRoleApparelRequirementNotMet)?.sourcePrecept as Precept_Role;
                return role != null && ColonyIdeo(role.ideo) ? role : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The role's clothes this colonist isn't wearing (as the game's mood counts them).</summary>
        public static List<PreceptApparelRequirement> Unmet(Pawn p, Precept_Role role)
        {
            var list = new List<PreceptApparelRequirement>();
            if (p?.apparel == null || role?.apparelRequirements == null) return list;
            foreach (var r in role.apparelRequirements)
            {
                var req = r?.requirement;
                if (req == null) continue;
                try
                {
                    if (req.IsActive(p) && !req.IsMet(p)) list.Add(r);
                }
                catch
                {
                }
            }
            return list;
        }

        /// <summary>
        /// What to do about the mood: each piece of the role's clothes they aren't wearing and how to come by it ("Toga: needs
        /// the research Togas (1200), not started."), then "Or choose other clothes for the role." Null when nothing can be said.
        /// </summary>
        public static string Advice(Pawn p, Precept_Role role)
        {
            if (p == null || role == null) return null;
            try
            {
                var unmet = Unmet(p, role);
                if (unmet.Count == 0) return null;
                var ctx = new Context(role.ideo, role.def);
                var lines = new List<string>();
                foreach (var r in unmet)
                {
                    var best = ctx.Best(r);
                    string how = best.item == null ? "CC_RoleClothes_How_Unmade".Translate().Resolve() : ctx.HowToGet(best.item, best.reach);
                    lines.Add("CC_RoleClothes_Line".Translate(LabelOf(r).Named("ITEM"), how.Named("HOW")).Resolve());
                }
                lines.Add("CC_RoleClothes_OrChoose".Translate().Resolve());
                return string.Join(" ", lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("role clothes: the mood's advice", e);
                return null;
            }
        }

        // ---------------- Choosing other clothes ----------------

        /// <summary>
        /// Replaces one of a role's clothes the way the game's editor does (Dialog_EditPrecept: the role's ApparelRequirements
        /// set anew, every precept's tooltip cleared), with a copy of the pool's requirement, and has whoever holds the role
        /// read their clothes again. The game's mood follows on its own. False, with the game's own message, when the new
        /// clothes can't be worn with the others.
        /// </summary>
        public static bool Replace(Precept_Role role, int index, PreceptApparelRequirement with)
        {
            if (role?.apparelRequirements == null || with?.requirement == null || index < 0 || index >= role.apparelRequirements.Count) return false;
            var list = role.apparelRequirements.ToList();
            var copy = new PreceptApparelRequirement();
            with.CopyTo(copy);
            list[index] = copy;
            if (list.Any(x => x.RequirementOverlapsOther(list, out _)))
            {
                Messages.Message("OverlappingRoleApparel".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            role.ApparelRequirements = list;
            foreach (var p in role.ideo.PreceptsListForReading) p.ClearTipCache();
            foreach (var p in role.ChosenPawns())
                p?.apparel?.Notify_RoleChanged();
            return true;
        }
    }
}
