using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing the colony can say to a camp, in a letter or from the World page.</summary>
    public class CampOption
    {
        public string label;
        public string tip;
        /// <summary>Why it can't be chosen now (not enough silver), or null.</summary>
        public string disabled;
        /// <summary>Does it; returns what happened, to tell (or null when a letter or window already tells it).</summary>
        public Func<string> act;
    }

    /// <summary>
    /// What the colony can do about a camp: let them be, set terms, give gifts, hire some of them, ask them to move on,
    /// or drive them off. The same answers serve the camp's letters and the World page.
    /// </summary>
    public static class CampAnswers
    {
        private static int Now => Find.TickManager.TicksGame;

        public static int Round10(float v) => Mathf.Max(10, Mathf.RoundToInt(v / 10f) * 10);

        /// <summary>A gift that means something to a group this size.</summary>
        public static int GiftSize(Camp c) => Round10((40f + 15f * Mathf.Max(1, c.Here.Count)) * LivingEvents.WealthFactor(c.map));

        public static int HirePrice(Pawn p) => Mathf.Clamp(Round10(p.MarketValue * 0.5f), 120, 1500);

        /// <summary>The colony's silver, in its stockpiles and home area.</summary>
        public static int Silver(Map map)
        {
            if (map == null) return 0;
            var home = map.areaManager.Home;
            return map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => Usable(t, home)).Sum(t => t.stackCount);
        }

        private static bool Usable(Thing t, Area home) =>
            t.Spawned && !t.IsForbidden(Faction.OfPlayer) && (StoreUtility.IsInAnyStorage(t) || (home != null && home[t.Position]));

        public static bool Pay(Map map, int amount)
        {
            if (amount <= 0) return true;
            if (Silver(map) < amount) return false;
            var home = map.areaManager.Home;
            int left = amount;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => Usable(t, home)).ToList())
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
            return true;
        }

        private static string NotEnough(Map map, int amount) =>
            Silver(map) < amount ? "CC_Dilemma_NotEnough".Translate(amount, ThingDefOf.Silver.label).ToString() : null;

        // ---------------- Answers ----------------

        /// <summary>Asked to move on. With a gift they go gladly; without, their people take it badly, and a proud group may refuse.</summary>
        public static string AskToLeave(Camp c, int gift, bool theyBrokeTerms = false)
        {
            if (c == null || !c.CanTalk) return null;
            c.asked = true;
            if (gift > 0)
            {
                c.gifts++;
                c.faction?.TryAffectGoodwillWith(Faction.OfPlayer, 2, true, false, HistoryEventDefOf.GaveGift);
                c.Leave("asked");
                return null;
            }
            if (!theyBrokeTerms)
            {
                int loss = 5;
                // The colony agreed to a stay and is cutting it short.
                if (c.termLimit && c.Settled && Now < c.untilTick)
                {
                    loss += 3;
                    c.AddTension(10f);
                }
                c.LoseGoodwill(loss);
            }
            float refuse = c.kind.pride * (0.5f + c.tension / 100f) * (c.refusals == 0 ? 1f : 0.5f) * (theyBrokeTerms ? 0.5f : 1f);
            if (c.Settled && Rand.Chance(refuse))
            {
                c.refusals++;
                c.asked = false;
                c.AddTension(10f);
                c.Ask("refused", c.Text("CC_Camp_RefusedTitle"), c.Text("CC_Camp_Refused", null, null, GiftSize(c)));
                return null;
            }
            c.Leave("asked");
            return null;
        }

        /// <summary>Asked to move on with a gift: the silver is only paid when they're still here to take it.</summary>
        public static string AskToLeavePaid(Camp c, int gift)
        {
            if (c == null || !c.CanTalk || !Pay(c.map, gift)) return null;
            return AskToLeave(c, gift);
        }

        /// <summary>After a refusal: told again, firmly. They go, and their people remember it.</summary>
        public static string Insist(Camp c)
        {
            if (c == null || !c.CanTalk) return null;
            c.LoseGoodwill(8);
            c.asked = true;
            c.Leave("asked");
            return null;
        }

        /// <summary>
        /// Driven off by force: their people take it as an attack (goodwill falls as the game does for one). If that
        /// makes them enemies, they hold their camp and fight; otherwise they leave at once.
        /// </summary>
        public static string DriveOff(Camp c)
        {
            if (c == null || !c.CanTalk) return null;
            int penalty = Mathf.RoundToInt(Mathf.Lerp(20f, 40f, c.kind.pride));
            c.faction?.TryAffectGoodwillWith(Faction.OfPlayer, -penalty, true, true, HistoryEventDefOf.AttackedMember);
            if (c.faction != null && c.faction.HostileTo(Faction.OfPlayer)) return c.Text("CC_Camp_Force_War");
            c.Leave("forced", true);
            return null;
        }

        public static string Gift(Camp c, int silver)
        {
            if (c == null || !c.CanTalk || !Pay(c.map, silver)) return null;
            c.gifts++;
            c.AddTension(-Mathf.Min(25f, silver / 8f));
            c.faction?.TryAffectGoodwillWith(Faction.OfPlayer, 2, true, false, HistoryEventDefOf.GaveGift);
            return c.Text("CC_Camp_Gifted", null, null, silver);
        }

        /// <summary>One of them joins the colony: hired for silver, or welcomed when they asked.</summary>
        public static string Hire(Camp c, Pawn p, int price)
        {
            if (c == null || p == null || p.Dead || !c.CanTalk || !c.Here.Contains(p)) return null;
            if (price > 0 && !Pay(c.map, price)) return null;
            if (p == c.trader)
            {
                p.mindState.wantsToTradeWithColony = false;
                if (p.trader != null) p.trader.traderKind = null;
                c.trader = null;
            }
            p.ownership?.UnclaimAll();
            if (!Arrivals.Join(p, c.map)) return null;
            c.willing.Remove(p);
            if (!c.joined.Contains(p)) c.joined.Add(p);
            string text = (price > 0 ? "CC_Camp_Hired" : "CC_Camp_Welcomed").Translate(p.Named("X"), (c.faction?.Name ?? "").Named("FACTION"), c.Label.Named("NAME"), price.Named("COUNT")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Camp_JoinedTitle".Translate(p.Named("X")).Resolve(), text, LetterDefOf.PositiveEvent, p);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, p));
            return null;
        }

        /// <summary>
        /// Terms put to them: each one they may take or refuse (proud groups refuse more, and a poor one can't pay a
        /// fee). Every term they accept costs a little goodwill between the two; dropping a term eases it.
        /// </summary>
        public static string SetTerms(Camp c, bool fee, bool share, bool boundary, bool limit)
        {
            if (c == null || !c.CanTalk) return null;
            var took = new List<string>();
            var refused = new List<string>();
            float baseRefuse = c.kind.pride * 0.6f + c.tension / 250f;
            void Try(bool want, ref bool have, string key, bool can, float extra)
            {
                if (want == have) return;
                if (!want)
                {
                    have = false;
                    c.AddTension(-2f);
                    return;
                }
                if (!can || Rand.Chance(Mathf.Clamp01(baseRefuse + extra)))
                {
                    refused.Add(key.Translate());
                    c.AddTension(5f);
                    return;
                }
                have = true;
                took.Add(key.Translate());
                c.AddTension(3f);
            }
            Try(fee, ref c.termFee, "CC_Camp_Term_Fee", c.kind.canPay, 0.15f);
            Try(share, ref c.termShare, "CC_Camp_Term_Share", c.kind.huntsPerDay > 0f, 0.05f);
            Try(boundary, ref c.termBoundary, "CC_Camp_Term_Boundary", true, 0f);
            Try(limit, ref c.termLimit, "CC_Camp_Term_Limit", true, 0.05f);
            if (c.termFee && c.fee <= 0)
            {
                c.fee = Round10(Mathf.Max(1, c.Here.Count) * 12f * LivingEvents.WealthFactor(c.map));
                c.nextFeeTick = Now + GenDate.TicksPerDay;
            }
            if (c.termLimit && c.arrivedTick >= 0) c.untilTick = Mathf.Min(c.untilTick, Now + GenDate.TicksPerDay * 3);
            if (took.Count == 0 && refused.Count == 0) return c.Text("CC_Camp_TermsSame");
            string text = "";
            if (took.Count > 0) text += "CC_Camp_TermsTook".Translate(TextUtil.JoinAnd(took)).Resolve();
            if (refused.Count > 0) text += (text.Length > 0 ? " " : "") + "CC_Camp_TermsRefused".Translate(TextUtil.JoinAnd(refused)).Resolve();
            return text;
        }

        public static string Remind(Camp c)
        {
            if (c == null || !c.CanTalk) return null;
            c.bendBoundary = false;
            c.skipShare = false;
            c.feeLate = false;
            c.AddTension(3f);
            if (Rand.Chance(c.kind.pride * 0.5f))
            {
                c.AddTension(8f);
                return c.Text("CC_Camp_RemindedStiff");
            }
            return c.Text("CC_Camp_Reminded");
        }

        public static string Extend(Camp c)
        {
            if (c == null || !c.CanTalk) return null;
            c.extended = true;
            c.untilTick = Now + Mathf.RoundToInt(Rand.Range(2f, 5f) * GenDate.TicksPerDay);
            c.AddTension(-2f);
            return c.Text("CC_Camp_Extended");
        }

        // ---------------- Letters ----------------

        public static List<CampOption> Options(Camp c, string what, Pawn pawn, string askKey = null)
        {
            var list = new List<CampOption>();
            int gift = GiftSize(c);
            switch (what)
            {
                case "request":
                {
                    // One of their requests (CampRequests.cs): grant it (the ask is done, its effects play out), or say no.
                    var ask = c.AskByKey(askKey);
                    if (ask == null) break;
                    var who = c.AskColonist(ask);
                    int cost = c.AskCost(ask);
                    list.Add(new CampOption
                    {
                        label = c.Format(ask.label, null, who, cost), disabled = c.AskBlocked(ask), act = () =>
                        {
                            c.DoAsk(ask);
                            return null;
                        }
                    });
                    list.Add(new CampOption
                    {
                        label = ask.refuseLabel.NullOrEmpty() ? c.Text("CC_CampAsk_Refuse") : c.Format(ask.refuseLabel, null, who, cost),
                        act = () => c.Refuse(ask, false)
                    });
                    break;
                }
                case "join":
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Welcome", pawn), act = () => Hire(c, pawn, 0) });
                    list.Add(new CampOption
                    {
                        label = c.Text("CC_Camp_Opt_NotNow"), act = () =>
                        {
                            c.willing.Remove(pawn);
                            return c.Text("CC_Camp_JoinDeclined", pawn);
                        }
                    });
                    break;
                case "tension":
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Terms"), act = () => Talk(c, true) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Gift", null, null, gift), disabled = NotEnough(c.map, gift), act = () => Gift(c, gift) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_AskLeave"), act = () => AskToLeave(c, 0) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_LetBe"), act = () => null });
                    break;
                case "broken":
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Remind"), act = () => Remind(c) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_AskLeave"), act = () => AskToLeave(c, 0, true) });
                    list.Add(new CampOption
                    {
                        label = c.Text("CC_Camp_Opt_LetGo"), act = () =>
                        {
                            c.AddTension(-2f);
                            return null;
                        }
                    });
                    break;
                case "overstay":
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Extend"), act = () => Extend(c) });
                    list.Add(new CampOption
                    {
                        label = c.Text("CC_Camp_Opt_TimeToGo"), act = () =>
                        {
                            c.TimeToGo();
                            return null;
                        }
                    });
                    break;
                case "refused":
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_AskGift", null, null, gift), disabled = NotEnough(c.map, gift), act = () => AskToLeavePaid(c, gift) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Insist"), act = () => Insist(c) });
                    list.Add(new CampOption { label = c.Text("CC_Camp_Opt_Force"), tip = c.Text("CC_Camp_Opt_Force_Tip"), act = () => DriveOff(c) });
                    list.Add(new CampOption
                    {
                        label = c.Text("CC_Camp_Opt_LetStay"), act = () =>
                        {
                            c.AddTension(-5f);
                            return c.Text("CC_Camp_LetStay");
                        }
                    });
                    break;
            }
            return list;
        }

        public static int DefaultIndex(string what)
        {
            switch (what)
            {
                case "join":
                case "overstay":
                case "request": return 1;
                case "broken": return 2;
                case "tension":
                case "refused": return 3;
                default: return 0;
            }
        }

        public static string DefaultLabel(Camp c, string what, Pawn pawn, string askKey = null)
        {
            var opts = Options(c, what, pawn, askKey);
            int i = DefaultIndex(what);
            return i < opts.Count ? opts[i].label : "";
        }

        /// <summary>Opens the talk with the camp (from a letter or the World page).</summary>
        public static string Talk(Camp c, bool terms)
        {
            if (c == null || !c.CanTalk) return null;
            Find.WindowStack.Add(new Dialog_Camp(c, terms));
            return null;
        }
    }

    /// <summary>A question about the camp, with its answers worked out when it's opened (see <see cref="CampAnswers.Options"/>).</summary>
    public class ChoiceLetter_Camp : ChoiceLetter
    {
        public int campId;
        public string what;
        public Pawn pawn;
        public bool resolved;
        /// <summary>The request a "request" letter is about (CampAsk.key).</summary>
        public string askKey;

        public override bool CanDismissWithRightClick => false;

        // Once they're leaving or at war, the question is moot: the letter only closes, and running out of time does nothing.
        private Camp Camp => CampTracker.Instance?.camp is Camp c && c.id == campId && c.CanTalk ? c : null;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var camp = Camp;
                if (ArchivedOnly || resolved || camp == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                var opts = CampAnswers.Options(camp, what, pawn, askKey);
                int def = CampAnswers.DefaultIndex(what);
                for (int i = 0; i < opts.Count; i++)
                {
                    var o = opts[i];
                    var d = new DiaOption(o.label);
                    if (o.disabled != null && i != def) d.Disable(o.disabled);
                    d.action = () =>
                    {
                        resolved = true;
                        Answer(camp, o);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    d.resolveTree = true;
                    yield return d;
                }
                yield return Option_JumpToLocation;
                yield return Option_Postpone;
            }
        }

        private static void Answer(Camp camp, CampOption o)
        {
            try
            {
                string told = o.act?.Invoke();
                if (!told.NullOrEmpty()) Messages.Message(told, camp.Look, MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp answer", e);
            }
        }

        /// <summary>Unanswered when the time runs out: the default happens.</summary>
        public override void Removed()
        {
            base.Removed();
            if (resolved) return;
            resolved = true;
            var camp = Camp;
            if (camp == null) return;
            // A request left unanswered: they take the silence for a no, and mind it less than a plain one.
            if (what == "request")
            {
                var ask = camp.AskByKey(askKey);
                if (ask != null) Answer(camp, new CampOption { act = () => camp.Refuse(ask, true) });
                return;
            }
            var opts = CampAnswers.Options(camp, what, pawn, askKey);
            int i = CampAnswers.DefaultIndex(what);
            if (i < opts.Count) Answer(camp, opts[i]);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref campId, "campId");
            Scribe_Values.Look(ref what, "what");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref askKey, "askKey");
        }
    }
}
