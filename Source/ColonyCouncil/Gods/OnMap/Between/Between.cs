using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>One happening between the colony's people under way: whose, where, who is in it, what it holds, and its stage.</summary>
    public class BetweenScene : IExposable
    {
        public OwnHappening h;
        public Map map;
        public int god = -1;
        /// <summary>The two it is between (a the first, b the second), and a third who speaks for the god or carries the coin on.</summary>
        public Pawn a, b, c;
        /// <summary>A thing in it: the coin, the coat, the frog, the cup, the machine, the grave.</summary>
        public Thing thing;
        public IntVec3 at = IntVec3.Invalid;
        /// <summary>Its stage (each happening's own; below zero it is over), when its next step is due, and when it is over at the latest.</summary>
        public int stage, next = -1, until = -1;
        /// <summary>A count and a word of its own.</summary>
        public int num;
        public string word;

        public God God => Gods.ById(god);

        public void ExposeData()
        {
            Scribe_Values.Look(ref h, "h");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref b, "b");
            Scribe_References.Look(ref c, "c");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref at, "at", IntVec3.Invalid);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref num, "num");
            Scribe_Values.Look(ref word, "word");
        }
    }

    /// <summary>A woodcutter of the colony's and how many trees they felled since when (Hollin's brambles round the stump).</summary>
    public class BetweenFeller : IExposable
    {
        public Pawn pawn;
        public int felled, since, lastAt = -1;
        public IntVec3 stump = IntVec3.Invalid;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref felled, "felled");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref lastAt, "lastAt", -1);
            Scribe_Values.Look(ref stump, "stump", IntVec3.Invalid);
        }
    }

    /// <summary>The happenings between the colony's people under way, and the little they remember (the woodcutters, the newcomers welcomed).</summary>
    public class BetweenState : IExposable
    {
        public List<BetweenScene> scenes = new List<BetweenScene>();
        public List<BetweenFeller> fellers = new List<BetweenFeller>();
        /// <summary>The newcomers already welcomed (their thing ids), at most <see cref="WelcomedKept"/>.</summary>
        public List<int> welcomed = new List<int>();
        /// <summary>The fights and the insults already answered (the second's thing id and the memory's age when it was), so one fight brings one pair of saplings.</summary>
        public List<string> answered = new List<string>();
        /// <summary>The sarcophagi whose sealed dead were already answered (their thing ids).</summary>
        public List<int> sealedSeen = new List<int>();

        public const int MaxScenes = 4, WelcomedKept = 20, AnsweredKept = 20, SealedKept = 12;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref scenes, "scenes", LookMode.Deep);
            Scribe_Collections.Look(ref fellers, "fellers", LookMode.Deep);
            Scribe_Collections.Look(ref welcomed, "welcomed", LookMode.Value);
            Scribe_Collections.Look(ref answered, "answered", LookMode.Value);
            Scribe_Collections.Look(ref sealedSeen, "sealedSeen", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                scenes ??= new List<BetweenScene>();
                scenes.RemoveAll(s => s == null || s.map == null);
                fellers ??= new List<BetweenFeller>();
                fellers.RemoveAll(f => f?.pawn == null);
                welcomed ??= new List<int>();
                answered ??= new List<string>();
                sealedSeen ??= new List<int>();
            }
        }

        /// <summary>Kept in bounds: the oldest let go.</summary>
        public void Trim()
        {
            while (scenes.Count > MaxScenes) scenes.RemoveAt(0);
            fellers.RemoveAll(f => f?.pawn == null || f.pawn.Dead || f.pawn.Discarded);
            while (fellers.Count > OwnHappeningsMath.FellersKept) fellers.RemoveAt(0);
            while (welcomed.Count > WelcomedKept) welcomed.RemoveAt(0);
            while (answered.Count > AnsweredKept) answered.RemoveAt(0);
            while (sealedSeen.Count > SealedKept) sealedSeen.RemoveAt(0);
        }
    }

    public partial class OwnHappeningsBook
    {
        /// <summary>The happenings between the colony's people (Gods/OnMap/Between/), saved beside the others.</summary>
        public BetweenState between = new BetweenState();

        internal void ExposeBetween()
        {
            Scribe_Deep.Look(ref between, "between");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) between ??= new BetweenState();
        }
    }

    /// <summary>
    /// One kind of happening between the colony's people: what calls it at a home each hour, how it plays out step by step,
    /// and how the dev tools bring it now. Each god's are in its own file beside this one (Between&lt;God&gt;.cs).
    /// </summary>
    public abstract class BetweenKind
    {
        public abstract OwnHappening H { get; }

        public string Key => OwnHappeningsMath.GodOf(H);

        public God God => OwnHappenings.God(Key);

        /// <summary>
        /// Of whichever god its people hold (round 7's "Faith..." happenings, OwnHappeningsMath.OfAnyGod): it has no god of its
        /// own by name, so the hour's look comes to it with none, and it finds the god in the people (each its own look at
        /// OwnHappenings.Acts for the god it found).
        /// </summary>
        public bool AnyGod => OwnHappeningsMath.OfAnyGod(H);

        /// <summary>The hour's look at a home: what calls it, and its beginning.</summary>
        public abstract void Hourly(OwnHappeningsBook b, God g, Map map, int now);

        /// <summary>A step of a scene of its own under way (its stage set below zero when it is over).</summary>
        public virtual void Step(OwnHappeningsBook b, BetweenScene s, int now) => s.stage = -1;

        /// <summary>Dev: brings it now at this home past its pace, saying what came of it or why it couldn't.</summary>
        public abstract string Fire(OwnHappeningsBook b, God g, Map map);
    }

    /// <summary>
    /// The gods between the colony's people (round 6 of each god's own happenings; the player, 20 November 2026: "additional
    /// God events, more immersion, more just further also building up on the gods in general, how they're integrated, the
    /// effects they have, the relations between pawns, the kind of events that generated from it"). Each of the fifteen gods
    /// has two or more happenings of its own that set its people toward or against each other (who believes, who doubts, who
    /// argues over a sign, who is drawn closer, who is ashamed), each played out on the map: someone walks over, stands, carries
    /// something, a sign shows in the god's own look, one sentence over a head. This is their shared frame:
    /// <list type="bullet">
    /// <item>the hour and the steps, called from the second seven's (OwnSeven.cs), each kind's own in its god's file;</item>
    /// <item>who stands where (BetweenStance: holding the god, a rival's, another's, none, doubting it all);</item>
    /// <item>the words: one sentence a speaker (Rimfolk_Pool_GodBetween_Say, by the happening and the speaker's part), a card in
    /// three wordings (CC_GodB_&lt;happening&gt;_Text, _2, _3: TextVariants), titled by the god's name only where the colony
    /// knows it was that god (Gods.CardName), and the book's line as its witness saw it (CC_GodB_Saw_&lt;happening&gt;);</item>
    /// <item>how it ends between the two (BetweenOutcome): a memory of each other in the game's own opinions, their belief
    /// held the harder after a quarrel, a plain line in the news and their journals.</item>
    /// </list>
    /// What follows is the people's own; the god's favour, wants and rules are never shown. No harm: nobody is hurt.
    /// </summary>
    public static class OwnBetween
    {
        public const string Pool = "Rimfolk_Pool_GodBetween_Say";

        internal static int Now => Find.TickManager.TicksGame;

        /// <summary>Every kind, the order of the gods.</summary>
        public static readonly BetweenKind[] Kinds =
        {
            new HollinBound(), new HollinStumps(),
            new SmoulderPraise(), new SmoulderNearMiss(),
            new BellwetherTold(), new BellwetherSpared(),
            new MorelWasted(), new MorelSealed(),
            new EnnuWelcomed(), new EnnuWaitedUp(),
            new PipTwoHands(), new PipTalked(),
            new CirrusTwoWatchers(), new CirrusCloudShape(),
            new HushCoat(), new HushNoise(),
            new UndertowWhisper(), new UndertowLampOut(),
            new HeliosSun(), new HeliosAlike(), new HeliosShade(),
            new SeleneSorry(), new SeleneSameDream(),
            new MogGraveCat(), new MogRiddle(),
            new MurmurSplash(), new MurmurFrogIn(),
            new DucatPassed(), new DucatMiser(),
            new RevelRivalsCup(), new RevelWager(),
            // Round 7 (docs/content-update-proposals-2.md, D1 to D15): of whichever god their people hold (BetweenFaith*.cs), and
            // the Guest's and the Hum's own (BetweenRound2Gods.cs).
            new FaithDare(), new FaithCameThrough(), new FaithSides(), new EnnuBurying(), new FaithLittleShrine(), new FaithTurned(), new PipWake(),
            new FaithTakenOffering(), new FaithBookRead(), new FaithWeddingSign(), new FaithRaiderWord(), new FaithGift(), new FaithBargain(), new FaithLightning()
        };

        public static BetweenKind KindOf(OwnHappening h) => Kinds.FirstOrDefault(k => k.H == h);

        // ================= The hour and the steps =================

        /// <summary>The gods' hour at a home (OwnHappenings.Seven): each kind's look, where its god acts.</summary>
        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            if (b?.between == null || map == null || !OwnHappenings.Quiet(map)) return;
            foreach (var k in Kinds.InRandomOrder())
            {
                var g = k.God;
                // One of whichever god its people hold looks for its god itself (and whether that god acts here).
                if (!k.AnyGod && (g == null || !OwnHappenings.Acts(g, map))) continue;
                // One at a time at a home: a scene under way here waits for its end.
                if (b.between.scenes.Any(s => s.map == map && s.stage >= 0)) return;
                OwnHappenings.Safe("between: " + k.H, () => k.Hourly(b, g, map, now));
            }
        }

        /// <summary>The steps between the hours (OwnHappenings.SevenSteps): each scene under way.</summary>
        internal static void Steps(OwnHappeningsBook b, int now)
        {
            var st = b?.between;
            if (st == null || st.scenes.Count == 0) return;
            foreach (var s in st.scenes.ToList())
            {
                if (s.map == null || !Homes.IsHome(s.map) || s.until >= 0 && now > s.until + GenDate.TicksPerHour * 2) s.stage = -1;
                if (s.stage >= 0 && (s.next < 0 || now >= s.next))
                {
                    var k = KindOf(s.h);
                    if (k == null) s.stage = -1;
                    else OwnHappenings.Safe("between's steps: " + s.h, () => k.Step(b, s, now));
                }
                if (s.stage < 0) st.scenes.Remove(s);
            }
            if (st.scenes.Count > 0) b.Due(now + 60);
        }

        /// <summary>A scene begun: kept, its happening noted as come, its first step soon.</summary>
        public static BetweenScene Begin(OwnHappeningsBook b, OwnHappening h, God g, Map map, IntVec3 at, Pawn a, Pawn other = null, float hours = 4f, string form = null)
        {
            var s = new BetweenScene { h = h, map = map, god = g?.id ?? -1, at = at, a = a, b = other, stage = 0, next = -1, until = Now + Mathf.RoundToInt(hours * GenDate.TicksPerHour) };
            b.between.scenes.Add(s);
            b.between.Trim();
            OwnHappenings.Came(b, h, Now, form, a?.LabelShort);
            b.Due(Now + 30);
            return s;
        }

        /// <summary>Ticks from now to the next time the home's clock shows this hour.</summary>
        public static int TicksToHour(Map map, int hour)
        {
            int d = hour * GenDate.TicksPerHour - GenLocalDate.DayTick(map);
            return d <= 0 ? d + GenDate.TicksPerDay : d;
        }

        /// <summary>Its next step after this many ticks.</summary>
        public static void Wait(BetweenScene s, int ticks) => s.next = Now + Math.Max(1, ticks);

        /// <summary>
        /// A happening that follows something (a fight, a fine piece, a body sealed, a newcomer): free of its gap, at its own
        /// chance, a little more the nearer its god, and its god given its turn among the others (GodTouch.Turn); the dev tools
        /// push it.
        /// </summary>
        public static bool Follows(OwnHappeningsBook b, OwnHappening h, God g, Map map, float chance)
        {
            if (b == null || g == null) return false;
            if (OwnHappenings.forced == h) return true;
            if (!OwnHappenings.Acts(g, map)) return false;
            return Rand.Chance(OwnHappeningsMath.FollowChance(h, OwnHappenings.DaysSince(b, h, Now), chance, GodNear.Of(g, map)) * GodTouch.Turn(g, map));
        }

        // ================= Who =================

        /// <summary>One of the colony's own people, awake and themselves (not downed, drafted or in a state), old enough to talk.</summary>
        public static bool Able(Pawn p) => OwnHappenings.Ours(p) && GodOmens.Awake(p) && !p.DevelopmentalStage.Baby();

        /// <summary>Able and free of anything that matters now (WorshipJobs.Free).</summary>
        public static bool FreeNow(Pawn p) => Able(p) && OwnHappenings.Free(p);

        /// <summary>Grown (not a child).</summary>
        public static bool Grown(Pawn p) => p != null && !p.DevelopmentalStage.Juvenile();

        /// <summary>They hold this god: lean toward it at least, not in dread of it.</summary>
        public static bool Holds(Pawn p, God g) => p != null && g != null && PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g);

        /// <summary>They believe none of it.</summary>
        public static bool Doubts(Pawn p) => p != null && (Worship.Scoffer(p) || GodLore.Credence(p) < GodLoreMath.ScofferBelow);

        /// <summary>How someone stands toward a god.</summary>
        public static BetweenStance StanceOf(Pawn p, God g)
        {
            if (p == null || g == null) return BetweenStance.Neither;
            var held = PawnGods.Held(p);
            bool holdsOther = held != null && held != g && PawnGods.HoldOf(p, held) >= PawnGodsMath.LeansFrom;
            bool rival = holdsOther && Gods.Rivals(g).Contains(held);
            return OwnHappeningsMath.Stance(Holds(p, g), rival, holdsOther, Doubts(p));
        }

        /// <summary>The god they hold most, when it is one they lean toward at least.</summary>
        public static God HeldBy(Pawn p)
        {
            var g = PawnGods.Held(p);
            return g != null && PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom ? g : null;
        }

        /// <summary>The most devout of these who hold the god, or null.</summary>
        public static Pawn Believer(IEnumerable<Pawn> among, God g) => among?.Where(p => Holds(p, g)).OrderByDescending(p => PawnGods.HoldOf(p, g)).FirstOrDefault();

        /// <summary>The one of these who doubts it most, or null.</summary>
        public static Pawn Doubter(IEnumerable<Pawn> among) => among?.Where(Doubts).OrderBy(GodLore.Credence).FirstOrDefault();

        /// <summary>What one thinks of the other (the game's opinion), 0 for none.</summary>
        public static float Opinion(Pawn of, Pawn about) => of?.relations == null || about == null ? 0f : of.relations.OpinionOf(about);

        /// <summary>The colony's people at a home who are able now.</summary>
        public static List<Pawn> People(Map map) => map?.mapPawns.FreeColonistsSpawned.Where(Able).ToList() ?? new List<Pawn>();

        // ================= On the map =================

        /// <summary>A standable cell near a place someone can reach, else Invalid.</summary>
        public static IntVec3 Near(Map map, IntVec3 at, Pawn p, int radius = 3)
        {
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            var c = CellFinder.RandomClosewalkCellNear(at, map, radius, x => x.Standable(map) && (p == null || p.CanReach(x, PathEndMode.OnCell, Danger.Some)));
            return c.IsValid && c.InBounds(map) ? c : IntVec3.Invalid;
        }

        /// <summary>Someone walks over to a place (a cell near it), for at most this many hours.</summary>
        public static bool Send(Pawn p, Map map, IntVec3 at, float hours = 2f, int radius = 3)
        {
            var to = Near(map, at, p, radius);
            return to.IsValid && OwnHappenings.GoTo(p, to, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
        }

        /// <summary>They are there (within this many cells) and done walking.</summary>
        public static bool There(Pawn p, IntVec3 at, float r = 3.5f) => p != null && p.Spawned && at.IsValid && p.Position.InHorDistOf(at, r) && p.CurJobDef != JobDefOf.Goto;

        /// <summary>Still able to be in it (on the map, alive, not downed, drafted or in a state).</summary>
        public static bool Still(Pawn p, Map map) => p != null && p.Spawned && p.Map == map && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState;

        /// <summary>The god's own look at a place (GodTouchFx), small, kind or dark.</summary>
        public static void Show(God g, Map map, IntVec3 at, bool dark = false)
        {
            if (g == null || map == null || !at.IsValid) return;
            GodTouchFx.Show(g, map, at, TouchTier.Small, dark);
        }

        /// <summary>Two stand facing each other a while (the game's waiting), as people do when they talk.</summary>
        public static void Face(Pawn a, Pawn b, int ticks)
        {
            // together: the two are side by side when this is called (each scene checks it first).
            if (a != null && b != null) OwnHappenings.Stand(a, b.Position, ticks);
            if (a != null && b != null) OwnHappenings.Stand(b, a.Position, ticks);
        }

        // ================= Words =================

        /// <summary>The tag of a happening's lines for one part ("hollinbound_believe").</summary>
        public static string Tag(OwnHappening h, string part) => h.ToString().ToLowerInvariant() + "_" + part;

        /// <summary>
        /// One sentence over <paramref name="p"/>'s head for its part in the happening (the pool's lines tagged so; {GOD} only on
        /// lines tagged named, where the colony is sure; {OTHER} the one it is said to), to <paramref name="to"/> when the two are
        /// together, else to nobody in particular. The line said, or null.
        /// </summary>
        public static string Say(Pawn p, OwnHappening h, string part, God g, Pawn to = null, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned) return null;
            var args = new List<NamedArgument> { (to ?? p).Named("OTHER") };
            if (more != null) args.AddRange(more);
            string line = OwnHappenings.LineFrom(p, Pool, g, args.ToArray(), Tag(h, part));
            if (line.NullOrEmpty()) return null;
            // together: said to the other only when the two are really together (Speech.Say checks it); else to nobody.
            Speech.Say(p, line, to != null && Together.Can(p, to) ? to : null);
            return line;
        }

        /// <summary>The words key of a happening ("CC_GodB_HollinBound_" + rest).</summary>
        public static string K(OwnHappening h, string rest) => "CC_GodB_" + h + "_" + rest;

        /// <summary>
        /// The card of what was seen, in one of its wordings (CC_GodB_&lt;h&gt;_Text, _Text_2, _Text_3): a letter titled by the
        /// god's name only where the colony knows it was that god, or a message (Tell.FromGod). <paramref name="variant"/> picks a
        /// second text of its own ("TextAlone"). Never names the god in its words.
        /// </summary>
        public static void Card(OwnHappening h, God g, Map map, IntVec3 at, bool letter, string variant = null, params NamedArgument[] args)
        {
            if (map == null) return;
            var all = new List<NamedArgument>(args ?? new NamedArgument[0]) { Homes.Name(map).Named("HOME") };
            string text = TextVariants.Vary(K(h, variant ?? "Text")).Translate(all.ToArray()).Resolve();
            var look = at.IsValid ? new LookTargets(new TargetInfo(at, map)) : null;
            if (letter) OwnHappenings.CardOf(h == OwnHappening.UndertowWhisper || h == OwnHappening.UndertowLampOut ? null : g, map, null, K(h, "Title"), K(h, "TitleNamed"), text, look, LetterDefOf.NeutralEvent, all.ToArray());
            else
            {
                GodPresence.Look(look);
                OwnHappenings.Line(map, text, look);
            }
        }

        /// <summary>The colony's book keeps it as its witness saw it (CC_GodB_Saw_&lt;h&gt;); those who saw it feel it, each by their belief (GodTouch).</summary>
        public static void Book(OwnHappening h, God g, Map map, IntVec3 at, GodActKind kind, Pawn witness, params NamedArgument[] more)
        {
            if (g == null || map == null) return;
            OwnHappenings.Saw(g, map, at, kind, witness, "CC_GodB_Saw_" + h, more);
        }

        /// <summary>A doubter's answer, set down beside the believers' in the colony's book.</summary>
        public static void Doubted(Pawn doubter, God g, string line)
        {
            if (doubter == null || g == null || line.NullOrEmpty()) return;
            GodLore.Disputed(doubter, g, line);
        }

        /// <summary>What the happening was, in a few words for the news ("the two saplings by the storehouse").</summary>
        public static string What(OwnHappening h, params NamedArgument[] args) => K(h, "What").Translate(args).Resolve();

        // ================= How it ends between them =================

        private static ThoughtDef Def(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        /// <summary>A memory of the other (the game's opinion), only with mood effects on.</summary>
        private static void Of(Pawn p, string def, Pawn other)
        {
            var d = Def(def);
            if (d == null || p?.needs?.mood == null || other == null || !(ColonyCouncilMod.Settings?.MoodEffects ?? true)) return;
            // together: every outcome is given where the two met on the map (each scene checks it), or of one seen there.
            CouncilGameComponent.GiveThought(p, d, other);
        }

        private static void Mood(Pawn p, string def)
        {
            var d = Def(def);
            if (d == null || p?.needs?.mood == null || !(ColonyCouncilMod.Settings?.MoodEffects ?? true)) return;
            CouncilGameComponent.GiveThought(p, d);
        }

        /// <summary>The memories two people had of a quarrel lately, let go (they made it up).</summary>
        private static void LetGo(Pawn p, Pawn other)
        {
            var mem = p?.needs?.mood?.thoughts?.memories;
            if (mem == null || other == null) return;
            foreach (var n in new[] { "HadAngeringFight", "HadCatharticFight", "Insulted", "Slighted", "Rimfolk_Between_Quarrel", "Rimfolk_Belief_GodsQuarrel" })
            {
                var d = Def(n);
                if (d != null) mem.RemoveMemoriesOfDefWhereOtherPawnIs(d, other);
            }
        }

        /// <summary>
        /// How it ends between <paramref name="a"/> and <paramref name="b"/>: their memories of each other (or one's own),
        /// their belief held the harder after a quarrel over their gods, a plain line in the news, and their journals. For
        /// Ashamed, Jealous, Wary, Laughed and Kind, <paramref name="a"/> is the one who feels it and <paramref name="b"/> the
        /// other. Nothing for None.
        /// </summary>
        public static void End(OwnHappening h, BetweenOutcome o, Pawn a, Pawn b, God ga = null, God gb = null, params NamedArgument[] whatArgs)
        {
            if (o == BetweenOutcome.None || a == null) return;
            var map = a.MapHeld;
            switch (o)
            {
                case BetweenOutcome.Closer:
                    Of(a, "Rimfolk_Between_Closer", b);
                    Of(b, "Rimfolk_Between_Closer", a);
                    break;
                case BetweenOutcome.Quarrel:
                    Of(a, "Rimfolk_Between_Quarrel", b);
                    Of(b, "Rimfolk_Between_Quarrel", a);
                    if (ga != null) PawnGods.Move(a, ga, BeliefEvent.Bickered);
                    if (gb != null && b != null) PawnGods.Move(b, gb, BeliefEvent.Bickered);
                    break;
                case BetweenOutcome.MadeUp:
                    LetGo(a, b);
                    LetGo(b, a);
                    Of(a, "Rimfolk_Between_MadeUp", b);
                    Of(b, "Rimfolk_Between_MadeUp", a);
                    break;
                case BetweenOutcome.Ashamed:
                    Mood(a, "Rimfolk_Between_Ashamed");
                    break;
                case BetweenOutcome.Jealous:
                    Of(a, "Rimfolk_Between_Jealous", b);
                    break;
                case BetweenOutcome.Wary:
                    Of(a, "Rimfolk_Between_Wary", b);
                    break;
                case BetweenOutcome.Laughed:
                    Of(a, "Rimfolk_Between_Laughed", b);
                    break;
                case BetweenOutcome.Kind:
                    Of(a, "Rimfolk_Between_Kind", b);
                    Of(b, "Rimfolk_Between_Closer", a);
                    break;
            }
            string what = What(h, whatArgs);
            var args = new List<NamedArgument> { a.Named("A"), what.Named("WHAT") };
            if (b != null) args.Add(b.Named("B"));
            string key = "CC_GodB_Out_" + o + (b == null && o == BetweenOutcome.Ashamed ? "Alone" : "");
            // together: the two were together on the map for it (each scene brings them to one place first).
            if (map != null) Tell.Minor(NewsKind.People, Importance.Flavor, map, TextVariants.Vary(key).Translate(args.ToArray()).Resolve().CapitalizeFirst(), b != null ? new LookTargets(new[] { a, b }) : new LookTargets(a));
            OwnHappenings.Journal(a, "CC_GodB_Journal_" + o, what.Named("WHAT"), (b?.LabelShort ?? "").Named("OTHER"));
            if (b != null && (o == BetweenOutcome.Closer || o == BetweenOutcome.Quarrel || o == BetweenOutcome.MadeUp))
                OwnHappenings.Journal(b, "CC_GodB_Journal_" + o, what.Named("WHAT"), a.LabelShort.Named("OTHER"));
        }

        /// <summary>One is proud of what they did: their own memory, and a line in their journal.</summary>
        public static void Proud(OwnHappening h, Pawn p, params NamedArgument[] whatArgs)
        {
            if (p == null) return;
            Mood(p, "Rimfolk_Between_Proud");
            OwnHappenings.Journal(p, "CC_GodB_Journal_Proud", What(h, whatArgs).Named("WHAT"));
        }

        /// <summary>A doubter shaken by what they saw: their belief moves toward the god a little (as a sign seen moves anyone's).</summary>
        public static void Shaken(Pawn doubter, God g)
        {
            if (doubter == null || g == null) return;
            PawnGods.Move(doubter, g, BeliefEvent.Saw);
        }
    }
}
