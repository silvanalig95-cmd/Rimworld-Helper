using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What holds a group together, and so what it's called.</summary>
    public enum GroupBinds
    {
        /// <summary>Strong friendships (named after the one it gathers round: "Hector's lot").</summary>
        Friends,
        /// <summary>The same work (<see cref="GroupKindDef.workTypes"/>).</summary>
        Work,
        /// <summary>Working the night hours.</summary>
        NightShift,
        /// <summary>Fighting side by side (comrades).</summary>
        Battle,
        /// <summary>The old.</summary>
        Elders,
        /// <summary>The young.</summary>
        Young,
        /// <summary>The same faith, held to.</summary>
        Faith,
        /// <summary>The colony's first people, who came on its first day.</summary>
        Founders,
        /// <summary>People who came to the colony together, later on.</summary>
        Arrived,
        /// <summary>The same kind of life before (<see cref="GroupKindDef.origin"/>: a backstory's spawn category).</summary>
        Origin,
        /// <summary>Being alike on one side of their character (<see cref="GroupKindDef.axis"/>).</summary>
        Likeness
    }

    /// <summary>
    /// A kind of group: what binds it, what it may be called (picked once, when it forms, and kept), what it does
    /// together (habits) and what ways come with it. Defs/GroupDefs/Rimfolk_GroupKinds.xml.
    /// </summary>
    public class GroupKindDef : Def
    {
        public GroupBinds binds = GroupBinds.Friends;
        /// <summary>Work: the work types (defNames) that bind it; any of them counts.</summary>
        public List<string> workTypes;
        /// <summary>Origin: a backstory spawn category ("Tribal", "Offworld"), counted only where the colony itself isn't of that kind.</summary>
        public string origin;
        /// <summary>Likeness: the side of character they share (a PersonaAxis name), at least <see cref="atLeast"/> on average (at most minus that with <see cref="low"/>).</summary>
        public string axis;
        public float atLeast = 30f;
        public bool low;
        /// <summary>The share of its members the binding must fit.</summary>
        public float minShare = 0.6f;
        /// <summary>
        /// What it may be called: "the hunters", "[lowtech] the spear-carriers", "{ANCHOR}'s lot". Era tags pick among
        /// them as in flavour lines; one is picked when it forms, and kept.
        /// </summary>
        [MustTranslate] public List<string> names;
        /// <summary>What holds it together, as the card says it: "They go out after game together."</summary>
        [MustTranslate] public string why;
        /// <summary>What it does together (GroupHabitDef names), the first that fits the colony first.</summary>
        public List<string> habits;
        /// <summary>Ways that come with it (GroupNormDef names), whatever its people are like.</summary>
        public List<string> norms;
        /// <summary>A one-word tag for lines (groupkind:hunters).</summary>
        public string tag;
        /// <summary>Lower wins when two kinds fit a group equally.</summary>
        public int order = 50;

        [Unsaved] private PersonaAxis? axisParsed;
        [Unsaved] private bool axisLooked;
        [Unsaved] private List<WorkTypeDef> works;
        [Unsaved] private List<GroupHabitDef> habitDefs;
        [Unsaved] private List<GroupNormDef> normDefs;

        public PersonaAxis? Axis
        {
            get
            {
                if (!axisLooked)
                {
                    axisLooked = true;
                    axisParsed = !axis.NullOrEmpty() && Enum.TryParse(axis, out PersonaAxis a) ? a : (PersonaAxis?)null;
                }
                return axisParsed;
            }
        }

        public List<WorkTypeDef> Works => works ??= (workTypes ?? new List<string>()).Select(n => DefDatabase<WorkTypeDef>.GetNamedSilentFail(n)).Where(w => w != null).ToList();

        public List<GroupHabitDef> Habits => habitDefs ??= (habits ?? new List<string>()).Select(n => DefDatabase<GroupHabitDef>.GetNamedSilentFail(n)).Where(h => h != null).ToList();

        public List<GroupNormDef> Norms => normDefs ??= (norms ?? new List<string>()).Select(n => DefDatabase<GroupNormDef>.GetNamedSilentFail(n)).Where(h => h != null).ToList();

        public static List<GroupKindDef> All => DefDatabase<GroupKindDef>.AllDefsListForReading;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (names.NullOrEmpty()) yield return "a group kind with no names";
            if (why.NullOrEmpty()) yield return "no 'why' text";
            if (binds == GroupBinds.Work && workTypes.NullOrEmpty()) yield return "a work group needs workTypes";
            if (binds == GroupBinds.Likeness && (axis.NullOrEmpty() || !Enum.TryParse(axis, out PersonaAxis _))) yield return "a likeness group needs an axis (Warmth, Humour...)";
            if (binds == GroupBinds.Origin && origin.NullOrEmpty()) yield return "an origin group needs an origin (a backstory spawn category)";
            if (binds == GroupBinds.Friends && !names.NullOrEmpty() && !names.All(n => n.Contains("{ANCHOR}"))) yield return "a friends group is named after its anchor: every name needs {ANCHOR}";
            if (!tag.NullOrEmpty() && tag.IndexOfAny(new[] { ',', '|', '[', ']', '!', ' ' }) >= 0) yield return "tag must be one word";
        }
    }

    /// <summary>
    /// A group's way: what its people share and expect of each other ("loud and merry", "keep to themselves",
    /// "suspicious of outsiders"). It holds while the members' character (or their tags) say so; members drift a little
    /// towards it over the weeks, and someone who goes against it gets friction from the rest.
    /// Defs/GroupDefs/Rimfolk_GroupNorms.xml.
    /// </summary>
    public class GroupNormDef : Def
    {
        /// <summary>What it means, for the card's tooltip.</summary>
        [MustTranslate] public string description;
        /// <summary>The side of character it's about (a PersonaAxis name), with the members' average at least <see cref="above"/> or at most <see cref="below"/>.</summary>
        public string axis;
        public float above = 999f;
        public float below = -999f;
        /// <summary>Or: a share of the members fit a tag expression (as in PeopleTags.Matches: "trait:Industriousness|trait:HardWorker").</summary>
        public string tags;
        public float tagShare = 0.5f;
        /// <summary>Only for groups of these kinds (GroupKindDef names); empty: any.</summary>
        public List<string> kinds;
        /// <summary>How far a member is drawn towards it each day (on its axis), the members' conformity.</summary>
        public float conform = 0.3f;
        /// <summary>A member this far the other way on its axis goes against it (friction with the rest).</summary>
        public float breakAt = 999f;
        /// <summary>How it opens (+) or closes (-) the group to newcomers.</summary>
        public float welcome;
        /// <summary>The way it clashes with (a GroupNormDef name): two groups with these two are more easily at odds.</summary>
        public string clashes;
        /// <summary>A one-word tag for lines (groupnorm:merry).</summary>
        public string tag;
        /// <summary>A habit it brings (a GroupHabitDef name): the merry play games of an evening.</summary>
        public string habit;

        [Unsaved] private PersonaAxis? axisParsed;
        [Unsaved] private bool axisLooked;

        public PersonaAxis? Axis
        {
            get
            {
                if (!axisLooked)
                {
                    axisLooked = true;
                    axisParsed = !axis.NullOrEmpty() && Enum.TryParse(axis, out PersonaAxis a) ? a : (PersonaAxis?)null;
                }
                return axisParsed;
            }
        }

        /// <summary>The direction it pulls on its axis: +1 when it's about being high on it, -1 when low.</summary>
        public int Sign => above < 900f ? 1 : -1;

        /// <summary>The line on its axis.</summary>
        public float Line => above < 900f ? above : below;

        public GroupHabitDef Habit => habit.NullOrEmpty() ? null : DefDatabase<GroupHabitDef>.GetNamedSilentFail(habit);

        public static List<GroupNormDef> All => DefDatabase<GroupNormDef>.AllDefsListForReading;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (description.NullOrEmpty()) yield return "no description";
            if (axis.NullOrEmpty() && tags.NullOrEmpty() && kinds.NullOrEmpty()) yield return "a norm needs an axis, tags or kinds";
            if (!axis.NullOrEmpty() && !Enum.TryParse(axis, out PersonaAxis _)) yield return "unknown axis " + axis;
            if (!axis.NullOrEmpty() && above > 900f && below < -900f) yield return "an axis norm needs above or below";
            if (!tag.NullOrEmpty() && tag.IndexOfAny(new[] { ',', '|', '[', ']', '!', ' ' }) >= 0) yield return "tag must be one word";
        }
    }

    /// <summary>
    /// Something a group does together: an evening at the fire, a shared table, a game night, the night hours. When
    /// members are together doing it (Together.Can), they keep it: they feel they belong, their strain eases, one of
    /// them says something, and the group grows closer. Now and then the anchor calls the group to it on the map
    /// (a gathering, with "Answers play out on the map"). Defs/GroupDefs/Rimfolk_GroupHabits.xml.
    /// </summary>
    public class GroupHabitDef : Def
    {
        /// <summary>The hours it's kept (local time; from may be later than to, across midnight).</summary>
        public int hourFrom = 18, hourTo = 23;
        /// <summary>
        /// What they do and where: fire (at leisure by a lit fire of the colony's), table (eating in the same room), rec
        /// (at leisure in a rec room), work (at their group's work, side by side), night (awake in the small hours),
        /// prayer (praying or meditating), outdoors (at leisure under the sky), any (at leisure anywhere).
        /// </summary>
        public string where = "any";
        /// <summary>Work: the work types; empty: the group kind's own.</summary>
        public List<string> workTypes;
        /// <summary>What's said at it (a FlavorPoolDef, lines spoken aloud, without names).</summary>
        public string pool;
        /// <summary>Arranged now and then as a gathering on the map: Bonfire or Gathering (empty: never arranged).</summary>
        public string playKind;
        /// <summary>Where an arranged one is held (PlaySpot names).</summary>
        public List<string> playSpots;
        /// <summary>How much keeping it adds to cohesion, and how much strain it eases.</summary>
        public float cohesion = 2f;
        public float ease = 3f;
        /// <summary>Only for a colony before electricity (a fire at night), or only after.</summary>
        public bool lowtechOnly;
        public bool hightechOnly;

        [Unsaved] private List<WorkTypeDef> works;

        public List<WorkTypeDef> Works => works ??= (workTypes ?? new List<string>()).Select(n => DefDatabase<WorkTypeDef>.GetNamedSilentFail(n)).Where(w => w != null).ToList();

        public bool InHours(int hour) => hourFrom <= hourTo ? hour >= hourFrom && hour <= hourTo : hour >= hourFrom || hour <= hourTo;

        public PlayKind? Play => !playKind.NullOrEmpty() && Enum.TryParse(playKind, out PlayKind k) && (k == PlayKind.Bonfire || k == PlayKind.Gathering) ? k : (PlayKind?)null;

        public List<PlaySpot> Spots =>
            (playSpots ?? new List<string>()).Select(s => Enum.TryParse(s, out PlaySpot p) ? (PlaySpot?)p : null).Where(p => p != null).Select(p => p.Value).ToList();

        public bool FitsEra => (!lowtechOnly || Avail.IsPreIndustrial) && (!hightechOnly || !Avail.IsPreIndustrial);

        private static readonly HashSet<string> Wheres = new HashSet<string> { "fire", "table", "rec", "work", "night", "prayer", "outdoors", "any" };

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!Wheres.Contains(where ?? "")) yield return "where must be one of " + string.Join(", ", Wheres);
            if (pool.NullOrEmpty()) yield return "no pool";
            if (!playKind.NullOrEmpty() && Play == null) yield return "playKind must be Bonfire or Gathering";
            if (hourFrom < 0 || hourFrom > 23 || hourTo < 0 || hourTo > 23) yield return "hours are 0-23";
        }
    }

    /// <summary>
    /// The group side of a moment between two colonists (on an InteractionDef with <see cref="InteractionWorker_GroupMoment"/>):
    /// who it's between, which roles, and what it does to the group afterwards.
    /// </summary>
    public class GroupMomentExtension : DefModExtension
    {
        /// <summary>
        /// sameGroup (two of the same group), rivalGroups (members of two groups at odds), welcome (a member to a newcomer
        /// their group is taking in), closeRanks (a member to a newcomer their group is shutting out), outOfStep (a member
        /// to one of their group who goes against its ways), sponsor (the member who speaks for a newcomer, to them).
        /// </summary>
        public string between = "sameGroup";
        /// <summary>The initiator's (or recipient's) role in that group (anchor, joker, peacemaker, firebrand, worrier, quiet, newcomer).</summary>
        public string initiatorRole;
        public string recipientRole;
        /// <summary>The recipient is low (mood under this, 0-1), for a joke or a word that helps.</summary>
        public float recipientMoodBelow = -1f;
        /// <summary>The initiator's group has a rival group (for the firebrand stirring things up).</summary>
        public bool hasRival;
        /// <summary>The group is at least this close (cohesion).</summary>
        public float minCohesion;
        /// <summary>Afterwards: added to the group's cohesion, to the rivalry, to the newcomer's way in.</summary>
        public float cohesion;
        public float rivalry;
        public float integration;
        /// <summary>Weight factor on top of the moment's own.</summary>
        public float factor = 1f;

        private static readonly HashSet<string> Betweens = new HashSet<string> { "sameGroup", "rivalGroups", "welcome", "closeRanks", "outOfStep", "sponsor" };

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!Betweens.Contains(between ?? "")) yield return "between must be one of " + string.Join(", ", Betweens);
            foreach (var r in new[] { initiatorRole, recipientRole })
                if (!r.NullOrEmpty() && !Enum.TryParse(r, true, out GroupRole _)) yield return "unknown role " + r;
        }
    }

    /// <summary>Rimfolk's group defs looked up by name (they may be missing if a file was removed).</summary>
    public static class GroupDefs
    {
        public static ThoughtDef Habit => DefRefs.Named<ThoughtDef>("Council_Soc_GroupHabit");
        public static ThoughtDef CircleBroke => DefRefs.Named<ThoughtDef>("Council_Grp_CircleBroke");
        public static ThoughtDef OddOneOut => DefRefs.Named<ThoughtDef>("Council_Grp_OutOfStep");
        public static SharedMemoryDef HabitMemory => DefRefs.Named<SharedMemoryDef>("Rimfolk_Mem_Group_Habit");
        public static SharedMemoryDef FoundedMemory => DefRefs.Named<SharedMemoryDef>("Rimfolk_Mem_Group_Founded");
        public static SharedMemoryDef SplitMemory => DefRefs.Named<SharedMemoryDef>("Rimfolk_Mem_Group_Split");
    }
}
