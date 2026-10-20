using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's name in the colony: what the others make of them from the deeds they know of (Social/Gossip/), side by
    /// side (<see cref="RepFacet"/>). Read here: the colony's view (<see cref="Standing(Pawn)"/>) and one person's view of
    /// another (<see cref="Impression"/>), the words for it, the rep:brave, rep:cruel... tags lines can ask for, and the
    /// small things a name does: a word of opinion, a little more courage beside the brave in a fight, a vote, fewer
    /// secrets told to a gossip, a colony name (an epithet) a little sooner. All of it follows the "Personalities and
    /// social life" setting.
    /// </summary>
    public static class Reputations
    {
        public static bool On => Persona.On && Current.Game != null;

        public static ReputationTracker Tracker => ReputationTracker.Instance;

        /// <summary>One of the colony's own people with a name to keep: a colonist (or a slave) with an inner life, not a baby or a lodger.</summary>
        public static bool Public(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && p.IsColonist && !p.IsQuestLodger() && People.HasInnerLife(p)
            && !p.DevelopmentalStage.Baby();

        // ---------------- Reading ----------------

        private static readonly float[] nothing = new float[RepMath.Facets];

        /// <summary>The colony's view of someone, side by side (all nothing when nothing is said). Don't change the array.</summary>
        public static float[] Standing(Pawn p)
        {
            if (!On || p == null) return nothing;
            return Tracker?.StandingOf(p.thingIDNumber) ?? nothing;
        }

        public static float Standing(Pawn p, RepFacet f) => Standing(p)[(int)f];

        /// <summary>What <paramref name="listener"/> makes of <paramref name="subject"/> from what they've heard, or null.</summary>
        public static float[] Impression(Pawn listener, Pawn subject)
        {
            if (!On || listener == null || subject == null || listener == subject) return null;
            return Tracker?.Impression(listener.thingIDNumber, subject.thingIDNumber);
        }

        /// <summary>What it all adds up to in the listener's eyes (see <see cref="RepMath.Esteem"/>).</summary>
        public static float Esteem(Pawn listener, Pawn subject) => RepMath.Esteem(Impression(listener, subject));

        // ---------------- Words and tags ----------------

        /// <summary>The word for a side of a name ("brave", "a coward"), or null when it doesn't show.</summary>
        public static string Word(RepFacet f, float v)
        {
            int d = RepMath.Degree(v);
            if (d == 0) return null;
            return ("CC_Rep_" + f + "_" + (d < 0 ? "M" : "P") + Mathf.Abs(d)).Translate();
        }

        /// <summary>Whether a side, as it stands, speaks well of them (a temper speaks against).</summary>
        public static bool Good(RepFacet f, float v) => v * RepMath.Worth[(int)f] > 0f;

        private static readonly string[][] TagNames =
        {
            new[] { "rep:coward", "rep:brave" },
            new[] { "rep:cruel", "rep:kind" },
            new[] { "rep:idle", "rep:reliable" },
            new[] { "rep:clumsy", "rep:skilled" },
            new[] { "rep:gossip", "rep:trustworthy" },
            new[] { "rep:levelheaded", "rep:hothead" }
        };

        /// <summary>The tag a side of a name gives once it shows (rep:brave, rep:cruel...), or null.</summary>
        public static string TagOf(RepFacet f, float v)
        {
            int d = RepMath.Degree(v);
            return d == 0 ? null : TagNames[(int)f][d > 0 ? 1 : 0];
        }

        /// <summary>Every tag a name can give, for the Almanac and the tests.</summary>
        public static IEnumerable<string> AllTags => TagNames.SelectMany(t => t);

        /// <summary>The rep:brave, rep:cruel... tags for lines written about (or by) someone with that name (PeopleTags).</summary>
        public static void AddTags(Pawn p, HashSet<string> tags)
        {
            if (!On || p == null || tags == null) return;
            try
            {
                var st = Standing(p);
                for (int f = 0; f < RepMath.Facets; f++)
                {
                    var tag = TagOf((RepFacet)f, st[f]);
                    if (tag == null) continue;
                    tags.Add(tag);
                    if (Mathf.Abs(st[f]) >= RepMath.Strong) tags.Add(tag + "+");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reputation tags", e);
            }
        }

        /// <summary>"brave and kind" / "a coward and a gossip": the sides that show, strongest first, the good or the bad ones.</summary>
        public static List<string> WordsOf(float[] st, bool good)
        {
            var list = new List<(float strength, string word)>();
            for (int f = 0; f < RepMath.Facets; f++)
            {
                if (Good((RepFacet)f, st[f]) != good) continue;
                var w = Word((RepFacet)f, st[f]);
                if (w != null) list.Add((Mathf.Abs(st[f]), w));
            }
            return list.OrderByDescending(x => x.strength).Select(x => x.word).ToList();
        }

        // ---------------- What a name does ----------------

        /// <summary>A small social mood: what someone has heard of another, good or bad (ThoughtWorker_Reputation). -1 for none.</summary>
        public static int OpinionStage(Pawn p, Pawn other)
        {
            if (!On || p == null || other == null || p == other) return -1;
            var tr = Tracker;
            if (tr == null) return -1;
            var imp = tr.Impression(p.thingIDNumber, other.thingIDNumber);
            return imp == null ? -1 : RepMath.OpinionStage(RepMath.Esteem(imp));
        }

        /// <summary>The lean at the ballot from what a voter has heard of a candidate (Offices). 0 with the setting off.</summary>
        public static float VoteLean(Pawn voter, Pawn candidate)
        {
            if (!On || voter == null || candidate == null || voter == candidate) return 0f;
            try
            {
                return RepMath.VoteLean(Esteem(voter, candidate));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reputation vote", e);
                return 0f;
            }
        }

        /// <summary>
        /// Whether a secret is told to this friend today (Secrets): someone known as a gossip is told far fewer, someone
        /// known to keep things to themselves a few more. Always true with the setting off.
        /// </summary>
        public static bool WouldConfideIn(Pawn owner, Pawn friend)
        {
            if (!On || friend == null) return true;
            // What the owner has heard of the friend counts first; the colony's view when they've heard nothing.
            var imp = Impression(owner, friend);
            float discretion = imp != null ? imp[(int)RepFacet.Discretion] : Standing(friend, RepFacet.Discretion);
            float f = RepMath.ConfideFactor(discretion);
            return f >= 1f || Rand.Chance(f);
        }

        /// <summary>Colony names (epithets) by the record they count, and the side of a name each belongs to.</summary>
        private static readonly Dictionary<string, RepFacet> EpithetSides = new Dictionary<string, RepFacet>
        {
            ["KillsHumanlikes"] = RepFacet.Courage, ["Headshots"] = RepFacet.Courage, ["KillsMechanoids"] = RepFacet.Courage,
            ["FiresExtinguished"] = RepFacet.Courage, ["DamageTaken"] = RepFacet.Courage,
            ["TimesTendedOther"] = RepFacet.Kindness, ["Council_PatientsPulledThrough"] = RepFacet.Kindness,
            ["Council_QuarrelsMended"] = RepFacet.Kindness, ["Council_BirthsAttended"] = RepFacet.Kindness, ["PrisonersChatted"] = RepFacet.Kindness,
            ["ThingsCrafted"] = RepFacet.Skill, ["ThingsConstructed"] = RepFacet.Skill, ["OperationsPerformed"] = RepFacet.Skill,
            ["MealsCooked"] = RepFacet.Skill, ["ResearchPointsResearched"] = RepFacet.Skill, ["AnimalsTamed"] = RepFacet.Skill,
            ["PrisonersRecruited"] = RepFacet.Skill, ["ThingsRepaired"] = RepFacet.Skill,
            ["ThingsHauled"] = RepFacet.Diligence, ["MessesCleaned"] = RepFacet.Diligence, ["PlantsHarvested"] = RepFacet.Diligence,
            ["PlantsSown"] = RepFacet.Diligence, ["CellsMined"] = RepFacet.Diligence, ["CorpsesBuried"] = RepFacet.Diligence,
            ["Council_SecretsKept"] = RepFacet.Discretion,
            ["TimesInMentalState"] = RepFacet.Temper
        };

        /// <summary>The side of a name a colony name belongs to, by the record it counts; null for one that stands on its own.</summary>
        public static RepFacet? SideOf(EpithetDef def) =>
            def?.record != null && EpithetSides.TryGetValue(def.record.defName, out var f) ? f : (RepFacet?)null;

        /// <summary>
        /// A factor on the bar for a colony name (Epithets): someone already known for that side (brave, for the
        /// Defender) earns the name a little sooner. 1 for names that stand on their own, and with the setting off.
        /// </summary>
        public static float EpithetEase(EpithetDef def, Pawn p)
        {
            if (!On || p == null) return 1f;
            var side = SideOf(def);
            if (side == null) return 1f;
            // The Storm (breaks counted) comes sooner to someone already known as a hothead, the Defender to the brave.
            return RepMath.EpithetEase(Standing(p, side.Value));
        }

        // ---------------- The character page ----------------

        /// <summary>
        /// "What people say about them" on a colonist's character page: their name in words (and the colony names they
        /// hold beside it), the deeds people know of and how far each has gone round (who told whom, when the story grew
        /// or shrank on the way), and who thinks well or ill of them for it.
        /// </summary>
        /// <summary>The deeds listed on the page (the most talked-of first).</summary>
        public const int MaxDeedsShown = 4;

        public static CharacterSection Section(Pawn p)
        {
            var s = new CharacterSection { title = p != null ? "CC_Rep_PageTitle".Translate(p.Named("PAWN")).Resolve() : "CC_Rep_PageTitle".Translate().ToString() };
            var tr = Tracker;
            if (!On || tr == null || p == null || !Persona.Eligible(p)) return s;
            int now = Find.TickManager.TicksGame;
            var st = Standing(p);
            var good = WordsOf(st, true);
            var bad = WordsOf(st, false);
            string tip = StandingTip(st);
            if (good.Count > 0 && bad.Count > 0)
                s.Line("CC_Rep_Known_Both".Translate(p.Named("PAWN"), TextUtil.JoinAnd(good).Named("GOOD"), TextUtil.JoinAnd(bad).Named("BAD")).Resolve(), tip);
            else if (good.Count > 0) s.Line("CC_Rep_Known_Good".Translate(p.Named("PAWN"), TextUtil.JoinAnd(good).Named("GOOD")).Resolve(), tip);
            else if (bad.Count > 0) s.Line("CC_Rep_Known_Bad".Translate(p.Named("PAWN"), TextUtil.JoinAnd(bad).Named("BAD")).Resolve(), tip);
            // (The colony's own names for them are in the Standing section above, with a word on how a name eases them.)
            var deeds = tr.By(p.thingIDNumber).Where(d => d.def != null).OrderByDescending(d => d.Weight(now) * (1 + d.heard.Count)).Take(MaxDeedsShown).ToList();
            int others = Mathf.Max(0, tr.PublicCount - 1);
            foreach (var d in deeds)
            {
                int knowing = d.heard.Count(k => tr.PawnOf(k.id) != null);
                string what = Deeds.TextOf(d, p);
                string line = "CC_Rep_Deed".Translate(what.CapitalizeFirst().Named("WHAT"), (d.label ?? "").Named("LABEL"), knowing.Named("COUNT"), others.Named("ALL")).Resolve();
                string chain = Chain(d);
                if (chain != null) line += " " + chain;
                string grown = Grown(d);
                if (grown != null) line += " " + grown;
                s.Line(line, ChainTip(d));
            }
            if (s.lines.Count == 0 || deeds.Count == 0)
            {
                if (s.lines.Count == 0) s.Line("CC_Rep_Nothing".Translate(p.Named("PAWN")).Resolve());
                return s;
            }
            // Who thinks what of them for it.
            var well = new List<string>();
            var ill = new List<string>();
            var unheard = new List<string>();
            foreach (var kv in tr.Colony)
            {
                if (kv.Key == p.thingIDNumber) continue;
                var imp = tr.Impression(kv.Key, p.thingIDNumber);
                if (imp == null)
                {
                    unheard.Add(kv.Value.LabelShort);
                    continue;
                }
                float e = RepMath.Esteem(imp);
                if (e >= 5f) well.Add(kv.Value.LabelShort);
                else if (e <= -5f) ill.Add(kv.Value.LabelShort);
            }
            var parts = new List<string>();
            if (well.Count > 0) parts.Add("CC_Rep_Split_Well".Translate(TextUtil.JoinAnd(well.Take(6).ToList())).Resolve());
            if (ill.Count > 0) parts.Add("CC_Rep_Split_Ill".Translate(TextUtil.JoinAnd(ill.Take(6).ToList())).Resolve());
            if (unheard.Count > 0 && (well.Count > 0 || ill.Count > 0)) parts.Add("CC_Rep_Split_Unheard".Translate(TextUtil.JoinAnd(unheard.Take(6).ToList())).Resolve());
            if (parts.Count > 0) s.Line(string.Join(" ", parts), "CC_Rep_Split_Tip".Translate());
            return s;
        }

        /// <summary>
        /// The longest way the story went round, when it went through three people or more ("It went from Ana to Bo to
        /// Cara."): from the one who saw it (or the doer, for a boast) to the last who heard it. Null for a short one.
        /// </summary>
        private static string Chain(Deed d)
        {
            List<string> best = null;
            foreach (var k in d.heard)
            {
                if (k.Witness) continue;
                var names = new List<string> { k.name };
                var cur = k;
                for (int guard = 0; guard < 12 && !cur.Witness; guard++)
                {
                    var prev = cur.from == d.doer ? null : d.KnowerOf(cur.from);
                    if (prev == null)
                    {
                        names.Insert(0, cur.fromName ?? "?");
                        break;
                    }
                    names.Insert(0, prev.name);
                    cur = prev;
                }
                if (best == null || names.Count > best.Count) best = names;
            }
            if (best == null || best.Count < 3) return null;
            return "CC_Rep_Chain_Inline".Translate(string.Join(" " + "CC_Rep_Chain_To".Translate() + " ", best).Named("CHAIN")).Resolve();
        }

        /// <summary>How the story has changed on its way round: grown in the telling, or told kinder than it was.</summary>
        private static string Grown(Deed d)
        {
            var told = d.heard.Where(k => !k.Witness).ToList();
            if (told.Count == 0 || d.size <= 0f) return null;
            float mean = told.Average(k => k.version) / d.size;
            bool bad = d.def.Valence < 0;
            if (mean >= 1.2f) return (bad ? "CC_Rep_Grown_Worse" : "CC_Rep_Grown_Better").Translate();
            if (mean <= 0.8f) return (bad ? "CC_Rep_Grown_Softer" : "CC_Rep_Grown_Smaller").Translate();
            return null;
        }

        /// <summary>The tooltip on a deed: who saw it, and who heard it from whom, how it was told and whether they believe it.</summary>
        private static string ChainTip(Deed d)
        {
            var seen = d.heard.Where(k => k.Witness).Select(k => k.name).ToList();
            var lines = new List<string>();
            if (seen.Count > 0) lines.Add("CC_Rep_Chain_Saw".Translate(TextUtil.JoinAnd(seen)).Resolve());
            foreach (var k in d.heard.Where(k => !k.Witness).Take(12))
            {
                string how = ("CC_Rep_Slant_" + k.slant).Translate();
                string line = "CC_Rep_Chain_Heard".Translate(k.name.Named("WHO"), (k.fromName ?? "?").Named("FROM"), how.Named("HOW")).Resolve();
                if (k.belief < 0.45f) line += " " + "CC_Rep_Chain_Doubts".Translate().Resolve();
                lines.Add(line);
            }
            if (lines.Count == 0) lines.Add("CC_Rep_Chain_Nobody".Translate().Resolve());
            lines.Add("");
            lines.Add("CC_Rep_Chain_Fades".Translate().Resolve());
            return string.Join("\n", lines);
        }

        private static string StandingTip(float[] st)
        {
            var lines = new List<string> { "CC_Rep_StandingTip".Translate().Resolve(), "" };
            for (int f = 0; f < RepMath.Facets; f++)
            {
                string w = Word((RepFacet)f, st[f]) ?? "CC_Rep_NothingSaid".Translate().ToString();
                lines.Add(("CC_Rep_Facet_" + (RepFacet)f).Translate() + ": " + w + " (" + st[f].ToString("0") + ")");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>Puts "What people say about them" on the character page (section 60).</summary>
    [StaticConstructorOnStartup]
    public static class ReputationPage
    {
        static ReputationPage()
        {
            CharacterPage.Add("reputation", 60, Reputations.Section);
        }
    }

    /// <summary>
    /// What someone has heard of another colonist, as a small opinion (good or bad), from the deeds they know of: the
    /// bad sooner felt than the good. See Social/Gossip/.
    /// </summary>
    public class ThoughtWorker_Reputation : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            try
            {
                int stage = Reputations.OpinionStage(p, other);
                return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reputation opinion", e);
                return ThoughtState.Inactive;
            }
        }
    }
}
