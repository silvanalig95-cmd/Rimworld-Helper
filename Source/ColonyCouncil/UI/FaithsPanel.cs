using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Faiths (with Ideology and "Faith in the colony" on): each faith in the colony as a card, most believers
    /// first, with its colour and icon, its believers and how firmly each holds it (devout, wavering), its leader and
    /// moral guide or that the role is empty here, what it asks that weighs on someone right now (their negative moods
    /// from its precepts), and who is likely to sway whom (devout, well-liked speakers against wavering, open listeners);
    /// then how to bring people together with the game's own tools. Worked out once a second.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FaithsPanel
    {
        // ---------------- LEAD HOOK: the council's buttons ----------------
        //
        // The council's own ways to bring faiths together (the evening of faith, the one-faith decree) add their buttons
        // here, from their own files, once at startup:
        //
        //     FaithsPanel.Buttons.Add(new FaithsPanel.Button { key = "evening", perFaith = true, label = ..., act = ... });
        //
        // A button with perFaith set is drawn at the foot of each faith's card and is handed that faith; one without is
        // drawn under "How to bring people together" and is handed null. "shows" hides it where it doesn't apply (null:
        // always shown); "blocked" greys it out with the reason as its tooltip (null or empty: it can be pressed). The list
        // starts empty, and the page draws nothing for it until something is added.

        /// <summary>A button another part of Rimfolk puts on the faiths page (see the hook above).</summary>
        public class Button
        {
            /// <summary>Names it, so it can be added once (a second Add with the same key replaces the first).</summary>
            public string key;
            /// <summary>On each faith's card (handed the faith) rather than once under "How to bring people together" (handed null).</summary>
            public bool perFaith;
            public Func<Ideo, bool> shows;
            public Func<Ideo, string> label;
            public Func<Ideo, string> tip;
            /// <summary>Why it can't be pressed now (null or empty: it can).</summary>
            public Func<Ideo, string> blocked;
            public Action<Ideo> act;
        }

        /// <summary>The council's buttons on this page. Empty until the council's faith features add theirs.</summary>
        public static readonly List<Button> Buttons = new List<Button>();

        /// <summary>Adds a button, or replaces the one with the same key.</summary>
        public static void AddButton(Button b)
        {
            if (b == null) return;
            Buttons.RemoveAll(x => x.key != null && x.key == b.key);
            Buttons.Add(b);
        }

        // ---------------- The model ----------------

        private class Believer
        {
            public Pawn p;
            public float certainty;
            public string role;
            public string tip;
        }

        private class AskRow
        {
            public string label;
            public float mood;
            public List<Pawn> who = new List<Pawn>();
            public string text, tip;
        }

        private class SwayRow
        {
            public Pawn speaker, listener;
            public float amount;
            public string why;
            public string text, tip;
        }

        private class Card
        {
            public Ideo ideo;
            public string name;
            public Color color;
            public Texture2D icon;
            public float average;
            public string countText;
            public readonly List<Believer> believers = new List<Believer>();
            public readonly List<UiKit.PillSpec> memes = new List<UiKit.PillSpec>();
            public bool hasLeader, hasGuide;
            public Pawn leader, guide;
            /// <summary>The role holder isn't one of the colony's believers (they're elsewhere in the world).</summary>
            public bool leaderAway, guideAway;
            public readonly List<AskRow> asks = new List<AskRow>();
            public readonly List<SwayRow> sways = new List<SwayRow>();
        }

        private class Model
        {
            public readonly List<Card> cards = new List<Card>();
            public int people;
            public float split;
            public int wavering;
            public string statText, splitTip, togetherText;
        }

        private static Model model;
        private static float modelAt = -1f;
        private static Vector2 scroll;
        private static float height = 600f;

        private const int MaxAskRows = 5, MaxSwayRows = 3;

        private static Model Build()
        {
            var m = new Model();
            // Who holds what right now: the colony's own people, as the core's census counts them, read fresh (the census
            // itself is kept an hour, and the game's own Convert ability can change a faith in between).
            var all = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep
                .Where(p => p != null && !p.Dead && !p.IsQuestLodger() && Faiths.Of(p) != null).ToList();
            var groups = all.GroupBy(Faiths.Of).Select(g => (ideo: g.Key, believers: g.ToList()))
                .OrderByDescending(g => g.believers.Count).ThenByDescending(g => g.believers.Average(Faiths.Certainty)).ToList();
            m.people = all.Count;
            m.split = FaithMath.Split(groups.Count, all.Count);
            // The game's stats, read once per build: each speaker's conversion power, each listener's certainty-loss factor.
            var power = new Dictionary<Pawn, float>();
            var loss = new Dictionary<Pawn, float>();
            foreach (var p in all)
            {
                try
                {
                    power[p] = p.GetStatValue(StatDefOf.ConversionPower);
                    loss[p] = p.GetStatValue(StatDefOf.CertaintyLossFactor);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faiths page: conversion stats", e);
                    power[p] = 1f;
                    loss[p] = 1f;
                }
            }
            foreach (var (ideo, believers) in groups)
            {
                if (ideo == null) continue;
                var card = new Card { ideo = ideo, name = ideo.name.CapitalizeFirst() };
                try
                {
                    card.color = ideo.Color;
                    card.icon = ideo.Icon;
                    if (card.icon == BaseContent.BadTex) card.icon = null;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faiths page: an ideoligion's icon", e);
                    card.color = UiKit.Gold;
                }
                if (ideo.memes != null)
                    foreach (var meme in ideo.memes.Where(x => x != null))
                        card.memes.Add(new UiKit.PillSpec(meme.LabelCap, UiKit.Info, null, meme.description));
                Roles(card, believers);
                var asks = new Dictionary<string, AskRow>();
                foreach (var p in believers.OrderByDescending(Faiths.Certainty))
                {
                    var b = new Believer { p = p, certainty = Faiths.Certainty(p) };
                    var role = FaithCharacter.SafeRole(ideo, p);
                    if (role != null) b.role = role.LabelCap;
                    var mine = FaithAsks.Of(p);
                    foreach (var a in mine)
                    {
                        if (!asks.TryGetValue(a.label, out var row)) asks[a.label] = row = new AskRow { label = a.label, mood = a.mood };
                        row.mood = Mathf.Min(row.mood, a.mood);
                        row.who.Add(p);
                    }
                    b.tip = BelieverTip(p, b, mine);
                    card.believers.Add(b);
                    if (FaithMath.Wavering(b.certainty)) m.wavering++;
                }
                card.average = card.believers.Count > 0 ? card.believers.Average(b => b.certainty) : 0f;
                card.countText = "CC_FaithPg_Count".Translate(card.believers.Count.Named("N"), card.average.ToStringPercent().Named("PCT")).Resolve();
                card.asks.AddRange(asks.Values.OrderBy(a => a.mood * a.who.Count).Take(MaxAskRows));
                string asksTip = "CC_FaithP_AsksTip".Translate();
                foreach (var a in card.asks)
                {
                    a.text = "CC_FaithPg_AskRow".Translate(a.label.Named("WHAT"), a.mood.ToString("0").Named("MOOD"),
                        TextUtil.JoinAnd(a.who.Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve();
                    a.tip = a.text + "\n\n" + asksTip;
                }
                // Who of this faith could sway whom of another: its devout speakers against everyone else, best first.
                foreach (var speaker in card.believers.Where(b => FaithMath.Devout(b.certainty)).Select(b => b.p))
                foreach (var listener in all.Where(o => !Faiths.Same(o, speaker)))
                {
                    float v = FaithPeople.Expected(listener, speaker, FaithPeopleMath.DebateSway,
                        power.TryGetValue(speaker, out float pw) ? pw : (float?)null, loss.TryGetValue(listener, out float lf) ? lf : (float?)null);
                    if (v < FaithPeopleMath.ShowSwayFrom) continue;
                    card.sways.Add(new SwayRow { speaker = speaker, listener = listener, amount = v, why = Why(listener, speaker) });
                }
                card.sways.Sort((a, b) => b.amount.CompareTo(a.amount));
                // One line per listener: the speaker likeliest to reach them.
                var seen = new HashSet<Pawn>();
                card.sways.RemoveAll(s => !seen.Add(s.listener));
                if (card.sways.Count > MaxSwayRows) card.sways.RemoveRange(MaxSwayRows, card.sways.Count - MaxSwayRows);
                string swayTip = "CC_FaithP_SwayTip".Translate();
                foreach (var s in card.sways)
                {
                    string pct = Mathf.Max(1, Mathf.RoundToInt(s.amount * 100f)).ToString();
                    s.text = (s.why.NullOrEmpty() ? "CC_FaithPg_SwayRowPlain" : "CC_FaithPg_SwayRow")
                        .Translate(s.speaker.Named("PAWN"), s.listener.Named("OTHER"), pct.Named("PTS"), (s.why ?? "").Named("WHY")).Resolve();
                    s.tip = s.text + "\n\n" + swayTip;
                }
                m.cards.Add(card);
            }
            m.statText = m.cards.Count > 0 ? "CC_FaithPg_Stat".Translate(m.cards.Count.Named("N"), m.people.Named("PEOPLE")).Resolve() : null;
            m.splitTip = "CC_FaithPg_SplitTip".Translate(m.split.ToStringPercent().Named("PCT")).Resolve();
            m.togetherText = m.cards.Count > 1 ? "CC_FaithPg_TogetherSplit".Translate(m.cards.Count.Named("N"), m.wavering.Named("WAVERING")).Resolve() : null;
            return m;
        }

        private static void Roles(Card card, List<Pawn> believers)
        {
            try
            {
                foreach (var r in card.ideo.RolesListForReading)
                {
                    if (r?.def == null) continue;
                    bool leader = r.def == PreceptDefOf.IdeoRole_Leader, guide = r.def == PreceptDefOf.IdeoRole_Moralist;
                    if (!leader && !guide) continue;
                    var who = r.ChosenPawnSingle();
                    bool here = who != null && believers.Contains(who);
                    if (leader)
                    {
                        card.hasLeader = true;
                        card.leader = here ? who : null;
                        card.leaderAway = who != null && !here;
                    }
                    else
                    {
                        card.hasGuide = true;
                        card.guide = here ? who : null;
                        card.guideAway = who != null && !here;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faiths page: roles", e);
            }
        }

        /// <summary>Why a listener might be swayed: wavering, open by nature, likes the speaker.</summary>
        private static string Why(Pawn listener, Pawn speaker)
        {
            var parts = new List<string>();
            float c = Faiths.Certainty(listener);
            if (FaithMath.Wavering(c)) parts.Add("CC_FaithPg_WhyWavering".Translate());
            if (Persona.Get(listener, PersonaAxis.Openness) >= 30f) parts.Add("CC_FaithPg_WhyOpen".Translate());
            if ((listener.relations?.OpinionOf(speaker) ?? 0) >= 20) parts.Add("CC_FaithPg_WhyLikes".Translate(speaker.Named("OTHER")).Resolve());
            if (Persona.Get(listener, PersonaAxis.Pride) >= 40f) parts.Add("CC_FaithPg_WhyProud".Translate());
            if (Faiths.DislikesPreaching(listener)) parts.Add("CC_FaithPg_WhyNoPreaching".Translate());
            return TextUtil.JoinAnd(parts);
        }

        private static string BelieverTip(Pawn p, Believer b, List<FaithAsks.Ask> asks)
        {
            var lines = new List<string> { p.LabelShortCap + ": " + "CC_FaithPg_Certainty".Translate(b.certainty.ToStringPercent().Named("PCT"), FaithPeople.StateWord(b.certainty).Named("STATE")).Resolve() };
            if (b.role != null) lines.Add(b.role);
            if (asks.Count > 0) lines.Add("CC_FaithPg_TipAsks".Translate(TextUtil.JoinAnd(asks.Take(3).Select(FaithAsks.Text).ToList())).Resolve());
            var notes = FaithPeople.Notes(p).Take(3).ToList();
            if (notes.Count > 0)
            {
                lines.Add("");
                foreach (var n in notes) lines.Add(PersonaMarks.DateLabel(n.tick, p) + ": " + FaithCharacter.NoteText(n));
            }
            lines.Add("");
            lines.Add(FaithCharacter.CertaintyTip(p));
            lines.Add("");
            lines.Add("CC_UI_OpenPage".Translate());
            return string.Join("\n", lines);
        }

        // ---------------- Drawing ----------------

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!Faiths.On)
            {
                UiKit.EmptyState(rect, Icons.CatIdeology ?? Icons.People, OffReason());
                return;
            }
            if (model == null || Time.realtimeSinceStartup - modelAt > 1f)
            {
                try
                {
                    model = Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faiths page", e);
                    model = new Model();
                }
                modelAt = Time.realtimeSinceStartup;
            }
            var m = model;
            float top = UiKit.PageHeader(rect, Icons.CatIdeology, "CC_Tab_Faiths".Translate(), "CC_UI_Sub_Faiths".Translate(), m.statText,
                m.split >= 0.5f ? UiKit.Warn : (Color?)null, 0f, m.splitTip);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f, w = view.width;
            y += UiKit.Paragraph(0f, y, w, "CC_FaithPg_Intro".Translate(), UiKit.Faint) + 10f;
            if (m.cards.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.CatIdeology, "CC_FaithPg_None".Translate(), 90f);
            foreach (var card in m.cards)
            {
                try
                {
                    y += DrawCard(card, 0f, y, w) + 10f;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faiths page: a faith's card", e);
                }
            }
            y += 6f;
            y += DrawTogether(0f, y, w, m);
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static string OffReason()
        {
            if (!ModsConfig.IdeologyActive) return "CC_FaithPg_NoIdeology".Translate();
            if (Find.IdeoManager != null && Find.IdeoManager.classicMode) return "CC_FaithPg_Classic".Translate();
            return "CC_FaithPg_Off".Translate();
        }

        private const float Pad = 8f, Face = 34f, FaceW = 96f, Row = 20f;

        private static float DrawCard(Card card, float x, float y, float w)
        {
            float cy = y + Pad;
            float ix = x + Pad + 4f, inner = w - Pad * 2f - 4f;
            // Its name and icon, in its own colour; how many hold it and how firmly.
            if (card.icon != null) UiKit.Icon(new Rect(ix, cy + 1f, 24f, 24f), card.icon, card.color);
            float nx = ix + (card.icon != null ? 30f : 0f);
            Text.Font = GameFont.Medium;
            float nameW = Mathf.Min(inner * 0.55f, Text.CalcSize(card.name).x + 8f);
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(nx, cy - 2f, nameW, 30f), card.name, Color.Lerp(card.color, Color.white, 0.35f), GameFont.Medium);
            UiKit.LabelFit(new Rect(nx + nameW + 6f, cy + 4f, Mathf.Max(0f, inner - (nx - ix) - nameW - 6f), 20f), card.countText, UiKit.Faint, GameFont.Tiny);
            var ideo = card.ideo;
            TooltipHandler.TipRegion(new Rect(ix, cy, inner, 26f), () => IdeoTip(ideo), ideo.id ^ 0x2F1A);
            cy += 30f;
            if (card.memes.Count > 0) cy += UiKit.Pills(ix, cy, inner, card.memes) + 6f;

            // Its leader and moral guide, or that the role is empty here.
            float half = (inner - 10f) / 2f;
            DrawRole(ix, cy, half, "CC_FaithPg_Leader".Translate(), card.hasLeader, card.leader, card.leaderAway);
            DrawRole(ix + half + 10f, cy, half, "CC_FaithPg_Guide".Translate(), card.hasGuide, card.guide, card.guideAway);
            cy += 28f;

            // Its believers, each with how firmly they hold it.
            int perRow = Mathf.Max(1, Mathf.FloorToInt(inner / FaceW));
            for (int i = 0; i < card.believers.Count; i++)
                DrawBeliever(ix + (i % perRow) * FaceW, cy + (i / perRow) * (Face + 44f), card.believers[i]);
            cy += Mathf.CeilToInt(card.believers.Count / (float)perRow) * (Face + 44f) + 2f;

            // What it asks that weighs on someone right now.
            UiKit.LabelFit(new Rect(ix, cy, inner, Row), "CC_FaithPg_Asks".Translate(), UiKit.HeaderColor, GameFont.Tiny);
            TooltipHandler.TipRegion(new Rect(ix, cy, inner, Row), "CC_FaithP_AsksTip".Translate());
            cy += Row;
            if (card.asks.Count == 0)
            {
                UiKit.LabelFit(new Rect(ix + 10f, cy, inner - 10f, Row), "CC_FaithPg_AsksNone".Translate(), UiKit.Dim, GameFont.Tiny);
                cy += Row;
            }
            foreach (var a in card.asks)
            {
                var r = new Rect(ix + 10f, cy, inner - 10f, Row);
                UiKit.LabelFit(r, a.text, a.mood <= -5f ? UiKit.Bad : UiKit.LossColor, GameFont.Tiny);
                TooltipHandler.TipRegion(r, a.tip);
                cy += Row;
            }

            // Who is likely to sway whom.
            cy += 4f;
            UiKit.LabelFit(new Rect(ix, cy, inner, Row), "CC_FaithPg_Sways".Translate(), UiKit.HeaderColor, GameFont.Tiny);
            TooltipHandler.TipRegion(new Rect(ix, cy, inner, Row), "CC_FaithP_SwayTip".Translate());
            cy += Row;
            if (card.sways.Count == 0)
            {
                string none = card.believers.Any(b => FaithMath.Devout(b.certainty)) ? "CC_FaithPg_SwaysNone" : "CC_FaithPg_SwaysNoDevout";
                UiKit.LabelFit(new Rect(ix + 10f, cy, inner - 10f, Row), none.Translate(), UiKit.Dim, GameFont.Tiny);
                cy += Row;
            }
            foreach (var s in card.sways)
            {
                var r = new Rect(ix + 10f, cy, inner - 10f, Row);
                Widgets.DrawHighlightIfMouseover(r);
                UiKit.LabelFit(r, s.text, UiKit.Info, GameFont.Tiny);
                TooltipHandler.TipRegion(r, s.tip);
                if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenPerson(s.listener);
                cy += Row;
            }

            // The council's buttons for this faith (see the hook at the top).
            cy += DrawButtons(ix, cy + 4f, inner, card.ideo, true);
            cy += Pad;
            UiKit.Card(new Rect(x, y, w, cy - y), card.color, false, false);
            return cy - y;
        }

        private static string IdeoTip(Ideo ideo)
        {
            try
            {
                string tip = ideo.name.CapitalizeFirst();
                if (!ideo.description.NullOrEmpty()) tip += "\n\n" + ideo.description;
                return tip + "\n\n" + "CC_FaithPg_IdeoTip".Translate();
            }
            catch (Exception)
            {
                return ideo?.name ?? "";
            }
        }

        private static void DrawRole(float x, float y, float w, string what, bool exists, Pawn holder, bool away)
        {
            Text.Font = GameFont.Small;
            float lw = Mathf.Min(w * 0.45f, Text.CalcSize(what + ":").x + 6f);
            UiKit.LabelFit(new Rect(x, y, lw, 24f), what + ":", UiKit.Faint);
            var rest = new Rect(x + lw, y, w - lw, 24f);
            if (holder != null)
            {
                UiKit.PawnChip(rest.x, y, holder, 24f, true, rest.width);
                return;
            }
            string text = !exists ? "CC_FaithPg_RoleNone".Translate() : away ? "CC_FaithPg_RoleAway".Translate() : "CC_FaithPg_RoleEmpty".Translate();
            UiKit.LabelFit(rest, text, exists ? UiKit.Warn : UiKit.Dim);
            TooltipHandler.TipRegion(rest, () => "CC_FaithPg_RoleEmptyTip".Translate(what.Named("ROLE")).Resolve(), what.GetHashCode() ^ 0x51D3);
        }

        private static void DrawBeliever(float x, float y, Believer b)
        {
            var p = b.p;
            float c = b.certainty;
            var color = FaithMath.Devout(c) ? UiKit.Good : FaithMath.Wavering(c) ? UiKit.Warn : UiKit.Info;
            var face = new Rect(x + (FaceW - Face) / 2f, y, Face, Face);
            UiKit.Portrait(face, p, color);
            UiKit.LabelFit(new Rect(x, y + Face + 1f, FaceW - 6f, 15f), p.LabelShort, Color.white, GameFont.Tiny, TextAnchor.UpperCenter);
            UiKit.Bar(new Rect(x + 8f, y + Face + 18f, FaceW - 22f, 6f), c, color);
            string word = FaithPeople.StateWord(c) + " " + c.ToStringPercent();
            UiKit.LabelFit(new Rect(x, y + Face + 25f, FaceW - 6f, 15f), word, color, GameFont.Tiny, TextAnchor.UpperCenter);
            var area = new Rect(x, y, FaceW - 6f, Face + 41f);
            Widgets.DrawHighlightIfMouseover(area);
            TooltipHandler.TipRegion(area, b.tip);
            if (Widgets.ButtonInvisible(area)) MainTabWindow_Council.OpenPerson(p);
        }

        /// <summary>The hook's buttons for a faith (perFaith) or for the colony; returns the height used.</summary>
        private static float DrawButtons(float x, float y, float w, Ideo ideo, bool perFaith)
        {
            if (Buttons.Count == 0) return 0f;
            float bx = x;
            bool any = false;
            foreach (var b in Buttons)
            {
                if (b == null || b.perFaith != perFaith) continue;
                try
                {
                    if (b.shows != null && !b.shows(ideo)) continue;
                    string label = b.label?.Invoke(ideo) ?? b.key ?? "?";
                    Text.Font = GameFont.Small;
                    float bw = Mathf.Min(w, Text.CalcSize(label).x + 24f);
                    if (bx > x && bx + bw > x + w) break;
                    var r = new Rect(bx, y, bw, 28f);
                    string blocked = b.blocked?.Invoke(ideo);
                    string tip = b.tip?.Invoke(ideo);
                    if (!blocked.NullOrEmpty()) tip = (tip.NullOrEmpty() ? "" : tip + "\n\n") + blocked;
                    if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
                    if (blocked.NullOrEmpty())
                    {
                        if (Widgets.ButtonText(r, label)) b.act?.Invoke(ideo);
                    }
                    else
                    {
                        GUI.color = UiKit.Dim;
                        Widgets.ButtonText(r, label, true, true, false);
                        GUI.color = Color.white;
                    }
                    bx += bw + 6f;
                    any = true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faiths page: button " + b.key, e);
                }
            }
            return any ? 34f : 0f;
        }

        /// <summary>How to bring people together: the game's own tools, and what Rimfolk adds; then the council's buttons.</summary>
        private static float DrawTogether(float x, float y, float w, Model m)
        {
            float start = y;
            y += UiKit.SectionTitle(x, y, w, "CC_FaithPg_Together".Translate(), Icons.Heart, UiKit.HeaderColor);
            if (m.togetherText != null) y += UiKit.Paragraph(x, y, w, m.togetherText, UiKit.Faint) + 6f;
            foreach (var key in new[] { "CC_FaithPg_ToolConvert", "CC_FaithPg_ToolRitual", "CC_FaithPg_ToolMood", "CC_FaithPg_ToolTalk", "CC_FaithPg_ToolRoles" })
            {
                UiKit.DiscFill(new Rect(x + 4f, y + 7f, 5f, 5f), UiKit.Gold);
                y += UiKit.Paragraph(x + 16f, y, w - 16f, key.Translate(), Color.white) + 4f;
            }
            y += DrawButtons(x, y + 4f, w, null, false);
            var almanac = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail("Rimfolk_Alm_C_Faith");
            if (almanac != null)
            {
                y += 4f;
                if (Widgets.ButtonText(new Rect(x, y, 220f, 28f), "CC_FaithPg_ReadMore".Translate())) MainTabWindow_Council.OpenAlmanac(almanac);
                y += 32f;
            }
            return y - start;
        }
    }
}
