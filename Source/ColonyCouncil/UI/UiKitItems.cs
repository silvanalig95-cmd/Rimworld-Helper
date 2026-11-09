using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Things drawn as themselves: an item's own picture in a framed tile (the weapon card's header, the weapons it lists, the
    /// suggestions; the apparel card to come), framed in its quality's colour.
    /// </summary>
    public static partial class UiKit
    {
        /// <summary>A quality's colour in Rimfolk's look (a token: ItemPortraitMath, the game has none).</summary>
        public static Color QualityColour(QualityCategory q)
        {
            int i = (int)q;
            return i >= 0 && i < qualityColours.Length ? qualityColours[i] : EdgeStrong;
        }

        /// <summary>The quality a picture shows: the thing's own; a kind that takes quality, normal (as its card rates it); else none.</summary>
        public static QualityCategory? QualityOf(Thing thing, ThingDef def)
        {
            if (thing != null) return thing.TryGetQuality(out var q) ? q : (QualityCategory?)null;
            return def != null && def.HasComp(typeof(CompQuality)) ? QualityCategory.Normal : (QualityCategory?)null;
        }

        private static readonly HashSet<ThingDef> pictureFailed = new HashSet<ThingDef>();

        /// <summary>
        /// An item's own picture in a framed tile. A thing is drawn as it looks (Widgets.ThingIcon: its stuff's colour, its
        /// style or its faith's look, a unique or persona weapon's own graphic, the very variant a random graphic picked for
        /// it, whatever graphic a mod gives it); a kind in a stuff as made (Widgets.DefIcon, its usual stuff when none is
        /// given). The tile is dark, framed in the quality's colour (a large tile twice), with a faint wash of it; a large
        /// tile with <paramref name="qualityStrip"/> names its quality on a strip at its foot. Its tooltip gives the label
        /// (or <paramref name="tip"/>). A picture a mod's graphic can't give is left out quietly, the frame still drawn.
        /// </summary>
        public static void ItemPortrait(Rect r, Thing thing, ThingDef def = null, ThingDef stuff = null, bool qualityStrip = false, Color? ground = null,
            Func<string> tip = null)
        {
            def ??= thing?.def;
            if (def == null) return;
            var q = QualityOf(thing, def);
            var edge = q.HasValue ? QualityColour(q.Value) : EdgeStrong;
            float side = Mathf.Min(r.width, r.height);
            bool large = side >= ItemPortraitMath.LargeTile;
            int radius = large ? SkinMath.RadiusCard : SkinMath.RadiusSmall;
            if (Event.current.type == EventType.Repaint)
            {
                Round(r, ground ?? Ground, radius);
                if (q.HasValue) Round(r, WithAlpha(edge, 0.07f), radius);
                RoundEdge(r, WithAlpha(edge, q.HasValue ? 0.9f : 0.6f), radius);
                if (large && q.HasValue) RoundEdge(r.ContractedBy(1f), WithAlpha(edge, 0.45f), radius);
            }
            bool strip = qualityStrip && large && q.HasValue;
            var pic = r.ContractedBy(ItemPortraitMath.Pad(side));
            if (strip) pic.yMax = r.yMax - ItemPortraitMath.StripH - 1f;
            if (!pictureFailed.Contains(def))
            {
                try
                {
                    if (thing != null) Widgets.ThingIcon(pic, thing);
                    else Widgets.DefIcon(pic, def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
                }
                catch (Exception e)
                {
                    pictureFailed.Add(def);
                    Log.Warning("[Rimfolk] Couldn't draw the picture of " + def.defName + ", so its tile stays empty: " + e.Message);
                }
            }
            GUI.color = Color.white;
            if (strip)
            {
                var s = new Rect(r.x + 2f, r.yMax - ItemPortraitMath.StripH - 2f, r.width - 4f, ItemPortraitMath.StripH);
                if (Event.current.type == EventType.Repaint) Round(s, WithAlpha(Shade, 0.6f), SkinMath.RadiusSmall);
                LabelFit(s, q.Value.GetLabel().CapitalizeFirst(), edge, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
            var t = thing;
            var d = def;
            var st = stuff;
            TooltipHandler.TipRegion(r, tip ?? (() => t != null ? t.LabelCapNoCount : GenLabel.ThingLabel(d, st ?? (d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null)).CapitalizeFirst()),
                (thing?.thingIDNumber ?? def.shortHash) ^ 0x1A7E);
        }
    }
}
