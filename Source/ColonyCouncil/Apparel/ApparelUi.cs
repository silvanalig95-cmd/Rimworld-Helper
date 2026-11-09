using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How the apparel rating is said and drawn wherever it shows, on the weapons' own badge and chips (WeaponUi): the
    /// role, sub-ratings, situations and regions in words, the steps of a colonist's rating, the badge's tooltip (the
    /// rating, its line, the tags with why and the colonist's own rating), and the body outline of what a piece covers.
    /// </summary>
    public static class ApparelUi
    {
        public static string RoleWords(string role) => ("CC_Apparel_Role_" + ApparelForMath.KeyWord(role)).Translate();

        public static string SubWords(string key) => ("CC_Apparel_Sub_" + ApparelForMath.KeyWord(key)).Translate();

        public static string SubTip(string key) => ("CC_Apparel_SubExpl_" + ApparelForMath.KeyWord(key)).Translate();

        public static string SitWords(string key) => ("CC_Apparel_Sit_" + ApparelForMath.KeyWord(key)).Translate();

        public static string SitTip(string key) => ("CC_Apparel_SitExpl_" + ApparelForMath.KeyWord(key)).Translate();

        public static string RegionWords(string key) => ("CC_Apparel_Region_" + ApparelForMath.KeyWord(key)).Translate();

        /// <summary>Who a piece is rated for: anyone, a shooter or a melee fighter.</summary>
        public static string ForWords(string ratedFor) =>
            (ratedFor == "a shooter" ? "CC_Apparel_For_Shooter" : ratedFor == "a melee fighter" ? "CC_Apparel_For_Melee" : "CC_Apparel_For_Anyone").Translate();

        /// <summary>A step of a colonist's rating in words.</summary>
        public static string StepWords(string why)
        {
            switch (why)
            {
                case "kind:shooter": return "CC_Apparel_Step_Shooter".Translate();
                case "kind:melee": return "CC_Apparel_Step_Melee".Translate();
                case "kind:none": return "CC_Apparel_Step_None".Translate();
                case "kind:both": return "CC_Apparel_Step_Both".Translate();
                case "body": return "CC_Apparel_Step_Body".Translate();
                case "climate": return "CC_Apparel_Step_Climate".Translate();
                case "worn-already": return "CC_Apparel_Step_WornAlready".Translate();
                case "gender": return "CC_Apparel_Step_Gender".Translate();
                default: return why.CapitalizeFirst();
            }
        }

        /// <summary>The steps that move a colonist's rating, in a line: "as a melee fighter +2.1, where they live -0.4".</summary>
        public static string Steps(ApparelForColonist c)
        {
            var steps = ApparelForMath.Telling(c);
            return steps.Count == 0 ? "CC_Apparel_SameAsUsual".Translate().Resolve()
                : string.Join(", ", steps.Select(s => StepWords(s.why).ToLowerInvariant() + " " + WeaponForMath.Delta(s.delta)));
        }

        /// <summary>The ratings of a shooter and a melee fighter, when they differ by half a point.</summary>
        public static string BothLine(ApparelRating r) =>
            ApparelForMath.BothDiffer(r)
                ? "CC_Apparel_BothLine".Translate(WeaponForMath.RatingText(r.OverallShooter).Named("SHOOTER"), WeaponForMath.RatingText(r.OverallMelee).Named("MELEE")).Resolve()
                : null;

        /// <summary>The badge's tooltip: the rating, band and role, the line, the tags with why, and the colonist's own rating.</summary>
        public static string Tip(ApparelReport r, Pawn p, Thing thing = null, bool clickOpensCard = false)
        {
            var g = r.Rating;
            var sb = new StringBuilder();
            sb.AppendLine("CC_Apparel_BadgeTip".Translate(WeaponForMath.RatingText(g.Overall).Named("RATING"), WeaponWords.Quality(g.Overall).Named("BAND"),
                RoleWords(g.Role).Named("ROLE"), ForWords(g.For).Named("FOR")).Resolve() + (r.Rough ? " (" + "CC_Weapon_Rough".Translate() + ")" : ""));
            string both = BothLine(g);
            if (both != null) sb.AppendLine(both);
            if (!r.Plain.NullOrEmpty()) sb.AppendLine(r.Plain);
            if (r.Tags.Count > 0)
            {
                sb.AppendLine();
                foreach (var t in r.Tags) sb.AppendLine(t.Label.Colorize(WeaponUi.Colour(WeaponTagMath.Style(t.Category).Colour)) + ": " + t.Why);
            }
            if (p != null && p.RaceProps.Humanlike)
            {
                string cant = ApparelLive.CantWear(p, thing, thing?.def);
                sb.AppendLine();
                if (cant != null) sb.AppendLine(cant);
                else
                {
                    var (_, fc) = ApparelLive.ForColonist(p, r, thing);
                    if (fc != null)
                        sb.AppendLine("CC_Apparel_ForShort".Translate(p.Named("PAWN"), WeaponForMath.RatingText(fc.Rating).Named("RATING")).Resolve() +
                                      (ApparelForMath.Telling(fc).Count > 0 ? " (" + Steps(fc) + ")" : ""));
                }
            }
            if (clickOpensCard && ApparelCardWindow.On) sb.Append("CC_Apparel_ClickForCard".Translate());
            return sb.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------- the body outline

        /// <summary>Where each region sits on a figure 92 wide and 150 tall: the head, neck, shoulders, torso, both arms, hands, legs and feet.</summary>
        public static readonly Dictionary<string, Rect[]> Figure = new Dictionary<string, Rect[]>
        {
            ["head"] = new[] { new Rect(35f, 0f, 22f, 22f) },
            ["neck"] = new[] { new Rect(41f, 23f, 10f, 6f) },
            ["shoulders"] = new[] { new Rect(20f, 30f, 12f, 8f), new Rect(60f, 30f, 12f, 8f) },
            ["torso"] = new[] { new Rect(33f, 30f, 26f, 50f) },
            ["arms"] = new[] { new Rect(18f, 39f, 13f, 44f), new Rect(61f, 39f, 13f, 44f) },
            ["hands"] = new[] { new Rect(16f, 85f, 14f, 13f), new Rect(62f, 85f, 14f, 13f) },
            ["legs"] = new[] { new Rect(33f, 82f, 12f, 52f), new Rect(47f, 82f, 12f, 52f) },
            ["feet"] = new[] { new Rect(29f, 136f, 16f, 10f), new Rect(47f, 136f, 16f, 10f) },
        };

        public const float FigureW = 92f, FigureH = 150f;

        /// <summary>
        /// The body as a figure, each region filled by how much of it the piece covers (none quiet, all in the colour of its
        /// armour against guns), its name, share of shots and the share covered on the tooltip.
        /// </summary>
        public static void DrawFigure(Vector2 at, List<ApparelRegion> regions, Color cover)
        {
            foreach (var reg in regions)
            {
                if (!Figure.TryGetValue(reg.Key, out var rects)) continue;
                var fill = reg.Covered <= 0.001f ? UiKit.WithAlpha(UiKit.Faint, 0.18f) : UiKit.WithAlpha(cover, 0.35f + 0.6f * reg.Covered);
                foreach (var r0 in rects)
                {
                    var r = new Rect(at.x + r0.x, at.y + r0.y, r0.width, r0.height);
                    if (Event.current.type == EventType.Repaint)
                    {
                        UiKit.Round(r, fill, SkinMath.RadiusSmall);
                        UiKit.RoundEdge(r, UiKit.WithAlpha(reg.Covered > 0.001f ? cover : UiKit.Faint, 0.6f), SkinMath.RadiusSmall);
                    }
                    if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
                    var k = reg;
                    TooltipHandler.TipRegion(r, () => RegionTip(k), reg.Key.GetHashCode() ^ 0x2BD1);
                }
            }
        }

        public static string RegionTip(ApparelRegion r) =>
            "CC_Apparel_RegionTip".Translate(RegionWords(r.Key).Named("REGION"), r.Shots.ToStringPercent().Named("SHOTS"), r.Covered.ToStringPercent().Named("COVERED")).Resolve();
    }
}
