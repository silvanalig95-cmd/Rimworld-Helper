using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A line of a card: words that may carry links (LinkText reads them), in a colour and size, with an optional icon.</summary>
    public sealed class CardLine
    {
        public string text;
        public Color color = Color.white;
        public GameFont font = GameFont.Small;
        public Texture2D icon;
        public Color iconColor = Color.white;
        public string tip;

        public CardLine(string text, Color? color = null, GameFont font = GameFont.Small, Texture2D icon = null, Color? iconColor = null)
        {
            this.text = text;
            this.color = color ?? Color.white;
            this.font = font;
            this.icon = icon;
            this.iconColor = iconColor ?? this.color;
        }
    }

    /// <summary>Someone or something in a card's list: its picture, its name (a link), what it is, a quieter second line, an act beside it.</summary>
    public sealed class CardRow
    {
        public ThingRef thing;
        public Pawn pawn;
        public Faction faction;
        public Texture2D icon;
        public Color ring = UiKit.Neutral;
        /// <summary>The first line, with links.</summary>
        public string name;
        public string second;
        public Color secondColor = UiKit.Faint;
        public CardAct act;
    }

    /// <summary>A short tag on a card's head: a state (a dot and a word) or a thing (a link with its picture).</summary>
    public sealed class CardChip
    {
        public string text;
        public Color color = UiKit.Faint;
        /// <summary>A thing chip leads to this; none for a state chip.</summary>
        public ThingRef thing;
        public Faction faction;
        public Texture2D icon;
        public string tip;
    }

    /// <summary>What the colony can do from a card: its label, why it can't be now (null when it can), and what it does.</summary>
    public sealed class CardAct
    {
        public string label;
        public string why;
        public string tip;
        public Action run;
        public Texture2D icon;
    }

    /// <summary>One section a card's builder fills: its slot and place in it, its heading with a count, and its lines, rows or steps.</summary>
    public sealed class CardBlock
    {
        public CardSlot slot;
        public float order;
        /// <summary>Its fold key, kept per kind of card for the session.</summary>
        public string id;
        /// <summary>Its heading, already in words; none draws the lines without one.</summary>
        public string heading;
        public int count = -1;
        public string right;
        public readonly List<CardLine> lines = new List<CardLine>();
        public readonly List<CardRow> rows = new List<CardRow>();
        /// <summary>Something under way in steps (written, on the road, arrived, answered): the words, and how far it is (-1 none).</summary>
        public string[] steps;
        public int step = -1;
        /// <summary>A link under the section ("Its whole history (14)") and what it opens.</summary>
        public string more;
        public Action moreRun;

        public bool Empty => lines.Count == 0 && rows.Count == 0 && steps == null && more == null;

        public CardBlock(CardSlot slot, float order, string id, string heading = null)
        {
            this.slot = slot;
            this.order = order;
            this.id = id;
            this.heading = heading;
        }

        public CardBlock Line(string text, Color? color = null, GameFont font = GameFont.Small, Texture2D icon = null)
        {
            if (!text.NullOrEmpty()) lines.Add(new CardLine(text, color, font, icon));
            return this;
        }
    }

    /// <summary>
    /// A card's model: what its builder found about the thing once a second while it shows (UI/Cards/Cards.cs). The
    /// head (picture, name, what it is, chips), the sections by slot, the acts at its foot, and two or three lines that
    /// matter most now for its preview. Nothing in it is drawn or measured: CardView draws it.
    /// </summary>
    public sealed class CardModel
    {
        public ThingRef thing;
        public CardKind kind;
        public float builtAt;

        public string title;
        /// <summary>What it is in one line, with links ("a town of the Ashfall Clans").</summary>
        public string subtitle;
        public Pawn pawn;
        public Faction faction;
        public Texture2D icon;
        public Color iconColor = Color.white;
        public Color ring = UiKit.Neutral;
        /// <summary>No longer in the world: the card shows what was kept of it.</summary>
        public bool gone;

        /// <summary>For a place: its world object and tile (the world view button), and where its card opens in full.</summary>
        public WorldObject place;
        public PlanetTile tile = PlanetTile.Invalid;

        public readonly List<CardChip> chips = new List<CardChip>();
        public readonly List<CardBlock> blocks = new List<CardBlock>();
        public readonly List<CardAct> acts = new List<CardAct>();
        /// <summary>The preview's lines: the two or three that matter most now.</summary>
        public readonly List<string> glance = new List<string>();

        public CardBlock Block(CardSlot slot, float order, string id, string heading = null)
        {
            var b = new CardBlock(slot, order, id, heading);
            blocks.Add(b);
            return b;
        }

        public void State(string text, Color color, string tip = null)
        {
            if (!text.NullOrEmpty()) chips.Add(new CardChip { text = text, color = color, tip = tip });
        }

        public void Chip(ThingRef r, string text = null, Faction f = null, Texture2D icon = null)
        {
            if (r.IsNone) return;
            chips.Add(new CardChip { thing = r, text = text ?? ThingRefs.Name(r), faction = f, icon = icon, color = UiKit.Link });
        }

        public CardAct Act(string label, string why, Action run, string tip = null)
        {
            var a = new CardAct { label = label, why = why, run = run, tip = tip };
            acts.Add(a);
            return a;
        }

        public void Glance(string line)
        {
            if (!line.NullOrEmpty() && glance.Count < 3) glance.Add(line);
        }
    }
}
