using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods in words (Languages/English/Keyed/Rimfolk_Gods.xml): favour, ties, natures, kinds and forms as the page and
    /// the news say them, the colours of favour, and the gods named in a letter ({GOD}, {GODEPITHET}, {GODOF}).
    /// </summary>
    public static class GodWords
    {
        /// <summary>A favour word: "pleased", "wrathful".</summary>
        public static string Word(FavourWord w) => ("CC_God_Word_" + w).Translate();

        /// <summary>The colour of a favour word: warm for the good, red for the bad, plain for unknown.</summary>
        public static Color WordColor(FavourWord w) => w switch
        {
            FavourWord.Wrathful => UiKit.Bad,
            FavourWord.Displeased => UiKit.Sour,
            FavourWord.Unknown => UiKit.Faint,
            FavourWord.Noticed => UiKit.Ink,
            FavourWord.Pleased => UiKit.Good,
            FavourWord.Favoured => UiKit.Good,
            _ => UiKit.Gold
        };

        /// <summary>What a tie is: "rivals", "lovers", "parent and child".</summary>
        public static string TieWord(GodTieKind k) => ("CC_God_Tie_" + k).Translate();

        /// <summary>A tie from one god's side, with the other's name: "rival of Thoran", "child of Ama", "cast down by Vargan".</summary>
        public static string TieFrom(God me, GodTie t, God other)
        {
            if (me == null || t == null || other == null) return "";
            bool first = t.a == me.id;
            string key = "CC_God_TieOf_" + t.kind + (Directed(t.kind) ? first ? "_First" : "_Second" : "");
            return key.Translate(other.name.Named("OTHER")).Resolve();
        }

        /// <summary>A tie that reads differently from each side (parent and child, the one who cast down and the one cast down).</summary>
        public static bool Directed(GodTieKind k) => k == GodTieKind.Parent || k == GodTieKind.CastDown || k == GodTieKind.StolePeople;

        /// <summary>The colour of a tie on the web: red for rivals and wrongs, pink for lovers, grey-pink for the estranged, gold for kin, green for allies.</summary>
        public static Color TieColor(GodTieKind k) => k switch
        {
            GodTieKind.Rivals => UiKit.Bad,
            GodTieKind.Lovers => new Color(0.95f, 0.55f, 0.7f),
            GodTieKind.Estranged => new Color(0.65f, 0.5f, 0.6f),
            GodTieKind.CastDown => new Color(0.6f, 0.2f, 0.2f),
            GodTieKind.StolePeople => new Color(0.6f, 0.2f, 0.2f),
            GodTieKind.Allies => UiKit.Good,
            _ => UiKit.Gold
        };

        public static string NatureWord(GodNature n) => ("CC_God_Nature_" + n).Translate();

        /// <summary>A nature in a sentence: how its favour moves, what offends it, how loudly it answers.</summary>
        public static string NatureLine(GodNature n) => ("CC_God_NatureLine_" + n).Translate();

        public static string KindWord(GodKind k) => ("CC_God_Kind_" + k).Translate();

        /// <summary>The kind in a sentence, for one that isn't ordinary.</summary>
        public static string KindLine(GodKind k) => ("CC_God_KindLine_" + k).Translate();

        public static string FormWord(GodForm f) => ("CC_God_Form_" + f).Translate();

        /// <summary>Its domains in words: "the harvest", "the harvest and storms".</summary>
        public static string Domains(God g)
        {
            var a = GodDomainDef.Of(g?.domain ?? GodDomain.None);
            if (a == null) return "";
            var b = g.second != GodDomain.None ? GodDomainDef.Of(g.second) : null;
            return b == null ? a.ofWords : "CC_God_TwoDomains".Translate(a.ofWords.Named("A"), b.ofWords.Named("B")).Resolve();
        }

        /// <summary>How its people tell of it: a patron's lore, or the old ones' line by their kind.</summary>
        public static string Lore(God g)
        {
            if (g == null) return null;
            var pd = g.PatronDef;
            if (pd != null && !pd.lore.NullOrEmpty()) return pd.lore;
            if (g.source == "old") return ("CC_God_OldLore_" + g.baseKind).Translate(g.name.Named("GOD")).Resolve();
            if (g.source == "newborn") return "CC_God_NewbornLore".Translate(g.name.Named("GOD")).Resolve();
            return null;
        }

        /// <summary>
        /// A letter's god (Letters/LetterFormShapes.cs): the god its writer would swear by (else the first of their people's),
        /// filled into {GOD}, {GODEPITHET} and {GODOF}; a letter of another people's that names one teaches the colony of it.
        /// </summary>
        public static void Letter(Dictionary<string, string> values, Pawn writer, Faction f)
        {
            if (values == null || !Gods.On) return;
            try
            {
                var g = Gods.SwearBy(writer) ?? (f != null ? Gods.Of(f).FirstOrDefault(x => x.kind != GodKind.Dead) : null);
                if (g == null) return;
                Gods.Fill(values, g);
                if (f != null && !f.IsPlayer) Gods.Learn(g, "letter");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a letter's god", e);
            }
        }
    }
}
