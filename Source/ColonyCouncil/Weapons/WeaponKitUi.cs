using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's kit in words and at a glance (Weapons/WeaponKitMath.cs): each slot as a chip (ranged, melee, off hand:
    /// what fills it and its rating for them, missing, or not for them and why), and what was looked at for an empty slot
    /// with why each was left out (the quiet line's tooltip). Made only when the view is made again (once a real second) or
    /// while the mouse is over it.
    /// </summary>
    public static class WeaponKitWords
    {
        /// <summary>One slot of a kit, ready to draw: its words, its weapon and rating when filled, its colour's state, its tooltip.</summary>
        public sealed class Chip
        {
            public KitSlot slot;
            public KitState state;
            public string label, text, tip;
            public Thing thing;
            public float rating;
            /// <summary>An empty slot a suggestion fills.</summary>
            public bool suggested;
        }

        private static string F1(float v) => WeaponForMath.RatingText(v);

        /// <summary>A colonist's kit as chips: ranged, melee, and the off hand when Dual Wield is loaded.</summary>
        public static List<Chip> Chips(WeaponPick.Picks picks, Pawn p)
        {
            var list = new List<Chip>();
            var kit = picks?.Kit(p);
            var c = picks?.input.Colonist(WeaponPick.Key(p));
            if (kit == null || c == null) return list;
            foreach (var v in kit)
            {
                if (v.Slot == KitSlot.OffHand && !picks.input.Rules.DualWield) continue;
                var chip = new Chip { slot = v.Slot, state = v.State, label = ("CC_WeaponKit_Slot_" + v.Slot).Translate().Resolve() };
                switch (v.State)
                {
                    case KitState.Filled:
                        chip.thing = picks.Thing(v.Weapon);
                        var w = picks.input.Weapon(v.Weapon);
                        chip.rating = picks.input.Option(c, w)?.Rating ?? w?.Overall ?? 0f;
                        chip.text = chip.thing?.LabelNoCount ?? w?.Label ?? "?";
                        chip.tip = "CC_WeaponKit_Chip_Filled".Translate(chip.label.Named("SLOT"), chip.text.Named("WEAPON"), F1(chip.rating).Named("RATING"), p.Named("PAWN")).Resolve();
                        break;
                    case KitState.Missing:
                        var fill = picks.list.FirstOrDefault(s => s.Colonist == c.Key && WeaponKitMath.Fills(picks.input, s, v.Slot));
                        chip.suggested = fill != null;
                        chip.text = "CC_WeaponKit_Chip_Missing".Translate().Resolve();
                        chip.tip = WeaponPick.GapWords(v.Slot) + ". " + (fill != null
                            ? "CC_WeaponKit_Chip_Suggested".Translate(WeaponPick.FillWords(picks, fill).Named("FILL")).Resolve()
                            : "CC_WeaponKit_Chip_NothingFills".Translate(p.Named("PAWN")).Resolve());
                        break;
                    default:
                        chip.text = "-";
                        chip.tip = chip.label + ": " + BarWords(v.Bar, p);
                        break;
                }
                list.Add(chip);
            }
            return list;
        }

        /// <summary>Why a slot isn't for them, in words.</summary>
        public static string BarWords(KitBar bar, Pawn p) => ("CC_WeaponKit_Bar_" + bar).Translate(p.Named("PAWN")).Resolve();

        /// <summary>
        /// What was looked at for each empty slot nothing fills and why each was left out: the weapons of its kind in storage
        /// (forbidden ones too), in their own pack, other colonists' spares; a forbidden one they may ask for named as such.
        /// </summary>
        public static string LookedTip(WeaponPick.Picks picks, Pawn p, List<KitSlot> slots)
        {
            var c = picks?.input.Colonist(WeaponPick.Key(p));
            if (c == null || slots == null) return "";
            var sb = new StringBuilder();
            foreach (var slot in slots)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(("CC_WeaponKit_Looked_" + slot).Translate(p.Named("PAWN")).Resolve());
                var looks = WeaponKitMath.Looked(picks.input, c, slot, picks.list);
                if (looks.Count == 0) sb.Append("\n").Append("CC_WeaponKit_LookedNone".Translate(p.Named("PAWN")).Resolve());
                foreach (var l in looks)
                    sb.Append("\n- ").Append(WeaponPick.Label(picks, l.Weapon)).Append(" (").Append(Where(picks, l, p)).Append("): ").Append(LeftOut(picks, l, p));
                var ask = picks.kitAsks.FirstOrDefault(s => s.Colonist == c.Key && WeaponKitMath.Fills(s, slot));
                if (ask != null) sb.Append("\n").Append("CC_WeaponKit_MayAsk".Translate(p.Named("PAWN"), WeaponPick.Label(picks, ask.Weapon).Named("WEAPON")).Resolve());
            }
            return sb.ToString();
        }

        private static string Where(WeaponPick.Picks picks, KitLook l, Pawn p)
        {
            if (l.Own) return "CC_WeaponKit_InPack".Translate(p.Named("PAWN")).Resolve();
            if (l.Owner != null) return "CC_WeaponKit_SpareOf".Translate(picks.Pawn(l.Owner).Named("OTHER")).Resolve();
            string place = picks.Place(l.Weapon);
            return place.NullOrEmpty() ? "CC_WeaponPick_InStockpile".Translate().Resolve() : place;
        }

        /// <summary>Why a weapon was left out of an empty slot, in words: forbidden, past Simple Sidearms' weight, too weak for them, given to another...</summary>
        public static string LeftOut(WeaponPick.Picks picks, KitLook l, Pawn p)
        {
            string value = l.Why == KitLeftOut.TooHeavy ? l.Value.ToString("0.0") : WeaponPickMath.GainText(l.Value);
            if (l.Why == KitLeftOut.TooWeak || l.Why == KitLeftOut.NoBetterThanBash) value = F1(l.Value);
            string limit = l.Why == KitLeftOut.TooHeavy ? l.Limit.ToString("0.0") : F1(l.Limit);
            var other = l.Why == KitLeftOut.Given ? picks.Pawn(l.Text) : null;
            return ("CC_WeaponKit_Left_" + l.Why).Translate(p.Named("PAWN"), value.Named("VALUE"), limit.Named("LIMIT"), (l.Text ?? "").Named("WHY"),
                (other?.LabelShort ?? "?").Named("OTHER")).Resolve();
        }

        /// <summary>
        /// The kit's chips in a row (the Defenses page): each slot's name, its weapon's picture and rating for them, "missing"
        /// in the warning colour (good when a suggestion fills it), or a dash when it isn't for them; the tooltip says which.
        /// </summary>
        public static void DrawChips(Rect r, List<Chip> chips)
        {
            if (chips == null || chips.Count == 0) return;
            float w = (r.width - 6f * (chips.Count - 1)) / chips.Count;
            for (int i = 0; i < chips.Count; i++)
            {
                var ch = chips[i];
                var cr = new Rect(r.x + i * (w + 6f), r.y, w, r.height);
                if (Event.current.type == EventType.Repaint)
                    Widgets.DrawBoxSolid(cr, UiKit.WithAlpha(ch.state == KitState.Missing ? (ch.suggested ? UiKit.Good : UiKit.Warn) : UiKit.Faint, 0.08f));
                float x = cr.x + 4f;
                float lw = Mathf.Min(58f, UiKit.KeptWidth(ch.label, GameFont.Tiny) + 4f);
                UiKit.LabelFit(new Rect(x, cr.y, lw, cr.height), ch.label, UiKit.Faint, GameFont.Tiny);
                x += lw + 2f;
                if (ch.state == KitState.Filled)
                {
                    if (ch.thing != null)
                    {
                        UiKit.ItemPortrait(new Rect(x, cr.y + 1f, cr.height - 2f, cr.height - 2f), ch.thing);
                        x += cr.height + 1f;
                    }
                    WeaponUi.Badge(new Rect(x, cr.center.y - 8f, 30f, 16f), ch.rating);
                    x += 33f;
                    UiKit.LabelFit(new Rect(x, cr.y, cr.xMax - x - 2f, cr.height), ch.text, UiKit.Ink, GameFont.Tiny);
                }
                else UiKit.LabelFit(new Rect(x, cr.y, cr.xMax - x - 2f, cr.height), ch.text, ch.state == KitState.Missing ? (ch.suggested ? UiKit.Good : UiKit.Warn) : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(cr, ch.tip);
            }
        }
    }
}
