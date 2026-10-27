using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's families known by name (the pure part is FamilyLifeMath.cs): two or more of the colony with the same
    /// last name who are family to each other (by blood, marriage or adoption, or a couple) are "the Boyds". What the
    /// family's name stands for is what the colony says of them all, each side of their names averaged
    /// (Social/Gossip/): "The Boyds are known as kind and reliable." It shows in each one's story, and a story told about
    /// one of them now and then brings the family into it. A couple with two names can be asked whether they take one
    /// (<see cref="DilemmaWorker_SharedName"/>): a married couple takes it at once, an engaged one at the wedding (the
    /// game's own choice of name for it, <c>nextMarriageNameChange</c>). Only which couples were asked is saved
    /// (<see cref="CouncilGameComponent.sharedNameAsked"/>); the families are worked out again as they are needed.
    /// </summary>
    public static class FamilyNames
    {
        private class Known
        {
            public string last;
            public List<Pawn> members;
        }

        /// <summary>Each of the colony's people in a known family, by thing id: worked out at most once an hour.</summary>
        private static readonly Dictionary<int, Known> byPawn = new Dictionary<int, Known>();
        private static int builtAt = int.MinValue;

        /// <summary>Couples engaged this session (their pair key) and when: asked about a day later (lost on reload; the question still comes in its turn).</summary>
        private static readonly Dictionary<string, int> engagedAt = new Dictionary<string, int>();

        public static void Reset()
        {
            byPawn.Clear();
            builtAt = int.MinValue;
            engagedAt.Clear();
        }

        /// <summary>Their last name, or null.</summary>
        public static string Last(Pawn p) => (p?.Name as NameTriple)?.Last?.Trim();

        private static bool Linked(Pawn a, Pawn b) => Family.Of(a, b) || LovePartnerRelationUtility.LovePartnerRelationExists(a, b);

        private static void Build()
        {
            int now = Find.TickManager.TicksGame;
            if (builtAt != int.MinValue && now >= builtAt && now - builtAt < GenDate.TicksPerHour) return;
            builtAt = now;
            byPawn.Clear();
            var people = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => p.RaceProps.Humanlike && !p.IsQuestLodger() && !Last(p).NullOrEmpty()).ToList();
            foreach (var same in people.GroupBy(p => Last(p).ToLowerInvariant()))
            {
                var list = same.ToList();
                if (list.Count < FamilyLifeMath.FamilyAtLeast) continue;
                var links = new List<(int, int)>();
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        if (Linked(list[i], list[j])) links.Add((i, j));
                foreach (var c in FamilyLifeMath.Clusters(list.Count, links))
                {
                    if (c.Count < FamilyLifeMath.FamilyAtLeast) continue;
                    var k = new Known { last = Last(list[c[0]]), members = c.Select(i => list[i]).ToList() };
                    foreach (var m in k.members) byPawn[m.thingIDNumber] = k;
                }
            }
        }

        private static Known Of(Pawn p)
        {
            if (p == null || Current.Game == null) return null;
            Build();
            return byPawn.TryGetValue(p.thingIDNumber, out var k) ? k : null;
        }

        /// <summary>The family they are known by ("Boyds", said as "the Boyds"), or null when they aren't one of a known family.</summary>
        public static string Plural(Pawn p)
        {
            var k = Of(p);
            return k != null ? FamilyLifeMath.FamilyPlural(k.last) : null;
        }

        /// <summary>The colony's people of their known family, them included (empty when there is none).</summary>
        public static List<Pawn> Members(Pawn p) => Of(p)?.members.ToList() ?? new List<Pawn>();

        /// <summary>The two are of the same known family.</summary>
        public static bool Same(Pawn a, Pawn b)
        {
            var k = Of(a);
            return k != null && k == Of(b);
        }

        /// <summary>What their family's name stands for, each side averaged over those of them with a name to keep, or null.</summary>
        public static float[] Standing(Pawn p)
        {
            var k = Of(p);
            if (k == null || !Reputations.On) return null;
            return FamilyLifeMath.FamilyStanding(k.members.Where(Reputations.Public).Select(m => Reputations.Standing(m)).ToList());
        }

        /// <summary>Which way their family's name leans, by its strongest side: 1 well spoken of, -1 badly, 0 nothing said.</summary>
        public static int Leans(Pawn p)
        {
            var sides = FamilyLifeMath.FamilySides(Standing(p), 1);
            if (sides.Count == 0) return 0;
            return FamilyLifeMath.GoodSide(sides[0].facet, sides[0].plus) ? 1 : -1;
        }

        /// <summary>"The Boyds are known as kind and reliable, though quick to anger.", or null when nothing is said of them.</summary>
        public static string ReputeLine(Pawn p)
        {
            string family = Plural(p);
            var sides = FamilyLifeMath.FamilySides(Standing(p));
            if (family == null || sides.Count == 0) return null;
            string Words(bool good) => TextUtil.JoinAnd(sides.Where(s => FamilyLifeMath.GoodSide(s.facet, s.plus) == good)
                .Select(s => ("CC_FamRep_" + s.facet + "_" + (s.plus ? "P" : "M")).Translate().ToString()).ToList());
            string goodWords = Words(true), badWords = Words(false);
            string key = goodWords.NullOrEmpty() ? "CC_FamRep_Bad" : badWords.NullOrEmpty() ? "CC_FamRep_Good" : "CC_FamRep_Both";
            return key.Translate(family.Named("FAMILY"), goodWords.Named("GOOD"), badWords.Named("BAD")).Resolve();
        }

        /// <summary>"Ken is one of the Boyds, three of them in the colony.", with what their name stands for after; null when they aren't one of a known family.</summary>
        public static string StoryLine(Pawn p)
        {
            var k = Of(p);
            if (k == null) return null;
            string line = "CC_FamName_Story".Translate(p.Named("PAWN"), FamilyLifeMath.FamilyPlural(k.last).Named("FAMILY"), k.members.Count.Named("COUNT")).Resolve();
            string repute = ReputeLine(p);
            return repute.NullOrEmpty() ? line : line + " " + repute;
        }

        // ---------------- Taking one name ----------------

        /// <summary>The two as a couple, the same whichever comes first.</summary>
        public static string PairKey(Pawn a, Pawn b) => Math.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Math.Max(a.thingIDNumber, b.thingIDNumber);

        /// <summary>Married or engaged to each other.</summary>
        public static bool Couple(Pawn a, Pawn b) =>
            a?.relations != null && b != null && (a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b));

        /// <summary>Engaged to each other, not yet married.</summary>
        public static bool Engaged(Pawn a, Pawn b) => a?.relations != null && b != null && a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b);

        /// <summary>The game's own word for <paramref name="taker"/> taking their partner's name at the wedding (as it tells the two apart).</summary>
        public static MarriageNameChange Change(Pawn taker, Pawn other)
        {
            SpouseRelationUtility.DetermineManAndWomanSpouses(taker, other, out var man, out _);
            return man == other ? MarriageNameChange.MansName : MarriageNameChange.WomansName;
        }

        /// <summary>Their faith lets them marry this way: taking the man's name, the woman's, or keeping their own (the game's own precepts, as at its roll).</summary>
        public static bool Willing(Pawn p, MarriageNameChange change)
        {
            if (p == null) return false;
            var def = change == MarriageNameChange.MansName ? HistoryEventDefOf.GotMarried_TookMansName
                : change == MarriageNameChange.WomansName ? HistoryEventDefOf.GotMarried_TookWomansName : HistoryEventDefOf.GotMarried_KeptName;
            try
            {
                return def == null || IdeoUtility.DoerWillingToDo(def, p);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// A couple the council could ask: two of the colony, married or engaged, with two last names, never asked
        /// before, and an engaged couple only while they mean to keep their own names (the game's roll at the proposal);
        /// at least one of them willing to take the other's name.
        /// </summary>
        public static bool CouldAsk(Pawn a, Pawn b)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || a == null || b == null || a == b || !Couple(a, b)) return false;
            if (Last(a).NullOrEmpty() || Last(b).NullOrEmpty() || FamilyLifeMath.SameName(Last(a), Last(b))) return false;
            if (comp.sharedNameAsked.Contains(PairKey(a, b))) return false;
            if (Engaged(a, b) && (a.relations.nextMarriageNameChange != MarriageNameChange.NoChange || b.relations.nextMarriageNameChange != MarriageNameChange.NoChange)) return false;
            return CanTake(a, b) || CanTake(b, a);
        }

        /// <summary><paramref name="taker"/> could take <paramref name="other"/>'s name: both of their faiths allow it.</summary>
        public static bool CanTake(Pawn taker, Pawn other)
        {
            var change = Change(taker, other);
            return Willing(taker, change) && Willing(other, change);
        }

        /// <summary>The question about this couple has been put to the council: once.</summary>
        public static void Asked(Pawn a, Pawn b)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || a == null || b == null) return;
            string key = PairKey(a, b);
            engagedAt.Remove(key);
            if (!comp.sharedNameAsked.Contains(key)) comp.sharedNameAsked.Add(key);
            if (comp.sharedNameAsked.Count > 40) comp.sharedNameAsked.RemoveRange(0, comp.sharedNameAsked.Count - 40);
        }

        /// <summary>A couple of the colony just engaged (Social/FamilyLife.cs): asked about a day later, if they keep two names.</summary>
        public static void Notify_Engaged(Pawn a, Pawn b)
        {
            // apart: the question waits a day, and comes to the council, not to anyone's face.
            if (a == null || b == null || CouncilGameComponent.Instance == null) return;
            engagedAt[PairKey(a, b)] = Find.TickManager.TicksGame;
            if (engagedAt.Count > 20) engagedAt.Remove(engagedAt.OrderBy(x => x.Value).First().Key);
        }

        /// <summary>While the question comes for a couple just engaged, outside its turn: only about such a couple.</summary>
        public static bool OnlyEngaged { get; private set; }

        /// <summary>The couple just engaged that the question is for now, this session (their pair key), or false.</summary>
        public static bool JustEngaged(Pawn a, Pawn b) => a != null && b != null && engagedAt.ContainsKey(PairKey(a, b));

        /// <summary>The council's hour: a couple engaged a day ago, with two names, brings the question now.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null || engagedAt.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var key in engagedAt.Keys.ToList())
                if (now - engagedAt[key] > GenDate.TicksPerDay * 10) engagedAt.Remove(key);
            if (!ColonyCouncilMod.Settings.enableDilemmas || !engagedAt.Values.Any(t => now - t >= GenDate.TicksPerDay)) return;
            OnlyEngaged = true;
            try
            {
                comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_SharedName);
            }
            finally
            {
                OnlyEngaged = false;
            }
        }

        /// <summary>
        /// <paramref name="taker"/> takes <paramref name="other"/>'s last name: a married couple now (the game's own name
        /// change; the name they were born with is kept, for a divorce), an engaged couple at the wedding. With
        /// <paramref name="keep"/>, each keeps their own, at the wedding too. Returns the name taken now, or null.
        /// </summary>
        public static string Take(Pawn taker, Pawn other, bool keep)
        {
            if (taker?.relations == null || other?.relations == null || !Couple(taker, other)) return null;
            var change = keep ? MarriageNameChange.NoChange : Change(taker, other);
            if (Engaged(taker, other))
            {
                taker.relations.nextMarriageNameChange = change;
                other.relations.nextMarriageNameChange = change;
                return null;
            }
            if (keep || !(taker.Name is NameTriple was)) return null;
            if (taker.story != null && taker.story.birthLastName.NullOrEmpty()) taker.story.birthLastName = was.Last;
            if (!SpouseRelationUtility.ChangeNameAfterMarriage(taker, other, change)) return null;
            builtAt = int.MinValue;
            string last = Last(taker);
            var comp = CouncilGameComponent.Instance;
            if (comp != null)
            {
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_FamName_Chronicle".Translate(taker.Named("A"), other.Named("B"), last.Named("LAST")).Resolve(), taker));
                if (!comp.journals.WroteWithin(taker)) comp.journals.Write(taker, "Council_Journal_TookName", null, other.Named("OTHER"), (was.Last ?? "").Named("OLD"), last.Named("NEW"));
            }
            return last;
        }
    }

    /// <summary>
    /// The context of the question about one name: the two last names as they were when it was asked ({LASTA}, {LASTB}),
    /// so the letter reads the same after the answer changed one of them.
    /// </summary>
    public class SharedNameContext : DilemmaContext
    {
        public string lastA;
        public string lastB;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((lastA ?? "").Named("LASTA"));
            args.Add((lastB ?? "").Named("LASTB"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastA, "lastA");
            Scribe_Values.Look(ref lastB, "lastB");
        }
    }

    /// <summary>
    /// Whether a couple with two names takes one (<see cref="FamilyNames"/>): A and B married or engaged, a couple just
    /// engaged first. Its own tag for the texts: "engaged" for a couple not yet married.
    /// </summary>
    public class DilemmaWorker_SharedName : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (CouncilGameComponent.Instance == null || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            var pairs = new List<(Pawn a, Pawn b)>();
            foreach (var a in pawns)
                foreach (var b in pawns)
                    if (a.thingIDNumber < b.thingIDNumber && FamilyNames.CouldAsk(a, b) && (!FamilyNames.OnlyEngaged || FamilyNames.JustEngaged(a, b)))
                        pairs.Add((a, b));
            if (pairs.Count == 0) return false;
            var (x, y) = pairs.OrderByDescending(p => FamilyNames.JustEngaged(p.a, p.b)).ThenBy(_ => Rand.Value).First();
            // A is the one more likely to take the other's name: the one whose faith lets them, else either.
            if (!FamilyNames.CanTake(x, y) || (FamilyNames.CanTake(y, x) && Rand.Bool)) (x, y) = (y, x);
            ctx = new SharedNameContext { def = def, map = map, a = x, b = y, lastA = FamilyNames.Last(x), lastB = FamilyNames.Last(y) };
            FamilyNames.Asked(x, y);
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a == null || ctx.b == null) return;
            if (FamilyNames.Engaged(ctx.a, ctx.b)) tags.Add("engaged");
        }
    }

    /// <summary>
    /// The council's answer about one name (<see cref="FamilyNames.Take"/>): <see cref="taker"/> (A unless given) takes
    /// the other's name, or with <see cref="keep"/> each keeps their own. Can't be picked when their faith is against it.
    /// </summary>
    public class DilemmaEffect_SharedName : DilemmaEffect
    {
        public PlayWho taker = PlayWho.A;
        public bool keep;

        private static (Pawn taker, Pawn other) Who(PlayWho taker, DilemmaContext ctx) => taker == PlayWho.B ? (ctx.b, ctx.a) : (ctx.a, ctx.b);

        public override string Unavailable(DilemmaContext ctx)
        {
            var (t, o) = Who(taker, ctx);
            if (t == null || o == null) return null;
            if (keep)
                return FamilyNames.Willing(t, MarriageNameChange.NoChange) && FamilyNames.Willing(o, MarriageNameChange.NoChange)
                    ? null : "CC_FamName_FaithTake".Translate(t.Named("A"), o.Named("B")).Resolve();
            return FamilyNames.CanTake(t, o) ? null : "CC_FamName_FaithKeep".Translate(t.Named("A")).Resolve();
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: a name is the council's word and the couple's; nothing on the map changes but the name itself.
            var (t, o) = Who(taker, ctx);
            if (t == null || o == null || t.Dead || o.Dead) return;
            string last = FamilyNames.Take(t, o, keep);
            if (last.NullOrEmpty()) return;
            Messages.Message("CC_FamName_Taken".Translate(t.Named("A"), o.Named("B"), last.Named("LAST")).Resolve(), new LookTargets(t, o), MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
