using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What would help, as advice the player can act on (docs/design/heart-felt.md, release 2, point 13; the player's words
    /// of 9 November 2026: "include recommendations and advice on potential actions i can take to reduce strain. for
    /// example certain decrees. or i could for example also just grant a single stained pawn a break day"). For each
    /// strained colonist, the few things that would ease them most, ranked from what is wearing on them now (the heart's
    /// sources lately, their needs and how they are; HeartCountMath.Rank) and what the colony has: a day off, a decree of the
    /// council's that eases strain (with what it costs), a gathering, time with a friend, a proper meal, a better bed,
    /// recreation, their pain treated, keeping them out of the next fight, time to grieve, light. Each with a button where
    /// Rimfolk can do it (give the day off, put the decree to the council as their petition, call a party, send a friend
    /// over); where it can't, it says plainly what the player can do. Built at most once a game hour for each colonist
    /// (kept unsaved on the heart), never per frame. Shown on the heart's page, the card, the message when someone becomes
    /// worn down, the council's Moods page for the whole colony, and RimHUD's tooltip.
    /// </summary>
    public static class HeartAdvice
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The heart and its advice are on.</summary>
        public static bool On => Heart.On && (S?.heartAdvice ?? true);

        /// <summary>One piece of advice: what it is, why, and the button where Rimfolk can do it.</summary>
        public sealed class Item
        {
            public AdviceKind kind;
            public float weight;
            public DecreeDef decree;
            /// <summary>The friend it names (time with a friend), if any.</summary>
            public Pawn friend;
            /// <summary>In a few words ("a day off", "days of rest"), for short lists, and in a sentence with what it costs.</summary>
            public string name, text;
            /// <summary>The button's label, or null where Rimfolk can't do it; why it can't now, or null.</summary>
            public string act, whyNot, tip;
            public Action run;
        }

        /// <summary>How close two colonists are for the heart (HeartCountMath.Closeness): a partner, kin by blood, a friend by opinion.</summary>
        public static float ClosenessOf(Pawn p, Pawn o)
        {
            if (p?.relations == null || o == null || o == p) return 0f;
            return HeartCountMath.Closeness(ArcUtil.Partners(p, o), ArcUtil.Kin(p, o), p.relations.OpinionOf(o));
        }

        /// <summary>
        /// What would help them now, the most easing first: built at most once a game hour, and again after a button was
        /// used (the list is kept: callers show its first few, never copying it per frame). Empty below strained, with the
        /// advice off, or with nothing to say.
        /// </summary>
        public static List<Item> For(Pawn p, PawnHeart h)
        {
            if (!On || p == null || h == null || h.strain < HeartMath.BandStart(StrainBand.Strained)) return Empty;
            int now = Find.TickManager.TicksGame;
            if (h.advice == null || now - h.adviceTick >= GenDate.TicksPerHour || now < h.adviceTick)
            {
                try
                {
                    h.advice = Build(p, h, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the heart's advice", e);
                    h.advice = new List<Item>();
                }
                h.adviceTick = now;
            }
            return h.advice;
        }

        private static readonly List<Item> Empty = new List<Item>();

        /// <summary>The advice is built again at the next look (a button was used, the day changed).</summary>
        public static void Stale(PawnHeart h)
        {
            if (h != null) h.adviceTick = -999999;
        }

        /// <summary>The names of the first few ("a day off, days of rest and a proper meal"), for a message; null with none.</summary>
        public static string Short(Pawn p, PawnHeart h, int n)
        {
            var list = For(p, h);
            return list.Count == 0 ? null : TextUtil.JoinAnd(list.Take(n).Select(i => i.name).ToList());
        }

        /// <summary>The advice in lines, for a tooltip (RimHUD's, the card's): "What would help: ..." and each in its sentence.</summary>
        public static string Tip(Pawn p, PawnHeart h, int n = 3)
        {
            var list = For(p, h);
            if (list.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            sb.Append("CC_Advice_Head".Translate(p.Named("PAWN")).Resolve());
            for (int i = 0; i < list.Count && i < n; i++) sb.Append("\n- ").Append(list[i].text);
            return sb.ToString();
        }

        // ---------------- Building it ----------------

        private static List<Item> Build(Pawn p, PawnHeart h, int now)
        {
            var f = new HeartCountMath.AdviceFacts { strain = h.strain };
            for (int i = 0; i < h.recentBuild.Count && i < f.recent.Length; i++) f.recent[i] = h.recentBuild[i];
            var needs = p.needs;
            f.hungry = needs?.food != null && needs.food.CurCategory >= HungerCategory.Hungry;
            f.inPain = (p.health?.hediffSet?.PainTotal ?? 0f) >= 0.2f;
            f.joyless = needs?.joy != null && needs.joy.CurLevelPercentage < 0.25f;
            f.badBed = p.ownership != null && p.ownership.OwnedBed == null;
            f.lonely = h.needs != null && h.needs.Length == PawnHeart.NeedCount && h.needs[(int)HeartNeed.Belonging].value < HeartNeedMath.MissedBelow;
            f.grieving = (WeightKind)h.weightKind == WeightKind.Loss && h.weight >= 10f;
            f.dayOff = HeartTimeOff.DayOffOn;
            var map = p.MapHeld;
            f.gathering = map != null && map.IsPlayerHome && map.gatherSpotLister.activeSpots.Count > 0;

            var items = new List<Item>();
            foreach (var (kind, weight) in HeartCountMath.Rank(f))
            {
                var item = Make(p, h, kind, now);
                if (item == null) continue;
                item.weight = weight;
                items.Add(item);
            }
            // The council's decrees that ease strain: the best two that suit what wears on them, each with what it costs.
            var decrees = DecreeTracker.Of(p);
            if (decrees != null)
            {
                var best = new List<(DecreeDef def, float weight)>();
                foreach (var def in Easing)
                {
                    if (def.Hidden) continue;
                    float w = HeartCountMath.DecreeWeight(f, def.strainRelief > 1f ? null : def.heartEases);
                    if (w >= HeartCountMath.AdviceMin) best.Add((def, w));
                }
                foreach (var (def, w) in best.OrderByDescending(x => x.weight).Take(2))
                {
                    var item = Decree(p, def, decrees);
                    item.weight = w;
                    items.Add(item);
                }
            }
            items.Sort((a, b) => b.weight.CompareTo(a.weight));
            return items;
        }

        private static List<DecreeDef> easing;

        /// <summary>The council's decrees that ease strain: days of rest (strainRelief), and those that name what they ease (heartEases).</summary>
        public static List<DecreeDef> Easing => easing ??= DefDatabase<DecreeDef>.AllDefsListForReading.Where(d => d.strainRelief > 1f || !d.heartEases.NullOrEmpty()).ToList();

        public static void ResetCaches() => easing = null;

        private static Item Make(Pawn p, PawnHeart h, AdviceKind kind, int now)
        {
            var item = new Item { kind = kind, name = ("CC_Advice_Name_" + kind).Translate().Resolve() };
            switch (kind)
            {
                case AdviceKind.DayOff:
                    if (h.Off(TimeOffKind.DayOff))
                    {
                        item.text = "CC_Advice_DayOffNow".Translate(p.Named("PAWN")).Resolve();
                        return item;
                    }
                    item.text = "CC_Advice_DayOff".Translate(p.Named("PAWN")).Resolve();
                    item.act = HeartTimeOff.DayOffLabel(p, h, now);
                    item.whyNot = HeartTimeOff.WhyNotDayOff(p, h, now);
                    item.tip = "CC_Count_DayOff_Desc".Translate(p.Named("PAWN")).Resolve();
                    item.run = () => HeartTimeOff.GiveDayOff(p);
                    return item;
                case AdviceKind.Gathering:
                {
                    item.text = "CC_Advice_Gathering".Translate(p.Named("PAWN")).Resolve();
                    item.act = "CC_Advice_Act_Party".Translate().Resolve();
                    item.whyNot = PartyWhyNot(p);
                    item.tip = "CC_Advice_Act_PartyTip".Translate().Resolve();
                    item.run = () => Party(p);
                    return item;
                }
                case AdviceKind.Friend:
                {
                    var friend = FriendOf(p);
                    item.friend = friend;
                    if (friend == null)
                    {
                        item.text = "CC_Advice_FriendNone".Translate(p.Named("PAWN")).Resolve();
                        return item;
                    }
                    item.text = "CC_Advice_Friend".Translate(p.Named("PAWN"), friend.Named("FRIEND")).Resolve();
                    item.act = "CC_Advice_Act_Friend".Translate(friend.Named("FRIEND")).Resolve();
                    item.whyNot = Together.Able(friend) && Together.Able(p) ? null : "CC_Advice_FriendBusy".Translate(friend.Named("FRIEND")).Resolve();
                    item.tip = "CC_Advice_Act_FriendTip".Translate(p.Named("PAWN"), friend.Named("FRIEND")).Resolve();
                    item.run = () =>
                    {
                        // together: the talk itself waits for the two to meet, one walking over (HeartActs.FriendVisit, Together.When);
                        // the message only says it is arranged.
                        if (HeartActs.FriendVisit(p, friend))
                            Notify.Message("CC_Advice_FriendSent".Translate(p.Named("PAWN"), friend.Named("FRIEND")).Resolve(), p, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                    };
                    return item;
                }
                case AdviceKind.Recreation:
                {
                    string bored = Bored(p);
                    item.text = bored == null ? "CC_Advice_Recreation".Translate(p.Named("PAWN")).Resolve()
                        : "CC_Advice_RecreationBored".Translate(p.Named("PAWN"), bored.Named("BORED")).Resolve();
                    return item;
                }
                default:
                    item.text = ("CC_Advice_" + kind).Translate(p.Named("PAWN")).Resolve();
                    return item;
            }
        }

        /// <summary>
        /// A decree of the council's: what it does for them and what it costs; its button puts it to the council as their
        /// petition (or someone else's who likes it, if they don't), and opens the Decrees page where it is declared.
        /// </summary>
        private static Item Decree(Pawn p, DecreeDef def, DecreeTracker tracker)
        {
            string help = def.heartHelp.NullOrEmpty() ? "CC_Advice_DecreeEases".Translate().Resolve() : def.heartHelp;
            string cost = def.heartCost.NullOrEmpty() ? "CC_Advice_DecreeCostPlain".Translate(TextUtil.DaysString(Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay))).Resolve() : def.heartCost;
            var item = new Item
            {
                kind = AdviceKind.Decree, decree = def, name = "CC_Advice_Name_Decree".Translate(def.label.Named("DECREE")).Resolve(),
                text = "CC_Advice_Decree".Translate(def.LabelCap.Named("DECREE"), help.Named("HELP"), cost.Named("COST")).Resolve()
            };
            if (tracker.IsActive(def))
            {
                item.text = "CC_Advice_DecreeInForce".Translate(def.LabelCap.Named("DECREE"), help.Named("HELP")).Resolve();
                return item;
            }
            item.act = tracker.RequestFor(def) != null ? "CC_Advice_Act_SeeDecree".Translate().Resolve() : "CC_Advice_Act_Propose".Translate().Resolve();
            item.whyNot = tracker.CannotEnact(def);
            item.tip = "CC_Advice_Act_ProposeTip".Translate(def.label.Named("DECREE")).Resolve();
            item.run = () => Propose(p, def);
            return item;
        }

        // ---------------- What the buttons do ----------------

        /// <summary>
        /// Puts a decree to the council through its own path: their petition (DecreeTracker.Petition; someone who likes it if
        /// they would rather not), and the Decrees page opened where it is declared. A petition left unanswered lapses as any
        /// does.
        /// </summary>
        public static void Propose(Pawn p, DecreeDef def)
        {
            var tracker = DecreeTracker.Of(p);
            if (tracker == null || def == null) return;
            if (!tracker.IsActive(def) && tracker.RequestFor(def) == null && tracker.CannotEnact(def) == null)
            {
                var asker = def.ReactionOf(p) >= DecreeReaction.Neutral && CouncilGameComponent.CanVoiceWishes(p) ? p
                    : p.MapHeld?.mapPawns.FreeColonistsSpawned.Where(o => def.ReactionOf(o) >= DecreeReaction.Like && CouncilGameComponent.CanVoiceWishes(o)).RandomElementWithFallback();
                if (asker != null) tracker.Petition(def, asker);
            }
            Stale(Heart.Of(p));
            MainTabWindow_Council.OpenTab("Decrees");
        }

        /// <summary>Why the colony can't call a party now, or null (the game's own party: somewhere to gather, someone to host).</summary>
        public static string PartyWhyNot(Pawn p)
        {
            var map = p?.MapHeld;
            var party = GatheringDefOf.Party;
            if (map == null || party?.Worker == null || !map.IsPlayerHome) return "CC_Advice_PartyAway".Translate().Resolve();
            if (map.gatherSpotLister.activeSpots.Count == 0) return "CC_Advice_PartyNoSpot".Translate().Resolve();
            if (MapFacts.ThreatOn(map)) return "CC_Count_WhyNot_Danger".Translate(p.Named("PAWN")).Resolve();
            try
            {
                if (!party.Worker.CanExecute(map)) return "CC_Advice_PartyNotNow".Translate().Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's advice: a party", e);
                return "CC_Advice_PartyNotNow".Translate().Resolve();
            }
            return null;
        }

        /// <summary>Calls the game's party, thrown by the one strained if they can host it, else by anyone who can.</summary>
        public static bool Party(Pawn p)
        {
            var map = p?.MapHeld;
            var party = GatheringDefOf.Party;
            if (map == null || party?.Worker == null || PartyWhyNot(p) != null) return false;
            try
            {
                bool done = p.Spawned && p.Map == map && party.Worker.CanExecute(map, p) && party.Worker.TryExecute(map, p);
                if (!done) done = party.Worker.TryExecute(map);
                if (done) Stale(Heart.Of(p));
                return done;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's advice: calling a party", e);
                return false;
            }
        }

        /// <summary>The one closest to them (a partner, kin, a friend by opinion) at home, or null.</summary>
        public static Pawn FriendOf(Pawn p)
        {
            var map = p?.MapHeld;
            if (map == null) return null;
            Pawn best = null;
            float bestC = 0f;
            foreach (var o in map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.DevelopmentalStage.Baby() || o.Dead) continue;
                // Not someone as worn down as they are: a friend with something to give.
                var oh = Heart.Of(o);
                if (oh != null && oh.Band >= StrainBand.Worn) continue;
                float c = ClosenessOf(p, o);
                if (c > bestC)
                {
                    bestC = c;
                    best = o;
                }
            }
            return best;
        }

        /// <summary>The kinds of recreation they've had too much of lately ("socializing and television"), or null.</summary>
        private static string Bored(Pawn p)
        {
            var tol = p.needs?.joy?.tolerances;
            if (tol == null) return null;
            var kinds = new List<string>();
            foreach (var k in DefDatabase<JoyKindDef>.AllDefsListForReading)
                if (tol.BoredOf(k) && kinds.Count < 3) kinds.Add(k.label);
            return kinds.Count == 0 ? null : TextUtil.JoinAnd(kinds);
        }

        // ---------------- Drawing ----------------

        /// <summary>
        /// The advice as rows, each its sentence and, at its right, its button (greyed with why not) where Rimfolk can act.
        /// Returns the height used. The sentences are measured as drawn; the list itself is built hourly.
        /// </summary>
        public static float Draw(Pawn p, List<Item> items, float x, float y, float w, int max = 4)
        {
            if (items == null || items.Count == 0) return 0f;
            float top = y;
            for (int k = 0; k < items.Count && k < max; k++)
            {
                var i = items[k];
                float bw = i.act != null ? UiKit.RowActWidth(i.act) : 0f;
                float tw = Mathf.Max(40f, w - (bw > 0f ? bw + 6f : 0f));
                float th = UiKit.Paragraph(x, y, tw, "· " + i.text, MoodView.AdviceColor, GameFont.Tiny);
                float rh = Mathf.Max(th, i.act != null ? 20f : 0f);
                if (i.act != null && UiKit.RowAct(new Rect(x + w - bw, y, bw, 20f), i.act, i.whyNot, i.tip))
                {
                    try
                    {
                        i.run?.Invoke();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("the heart's advice: a button", e);
                    }
                    Stale(Heart.Of(p));
                }
                y += rh + 3f;
            }
            return y - top;
        }

        // ---------------- The whole colony (the council's Moods page) ----------------

        /// <summary>One row of the colony's advice: what, for whom, and its button.</summary>
        public sealed class ColonyRow
        {
            public string text;
            public Item item;
            public Pawn pawn;
        }

        /// <summary>
        /// The colony's advice, for the Moods page: each decree once with everyone it would ease, a party once, and each
        /// strained colonist's own first two (a day off, a friend, a meal...). The most easing first, at most
        /// <paramref name="max"/>.
        /// </summary>
        public static List<ColonyRow> Colony(IEnumerable<Pawn> pawns, int max = 8)
        {
            var rows = new List<(ColonyRow row, float weight)>();
            if (!On) return new List<ColonyRow>();
            var byDecree = new Dictionary<DecreeDef, (Item item, Pawn first, List<string> who, float weight)>();
            (Item item, Pawn first, List<string> who, float weight)? party = null;
            foreach (var p in pawns)
            {
                var h = Heart.Of(p);
                if (h == null || h.strain < HeartMath.BandStart(StrainBand.Strained)) continue;
                int personal = 0;
                var all = For(p, h);
                for (int k = 0; k < all.Count && k < 5; k++)
                {
                    var i = all[k];
                    if (i.kind == AdviceKind.Decree && i.decree != null)
                    {
                        if (!byDecree.TryGetValue(i.decree, out var d)) d = (i, p, new List<string>(), 0f);
                        d.who.Add(p.LabelShort);
                        d.weight += i.weight;
                        byDecree[i.decree] = d;
                    }
                    else if (i.kind == AdviceKind.Gathering)
                    {
                        var g = party ?? (i, p, new List<string>(), 0f);
                        g.who.Add(p.LabelShort);
                        g.weight += i.weight;
                        party = g;
                    }
                    else if (personal < 2)
                    {
                        personal++;
                        rows.Add((new ColonyRow { pawn = p, item = i, text = "CC_Advice_ColonyFor".Translate(p.Named("PAWN"), i.text.Named("WHAT")).Resolve() }, i.weight));
                    }
                }
            }
            foreach (var d in byDecree.Values)
                rows.Add((new ColonyRow { pawn = d.first, item = d.item, text = "CC_Advice_ColonyMany".Translate(d.item.text.Named("WHAT"), TextUtil.JoinAnd(d.who).Named("WHO")).Resolve() }, d.weight));
            if (party != null)
            {
                var g = party.Value;
                rows.Add((new ColonyRow { pawn = g.first, item = g.item, text = "CC_Advice_ColonyMany".Translate(g.item.text.Named("WHAT"), TextUtil.JoinAnd(g.who).Named("WHO")).Resolve() }, g.weight));
            }
            return rows.OrderByDescending(r => r.weight).Take(max).Select(r => r.row).ToList();
        }
    }
}
