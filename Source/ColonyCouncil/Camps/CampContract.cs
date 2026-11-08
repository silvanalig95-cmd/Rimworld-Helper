using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // A camp hired by contract (CampKindDef.work "contract": the Ashen Banner's squad, Peoples/PeoplesE2.cs): one of them on
    // watch at the colony's gate by day, the wage asked by letter every five days of the contract, and what comes of it
    // left unpaid.
    public partial class Camp
    {
        /// <summary>The marks in asksMade of a wage of the contract asked and paid ("wagePaid1"), and of the contract kept to its end.</summary>
        public const string WagePaid = "wagePaid", WageAsked = "wageAsked", ContractKept = "contractKept";

        /// <summary>The ask's price that is the contract's wage (CampAsk.price): paid as it is answered, carried out to their fire.</summary>
        public const string ContractWagePrice = "wage";

        /// <summary>The wage for the squad as it stands now ({COUNT} in the wage's letter).</summary>
        public int ContractWage() => BannerSquad.WageFor(this);

        /// <summary>
        /// The contract's hour: on the fifth and the tenth day the wage is asked by letter (their ask "wage"); paid, they
        /// stay; turned down or let go unanswered, they strike camp and walk, and their people remember it. At the season's
        /// end the contract is kept, and their people think well of the colony for it.
        /// </summary>
        private void ContractHourly(int hour)
        {
            if (map == null || Hostile) return;
            float days = DaysHere;
            if (days >= PeoplesEMath.HireDays - 0.5f)
            {
                if (asksMade.Contains(ContractKept)) return;
                asksMade.Add(ContractKept);
                BannerSquad.Kept(this);
                return;
            }
            int k = PeoplesEMath.WageNumber(days);
            if (k <= 0 || !PeoplesEMath.WageDue(days, 0) || asksMade.Contains(WagePaid + k)) return;
            bool asked = asksMade.Contains(WageAsked + k);
            if (LetterOpen("wage"))
            {
                if (!asked) asksMade.Add(WageAsked + k);
                return;
            }
            if (!asked)
            {
                var ask = AskByKey("wage");
                if (ask != null && !LetterOpen() && Request(ask)) asksMade.Add(WageAsked + k);
                return;
            }
            BannerSquad.Unpaid(this);
            Leave("unpaid");
        }

        /// <summary>By day one of the squad stands the watch at the colony's gate a while, by turns (one at a time).</summary>
        private Job ContractJob(Pawn p)
        {
            var def = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_CampStandWatch");
            if (def == null || Hostile || !Rand.Chance(0.3f) || Here.Any(o => o != p && o.CurJobDef == def)) return null;
            var gate = PeoplesD.GateSpot(map);
            if (!gate.HasValue) return null;
            if (!CellFinder.TryFindRandomCellNear(gate.Value, map, 4, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null
                                                                         && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return null;
            SayMoment(p, "watch", 0.25f);
            return JobMaker.MakeJob(def, cell);
        }

        /// <summary>The wage can be asked only while one is due and unpaid, and paid only with the silver for it in the stores.</summary>
        private string ContractWageBlocked()
        {
            int k = PeoplesEMath.WageNumber(DaysHere);
            if (k <= 0 || !PeoplesEMath.WageDue(DaysHere, 0)) return "CC_PeoplesE_WageNotDue".Translate().ToString();
            if (asksMade.Contains(WagePaid + k)) return "CC_CampAsk_Done".Translate().ToString();
            int wage = ContractWage();
            return CampAnswers.Silver(map) < wage ? "CC_Dilemma_NotEnough".Translate(wage, ThingDefOf.Silver.label).ToString() : null;
        }
    }
}
