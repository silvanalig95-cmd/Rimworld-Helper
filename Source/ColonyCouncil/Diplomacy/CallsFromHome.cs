using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A call to arms answered from home (docs/content-update-proposals.md, B27; the numbers are CallsFromHomeMath's), for a
    /// colony that never leaves its walls: two more answers on a defence partner's call, a liege's call for levies and a
    /// creditor's call for fighters (Diplomacy/CallsToArms.cs). Supplies in place of fighters: food, medicine and up to two
    /// spare weapons from the stores, each carried out to the gate by the colony's people, where their runners wait to take
    /// them up the road (DilemmaEffect_CarryOut and the couriers); under fealty, from the medieval age on, a season's tribute in
    /// silver in place of service, carried out the same way. Or the colony's beds for their wounded: two to four of them come
    /// a day to three later, hurt from their fight, and lie in its beds as its guests until they can walk home
    /// (World/Region/TakenIn.cs); their people remember the doctor who tended them (the game's own count of a guest tended,
    /// World/KinMemory.cs). Either keeps the colony's word as a smaller kept word than fighters: the pact goes on, the debt is
    /// settled, the levies count as sent, each remembered at half the weight.
    /// </summary>
    public static class CallsFromHome
    {
        // ================= What goes =================

        /// <summary>Silver in place of service: a liege's call on a colony past the tribal age.</summary>
        public static bool PaysSilver(Tie t) => t != null && CallsFromHomeMath.PaysSilver(t.kind == Tie.Kind.Fealty, (int)Ages.Ours);

        public static int SilverSum(Map home) => CallsFromHomeMath.Silver(home?.wealthWatcher?.WealthTotal ?? 0f);

        /// <summary>The supplies the stores can send now: food (raw, or meals), medicine, and up to two spare weapons.</summary>
        public static List<(string what, int count)> Supplies(Map home)
        {
            var list = new List<(string, int)>();
            if (home == null) return list;
            var food = CallsFromHomeMath.FoodKind(PlayCarry.Have(home, "rawfood", null), PlayCarry.Have(home, "meals", null));
            if (food != null) list.Add((food, CallsFromHomeMath.FoodCount(food)));
            list.Add(("medicine", CallsFromHomeMath.Medicine));
            int arms = CallsFromHomeMath.ArmsSent(PlayCarry.Have(home, PlayCarry.Arms, null));
            if (arms > 0) list.Add((PlayCarry.Arms, arms));
            return list;
        }

        /// <summary>"60 raw food, 6 medicine and 2 weapons".</summary>
        public static string Words(List<(string what, int count)> list) => TextUtil.JoinAnd(list.Select(x => Couriers.Amount(x.count, x.what, null)).ToList());

        /// <summary>Why the colony can't send supplies (or silver) now: too little in the stores; null when it can.</summary>
        public static string WhyNot(Tie t, Map home)
        {
            if (home == null) return "CC_Arms_NoHome".Translate().Resolve();
            if (PaysSilver(t))
            {
                int sum = SilverSum(home);
                return PlayCarry.Have(home, "silver", ThingDefOf.Silver) < sum ? "CC_Dilemma_NotEnough".Translate(sum, ThingDefOf.Silver.label).ToString() : null;
            }
            int raw = PlayCarry.Have(home, "rawfood", null), meals = PlayCarry.Have(home, "meals", null), med = PlayCarry.Have(home, "medicine", null);
            if (CallsFromHomeMath.SuppliesEnough(raw, meals, med)) return null;
            return CallsFromHomeMath.FoodKind(raw, meals) == null
                ? "CC_Dilemma_NotEnough".Translate(CallsFromHomeMath.Food, PlayCarry.Label("rawfood", null)).ToString()
                : "CC_Dilemma_NotEnough".Translate(CallsFromHomeMath.Medicine, PlayCarry.Label("medicine", null)).ToString();
        }

        /// <summary>Why the colony can't take in their wounded: nobody of it can tend wounds; null when it can.</summary>
        public static string WoundedWhyNot(Map home) =>
            home != null && home.mapPawns.FreeColonistsSpawned.Any(p => p != null && !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                ? null
                : "CC_Arms_NoDoctor".Translate().Resolve();

        /// <summary>The answer's label: "Send supplies instead (60 raw food, 6 medicine and 2 weapons)", or "Pay 300 silver in place of levies".</summary>
        public static string Label(Tie t, Map home, Faction f)
        {
            if (PaysSilver(t)) return PactWords.P("CC_Arms_Opt_Silver", SilverSum(home).Named("COUNT"), FealtyWords.LevyForm(home, f).word.Named("WORD"));
            var list = Supplies(home);
            return PactWords.P("CC_Arms_Opt_Supplies", (list.Count > 0 ? Words(list) : PlayCarry.Label("medicine", null)).Named("GOODS"));
        }

        /// <summary>The letter's paragraph on answering from home (flavour words, as the letters are).</summary>
        public static string LetterText(Tie t, Map home, Faction f)
        {
            if (PaysSilver(t))
                return PactWords.P("CC_Arms_FromHome_Silver", SilverSum(home).Named("COUNT"), FealtyWords.LevyForm(home, f).word.Named("WORD"), PactWords.People(f).Named("PEOPLE"));
            return PactWords.P("CC_Arms_FromHome", PactWords.People(f).Named("PEOPLE"));
        }

        /// <summary>The people the call was against: a pact's or a liege's (kept on the tie), or a debt's.</summary>
        public static Faction EnemyOf(Tie t, bool pact) =>
            t == null ? null : pact ? Refs.Faction(t.terms.about) : t.terms.count > 0 ? Refs.Faction(Ref.People(t.terms.count - 1)) : null;

        // ================= The answers =================

        /// <summary>Supplies (or silver) carried to the gate for their runners, and the call kept as a smaller kept word.</summary>
        public static void Send(Tie t, Faction f, Map home, Settlement town, bool pact)
        {
            if (t == null || f == null || home == null) return;
            var enemy = EnemyOf(t, pact);
            string townName = town?.Name ?? f.Name;
            var ctx = new DilemmaContext { map = home, faction = f };
            string label = PactWords.P("CC_Arms_CarryLabel", townName.Named("TOWN"));
            string what;
            if (PaysSilver(t))
            {
                int sum = SilverSum(home);
                // apart: one of the colony carries the silver out to the gate, where their collector waits to take it up the road.
                new DilemmaEffect_CarryOut { what = "silver", count = sum, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = label }.Apply(ctx);
                what = Couriers.Amount(sum, "silver", ThingDefOf.Silver);
                Kept(t, f, home, town, enemy, pact, PactWords.P("CC_Arms_Hist_Silver", PactWords.People(f).Named("PEOPLE"), sum.Named("COUNT"), FealtyWords.LevyForm(home, f).word.Named("WORD")),
                    PactWords.P("CC_Arms_Deed_Silver", sum.Named("COUNT")));
                Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P("CC_Arms_Msg_Silver", what.Named("GOODS"), PactWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
                return;
            }
            var list = Supplies(home);
            foreach (var (part, n) in list)
                // apart: the colony's people carry each part out to the gate, where their runners wait to take it to the fight.
                new DilemmaEffect_CarryOut { what = part, count = n, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = label }.Apply(ctx);
            what = Words(list);
            Kept(t, f, home, town, enemy, pact, PactWords.P("CC_Arms_Hist_Supplies", PactWords.People(f).Named("PEOPLE"), townName.Named("TOWN"), what.Named("GOODS")),
                PactWords.P("CC_Arms_Deed_Supplies", townName.Named("TOWN")));
            Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P("CC_Arms_Msg_Supplies", what.Named("GOODS"), PactWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The colony's beds for their wounded: two to four come in a day to three, and the call is kept as a smaller kept word.</summary>
        public static void OfferBeds(Tie t, Faction f, Map home, Settlement town, bool pact)
        {
            if (t == null || f == null || home == null) return;
            string townName = town?.Name ?? f.Name;
            TakenIn.Expect(f, home, CallsFromHomeMath.Wounded(Rand.Value), townName);
            Kept(t, f, home, town, EnemyOf(t, pact), pact, PactWords.P("CC_Arms_Hist_Wounded", PactWords.People(f).Named("PEOPLE"), townName.Named("TOWN")),
                PactWords.P("CC_Arms_Deed_Wounded", townName.Named("TOWN")));
            Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P("CC_Arms_Msg_Wounded", PactWords.People(f).Named("PEOPLE"), townName.Named("TOWN")), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The call kept from home: in the history (dip:arms, or dip:levy for a liege), remembered at half a fighter's weight (the
        /// deed keptWord, or levies), and the call closed: a pact's cleared (the pact goes on), a liege's cleared, a debt settled.
        /// </summary>
        private static void Kept(Tie t, Faction f, Map home, Settlement town, Faction enemy, bool pact, string history, string deed)
        {
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For) };
            if (enemy != null) actors.Add(new HistoryActor(Refs.Of(enemy), HistoryRole.Against));
            var place = town != null ? Refs.Of(town) : Diplomacy.Colony(home);
            if (t.kind == Tie.Kind.Fealty)
            {
                FactionMemory.Note(f, "levies", town?.Name, CallsFromHomeMath.LeviesWeight);
                Diplomacy.Write(DiplomacyMath.Levy, actors, place, history, true);
            }
            else
            {
                FactionMemory.Note(f, "keptWord", deed, CallsFromHomeMath.KeptWeight);
                Diplomacy.Write(DiplomacyMath.Arms, actors, place, history, true);
            }
            CallsToArms.Close(t, pact, Tie.State.Kept);
        }
    }
}
