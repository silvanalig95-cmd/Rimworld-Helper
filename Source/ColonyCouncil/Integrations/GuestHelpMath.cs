using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The reason a guest would take up a kind of work every day, strongest first.</summary>
    public enum HelpFit
    {
        None,
        /// <summary>A minor passion for one of the work's skills.</summary>
        Passion,
        /// <summary>A burning passion for one of them.</summary>
        BurningPassion,
        /// <summary>Their people live by it (one of Rimfolk's peoples: herders tend animals, healers the sick).</summary>
        Calling,
        /// <summary>Their faith's precepts speak for it.</summary>
        Faith,
        /// <summary>A hard worker (or industrious): hauling and cleaning.</summary>
        HardWorker
    }

    /// <summary>Why a guest won't take up a kind of work.</summary>
    public enum HelpRefusal
    {
        None,
        /// <summary>Titled in the Empire: no menial work.</summary>
        Titled,
        /// <summary>A notable of high standing among their people (a town's head or heir): no menial work.</summary>
        Notable,
        /// <summary>A proud notable: no menial work.</summary>
        Proud,
        /// <summary>Their people work for hire (the Ashen Banner): nothing every day, though they pitch in in trouble.</summary>
        Hire,
        /// <summary>Their faith forbids it.</summary>
        Faith,
        /// <summary>Never for a guest: prisoners, surgery, the dead, the colony's children, its management.</summary>
        Never
    }

    /// <summary>The colony's troubles a guest pitches in to, as flags.</summary>
    [Flags]
    public enum HelpCrisis
    {
        None = 0,
        /// <summary>A fire in the colony's home area near them.</summary>
        Fire = 1,
        /// <summary>Someone of the colony downed outside a bed.</summary>
        Rescue = 2,
        /// <summary>The colony's wounded waiting to be tended after a raid.</summary>
        Tend = 4,
        /// <summary>The frost harvest: crops marked to bring in before a killing frost.</summary>
        Frost = 8,
        /// <summary>The colony's food about to run out.</summary>
        Food = 16
    }

    /// <summary>Why a guest offers a day of work before leaving.</summary>
    public enum HelpThanks
    {
        None,
        /// <summary>The colony carried them in when they were down (Hospitality's rescued guest).</summary>
        Rescued,
        /// <summary>The colony once set them free (a notable met as "freed").</summary>
        Freed,
        /// <summary>One of the colony's doctors tended them.</summary>
        Tended,
        /// <summary>A feast the colony gave their people while they were here.</summary>
        Feast,
        /// <summary>A stay going very well by Hospitality's own score.</summary>
        Stay
    }

    /// <summary>
    /// Hospitality's guests helping out (Integrations/GuestHelp.cs), free of game types so Tools/Tests can cover it. In
    /// everyday times a guest only takes up the work they love or that fits who they are (a passion, a hard worker's
    /// hauling and cleaning, their people's calling, their faith's precepts), and only narrows Hospitality's own answer.
    /// In real trouble (a fire near them, someone downed, the wounded after a raid, the frost harvest, the food running
    /// out) they pitch in to that work alone, whatever their mood. A guest the colony rescued, freed, healed or hosted
    /// very well offers a day of their best-loved work. The titled, the proud and the high-standing do nothing menial,
    /// each faith and people keeps its customs, and nobody does surgery, prisoner work or the dead's. Everyday work is
    /// capped a day; a day too long is held against the colony a little. Helping counts both ways: friendship beside the
    /// colonists, a good word at home, and regard lost for one of theirs hurt at the colony's work.
    /// </summary>
    public static class GuestHelpMath
    {
        // ================= The work, by the game's work types =================

        /// <summary>The game's work types by defName.</summary>
        public static class Work
        {
            public const string Firefighter = "Firefighter", Patient = "Patient", PatientBedRest = "PatientBedRest", Doctor = "Doctor", BasicWorker = "BasicWorker",
                Warden = "Warden", Handling = "Handling", Cooking = "Cooking", Hunting = "Hunting", Construction = "Construction", Growing = "Growing",
                Mining = "Mining", PlantCutting = "PlantCutting", Smithing = "Smithing", Tailoring = "Tailoring", Art = "Art", Crafting = "Crafting",
                Hauling = "Hauling", Cleaning = "Cleaning", Research = "Research", Childcare = "Childcare", DarkStudy = "DarkStudy", Fishing = "Fishing";
        }

        /// <summary>A guest's own care (being treated, resting in bed): never narrowed, never counted as helping.</summary>
        public static bool OwnCare(string workType) => workType == Work.Patient || workType == Work.PatientBedRest;

        /// <summary>
        /// Work never for a guest: prisoners (Warden), the colony's children (Childcare), the dark study of entities, finishing
        /// off the downed (Allow Tool), the colony's management (Colony Manager), Hospitality's own talks and Therapy's sessions.
        /// </summary>
        public static readonly string[] NeverWork = { Work.Warden, Work.Childcare, Work.DarkStudy, "FinishingOff", "Managing", "Diplomat", "Therapist" };

        /// <summary>The work givers never for a guest (the dead: hauling corpses, stripping them, cremation; entities kept).</summary>
        public static readonly string[] NeverGivers = { "HaulCorpses", "Strip", "DoBillsCremate", "DoctorTendToEntities", "FeedPrisoner" };

        /// <summary>Menial work the titled, the proud and the high-standing never do.</summary>
        public static readonly string[] Menial = { Work.Hauling, Work.Cleaning, Work.PlantCutting, "HaulingUrgent" };

        public static bool Never(string workType) => workType != null && Array.IndexOf(NeverWork, workType) >= 0;

        /// <summary>A work giver never for a guest: one of <see cref="NeverGivers"/>, any that handles graves or bodies, or surgery.</summary>
        public static bool NeverGiver(string giverDefName, bool operation)
        {
            if (operation) return true;
            if (giverDefName == null) return false;
            if (Array.IndexOf(NeverGivers, giverDefName) >= 0) return true;
            foreach (var part in new[] { "Corpse", "Cremat", "Bury", "Burial", "Grave", "Sarcophag" })
                if (giverDefName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ================= Who they are: their people's calling and their faith =================

        /// <summary>
        /// What each of Rimfolk's peoples lives by, as work (Peoples/, docs/design/peoples*.md): the road league and the
        /// enclave carry, the order tends the sick, the herders and the tamers handle animals, the pilgrims keep hostels
        /// (cooking, cleaning), the scholars, the seers and the Helix Works study, the river folk fish and cook, the wardens
        /// hunt, the Quiet Bell sweeps (never the graves: the dead are never a guest's), the Wheel welds and mends, the
        /// Mossborn grow (never fell), the Kiln Guild builds and makes, the linesmen and the Vault mend and build, the union
        /// mines, the Republic's citizens and the Fleet's deckhands carry and clean, the Lattice crafts, the Choir makes art.
        /// The Ashen Banner works for hire only (<see cref="HelpRefusal.Hire"/>).
        /// </summary>
        public static readonly Dictionary<string, string[]> CallingWork = new Dictionary<string, string[]>
        {
            [PeoplesMath.Traders] = new[] { Work.Hauling },
            [PeoplesMath.Healers] = new[] { Work.Doctor },
            [PeoplesMath.Herders] = new[] { Work.Handling },
            [PeoplesMath.Pilgrims] = new[] { Work.Cooking, Work.Cleaning },
            [PeoplesMath.Scholars] = new[] { Work.Research },
            [PeoplesMath.Brokers] = new[] { Work.Hauling },
            [PeoplesMath.Fishers] = new[] { Work.Fishing, Work.Cooking },
            [PeoplesMath.Hunters] = new[] { Work.Hunting },
            [PeoplesMath.Sextons] = new[] { Work.Cleaning },
            [PeoplesMath.Tamers] = new[] { Work.Handling },
            [PeoplesMath.Salvagers] = new[] { Work.Smithing, Work.Crafting },
            [PeoplesMath.Seers] = new[] { Work.Research },
            [PeoplesMath.Grovekeepers] = new[] { Work.Growing },
            [PeoplesMath.Mercenaries] = new string[0],
            [PeoplesMath.Artisans] = new[] { Work.Construction, Work.Smithing, Work.Crafting, Work.Art },
            [PeoplesMath.Linesmen] = new[] { Work.Construction },
            [PeoplesMath.Drillers] = new[] { Work.Mining },
            [PeoplesMath.Citizens] = new[] { Work.Hauling, Work.Cleaning },
            [PeoplesMath.Crew] = new[] { Work.Cleaning, Work.Hauling },
            [PeoplesMath.Editors] = new[] { Work.Research },
            [PeoplesMath.Mechanitors] = new[] { Work.Crafting },
            [PeoplesMath.Listeners] = new[] { Work.Art },
            [PeoplesMath.Vaultfolk] = new[] { Work.Construction }
        };

        /// <summary>
        /// Precepts that speak for a kind of work (the base game's Ideology and the player's meme mods, each an active row
        /// of docs/player-mods): learning prized, mining's yield, trees loved, art desired, dumb labour exalted, crafting
        /// done well by hand, a strong bond with animals.
        /// </summary>
        public static readonly Dictionary<string, string[]> FaithWork = new Dictionary<string, string[]>
        {
            ["Research_Fast"] = new[] { Work.Research },
            ["Research_VeryFast"] = new[] { Work.Research },
            ["MiningYield_High"] = new[] { Work.Mining },
            ["AB_MiningYield_VeryHigh"] = new[] { Work.Mining },
            ["Trees_Desired"] = new[] { Work.Growing },
            ["AM_Art_Desired"] = new[] { Work.Art },
            ["AM_ArtQuality_Expected"] = new[] { Work.Art },
            ["AM_ArtProductionSpeed_Increased"] = new[] { Work.Art },
            ["VME_DumbLabor_Exalted"] = new[] { Work.Hauling, Work.Cleaning },
            ["VME_CraftingQuality_Increased"] = new[] { Work.Crafting },
            ["VME_CraftingSpeed_FasterForManual"] = new[] { Work.Crafting },
            ["AnimalConnection_Strong"] = new[] { Work.Handling }
        };

        /// <summary>Precepts that forbid a kind of work: mining, research at all, dumb labour, killing animals.</summary>
        public static readonly Dictionary<string, string[]> FaithForbids = new Dictionary<string, string[]>
        {
            ["Mining_Prohibited"] = new[] { Work.Mining },
            ["Mining_Horrible"] = new[] { Work.Mining },
            ["Mining_Disapproved"] = new[] { Work.Mining },
            ["Research_None"] = new[] { Work.Research },
            ["VME_DumbLabor_Disliked"] = new[] { Work.Hauling, Work.Cleaning },
            ["VME_DumbLabor_Horrible"] = new[] { Work.Hauling, Work.Cleaning },
            ["AnimalSlaughter_Prohibited"] = new[] { Work.Hunting },
            ["AnimalSlaughter_Horrible"] = new[] { Work.Hunting },
            ["AnimalSlaughter_Disapproved"] = new[] { Work.Hunting },
            ["KillingInnocentAnimals_Abhorrent"] = new[] { Work.Hunting },
            ["KillingInnocentAnimals_Horrible"] = new[] { Work.Hunting },
            ["KillingInnocentAnimals_Disapproved"] = new[] { Work.Hunting }
        };

        /// <summary>Work givers a faith keeps them from: slaughter where killing animals is wrong, butchering where meat is.</summary>
        public static readonly Dictionary<string, string[]> FaithForbidsGiver = new Dictionary<string, string[]>
        {
            ["AnimalSlaughter_Prohibited"] = new[] { "Slaughter" },
            ["AnimalSlaughter_Horrible"] = new[] { "Slaughter" },
            ["AnimalSlaughter_Disapproved"] = new[] { "Slaughter" },
            ["KillingInnocentAnimals_Abhorrent"] = new[] { "Slaughter" },
            ["KillingInnocentAnimals_Horrible"] = new[] { "Slaughter" },
            ["KillingInnocentAnimals_Disapproved"] = new[] { "Slaughter" },
            ["MeatEating_Abhorrent"] = new[] { "DoBillsButcherFlesh" },
            ["MeatEating_Horrible"] = new[] { "DoBillsButcherFlesh" },
            ["MeatEating_Disapproved"] = new[] { "DoBillsButcherFlesh" }
        };

        /// <summary>Precepts against felling trees: such a guest never works on a tree.</summary>
        public static readonly string[] FaithNoTrees = { "TreeCutting_Prohibited", "TreeCutting_Horrible", "TreeCutting_Disapproved" };

        /// <summary>Every precept named here, for the tests' check against the player's mods.</summary>
        public static IEnumerable<string> PreceptsNamed =>
            FaithWork.Keys.Concat(FaithForbids.Keys).Concat(FaithForbidsGiver.Keys).Concat(FaithNoTrees).Distinct();

        /// <summary>The first of their precepts that speaks for this work, or null.</summary>
        public static string FaithFor(string workType, ICollection<string> precepts) => First(FaithWork, workType, precepts);

        /// <summary>The first of their precepts that forbids this work, or null.</summary>
        public static string FaithAgainst(string workType, ICollection<string> precepts) => First(FaithForbids, workType, precepts);

        /// <summary>The first of their precepts that forbids this work giver, or null.</summary>
        public static string FaithAgainstGiver(string giverDefName, ICollection<string> precepts) => First(FaithForbidsGiver, giverDefName, precepts);

        private static string First(Dictionary<string, string[]> table, string what, ICollection<string> precepts)
        {
            if (what == null || precepts == null || precepts.Count == 0) return null;
            foreach (var p in precepts)
                if (p != null && table.TryGetValue(p, out var works) && Array.IndexOf(works, what) >= 0) return p;
            return null;
        }

        /// <summary>They never work on a tree: the Mossborn, who hold that the forest remembers every axe, or a faith against felling.</summary>
        public static bool NoTrees(string calling, ICollection<string> precepts) =>
            calling == PeoplesMath.Grovekeepers || precepts != null && precepts.Any(p => Array.IndexOf(FaithNoTrees, p) >= 0);

        // ================= Work they love =================

        /// <summary>
        /// Why they'd take up this work every day, the strongest reason: a burning passion, their people's calling, their
        /// faith, a passion, a hard worker's hauling and cleaning; none for their own care or work never for a guest.
        /// <paramref name="passion"/>: the highest passion among the work's skills (0 none, 1 minor, 2 burning).
        /// </summary>
        public static HelpFit Fit(string workType, int passion, bool hardWorker, string calling, ICollection<string> precepts)
        {
            if (workType == null || OwnCare(workType) || Never(workType)) return HelpFit.None;
            if (passion >= 2) return HelpFit.BurningPassion;
            if (calling != null && CallingWork.TryGetValue(calling, out var works) && Array.IndexOf(works, workType) >= 0) return HelpFit.Calling;
            if (FaithFor(workType, precepts) != null) return HelpFit.Faith;
            if (passion == 1) return HelpFit.Passion;
            if (hardWorker && (workType == Work.Hauling || workType == Work.Cleaning)) return HelpFit.HardWorker;
            return HelpFit.None;
        }

        /// <summary>The proud stand on it from this pride on (the persona's Pride axis; the same line as Diplomacy/NatureMath.Threshold).</summary>
        public const float ProudAt = NatureMath.Threshold;

        /// <summary>
        /// Why they won't take up this work, or None: never for a guest; the titled, the high-standing and the proud never
        /// do menial work; their faith; and every day, the Banner's own (it works for hire).
        /// </summary>
        public static HelpRefusal Refuse(string workType, bool titled, bool highStanding, bool proud, string calling, ICollection<string> precepts, bool everyday)
        {
            if (Never(workType)) return HelpRefusal.Never;
            bool menial = workType != null && Array.IndexOf(Menial, workType) >= 0;
            if (menial && titled) return HelpRefusal.Titled;
            if (menial && highStanding) return HelpRefusal.Notable;
            if (menial && proud) return HelpRefusal.Proud;
            if (FaithAgainst(workType, precepts) != null) return HelpRefusal.Faith;
            if (everyday && calling == PeoplesMath.Mercenaries) return HelpRefusal.Hire;
            return HelpRefusal.None;
        }

        /// <summary>A work giver they won't take: never for a guest (the dead, surgery, entities), else their faith's.</summary>
        public static HelpRefusal RefuseGiver(string giverDefName, bool operation, ICollection<string> precepts)
        {
            if (NeverGiver(giverDefName, operation)) return HelpRefusal.Never;
            return FaithAgainstGiver(giverDefName, precepts) != null ? HelpRefusal.Faith : HelpRefusal.None;
        }

        /// <summary>The titled, the high-standing and the proud only pitch in to save a life.</summary>
        public static bool LifeOnly(bool titled, bool highStanding, bool proud) => titled || highStanding || proud;

        // ================= The colony in trouble =================

        /// <summary>A fire this near them (cells) is one to help with.</summary>
        public const float FireNear = 20f;

        /// <summary>The Medicine it takes to tend the colony's wounded (Hospitality's own minimum aside), and the Cooking to cook for it.</summary>
        public const int TendMedicine = 4, CookSkill = 4;

        /// <summary>How long after a raid its wounded are a crisis: a day.</summary>
        public const int TendWindowTicks = 60000;

        /// <summary>The food's days (Lean/MapFacts.FoodDays) under which the colony is running out, and over which it no longer is.</summary>
        public const float FoodStartDays = 3f, FoodEndDays = 4f;

        /// <summary>Troubles where a life is at stake: the titled pitch in to these, and the overworked keep at them.</summary>
        public const HelpCrisis LifeSaving = HelpCrisis.Fire | HelpCrisis.Rescue | HelpCrisis.Tend;

        /// <summary>Troubles a guest helps with even when Hospitality's own helping is switched off.</summary>
        public const HelpCrisis WithHospitalityOff = HelpCrisis.Fire | HelpCrisis.Rescue;

        public static readonly HelpCrisis[] Crises = { HelpCrisis.Fire, HelpCrisis.Rescue, HelpCrisis.Tend, HelpCrisis.Frost, HelpCrisis.Food };

        /// <summary>A fire in the colony's home area: a crisis for a guest this close to it.</summary>
        public static bool FireIsNear(float distance) => distance >= 0f && distance <= FireNear;

        /// <summary>Someone of the colony is downed outside a bed: they need carrying in.</summary>
        public static bool RescueNeeded(int downedOutOfBed) => downedOutOfBed > 0;

        /// <summary>
        /// The colony's wounded after a raid: no enemy about any more, the raid's threat within a day, and someone of the
        /// colony still needing tending. Over when they're all tended, or a day on.
        /// </summary>
        public static bool TendNeeded(bool hostilesNow, int ticksSinceThreat, int needTending) =>
            !hostilesNow && ticksSinceThreat >= 0 && ticksSinceThreat <= TendWindowTicks && needTending > 0;

        /// <summary>The frost harvest is on while crops it marked still stand.</summary>
        public static bool FrostHarvest(int markedStanding) => markedStanding > 0;

        /// <summary>The food is about to run out: under three days starts it, and it lasts until there are four again.</summary>
        public static bool FoodShort(float foodDays, bool wasShort, int eaters) =>
            eaters > 0 && (wasShort ? foodDays < FoodEndDays : foodDays < FoodStartDays);

        /// <summary>
        /// The troubles this guest pitches in to: a fire only near them, the wounded only with Medicine enough, the food only
        /// with Cooking enough, the harvest only if they can work plants; the titled, the proud and the high-standing, and
        /// anyone overworked today, only to save a life; with Hospitality's helping switched off, only fires and rescues.
        /// </summary>
        public static HelpCrisis ForGuest(HelpCrisis onMap, bool fireNear, bool canTend, bool canCook, bool canHarvest, bool lifeOnly, bool overworked,
            bool hospitalityWorkOff)
        {
            var c = HelpCrisis.None;
            if ((onMap & HelpCrisis.Fire) != 0 && fireNear) c |= HelpCrisis.Fire;
            if ((onMap & HelpCrisis.Rescue) != 0) c |= HelpCrisis.Rescue;
            if ((onMap & HelpCrisis.Tend) != 0 && canTend) c |= HelpCrisis.Tend;
            if ((onMap & HelpCrisis.Frost) != 0 && canHarvest) c |= HelpCrisis.Frost;
            if ((onMap & HelpCrisis.Food) != 0 && canCook) c |= HelpCrisis.Food;
            if (lifeOnly || overworked) c &= LifeSaving;
            if (hospitalityWorkOff) c &= WithHospitalityOff;
            return c;
        }

        /// <summary>The tending givers of the colony's wounded (never their own, an animal's or an entity's).</summary>
        public static readonly string[] TendGivers = { "DoctorTendToHumanlikes", "DoctorTendEmergency" };

        /// <summary>The frost harvest's givers: the field's ripe crops, and the crops it marked to harvest.</summary>
        public static readonly string[] FrostGivers = { "GrowerHarvest", "PlantsCut" };

        /// <summary>Cooking's givers that make drink, not food: never the food crisis's.</summary>
        public static bool Brewing(string giverDefName) =>
            giverDefName != null && (giverDefName.IndexOf("Brew", StringComparison.OrdinalIgnoreCase) >= 0 || giverDefName.IndexOf("Ferment", StringComparison.OrdinalIgnoreCase) >= 0
                                     || giverDefName.IndexOf("Beer", StringComparison.OrdinalIgnoreCase) >= 0 || giverDefName.StartsWith("VBE_", StringComparison.Ordinal));

        /// <summary>
        /// The trouble a work giver answers: firefighting the fire; carrying the downed (the game's rescue) the rescue;
        /// tending the colony's people the wounded; the field's harvest and the marked crops the frost; cooking (never
        /// brewing) the food. None for any other.
        /// </summary>
        public static HelpCrisis CrisisOf(string workType, string giverDefName, string giverClass)
        {
            if (workType == Work.Firefighter) return HelpCrisis.Fire;
            if (giverClass == "WorkGiver_RescueDowned" || giverDefName == "DoctorRescue") return HelpCrisis.Rescue;
            if (giverDefName != null && Array.IndexOf(TendGivers, giverDefName) >= 0) return HelpCrisis.Tend;
            if (giverDefName != null && Array.IndexOf(FrostGivers, giverDefName) >= 0) return HelpCrisis.Frost;
            if (workType == Work.Cooking && !Brewing(giverDefName)) return HelpCrisis.Food;
            return HelpCrisis.None;
        }

        // ================= The verdict on each work giver =================

        /// <summary>What the postfix on JobGiver_Work.PawnCanUseWorkGiver knows of a guest and one of their work givers.</summary>
        public struct Ask
        {
            /// <summary>Hospitality's own answer (its skill minimum, its exclusions and its wish to help).</summary>
            public bool hospitality;
            /// <summary>"Guests help with what they love" (and the deeper game hooks, with Hospitality).</summary>
            public bool everydayOn;
            /// <summary>"Guests pitch in when the colony is in trouble".</summary>
            public bool pitchInOn;
            /// <summary>The guest tab's "Let them help".</summary>
            public bool letHelp;
            /// <summary>Their own care (Patient work).</summary>
            public bool ownCare;
            /// <summary>Why they'd refuse this giver or its work, None for nothing.</summary>
            public HelpRefusal refused;
            /// <summary>A reason they love this work.</summary>
            public bool fits;
            /// <summary>Their everyday hours today leave room.</summary>
            public bool capOpen;
            /// <summary>Overworked today.</summary>
            public bool overworked;
            /// <summary>The troubles they pitch in to now (<see cref="ForGuest"/>).</summary>
            public HelpCrisis inCrisis;
            /// <summary>The trouble this giver answers (<see cref="CrisisOf"/>).</summary>
            public HelpCrisis giverCrisis;
            /// <summary>They can do the giver at all (the game's own capacity and work checks; the trouble's skill).</summary>
            public bool crisisCan;
            /// <summary>Their thanks day runs, and this is its work.</summary>
            public bool thanksWork;
            /// <summary>Hospitality's own rules allow it, all but its roll (its skill minimum and exclusions kept).</summary>
            public bool thanksCan;
        }

        /// <summary>
        /// Whether a guest may use a work giver. Their own care is Hospitality's answer, untouched. With "Let them help" off,
        /// nothing. Work never for a guest, never. In trouble they pitch in to, only that trouble's work, whatever
        /// Hospitality's roll said (their skill as the trouble asks). On their thanks day, its work beyond the cap and
        /// Hospitality's roll (its minimum and exclusions kept). Every other time it only narrows Hospitality's answer: work
        /// they love, nothing they'd refuse, within the day's hours, and not when overworked.
        /// </summary>
        public static bool Allow(Ask a)
        {
            if (!a.everydayOn && !a.pitchInOn) return a.hospitality;
            if (a.ownCare) return a.hospitality;
            if (!a.letHelp) return false;
            if (a.refused == HelpRefusal.Never) return false;
            if (a.pitchInOn && a.inCrisis != HelpCrisis.None)
            {
                if ((a.giverCrisis & a.inCrisis) == HelpCrisis.None) return false;
                // Their customs hold in trouble too, unless a life is at stake; the Banner pitches in like anyone.
                if (a.refused != HelpRefusal.None && a.refused != HelpRefusal.Hire && (a.giverCrisis & LifeSaving) == HelpCrisis.None) return false;
                return a.crisisCan;
            }
            if (!a.everydayOn) return a.hospitality;
            if (a.thanksWork && a.refused == HelpRefusal.None && !a.overworked) return a.hospitality || a.thanksCan;
            return a.hospitality && a.fits && a.refused == HelpRefusal.None && a.capOpen && !a.overworked;
        }

        /// <summary>
        /// Hospitality's "wants to help" (its mood line) for a guest: open in trouble they pitch in to and on their thanks
        /// day, whatever their mood; otherwise Hospitality's own answer.
        /// </summary>
        public static bool Gate(bool hospitality, bool everydayOn, bool pitchInOn, bool letHelp, HelpCrisis inCrisis, bool thanksDay)
        {
            if (!letHelp) return hospitality;
            if (pitchInOn && inCrisis != HelpCrisis.None) return true;
            if (everydayOn && thanksDay) return true;
            return hospitality;
        }

        /// <summary>Rimfolk's own step in the guest's day, before their needs: in trouble they pitch in to, unless starving or dropping.</summary>
        public static bool PitchingIn(bool pitchInOn, bool letHelp, HelpCrisis inCrisis, bool starving, bool exhausted) =>
            pitchInOn && letHelp && inCrisis != HelpCrisis.None && !starving && !exhausted;

        // ================= The day's limits =================

        public const int HourTicks = 2500;

        /// <summary>About three hours of everyday work a day (trouble's work excepted).</summary>
        public const int CapHours = 3;

        public const int CapTicks = CapHours * HourTicks;

        /// <summary>All their work in a day past this (twice the cap: trouble on top of their share) is overworking them.</summary>
        public const int OverworkTicks = 2 * CapTicks;

        /// <summary>Hospitality's own line for helping: a mood over 75% (its Duties_Visit.xml). Under it they stop everyday work.</summary>
        public const float HappyEnough = 0.75f;

        /// <summary>Their everyday hours today leave room.</summary>
        public static bool CapOpen(int everydayTicks) => everydayTicks < CapTicks;

        /// <summary>Their day's work, everyday and trouble's together, has gone past what a guest should give.</summary>
        public static bool Overworked(int everydayTicks, int crisisTicks) => everydayTicks + crisisTicks >= OverworkTicks;

        /// <summary>Hours to show, to the half hour, rounded down.</summary>
        public static float Hours(int ticks) => ticks <= 0 ? 0f : (float)Math.Floor(ticks * 2.0 / HourTicks) / 2f;

        /// <summary>Unhappy: under Hospitality's line, an everyday job is stopped.</summary>
        public static bool TooUnhappy(float mood) => mood < HappyEnough;

        // ================= Thanks paid in work =================

        /// <summary>A visit going this well by Hospitality's own score is hosting them really well.</summary>
        public const float HostedWell = 0.9f;

        /// <summary>A day's work in thanks: one in-game day.</summary>
        public const int ThanksTicks = 60000;

        /// <summary>
        /// Why they'd offer a day of work: carried in when they were down, set free by the colony once, tended by its
        /// doctors, feasted, or a stay going very well; the first that holds.
        /// </summary>
        public static HelpThanks Thanks(bool rescued, bool freed, bool tended, bool feasted, float visitScore)
        {
            if (rescued) return HelpThanks.Rescued;
            if (freed) return HelpThanks.Freed;
            if (tended) return HelpThanks.Tended;
            if (feasted) return HelpThanks.Feast;
            if (visitScore >= HostedWell) return HelpThanks.Stay;
            return HelpThanks.None;
        }

        /// <summary>They offer it in the morning (6 to 11), so the day's work is a day.</summary>
        public static bool ThanksHour(int hour) => hour >= 6 && hour < 11;

        /// <summary>Their best-loved work: the highest passion, then the best skill, then by name. Null for none.</summary>
        public static string BestLoved(IEnumerable<(string work, int passion, int skill)> candidates)
        {
            if (candidates == null) return null;
            string best = null;
            int bp = -1, bs = -1;
            foreach (var c in candidates)
            {
                if (c.work == null) continue;
                if (c.passion > bp || c.passion == bp && (c.skill > bs || c.skill == bs && string.CompareOrdinal(c.work, best) < 0))
                {
                    best = c.work;
                    bp = c.passion;
                    bs = c.skill;
                }
            }
            return best;
        }

        // ================= It counts both ways =================

        /// <summary>Two working this close at the same work are working side by side.</summary>
        public const float SideBySideCells = 6f;

        public static bool SideBySide(float distance, string workA, string workB) => distance >= 0f && distance <= SideBySideCells && workA != null && workA == workB;

        /// <summary>The good word a guest who helped takes home: a little for a few hours, more for long help, a trouble or a thanks day.</summary>
        public static float SpeakWell(int everydayTicks, int crisesHelped, bool thanksGiven)
        {
            float r = 0f;
            if (everydayTicks >= HourTicks) r += 1f;
            if (everydayTicks >= 4 * HourTicks) r += 1f;
            if (crisesHelped > 0) r += 2f;
            if (thanksGiven) r += 1f;
            return r > MaxSpeakWell ? MaxSpeakWell : r;
        }

        public const float MaxSpeakWell = 4f;

        /// <summary>What one of theirs hurt at the colony's work costs with each of their people's notables, and overworking a notable guest.</summary>
        public const float HurtRegard = 2f, OverworkRegard = 2f;

        /// <summary>At most this many of their people's notables hear of it.</summary>
        public const int MaxNotablesTold = 6;

        /// <summary>An injury this new (ticks) found at a pass came at the work.</summary>
        public const int NewInjuryTicks = 300;

        /// <summary>Rooms a guest never goes into for work: a bedroom or barracks, a prison cell.</summary>
        public static bool PrivateRoom(string roleDefName) =>
            roleDefName == "Bedroom" || roleDefName == "Barracks" || roleDefName == "PrisonCell" || roleDefName == "PrisonBarracks";

        // ================= What's kept =================

        /// <summary>Guests kept at once (a few parties), and the kinds of work each has said why they took up.</summary>
        public const int MaxGuests = 30, MaxTold = 12;

        /// <summary>The work types turned on for a guest's trouble at most (one per trouble).</summary>
        public const int MaxEnabled = 5;
    }
}
