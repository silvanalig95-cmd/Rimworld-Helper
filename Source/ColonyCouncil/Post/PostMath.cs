using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Who carried a letter away, or brought one in.</summary>
    public enum PostCarrier
    {
        None,
        /// <summary>The regular post: a rider, a mail run, a shuttle (Post/PostRider.cs).</summary>
        PostRider,
        /// <summary>A trade caravan.</summary>
        Traders,
        /// <summary>Visitors and guests (the game's, Hospitality's).</summary>
        Visitors,
        /// <summary>A camp breaking up (Camps/), or arriving.</summary>
        Camp,
        /// <summary>Pilgrims on their road.</summary>
        Pilgrims,
        /// <summary>Travellers passing, wanderers, refugees, anyone else on the road.</summary>
        Travellers,
        /// <summary>A people's own messengers and porters (Couriers/).</summary>
        Messengers,
        /// <summary>The colony's own caravan setting out.</summary>
        OwnCaravan,
        /// <summary>The colony's own rider, straight there (Post/OwnRiders.cs).</summary>
        OwnRider,
        /// <summary>Someone of their own people at the colony (Outsiders/), by hand.</summary>
        Bearer
    }

    /// <summary>How soon a letter to the colony must come, if nobody is coming its way: a messenger is sent after the wait for its kind.</summary>
    public enum PostUrgency
    {
        /// <summary>Kind words, news, a town's letter, an envoy's or a ward's: they can wait for the next carrier.</summary>
        Ordinary,
        /// <summary>An answer to business (a debt called in, a pact, a match): a few days at most.</summary>
        Business,
        /// <summary>Help, a warning heeded, a truce, a ransom: half a day.</summary>
        Urgent
    }

    /// <summary>How often the regular post comes (the setting).</summary>
    public enum PostPace
    {
        Seldom,
        Usual,
        Often
    }

    /// <summary>Why a place is on a carrier's way, or not.</summary>
    public enum WayWhy
    {
        No,
        /// <summary>One of their own people's towns: they go home.</summary>
        Theirs,
        /// <summary>A town along the road to where they are going.</summary>
        Road,
        /// <summary>In the direction they leave in (a generous cone).</summary>
        Cone
    }

    /// <summary>How far a town is from the colony for letters, until radio (the player's diplomatic range, 8 November 2026).</summary>
    public enum ReachBand
    {
        /// <summary>A call on the comms console reaches them, or shuttles do: no range.</summary>
        Radio,
        /// <summary>Within range: the post, passing carriers and the colony's rider reach.</summary>
        Near,
        /// <summary>Beyond range: by a relay through a nearer people's town, or a long ride.</summary>
        Far,
        /// <summary>Out of reach: too far, or cut off by the land. Letters wait for someone really going there.</summary>
        Cut
    }

    /// <summary>A town a letter could be relayed through: days from the colony, days on to the town, on good terms.</summary>
    public struct RelayOption
    {
        public int id;
        public float toRelay;
        public float onward;
        public bool goodTerms;
    }

    /// <summary>
    /// The post (docs/design/post-and-letters.md, step 1): letters wait in a mailbag at the gate and travel with whoever is
    /// going their way; a regular post by the colony's age; the colony's own rider; mail coming in with whoever comes, or a
    /// messenger once the wait would be unreasonable; and the diplomatic range until radio, by travel time over the land.
    /// No game types, so Tools/Tests can check it (PostTests).
    /// </summary>
    public static class PostMath
    {
        public const int TicksPerDay = 60000;

        // ---------------- On their way ----------------

        /// <summary>Degrees either side of the way a carrier leaves in that still count as their way: a generous cone.</summary>
        public const float ConeHalf = 60f;

        /// <summary>Degrees either side of the road to where they're going that a town on it may lie.</summary>
        public const float RoadHalf = 35f;

        /// <summary>A town this close to where they're going (world tiles) is on their road, whatever the angle.</summary>
        public const float NearDestination = 8f;

        /// <summary>How much further than their destination a town on the road may lie (tiles).</summary>
        public const float RoadSlack = 6f;

        /// <summary>The angle between two headings in degrees (0 to 180).</summary>
        public static float Angle(float a, float b)
        {
            float d = ((a - b) % 360f + 540f) % 360f - 180f;
            return Math.Abs(d);
        }

        /// <summary>A heading in degrees from a step east and north: 0 north, 90 east, 180 south, 270 west (the world's own).</summary>
        public static float Heading(float east, float north)
        {
            if (Math.Abs(east) < 1e-6f && Math.Abs(north) < 1e-6f) return 0f;
            double deg = Math.Atan2(east, north) * 180.0 / Math.PI;
            return (float)((deg + 360.0) % 360.0);
        }

        /// <summary>
        /// Whether a letter's town is on a carrier's way: one of their own people's towns (they go home); a town along the road
        /// to where they're going (their destination known: near it, or no further and within <see cref="RoadHalf"/> of the
        /// way there); else in the direction they leave in (the map's edge, within <see cref="ConeHalf"/>), or toward their
        /// destination. A town at the colony's own door (under two tiles) is on everyone's way.
        /// </summary>
        public static WayWhy OnTheirWay(bool theirs, float townHeading, float townTiles, bool knowDest, float destHeading, float destTiles, bool knowEdge,
            float edgeHeading)
        {
            if (theirs) return WayWhy.Theirs;
            if (townTiles < 2f) return WayWhy.Cone;
            if (knowDest)
            {
                float angle = Angle(townHeading, destHeading);
                // How far the town lies from where they're going (law of cosines on the flat, near enough over a region).
                double rad = angle * Math.PI / 180.0;
                double apart = Math.Sqrt(Math.Max(0.0, townTiles * townTiles + destTiles * destTiles - 2.0 * townTiles * destTiles * Math.Cos(rad)));
                if (apart <= NearDestination) return WayWhy.Road;
                if (angle <= RoadHalf && townTiles <= destTiles + RoadSlack) return WayWhy.Road;
            }
            if (knowEdge && Angle(townHeading, edgeHeading) <= ConeHalf) return WayWhy.Cone;
            if (knowDest && Angle(townHeading, destHeading) <= ConeHalf) return WayWhy.Cone;
            return WayWhy.No;
        }

        /// <summary>Who a group of outsiders is, by the name of their group's job (the game's, Hospitality's, Rimfolk's): traders, visitors, a camp...</summary>
        public static PostCarrier CarrierOf(string lordJob)
        {
            string n = lordJob ?? "";
            if (n.IndexOf("PostRider", StringComparison.Ordinal) >= 0) return PostCarrier.PostRider;
            if (n.IndexOf("Trade", StringComparison.Ordinal) >= 0 || n.IndexOf("Caravan", StringComparison.Ordinal) >= 0 || n.IndexOf("Market", StringComparison.Ordinal) >= 0)
                return PostCarrier.Traders;
            if (n.IndexOf("Camp", StringComparison.Ordinal) >= 0) return PostCarrier.Camp;
            if (n.IndexOf("Pilgrim", StringComparison.Ordinal) >= 0) return PostCarrier.Pilgrims;
            if (n.IndexOf("Courier", StringComparison.Ordinal) >= 0 || n.IndexOf("Envoy", StringComparison.Ordinal) >= 0 || n.IndexOf("Messenger", StringComparison.Ordinal) >= 0)
                return PostCarrier.Messengers;
            if (n.IndexOf("Visit", StringComparison.Ordinal) >= 0 || n.IndexOf("Guest", StringComparison.Ordinal) >= 0) return PostCarrier.Visitors;
            return PostCarrier.Travellers;
        }

        /// <summary>
        /// Whether a group by its job's name goes about the road at all: not a raid, a war band or a siege, not a gathering,
        /// a ceremony, a meeting or an election of the colony's, not a stray beast.
        /// </summary>
        public static bool CarriesMail(string lordJob)
        {
            string n = lordJob ?? "";
            foreach (var no in new[] { "Raid", "War", "Assault", "Siege", "Kidnap", "Steal", "Sapper", "Gathering", "Meeting", "Election", "PlayOut", "Stray", "Ritual", "Ceremony",
                         "Defend", "Hunt" })
                if (n.IndexOf(no, StringComparison.Ordinal) >= 0)
                    return false;
            return true;
        }

        /// <summary>A carrier that walks to the mailbag on the map (not the colony's own caravan or rider, nor someone of theirs here: they're handed it).</summary>
        public static bool Outsider(PostCarrier c) => c != PostCarrier.None && c != PostCarrier.OwnCaravan && c != PostCarrier.OwnRider && c != PostCarrier.Bearer;

        // ---------------- The carrier's means ----------------

        /// <summary>
        /// How a carrier travels: a people of the spacer age by shuttle, of the industrial age by vehicle on the roads, anyone
        /// with a mount mounted, else on foot (the runner's pace, a caravan's walk).
        /// </summary>
        public static LetterMeans MeansOf(WorldAge theirs, bool mounted)
        {
            if (theirs == WorldAge.Spacer) return LetterMeans.Shuttle;
            if (theirs == WorldAge.Industrial) return LetterMeans.Courier;
            return mounted || theirs == WorldAge.Medieval ? LetterMeans.Rider : LetterMeans.Runner;
        }

        /// <summary>
        /// The share of a walking caravan's time over the same road a carrier takes: on foot, lighter than a caravan, 0.8;
        /// mounted 0.5; a vehicle on the roads 0.3. A shuttle goes over the land, not along it (<see cref="ShuttleDays"/>).
        /// </summary>
        public static float MeansFactor(LetterMeans m)
        {
            switch (m)
            {
                case LetterMeans.Runner: return 0.8f;
                case LetterMeans.Rider: return 0.5f;
                case LetterMeans.Courier: return 0.3f;
                default: return 0.15f;
            }
        }

        /// <summary>A shuttle: half a day to start, then the straight distance at 0.015 days a tile, at most two days.</summary>
        public static float ShuttleDays(float tiles) => Math.Min(2f, LetterMath.LegStartDays + Math.Max(0f, tiles) * 0.015f);

        /// <summary>
        /// Days a leg takes by the land's own travel time (<paramref name="walkDays"/>: a walking caravan's days over the
        /// world's path, hills, swamps, rivers, roads and the season all counted; -1 when there is no path): half a day to
        /// start, then the walk at the means' share. A shuttle by the straight distance. -1: no way by land.
        /// </summary>
        public static float LegDays(float walkDays, float tiles, LetterMeans m)
        {
            if (m == LetterMeans.Shuttle) return ShuttleDays(tiles);
            if (walkDays < 0f) return -1f;
            return LetterMath.LegStartDays + walkDays * MeansFactor(m);
        }

        // ---------------- The land's cost ----------------

        /// <summary>
        /// A walking caravan's days over one tile, the model of the game's own cost (Caravan_PathFollower.CostToMove, used
        /// when the game's path can't be read): the land's movement difficulty (flat 1, hills 2, mountains 3, swamp and ice
        /// more) times a road's factor (a dirt road 0.5, a stone road 0.3: under 1) and a river's ease along it, times the
        /// season's (winter and snow on the ground up to 2.5), at 3300 ticks a tile of the caravan's walk and twelve hours on
        /// the road a day. Impassable land (difficulty 1000 or more, or none at all): -1.
        /// </summary>
        public static float TileDays(float difficulty, float roadFactor, float riverFactor, float seasonFactor)
        {
            if (difficulty <= 0f || difficulty >= 1000f) return -1f;
            float road = roadFactor > 0f ? Math.Min(1f, roadFactor) : 1f;
            float river = riverFactor > 0f ? Math.Min(1f, riverFactor) : 1f;
            float season = seasonFactor > 0f ? Math.Max(1f, seasonFactor) : 1f;
            float ticks = 3300f * difficulty * road * river * season;
            return ticks / (TicksPerDay * 0.5f);
        }

        /// <summary>The days over a route of tiles (each <see cref="TileDays"/>); -1 when any of it can't be crossed.</summary>
        public static float RouteDays(IList<float> tileDays)
        {
            if (tileDays == null) return -1f;
            float sum = 0f;
            foreach (float d in tileDays)
            {
                if (d < 0f) return -1f;
                sum += d;
            }
            return sum;
        }

        // ---------------- The range, until radio ----------------

        /// <summary>
        /// How far letters reach in days on the road by the colony's age (its means): five by runners, seven by riders, nine by
        /// vehicles on the roads; a spacer colony's shuttles have no range (-1). Mounts, roads and vehicles widen it by making
        /// the same towns fewer days off; the Signal Line's masts by half again.
        /// </summary>
        public static float RangeDays(WorldAge ours, bool mast)
        {
            float d;
            switch (ours)
            {
                case WorldAge.Tribal: d = 5f; break;
                case WorldAge.Medieval: d = 7f; break;
                case WorldAge.Industrial: d = 9f; break;
                default: return -1f;
            }
            return mast ? d * 1.5f : d;
        }

        /// <summary>The colony's own means for the range: shuttles in the spacer age, vehicles in the industrial, riders in the medieval or with a mount, else runners.</summary>
        public static LetterMeans ColonyMeans(WorldAge ours, bool mount) => MeansOf(ours, mount);

        /// <summary>A relay hands a letter on after this long at their town.</summary>
        public const float RelayDays = 1f;

        /// <summary>
        /// Where a town lies for the colony's letters: radio (the console reaches them, or the colony's shuttles do); within
        /// range by the land's days (<paramref name="days"/>: -1, no way by land); beyond it, reached only by a relay
        /// (<paramref name="relay"/> found); else out of reach.
        /// </summary>
        public static ReachBand Band(bool radio, float days, float range, bool relay)
        {
            if (radio || range < 0f) return ReachBand.Radio;
            if (days >= 0f && days <= range) return ReachBand.Near;
            return relay ? ReachBand.Far : ReachBand.Cut;
        }

        /// <summary>
        /// The best town to relay a letter through, or -1: on good terms with the colony, within range of it, its own way on
        /// to the letter's town within the same range, the fewest days in all (there, a day at the relay, and on).
        /// </summary>
        public static int Relay(IList<RelayOption> options, float range, out float days)
        {
            days = -1f;
            int best = -1;
            if (options == null || range < 0f) return -1;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (!o.goodTerms || o.toRelay < 0f || o.onward < 0f || o.toRelay > range || o.onward > range) continue;
                float total = o.toRelay + RelayDays + o.onward;
                if (best < 0 || total < days)
                {
                    best = i;
                    days = total;
                }
            }
            return best;
        }

        /// <summary>The radio reaches them: a people of the industrial age or later, or a town within reach of the Signal Line's masts.</summary>
        public static bool RadioReaches(int theirTech, float mastTiles) => theirTech >= 4 || mastTiles >= 0f && mastTiles <= MastReach;

        /// <summary>How far a Signal Line mast carries (world tiles).</summary>
        public const float MastReach = 30f;

        // ---------------- The regular post ----------------

        /// <summary>The post's age: what the colony and the best of its region's peoples share (a tribal colony takes no part in a post).</summary>
        public static WorldAge PostAge(WorldAge colony, WorldAge bestNeighbour) => colony < bestNeighbour ? colony : bestNeighbour;

        /// <summary>A regular post at all: from the medieval age on.</summary>
        public static bool HasPost(WorldAge postAge) => postAge >= WorldAge.Medieval;

        /// <summary>The post's days apart by its age: a rider on the road every 7 to 14 days, a mail run every 3 to 7, a shuttle every 2 to 4.</summary>
        public static void PostDays(WorldAge postAge, out float min, out float max)
        {
            switch (postAge)
            {
                case WorldAge.Medieval:
                    min = 7f;
                    max = 14f;
                    return;
                case WorldAge.Industrial:
                    min = 3f;
                    max = 7f;
                    return;
                case WorldAge.Spacer:
                    min = 2f;
                    max = 4f;
                    return;
                default:
                    min = max = -1f;
                    return;
            }
        }

        /// <summary>The post's pace (the setting): seldom half as long again between visits, often six tenths.</summary>
        public static float PaceFactor(PostPace p) => p == PostPace.Seldom ? 1.5f : p == PostPace.Often ? 0.6f : 1f;

        /// <summary>Days until the next post comes; -1 when there is no post in this age.</summary>
        public static float NextPostDays(WorldAge postAge, PostPace pace, float roll)
        {
            PostDays(postAge, out float min, out float max);
            if (min < 0f) return -1f;
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            return (min + (max - min) * roll) * PaceFactor(pace);
        }

        /// <summary>The post takes letters to a people that is on it: at peace with the colony and of the medieval age or later (a tribe's camps are off the road).</summary>
        public static bool PostServes(WorldAge theirs, bool hostile) => !hostile && theirs >= WorldAge.Medieval;

        /// <summary>The post comes by day (7:00 to 18:00), as couriers do.</summary>
        public static bool ByDay(int hour) => hour >= 7 && hour < 18;

        // ---------------- Mail for the colony ----------------

        /// <summary>
        /// How long a letter for the colony waits for someone coming its way before a messenger of the writer's people brings
        /// it: urgent half a day, business three days, ordinary letters eight (with a regular post, ordinary ones wait for it
        /// a little longer, twelve, since it will come).
        /// </summary>
        public static float MessengerWaitDays(PostUrgency u, bool post)
        {
            switch (u)
            {
                case PostUrgency.Urgent: return 0.5f;
                case PostUrgency.Business: return 3f;
                default: return post ? 12f : 8f;
            }
        }

        /// <summary>An answer's urgency by what the letter was for: help, a warning, a truce or a ransom urgent; business; else ordinary.</summary>
        public static PostUrgency UrgencyOf(LetterPurpose p)
        {
            switch (p)
            {
                case LetterPurpose.Help:
                case LetterPurpose.Warning:
                case LetterPurpose.Truce:
                case LetterPurpose.Ransom:
                    return PostUrgency.Urgent;
                case LetterPurpose.Greetings:
                case LetterPurpose.Condolences:
                case LetterPurpose.News:
                case LetterPurpose.Invitation:
                    return PostUrgency.Ordinary;
                default:
                    return PostUrgency.Business;
            }
        }

        /// <summary>Incoming mail a carrier coming in brings: from places on their way (as for letters out), or all of it by the regular post.</summary>
        public static bool Brings(PostCarrier c, WayWhy way, bool served) => c == PostCarrier.PostRider ? served : way != WayWhy.No;

        // ---------------- The outbox ----------------

        /// <summary>Letters open at once (in the outbox, on the road, answers to come): the outbox replaces the old three under way.</summary>
        public const int MaxOpen = 12;

        /// <summary>Mail for the colony kept waiting for a carrier at once; past it the oldest is brought by a messenger now.</summary>
        public const int MaxIncoming = 16;

        /// <summary>A tribal colony gives a spoken message to the carrier rather than writing.</summary>
        public static bool Spoken(WorldAge colony) => colony == WorldAge.Tribal;

        /// <summary>Who may write: grown, free, able to read and write by the game's rules (its books); a spoken message wants only a voice.</summary>
        public static bool CanWrite(bool grown, bool free, bool reads, bool talks, bool spoken) => grown && free && (spoken ? talks : reads);

        /// <summary>How long a carrier waits at the gate for a colonist to give them a spoken message (ticks): an hour and a half.</summary>
        public const int SpokenWaitTicks = 3750;

        // ---------------- How to reach them, in words ----------------

        /// <summary>"about a day", "about two days": whole days, at least one, for the reach line (never a countdown).</summary>
        public static int RoughDays(float days)
        {
            if (days <= 0f) return 1;
            return Math.Max(1, (int)Math.Round(days, MidpointRounding.AwayFromZero));
        }

        /// <summary>What the reach line leads with, best first: by radio; by someone of theirs here; by the post; with carriers on the map going that way; by relay; by whoever goes that way next; or (out of reach) only someone really going there.</summary>
        public enum ReachBy
        {
            Radio,
            Bearer,
            Post,
            Camped,
            Relay,
            Travellers,
            GoBetween,
            OnlyGoing
        }

        /// <summary>The reach line's lead (pure): radio first, then someone of theirs at the colony, then the band and who is coming.</summary>
        public static ReachBy Lead(ReachBand band, bool hostile, bool bearerHere, bool campedGoing, bool postServes)
        {
            if (band == ReachBand.Radio) return ReachBy.Radio;
            if (bearerHere) return ReachBy.Bearer;
            if (hostile) return ReachBy.GoBetween;
            if (band == ReachBand.Cut) return ReachBy.OnlyGoing;
            if (campedGoing) return ReachBy.Camped;
            if (band == ReachBand.Far) return ReachBy.Relay;
            return postServes ? ReachBy.Post : ReachBy.Travellers;
        }
    }
}
