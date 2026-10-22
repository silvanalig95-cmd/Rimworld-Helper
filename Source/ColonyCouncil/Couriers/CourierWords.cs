using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What couriers say (through Speech, from the pool Rimfolk_CourierLines, in their own people's way: a tribe's runner
    /// talks like one), and the messages about them: when they come, when they've done their business, and when what
    /// was left of a deal was settled another way.
    /// </summary>
    public static class CourierWords
    {
        private const string Pool = "Rimfolk_CourierLines";

        private static FlavorPoolDef pool;
        private static bool looked;

        private static FlavorPoolDef Lines
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
                }
                return pool;
            }
        }

        // ---------------- Speech ----------------

        /// <summary>A line from one of the trip's people at a moment of it: arrive, await, deliver, collect, receive, leave.</summary>
        public static void Say(Pawn p, CourierTrip t, string moment)
        {
            if (p == null || t == null || !p.Spawned) return;
            var tags = new HashSet<string> { moment };
            string goods = moment == "deliver" ? t.bringTag : moment == "collect" || moment == "receive" || moment == "await" ? t.takeTag : t.bringTag ?? t.takeTag;
            if (!goods.NullOrEmpty()) tags.Add(goods);
            if (t.Brings) tags.Add("bring");
            if (t.Takes || t.kind == CourierKind.Await) tags.Add("take");
            SayTags(p, p.Faction, tags);
        }

        /// <summary>Someone of the event who is here hands things over themselves.</summary>
        public static void SayInPerson(Pawn p, string goodsTag)
        {
            if (p == null) return;
            var tags = new HashSet<string> { "inperson" };
            if (!goodsTag.NullOrEmpty()) tags.Add(goodsTag);
            SayTags(p, p.Faction, tags);
        }

        private static void SayTags(Pawn p, Faction f, HashSet<string> tags)
        {
            try
            {
                var lines = Lines?.lines;
                if (lines == null) return;
                if (f?.def != null) tags.Add(f.def.techLevel <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
                foreach (var t in Flavor.TagsFor(p)) tags.Add(t);
                var tone = ToneUtility.ToneFor(p);
                string line = Flavor.PickFresh(lines.For(tone), tags, lines.For(tone));
                if (line == null && tone != Tone.Plain) line = Flavor.PickFresh(lines.plain, tags, lines.plain);
                if (!line.NullOrEmpty()) Speech.Say(p, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier words", e);
            }
        }

        // ---------------- Who they are ----------------

        /// <summary>"runner", "porter", "courier": what the people of this tech level are called, one or more.</summary>
        private static string Noun(Faction f, bool plural)
        {
            var tech = f?.def?.techLevel ?? TechLevel.Industrial;
            string key = tech <= TechLevel.Neolithic ? "Runner" : tech == TechLevel.Medieval ? "Porter" : "Courier";
            return ("CC_Courier_" + key + (plural ? "s" : "")).Translate().Resolve();
        }

        /// <summary>
        /// "A runner", "Two porters", "Two porters and a muffalo" (or, definite, "the porters", "the porter and the
        /// muffalo"); <paramref name="many"/>: the verb after it is plural.
        /// </summary>
        public static string People(CourierTrip t, bool definite, out bool many)
        {
            int n = Math.Max(1, t.people.Count);
            string noun = Noun(t.faction, n > 1);
            string who;
            if (definite) who = "CC_Courier_The".Translate(noun).Resolve();
            else if (n == 1) who = Find.ActiveLanguageWorker.WithIndefiniteArticle(noun);
            else who = (n == 2 ? "CC_Courier_Two" : "CC_Courier_Three").Translate(noun).Resolve();
            many = n > 1;
            if (t.animal != null)
            {
                string beast = definite ? "CC_Courier_The".Translate(t.animal.KindLabel).Resolve() : Find.ActiveLanguageWorker.WithIndefiniteArticle(t.animal.KindLabel);
                who = "CC_Courier_And".Translate(who, beast).Resolve();
                many = true;
            }
            return who.CapitalizeFirst();
        }

        private static string FactionName(CourierTrip t) => t.faction?.Name ?? t.preferred?.Name ?? "?";

        private static LookTargets Look(CourierTrip t)
        {
            var here = t.Party.Where(p => p.Spawned).ToList();
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        // ---------------- Messages ----------------

        /// <summary>They've come (walking in from the edge).</summary>
        public static void Arrive(CourierTrip t)
        {
            if (t == null || t.quiet) return;
            try
            {
                string who = People(t, false, out bool many);
                string kind = t.kind.ToString();
                string text = ("CC_Courier_Arrive_" + kind + (many ? "_Many" : "_One"))
                    .Translate(who, FactionName(t), t.takeLabel ?? t.bringLabel ?? "", t.bringLabel ?? "").Resolve();
                Notify.Message(text, Look(t), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier arrive words", e);
            }
        }

        /// <summary>Their business is done: what they set down, what they took.</summary>
        public static void Done(CourierTrip t)
        {
            if (t == null || t.quiet) return;
            try
            {
                string who = People(t, true, out _);
                string faction = FactionName(t);
                string where = (t.atGate ? "CC_Courier_ByGate" : "CC_Courier_NearColony").Translate().Resolve();
                string took = t.Takes ? (t.things.Count > 0 || t.taken >= t.amount ? t.takeLabel : Couriers.Amount(t.taken, t.what, t.def)) : t.takeLabel;
                string text;
                switch (t.kind)
                {
                    case CourierKind.Deliver:
                        text = "CC_Courier_Done_Deliver".Translate(who, faction, t.bringLabel ?? "", where).Resolve();
                        break;
                    case CourierKind.Exchange:
                        text = "CC_Courier_Done_Exchange".Translate(who, faction, took ?? "", where, t.bringLabel ?? "").Resolve();
                        break;
                    case CourierKind.Await:
                        text = "CC_Courier_Done_Await".Translate(who, faction, t.takeLabel ?? "").Resolve();
                        break;
                    default:
                        text = t.taken <= 0 ? "CC_Courier_Done_Nothing".Translate(who, faction).Resolve()
                            : "CC_Courier_Done_Collect".Translate(who, faction, took ?? "").Resolve();
                        break;
                }
                var look = Look(t);
                Notify.Message(text, look, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier done words", e);
            }
        }

        /// <summary>What was left of a deal went another way: they never came, or left before they were done.</summary>
        public static void Settled(CourierTrip t, CourierEnd end, bool incoming, bool outgoing, List<Thing> look)
        {
            if (t == null) return;
            try
            {
                string faction = FactionName(t);
                string text;
                if (t.people.Count == 0)
                {
                    // They never came.
                    var parts = new List<string>();
                    if (incoming) parts.Add("CC_Courier_Fell_In".Translate(t.bringLabel ?? "", faction).Resolve().CapitalizeFirst());
                    if (outgoing) parts.Add("CC_Courier_Fell_Out".Translate(t.takeLabel ?? "", faction).Resolve().CapitalizeFirst());
                    text = string.Join(" ", parts);
                }
                else
                {
                    string who = People(t, true, out _);
                    string key = end == CourierEnd.Danger ? "CC_Courier_End_Danger" : end == CourierEnd.TimedOut ? "CC_Courier_End_TimedOut" : "CC_Courier_End_Other";
                    text = key.Translate(who, faction).Resolve();
                    if (incoming) text += " " + "CC_Courier_End_In".Translate().Resolve();
                    if (outgoing) text += " " + "CC_Courier_End_Out".Translate(t.takeLabel ?? "").Resolve();
                }
                if (text.NullOrEmpty()) return;
                var targets = look?.Where(x => x != null && x.Spawned).ToList();
                Notify.Message(text, targets != null && targets.Count > 0 ? new LookTargets(targets) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier settled words", e);
            }
        }
    }
}
