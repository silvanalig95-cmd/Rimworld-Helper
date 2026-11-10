using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of diplomacy (Languages/English/Keyed/Rimfolk_Diplomacy.xml): a debt's words by both sides' ages (the
    /// colony's word for it by its own age, their rite by theirs: Ages.Form, the feature Favor), what it was for
    /// ({FAVOURWHY}), a people's stance, the colony's means of calling (a runner or a rider, the radio, comms). Lines that
    /// are told often have numbered fellows (CC_X_2...), one picked at random (TextVariants).
    /// </summary>
    public static class DipWords
    {
        /// <summary>A keyed line, one of its fellows at random when it has some.</summary>
        public static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        /// <summary>A keyed line as it is (for the pages: the same words each time they are drawn).</summary>
        public static string P(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        public static string People(Faction f) => f?.Name ?? "CC_Dip_Someone".Translate().Resolve();

        /// <summary>The debt's word by the colony's age (a debt of honor, an IOU) and rite by theirs (a gift owed back, credit owed).</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Favor, home ?? Homes.Seat, f);

        /// <summary>
        /// What a debt was for, as a short phrase that reads in either side's mouth: "a prisoner freed", "relief sent".
        /// {FAVOURWHY} in the talk and the letters.
        /// </summary>
        public static string Why(string deedKind)
        {
            if (deedKind.NullOrEmpty()) return "CC_Dip_For_Any".Translate().Resolve();
            string key = "CC_Dip_For_" + deedKind;
            return key.CanTranslate() ? key.Translate().Resolve() : "CC_Dip_For_Any".Translate().Resolve();
        }

        public static string Why(Tie t) => Why(t?.terms?.key);

        /// <summary>A people's stance toward the colony in words, and its colour.</summary>
        public static string Stance(Faction f)
        {
            if (f == null) return "";
            bool hostile = Diplomacy.Hostile(f);
            int gw = f.HasGoodwill ? f.PlayerGoodwill : 0;
            bool ally = !hostile && f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally;
            return ("CC_Dip_Stance_" + DiplomacyMath.StanceKey(gw, hostile, ally)).Translate().Resolve();
        }

        public static UnityEngine.Color StanceColor(Faction f)
        {
            if (f == null) return UiKit.Faint;
            if (Diplomacy.Hostile(f)) return UiKit.Bad;
            int gw = f.HasGoodwill ? f.PlayerGoodwill : 0;
            return gw >= 25 ? UiKit.Good : gw >= 0 ? UiKit.Info : UiKit.Warn;
        }

        /// <summary>The colony's own way of calling on them, by its age: a runner, a rider, the radio, comms.</summary>
        public static string Means(Map home) => ("CC_Dip_Means_" + Ages.Of(home ?? Homes.Seat)).Translate().Resolve();

        /// <summary>"Ada" for a person of the ledger (the name it keeps for one gone).</summary>
        public static string NameOf(Ref r)
        {
            if (r.IsNone) return "?";
            switch (r.kind)
            {
                case RefKind.People: return People(Refs.Faction(r));
                case RefKind.Colony: return Homes.All.Count > 1 && Refs.Home(r) != null ? Homes.Name(Refs.Home(r)) : "CC_Dip_TheColony".Translate().Resolve();
                case RefKind.Person: return WorldLedger.NameOf(r) ?? "?";
                default: return "?";
            }
        }

        /// <summary>The ask the colony makes, in a few words ("a load of steel", "a word with the Iron Hand").</summary>
        public static string Ask(FavourAsk ask, string arg)
        {
            switch (ask)
            {
                case FavourAsk.Goods: return P("CC_Dip_Ask_Goods", GoodsWord(arg).Named("GOODS"));
                case FavourAsk.Word: return P("CC_Dip_Ask_Word", People(FactionOf(arg)).Named("PEOPLE"));
                case FavourAsk.Captive: return P("CC_Dip_Ask_Captive", (PawnOf(arg)?.LabelShort ?? "?").Named("NAME"));
                case FavourAsk.Fighters: return P("CC_Dip_Ask_Fighters");
                default: return P("CC_Dip_Ask_Peace", People(FactionOf(arg)).Named("PEOPLE"));
            }
        }

        /// <summary>A kind of goods in words: food, medicine, wood, steel, silver.</summary>
        public static string GoodsWord(string kind) => ("CC_Dip_Goods_" + (kind ?? "food")).CanTranslate() ? ("CC_Dip_Goods_" + kind).Translate().Resolve() : kind;

        /// <summary>What they need, in a few words ("medicine", "food", "hands at their harvest").</summary>
        public static string Need(FavourNeed n) => ("CC_Dip_Need_" + n).Translate().Resolve();

        /// <summary>A people by its saved form in an ask ("f12").</summary>
        public static Faction FactionOf(string arg) => Ref.TryParse(arg, out var r) ? Refs.Faction(r) : null;

        public static Pawn PawnOf(string arg) => Ref.TryParse(arg, out var r) ? Refs.Pawn(r) : null;

        /// <summary>"since 12 days ago", "since last year": never a countdown.</summary>
        public static string Since(int tick) => P("CC_Dip_Since", FactionMemory.AgoText(tick).Named("AGO"));

        /// <summary>The debt in a sentence for a page or a panel, from whoever owes to whoever is owed, with its words by both ages.</summary>
        public static string DebtLine(Tie t)
        {
            if (t == null) return "";
            var f = FavoursOwed.PeopleOf(t);
            var home = FavoursOwed.HomeOf(t);
            var form = Form(home, f);
            string key = FavoursOwed.ColonyOwes(t) ? "CC_Dip_Debt_Ours" : FavoursOwed.IsPersonal(t) ? "CC_Dip_Debt_Personal" : "CC_Dip_Debt_Theirs";
            return P(key, NameOf(t.from).Named("FROM"), NameOf(t.to).Named("TO"), Why(t).Named("WHY"), form.word.Named("WORD"), form.rite.Named("RITE"),
                FavourCalls.WeightWords(t).Named("WEIGHT")).CapitalizeFirst();
        }

        /// <summary>Where the debt stands in words: open, called in (and for what), never a countdown.</summary>
        public static string StateLine(Tie t)
        {
            if (t == null) return "";
            if (t.state != Tie.State.CalledIn) return Since(t.since);
            if (FavourMath.ParseNeed(t.terms.goods, out var need)) return P("CC_Dip_State_TheyAsked", Need(need).Named("NEED"));
            if (FavourMath.ParseAsk(t.terms.goods, out var ask, out var arg)) return P("CC_Dip_State_Asked", Ask(ask, arg).Named("ASK"));
            return P("CC_Dip_State_Called");
        }

        public static List<string> Lines(IEnumerable<Tie> ties)
        {
            var list = new List<string>();
            foreach (var t in ties) list.Add(DebtLine(t));
            return list;
        }
    }
}
