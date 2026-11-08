using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Mail for the colony (docs/design/post-and-letters.md, step 1, point 5): an answer to one of its letters, or one of
    /// the world's letters carried to it (a town's letter, the colony's envoy's or ward's from abroad), comes with whoever is
    /// coming its way: the post rider, a trader or visitor walking in, a camp arriving (Post/MailWatch.cs). The carrier sets
    /// the bag down at the mailbag, and the letter opens then. When nobody comes for longer than is reasonable for its kind
    /// (PostMath.MessengerWaitDays: urgent half a day, business three days, ordinary letters eight, twelve with a post), a
    /// messenger of the writer's people brings it (Couriers.MailAnswer, as before). What an answer sends in goods still comes
    /// with its messenger: those answers never wait here.
    /// </summary>
    public static class MailIn
    {
        private static int Now => Find.TickManager.TicksGame;

        private static PostBook Book => PostBook.Instance;

        /// <summary>The courier trip number of mail brought at last by a messenger that is no letter of the colony's own (as TownsAfter.MailId).</summary>
        public const int MailId = (1 << 30) + 7;

        /// <summary>
        /// A letter for the colony, in the region from <paramref name="arrive"/>: it waits for a carrier coming its way. Its
        /// number in the post, or -1 when the post is off (or no home): the caller tells it as it always did.
        /// </summary>
        public static int Add(Map map, Faction from, Settlement town, string label, string text, LetterDef def, string chronicle, Pawn chroniclePawn,
            int letterId, int arrive, PostUrgency urgency, string bringLabel)
        {
            var book = Book;
            if (!PostBook.On || book == null || map == null || !Find.Maps.Contains(map) || text.NullOrEmpty()) return -1;
            var item = new PostItem
            {
                id = book.nextId++, map = map, from = from, town = town?.ID ?? -1, label = label, text = text, def = def ?? LetterDefOf.NeutralEvent, chronicle = chronicle,
                chroniclePawn = chroniclePawn, letterId = letterId, arrive = arrive, urgency = urgency, bringLabel = bringLabel
            };
            book.incoming.Add(item);
            // Too many waiting: the oldest is brought by a messenger now.
            if (book.incoming.Count > PostMath.MaxIncoming) Messenger(book.incoming.OrderBy(i => i.arrive).First(i => i.carrier == null));
            NotableBook.Changed();
            return item.id;
        }

        /// <summary>
        /// One of the world's letters carried to the colony (a town's letter, the colony's envoy or ward writing from abroad):
        /// on the road from their town by their people's means over the land's own way (Post/PostReach.cs), then waiting for
        /// whoever comes. False: the post is off; the caller tells it as before.
        /// </summary>
        public static bool FromAbroad(Map map, Faction from, Settlement town, string label, string text, LetterDef def, string bringLabel = null)
        {
            if (!PostBook.On || map == null) return false;
            float days = 1f;
            try
            {
                var means = PostMath.MeansOf(Ages.Of(from), false);
                days = town != null ? PostReach.Days(map, town, means, out _) : 1f;
                if (days < 0f) days = LetterMath.LegDays(town != null ? LivingEvents.Distance(map.Tile, town.Tile) : 20f, means);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a letter from abroad", e);
            }
            return Add(map, from, town, label, text, def, null, null, -1, Now + (int)(days * GenDate.TicksPerDay), PostUrgency.Ordinary, bringLabel ?? label) >= 0;
        }

        /// <summary>Mail in the region and waiting for someone to bring it the rest of the way (not on its way with a carrier).</summary>
        public static List<PostItem> Due(Map map) =>
            Book?.incoming.Where(i => i.map == map && i.carrier == null && Now >= i.arrive).ToList() ?? new List<PostItem>();

        public static bool AnyDue(Map map) => Book?.incoming.Any(i => i.map == map && i.carrier == null && Now >= i.arrive) ?? false;

        /// <summary>Mail a carrier coming in from the way they came brings: from their own people, from towns that way, or all of it by the post.</summary>
        public static List<PostItem> For(MailCarrier c, bool entryKnown, float entryHeading)
        {
            var list = new List<PostItem>();
            foreach (var i in Due(c.map))
            {
                var town = NotableBook.TownOf(i.town);
                bool theirs = c.people != null && i.from == c.people;
                bool served = i.from != null && PostMath.PostServes(Ages.Of(i.from), i.from.HostileTo(Faction.OfPlayer));
                WayWhy way = theirs ? WayWhy.Theirs : WayWhy.No;
                if (!theirs && town != null)
                    way = PostMath.OnTheirWay(false, Outbox.Heading(c.map.Tile, town.Tile), LivingEvents.Distance(c.map.Tile, town.Tile), false, 0f, 0f, entryKnown,
                        entryHeading);
                // Shuttles come from anywhere.
                if (c.means == LetterMeans.Shuttle) way = WayWhy.Cone;
                if (PostMath.Brings(c.kind, way, served || c.kind == PostCarrier.PostRider && i.from == null)) list.Add(i);
            }
            return list;
        }

        /// <summary>
        /// The carrier set the bag down at the mailbag (from the job): each letter opens now, with the carrier and the bag on
        /// the map in its look, and an answer of the colony's own letter is in from then.
        /// </summary>
        public static void Delivered(Pawn carrier)
        {
            var book = Book;
            if (book == null || carrier == null) return;
            var mine = book.incoming.Where(i => i.carrier == carrier).ToList();
            if (mine.Count == 0) return;
            foreach (var i in mine)
            {
                book.incoming.Remove(i);
                Open(i, new LookTargets(carrier));
            }
            PostWords.Say(carrier, "drop", null);
            NotableBook.Changed();
        }

        /// <summary>Someone who comes in person (an invited guest) brings their answer still in the post: it opens now, with them in its look.</summary>
        public static void Hand(int id, Pawn by)
        {
            var book = Book;
            var i = book?.Item(id);
            if (i == null) return;
            book.incoming.Remove(i);
            Open(i, by != null ? new LookTargets(by) : LookTargets.Invalid);
            NotableBook.Changed();
        }

        /// <summary>A letter opens: told as a letter, written in the chronicle, and its answer counted in.</summary>
        private static void Open(PostItem i, LookTargets look)
        {
            try
            {
                Tell.Letter(NewsKind.World, Importance.Notable, i.map, i.label, i.text, i.def ?? LetterDefOf.NeutralEvent, look, i.from);
                if (!i.chronicle.NullOrEmpty())
                    CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, i.chronicle, KinMemory.Ours(i.chroniclePawn) ? i.chroniclePawn : null));
                if (i.letterId >= 0) NeighbourLetters.AnswerIn(i.letterId);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a letter opened", e);
            }
        }

        /// <summary>
        /// Brought at last by a messenger of the writer's people (Couriers.MailAnswer: they walk it to the gate); with nobody
        /// to send, it is told now as it always was.
        /// </summary>
        public static void Messenger(PostItem i)
        {
            var book = Book;
            if (book == null || i == null) return;
            book.incoming.Remove(i);
            try
            {
                int id = i.letterId >= 0 ? i.letterId : MailId;
                var trip = i.map != null && Find.Maps.Contains(i.map)
                    ? Couriers.MailAnswer(i.map, i.from, new List<Thing>(), id, Now, i.bringLabel ?? i.label, i.label, i.text, i.def, i.chronicle, i.chroniclePawn)
                    : null;
                if (trip != null)
                {
                    if (i.letterId >= 0) NeighbourLetters.MessengerFor(i.letterId, trip.id);
                    return;
                }
                var town = NotableBook.TownOf(i.town);
                Open(i, town != null ? new LookTargets(town) : LookTargets.Invalid);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a messenger with the mail", e);
            }
        }

        /// <summary>
        /// The hour: mail waiting longer than is reasonable for its kind goes by messenger; a carrier who never brought theirs
        /// (gone, hurt, turned hostile) lets it go back to waiting; mail for a home that is gone is told now.
        /// </summary>
        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null || book.incoming.Count == 0) return;
            if (!PostBook.On)
            {
                Flush();
                return;
            }
            foreach (var i in book.incoming.ToList())
            {
                try
                {
                    if (i.map == null || !Find.Maps.Contains(i.map))
                    {
                        book.incoming.Remove(i);
                        i.map = HappeningTracker.HomeMap();
                        if (i.map != null) Open(i, LookTargets.Invalid);
                        continue;
                    }
                    if (i.carrier != null)
                    {
                        var c = i.carrier;
                        if (c.Dead || !c.Spawned || c.Map != i.map || c.Downed || c.HostileTo(Faction.OfPlayer) || c.CurJobDef != PostDefOf.Rimfolk_PostDrop) i.carrier = null;
                        continue;
                    }
                    if (now < i.arrive) continue;
                    bool post = PostRider.Runs(i.map);
                    if (now - i.arrive > PostMath.MessengerWaitDays(i.urgency, post) * GenDate.TicksPerDay) Messenger(i);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: the mail's hour", e);
                }
            }
        }

        /// <summary>The setting turned off: whatever waits goes by messenger now.</summary>
        public static void Flush()
        {
            var book = Book;
            if (book == null) return;
            foreach (var i in book.incoming.ToList()) Messenger(i);
        }
    }
}
