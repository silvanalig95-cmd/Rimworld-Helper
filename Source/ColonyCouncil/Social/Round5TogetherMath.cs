using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the fifth content round's moments between the colony's people (docs/content-round-5-together.md),
    /// without game types: the scenes a social event may name, who counts as an old hand, a newcomer or one of the old,
    /// which memories of a death a child asks about, how near someone must be for a quirk's word, and how a shared memory's
    /// label says when it was. Checked in Tools/Tests/Round5TogetherTests.cs.
    /// </summary>
    public static class Round5TogetherMath
    {
        /// <summary>The scenes a social event may name (SocialEventDef.scene): two sowing, two old hands, a game, a child's loss.</summary>
        public const string Sowing = "sowing", OldHands = "oldhands", Game = "game", ChildGrief = "childgrief";

        public static readonly string[] Scenes = { Sowing, OldHands, Game, ChildGrief };

        public static bool KnownScene(string scene) => !string.IsNullOrEmpty(scene) && Array.IndexOf(Scenes, scene) >= 0;

        /// <summary>An old hand has lived at the colony a year (sixty days) or more, by the game's own record of time as a colonist.</summary>
        public const float OldHandDays = 60f;

        public static bool OldHand(float daysHere) => daysHere >= OldHandDays;

        /// <summary>A newcomer is in their first ten days at the colony, as the moment's "newcomer" tag counts it (Flavor.TagsFor).</summary>
        public const float NewcomerDays = 10f;

        public static bool Newcomer(float daysHere) => daysHere >= 0f && daysHere < NewcomerDays;

        /// <summary>One of the colony's old: 55 or more in human years, as the elders' other moments count them.</summary>
        public const float ElderAge = 55f;

        public static bool Elder(float age) => age >= ElderAge;

        /// <summary>
        /// The game's memories of a death that a child asks about (the dead is the memory's other pawn): someone of the colony,
        /// someone they thought well of, someone close, and Vanilla Social Interactions Expanded's best friend (by name: it
        /// simply never comes without the mod). Not an animal's death, which is a different question.
        /// </summary>
        public static readonly string[] GriefThoughts =
        {
            "KnowColonistDied", "PawnWithGoodOpinionDied", "WitnessedDeathFamily", "MySonDied", "MyDaughterDied", "MyHusbandDied",
            "MyWifeDied", "MyFianceDied", "MyFianceeDied", "MyLoverDied", "MyBrotherDied", "MySisterDied", "MyFatherDied",
            "MyMotherDied", "VSIE_MyBestFriendDied"
        };

        public static bool ChildGriefThought(string thought) => !string.IsNullOrEmpty(thought) && Array.IndexOf(GriefThoughts, thought) >= 0;

        /// <summary>A child asks about a death within this many days of it.</summary>
        public const float GriefDays = 5f;

        /// <summary>Whether a memory <paramref name="ageTicks"/> old is recent enough to be asked about.</summary>
        public static bool GriefFresh(int ageTicks, int ticksPerDay = 60000) => ageTicks >= 0 && ageTicks <= GriefDays * ticksPerDay;

        /// <summary>A quirk's word over the head waits for the newcomer, the child or the old one to be this near and able to hear it (cells).</summary>
        public const float NearCells = 8f;

        /// <summary>Two playing at the same game: the same building (its thingIDNumber), a real one.</summary>
        public static bool SameGame(int a, int b) => a > 0 && a == b;

        /// <summary>
        /// A shared memory's label with when it was filled in: {YEAR} the year and {SEASON} the season by the map's calendar
        /// ("the spring sowing of {YEAR}"). The event's own words ({THING}, {X_nameDef}) are filled in after this.
        /// </summary>
        public static string Label(string format, int year, string season)
        {
            if (string.IsNullOrEmpty(format)) return format ?? "";
            return format.Replace("{YEAR}", year.ToString()).Replace("{SEASON}", season ?? "");
        }
    }
}
