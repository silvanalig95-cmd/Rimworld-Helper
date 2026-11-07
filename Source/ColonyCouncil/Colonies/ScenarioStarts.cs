using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.SketchGen;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One of a scenario's parts as a scenario's start reads it: the part, whether it comes, and what it changes if not.</summary>
    public sealed class StartPart
    {
        public ScenPart part;
        public ScenCarry carry;
        public ScenWhole whole;
        public string Type => part?.GetType().FullName;
    }

    /// <summary>What a scenario brings to a new colony's start (the player's decision 3A), read once from its parts.</summary>
    public sealed class ScenarioStart
    {
        public Scenario scenario;
        public string key;
        public bool hooks;
        public readonly List<StartPart> parts = new List<StartPart>();
        /// <summary>Its people part, when it is one the code knows; else null, and the newcomers come as a new game's group.</summary>
        public ScenPart_ConfigPage_ConfigureStartingPawnsBase people;
        /// <summary>How many come, and how many to choose them from (the scenario's count and choice).</summary>
        public int count = 3, candidates = 3;
        public int method;
        public bool noPossessions;
        /// <summary>What the kit is worth (its things, what it lays about, its animals and mechs), and the people it brings (the children in its care).</summary>
        public float kitWorth;
        public int kitPeople;

        public string Name => scenario?.name ?? "?";

        public IEnumerable<StartPart> Of(ScenCarry c) => parts.Where(p => p.carry == c);
    }

    /// <summary>A scenario's kit as made when the newcomers are chosen: what comes with them, what it lays about the start, and how they arrive.</summary>
    public sealed class ScenarioKit
    {
        public readonly List<Thing> things = new List<Thing>();
        public readonly List<Thing> near = new List<Thing>();
        public readonly List<Thing> anywhere = new List<Thing>();
        public StartArrival arrival;
        /// <summary>The scenario asked for its gravship, and it isn't offered here (said in the letter).</summary>
        public bool gravshipRefused;
        public string name;
    }

    /// <summary>
    /// A scenario's start for a new colony (docs/design/stage3-many-colonies.md, section 1, "A scenario's start";
    /// release 3, the player's decision 3A). Any loaded scenario (the game's, its DLCs', Rimfolk's seventeen, a mod's, and
    /// the player's own already loaded) can shape the new colony alone: its people are made in the newcomers' window by
    /// its people part's own fields (count and choice, required kinds, xenotypes, mutants, ages and skills; never through
    /// PostIdeoChosen), the parts that shape starting people reach them through their own pawn hooks
    /// (Patch_ScenarioStartShape*), the kit is its own PlayerStartingThings with what it lays about the start, its animals
    /// come, they arrive its way (on foot, by drop pods, or the gravship when the old colony is left behind), and
    /// Rimfolk's own parts are set up on that colony's council, calendar and people once they are there. What changes the
    /// whole game is listed as not carried; Bad Blood's families never come. The window for choosing is
    /// Colonies/ScenarioStartWindow.cs; the founding plan keeps the choice by its key.
    /// </summary>
    public static class ScenarioStarts
    {
        /// <summary>
        /// While the newcomers' window is open with a scenario's start: the scenario whose parts shape the people it makes
        /// (the game's own pawn hooks, routed by Patch_ScenarioStartShape*); null otherwise.
        /// </summary>
        public static Scenario Shaping;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The scenario shaping is still the newcomers' window's: the game in play and its start data set. Anything else lets it go.</summary>
        public static bool ShapingLive
        {
            get
            {
                if (Shaping == null) return false;
                if (Current.ProgramState == ProgramState.Playing && Current.Game?.InitData != null) return true;
                Shaping = null;
                return false;
            }
        }

        /// <summary>The parts that shape starting people can be reached: the deeper game hooks on and all three applied.</summary>
        public static bool HooksOn => RimfolkPatches.On && new[] { nameof(Patch_ScenarioStartShapeNew), nameof(Patch_ScenarioStartShapeDone), nameof(Patch_ScenarioStartShapeAllow) }
            .All(RimfolkPatches.Applied.Contains);

        // ================= The scenarios loaded =================

        public struct Entry
        {
            public string key;
            public Scenario scenario;
            public int from;
        }

        /// <summary>
        /// The scenarios a new colony may start as: every scenario def loaded (the game's, its DLCs', Rimfolk's, other mods'),
        /// this game's own when it is none of those, and the player's own and the workshop's the game already holds (none
        /// read from the disk mid-game).
        /// </summary>
        public static List<Entry> Loaded()
        {
            var list = new List<Entry>();
            var seen = new HashSet<Scenario>();
            void Add(int from, string name, Scenario s)
            {
                if (s == null || !s.valid || from != ScenarioStartMath.ThisGame && !s.showInUI || !seen.Add(s)) return;
                string key = ScenarioStartMath.Key(from, name);
                if (key != null) list.Add(new Entry { key = key, scenario = s, from = from });
            }
            try
            {
                foreach (var d in DefDatabase<ScenarioDef>.AllDefsListForReading.OrderBy(d => d.scenario?.name ?? d.defName)) Add(ScenarioStartMath.FromDef, d.defName, d.scenario);
                var game = Find.Scenario;
                if (game != null && !list.Any(e => e.scenario.name == game.name)) Add(ScenarioStartMath.ThisGame, null, game);
                foreach (var s in ScenarioFiles.AllScenariosLocal) Add(ScenarioStartMath.Local, s?.name, s);
                foreach (var s in ScenarioFiles.AllScenariosWorkshop) Add(ScenarioStartMath.Workshop, s?.name, s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: the scenarios loaded", e);
            }
            return list;
        }

        /// <summary>The scenario a key names, or null when it isn't loaded any more.</summary>
        public static Scenario Resolve(string key)
        {
            if (!ScenarioStartMath.Parse(key, out int from, out string name)) return null;
            try
            {
                switch (from)
                {
                    case ScenarioStartMath.FromDef: return DefDatabase<ScenarioDef>.GetNamedSilentFail(name)?.scenario;
                    case ScenarioStartMath.ThisGame: return Find.Scenario;
                    case ScenarioStartMath.Local: return ScenarioFiles.AllScenariosLocal.FirstOrDefault(s => s != null && s.name == name);
                    case ScenarioStartMath.Workshop: return ScenarioFiles.AllScenariosWorkshop.FirstOrDefault(s => s != null && s.name == name);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: finding the scenario", e);
            }
            return null;
        }

        // ================= What a scenario carries =================

        /// <summary>
        /// The game's own fields the scenario's parts keep to themselves, read by name; made on first use, each on its own,
        /// so one the game renamed costs only what reads it (nothing else in the start, and never the pawn hooks).
        /// </summary>
        private static class F
        {
            internal static readonly AccessTools.FieldRef<ScenPart_ThingCount, ThingDef> ThingDef = Make<ScenPart_ThingCount, ThingDef>("thingDef");
            internal static readonly AccessTools.FieldRef<ScenPart_ThingCount, ThingDef> Stuff = Make<ScenPart_ThingCount, ThingDef>("stuff");
            internal static readonly AccessTools.FieldRef<ScenPart_ThingCount, int> Count = Make<ScenPart_ThingCount, int>("count");
            internal static readonly AccessTools.FieldRef<ScenPart_ThingCount, QualityCategory?> Quality = Make<ScenPart_ThingCount, QualityCategory?>("quality");
            internal static readonly AccessTools.FieldRef<ScenPart_StartingAnimal, PawnKindDef> AnimalKind = Make<ScenPart_StartingAnimal, PawnKindDef>("animalKind");
            internal static readonly AccessTools.FieldRef<ScenPart_StartingAnimal, int> AnimalCount = Make<ScenPart_StartingAnimal, int>("count");
            internal static readonly AccessTools.FieldRef<ScenPart_StartingMech, PawnKindDef> MechKind = Make<ScenPart_StartingMech, PawnKindDef>("mechKind");
            internal static readonly AccessTools.FieldRef<ScenPart_PlayerPawnsArriveMethod, PlayerPawnsArriveMethod> Method =
                Make<ScenPart_PlayerPawnsArriveMethod, PlayerPawnsArriveMethod>("method");
            internal static readonly AccessTools.FieldRef<ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs, PawnKindDef> LeftBehindKind =
                Make<ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs, PawnKindDef>("leftBehindPawnKind");
            internal static readonly AccessTools.FieldRef<ScenPart_PlayerFaction, FactionDef> Faction = Make<ScenPart_PlayerFaction, FactionDef>("factionDef");

            private static AccessTools.FieldRef<T, V> Make<T, V>(string name)
            {
                try
                {
                    return AccessTools.FieldRefAccess<T, V>(name);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: the game's field " + typeof(T).Name + "." + name, e);
                    return null;
                }
            }

            internal static V Get<T, V>(AccessTools.FieldRef<T, V> field, T part) where T : class => field == null || part == null ? default : field(part);
        }

        private static ThingDef ThingDefOfPart(ScenPart_ThingCount p) => F.Get(F.ThingDef, p);
        private static ThingDef StuffOfPart(ScenPart_ThingCount p) => F.Get(F.Stuff, p);
        private static int CountOfPart(ScenPart_ThingCount p) => F.Get(F.Count, p);
        private static QualityCategory? QualityOfPart(ScenPart_ThingCount p) => F.Get(F.Quality, p);
        private static PawnKindDef AnimalKind(ScenPart_StartingAnimal p) => F.Get(F.AnimalKind, p);
        private static int AnimalCount(ScenPart_StartingAnimal p) => F.Get(F.AnimalCount, p);
        private static PawnKindDef MechKind(ScenPart_StartingMech p) => F.Get(F.MechKind, p);
        private static PlayerPawnsArriveMethod Method(ScenPart_PlayerPawnsArriveMethod p) => F.Get(F.Method, p);
        private static PawnKindDef LeftBehindKind(ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs p) => F.Get(F.LeftBehindKind, p);
        private static FactionDef FactionOfPart(ScenPart_PlayerFaction p) => F.Get(F.Faction, p);

        /// <summary>A scenario read for a new colony's start: each part sorted by what it does here, its people, its arrival and what its kit is worth.</summary>
        public static ScenarioStart Read(Scenario s, string key)
        {
            var st = new ScenarioStart { scenario = s, key = key, hooks = HooksOn };
            if (s == null) return st;
            foreach (var part in s.AllParts)
            {
                if (part == null) continue;
                string type = part.GetType().FullName;
                var sp = new StartPart { part = part, carry = ScenarioStartMath.Carry(type, st.hooks), whole = ScenarioStartMath.WholeOf(type, st.hooks) };
                try
                {
                    // The colony stays the people it is: the scenario's own people listed only when it is another kind.
                    if (part is ScenPart_PlayerFaction pf)
                    {
                        var fd = FactionOfPart(pf);
                        sp.carry = ScenarioStartMath.FactionListed(fd == null || fd == Faction.OfPlayer.def) ? ScenCarry.Whole : ScenCarry.Hidden;
                    }
                    if (sp.carry == ScenCarry.People && st.people == null && part is ScenPart_ConfigPage_ConfigureStartingPawnsBase pp)
                    {
                        st.people = pp;
                        st.count = ScenarioStartMath.Count(PeopleCount(pp));
                        st.candidates = ScenarioStartMath.Candidates(st.count, pp.pawnChoiceCount);
                    }
                    if (part is ScenPart_PlayerPawnsArriveMethod am) st.method = (int)Method(am);
                    if (part is ScenPart_NoPossessions) st.noPossessions = true;
                    if (ScenarioStartMath.IsCarried(sp.carry)) st.kitWorth += Worth(part, ref st.kitPeople);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: reading " + type, e);
                }
                st.parts.Add(sp);
            }
            return st;
        }

        /// <summary>How many the people part asks for, as its own TotalPawnCount sums them.</summary>
        private static int PeopleCount(ScenPart_ConfigPage_ConfigureStartingPawnsBase p)
        {
            switch (p)
            {
                case ScenPart_ConfigPage_ConfigureStartingPawns c: return c.pawnCount;
                case ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs k: return k.kindCounts?.Sum(x => x?.count ?? 0) ?? 0;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes x: return x.xenotypeCounts?.Sum(c => c?.count ?? 0) ?? 0;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Mutants m: return m.mutantCounts?.Sum(c => c?.count ?? 0) ?? 0;
            }
            return 3;
        }

        private static float averagePet = -1f, averageMech = -1f;

        /// <summary>What a carried part's kit is worth by the game's market values (a random pet or mech at their kinds' average), and the people it brings.</summary>
        private static float Worth(ScenPart part, ref int people)
        {
            switch (part)
            {
                case ScenPart_ThingCount tc:
                    var def = ThingDefOfPart(tc);
                    if (def == null) return 0f;
                    return def.GetStatValueAbstract(StatDefOf.MarketValue, StuffOfPart(tc)) * Math.Max(0, CountOfPart(tc));
                case ScenPart_StartingAnimal a:
                    var kind = AnimalKind(a);
                    if (averagePet < 0f)
                    {
                        var pets = DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps != null && k.RaceProps.Animal && k.RaceProps.petness > 0f).ToList();
                        averagePet = pets.Count > 0 ? pets.Average(k => k.race.BaseMarketValue) : 100f;
                    }
                    return (kind != null ? kind.race.BaseMarketValue : averagePet) * Math.Max(0, AnimalCount(a));
                case ScenPart_StartingMech m:
                    var mech = MechKind(m);
                    if (averageMech < 0f)
                    {
                        var mechs = DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps != null && k.RaceProps.IsMechanoid && k.race.GetCompProperties<CompProperties_OverseerSubject>() != null).ToList();
                        averageMech = mechs.Count > 0 ? mechs.Average(k => k.race.BaseMarketValue) : 500f;
                    }
                    return mech != null ? mech.race.BaseMarketValue : averageMech;
                case ScenPart_CouncilWards w:
                    if (ModsConfig.BiotechActive) people += Mathf.RoundToInt(w.count.Average);
                    return 0f;
            }
            return 0f;
        }

        // ================= The words =================

        /// <summary>A part's own summary in the game's words, when it has one (kits are listed by their entries instead, which leave the game's summary marks alone).</summary>
        private static string Summary(ScenPart part, Scenario s)
        {
            try
            {
                string text = part.Summary(s);
                if (!text.NullOrEmpty()) return text.Trim().CapitalizeFirst();
            }
            catch (Exception)
            {
                // A part with nothing to say (the age filter over every age).
            }
            return LabelOf(part);
        }

        /// <summary>A part's name: its def's label, else its class's.</summary>
        private static string LabelOf(ScenPart part) => part?.def != null ? part.Label : part?.GetType().Name ?? "?";

        private static string Entries(IEnumerable<ScenPart> parts, string tag)
        {
            var list = new List<string>();
            foreach (var p in parts)
            {
                try
                {
                    list.AddRange(p.GetSummaryListEntries(tag).Where(x => !x.NullOrEmpty()));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: a kit's entries", e);
                }
            }
            return list.Count == 0 ? null : string.Join(", ", list);
        }

        /// <summary>The people as the scenario has them: how many and from how many, what it requires, its ages and skills.</summary>
        private static string PeopleLine(ScenarioStart st)
        {
            if (st.people == null) return null;
            string line = (st.candidates > st.count ? "CC_ScenStart_People_Choice" : "CC_ScenStart_People").Translate(st.count.Named("COUNT"), st.candidates.Named("CHOICE")).Resolve();
            var more = new List<string>();
            switch (st.people)
            {
                case ScenPart_ConfigPage_ConfigureStartingPawns c:
                    if (c.allowedDevelopmentalStages != (DevelopmentalStage.Baby | DevelopmentalStage.Child | DevelopmentalStage.Adult))
                        more.Add("CC_ScenStart_People_Ages".Translate(c.allowedDevelopmentalStages.ToCommaListOr().Named("AGES")).Resolve());
                    if (!c.requiredSkills.NullOrEmpty())
                        more.Add("CC_ScenStart_People_Skills".Translate(string.Join(", ", c.requiredSkills.Where(x => x != null).Select(x => x.skillLabel)).Named("SKILLS")).Resolve());
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs k:
                    more.AddRange(k.kindCounts.Where(x => x?.kindDef != null).Select(x => x.Summary + (x.requiredAtStart ? " " + "CC_ScenStart_Required".Translate().Resolve() : "")));
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes x:
                    more.AddRange(x.xenotypeCounts.Where(c => c?.xenotype != null).Select(c => c.Summary + (c.requiredAtStart ? " " + "CC_ScenStart_Required".Translate().Resolve() : "")));
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Mutants m:
                    more.AddRange(m.mutantCounts.Where(c => c != null).Select(c => c.Summary + (c.requiredAtStart ? " " + "CC_ScenStart_Required".Translate().Resolve() : "")));
                    break;
            }
            return more.Count == 0 ? line : line + ": " + string.Join("; ", more);
        }

        /// <summary>
        /// What comes, what doesn't and what never does, line by line, for the list of scenarios. <paramref name="oldLeftBehind"/>
        /// and <paramref name="partyGoes"/> are the founding as it stands, for the gravship.
        /// </summary>
        public static (List<string> carried, List<string> notCarried, List<string> never) Lines(ScenarioStart st, bool oldLeftBehind, bool partyGoes)
        {
            var carried = new List<string>();
            var not = new List<string>();
            var never = new List<string>();
            if (st?.scenario == null) return (carried, not, never);
            var s = st.scenario;
            string people = PeopleLine(st);
            if (people != null) carried.Add(people);
            foreach (var p in st.Of(ScenCarry.Shaping)) carried.Add(Summary(p.part, s));
            var kitParts = st.Of(ScenCarry.Kit).Select(p => p.part).ToList();
            string kit = Entries(kitParts.Where(p => !(p is ScenPart_ScatterThingsAnywhere)), "PlayerStartsWith");
            if (kit != null) carried.Add("CC_ScenStart_Kit".Translate(kit.Named("LIST")).Resolve());
            string about = Entries(kitParts.Where(p => p is ScenPart_ScatterThingsAnywhere), "MapScatteredWith");
            if (about != null) carried.Add("CC_ScenStart_Scattered".Translate(about.Named("LIST")).Resolve());
            string animals = Entries(st.Of(ScenCarry.Animals).Select(p => p.part), "PlayerStartsWith");
            if (animals != null) carried.Add("CC_ScenStart_Animals".Translate(animals.Named("LIST")).Resolve());
            if (st.Of(ScenCarry.Arrival).Any()) carried.Add(ArrivalLine(st, oldLeftBehind, partyGoes));
            foreach (var p in st.Of(ScenCarry.Rimfolk)) carried.Add(Summary(p.part, s));
            if (st.Of(ScenCarry.Rimfolk).Any(p => p.part is ScenPart_CouncilBeholden)) carried.Add("CC_ScenStart_BeholdenCold".Translate().Resolve());
            foreach (var w in ScenarioStartMath.WholeOrder(st.Of(ScenCarry.Whole).Select(p => p.whole)))
            {
                var parts = st.Of(ScenCarry.Whole).Where(p => p.whole == w).Select(p => p.part).ToList();
                string what = w == ScenWhole.Faction ? FactionOfPart((ScenPart_PlayerFaction)parts[0])?.label ?? "?" : string.Join(", ", parts.Select(p => Summary(p, s)).Distinct().Take(3));
                not.Add(("CC_ScenStart_Whole_" + w).Translate(what.Named("WHAT")).Resolve());
            }
            foreach (var p in st.Of(ScenCarry.Unknown)) not.Add("CC_ScenStart_Unknown".Translate(LabelOf(p.part).Named("LABEL")).Resolve());
            if (st.people == null && st.parts.Any(p => p.part is ScenPart_ConfigPage)) not.Add("CC_ScenStart_PeopleUnknown".Translate().Resolve());
            foreach (var p in st.Of(ScenCarry.Never)) never.Add("CC_ScenStart_Never_Families".Translate().Resolve());
            return (carried, not, never);
        }

        /// <summary>How they arrive, by the scenario's way and the founding as it stands.</summary>
        public static string ArrivalLine(ScenarioStart st, bool oldLeftBehind, bool partyGoes)
        {
            var how = ScenarioStartMath.Arrival(st.method, ModsConfig.OdysseyActive, !partyGoes, oldLeftBehind);
            string line = ("CC_ScenStart_Arrive_" + how).Translate().Resolve();
            if (ScenarioStartMath.GravshipRefused(st.method, ModsConfig.OdysseyActive, !partyGoes, oldLeftBehind)) line += " " + "CC_ScenStart_NoGravship".Translate().Resolve();
            return line;
        }

        /// <summary>The founding window's short line: what comes and what doesn't, in a sentence each, and the kit's part of the passage.</summary>
        public static string Brief(ScenarioStart st, bool oldLeftBehind, bool partyGoes, bool free)
        {
            if (st?.scenario == null) return "";
            var heads = ScenarioStartMath.CarriedOrder(st.parts.Select(p => p.carry)).Select(c => c == ScenCarry.People && st.people != null
                ? "CC_ScenStart_Brief_People".Translate(st.count.Named("COUNT")).Resolve()
                : ("CC_ScenStart_Brief_" + c).Translate().Resolve()).ToList();
            string text = "CC_ScenStart_Brief".Translate(TextUtil.JoinAnd(heads).Named("LIST")).Resolve();
            var wholes = ScenarioStartMath.WholeOrder(st.Of(ScenCarry.Whole).Select(p => p.whole)).Select(w => ("CC_ScenStart_BriefWhole_" + w).Translate().Resolve()).ToList();
            if (st.Of(ScenCarry.Unknown).Any() || st.Of(ScenCarry.Never).Any()) wholes.Add("CC_ScenStart_BriefWhole_Other".Translate().Resolve());
            if (wholes.Count > 0) text += " " + "CC_ScenStart_BriefNot".Translate(TextUtil.JoinAnd(wholes).Named("LIST")).Resolve();
            if (ScenarioStartMath.GravshipRefused(st.method, ModsConfig.OdysseyActive, !partyGoes, oldLeftBehind)) text += " " + "CC_ScenStart_NoGravship".Translate().Resolve();
            int kit = ScenarioStartMath.Passage(Enumerable.Empty<float>(), st.kitWorth, st.kitPeople, free);
            if (kit > 0) text += " " + "CC_ScenStart_KitPrice".Translate(kit.Named("SILVER")).Resolve();
            return text;
        }

        // ================= The people, in the newcomers' window =================

        /// <summary>
        /// The game's start data set as the scenario's people part would set it, read from its fields (never through its
        /// PostIdeoChosen, which would also clear the game's custom xenotypes and reset the default drug policy).
        /// </summary>
        public static void InitData(ScenarioStart st)
        {
            var d = Find.GameInitData;
            if (d == null) return;
            d.playerFaction = Faction.OfPlayer;
            if (st.people == null) return;
            d.startingPawnCount = st.count;
            switch (st.people)
            {
                case ScenPart_ConfigPage_ConfigureStartingPawns c:
                    d.allowedDevelopmentalStages = c.allowedDevelopmentalStages;
                    d.startingSkillsRequired = c.requiredSkills;
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs k:
                    d.startingPawnKind = LeftBehindKind(k);
                    d.startingPawnsRequired = k.kindCounts.Where(c => c != null && c.requiredAtStart).ToList();
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes x:
                    d.startingXenotypesRequired = x.xenotypeCounts.Where(c => c != null && c.requiredAtStart).ToList();
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Mutants m:
                    d.startingMutantsRequired = m.mutantCounts.Where(c => c != null && c.requiredAtStart).ToList();
                    break;
            }
        }

        /// <summary>Each place's request shaped as the people part's own GenerateStartingPawns shapes it: its kind, its xenotype or its mutant.</summary>
        private static List<Func<PawnGenerationRequest, PawnGenerationRequest>> Places(ScenarioStart st)
        {
            var list = new List<Func<PawnGenerationRequest, PawnGenerationRequest>>();
            switch (st.people)
            {
                case ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs k:
                    foreach (var c in k.kindCounts.Where(c => c?.kindDef != null))
                        for (int i = 0; i < c.count; i++)
                            list.Add(r =>
                            {
                                r.KindDef = c.kindDef;
                                if (ModsConfig.BiotechActive) r.ForcedXenotype = c.kindDef.xenotypeSet != null ? null : XenotypeDefOf.Baseliner;
                                return r;
                            });
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes x:
                    foreach (var c in x.xenotypeCounts.Where(c => c != null))
                        for (int i = 0; i < c.count; i++)
                            list.Add(r =>
                            {
                                r.ForcedXenotype = c.xenotype;
                                r.PawnKindDefGetter = c.xenotype == null ? null : (Func<XenotypeDef, PawnKindDef>)(t =>
                                    x.overrideKinds?.FirstOrDefault(o => o?.xenotype == t)?.pawnKind ?? Find.GameInitData.startingPawnKind ?? Faction.OfPlayer.def.basicMemberKind);
                                return r;
                            });
                    break;
                case ScenPart_ConfigPage_ConfigureStartingPawns_Mutants m:
                    foreach (var c in m.mutantCounts.Where(c => c != null))
                        for (int i = 0; i < c.count; i++)
                            list.Add(r =>
                            {
                                r.ForcedMutant = c.mutant;
                                return r;
                            });
                    break;
                default:
                    for (int i = 0; i < st.count; i++) list.Add(r => r);
                    break;
            }
            if (list.Count > st.count) list.RemoveRange(st.count, list.Count - st.count);
            while (list.Count < st.count) list.Add(r => r);
            return list;
        }

        /// <summary>
        /// The scenario's people made as its own part makes them: each place's request shaped, tried again up to twenty
        /// times until the work the colony can't do without is covered (the game's own rule), then the rest to choose from.
        /// </summary>
        public static void Generate(ScenarioStart st)
        {
            var places = Places(st);
            int tries = 0;
            do
            {
                StartingPawnUtility.ClearAllStartingPawns();
                for (int i = 0; i < places.Count; i++)
                {
                    var r = places[i](StartingPawnUtility.GetGenerationRequest(i));
                    StartingPawnUtility.SetGenerationRequest(i, r);
                    StartingPawnUtility.AddNewPawn(i);
                }
                tries++;
            } while (tries <= 20 && !StartingPawnUtility.WorkTypeRequirementsSatisfied());
            while (Find.GameInitData.startingAndOptionalPawns.Count < st.candidates) StartingPawnUtility.AddNewPawn();
            NoPossessions(st);
        }

        /// <summary>A scenario with no possessions: the newcomers bring none.</summary>
        public static void NoPossessions(ScenarioStart st)
        {
            if (st == null || !st.noPossessions || Find.GameInitData == null) return;
            foreach (var l in Find.GameInitData.startingPossessions.Values)
                if (l.Count > 0)
                    l.Clear();
        }

        /// <summary>Why the chosen don't make the scenario's start (the game's own words for its own page's checks), or null.</summary>
        public static string WhyNot(ScenarioStart st, List<Pawn> chosen)
        {
            var d = Find.GameInitData;
            if (st?.people == null || d == null || chosen == null) return null;
            if (ModsConfig.BiotechActive && ScenarioStartMath.AllBabies(chosen.Count(p => p.DevelopmentalStage.Baby()), chosen.Count)) return "ChooseChildOrAdult".Translate();
            if (chosen.Any(p => !ScenarioStartMath.StageAllowed((uint)p.DevelopmentalStage, (uint)d.allowedDevelopmentalStages)))
                return "SelectedCharactersMustBeOfAllowedDevelopmentalStages".Translate(d.allowedDevelopmentalStages.ToCommaListOr());
            if (!d.startingSkillsRequired.NullOrEmpty())
                foreach (var skill in d.startingSkillsRequired.Where(s => s != null))
                    if (!chosen.Any(p => p.skills?.GetSkill(skill) is SkillRecord r && !r.TotallyDisabled))
                        return "SelectedCharacterMustBeCapableOf".Translate(skill.skillLabel);
            if (!d.startingPawnsRequired.NullOrEmpty())
                foreach (var r in d.startingPawnsRequired)
                    if (!ScenarioStartMath.RequiredMet(StartRequired.Kind, r.count, chosen.Count(p => p.kindDef == r.kindDef)))
                        return "SelectedCharactersMustInclude".Translate(r.Summary.Named("SUMMARY"));
            if (!d.startingXenotypesRequired.NullOrEmpty())
                foreach (var r in d.startingXenotypesRequired)
                    if (!ScenarioStartMath.RequiredMet(StartRequired.Xenotype, r.count, chosen.Count(p => p.genes?.Xenotype == r.xenotype && r.allowedDevelopmentalStages.Has(p.DevelopmentalStage))))
                        return "SelectedCharactersMustInclude".Translate(r.Summary.Named("SUMMARY"));
            if (!d.startingMutantsRequired.NullOrEmpty())
                foreach (var r in d.startingMutantsRequired)
                    if (!ScenarioStartMath.RequiredMet(StartRequired.Mutant, r.count, chosen.Count(p => p.IsMutant && p.mutant.Def == r.mutant && r.allowedDevelopmentalStages.Has(p.DevelopmentalStage))))
                        return "SelectedCharactersMustInclude".Translate(r.Summary.Named("SUMMARY"));
            return null;
        }

        /// <summary>
        /// Those left behind are let go before the chosen leave the start data, as the game's window lets its people go:
        /// their own families only when none of the chosen is kin (so a chosen one's parent stays), then they are discarded.
        /// </summary>
        public static void LetGoLeftBehind(List<Pawn> chosen)
        {
            var d = Find.GameInitData;
            if (d == null) return;
            foreach (var p in d.startingAndOptionalPawns.Where(p => p != null && !chosen.Contains(p)).ToList())
            {
                try
                {
                    PawnUtility.TryDestroyStartingColonistFamily(p);
                    p.relations?.ClearAllRelations();
                    PawnComponentsUtility.RemoveComponentsOnDespawned(p);
                    d.startingAndOptionalPawns.Remove(p);
                    d.startingPossessions.Remove(p);
                    Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: letting go those left behind", e);
                }
            }
            d.startingPawnCount = d.startingAndOptionalPawns.Count;
        }

        // ================= The pawn hooks (Patch_ScenarioStartShape*) =================

        private static IEnumerable<ScenPart> ShapingParts(Scenario s) => s.AllParts.Where(p => p != null && ScenarioStartMath.NeedsHooks(p.GetType().FullName));

        public static void NewPawnGenerating(Scenario s, Pawn pawn, PawnGenerationContext context)
        {
            foreach (var p in ShapingParts(s)) p.Notify_NewPawnGenerating(pawn, context);
        }

        public static void PawnGenerated(Scenario s, Pawn pawn, PawnGenerationContext context, bool redressed)
        {
            foreach (var p in ShapingParts(s)) p.Notify_PawnGenerated(pawn, context, redressed);
        }

        public static bool Allow(Scenario s, Pawn pawn, bool tryingToRedress, PawnGenerationRequest req)
        {
            foreach (var p in ShapingParts(s))
                if (!p.AllowPlayerStartingPawn(pawn, tryingToRedress, req))
                    return false;
            return true;
        }

        // ================= The kit, made as they are chosen =================

        /// <summary>
        /// The kit made while the start data still holds the chosen (its animals bond with them and its mechs take an
        /// overseer, as at a new game): the scenario's own PlayerStartingThings, the children in its care, what it lays
        /// about the start; the shaping parts that wait for the map (a hidden hediff) applied now; and, coming down in pods,
        /// the game's own "crashed together".
        /// </summary>
        public static ScenarioKit MakeKit(ScenarioStart st, bool oldLeftBehind, bool mapForThem)
        {
            var kit = new ScenarioKit { name = st.Name };
            kit.arrival = ScenarioStartMath.Arrival(st.method, ModsConfig.OdysseyActive, mapForThem, oldLeftBehind);
            kit.gravshipRefused = ScenarioStartMath.GravshipRefused(st.method, ModsConfig.OdysseyActive, mapForThem, oldLeftBehind);
            foreach (var sp in st.parts)
            {
                try
                {
                    if (sp.part is ScenPart_ScatterThings sc && sp.carry == ScenCarry.Kit) (sc is ScenPart_ScatterThingsAnywhere ? kit.anywhere : kit.near).AddRange(Scattered(sc));
                    else if (sp.carry == ScenCarry.Kit || sp.carry == ScenCarry.Animals || sp.part is ScenPart_CouncilWards && sp.carry == ScenCarry.Rimfolk)
                        kit.things.AddRange(sp.part.PlayerStartingThings().Where(t => t != null));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: the kit of " + sp.Type, e);
                }
            }
            foreach (var sp in st.Of(ScenCarry.Shaping).Where(p => p.part is ScenPart_PawnModifier))
            {
                try
                {
                    sp.part.PostMapGenerate(Find.CurrentMap);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: what waits for the map", e);
                }
            }
            if (kit.arrival == StartArrival.Pods)
            {
                try
                {
                    PawnUtility.GiveAllStartingPlayerPawnsThought(ThoughtDefOf.CrashedTogether);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: crashed together", e);
                }
            }
            return kit;
        }

        /// <summary>What a scatter part lays about the start, made as the game's scatter makes it: its count in stacks, minified when it can be, a quality.</summary>
        private static IEnumerable<Thing> Scattered(ScenPart_ScatterThings sc)
        {
            var def = ThingDefOfPart(sc);
            int count = CountOfPart(sc);
            if (def == null || count <= 0) yield break;
            var stuff = StuffOfPart(sc);
            var quality = QualityOfPart(sc);
            int left = count;
            int guard = 0;
            while (left > 0 && guard++ < 500)
            {
                var thing = ThingMaker.MakeThing(def, stuff);
                if (thing.TryGetComp(out CompQuality q)) q.SetQuality(quality ?? QualityCategory.Normal, ArtGenerationContext.Outsider);
                if (def.IsIngestible && def.ingestible.IsMeal) FoodUtility.GenerateGoodIngredients(thing, Faction.OfPlayer.ideos?.PrimaryIdeo);
                int n = def.category == ThingCategory.Building ? 1 : Math.Min(left, Math.Max(1, def.stackLimit));
                if (def.Minifiable) thing = thing.MakeMinified();
                else thing.stackCount = n;
                left -= n;
                yield return thing;
            }
        }

        // ================= They arrive =================

        /// <summary>
        /// The newcomers arrive the scenario's way, with their possessions on them and its kit: on foot from the edge of the
        /// land (the kit set down where they come in), in drop pods (a pod each, the kit shared, near the middle of a new
        /// colony's land or near the colony's people), or in their gravship on a map made for them alone; what the scenario
        /// lays about comes near where they arrive, or anywhere. A letter tells it as it happened. Then Rimfolk's own parts
        /// wait for them to be there (<see cref="SetUpWhenHere"/>).
        /// </summary>
        public static void Arrive(Map map, List<Pawn> pawns, Dictionary<Pawn, List<ThingDefCount>> possessions, FoundingPlan plan, int price, ScenarioKit kit, bool founding)
        {
            var came = pawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
            if (came.Count == 0) return;
            foreach (var p in came)
            {
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                // By pods or the gravship their possessions come with the kit, as the game's own start brings them.
                if (kit.arrival != StartArrival.Standing && possessions != null && possessions.TryGetValue(p, out var things))
                    foreach (var t in things)
                    {
                        var made = StartingPawnUtility.GenerateStartingPossession(t);
                        if (made != null) kit.things.Add(made);
                    }
            }
            foreach (var t in kit.things)
                if (t.def.CanHaveFaction && t.Faction != Faction.OfPlayer)
                    t.SetFactionDirect(Faction.OfPlayer);
            var how = kit.arrival;
            IntVec3 at = IntVec3.Invalid;
            if (how == StartArrival.Gravship && !(founding && Gravship(map, came, kit, out at)))
            {
                how = StartArrival.Standing;
                kit.gravshipRefused = true;
            }
            try
            {
                if (how == StartArrival.Pods) at = Pods(map, came, kit, founding);
                else if (how == StartArrival.Standing)
                {
                    at = Walk(map, came, kit);
                    // On foot they carry their possessions, as release 1's newcomers do.
                    if (kit.arrival == StartArrival.Standing && possessions != null)
                        foreach (var p in came)
                            if (p.Spawned && possessions.TryGetValue(p, out var things))
                                Foundings.GivePossessions(p, things);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: the arrival", e);
            }
            // Anyone or anything the arrival didn't take stands where they came (never left between maps).
            if (!at.IsValid) at = Foundings.EdgeCell(map);
            foreach (var t in came.Cast<Thing>().Concat(kit.things))
                if (!t.Destroyed && !t.Spawned && t.holdingOwner == null)
                    GenPlace.TryPlaceThing(t, CellFinder.RandomClosewalkCellNear(at, map, 4), map, ThingPlaceMode.Near);
            foreach (var t in kit.near) Scatter(t, map, at, true);
            foreach (var t in kit.anywhere) Scatter(t, map, at, false);
            FloodFillerFog.FloodUnfog(at, map);
            TellArrival(map, came, plan, price, kit, how, founding, at);
            var people = came.Concat(kit.things.OfType<Pawn>().Where(p => p.RaceProps.Humanlike)).Distinct().ToList();
            plan.scenarioPeople = people;
            plan.scenarioArrived = Now;
        }

        /// <summary>On foot from the edge of the land, as release 1's newcomers walk in, the kit set down where they come in.</summary>
        private static IntVec3 Walk(Map map, List<Pawn> came, ScenarioKit kit)
        {
            var cell = Foundings.EdgeCell(map);
            foreach (var p in came.Where(p => !p.Spawned)) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(cell, map, 3), map);
            foreach (var t in kit.things.Where(t => !t.Spawned && t.holdingOwner == null && !t.Destroyed))
            {
                var at = CellFinder.RandomClosewalkCellNear(cell, map, 4);
                if (t is Pawn pawn)
                {
                    GenSpawn.Spawn(pawn, at, map);
                    continue;
                }
                t.SetForbidden(true, false);
                GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            return cell;
        }

        /// <summary>
        /// Drop pods as the game's own crash brings them: a pod for each of them, the kit shared between the pods, opening
        /// as the game's do; near the middle of a new colony's land (the game's start spot for it), or near the colony's
        /// people when they are there first.
        /// </summary>
        private static IntVec3 Pods(Map map, List<Pawn> came, ScenarioKit kit, bool founding)
        {
            var spot = founding ? StartSpot(map) : DropCellFinder.TradeDropSpot(map);
            FloodFillerFog.FloodUnfog(spot, map);
            var groups = came.Select(p => new List<Thing> { p }).ToList();
            for (int i = 0; i < kit.things.Count; i++) groups[i % groups.Count].Add(kit.things[i]);
            // On a new colony's land as the game's crash: slag left, roofs punched; near the colony's people, neither.
            DropPodUtility.DropThingGroupsNear(spot, map, groups, LandingMath.PodOpenDelay, false, founding, founding, true, false);
            return spot;
        }

        /// <summary>The game's start spot of a map just made for them, else open ground near the middle.</summary>
        private static IntVec3 StartSpot(Map map)
        {
            if (MapGenerator.PlayerStartSpotValid && MapGenerator.PlayerStartSpot.InBounds(map) && MapGenerator.PlayerStartSpot.Standable(map)) return MapGenerator.PlayerStartSpot;
            if (RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map) && !c.Roofed(map), map, out var c)) return c;
            return CellFinder.RandomClosewalkCellNear(map.Center, map, 20);
        }

        /// <summary>
        /// The gravship start (Odyssey), as the game's own makes it at a new game: its gravship drawn up, set down where it
        /// fits best near the middle of the land (the ground under it cleared, as the game clears it), the people on its deck,
        /// the kit on its shelves, its tanks full and its engine quiet. False when it can't be (they walk in instead).
        /// </summary>
        private static bool Gravship(Map map, List<Pawn> came, ScenarioKit kit, out IntVec3 at)
        {
            at = IntVec3.Invalid;
            if (!ModsConfig.OdysseyActive || SketchResolverDefOf.Gravship == null) return false;
            try
            {
                var parms = default(SketchResolveParams);
                parms.sketch = new Sketch();
                var sketch = RimWorld.SketchGen.SketchGen.Generate(SketchResolverDefOf.Gravship, parms);
                sketch.Rotate(Rot4.Random);
                var cells = sketch.OccupiedRect.Cells.Select(c => c - sketch.OccupiedCenter).ToHashSet();
                MapGenerator.PlayerStartSpot = IntVec3.Invalid;
                GenStep_ReserveGravshipArea.SetStartSpot(map, cells, new List<CellRect>());
                if (!MapGenerator.PlayerStartSpotValid) return false;
                var spot = MapGenerator.PlayerStartSpot;
                GravshipPlacementUtility.ClearAreaForGravship(map, spot, cells);
                var spawned = new List<Thing>();
                sketch.Spawn(map, spot, Faction.OfPlayer, Sketch.SpawnPosType.OccupiedCenter, Sketch.SpawnMode.Normal, true, true, true, spawned, false, true);
                var rect = sketch.OccupiedRect.MovedBy(spot - sketch.OccupiedCenter);
                bool Deck(IntVec3 c) => c.InBounds(map) && c.Standable(map) && (c.GetTerrain(map)?.IsSubstructure ?? false);
                foreach (var t in came.Cast<Thing>().Concat(kit.things.OfType<Pawn>()))
                    if (rect.Cells.Where(Deck).TryRandomElement(out var cell))
                        GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
                var shelves = spawned.Where(t => t.def == ThingDefOf.Shelf || ThingDefOf.ShelfSmall != null && t.def == ThingDefOf.ShelfSmall).ToList();
                foreach (var t in kit.things.Where(t => !(t is Pawn)))
                {
                    var cell = shelves.Count > 0 ? shelves.RandomElement().OccupiedRect().RandomCell : rect.Cells.Where(Deck).RandomElementWithFallback(spot);
                    t.SetForbidden(true, false);
                    GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
                }
                foreach (var b in spawned)
                {
                    if (b.def == ThingDefOf.Door)
                        foreach (var c in GenAdj.CellsAdjacentCardinal(b))
                            if (c.InBounds(map))
                                FloodFillerFog.FloodUnfog(c, map);
                    if (b.TryGetComp(out CompRefuelable fuel)) fuel.Refuel(fuel.Props.fuelCapacity);
                    if (b is Building_GravEngine engine) engine.silentlyActivate = true;
                }
                if (TerrainDefOf.Substructure != null)
                    foreach (var c in rect)
                        if (c.InBounds(map) && c.GetTerrain(map) == TerrainDefOf.Substructure)
                            map.areaManager.Home[c] = true;
                at = spot;
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: the gravship", e);
                return false;
            }
        }

        /// <summary>A piece the scenario lays about: near where they arrive (the game's 20 cells), or anywhere a not-edge cell takes it; forbidden as the scatter leaves it.</summary>
        private static void Scatter(Thing thing, Map map, IntVec3 at, bool near)
        {
            try
            {
                if (thing.def.category == ThingCategory.Building)
                {
                    var rot = thing.def.rotatable ? Rot4.Random : Rot4.North;
                    bool Fits(IntVec3 c) => GenSpawn.CanSpawnAt(thing.def, c, map, rot, false);
                    bool found = near ? CellFinder.TryFindRandomCellNear(at, map, LandingMath.NearRadius, Fits, out var cell)
                        : CellFinderLoose.TryFindRandomNotEdgeCellWith(5, Fits, map, out cell);
                    if (!found) cell = CellFinder.RandomClosewalkCellNear(at, map, LandingMath.NearRadius);
                    GenSpawn.Spawn(thing, cell, map, rot, WipeMode.VanishOrMoveAside);
                    thing.SetForbidden(true, false);
                    return;
                }
                IntVec3 where;
                if (near) where = CellFinder.RandomClosewalkCellNear(at, map, LandingMath.NearRadius, c => c.Standable(map));
                else if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(5, c => c.Standable(map), map, out where)) where = at;
                thing.SetForbidden(true, false);
                GenPlace.TryPlaceThing(thing, where, map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: what the scenario lays about", e);
            }
        }

        /// <summary>The letter, as it happened: who came, as which scenario has it, how they arrived, the old colony and the passage.</summary>
        private static void TellArrival(Map map, List<Pawn> came, FoundingPlan plan, int price, ScenarioKit kit, StartArrival how, bool founding, IntVec3 at)
        {
            string call = Ages.Form(AgeFeature.Call, Ages.Of(map), Ages.Of(map)).word;
            string text = (founding ? "CC_ScenStart_FoundText" : "CC_ScenStart_ComeText").Translate(Foundings.Names(came).Named("NAMES"), Homes.Name(map).Named("PLACE"), call.Named("CALL"),
                (plan.fromName ?? "?").Named("MOTHER"), (kit.name ?? "?").Named("SCENARIO")).Resolve();
            text += " " + ("CC_ScenStart_Came_" + how).Translate().Resolve();
            if (kit.gravshipRefused && how != StartArrival.Gravship) text += " " + "CC_ScenStart_NoGravshipLetter".Translate().Resolve();
            if (founding) text += " " + Foundings.OldColonyLine(plan, map, plan.seatMoved);
            text += " " + (price > 0 ? "CC_Found_NewcomersPaid".Translate(price.Named("SILVER"), (plan.fromName ?? "?").Named("MOTHER")).Resolve() : "CC_Found_NewcomersFree".Translate().Resolve());
            ColonyCouncil.Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_Found_NewcomersLabel".Translate(Homes.Name(map).Named("PLACE")).Resolve(), text, LetterDefOf.PositiveEvent,
                new LookTargets(new TargetInfo(at, map)));
            History.Add(FoundingMath.KindFounded + ":newcomers", came.Select(p => new HistoryActor(Refs.Of(p), HistoryRole.Did)).ToList(), Refs.Colony(map), HistoryMath.Everyday,
                "CC_ScenStart_History".Translate(Foundings.Names(came).Named("NAMES"), Homes.Name(map).Named("PLACE"), (kit.name ?? "?").Named("SCENARIO")).Resolve());
            CameraJumper.TryJump(new GlobalTargetInfo(at, map));
        }

        // ================= Rimfolk's own parts at the new colony =================

        /// <summary>
        /// In the founding's hour, once the scenario's people are there (out of their pods), or a day after they came with
        /// whoever is: Rimfolk's own parts of the scenario set up on that colony's council, calendar and people, as at a new
        /// game but for that colony alone (the Beholden's grant its tie, without the warm start; Bad Blood's families never),
        /// and one letter says how things stand.
        /// </summary>
        public static void SetUpWhenHere(FoundingPlan plan, Map map)
        {
            if (plan?.scenarioPeople == null || plan.scenarioPeople.Count == 0 || map == null) return;
            var came = plan.scenarioPeople.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
            var here = came.Where(p => p.Spawned && p.Map == map).ToList();
            if (!ScenarioStartMath.SetupDue(here.Count, came.Count, Now - plan.scenarioArrived)) return;
            plan.scenarioPeople.Clear();
            var scen = Resolve(plan.scenario);
            var comp = CouncilGameComponent.Instance;
            var colonists = here.Where(p => p.IsColonist && !p.DevelopmentalStage.Baby()).ToList();
            if (scen == null || comp == null || colonists.Count == 0) return;
            var parts = scen.AllParts.OfType<ScenPart_Council>().Where(p => ScenarioStartMath.Carry(p.GetType().FullName) == ScenCarry.Rimfolk).ToList();
            if (parts.Count == 0) return;
            var ctx = new ScenarioSetupContext { comp = comp, map = map, colonists = colonists, scenario = scen, warmStart = ScenarioStartMath.WarmStart(false) };
            foreach (var part in parts)
            {
                try
                {
                    part.Setup(ctx);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("scenario start: " + part.def?.defName, e);
                }
            }
            if (ctx.lines.Count == 0) return;
            string text = "CC_ScenStart_SetupIntro".Translate(Homes.Name(map).Named("PLACE"), scen.name.Named("SCENARIO")).Resolve() + "\n\n" + string.Join("\n\n", ctx.lines);
            var look = ctx.look.Where(p => p != null && p.Spawned).Distinct().ToList();
            ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_ScenStart_SetupLabel".Translate(Homes.Name(map).Named("PLACE")).Resolve(), text,
                ctx.threat ? LetterDefOf.ThreatSmall : LetterDefOf.NeutralEvent, look.Count > 0 ? new LookTargets(look) : LookTargets.Invalid);
        }
    }
}
