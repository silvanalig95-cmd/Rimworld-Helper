using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// House renown in the windows (docs/design/interface.md, "Where stage 2 and stage 3 go": House cards, People → Houses;
    /// no acts, it is earned; nothing in This week): the House card of one of the colony's houses (its people, the gone
    /// among them, its seat), and on every House card its renown (how far its name carries and for what, the deeds it came
    /// from and what is held against it, who knows it, what it does), its ties (marriages, wards, feuds); a line on a
    /// colonist's card naming their house; and the words People → Houses and Here and away show for a house. Its story is
    /// the history's lines with the house in them (dip:renown).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RenownPanels
    {
        static RenownPanels()
        {
            CardSections.RegisterLines(CardKind.House, CardSlot.Now, 4f, HouseLines, "CC_Renown_Card_Head", "renown");
            CardSections.RegisterLines(CardKind.House, CardSlot.Ties, 6f, TieLines, "CC_Renown_Card_Ties", "renown-ties");
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Who, 30f, ColonistLines, null, "renown-colonist");
        }

        private static string P(string key, params NamedArgument[] args) => RenownWords.P(key, args);

        // ================= One of the colony's houses =================

        /// <summary>The card's head and people for one of the colony's houses (Cards.House asks): a house of the colony, its people as rows, the gone by name.</summary>
        public static void OurHouse(CardModel m, LedgerHouse h)
        {
            m.title = RenownWords.Name(h).CapitalizeFirst();
            m.icon = Icons.People;
            m.iconColor = UiKit.Gold;
            var home = Homes.MapOf(h.seat) ?? Homes.Seat;
            var colony = home != null ? ThingRefs.Of(home.Parent as RimWorld.Planet.Settlement) : ThingRef.None;
            string where = !colony.IsNone && ThingRefs.Exists(colony) ? ThingRefs.Link(colony) : Homes.Name(home) ?? Faction.OfPlayer.Name;
            m.subtitle = P("CC_Renown_Card_HouseOf", where.Named("COLONY"));
            var who = m.Block(CardSlot.Who, 10f, "members", "CC_Card_Head_Members".Translate().Resolve());
            var living = Renown.Living(h);
            for (int i = 0; i < living.Count; i++)
            {
                var p = living[i];
                var r = ThingRefs.Of(p);
                who.rows.Add(new CardRow
                {
                    thing = r, pawn = p, name = ThingRefs.Link(p.LabelShort, r),
                    second = i == 0 ? P("CC_Renown_Card_Eldest") : p.ageTracker != null ? P("CC_Renown_Card_Age", p.ageTracker.AgeBiologicalYears.Named("AGE")) : null
                });
            }
            // The gone: neither of it nor of another of the colony's houses now (one who took another's name on marrying is still here).
            var gone = h.folk.Where(r => !Renown.StillOurs(r)).Select(DipWords.NameOf).Where(n => !n.NullOrEmpty() && n != "?").Distinct().ToList();
            if (gone.Count > 0) who.Line(P("CC_Renown_Card_Gone", gone.ToCommaList(true).Named("NAMES")), UiKit.Faint);
            who.count = living.Count;
            if (who.Empty) m.blocks.Remove(who);
            m.Glance(LinkTextMath.Plain(m.subtitle).CapitalizeFirst() + ".");
            if (living.Count > 0) m.Glance(P("CC_Renown_Card_PeopleLine", living.Select(p => p.LabelShort).ToCommaList(true).Named("NAMES")));
        }

        // ================= Every house =================

        private static List<string> HouseLines(ThingRef t)
        {
            var lines = new List<string>();
            var h = ThingRefs.House(t);
            if (h == null) return lines;
            if (!Renown.On)
            {
                lines.Add(P("CC_Renown_Card_Off"));
                return lines;
            }
            var r = Renown.Read(h);
            if (r == null) return lines;
            lines.Add(RenownWords.Standing(h, r, Ages.Ours));
            string good = RenownWords.Parts(r, true), bad = RenownWords.Parts(r, false, 3);
            if (good != null) lines.Add(P("CC_Renown_Card_From", good.Named("PARTS")));
            if (bad != null) lines.Add(P("CC_Renown_Card_Against", bad.Named("PARTS")));
            if (h.ours)
            {
                string known = RenownWords.KnownTo(Renown.Living(h).SelectMany(Renown.KnownTo));
                if (known != null) lines.Add(P("CC_Renown_Card_KnownTo", known.Named("NOTABLES")));
                if (r.score > 0f && r.band >= RenownBand.Town)
                    lines.Add(P("CC_Renown_Card_Does", ((int)Math.Round(RenownMath.RegardBonus(r.score))).Named("REGARD")));
                else if (r.band == RenownBand.Infamous)
                    lines.Add(P("CC_Renown_Card_DoesBad", ((int)Math.Round(-RenownMath.RegardBonus(r.score))).Named("REGARD")));
            }
            return lines;
        }

        /// <summary>Its marriages, the wards it fostered or sent, its feuds: each tie a link to its card.</summary>
        private static List<string> TieLines(ThingRef t)
        {
            var lines = new List<string>();
            var h = ThingRefs.House(t);
            if (h == null) return lines;
            var folk = h.ours ? new HashSet<Ref>(h.folk) : new HashSet<Ref> { h.head };
            folk.Remove(Ref.None);
            if (folk.Count == 0) return lines;
            foreach (var k in new[] { Tie.Kind.Marriage, Tie.Kind.Ward })
                foreach (var tie in WorldLedger.OwnTies(k))
                    if (tie != null && tie.Live && (folk.Contains(tie.from) || folk.Contains(tie.to) || tie.terms != null && folk.Contains(tie.terms.about)))
                        lines.Add(ThingRefs.Link(CardWords.TieName(tie).CapitalizeFirst(), ThingRefs.Of(tie)));
            foreach (var g in Renown.Feuds(h))
                lines.Add(ThingRefs.Link(P("CC_Renown_Card_Feud", DipWords.NameOf(g.from).Named("HOLDER"), DipWords.NameOf(g.terms?.about ?? Ref.None).Named("ABOUT")), ThingRefs.Of(g)));
            return lines;
        }

        /// <summary>A colonist's house, a link to its card, and how far its name carries.</summary>
        private static List<string> ColonistLines(ThingRef t)
        {
            var lines = new List<string>();
            var p = ThingRefs.Pawn(t);
            if (p == null || !Renown.On) return lines;
            var h = Renown.HouseOf(p);
            var r = Renown.Read(h);
            if (h == null || r == null) return lines;
            lines.Add(P("CC_Renown_Colonist", ThingRefs.Link(RenownWords.Name(h), ThingRefs.Of(h.house)).Named("HOUSE"), RenownWords.Standing(h, r, Ages.Ours).Named("STANDING")));
            return lines;
        }

        // ================= People → Houses, Here and away =================

        /// <summary>A house's renown in a line for the lists (People → Houses, Here and away), or null when it is off or none is read.</summary>
        public static string ListLine(LedgerHouse h)
        {
            if (h == null || !Renown.On) return null;
            try
            {
                return RenownWords.Standing(h, Renown.Read(h), Ages.Ours);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a house's line", e);
                return null;
            }
        }

        /// <summary>The colony's house of a family named so (People → Houses' families), or null.</summary>
        public static LedgerHouse OursNamed(string last) => WorldLedger.OurHouse(RenownMath.Key(last));
    }
}
