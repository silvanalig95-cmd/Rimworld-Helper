using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Where a letter (or a call) is.</summary>
    public enum LetterStage
    {
        /// <summary>The writer will sit down to it when they have a moment.</summary>
        ToWrite,
        /// <summary>At a table, writing.</summary>
        Writing,
        /// <summary>Written: a messenger of theirs is to come to the gate for it.</summary>
        AwaitMessenger,
        /// <summary>Carried off; read when it gets there.</summary>
        Travelling,
        /// <summary>Read and answered: the answer is on its way back (or never comes).</summary>
        Answering,
        /// <summary>An invitation taken up: they come to visit.</summary>
        Visiting,
        /// <summary>They are here as the colony's guest.</summary>
        Guesting,
        /// <summary>The caller will go to the comms console when they have a moment.</summary>
        ToCall,
        /// <summary>At the console, calling.</summary>
        Calling,
        /// <summary>An aide took a message: the notable calls back later.</summary>
        CallbackWait,
        /// <summary>The console is ringing.</summary>
        Ringing,
        /// <summary>Written (or a message to be spoken): in the mailbag at the gate, waiting for someone going its way (Post/Outbox.cs).</summary>
        Outbox,
        /// <summary>With the colony's own rider, on the road there (Post/OwnRiders.cs).</summary>
        Riding,
        Done,
        Cancelled
    }

    /// <summary>One letter or call to a notable: who writes, what for, how it goes, where it is, and how it was answered.</summary>
    public class NeighbourLetter : IExposable
    {
        public int id;
        public int notable;
        public LetterPurpose purpose;
        public LetterChannel channel;
        public LetterStage stage;
        public Pawn writer;
        public string writerName;
        public string toName;
        public Map map;
        public int created;
        public int stageTick;
        /// <summary>When the next thing happens (read, answer back, the visit, the callback): never shown as a countdown.</summary>
        public int due = -1;
        /// <summary>Messengers that came without taking it, callbacks that couldn't get through.</summary>
        public int tries;
        /// <summary>What it's about (the threat warned of, the loss mourned), and its key (each only once).</summary>
        public string topic;
        public string topicKey;
        /// <summary>The help asked for: medicine or food.</summary>
        public string help;
        public int tripId = -1;
        /// <summary>Days for the answer to come back, worked out when it set out.</summary>
        public float backDays;
        public AnswerTone tone;
        public CallReach reach;
        /// <summary>Who took the callback, or the guest who came.</summary>
        public Pawn other;
        public bool hostile;
        /// <summary>The messenger come for it reached the gate (else they never could come).</summary>
        public bool metAtGate;
        /// <summary>Someone of their people at the colony who carries it home instead of a messenger (Outsiders/), while they're here.</summary>
        public Pawn bearer;
        public string bearerName;
        /// <summary>
        /// A warning heeded warmly: their people count it a debt of honour, made when the answer that says so reaches the
        /// colony (NeighbourLetters.Owed), not before.
        /// </summary>
        public bool owed;

        // ---- The post (Post/): who took it and how, from the outbox ----

        /// <summary>Who carried it away (the post rider, traders, visitors, the colony's caravan or rider...), how, and through which town it was relayed.</summary>
        public PostCarrier carrier;
        public string carrierName;
        public LetterMeans carrierMeans;
        public string relayName;
        /// <summary>A tribal colony's spoken message: a colonist gives the words to the carrier at the gate.</summary>
        public bool spoken;
        /// <summary>It goes with the colony's own rider (<see cref="rider"/>).</summary>
        public bool ownRider;
        public Pawn rider;
        /// <summary>The carrier it is kept for while they come for it, or the one of a forming caravan who has it in their pack (<see cref="packed"/>).</summary>
        public Pawn packedBy;
        public bool packed;
        /// <summary>Its answer waiting in the post for a carrier (Post/MailIn.cs), or -1.</summary>
        public int postId = -1;

        public bool Open => stage != LetterStage.Done && stage != LetterStage.Cancelled;

        public string WriterName => writer != null && !writer.Discarded ? writer.LabelShort : writerName ?? "?";

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (writer != null && writer.Discarded) writer = null;
                if (other != null && other.Discarded) other = null;
                if (bearer != null && bearer.Discarded) bearer = null;
                if (rider != null && rider.Discarded) rider = null;
                if (packedBy != null && packedBy.Discarded) packedBy = null;
                if (map != null && !Find.Maps.Contains(map)) map = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref notable, "notable");
            Scribe_Values.Look(ref purpose, "purpose");
            Scribe_Values.Look(ref channel, "channel");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref writer, "writer");
            Scribe_Values.Look(ref writerName, "writerName");
            Scribe_Values.Look(ref toName, "toName");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref stageTick, "stageTick");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref tries, "tries");
            Scribe_Values.Look(ref topic, "topic");
            Scribe_Values.Look(ref topicKey, "topicKey");
            Scribe_Values.Look(ref help, "help");
            Scribe_Values.Look(ref tripId, "tripId", -1);
            Scribe_Values.Look(ref backDays, "backDays");
            Scribe_Values.Look(ref tone, "tone");
            Scribe_Values.Look(ref reach, "reach");
            Scribe_References.Look(ref other, "other");
            Scribe_Values.Look(ref hostile, "hostile");
            Scribe_Values.Look(ref metAtGate, "metAtGate");
            Scribe_References.Look(ref bearer, "bearer");
            Scribe_Values.Look(ref bearerName, "bearerName");
            Scribe_Values.Look(ref owed, "owed");
            Scribe_Values.Look(ref carrier, "carrier");
            Scribe_Values.Look(ref carrierName, "carrierName");
            Scribe_Values.Look(ref carrierMeans, "carrierMeans");
            Scribe_Values.Look(ref relayName, "relayName");
            Scribe_Values.Look(ref spoken, "spoken");
            Scribe_Values.Look(ref ownRider, "ownRider");
            Scribe_References.Look(ref rider, "rider");
            Scribe_References.Look(ref packedBy, "packedBy");
            Scribe_Values.Look(ref packed, "packed");
            Scribe_Values.Look(ref postId, "postId", -1);
        }
    }

    [DefOf]
    public static class NotableDefOf
    {
        public static JobDef Council_WriteLetter;
        public static JobDef Council_HandLetter;
        public static JobDef Council_NeighbourCall;
        public static JobDef Council_AnswerCall;

        static NotableDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NotableDefOf));
        }
    }

    /// <summary>A topic a letter can be about: a threat or a loss the colony knows of, with a key so each is written of once.</summary>
    public struct LetterTopic
    {
        public string key;
        public string text;
    }

    /// <summary>
    /// Letters to the colony's neighbours, and calls to them. From the World page or the region map's town panel the
    /// player picks a notable and what for (greetings, condolences, a warning, asking for news, an invitation, asking for
    /// a small help) and who writes. By letter: the writer sits down at a table for a while (a real job), a messenger of
    /// their people (of a people at peace with the colony, under a white flag, for one at war with it) comes to the gate
    /// for it and is handed it, and the answer comes back the same way after the walk there and back, as a letter in the
    /// game, with gifts in the messenger's pack when it brings some. By call, once the colony has a powered comms console
    /// and they have the means to answer: the caller goes to the console and talks (a real job); the notable may answer
    /// at once, or an aide takes a message and they call back later (someone answers the console when it rings). Words
    /// travel by console, things don't: what an answer sends still comes by courier. The answer follows their nature and
    /// regard, moves how they see the colony and the writer, and a little of their people's goodwill, and is remembered
    /// on both sides. One letter at a time to each notable, a wait between letters for the same thing, three under way at
    /// most. Hourly from <see cref="NotableBook"/>; the console's ringing and the jobs are looked at every 250 ticks.
    /// </summary>
    public static class NeighbourLetters
    {
        public static bool On => (ColonyCouncilMod.Settings?.neighbourLetters ?? true) && NotableBook.On;

        private static int Now => Find.TickManager.TicksGame;

        private static NotableBook Book => NotableBook.Instance;

        private const string SayPool = "Rimfolk_Pool_NeighbourSays";
        private const string WritePool = "Rimfolk_Pool_NeighbourWrites";

        /// <summary>How long writing a letter takes (about an hour).</summary>
        public const int WriteTicks = 2200;
        /// <summary>How long a call takes.</summary>
        public const int CallTicks = 1100;

        public static NeighbourLetter Get(int id) => Book?.letters.FirstOrDefault(l => l.id == id);

        public static NeighbourLetter OpenTo(Notable n) => n == null ? null : Book?.letters.FirstOrDefault(l => l.Open && l.notable == n.id);

        public static int OpenCount => Book?.letters.Count(l => l.Open) ?? 0;

        // ---------------- What can be written, by whom, how ----------------

        /// <summary>
        /// The colony's grown, free people at home, who can call, answer the console or hand a letter over. With
        /// <paramref name="pen"/>, those who can write one: able to read and write by the game's rules (its books:
        /// BookUtility.CanReadEver); in a tribal colony, with the post, anyone grown who can speak gives a spoken message
        /// instead (PostMath.CanWrite).
        /// </summary>
        public static List<Pawn> Writers(Map map, bool pen = false)
        {
            if (map == null) return new List<Pawn>();
            bool spoken = Spoken;
            return IncidentWorker_Happening.Colonists(map).Where(p => PostMath.CanWrite(!p.DevelopmentalStage.Juvenile(), !p.IsSlave, !pen || Reads(p),
                p.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? true, spoken && pen)).ToList();
        }

        /// <summary>A tribal colony with the post: its letters are spoken messages given to the carrier.</summary>
        public static bool Spoken => PostBook.On && PostMath.Spoken(Ages.Ours);

        private static bool Reads(Pawn p)
        {
            try
            {
                return BookUtility.CanReadEver(p);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Who writes unless the player picks someone else: the speaker, else the colony's best talker.</summary>
        public static Pawn DefaultWriter(Map map)
        {
            var all = Writers(map, true);
            // The speaker of the colony writing.
            var speaker = ColonyState.For(map)?.offices?.Speaker;
            if (speaker != null && all.Contains(speaker)) return speaker;
            return all.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
        }

        /// <summary>A powered comms console that works now, or null (no power, a solar flare: the air is dead).</summary>
        public static Building_CommsConsole Console(Map map, Pawn near = null)
        {
            if (map == null) return null;
            Building_CommsConsole best = null;
            float bestD = float.MaxValue;
            foreach (var c in map.listerBuildings.allBuildingsColonist.OfType<Building_CommsConsole>())
            {
                if (!c.Spawned || !c.CanUseCommsNow || c.IsForbidden(Faction.OfPlayer)) continue;
                float d = near != null && near.Spawned ? c.Position.DistanceToSquared(near.Position) : 0f;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>The colony has a comms console at all (working or not).</summary>
        public static bool HasConsole(Map map) => map != null && map.listerBuildings.allBuildingsColonist.Any(b => b is Building_CommsConsole);

        private static int TechOf(Faction f) => (int)(Peoples.TechOf(f, TechLevel.Industrial));

        public static bool Traditional(Faction f) => NotableMath.Traditional(TechOf(f), f?.def?.HasRoyalTitles ?? false);

        /// <summary>Why this way of sending can't be used now, or null.</summary>
        public static string ChannelWhyNot(Notable n, LetterChannel c, Map map)
        {
            if (map == null) return "CC_Letter_Why_NoHome".Translate();
            if (c == LetterChannel.Courier)
            {
                // With the post, letters wait in the outbox for whoever goes their way; a people at war still needs a go-between.
                if (PostBook.On && !NotableBook.Hostile(n)) return null;
                return Couriers.CanCarryMail(map, n.faction) ? null : "CC_Letter_Why_NoMessenger".Translate().Resolve();
            }
            // Radio: a people of the industrial age or later, or one within reach of the Signal Line's masts (Post/PostReach.cs).
            if (!PostReach.Radio(n.faction, NotableBook.TownOf(n.town))) return "CC_Letter_Why_NoMeans".Translate(n.faction?.Name ?? "").Resolve();
            if (!HasConsole(map)) return "CC_Letter_Why_NoConsole".Translate();
            if (Console(map) == null) return "CC_Letter_Why_DeadAir".Translate();
            return null;
        }

        /// <summary>Why this can't be written to them now, or null (a wait, nothing to write about, too little regard).</summary>
        public static string WhyNot(Notable n, LetterPurpose p)
        {
            if (n == null || !n.Alive) return "CC_Letter_Why_Gone".Translate();
            if (OpenTo(n) != null) return "CC_Letter_Why_OneAtATime".Translate();
            // With the post the outbox keeps letters from leaving at once, so only a sane cap on letters open (PostMath.MaxOpen).
            int max = PostBook.On ? PostMath.MaxOpen : NotableMath.MaxOpen;
            if (OpenCount >= max) return "CC_Letter_Why_TooMany".Translate(max).Resolve();
            if (n.pawn.Spawned) return "CC_Letter_Why_Here".Translate(n.Name.Named("NAME")).Resolve();
            int last = n.Last(p);
            if (last >= 0 && Now - last < NotableMath.WaitDays(p) * GenDate.TicksPerDay) return "CC_Letter_Why_Lately".Translate();
            bool hostile = NotableBook.Hostile(n);
            switch (p)
            {
                case LetterPurpose.Condolences:
                    if (Topics(n, p).Count == 0) return "CC_Letter_Why_NoLoss".Translate();
                    break;
                case LetterPurpose.Warning:
                    if (Topics(n, p).Count == 0) return "CC_Letter_Why_NoThreat".Translate();
                    break;
                case LetterPurpose.Invitation:
                    if (!NotableMath.CanInvite(hostile)) return "CC_Letter_Why_AtWar".Translate();
                    break;
                case LetterPurpose.Help:
                    if (hostile) return "CC_Letter_Why_AtWar".Translate();
                    if (!NotableMath.CanAskHelp(NotableBook.Opinion(n), false)) return "CC_Letter_Why_NotEnough".Translate(n.Name.Named("NAME")).Resolve();
                    break;
                default:
                    // Stage 2's purposes (Diplomacy/DiplomacyLetters.cs): a debt of honour called in, the colony's word given.
                    if (DiplomacyLetters.Owns(p)) return DiplomacyLetters.WhyNot(n, p);
                    break;
            }
            return null;
        }

        /// <summary>What a condolence or a warning can be about: the losses and threats the colony knows of, not yet written of.</summary>
        public static List<LetterTopic> Topics(Notable n, LetterPurpose p)
        {
            var list = new List<LetterTopic>();
            if (n?.faction == null) return list;
            var f = n.faction;
            var town = NotableBook.TownOf(n.town);
            void Add(string key, string text)
            {
                if (!n.done.Contains(key) && list.All(t => t.key != key)) list.Add(new LetterTopic { key = key, text = text });
            }
            try
            {
                var running = WorldSituations.Instance?.Running?.ToList() ?? new List<WorldSituation>();
                if (p == LetterPurpose.Condolences)
                {
                    foreach (var l in Book?.losses ?? new List<NotableLoss>())
                        if (l.faction == f && l.name != n.Name)
                            Add("loss:" + l.name, "CC_Letter_Topic_Loss".Translate(l.name.Named("NAME"), (l.role ?? "").Named("ROLE")).Resolve());
                    foreach (var s in running.Where(s => s.def != null && s.def.bad && s.ctx?.faction == f))
                        Add("hard:" + s.id, Called(s));
                }
                else if (p == LetterPurpose.Warning)
                {
                    // A hard time in the region, or one that falls on them.
                    foreach (var s in running.Where(s => s.def != null && s.def.bad && (s.ctx?.faction == null || s.ctx.faction == f)))
                        Add("sit:" + s.id, Called(s));
                    // Raiders who came at the colony lately and are at war with them too.
                    var mem = FactionMemory.Instance;
                    if (mem != null)
                        foreach (var g in mem.deeds.Where(d => d.kind == "raided" && d.faction != null && d.faction != f && !d.faction.defeated && Now - d.tick < GenDate.TicksPerDay * 30)
                                     .Select(d => d.faction).Distinct())
                            if (g.HostileTo(f))
                                Add("raid:" + g.loadID, "CC_Letter_Topic_Raids".Translate(g.Name.Named("FACTION")).Resolve());
                    // A hostile people's camp near their town.
                    if (town != null)
                        foreach (var site in Find.WorldObjects.Sites)
                            if (site?.Faction != null && site.Faction.HostileTo(f) && site.Tile.Layer == town.Tile.Layer
                                && Find.WorldGrid.ApproxDistanceInTiles(site.Tile, town.Tile) <= 12f)
                                Add("site:" + site.ID, "CC_Letter_Topic_Site".Translate(site.LabelCap.Named("SITE"), Direction(town.Tile, site.Tile).Named("DIR"), town.Name.Named("TOWN")).Resolve());
                }
                // A debt of honour to call in, or the colony's word owed on a call of theirs; a pact's kind, a truce's term (Diplomacy/DiplomacyLetters.cs).
                else if (DiplomacyLetters.Owns(p)) list.AddRange(DiplomacyLetters.Topics(n, p));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter topics", e);
            }
            return list;
        }

        /// <summary>A world event as words read: its own name ("the gray cough"), else what it is ("a plague").</summary>
        private static string Called(WorldSituation s) =>
            !(s.ctx?.name).NullOrEmpty() ? s.ctx.name : Find.ActiveLanguageWorker.WithIndefiniteArticle(s.def?.label ?? "?");

        /// <summary>"north", "southeast": which way one tile lies from another.</summary>
        public static string Direction(PlanetTile from, PlanetTile to)
        {
            try
            {
                if (!from.Valid || !to.Valid || from.Layer != to.Layer) return "";
                switch (Find.WorldGrid.GetDirection8WayFromTo(from, to))
                {
                    case Direction8Way.North: return "CC_Letter_Dir_N".Translate();
                    case Direction8Way.NorthEast: return "CC_Letter_Dir_NE".Translate();
                    case Direction8Way.East: return "CC_Letter_Dir_E".Translate();
                    case Direction8Way.SouthEast: return "CC_Letter_Dir_SE".Translate();
                    case Direction8Way.South: return "CC_Letter_Dir_S".Translate();
                    case Direction8Way.SouthWest: return "CC_Letter_Dir_SW".Translate();
                    case Direction8Way.West: return "CC_Letter_Dir_W".Translate();
                    default: return "CC_Letter_Dir_NW".Translate();
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>How the colony's words are likely to be taken, in words (never a number).</summary>
        public static string Outlook(Notable n, Pawn writer, LetterPurpose p, LetterChannel c)
        {
            var x = Input(n, writer, p, c);
            float s = NotableMath.Score(x);
            string key = x.hostile ? "CC_Letter_Outlook_War" : s >= 40f ? "CC_Letter_Outlook_Warm" : s >= 12f ? "CC_Letter_Outlook_Good" : s >= -15f ? "CC_Letter_Outlook_Fair" : "CC_Letter_Outlook_Poor";
            string line = key.Translate(n.Name.Named("NAME"), (writer?.LabelShort ?? "?").Named("WRITER")).Resolve();
            if (c == LetterChannel.Courier && NotableMath.Personal(p) && Traditional(n.faction)) line += " " + "CC_Letter_Outlook_ByHand".Translate().Resolve();
            if (c == LetterChannel.Console && NotableMath.Personal(p)) line += " " + "CC_Letter_Outlook_Call".Translate().Resolve();
            return line;
        }

        private static LetterInput Input(Notable n, Pawn writer, LetterPurpose p, LetterChannel c)
        {
            int last = n.Last(p);
            return new LetterInput
            {
                purpose = p, channel = c, opinion = NotableBook.Opinion(n), ofWriter = NotableBook.OpinionOf(n, writer),
                social = writer?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4, warmth = NotableBook.Warmth(n.pawn), hostile = NotableBook.Hostile(n),
                traditional = Traditional(n.faction), troubled = WorldSituations.Instance?.HardTimes(n.faction) ?? false,
                lately = last >= 0 && Now - last < NotableMath.LatelyDays * GenDate.TicksPerDay
            };
        }

        // ---------------- Sending ----------------

        /// <summary>
        /// The player sends it: the writer will sit down to it (or go to the console) when they have a moment. With a
        /// <paramref name="bearer"/> (someone of their people at the colony, Outsiders/), it goes by their hand instead
        /// of a messenger's, and the writer sits down to it at once if they're free.
        /// </summary>
        public static NeighbourLetter Send(Notable n, LetterPurpose p, LetterChannel c, Pawn writer, LetterTopic? topic, string help, Pawn bearer = null, Pawn rider = null)
        {
            var book = Book;
            var map = writer?.MapHeld ?? HappeningTracker.HomeMap();
            if (bearer != null) c = LetterChannel.Courier;
            if (book == null || n == null || writer == null || map == null || WhyNot(n, p) != null || bearer == null && ChannelWhyNot(n, c, map) != null) return null;
            var l = new NeighbourLetter
            {
                id = book.nextLetterId++, notable = n.id, purpose = p, channel = c, writer = writer, writerName = writer.LabelShort, toName = NotableBook.Called(n),
                map = map, created = Now, stageTick = Now, stage = c == LetterChannel.Console ? LetterStage.ToCall : LetterStage.ToWrite,
                topic = topic?.text, topicKey = topic?.key, help = help, hostile = NotableBook.Hostile(n), bearer = bearer, bearerName = bearer?.LabelShort
            };
            book.letters.Add(l);
            NotableBook.Changed();
            if (rider != null && c == LetterChannel.Courier && bearer == null) OwnRiders.Send(l, rider);
            // A tribal colony's spoken message: no writing, the words wait for a carrier at the gate (Post/).
            if (c == LetterChannel.Courier && bearer == null && Spoken && !l.hostile)
            {
                l.spoken = true;
                Outbox.Put(l, writer);
                if (l.ownRider) OwnRiders.SetOut(l);
                return l;
            }
            // apart: the player's choice, said in a message; the writer sits down to it on their own later.
            if (bearer != null)
                Notify.Message("CC_Letter_Msg_WillWriteFor".Translate(writer.Named("PAWN"), l.toName.Named("TO"), bearer.Named("BEARER")).Resolve(), new LookTargets(writer),
                    MessageTypeDefOf.NeutralEvent, Importance.Notable);
            else
                Notify.Message((c == LetterChannel.Console ? "CC_Letter_Msg_WillCall" : "CC_Letter_Msg_WillWrite")
                    .Translate(writer.Named("PAWN"), l.toName.Named("TO")).Resolve(), new LookTargets(writer), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (bearer != null) TryWrite(l, Now);
            return l;
        }

        /// <summary>Called off (the writer or the notable gone, nobody came for it); <paramref name="why"/>: the message, or none.</summary>
        public static void Cancel(NeighbourLetter l, string why)
        {
            if (l == null || !l.Open) return;
            l.stage = LetterStage.Cancelled;
            l.stageTick = Now;
            NotableBook.Changed();
            if (!why.NullOrEmpty())
                // apart: what became of a letter, told in a message.
                Notify.Message(why.Translate(l.WriterName.Named("PAWN"), (l.toName ?? "?").Named("TO")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- The hour ----------------

        public static void Hourly(NotableBook book, int now)
        {
            if (book.letters.Count == 0) return;
            foreach (var l in book.letters.ToList())
            {
                if (!l.Open) continue;
                try
                {
                    Step(book, l, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("letter " + l.stage, e);
                    Cancel(l, null);
                }
            }
            book.letters.RemoveAll(l => !l.Open && now - l.stageTick > GenDate.TicksPerDay * 2);
        }

        /// <summary>Every 250 ticks while a letter is being written or a call is under way or ringing: the jobs and the console.</summary>
        public static void Rare(NotableBook book, int now)
        {
            foreach (var l in book.letters)
            {
                if (l.stage == LetterStage.Ringing && !NotableMath.Retired(l.purpose)) TryAnswer(l, now);
                else if ((l.stage == LetterStage.Writing || l.stage == LetterStage.Calling) && now - l.stageTick > Sched.HourTicks / 2 && !AtIt(l))
                    Back(l);
            }
        }

        /// <summary>Anything that wants the 250-tick look.</summary>
        public static bool WantsRare(NotableBook book) => book.letters.Any(l => l.stage == LetterStage.Ringing || l.stage == LetterStage.Writing || l.stage == LetterStage.Calling);

        private static void Step(NotableBook book, NeighbourLetter l, int now)
        {
            var n = book.Get(l.notable);
            if (n == null)
            {
                Cancel(l, "CC_Letter_Cancel_Gone");
                return;
            }
            // The council's business in an older save (retired with the council's tasks): called off before it is read,
            // quietly; an answer already on its way back comes as it was written.
            if (NotableMath.Retired(l.purpose) && l.stage != LetterStage.Answering)
            {
                Cancel(l, null);
                return;
            }
            bool needsWriter = l.stage == LetterStage.ToWrite || l.stage == LetterStage.Writing || l.stage == LetterStage.ToCall || l.stage == LetterStage.Calling;
            if (needsWriter && !KinMemory.Ours(l.writer))
            {
                Cancel(l, "CC_Letter_Cancel_Writer");
                return;
            }
            switch (l.stage)
            {
                case LetterStage.ToWrite:
                case LetterStage.ToCall:
                    if (now - l.created > NotableMath.GiveUpDays * GenDate.TicksPerDay)
                    {
                        Cancel(l, "CC_Letter_Cancel_NoTime");
                        return;
                    }
                    if (l.stage == LetterStage.ToWrite) TryWrite(l, now);
                    else TryCall(l, now);
                    return;
                case LetterStage.Writing:
                case LetterStage.Calling:
                    if (!AtIt(l)) Back(l);
                    return;
                case LetterStage.AwaitMessenger:
                {
                    // Carried home by someone of theirs at the colony: handed over while they're here, else a messenger after all.
                    if (l.bearer != null && l.tripId < 0)
                    {
                        if (Outsiders.HandLetter(l, l.writer)) return;
                        // apart: what became of a letter, told in a message.
                        Notify.Message("CC_Letter_Msg_BearerGone".Translate((l.bearerName ?? "?").Named("BEARER"), l.WriterName.Named("PAWN"), l.toName.Named("TO")).Resolve(),
                            LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                        l.bearer = null;
                        Pickup(l, n);
                        return;
                    }
                    var trip = CourierTracker.Instance?.Get(l.tripId);
                    if (trip != null && !trip.settled) return;
                    if (trip != null && trip.received) return;
                    // An older save's letter whose messenger never came (or the post turned on): it goes in the outbox.
                    if (PostBook.On && !l.hostile && !NotableBook.Hostile(n))
                    {
                        l.tripId = -1;
                        Outbox.Put(l, null);
                        return;
                    }
                    if (l.tripId >= 0) l.tries++;
                    if (l.tries >= NotableMath.MaxPickups)
                    {
                        Cancel(l, "CC_Letter_Cancel_NoPickup");
                        return;
                    }
                    // apart: what became of a letter, told in a message.
                    if (l.tripId >= 0)
                        Notify.Message((l.metAtGate ? "CC_Letter_Msg_PickupMissed" : "CC_Letter_Msg_PickupNever").Translate(l.WriterName.Named("PAWN"), l.toName.Named("TO")).Resolve(),
                            LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                    l.metAtGate = false;
                    Pickup(l, n);
                    return;
                }
                case LetterStage.Outbox:
                    // A people now at war: a go-between under a white flag comes for it, the old way.
                    if (NotableBook.Hostile(n) && !l.ownRider)
                    {
                        l.hostile = true;
                        l.packedBy = null;
                        l.packed = false;
                        l.stage = LetterStage.AwaitMessenger;
                        l.stageTick = now;
                        Pickup(l, n);
                        return;
                    }
                    // The rider chosen is gone: it waits for a carrier again; still there with no ride under way: they set out.
                    if (l.ownRider && PostBook.Instance?.RideFor(l.id) == null)
                    {
                        if (l.rider == null || !KinMemory.Ours(l.rider) || l.rider.MapHeld != l.map || !OwnRiders.SetOut(l))
                        {
                            l.ownRider = false;
                            l.rider = null;
                        }
                    }
                    // The post turned off: a messenger comes for it, as before.
                    if (!PostBook.On && !l.ownRider && !l.packed)
                    {
                        l.packedBy = null;
                        l.stage = LetterStage.AwaitMessenger;
                        l.stageTick = now;
                        Pickup(l, n);
                    }
                    return;
                case LetterStage.Riding:
                    if (PostBook.Instance?.RideFor(l.id) == null)
                    {
                        l.stage = LetterStage.Outbox;
                        l.ownRider = false;
                        l.rider = null;
                    }
                    return;
                case LetterStage.Travelling:
                    if (now >= l.due) Read(l, n, l.writer, now);
                    return;
                case LetterStage.Answering:
                {
                    // The colony's own rider brings it, or it waits in the post for a carrier (Post/): they say when it's in.
                    if (l.ownRider && PostBook.Instance?.RideFor(l.id) != null) return;
                    if (l.postId >= 0 && PostBook.Instance?.Item(l.postId) != null) return;
                    if (l.ownRider || l.postId >= 0)
                    {
                        Owed(l, n);
                        Finish(l, n);
                        return;
                    }
                    if (l.tripId >= 0)
                    {
                        var trip = CourierTracker.Instance?.Get(l.tripId);
                        if (trip == null || trip.settled)
                        {
                            // The answer is in: what it says they owe the colony is owed from now.
                            Owed(l, n);
                            Finish(l, n);
                        }
                        return;
                    }
                    if (now < l.due) return;
                    // No answer came (a people at war keeping silent).
                    // apart: a letter's fate, told in a message.
                    Notify.Message("CC_Letter_Msg_NoAnswer".Translate(l.WriterName.Named("PAWN"), l.toName.Named("TO")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                    Finish(l, n);
                    return;
                }
                case LetterStage.Visiting:
                    if (now >= l.due) TryVisit(l, n, now);
                    return;
                case LetterStage.Guesting:
                    Guesting(l, n, now);
                    return;
                case LetterStage.CallbackWait:
                    if (now < l.due) return;
                    if (Console(l.map) != null)
                    {
                        l.stage = LetterStage.Ringing;
                        l.stageTick = now;
                        var console = Console(l.map);
                        Notify.Message("CC_Call_Msg_Ringing".Translate(n.Name.Named("NAME")).Resolve(), new LookTargets(console), MessageTypeDefOf.NeutralEvent, Importance.Notable);
                        NotableBook.Changed();
                        TryAnswer(l, now);
                        return;
                    }
                    // The air is dead at the colony's end: they try again later, then leave word.
                    l.tries++;
                    if (l.tries >= 2) LeftWord(l, n, now);
                    else l.due = now + GenDate.TicksPerHour * 12;
                    return;
                case LetterStage.Ringing:
                    // Nobody took it in time (a raid on, everyone asleep or busy): they leave word instead.
                    if (now - l.stageTick > NotableMath.RingTicks && !(l.map?.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == NotableDefOf.Council_AnswerCall) ?? false))
                        LeftWord(l, n, now);
                    return;
            }
        }

        /// <summary>The answer is in (the post's carrier set it down, or the colony's rider is home with it): what it says they owe is owed from now.</summary>
        public static void AnswerIn(int letterId)
        {
            var l = Get(letterId);
            var n = l != null ? Book?.Get(l.notable) : null;
            if (l == null || !l.Open) return;
            l.postId = -1;
            if (n != null) Owed(l, n);
            if (l.stage == LetterStage.Answering) Finish(l, n);
            else NotableBook.Changed();
        }

        /// <summary>The answer waited too long in the post: a messenger of theirs brings it after all (the courier trip it now waits for).</summary>
        public static void MessengerFor(int letterId, int tripId)
        {
            var l = Get(letterId);
            if (l == null) return;
            l.postId = -1;
            l.tripId = tripId;
            NotableBook.Changed();
        }

        /// <summary>The colony's own rider is at their town with it (Post/OwnRiders.cs): read there, and answered at once.</summary>
        public static void ReadByRider(NeighbourLetter l, Pawn rider)
        {
            var n = Book?.Get(l?.notable ?? -1);
            if (l == null || n == null || !l.Open || l.stage != LetterStage.Riding) return;
            try
            {
                var town = NotableBook.TownOf(n.town);
                if (l.backDays <= 0f && town != null) l.backDays = PostReach.RiderDays(l.map, town, PostBook.Instance?.RideFor(l.id)?.mount != null);
                Read(l, n, l.writer, Now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter read by the rider", e);
            }
        }

        private static void Finish(NeighbourLetter l, Notable n)
        {
            l.stage = LetterStage.Done;
            l.stageTick = Now;
            NotableBook.Changed();
        }

        // ---------------- Writing ----------------

        /// <summary>Free to sit down to a letter or a call: at home, awake, not needed elsewhere, at a waking hour, no danger.</summary>
        private static bool Free(Pawn p, Map map)
        {
            if (p == null || !p.Spawned || p.Map != map || !p.Awake() || p.GetLord() != null) return false;
            if (!PlayOuts.CanJoin(p) || !PlayOuts.Interruptible(p) || PlayOuts.NeedsPress(p)) return false;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 7 || hour >= 22) return false;
            return PlayOuts.WhyNotNow(map, false) == null;
        }

        private static void TryWrite(NeighbourLetter l, int now)
        {
            var p = l.writer;
            var map = p?.Map;
            if (map == null || !map.IsPlayerHome || !Free(p, map)) return;
            if (!Desk(p, out var table, out var seat)) return;
            var job = JobMaker.MakeJob(NotableDefOf.Council_WriteLetter, table != null ? (LocalTargetInfo)table : seat, seat);
            job.ignoreJoyTimeAssignment = true;
            l.stage = LetterStage.Writing;
            l.stageTick = now;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (p.CurJobDef != NotableDefOf.Council_WriteLetter) Back(l);
            NotableBook.Changed();
        }

        /// <summary>Where to write: a chair at one of the colony's tables, the nearest first; else any chair; else where they stand.</summary>
        public static bool Desk(Pawn p, out Thing table, out IntVec3 seat)
        {
            table = null;
            seat = IntVec3.Invalid;
            var map = p.Map;
            foreach (var t in map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.IsTable && !(b is Frame) && !b.IsForbidden(p) && !b.Position.Fogged(map))
                         .OrderBy(b => b.Position.DistanceToSquared(p.Position)).Take(10))
            {
                foreach (var c in GenAdj.CellsAdjacent8Way(t))
                {
                    var chair = c.InBounds(map) ? c.GetEdifice(map) : null;
                    if (chair == null || !(chair.def.building?.isSittable ?? false) || chair.IsForbidden(p)) continue;
                    if (!p.CanReserveAndReach(chair, PathEndMode.OnCell, Danger.Some)) continue;
                    table = t;
                    seat = c;
                    return true;
                }
            }
            foreach (var chair in map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && (b.def.building?.isSittable ?? false) && !b.IsForbidden(p))
                         .OrderBy(b => b.Position.DistanceToSquared(p.Position)).Take(10))
            {
                if (!p.CanReserveAndReach(chair, PathEndMode.OnCell, Danger.Some)) continue;
                seat = chair.Position;
                return true;
            }
            seat = p.Position;
            return seat.IsValid;
        }

        /// <summary>Still at the job for it.</summary>
        private static bool AtIt(NeighbourLetter l)
        {
            var def = l.writer?.CurJobDef;
            return l.stage == LetterStage.Writing ? def == NotableDefOf.Council_WriteLetter : def == NotableDefOf.Council_NeighbourCall;
        }

        /// <summary>Interrupted: they'll sit down to it again when they have a moment.</summary>
        public static void Back(NeighbourLetter l)
        {
            if (l == null) return;
            if (l.stage == LetterStage.Writing) l.stage = LetterStage.ToWrite;
            else if (l.stage == LetterStage.Calling) l.stage = LetterStage.ToCall;
            else return;
            l.stageTick = Now;
            NotableBook.Changed();
        }

        /// <summary>The letter someone is writing (or calling about) now.</summary>
        public static NeighbourLetter At(Pawn p, LetterStage stage) => Book?.letters.FirstOrDefault(l => l.stage == stage && l.writer == p);

        /// <summary>The letter is written: a messenger is sent for.</summary>
        public static void Written(NeighbourLetter l, Pawn p)
        {
            var n = Book?.Get(l?.notable ?? -1);
            if (l == null || n == null || l.stage != LetterStage.Writing) return;
            l.stage = LetterStage.AwaitMessenger;
            l.stageTick = Now;
            Say(p, "written", n);
            // Someone of theirs at the colony carries it: the writer takes it over to them (Outsiders/).
            if (l.bearer != null && Outsiders.HandLetter(l, p))
            {
                Notify.Message("CC_Letter_Msg_WrittenFor".Translate(p.Named("PAWN"), l.toName.Named("TO"), l.bearer.Named("BEARER")).Resolve(), new LookTargets(p),
                    MessageTypeDefOf.NeutralEvent, Importance.Notable);
                NotableBook.Changed();
                return;
            }
            l.bearer = null;
            // With the post: it goes in the mailbag at the gate, and leaves with whoever goes its way (Post/Outbox.cs).
            if (PostBook.On && !l.hostile && !NotableBook.Hostile(n))
            {
                Outbox.Put(l, p);
                if (l.ownRider) OwnRiders.SetOut(l);
                return;
            }
            Notify.Message("CC_Letter_Msg_Written".Translate(p.Named("PAWN"), l.toName.Named("TO")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            Pickup(l, n);
            NotableBook.Changed();
        }

        /// <summary>
        /// Handed to someone of their people at the colony (Outsiders/, the two face to face): it goes home with them,
        /// and is read once they're back, as long as the walk home takes their people.
        /// </summary>
        public static void HandedToBearer(NeighbourLetter l, Pawn by, Pawn bearer)
        {
            var n = Book?.Get(l?.notable ?? -1);
            if (l == null || n == null || l.stage != LetterStage.AwaitMessenger) return;
            float tiles = Tiles(n, l.map);
            // Carried home by one of theirs, at their people's means; the answer comes back at the colony's (Means).
            float there = LetterMath.LegDays(tiles, LetterMath.MeansOf(Ages.Of(bearer?.Faction ?? n.faction)));
            l.backDays = LetterMath.LegDays(tiles, Means);
            l.stage = LetterStage.Travelling;
            l.stageTick = Now;
            // They set off for home when their stay ends: about half a day on.
            l.due = Now + Mathf.RoundToInt((there + 0.5f + NotableMath.ReplyDays(Rand.Value)) * GenDate.TicksPerDay);
            Say(by, "hand", n);
            // together: handed over face to face (the talk job brought the two together).
            Notify.Message("CC_Letter_Msg_HandedBearer".Translate(by.Named("PAWN"), l.toName.Named("TO"), bearer.Named("BEARER")).Resolve(), new LookTargets(bearer),
                MessageTypeDefOf.NeutralEvent, Importance.Important);
            NotableBook.Changed();
        }

        private static void Pickup(NeighbourLetter l, Notable n)
        {
            string label = "CC_Letter_Label_Out".Translate(l.WriterName.Named("PAWN"), l.toName.Named("TO")).Resolve();
            var trip = Couriers.MailPickup(l.map, n.faction, l.id, label);
            if (trip == null)
            {
                Cancel(l, "CC_Letter_Cancel_NoMessenger");
                return;
            }
            l.tripId = trip.id;
        }

        // ---------------- At the gate ----------------

        /// <summary>
        /// A messenger waits at the gate for a letter (from CourierTracker): the writer carries it out to them, or, after a
        /// while, anyone free.
        /// </summary>
        public static void AtGate(CourierTrip t, int now)
        {
            var l = Get(t.mailId);
            if (l == null || l.stage != LetterStage.AwaitMessenger || t.received) return;
            var courier = t.Bearers.FirstOrDefault();
            var map = t.map;
            if (courier == null || map == null) return;
            l.metAtGate = true;
            // Someone is on the way out with it already.
            if (map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == NotableDefOf.Council_HandLetter)) return;
            var by = l.writer != null && Free(l.writer, map) && l.writer.CanReach(courier, PathEndMode.Touch, Danger.Some) ? l.writer : null;
            if (by == null && t.arrived >= 0 && now - t.arrived > Sched.HourTicks * 2)
                by = Writers(map).Where(c => Free(c, map) && c.CanReach(courier, PathEndMode.Touch, Danger.Some))
                    .OrderBy(c => c.Position.DistanceToSquared(courier.Position)).FirstOrDefault();
            if (by == null) return;
            var job = JobMaker.MakeJob(NotableDefOf.Council_HandLetter, courier);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The letter is in the messenger's hands: it sets off, and is read when it gets there.</summary>
        public static void Handed(Pawn by, Pawn courier)
        {
            var t = CourierTracker.Instance?.TripOf(courier);
            var l = t != null ? Get(t.mailId) : null;
            var n = l != null ? Book?.Get(l.notable) : null;
            if (t == null || l == null || n == null || t.received || l.stage != LetterStage.AwaitMessenger) return;
            t.received = true;
            Say(by, "hand", n);
            CourierWords.Say(courier, t, "receive");
            // On the road by the colony's means in its age (a runner, a rider, a courier, a courier by shuttle), there and
            // back: days, not weeks (phase 6 of the review). A letter already on the road keeps the day it was given.
            float tiles = Tiles(n, l.map);
            float there = LetterMath.LegDays(tiles, Means);
            l.backDays = LetterMath.LegDays(tiles, Means);
            l.stage = LetterStage.Travelling;
            l.stageTick = Now;
            l.due = Now + Mathf.RoundToInt((there + NotableMath.ReplyDays(Rand.Value)) * GenDate.TicksPerDay);
            NotableBook.Changed();
        }

        /// <summary>How the colony sends its letters, by its age (its means): a runner, a rider, a courier, a courier by shuttle.</summary>
        public static LetterMeans Means => LetterMath.MeansOf(Ages.Ours);

        private static float Tiles(Notable n, Map map)
        {
            var town = NotableBook.TownOf(n.town);
            return town != null && map != null ? LivingEvents.Distance(map.Tile, town.Tile) : 20f;
        }

        /// <summary>A messenger brought an answer to the gate (from CourierTracker): a word as they hand it over.</summary>
        public static void Delivered(CourierTrip t)
        {
            // Those who set things down had their word as they did (CourierTracker.Unload).
            if (t.unloaded > 0 || t.letterText.NullOrEmpty()) return;
            var first = t.Bearers.FirstOrDefault();
            if (first != null) CourierWords.Say(first, t, "deliver");
        }

        /// <summary>
        /// The messenger bringing an answer was set upon on the road (from CourierTracker, as they set out): they come in
        /// with a cut to an arm or a leg, and the letter says so. Tended at the colony, their people hear of it the game's
        /// own way.
        /// </summary>
        public static void Waylaid(CourierTrip t, Pawn p)
        {
            if (t == null || p?.health == null) return;
            try
            {
                var part = p.health.hediffSet.GetNotMissingParts()
                    .Where(x => x.depth == BodyPartDepth.Outside && (x.def.defName == "Arm" || x.def.defName == "Leg" || x.def.defName == "Shoulder"))
                    .RandomElementWithFallback();
                if (part == null) return;
                var h = HediffMaker.MakeHediff(HediffDefOf.Cut, p, part);
                h.Severity = Rand.Range(3f, 6f);
                p.health.AddHediff(h, part);
                if (!t.letterText.NullOrEmpty()) t.letterText += "\n\n" + "CC_Letter_Waylaid".Translate(p.Named("PAWN")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter waylaid", e);
            }
        }

        /// <summary>A dangerous road to them: enemies of the colony camped or settled near their town.</summary>
        private static bool DangerousRoad(Notable n)
        {
            var town = NotableBook.TownOf(n.town);
            if (town == null) return false;
            foreach (var site in Find.WorldObjects.Sites)
                if (site?.Faction != null && site.Faction.HostileTo(Faction.OfPlayer) && site.Tile.Layer == town.Tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(site.Tile, town.Tile) <= 12f)
                    return true;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s?.Faction != null && s != town && s.Faction.HostileTo(Faction.OfPlayer) && s.Faction.HostileTo(n.faction) && s.Tile.Layer == town.Tile.Layer
                    && Find.WorldGrid.ApproxDistanceInTiles(s.Tile, town.Tile) <= 6f)
                    return true;
            return false;
        }

        // ---------------- The answer ----------------

        /// <summary>
        /// They have read it (or heard it on the console): the answer, what it changes on their side and the colony's, and
        /// how it comes back. Returns the tone (for what the caller says as the call ends).
        /// </summary>
        private static AnswerTone Read(NeighbourLetter l, Notable n, Pawn speaker, int now, string how = null)
        {
            var x = Input(n, speaker, l.purpose, l.channel);
            var tone = NotableMath.Answer(x, Rand.Value, Rand.Value);
            // Stage 2's purposes (Diplomacy/DiplomacyLetters.cs): what they do about it, which colours the answer's tone.
            var dip = DiplomacyLetters.Read(l, n, speaker ?? l.writer, ref tone);
            // An invitation to a feast with guests (Diplomacy/FeastLetters.cs): their answer to it, which sets the tone.
            string feast = FeastLetters.Read(l, n, speaker ?? l.writer, ref tone);
            // What the answer brings beside its words (phase 6 of the review, World/LetterMath.cs): a token, how their town
            // fares, their people's memory of it, a debt of honour; a debt only where debts of honour are kept.
            var brings = LetterMath.Brings(l.purpose, tone);
            if (brings == AnswerBrings.FavourOwed && !CanOwe(n.faction)) brings = AnswerBrings.Remembered;
            // A call never went by road: how far they are, at their people's means, still sets when a guest or raiders could come.
            if (l.backDays <= 0f) l.backDays = LetterMath.LegDays(Tiles(n, l.map), LetterMath.MeansOf(Ages.Of(n.faction)));
            l.tone = tone;
            float factor = NotableMath.ChannelFactor(l.purpose, l.channel, x.traditional);
            float shift = NotableMath.OpinionShift(tone, l.purpose, factor, x.lately);
            bool granted = NotableMath.Grants(x, tone);
            // Their side: what they now think of the colony and the one who wrote, and the letter remembered.
            if (tone != AnswerTone.Silence)
            {
                n.personal += shift;
                if (KinMemory.Ours(speaker)) n.RegardOf(speaker, true).personal += NotableMath.WriterShift(shift);
            }
            n.SetLast(l.purpose, now);
            if (!l.topicKey.NullOrEmpty() && !n.done.Contains(l.topicKey)) n.done.Add(l.topicKey);
            while (n.done.Count > 12) n.done.RemoveAt(0);
            n.Remember(NoteText(l, tone, speaker), shift);
            // Their people's goodwill, a little (never into a war).
            int gw = NotableMath.GoodwillShift(tone, l.purpose, x.hostile);
            if (gw != 0) WorldSituationWorker.Goodwill(n.faction, gw, false);
            // What their people remember of it: a warning heeded, mourning shared, help the colony now owes them for.
            if (tone != AnswerTone.Silence && tone != AnswerTone.Refusal)
            {
                // Heeded warmly: a debt of honour, made with its own weight when the answer reaches the colony (Owed).
                if (l.purpose == LetterPurpose.Warning)
                {
                    if (brings == AnswerBrings.FavourOwed) l.owed = true;
                    else KinMemory.Note(n.faction, "warned", speaker, l.topic);
                }
                else if (l.purpose == LetterPurpose.Condolences && tone <= AnswerTone.Cordial)
                    KinMemory.Note(n.faction, "mourned", speaker, l.topicKey != null && l.topicKey.StartsWith("loss:") ? l.topicKey.Substring(5) : null, 4);
            }
            if (l.purpose == LetterPurpose.Help && granted) FactionMemory.Note(n.faction, "letterAid", HelpLabel(l.help));
            // What comes back with it: carried to the gate by their messenger.
            var things = new List<Thing>();
            if (dip != null) things.AddRange(dip.things);
            if (l.purpose == LetterPurpose.Help && granted) things.AddRange(HelpThings(l.help, n.faction, l.map));
            else if (brings == AnswerBrings.Token) things.AddRange(Loot.Make(LetterMath.TokenValue(Rand.Value) * LivingEvents.WealthFactor(l.map), false));
            else if (NotableMath.Gift(tone, l.purpose, NotableBook.Opinion(n), Rand.Value))
                things.AddRange(Loot.Make(Rand.Range(80f, 160f) * LivingEvents.WealthFactor(l.map), false));
            string visit = null;
            bool noTraveller = false;
            if (l.purpose == LetterPurpose.Invitation && granted)
            {
                visit = feast ?? PlanVisit(l, n, now);
                noTraveller = visit == null;
            }
            // A feast declined says so in their own words (Feasts.Answer's no), not the plain "won't be coming".
            else if (feast != null) visit = feast;
            // How far it moved them, in words, and why when it went badly (World/LetterMath.cs).
            var said = new Said
            {
                brings = brings, goodwill = gw, before = x.opinion, after = NotableBook.Opinion(n), shift = shift, noTraveller = noTraveller,
                why = LetterMath.SaysWhy(tone) && !LetterMath.OwnReasons(l.purpose) || l.purpose == LetterPurpose.Invitation && visit == null && feast == null
                    ? LetterMath.Why(x) : (LetterWhy?)null
            };
            // A people at war may answer with a threat, and keep it.
            string threat = null;
            if (tone != AnswerTone.Silence && Raids.CanRaid(n.faction) && IncidentWorker_Happening.FightsPossible && l.map != null
                && Rand.Chance(NotableMath.ThreatChance(tone, x.hostile)))
            {
                threat = "CC_Letter_Threat".Translate(n.Name.Named("NAME"), (n.faction?.Name ?? "").Named("FACTION")).Resolve();
                CouncilGameComponent.Instance?.happenings?.AddRaid(l.map, n.faction, 0.6f, (l.backDays + Rand.Range(2f, 5f)) * 24f,
                    "CC_Letter_ThreatRaid_Label".Translate(n.Name.Named("NAME")).Resolve(),
                    "CC_Letter_ThreatRaid_Text".Translate(n.Name.Named("NAME"), (n.faction?.Name ?? "").Named("FACTION"), l.WriterName.Named("PAWN")).Resolve());
            }
            // A messenger on a dangerous road may be set upon and come in hurt.
            bool waylaid = l.channel == LetterChannel.Courier && tone != AnswerTone.Silence && Rand.Chance(NotableMath.WaylaidChance(DangerousRoad(n)));
            if (tone == AnswerTone.Silence && l.channel == LetterChannel.Console)
            {
                // A people at war hears them out and says nothing back.
                // apart: the call's end, told in a message.
                Notify.Message("CC_Call_Msg_NoAnswer".Translate((speaker?.LabelShort ?? l.WriterName).Named("PAWN"), n.Name.Named("NAME")).Resolve(),
                    KinMemory.Ours(speaker) ? new LookTargets(speaker) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                Finish(l, n);
                return tone;
            }
            if (tone == AnswerTone.Silence && l.ownRider)
            {
                // The colony's rider is heard out and comes home with nothing (Post/OwnRiders.cs).
                OwnRiders.Answered(l, null, null, null, null, null);
                l.stage = LetterStage.Answering;
                l.stageTick = now;
                NotableBook.Changed();
                return tone;
            }
            if (tone == AnswerTone.Silence)
            {
                l.stage = LetterStage.Answering;
                l.tripId = -1;
                l.due = now + Mathf.RoundToInt((l.backDays * 2f + 2f) * GenDate.TicksPerDay);
                l.stageTick = now;
                NotableBook.Changed();
                return tone;
            }
            string title = Title(l, n, how);
            string text = Body(l, n, speaker, tone, things, visit, how, dip?.line, said);
            if (threat != null) text += "\n\n" + threat;
            var def = tone <= AnswerTone.Cordial ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent;
            string chronicle = "CC_Letter_Chronicle".Translate(l.WriterName.Named("PAWN"), n.Name.Named("NAME"), ToneWord(tone).Named("TONE")).Resolve();
            if (l.channel == LetterChannel.Courier && l.ownRider)
            {
                // Read at their town with the colony's rider there: the answer, and what it sends, ride home with them (Post/OwnRiders.cs).
                OwnRiders.Answered(l, title, text, def, chronicle, things);
                l.tripId = -1;
            }
            else if (l.channel == LetterChannel.Courier && things.Count == 0 && !l.hostile
                     && (l.postId = MailIn.Add(l.map, n.faction, NotableBook.TownOf(n.town), title, text, def, chronicle, KinMemory.Ours(l.writer) ? l.writer : null, l.id,
                         now + Mathf.RoundToInt(l.backDays * GenDate.TicksPerDay), PostMath.UrgencyOf(l.purpose), "CC_Letter_Label_Back".Translate(n.Name.Named("NAME")).Resolve())) >= 0)
            {
                // Words only: the answer comes with whoever is coming this way, or a messenger after a while (Post/MailIn.cs).
                l.tripId = -1;
            }
            else if (l.channel == LetterChannel.Courier)
            {
                string label = "CC_Letter_Label_Back".Translate(n.Name.Named("NAME")).Resolve();
                int arrive = now + Mathf.RoundToInt(l.backDays * GenDate.TicksPerDay);
                var trip = Couriers.MailAnswer(l.map, n.faction, things, l.id, arrive, label, title, text, def, chronicle, KinMemory.Ours(l.writer) ? l.writer : null);
                if (trip == null)
                {
                    // Nobody to carry it (no people at peace left): it comes the old way, with what it brings set down.
                    SendNow(l, title, text, def, chronicle, things);
                }
                else
                {
                    l.tripId = trip.id;
                    trip.hurt = waylaid;
                }
            }
            else SendNow(l, title, text, def, chronicle, things);
            // Told at once (a call, or nobody to carry it): what it says they owe is owed at once.
            bool carried = l.tripId >= 0 || l.postId >= 0 || l.ownRider;
            if (!carried) Owed(l, n);
            if (visit == null || feast != null)
            {
                l.stage = l.channel == LetterChannel.Courier && carried ? LetterStage.Answering : LetterStage.Done;
            }
            l.stageTick = now;
            NotableBook.Changed();
            return tone;
        }

        /// <summary>The answer comes as a letter now (a call, or nobody to carry it); what it brings comes by courier.</summary>
        private static void SendNow(NeighbourLetter l, string title, string text, LetterDef def, string chronicle, List<Thing> things)
        {
            var map = l.map ?? HappeningTracker.HomeMap();
            LookTargets look = LookTargets.Invalid;
            if (KinMemory.Ours(l.other) && l.other.Spawned) look = new LookTargets(l.other);
            else if (KinMemory.Ours(l.writer) && l.writer.Spawned) look = new LookTargets(l.writer);
            Tell.Letter(NewsKind.World, Importance.Notable, null, title, text, def, look);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, chronicle, KinMemory.Ours(l.writer) ? l.writer : null));
            if (things.Count > 0 && map != null)
            {
                // Words go down the line, things don't: a courier of theirs brings what they send.
                var n = Book?.Get(l.notable);
                if (Couriers.MailAnswer(map, n?.faction, things, l.id, Now, null, null, null, null, null, null) == null)
                    Couriers.Loot(things, map, DropCellFinder.TradeDropSpot(map), n?.faction);
            }
            l.tripId = -1;
        }

        private static string HelpLabel(string help) => (help == "food" ? "CC_Letter_Help_Food" : "CC_Letter_Help_Medicine").Translate().Resolve();

        /// <summary>
        /// The help they send (Things/ThingRoles.cs): medicine of a kind the colony knows (of their age or the colony's,
        /// whichever is the earlier), or food that keeps on the road of their age, as much as 40 to 65 pemmican is worth.
        /// </summary>
        private static List<Thing> HelpThings(string help, Faction f, Map map)
        {
            if (help == "food")
            {
                var def = ThingRoles.From(ThingRole.Provisions, f, map) ?? ThingDefOf.RawPotatoes;
                var pemmican = ThingDefOf.Pemmican;
                int count = def == ThingDefOf.RawPotatoes ? Rand.RangeInclusive(60, 90)
                    : def == pemmican || pemmican == null ? Rand.RangeInclusive(40, 65)
                    : ThingRoles.CountFor(Rand.RangeInclusive(40, 65) * pemmican.BaseMarketValue, def, 3);
                return Couriers.Make(def, count);
            }
            var age = Ages.Of(f) < Ages.Of(map) ? Ages.Of(f) : Ages.Of(map);
            if (Avail.IsPreIndustrial && age > WorldAge.Medieval) age = WorldAge.Medieval;
            var med = ThingRoles.Pick(ThingRole.Medicine, age) ?? ThingDefOf.MedicineHerbal;
            // As much as the age's own medicine always came to (fewer of a dearer kind).
            return Couriers.Make(med, ThingRoles.CountFor(Rand.RangeInclusive(4, 8) * (ThingRoles.Fallback(ThingRole.Medicine, age) ?? med).BaseMarketValue, med, 2));
        }

        // ---------------- Words ----------------

        private static string PurposeWord(LetterPurpose p) => ("CC_Letter_Purpose_" + p).Translate().Resolve();

        public static string ToneWord(AnswerTone t) => ("CC_Letter_Tone_" + t).Translate().Resolve();

        private static string NoteText(NeighbourLetter l, AnswerTone tone, Pawn speaker)
        {
            string how = l.channel == LetterChannel.Console ? "CC_Notable_Note_Call" : "CC_Notable_Note_Letter";
            return how.Translate((speaker?.LabelShort ?? l.WriterName).Named("PAWN"), PurposeWord(l.purpose).Named("PURPOSE"), ToneWord(tone).Named("TONE")).Resolve();
        }

        private static string Title(NeighbourLetter l, Notable n, string how)
        {
            string key = how == "callback" ? "CC_Call_Title_Back" : how == "word" ? "CC_Call_Title_Word"
                : l.channel == LetterChannel.Console ? "CC_Call_Title" : "CC_Letter_Title";
            return key.Translate(n.Name.Named("NAME")).Resolve();
        }

        private static string Body(NeighbourLetter l, Notable n, Pawn speaker, AnswerTone tone, List<Thing> things, string visit, string how, string dip, Said said)
        {
            string town = n.townName ?? n.faction?.Name ?? "";
            string title = NotableBook.TitleOf(n);
            string introKey = how == "callback" ? "CC_Call_Intro_Back" : how == "word" ? "CC_Call_Intro_Word"
                : l.channel == LetterChannel.Console ? "CC_Call_Intro" : l.hostile ? "CC_Letter_Intro_War" : "CC_Letter_Intro";
            var parts = new List<string>
            {
                introKey.Translate(n.Name.Named("NAME"), title.Named("TITLE"), town.Named("TOWN"), (speaker?.LabelShort ?? l.WriterName).Named("PAWN"),
                    PurposeWord(l.purpose).Named("PURPOSE"), (n.faction?.Name ?? "").Named("FACTION")).Resolve()
            };
            // What is true between the colony and them, for their words and their news (the facts service, Text/Facts/).
            bool news = l.purpose == LetterPurpose.News && tone != AnswerTone.Refusal;
            var facts = Facts.For(FactScene.Letter(n, speaker ?? l.writer, l.map, news || said?.brings == AnswerBrings.TownNews));
            // Help is in their words only when it comes with the answer (World/NotableMath.cs, Grants): what they send is what the letter says.
            string quote = Quote(l, n, tone, speaker, facts, l.purpose == LetterPurpose.Help && things.Count > 0);
            if (!quote.NullOrEmpty()) parts.Add("\"" + quote + "\"");
            if (news) parts.Add(News(n, tone, facts));
            if (l.purpose == LetterPurpose.Help)
                parts.Add(things.Count > 0 ? (l.channel == LetterChannel.Console ? "CC_Letter_HelpSent_Call" : "CC_Letter_HelpSent").Translate(Couriers.Describe(things).Named("GOODS")).Resolve()
                    : "CC_Letter_HelpNot".Translate(n.Name.Named("NAME")).Resolve());
            else if (things.Count > 0)
                parts.Add((l.channel == LetterChannel.Console ? "CC_Letter_Gift_Call" : "CC_Letter_Gift").Translate(Couriers.Describe(things).Named("GOODS")).Resolve());
            if (l.purpose == LetterPurpose.Invitation)
                parts.Add(visit ?? "CC_Letter_InviteNot".Translate(n.Name.Named("NAME")).Resolve());
            // What they did about a debt of honour called in, or the colony's word given (Diplomacy/DiplomacyLetters.cs).
            if (!dip.NullOrEmpty()) parts.Add(dip);
            // What else it brings, why not when it went badly, and how far it moved them (phase 6 of the review).
            string writer = speaker?.LabelShort ?? l.WriterName;
            string brought = Brought(l, n, said, facts);
            if (!brought.NullOrEmpty()) parts.Add(brought);
            string why = WhyLine(l, n, said, writer);
            if (!why.NullOrEmpty()) parts.Add(why);
            parts.Add(RegardLine(n, tone, said, writer));
            return string.Join("\n\n", parts);
        }

        /// <summary>What a letter's answer did beside its words, worked out as it is read, for its words.</summary>
        private sealed class Said
        {
            public AnswerBrings brings;
            /// <summary>Their people's goodwill moved by this much.</summary>
            public int goodwill;
            /// <summary>Their regard of the colony before and after, and their own shift.</summary>
            public int before, after;
            public float shift;
            /// <summary>Why it went badly, when it did and the purpose has no reason of its own.</summary>
            public LetterWhy? why;
            /// <summary>An invitation taken up with nobody free to travel.</summary>
            public bool noTraveller;
        }

        /// <summary>A debt of honour can be kept with them: favours owed on, and a people the colony deals with.</summary>
        private static bool CanOwe(Faction f) => f != null && FavoursOwed.On && Diplomacy.Deals(f) && FavourMath.Earns("warned", true, LetterMath.HeededWarmly);

        /// <summary>
        /// A warning heeded warmly, its answer in: their people remember it with the weight of a debt of honour, owed to the
        /// colony (and named for the one who wrote), told in the answer's own words rather than on its own.
        /// </summary>
        private static void Owed(NeighbourLetter l, Notable n)
        {
            if (l == null || !l.owed || n?.faction == null) return;
            l.owed = false;
            try
            {
                FavoursOwed.Quietly(() => KinMemory.Note(n.faction, "warned", KinMemory.Ours(l.writer) ? l.writer : null, l.topic, LetterMath.HeededWarmly));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter: a warning's debt", e);
            }
        }

        /// <summary>What a letter of kind words brings beside them, in words (World/LetterMath.cs, Brings).</summary>
        private static string Brought(NeighbourLetter l, Notable n, Said said, FactSet facts)
        {
            if (said == null) return null;
            string people = n.faction?.Name ?? "";
            string line = null;
            switch (said.brings)
            {
                case AnswerBrings.TownNews:
                {
                    var rec = n.town >= 0 && RegionBook.FortunesOn ? WorldLedger.TownRecord(n.town) : null;
                    string fortune = rec != null ? RegionFortunes.Line(rec) : null;
                    string item = facts?.news?.FirstOrDefault();
                    if (!fortune.NullOrEmpty() && !(n.townName ?? "").NullOrEmpty())
                        line = "CC_LetterBring_Town".Translate(n.Name.Named("NAME"), n.townName.Named("TOWN"), fortune.UncapitalizeFirst().Named("FORTUNE")).Resolve();
                    else if (!item.NullOrEmpty()) line = "CC_LetterBring_TownNews".Translate(n.Name.Named("NAME"), item.Named("NEWS")).Resolve();
                    else line = "CC_LetterBring_TownQuiet".Translate(n.Name.Named("NAME")).Resolve();
                    break;
                }
                case AnswerBrings.Remembered:
                    line = (l.purpose == LetterPurpose.Condolences ? "CC_LetterBring_Mourned" : "CC_LetterBring_Heeded").Translate(people.Named("PEOPLE")).Resolve();
                    break;
                case AnswerBrings.FavourOwed:
                {
                    var form = DipWords.Form(l.map ?? Home, n.faction);
                    line = "CC_LetterBring_Owed".Translate(people.Named("PEOPLE"), form.word.Named("WORD"), l.WriterName.Named("PAWN")).Resolve();
                    break;
                }
            }
            if (said.goodwill > 0)
            {
                string gw = (LetterMath.GoodwillMarked(said.goodwill) ? "CC_LetterBring_GoodwillMarked" : "CC_LetterBring_Goodwill").Translate(people.Named("PEOPLE")).Resolve();
                line = line.NullOrEmpty() ? gw : line + " " + gw;
            }
            return line;
        }

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        /// <summary>Why a curt answer or a refusal went as it did (or an invitation not taken up), in words.</summary>
        private static string WhyLine(NeighbourLetter l, Notable n, Said said, string writer)
        {
            if (said == null) return null;
            if (said.noTraveller) return "CC_LetterWhy_NoTraveller".Translate(n.Name.Named("NAME"), (n.townName ?? n.faction?.Name ?? "").Named("TOWN")).Resolve();
            if (said.why == null) return null;
            return ("CC_LetterWhy_" + said.why.Value).Translate(n.Name.Named("NAME"), RegardWord(said.before).Named("LEVEL"), writer.Named("WRITER"),
                (n.townName ?? n.faction?.Name ?? "").Named("TOWN")).Resolve();
        }

        /// <summary>Their regard as one word ("civil", "warm"), for the answer's words.</summary>
        private static string RegardWord(int opinion) => ("CC_LetterRegard_Level_" + NotableMath.Level(opinion)).Translate().Resolve();

        /// <summary>How far the answer moved their regard of the colony, in words: the band before and after, or how much within it.</summary>
        private static string RegardLine(Notable n, AnswerTone tone, Said said, string writer)
        {
            if (said == null) return ("CC_Letter_Outcome_" + tone).Translate(n.Name.Named("NAME"), writer.Named("PAWN")).Resolve();
            float moved = said.after != said.before ? said.after - said.before : said.shift;
            var move = LetterMath.Move(said.before, said.after, moved);
            string line = ("CC_LetterRegard_" + move).Translate(n.Name.Named("NAME"), RegardWord(said.before).Named("OLD"), RegardWord(said.after).Named("NOW")).Resolve();
            if (tone == AnswerTone.Warm) line += " " + "CC_LetterRegard_Writer".Translate(n.Name.Named("NAME"), writer.Named("PAWN")).Resolve();
            return line;
        }

        /// <summary>
        /// Their own words: a line that fits what it was for, how they took it, their people's ways and the way it went,
        /// and (for lines written for them) the ties between the two sides and what happened at their town or to them
        /// lately (<paramref name="facts"/>: tie:KIND, town:lately..., with {FAVOURFOR} and the like). A line that names
        /// none of those is chosen as it always was. Help asked for is answered by the answer really given: with the help
        /// sent, by its tone; without it, in the refusal's words, whatever the tone (<paramref name="helpSent"/>).
        /// </summary>
        private static string Quote(NeighbourLetter l, Notable n, AnswerTone tone, Pawn speaker, FactSet facts, bool helpSent = false)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(WritePool);
            if (pool == null) return null;
            var said = l.purpose == LetterPurpose.Help ? NotableMath.HelpQuoteTone(tone, helpSent) : tone;
            var tags = new HashSet<string> { l.purpose.ToString().ToLowerInvariant(), said.ToString().ToLowerInvariant() };
            tags.Add(Traditional(n.faction) ? "theirLowtech" : "theirHightech");
            // One of Rimfolk's peoples writes in its own way: their:traders, their:healers, their:herders (Peoples/).
            Peoples.AddTags(n.faction, tags);
            tags.Add(l.channel == LetterChannel.Console ? "call" : "letter");
            if (l.hostile) tags.Add("war");
            if (l.help == "food") tags.Add("food");
            if (l.help == "medicine") tags.Add("medicine");
            if (facts != null) tags.UnionWith(facts.tags);
            // What only stage 2's purposes know: a pact ended, how a truce is to be sworn (Diplomacy/DiplomacyLetters.cs).
            DiplomacyLetters.QuoteTags(l, n, tags);
            var lines = pool.lines.For(Tone.Plain);
            string line = Flavor.PickTagged(lines.Where(x => LineTags.Of(x).tags?.Contains(l.purpose.ToString().ToLowerInvariant()) ?? false), tags)
                          ?? Flavor.PickTagged(lines, tags);
            if (line.NullOrEmpty()) return null;
            var values = facts != null ? new Dictionary<string, string>(facts.values) : new Dictionary<string, string>();
            values["WRITER"] = speaker?.LabelShort ?? l.WriterName;
            values["NAME"] = n.Name;
            values["TOWN"] = n.townName ?? "";
            values["FACTION"] = n.faction?.Name ?? "";
            values["TOPIC"] = l.topic ?? "";
            values["HELP"] = HelpLabel(l.help);
            return Texts.Fill(line, values);
        }

        /// <summary>
        /// What they tell of when asked for news: real things near them (a world event, a place, a quest of theirs, their
        /// wars), the world's news the facts service read for the letter.
        /// </summary>
        private static string News(Notable n, AnswerTone tone, FactSet facts)
        {
            int max = tone <= AnswerTone.Cordial ? 3 : tone == AnswerTone.Cool ? 2 : 1;
            var items = facts.news;
            if (items.Count == 0) return "CC_News_Nothing".Translate().Resolve();
            return "CC_News_Head".Translate(n.Name.Named("NAME")).Resolve() + "\n" + string.Join("\n", items.Take(max).Select(i => "  - " + i));
        }

        // ---------------- An invitation ----------------

        /// <summary>Who will come: the notable if they can travel, else (a people's leader can't leave their seat) someone of their town who can. The words, or null.</summary>
        private static string PlanVisit(NeighbourLetter l, Notable n, int now)
        {
            var who = LivingEvents.FreeToTravel(n.pawn) ? n : Book?.notables.FirstOrDefault(o => o.town == n.town && o != n && o.Alive && LivingEvents.FreeToTravel(o.pawn)
                                                                                                   && (o.role == NotableRole.Heir || o.role == NotableRole.Envoy));
            if (who == null) return null;
            l.other = who.pawn;
            l.stage = LetterStage.Visiting;
            l.due = now + Mathf.RoundToInt((l.backDays * 2f + Rand.Range(1f, 3f)) * GenDate.TicksPerDay);
            l.tries = 0;
            return who == n ? "CC_Letter_InviteYes".Translate(n.Name.Named("NAME")).Resolve()
                : "CC_Letter_InviteSends".Translate(n.Name.Named("NAME"), who.Name.Named("OTHER"), NotableBook.TitleOf(who).Named("TITLE")).Resolve();
        }

        private static void TryVisit(NeighbourLetter l, Notable n, int now)
        {
            var x = l.other;
            var map = l.map;
            var f = n.faction;
            bool can = x != null && LivingEvents.FreeToTravel(x) && x.Faction == f && LivingEvents.Friendly(f) && map != null && Find.Maps.Contains(map);
            if (!can)
            {
                // apart: a guest who can't come, told in a message.
                Notify.Message("CC_Letter_Msg_VisitOff".Translate((x?.LabelShort ?? n.Name).Named("NAME")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                Finish(l, n);
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 8 || hour > 15 || PlayOuts.WhyNotNow(map, false) != null || !CourierTracker.Safe(map))
            {
                l.tries++;
                if (l.tries > 48) Finish(l, n);
                return;
            }
            var lord = Arrivals.Visit(new List<Pawn> { x }, map, f, Gathering(map), Rand.Range(8f, 14f));
            if (lord == null)
            {
                l.tries++;
                if (l.tries > 48) Finish(l, n);
                return;
            }
            l.stage = LetterStage.Guesting;
            l.stageTick = now;
            // Their answer still in the post: the guest brings it themselves (Post/MailIn.cs).
            if (l.postId >= 0) MailIn.Hand(l.postId, x);
            Notify.Message("CC_Letter_Msg_Guest".Translate(x.Named("NAME"), (n.townName ?? f.Name).Named("TOWN")).Resolve(), new LookTargets(x), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            Say(x, "guest", n);
            NotableBook.Changed();
        }

        private static void Guesting(NeighbourLetter l, Notable n, int now)
        {
            var x = l.other;
            if (x != null && x.Spawned && !x.Dead && now - l.stageTick < GenDate.TicksPerDay * 2) return;
            // Home again: the visit is remembered on their side.
            var guest = Book?.notables.FirstOrDefault(o => o.pawn == x) ?? n;
            if (x != null && !x.Dead && x.Faction == n.faction)
            {
                guest.personal += 5f;
                guest.Remember("CC_Notable_Note_Visited".Translate(l.WriterName.Named("PAWN")).Resolve(), 5f);
                if (KinMemory.Ours(l.writer)) guest.RegardOf(l.writer, true).personal += 5f;
                FactionMemory.Note(n.faction, "welcomed", x.LabelShort);
            }
            Finish(l, n);
        }

        /// <summary>Where a guest spends their hours: a party spot or campfire, else a dining table, else just outside.</summary>
        private static IntVec3? Gathering(Map map)
        {
            var spot = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def.defName == "PartySpot" || b.def.defName == "Campfire");
            if (spot != null) return spot.Position;
            return Avail.ColonyBuildings(map, Avail.IsEatingTable).FirstOrDefault()?.Position;
        }

        // ---------------- A call ----------------

        private static void TryCall(NeighbourLetter l, int now)
        {
            var p = l.writer;
            var map = p?.Map;
            if (map == null || !map.IsPlayerHome || !Free(p, map)) return;
            var console = Console(map, p);
            if (console == null || !p.CanReserveAndReach(console, PathEndMode.InteractionCell, Danger.Some)) return;
            var job = JobMaker.MakeJob(NotableDefOf.Council_NeighbourCall, console);
            job.ignoreJoyTimeAssignment = true;
            l.stage = LetterStage.Calling;
            l.stageTick = now;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (p.CurJobDef != NotableDefOf.Council_NeighbourCall) Back(l);
            NotableBook.Changed();
        }

        /// <summary>
        /// The call went through (from the call's job): the notable answers at once, or an aide takes a message and they
        /// call back. The moment for the caller's last line: a tone, or "aide".
        /// </summary>
        public static string CallMade(NeighbourLetter l, Pawn p)
        {
            var n = Book?.Get(l?.notable ?? -1);
            if (l == null || n == null || l.stage != LetterStage.Calling) return null;
            if (Console(p.Map, p) == null)
            {
                // The air went dead during the call: they'll try again.
                Back(l);
                return "dead";
            }
            int now = Now;
            var town = NotableBook.TownOf(n.town);
            int hour = town != null ? GenLocalDate.HourOfDay(town.Tile) : 12;
            bool troubled = WorldSituations.Instance?.HardTimes(n.faction) ?? false;
            var reach = NotableMath.Reach(hour, troubled, PawnUtility.IsFactionLeader(n.pawn), Rand.Value);
            l.reach = reach;
            if (reach == CallReach.AtOnce)
            {
                var tone = Read(l, n, p, now);
                return tone.ToString().ToLowerInvariant();
            }
            l.stage = LetterStage.CallbackWait;
            l.stageTick = now;
            l.tries = 0;
            l.due = now + Mathf.RoundToInt(NotableMath.CallbackHours(reach, hour, Rand.Value) * GenDate.TicksPerHour);
            // apart: the call's end at the console, told in a message.
            Notify.Message(("CC_Call_Msg_Aide_" + reach).Translate(p.Named("PAWN"), n.pawn.Named("NAME")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            NotableBook.Changed();
            return "aide";
        }

        /// <summary>The console rings: the one who called answers if they can, else whoever is free nearest it.</summary>
        private static void TryAnswer(NeighbourLetter l, int now)
        {
            var map = l.map;
            var console = Console(map);
            if (console == null) return;
            if (map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == NotableDefOf.Council_AnswerCall)) return;
            Pawn by = l.writer != null && Free(l.writer, map) && l.writer.CanReserveAndReach(console, PathEndMode.InteractionCell, Danger.Some) ? l.writer : null;
            by ??= Writers(map).Where(c => Free(c, map) && c.CanReserveAndReach(console, PathEndMode.InteractionCell, Danger.Some))
                .OrderBy(c => c.Position.DistanceToSquared(console.Position)).FirstOrDefault();
            if (by == null) return;
            var job = JobMaker.MakeJob(NotableDefOf.Council_AnswerCall, console);
            job.ignoreJoyTimeAssignment = true;
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Someone took the callback (from the job): the answer, in their bubbles and the letter. The moment for their last line.</summary>
        public static string CallAnswered(Pawn p)
        {
            var l = Book?.letters.FirstOrDefault(x => x.stage == LetterStage.Ringing && x.map == p.Map);
            var n = l != null ? Book?.Get(l.notable) : null;
            if (l == null || n == null) return null;
            l.other = p;
            var tone = Read(l, n, p, Now, "callback");
            return tone.ToString().ToLowerInvariant();
        }

        /// <summary>A ringing console's letter, for the one who goes to answer it.</summary>
        public static Notable Ringing(Map map)
        {
            var l = Book?.letters.FirstOrDefault(x => x.stage == LetterStage.Ringing && x.map == map);
            return l != null ? Book?.Get(l.notable) : null;
        }

        /// <summary>Nobody took the callback: they left word, and the answer comes as their message.</summary>
        private static void LeftWord(NeighbourLetter l, Notable n, int now) => Read(l, n, l.writer, now, "word");

        // ---------------- Speech ----------------

        /// <summary>A line over someone's head at a moment of a letter or a call (one sentence, from the pool Rimfolk_Pool_NeighbourSays).</summary>
        public static void Say(Pawn p, string moment, Notable n)
        {
            if (p == null || !p.Spawned || moment.NullOrEmpty()) return;
            try
            {
                var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(SayPool);
                if (pool == null) return;
                var tags = Flavor.TagsFor(p);
                tags.Add(moment);
                if (n?.faction != null) tags.Add(Traditional(n.faction) ? "theirLowtech" : "theirHightech");
                var tone = ToneUtility.ToneFor(p);
                var lines = pool.lines.For(tone);
                string line = Flavor.PickTagged(lines.Where(x => LineTags.Of(x).tags?.Contains(moment) ?? false), tags)
                              ?? Flavor.PickTagged(pool.lines.plain.Where(x => LineTags.Of(x).tags?.Contains(moment) ?? false), tags);
                if (line.NullOrEmpty()) return;
                line = Texts.Fill(line, ("NAME", n?.Name ?? ""), ("TOWN", n?.townName ?? ""));
                Speech.Say(p, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter words", e);
            }
        }
    }
}
