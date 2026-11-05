using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A social fight Rimfolk noted as it started: who threw the first punch and at whom (thing ids), and when.</summary>
    public class BanishFight : IExposable
    {
        public int starter;
        public int other;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref starter, "s");
            Scribe_Values.Look(ref other, "o");
            Scribe_Values.Look(ref tick, "t");
        }
    }

    /// <summary>Where a banishment the council decided stands on the map.</summary>
    public enum BanishStage
    {
        None,
        /// <summary>Waiting for the one who tells them to find them, face to face.</summary>
        ToTell,
        /// <summary>Told: gone to their room to gather their things.</summary>
        Gathering,
        /// <summary>Walked out by the edge of the map.</summary>
        Gone
    }

    /// <summary>Someone the council was asked about: its word and when, a "let it be", a banishment under way, and after.</summary>
    public class BanishRecord : IExposable
    {
        public Pawn pawn;
        public int id;
        public string name;
        /// <summary>When the council last stepped in (a word said, their nights apart), and how ("word", "apart", "last"); -1 never.</summary>
        public int stepTick = -1;
        public string step;
        /// <summary>No question about them until this tick (after "let it be"); -1 none.</summary>
        public int letBeUntil = -1;
        /// <summary>A word the council asked for, still to be said face to face: by whom, since when, and whether it's the last.</summary>
        public Pawn wordBy;
        public int wordAsked = -1;
        public bool wordLast;
        /// <summary>A banishment under way on the map: its stage, who tells them, since when, and the age's word for it.</summary>
        public BanishStage stage;
        public Pawn teller;
        public int stageTick = -1;
        public string word;
        /// <summary>After: when they went, whether they went with a grudge, rode with raiders once, or came home again.</summary>
        public int banishedTick = -1;
        public bool grudge;
        public bool rode;
        public bool cameBack;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (pawn != null && pawn.Discarded) pawn = null;
                if (wordBy != null && wordBy.Discarded) wordBy = null;
                if (teller != null && teller.Discarded) teller = null;
            }
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref stepTick, "stepTick", -1);
            Scribe_Values.Look(ref step, "step");
            Scribe_Values.Look(ref letBeUntil, "letBeUntil", -1);
            Scribe_References.Look(ref wordBy, "wordBy");
            Scribe_Values.Look(ref wordAsked, "wordAsked", -1);
            Scribe_Values.Look(ref wordLast, "wordLast");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref teller, "teller");
            Scribe_Values.Look(ref stageTick, "stageTick", -1);
            Scribe_Values.Look(ref word, "word");
            Scribe_Values.Look(ref banishedTick, "banishedTick", -1);
            Scribe_Values.Look(ref grudge, "grudge");
            Scribe_Values.Look(ref rode, "rode");
            Scribe_Values.Look(ref cameBack, "cameBack");
        }

        public string Name => pawn != null && !pawn.Discarded ? pawn.LabelShort : name ?? "?";
    }

    /// <summary>
    /// Banishment by the council (the player's request, 5 November 2026; DESIGN.md, "Banishment by the council"). The
    /// trouble someone causes is read from the game's own records (fight and insult memories) and Rimfolk's note of each
    /// social fight as it starts (<see cref="Notify_SocialFight"/>, which knows who threw the first punch); the rules are in
    /// <see cref="BanishMath"/>. The questions are Rimfolk_Dilemma_Trouble (a word first) and Rimfolk_Dilemma_Banish (if it
    /// goes on: Social/BanishDilemmas.cs); this keeps what the council decided and plays it out on the map, once an hour
    /// (the council's people pass): a word said face to face (Together), and a banishment told face to face, the things
    /// gathered in their room, and the walk out by the edge through the game's own banishment. Afterwards one sent away
    /// may come back: at the gate (the existing event, Council_Evt_Prodigal) or, rarely, among raiders with a grudge.
    /// </summary>
    public class BanishTracker : GameComponent
    {
        public List<BanishRecord> records = new List<BanishRecord>();
        public List<BanishFight> fights = new List<BanishFight>();

        /// <summary>At most this many people are remembered (the oldest settled ones go first), and this many fights.</summary>
        public const int MaxRecords = 24;
        public const int MaxFights = 200;
        /// <summary>A word the council asked for is looked for this long; then it's let go, never said.</summary>
        public const float WordWaitDays = 4f;
        /// <summary>A banishment waits this long for the one who tells them; then whoever is with them tells them.</summary>
        public const float TellWaitDays = 2f;
        /// <summary>They stay in their room gathering their things at least this long.</summary>
        public const int GatherTicks = GenDate.TicksPerHour;

        private static BanishTracker cached;
        private static Game cachedGame;

        public static BanishTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<BanishTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public BanishTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The feature is on: the setting, with the council's questions.</summary>
        public static bool On => S != null && S.councilBanishment && S.enableDilemmas;

        private static int Now => Find.TickManager.TicksGame;

        public BanishRecord Of(Pawn p, bool create = false)
        {
            if (p == null) return null;
            var r = records.FirstOrDefault(x => x != null && (x.pawn == p || (x.pawn == null && x.id == p.thingIDNumber)));
            if (r != null || !create) return r;
            r = new BanishRecord { pawn = p, id = p.thingIDNumber, name = p.LabelShort };
            records.Add(r);
            Trim();
            return r;
        }

        /// <summary>The record of one the council sent away, or null.</summary>
        public static BanishRecord Banished(Pawn p)
        {
            var r = Instance?.Of(p);
            return r != null && r.banishedTick >= 0 ? r : null;
        }

        private void Trim()
        {
            // The oldest settled first: nobody waiting on a word or on the map, gone longest or quiet longest.
            while (records.Count > MaxRecords)
            {
                var old = records.Where(r => r.stage != BanishStage.ToTell && r.stage != BanishStage.Gathering && r.wordAsked < 0)
                    .OrderBy(r => Math.Max(r.banishedTick, Math.Max(r.stepTick, r.letBeUntil))).FirstOrDefault();
                if (old == null) break;
                records.Remove(old);
            }
        }

        // ================= The fights, as they start =================

        /// <summary>A social fight starting (Harmony hook): the one whose tracker starts it threw the first punch.</summary>
        public static void Notify_SocialFight(Pawn starter, Pawn other)
        {
            var t = Instance;
            if (t == null || starter == null || other == null || !starter.RaceProps.Humanlike || !other.RaceProps.Humanlike) return;
            if (!starter.IsColonist && !other.IsColonist) return;
            int now = Now;
            t.fights.Add(new BanishFight { starter = starter.thingIDNumber, other = other.thingIDNumber, tick = now });
            int from = now - (int)((BanishMath.WindowDays + 5f) * GenDate.TicksPerDay);
            t.fights.RemoveAll(f => f == null || f.tick < from);
            if (t.fights.Count > MaxFights) t.fights.RemoveRange(0, t.fights.Count - MaxFights);
        }

        // ================= The trouble, from the records =================

        private static readonly string[] FightMemories = { "HadAngeringFight", "HadCatharticFight" };

        /// <summary>
        /// Every sighting of a fight <paramref name="p"/> was in: Rimfolk's notes (who started it), their own memories of a
        /// fight and the memories of those they fought (the game keeps them twenty days).
        /// </summary>
        public List<BanishMath.Fight> Sightings(Pawn p, IEnumerable<Pawn> colony)
        {
            var list = new List<BanishMath.Fight>();
            if (p == null) return list;
            int id = p.thingIDNumber, now = Now;
            foreach (var f in fights)
            {
                if (f == null) continue;
                if (f.starter == id) list.Add(new BanishMath.Fight(f.other, f.tick, true));
                else if (f.other == id) list.Add(new BanishMath.Fight(f.starter, f.tick, false));
            }
            void From(Pawn holder, Pawn about, int otherId)
            {
                var memories = holder?.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null) return;
                foreach (var m in memories)
                    if (m?.def != null && m.otherPawn == about && FightMemories.Contains(m.def.defName))
                        list.Add(new BanishMath.Fight(otherId, now - m.age, null));
            }
            var mine = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mine != null)
                foreach (var m in mine)
                    if (m?.def != null && m.otherPawn != null && FightMemories.Contains(m.def.defName))
                        list.Add(new BanishMath.Fight(m.otherPawn.thingIDNumber, now - m.age, null));
            if (colony != null)
                foreach (var o in colony)
                    if (o != null && o != p)
                        From(o, p, o.thingIDNumber);
            return list;
        }

        /// <summary>When those of the colony were insulted by them, as the insulted remember it.</summary>
        public static List<int> Insults(Pawn p, IEnumerable<Pawn> colony)
        {
            var list = new List<int>();
            if (p == null || colony == null) return list;
            int now = Now;
            foreach (var o in colony)
            {
                if (o == null || o == p) continue;
                var memories = o.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null) continue;
                foreach (var m in memories)
                    if (m?.def != null && m.otherPawn == p && m.def.defName == "Insulted")
                        list.Add(now - m.age);
            }
            return list;
        }

        /// <summary>The trouble <paramref name="p"/> caused since <paramref name="from"/>, among <paramref name="colony"/>.</summary>
        public BanishMath.Trouble TroubleOf(Pawn p, IEnumerable<Pawn> colony, int from) =>
            BanishMath.Of(Sightings(p, colony), Insults(p, colony), from);

        /// <summary>Who they fought since <paramref name="from"/>, by name, the most recent first.</summary>
        public List<Pawn> FoughtWith(Pawn p, List<Pawn> colony, int from)
        {
            var ids = BanishMath.Merge(Sightings(p, colony)).Where(f => f.tick >= from).OrderByDescending(f => f.tick).Select(f => f.other).Distinct().ToList();
            return ids.Select(i => colony.FirstOrDefault(o => o.thingIDNumber == i)).Where(o => o != null).ToList();
        }

        // ================= Who =================

        /// <summary>
        /// Someone the council could be asked about: a grown colonist of the colony, never a child, a prisoner, a slave, a
        /// quest lodger or a guest, and never anyone held under a tie with another people (a ward or hostage either way, the
        /// guardian of a ward, an envoy, a spouse of a marriage alliance).
        /// </summary>
        public static bool Eligible(Pawn p)
        {
            if (p == null || p.Dead || !p.IsFreeColonist || p.IsSlave || p.IsPrisoner || p.IsQuestLodger() || !p.RaceProps.Humanlike) return false;
            if (!p.DevelopmentalStage.Adult() || p.HostFaction != null || !People.HasInnerLife(p)) return false;
            return !HeldByTie(p);
        }

        /// <summary>Held under a tie with another people (Diplomacy/): a ward or hostage, a ward's guardian, an envoy, a spouse of a match.</summary>
        public static bool HeldByTie(Pawn p)
        {
            try
            {
                if (Wards.Of(p) != null || Wards.GuardedBy(p) != null) return true;
                if (Envoys.Holds(p) || EnvoysAbroad.Holds(p)) return true;
                if (Matches.TieOf(p) != null || Matches.PlanOf(p) != null) return true;
                foreach (var k in new[] { KeepFor.Ward, KeepFor.Hostage, KeepFor.Envoy, KeepFor.Lent, KeepFor.Spouse })
                    if (WorldLedger.KeptFor(p, k)) return true;
            }
            catch (Exception e)
            {
                // A tie that can't be read holds them: never ask about someone who might be held.
                CouncilLog.ErrorOnce("banishment: ties", e);
                return true;
            }
            return false;
        }

        /// <summary>The colony at this home with a say: its free colonists here, lodgers and babies aside.</summary>
        public static List<Pawn> ColonyAt(Map map)
        {
            var colony = ColonyState.For(map);
            return map?.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.IsSlave && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p)
                                                                 && (colony == null || colony.Has(p))).ToList() ?? new List<Pawn>();
        }

        /// <summary>A partner, or kin by blood, marriage or adoption.</summary>
        public static bool Close(Pawn a, Pawn b) => a != null && b != null && (Family.Of(a, b) || LovePartnerRelationUtility.LovePartnerRelationExists(a, b));

        public static int Opinion(Pawn of, Pawn about) => of?.relations?.OpinionOf(about) ?? 0;

        /// <summary>What the rest of the colony feels about them: who dislikes them, who stands by them, who'd be glad to see them go.</summary>
        public static void Views(Pawn p, List<Pawn> colony, List<Pawn> dislike, List<Pawn> standBy, List<Pawn> glad)
        {
            foreach (var o in colony)
            {
                if (o == p) continue;
                int op = Opinion(o, p);
                bool close = Close(o, p);
                if (BanishMath.StandsBy(op, close)) standBy?.Add(o);
                else if (BanishMath.Dislikes(op)) dislike?.Add(o);
                if (!close && BanishMath.Glad(op)) glad?.Add(o);
            }
        }

        /// <summary>The question due about <paramref name="p"/> at this home now, with the trouble that brings it.</summary>
        public BanishMath.Ask Due(Pawn p, List<Pawn> colony, out BanishMath.Trouble window, out BanishMath.Trouble since)
        {
            window = since = default;
            if (!On || !Eligible(p) || !p.Spawned || colony == null || !colony.Contains(p)) return BanishMath.Ask.None;
            if (!BanishMath.BigEnough(colony.Count)) return BanishMath.Ask.None;
            var r = Of(p);
            // Something the council decided about them is still under way.
            if (r != null && (r.wordAsked >= 0 || r.stage == BanishStage.ToTell || r.stage == BanishStage.Gathering)) return BanishMath.Ask.None;
            var dislike = new List<Pawn>();
            var standBy = new List<Pawn>();
            Views(p, colony, dislike, standBy, null);
            if (!BanishMath.Disliked(colony.Count - 1, dislike.Count, standBy.Count)) return BanishMath.Ask.None;
            int now = Now;
            window = TroubleOf(p, colony, BanishMath.WindowFrom(now));
            int step = r?.stepTick ?? -1;
            if (BanishMath.StepHolds(now, step)) since = TroubleOf(p, colony, step);
            return BanishMath.Due(now, step, r?.letBeUntil ?? -1, window, since);
        }

        /// <summary>The one most in trouble at this home for whom this question is due, or null.</summary>
        public Pawn Pick(Map map, BanishMath.Ask ask, out BanishMath.Trouble trouble)
        {
            trouble = default;
            if (!On || map == null) return null;
            var colony = ColonyAt(map);
            Pawn best = null;
            foreach (var p in colony)
            {
                if (Due(p, colony, out var window, out var since) != ask) continue;
                var t = ask == BanishMath.Ask.Banish ? since : window;
                if (best != null && t.score <= trouble.score) continue;
                best = p;
                trouble = t;
            }
            return best;
        }

        // ================= Who speaks for the council =================

        /// <summary>
        /// Who has the word with them: the council's speaker, else the faith's leader, else another of the council, else the
        /// eldest grown colonist; never the one it's about. <paramref name="title"/> is what they are ("speaker", the faith's
        /// own name for its leader) or null.
        /// </summary>
        public static Pawn Teller(Map map, Pawn about, out string title)
        {
            title = null;
            if (map == null) return null;
            bool Able(Pawn x) => x != null && x != about && !x.Dead && x.Spawned && x.Map == map && x.IsFreeColonist && !x.IsSlave && x.DevelopmentalStage.Adult()
                                 && People.HasInnerLife(x) && !x.IsQuestLodger();
            var offices = S.offices ? OfficeTracker.For(map) : null;
            var speaker = offices?.HolderOf("Council_Office_Speaker");
            if (Able(speaker))
            {
                title = DefDatabase<OfficeDef>.GetNamedSilentFail("Council_Office_Speaker")?.label;
                return speaker;
            }
            var leader = FaithLeader(out string role);
            if (Able(leader) && ColonyState.For(map)?.Has(leader) != false)
            {
                title = role;
                return leader;
            }
            if (offices != null)
                foreach (var o in OfficeTracker.Offices)
                foreach (var h in offices.HoldersOf(o))
                    if (Able(h))
                    {
                        title = o.label;
                        return h;
                    }
            var eldest = ColonyAt(map).Where(Able).OrderByDescending(x => x.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            if (eldest != null) title = "CC_Banish_Eldest".Translate().Resolve();
            return eldest;
        }

        /// <summary>The leader of the colony's own faith (Ideology), and the faith's own name for the role.</summary>
        public static Pawn FaithLeader(out string role)
        {
            role = null;
            if (!ModsConfig.IdeologyActive) return null;
            try
            {
                var ideo = Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo;
                if (ideo == null) return null;
                foreach (var r in ideo.RolesListForReading)
                {
                    if (r?.def != PreceptDefOf.IdeoRole_Leader) continue;
                    role = r.Label;
                    return r.ChosenPawnSingle();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("banishment: the faith's leader", e);
            }
            return null;
        }

        // ================= The words of the age and of the faith =================

        /// <summary>The colony's word for it by its age: "cast out from the fire", "sent into exile", "expelled from the colony"...</summary>
        public static string AgeWord() => BanishMath.WordKey(Ages.Tag(Ages.Ours)).Translate().Resolve();

        /// <summary>The same as an answer: "Cast Barr out from the fire".</summary>
        public static string AgeVerb(Pawn p) => BanishMath.VerbKey(Ages.Tag(Ages.Ours)).Translate(p.Named("PAWN")).Resolve();

        /// <summary>A told line in one of its wordings: the key, or key_2, key_3... where those exist.</summary>
        public static string W(string key, params NamedArgument[] args)
        {
            var keys = new List<string> { key };
            for (int i = 2; i <= 6 && (key + "_" + i).CanTranslate(); i++) keys.Add(key + "_" + i);
            return keys.RandomElement().Translate(args).Resolve();
        }

        /// <summary>A line from one of the pools, said by <paramref name="p"/> to or about <paramref name="other"/>, or null.</summary>
        public static string Line(string pool, Pawn p, Pawn other, params string[] tags)
        {
            string raw = Flavor.FromPool(pool, p, null, null, tags);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), (other ?? p).Named("OTHER"), AgeWord().Named("WORD")).Resolve();
        }

        /// <summary>Tags for the lines: the colony's age, and the word's kind.</summary>
        private static string[] Tags(params string[] more) => new[] { Ages.Tag(Ages.Ours) }.Concat(more ?? new string[0]).Where(t => !t.NullOrEmpty()).ToArray();

        // ================= What the council decided =================

        /// <summary>A question about them was sent.</summary>
        public void Asked(Pawn p) => Of(p, true);

        /// <summary>"Let it be": nobody asks about them for a while.</summary>
        public void LetBe(Pawn p)
        {
            var r = Of(p, true);
            if (r != null) r.letBeUntil = Now + (int)(BanishMath.LetBeDays * GenDate.TicksPerDay);
        }

        /// <summary>Their nights kept apart from the others: the council stepped in.</summary>
        public void Apart(Pawn p)
        {
            var r = Of(p, true);
            if (r == null) return;
            r.stepTick = Now;
            r.step = "apart";
        }

        /// <summary>A word face to face from <paramref name="by"/>, said when the two are together.</summary>
        public void Word(Pawn p, Pawn by, bool last)
        {
            var r = Of(p, true);
            if (r == null) return;
            r.wordBy = by;
            r.wordAsked = Now;
            r.wordLast = last;
            TryWord(r);
        }

        /// <summary>The council sends them away: told face to face by <paramref name="teller"/>, then their things, then the edge.</summary>
        public void Banish(Pawn p, Pawn teller)
        {
            var r = Of(p, true);
            if (r == null) return;
            r.stage = BanishStage.ToTell;
            r.teller = teller;
            r.stageTick = Now;
            r.word = AgeWord();
            r.wordAsked = -1;
            r.wordBy = null;
            TryTell(r);
        }

        // ================= The hour =================

        /// <summary>The council's people pass, once an hour.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var t = Instance;
            if (t == null || t.records.Count == 0) return;
            foreach (var r in t.records.ToList())
            {
                try
                {
                    t.Hour(r);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("banishment: the hour", e);
                }
            }
        }

        private void Hour(BanishRecord r)
        {
            var p = r.pawn;
            int now = Now;
            if (p == null || p.Discarded || p.Dead)
            {
                if (r.stage == BanishStage.ToTell || r.stage == BanishStage.Gathering) r.stage = BanishStage.None;
                r.wordAsked = -1;
                return;
            }
            // One sent away who is one of the colony again (let in at the gate): it's over.
            if (r.banishedTick >= 0 && !r.cameBack && p.Faction == Faction.OfPlayer)
            {
                r.cameBack = true;
                r.stepTick = now;
                r.step = "back";
                return;
            }
            if (r.wordAsked >= 0)
            {
                if (now - r.wordAsked > WordWaitDays * GenDate.TicksPerDay)
                {
                    // Never found the moment: the council's word was never said, and nothing is remembered of it.
                    Together.Forget(WordKey(r));
                    if (r.wordBy != null && p.Spawned) Tell.Minor(NewsKind.Colony, null, p.Map, W("CC_Banish_WordMissed", r.wordBy.Named("PAWN"), p.Named("OTHER")), new LookTargets(p));
                    r.wordAsked = -1;
                    r.wordBy = null;
                }
                else TryWord(r);
            }
            switch (r.stage)
            {
                case BanishStage.ToTell:
                    // The feature switched off while they waited: nothing more happens.
                    if (!On)
                    {
                        Together.Forget(TellKey(r));
                        r.stage = BanishStage.None;
                        return;
                    }
                    TryTell(r);
                    break;
                case BanishStage.Gathering:
                    if (now - r.stageTick >= GatherTicks && !StillGathering(p, r, now)) TryLeave(r);
                    break;
            }
        }

        // ================= A word face to face =================

        private static string WordKey(BanishRecord r) => "banishword|" + r.id;

        private static string TellKey(BanishRecord r) => "banishtell|" + r.id;

        private void TryWord(BanishRecord r)
        {
            var p = r.pawn;
            if (p == null || !p.Spawned) return;
            var by = r.wordBy;
            if (by == null || by.Dead || !by.Spawned || by.Map != p.Map)
            {
                by = Teller(p.Map, p, out _);
                if (by == null) return;
                r.wordBy = by;
            }
            string key = WordKey(r);
            if (Together.Waiting(key)) return;
            // together: the council's word is said only with the two of them face to face; the speaker walks over in free time.
            Together.When(key, by, p, () => SayWord(r, by), 24f, 0f);
        }

        private void SayWord(BanishRecord r, Pawn by)
        {
            // together: Together.When fired this with the two face to face.
            var p = r.pawn;
            if (p == null || r.wordAsked < 0) return;
            var t = TroubleOf(p, ColonyAt(p.Map), BanishMath.WindowFrom(Now));
            string mood = BanishMath.Say(t.started, t.joined, p.needs?.mood?.CurLevelPercentage ?? 0.5f);
            Speech.Say(by, Line("Rimfolk_Pool_BanishWord", by, p, Tags(r.wordLast ? "last" : null)), p);
            Speech.Say(p, Line("Rimfolk_Pool_BanishWordReply", p, by, Tags(mood)), by);
            r.wordAsked = -1;
            r.stepTick = Now;
            r.step = r.wordLast ? "last" : "word";
            CouncilGameComponent.GiveThought(p, BanishDefOf.Rimfolk_Banish_Warned, by);
            Tell.Minor(NewsKind.Colony, null, p.Map, W(r.wordLast ? "CC_Banish_LastWordSaid" : "CC_Banish_WordSaid", by.Named("PAWN"), p.Named("OTHER")),
                new LookTargets(new[] { by, p }), MessageTypeDefOf.NeutralEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council,
                "CC_Chronicle_BanishWord".Translate(p.LabelShort, by.LabelShort).Resolve(), p));
        }

        // ================= Sent away =================

        /// <summary>Up to walking out: at home, up and about, not drafted or in a break (as someone walking out waits).</summary>
        private static bool CanWalkOut(Pawn p) =>
            p != null && p.Spawned && !p.Downed && !p.InMentalState && !p.Drafted && p.Map != null && p.Map.IsPlayerHome && p.Faction == Faction.OfPlayer;

        private void TryTell(BanishRecord r)
        {
            var p = r.pawn;
            if (!CanWalkOut(p) || !Together.Able(p)) return;
            string key = TellKey(r);
            if (Together.Waiting(key)) return;
            var by = r.teller;
            if (by == null || by.Dead || !by.Spawned || by.Map != p.Map || by == p)
            {
                by = Teller(p.Map, p, out _);
                r.teller = by;
            }
            // Waited long enough for them: whoever is with them now tells them.
            if (by == null || Now - r.stageTick > TellWaitDays * GenDate.TicksPerDay)
            {
                // together: said to them by whoever of the colony is beside them.
                Together.WhenInCompany(key, p, with => Told(r, with), 24f);
                return;
            }
            // together: the one who tells them walks over and says it face to face (Together.When).
            Together.When(key, by, p, () => Told(r, by), 24f, 0f);
        }

        private void Told(BanishRecord r, Pawn by)
        {
            // together: fired by Together with the two face to face.
            var p = r.pawn;
            if (p == null || r.stage != BanishStage.ToTell || by == null) return;
            r.teller = by;
            var colony = ColonyAt(p.Map);
            var t = TroubleOf(p, colony, BanishMath.WindowFrom(Now));
            string mood = BanishMath.Say(t.started, t.joined, p.needs?.mood?.CurLevelPercentage ?? 0.5f);
            Speech.Say(by, Line("Rimfolk_Pool_BanishTell", by, p, Tags()), p);
            Speech.Say(p, Line("Rimfolk_Pool_BanishReply", p, by, Tags(mood)), by);
            r.stage = BanishStage.Gathering;
            r.stageTick = Now;
            bool room = Gather(p);
            Tell.Minor(NewsKind.Colony, null, p.Map, W("CC_Banish_Told", by.Named("PAWN"), p.Named("OTHER"), (r.word ?? AgeWord()).Named("WORD")) + " "
                + W(room ? "CC_Banish_Gathering" : "CC_Banish_GatheringHere", p.Named("OTHER")), new LookTargets(p), MessageTypeDefOf.NegativeEvent);
            if (by.relations != null && !BanishMath.Glad(Opinion(by, p))) CouncilGameComponent.GiveThought(by, BanishDefOf.Rimfolk_Banish_HadToTell, p);
        }

        /// <summary>
        /// They go to their own bed's room and stay a while, gathering their things (what they wear and carry is all they
        /// take: the game's own rule). With no bed of their own, they gather them where they are. True when they went to a room.
        /// </summary>
        private static bool Gather(Pawn p)
        {
            try
            {
                var bed = p.ownership?.OwnedBed;
                if (bed == null || !bed.Spawned || bed.Map != p.Map || !p.CanReach(bed, PathEndMode.Touch, Danger.Some)) return false;
                var go = JobMaker.MakeJob(JobDefOf.Goto, bed.Position);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                var wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = GatherTicks;
                p.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                p.jobs.jobQueue?.EnqueueFirst(wait);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("banishment: gathering", e);
                return false;
            }
        }

        /// <summary>Still on their way to their room, or in it gathering, and not long since they were told.</summary>
        private static bool StillGathering(Pawn p, BanishRecord r, int now)
        {
            if (now - r.stageTick > GatherTicks * 4 || !p.Spawned) return false;
            var bed = p.ownership?.OwnedBed;
            var job = p.CurJob;
            if (bed == null || job == null) return false;
            if (job.def == JobDefOf.Goto && job.targetA.Cell == bed.Position) return true;
            return job.def == JobDefOf.Wait && p.Position.InHorDistOf(bed.Position, 6f);
        }

        private void TryLeave(BanishRecord r)
        {
            var p = r.pawn;
            // Not while hurt, in a break, drafted or away: they go when they can walk out (as someone walking out does).
            if (!CanWalkOut(p)) return;
            var map = p.Map;
            var colony = ColonyAt(map);
            if (!colony.Any(o => o != p && o.DevelopmentalStage.Adult())) return;
            var comp = CouncilGameComponent.Instance;
            using var _cause = Causes.Push("CC_Cause_Banished".Translate(p.LabelShort).Resolve());
            // apart: the colony's moods about one sent away; the word itself was said face to face (Told).
            var glad = new List<Pawn>();
            var standBy = new List<Pawn>();
            Views(p, colony, null, standBy, glad);
            var friends = colony.Where(o => o != p && !Close(o, p) && Opinion(o, p) >= BanishMath.StandByOpinion).ToList();
            var kin = colony.Where(o => o != p && Close(o, p) && !BanishMath.Dislikes(Opinion(o, p))).ToList();
            bool toDie = PawnBanishUtility.WouldBeLeftToDie(p, p.Tile);
            string word = r.word ?? AgeWord();
            // Their last words, and their journal's last entry.
            var t = TroubleOf(p, colony, BanishMath.WindowFrom(Now));
            string say = Line("Rimfolk_Pool_BanishLast", p, r.teller, Tags(BanishMath.Say(t.started, t.joined, p.needs?.mood?.CurLevelPercentage ?? 0.5f)));
            comp?.journals.Write(p, "Rimfolk_Journal_Banished", null, word.Named("WORD"));
            foreach (var o in glad) CouncilGameComponent.GiveThought(o, BanishDefOf.Rimfolk_Banish_Glad, p);
            foreach (var o in friends) CouncilGameComponent.GiveThought(o, BanishDefOf.Rimfolk_Banish_Grief, p);
            foreach (var o in kin)
            {
                CouncilGameComponent.GiveThought(o, BanishDefOf.Rimfolk_Banish_GriefKin, p);
                comp?.journals.Write(o, "Rimfolk_Journal_BanishedKin", null, p.Named("OTHER"), word.Named("WORD"));
            }
            foreach (var o in friends.Concat(kin)) comp?.standings.Change(o, -4f, "CC_Log_Banished".Translate(p.LabelShort).Resolve());
            // Their wishes go with them, and so does what trust in the council did for them.
            if (comp != null)
                foreach (var w in comp.wishes.Where(w => w.state == WishState.Open && w.Involves(p)).ToList())
                    comp.Resolve(w, WishState.Cancelled);
            TrustEffects.StripAll(p);
            r.stage = BanishStage.Gone;
            r.banishedTick = Now;
            r.grudge = Rand.Chance(BanishMath.GrudgeChance);
            // The letter, before the game takes them out of the colony (they're still standing where they were told).
            string text = W("CC_Banish_GoneText", p.Named("PAWN"), word.Named("WORD"));
            if (toDie) text += " " + W("CC_Banish_GoneToDie", p.Named("PAWN"));
            if (!say.NullOrEmpty()) text += "\n\n\"" + say + "\"";
            string feel = Feelings(p, glad, friends.Concat(kin).ToList());
            if (!feel.NullOrEmpty()) text += "\n\n" + feel;
            Tell.Letter(NewsKind.People, Importance.Important, map, W("CC_Banish_GoneTitle", p.Named("PAWN"), word.Named("WORD")), text, LetterDefOf.NegativeEvent, new LookTargets(p));
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_Banished".Translate(p.LabelShort, word).Resolve(), p));
            Speech.Say(p, say);
            // The game's own banishment, with its own moods for everyone (heavier when they're sent out to die).
            if (p.Drafted) p.drafter.Drafted = false;
            PawnBanishUtility.Banish(p, true);
            WalkOut(p);
        }

        /// <summary>Who is glad and who will miss them, in a sentence or two for the letter.</summary>
        private static string Feelings(Pawn p, List<Pawn> glad, List<Pawn> miss)
        {
            var parts = new List<string>();
            if (glad.Count > 0) parts.Add(W("CC_Banish_GladOf", Names(glad).Named("NAMES"), p.Named("PAWN")));
            if (miss.Count > 0) parts.Add(W("CC_Banish_MissedBy", Names(miss).Named("NAMES"), p.Named("PAWN")));
            return string.Join(" ", parts);
        }

        /// <summary>Names, up to three, then how many more.</summary>
        public static string Names(List<Pawn> who)
        {
            var named = who.Where(x => x != null).Select(x => x.LabelShort).Distinct().ToList();
            if (named.Count <= 3) return TextUtil.JoinAnd(named);
            return "CC_Play_NamesAndMore".Translate(string.Join(", ", named.Take(2)), named.Count - 2).Resolve();
        }

        /// <summary>Off the colony, they walk to the nearest edge they can reach and leave the map there (the game's own way of leaving).</summary>
        private static void WalkOut(Pawn p)
        {
            try
            {
                if (p == null || p.Dead || !p.Spawned || p.Downed || p.Faction == Faction.OfPlayer) return;
                if (!RCellFinder.TryFindBestExitSpot(p, out var spot)) return;
                var job = JobMaker.MakeJob(JobDefOf.Goto, spot);
                job.exitMapOnArrival = true;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs?.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("banishment: walking out", e);
            }
        }

        // ================= Back among raiders =================

        /// <summary>The one riding with the raid just arrived, for its letter (Patch_BanishRaidLetter), with the raid's parms.</summary>
        internal static Pawn rider;
        internal static IncidentParms riderParms;

        /// <summary>
        /// A raid of hostile people has just arrived at a home (Harmony hook, its pawns spawned, its letter still to come):
        /// one the council sent away with a grudge may ride with it, once, long enough after (BanishMath.MayRide, RaidChance).
        /// </summary>
        public void JoinArrivingRaid(IncidentParms parms, List<Pawn> raiders)
        {
            rider = null;
            riderParms = null;
            if (!On || parms?.faction == null || raiders.NullOrEmpty() || !(parms.target is Map map) || !map.IsPlayerHome) return;
            if (!parms.faction.HostileTo(Faction.OfPlayer) || !parms.faction.def.humanlikeFaction) return;
            var anchor = raiders.Where(x => x.Spawned && x.Map == map && x.Faction == parms.faction && x.RaceProps.Humanlike).RandomElementWithFallback();
            if (anchor == null) return;
            int now = Now;
            foreach (var r in records.Where(r => r.pawn != null && BanishMath.MayRide(r.grudge, r.rode, r.banishedTick, now) && !r.cameBack).ToList())
            {
                var x = r.pawn;
                if (x.Dead || x.Discarded || x.Spawned || !Find.WorldPawns.Contains(x) || x.IsPrisoner || x.GetCaravan() != null || People.Kidnapped(x)
                    || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x) || x.health.summaryHealth.SummaryHealthPercent < 0.6f) continue;
                if (x.Faction != null && !x.Faction.HostileTo(Faction.OfPlayer)) continue;
                if (!Rand.Chance(BanishMath.RaidChance)) continue;
                if (!CellFinder.TryFindRandomCellNear(anchor.Position, map, 6, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null, out var cell, 30)) return;
                r.rode = true;
                if (x.Faction != parms.faction) x.SetFaction(parms.faction);
                Find.WorldPawns.RemovePawn(x);
                GenSpawn.Spawn(x, cell, map);
                raiders.Add(x);
                rider = x;
                riderParms = parms;
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger,
                    "CC_Chronicle_BanishRode".Translate(x.LabelShort, parms.faction.Name).Resolve(), null));
                return;
            }
        }

        /// <summary>The raid letter's line about the one who came back with it, or null.</summary>
        public static string RaidNote(IncidentParms parms, List<Pawn> pawns)
        {
            if (rider == null || parms != riderParms || pawns == null || !pawns.Contains(rider)) return null;
            var r = Banished(rider);
            string ago = r != null ? TextUtil.DaysString(Now - r.banishedTick) : null;
            return ago == null ? null : W("CC_Banish_Rides", rider.Named("PAWN"), ago.Named("AGO"), (r.word ?? AgeWord()).Named("WORD"));
        }

        // ================= Saving =================

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            Scribe_Collections.Look(ref fights, "fights", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records ??= new List<BanishRecord>();
                fights ??= new List<BanishFight>();
                records.RemoveAll(r => r == null || (r.pawn == null && r.banishedTick < 0));
                fights.RemoveAll(f => f == null);
            }
        }
    }

    [DefOf]
    public static class BanishDefOf
    {
        public static ThoughtDef Rimfolk_Banish_Warned;
        public static ThoughtDef Rimfolk_Banish_Glad;
        public static ThoughtDef Rimfolk_Banish_Grief;
        public static ThoughtDef Rimfolk_Banish_GriefKin;
        public static ThoughtDef Rimfolk_Banish_HadToTell;

        static BanishDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BanishDefOf));
        }
    }
}
