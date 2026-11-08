using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The post's words: who carries a letter, how a letter is likely to leave, how to reach a town now, and the lines said over a head.</summary>
    public static class PostWords
    {
        private const string SayPool = "Rimfolk_Pool_PostSays";

        public static string P(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        private static string People(Faction f) => f?.Name ?? P("CC_Mail_SomePeople");

        /// <summary>"the traders of the Wheel", "a camp of the road league breaking up", "the post rider of Emberford's people".</summary>
        public static string CarrierName(PostCarrier kind, Faction f, Pawn p)
        {
            if (kind == PostCarrier.OwnCaravan || kind == PostCarrier.OwnRider || kind == PostCarrier.Bearer)
                return p != null ? p.LabelShort : P("CC_Mail_Carrier_" + kind, "".Named("PAWN"));
            return P("CC_Mail_Carrier_" + kind, People(f).Named("PEOPLE"), (p?.LabelShort ?? "").Named("PAWN"));
        }

        /// <summary>The post's words by its age: a rider, the mail run, the shuttle.</summary>
        public static string PostName(WorldAge age) => P("CC_Mail_Name_" + (age >= WorldAge.Spacer ? "Spacer" : age >= WorldAge.Industrial ? "Industrial" : "Medieval"));

        public static string PostComing(WorldAge age, Faction f) =>
            P("CC_Mail_Msg_Coming_" + (age >= WorldAge.Spacer ? "Spacer" : age >= WorldAge.Industrial ? "Industrial" : "Medieval"), People(f).Named("PEOPLE"));

        // ---------------- In the mailbag ----------------

        /// <summary>
        /// How a waiting letter is likely to leave, for the mailbag's pane and the message as it goes in: its own rider; a
        /// go-between (a people at war); out of reach (only someone going there); the next post; traders or visitors on the map
        /// going that way; else whoever goes that way next (relayed when far).
        /// </summary>
        public static string Likely(NeighbourLetter l)
        {
            try
            {
                if (l == null) return "";
                if (l.ownRider) return P("CC_Mail_Likely_OwnRider", (l.rider?.LabelShort ?? "?").Named("PAWN"));
                if (l.packed) return P("CC_Mail_Likely_Packed", (l.packedBy?.LabelShort ?? "?").Named("PAWN"));
                var town = Outbox.TownOf(l, out var n);
                var map = l.map;
                if (town == null || map == null) return P("CC_Mail_Likely_Anyone");
                if (NotableBook.Hostile(n)) return P("CC_Mail_Likely_War");
                var info = PostReach.Of(map, town, true);
                string dir = NeighbourLetters.Direction(map.Tile, (info.band == ReachBand.Far && info.relay != null ? info.relay : town).Tile);
                if (info.band == ReachBand.Cut) return P("CC_Mail_Likely_Cut", town.Name.Named("TOWN"));
                var going = Going(map, l);
                if (going != null) return P("CC_Mail_Likely_Here", going.Named("CARRIER"));
                if (PostRider.Runs(map) && PostMath.PostServes(Ages.Of(n.faction), false)) return P("CC_Mail_Likely_Post", PostName(PostRider.AgeOf(map)).Named("POST"));
                if (info.band == ReachBand.Far && info.relay != null) return P("CC_Mail_Likely_Relay", dir.Named("DIR"), info.relay.Name.Named("RELAY"));
                return P("CC_Mail_Likely_Way", dir.Named("DIR"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: likely", e);
                return P("CC_Mail_Likely_Anyone");
            }
        }

        /// <summary>A group on the map now (not leaving yet) that would take it when they go: their own people's, or traders camped.</summary>
        private static string Going(Map map, NeighbourLetter l)
        {
            var town = Outbox.TownOf(l, out var n);
            if (town == null || n?.faction == null) return null;
            foreach (var lord in map.lordManager.lords)
            {
                if (lord?.faction == null || lord.faction.IsPlayer || lord.faction.HostileTo(Faction.OfPlayer) || lord.LordJob is LordJob_PostRider) continue;
                var p = lord.ownedPawns.FirstOrDefault(MailWatch.Able);
                if (p == null || Outsiders.Leaving(p)) continue;
                if (lord.faction == n.faction)
                    return CarrierName(PostMath.CarrierOf(lord.LordJob?.GetType().Name), lord.faction, p);
            }
            return null;
        }

        /// <summary>One line for each letter in a home's outbox: to whom, what for, written by, how it's likely to leave.</summary>
        public static List<string> OutboxLines(Map map)
        {
            var lines = new List<string>();
            foreach (var l in Outbox.Waiting(map, true))
                lines.Add(P(l.spoken ? "CC_Mail_Line_Spoken" : "CC_Mail_Line", (l.toName ?? "?").Named("TO"), ("CC_Letter_Purpose_" + l.purpose).Translate().Resolve().Named("PURPOSE"),
                    l.WriterName.Named("PAWN"), Likely(l).Named("LIKELY")));
            return lines;
        }

        // ---------------- Where a letter is ----------------

        /// <summary>Where a letter of the post is, in words (null: not one of the post's stages; the letters page says the rest).</summary>
        public static string Status(NeighbourLetter l, Notable n)
        {
            switch (l.stage)
            {
                case LetterStage.Outbox:
                    return P(l.ownRider ? "CC_Mail_Status_RiderReady" : l.packed ? "CC_Mail_Status_Packed" : l.spoken ? "CC_Mail_Status_Spoken" : "CC_Mail_Status_Outbox",
                        l.WriterName.Named("PAWN"), (n?.Name ?? "?").Named("NAME"), (l.rider?.LabelShort ?? l.packedBy?.LabelShort ?? "?").Named("RIDER"));
                case LetterStage.Riding:
                    return P("CC_Mail_Status_Riding", (l.rider?.LabelShort ?? "?").Named("RIDER"), (n?.Name ?? "?").Named("NAME"));
                case LetterStage.Travelling:
                    if (l.carrier == PostCarrier.None || l.carrierName.NullOrEmpty()) return null;
                    return P(l.relayName.NullOrEmpty() ? "CC_Mail_Status_Carried" : "CC_Mail_Status_CarriedRelay", l.WriterName.Named("PAWN"), (n?.Name ?? "?").Named("NAME"),
                        l.carrierName.Named("CARRIER"), (l.relayName ?? "").Named("RELAY"));
                case LetterStage.Answering:
                    if (l.ownRider) return P("CC_Mail_Status_RiderBack", (l.rider?.LabelShort ?? "?").Named("RIDER"), (n?.Name ?? "?").Named("NAME"));
                    if (l.postId >= 0) return P("CC_Mail_Status_AnswerPost", (n?.Name ?? "?").Named("NAME"));
                    return null;
            }
            return null;
        }

        // ---------------- How to reach them now ----------------

        public static string DaysWords(float d) => d <= 1.5f ? P("CC_Mail_Days_One") : P("CC_Mail_Days_Many", PostMath.RoughDays(d).Named("DAYS"));

        /// <summary>
        /// How to reach a town now and roughly how soon, in plain words (a town's card, the letter's dialog): by radio; by
        /// someone of theirs at the colony; a go-between for a people at war; within reach by the post, with their people on
        /// the map or whoever goes that way, about so many days on the road; far, by relay; out of reach; and the colony's own
        /// rider, about so many days there.
        /// </summary>
        public static List<string> ReachLines(Map home, Settlement town)
        {
            if (home == null || town?.Faction == null || !PostBook.On) return new List<string>();
            long key = ((long)home.uniqueID << 32) ^ (uint)town.ID;
            float t = UnityEngine.Time.realtimeSinceStartup;
            if (reachLines.TryGetValue(key, out var kept) && t - kept.at < 2f && t >= kept.at) return kept.lines;
            var lines = ReachLinesNow(home, town);
            if (reachLines.Count > 200) reachLines.Clear();
            reachLines[key] = (lines, t);
            return lines;
        }

        private static readonly Dictionary<long, (List<string> lines, float at)> reachLines = new Dictionary<long, (List<string>, float)>();

        private static List<string> ReachLinesNow(Map home, Settlement town)
        {
            var lines = new List<string>();
            try
            {
                var f = town.Faction;
                var info = PostReach.Of(home, town, true);
                bool hostile = f.HostileTo(Faction.OfPlayer);
                var bearer = home.mapPawns.AllHumanlikeSpawned.FirstOrDefault(p => Outsiders.IsOutsider(p) && Outsiders.PeopleOf(p) == f && !Outsiders.Leaving(p));
                bool camped = !hostile && home.lordManager.lords.Any(lord => lord?.faction == f && !(lord.LordJob is LordJob_PostRider)
                                                                              && lord.ownedPawns.Any(p => MailWatch.Able(p) && !Outsiders.Leaving(p)));
                var band = info.call ? ReachBand.Radio : Ages.Ours >= WorldAge.Spacer ? ReachBand.Radio : info.band;
                var lead = PostMath.Lead(band, hostile, bearer != null && !info.call, camped, PostRider.Runs(home) && PostMath.PostServes(Ages.Of(f), hostile));
                string dir = NeighbourLetters.Direction(home.Tile, town.Tile);
                string days = info.days >= 0f ? DaysWords(info.days) : "";
                switch (lead)
                {
                    case PostMath.ReachBy.Radio:
                        lines.Add(P(info.call ? "CC_Mail_Reach_Radio" : "CC_Mail_Reach_Shuttle", days.Named("DAYS")));
                        break;
                    case PostMath.ReachBy.Bearer:
                        lines.Add(P("CC_Mail_Reach_Bearer", bearer.Named("BEARER")));
                        break;
                    case PostMath.ReachBy.GoBetween:
                        lines.Add(P("CC_Mail_Reach_War"));
                        break;
                    case PostMath.ReachBy.OnlyGoing:
                        lines.Add(P("CC_Mail_Reach_Cut", town.Name.Named("TOWN")));
                        break;
                    case PostMath.ReachBy.Camped:
                        lines.Add(P("CC_Mail_Reach_Camped", People(f).Named("PEOPLE"), days.Named("DAYS")));
                        break;
                    case PostMath.ReachBy.Relay:
                        lines.Add(P("CC_Mail_Reach_Relay", (info.relay?.Name ?? "?").Named("RELAY"), DaysWords(info.relayDays).Named("DAYS")));
                        break;
                    case PostMath.ReachBy.Post:
                        lines.Add(P("CC_Mail_Reach_Post", PostName(PostRider.AgeOf(home)).Named("POST"), days.Named("DAYS")));
                        break;
                    default:
                        lines.Add(P("CC_Mail_Reach_Way", dir.Named("DIR"), days.Named("DAYS")));
                        break;
                }
                if (info.radio && !info.call && lead != PostMath.ReachBy.Radio) lines.Add(P("CC_Mail_Reach_RadioLater"));
                if (!hostile && lead != PostMath.ReachBy.Radio && OwnRiders.Candidates(home).Count > 0 && Find.WorldReachability.CanReach(home.Tile, town.Tile))
                {
                    bool mount = OwnRiders.MountFor(home, null) != null;
                    float rd = PostReach.RiderDays(home, town, mount);
                    if (rd >= 0f) lines.Add(P(mount ? "CC_Mail_Reach_Rider" : "CC_Mail_Reach_RiderFoot", DaysWords(rd).Named("DAYS")));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the reach line", e);
            }
            return lines;
        }

        // ---------------- Speech ----------------

        /// <summary>A line over someone's head at a moment of the post (one sentence, from the pool Rimfolk_Pool_PostSays).</summary>
        public static void Say(Pawn p, string moment, NeighbourLetter l)
        {
            if (p == null || !p.Spawned || moment.NullOrEmpty()) return;
            try
            {
                var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(SayPool);
                if (pool == null) return;
                var tags = Flavor.TagsFor(p);
                tags.Add(moment);
                var lines = pool.lines.For(ToneUtility.ToneFor(p));
                string line = Flavor.PickTagged(lines.Where(x => LineTags.Of(x).tags?.Contains(moment) ?? false), tags)
                              ?? Flavor.PickTagged(pool.lines.plain.Where(x => LineTags.Of(x).tags?.Contains(moment) ?? false), tags);
                if (line.NullOrEmpty()) return;
                var n = l != null ? NotableBook.Instance?.Get(l.notable) : null;
                line = Texts.Fill(line, ("NAME", n?.Name ?? ""), ("TOWN", n?.townName ?? ""));
                Speech.Say(p, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post words", e);
            }
        }
    }
}
