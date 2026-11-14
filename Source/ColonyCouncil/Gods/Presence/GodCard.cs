using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's own card (docs/design/gods.md, "Presence"; brainstorm 25): not good news or bad news but a presence. It
    /// comes as a letter of its own kind (gold in the stack, no tag) through the teller, a few seconds after the sting,
    /// once the camera is there and everyone has turned; opened, it is dark paper with a gold border, the god named in its
    /// colour and set large, as the colony knows it (veiled, not named at all), and the words set apart from the narrator's:
    /// what was seen and heard, never why. Over the name, the colony's own sign of the god (Gods/GodSigns.cs) in the form of
    /// its age, only when the card names the god and the colony has been told what it is; a veiled card shows none. It
    /// doesn't pause the game, so the god's track plays on behind it.
    /// </summary>
    public static class GodCard
    {
        public const string LetterDefName = "Rimfolk_GodCard";

        /// <summary>The card's words for a moment, which the next GodLetter the stack receives takes (the teller makes the letter).</summary>
        internal static GodLetter pending;

        internal static void Send(GodLookDef look, string name, string epithet, Map map, IntVec3 at, PresenceKind kind, Pawn avatar, string title, string text, bool veiled,
            Color a, Color b, string sign = null)
        {
            var def = DefDatabase<LetterDef>.GetNamedSilentFail(LetterDefName);
            if (def == null) return;
            string label = !title.NullOrEmpty() ? title
                : veiled ? "CC_GodP_Card_VeiledTitle".Translate().Resolve()
                : ("CC_GodP_Card_Title_" + kind).Translate(name.Named("NAME")).Resolve();
            label = label.CapitalizeFirst();
            string body = !text.NullOrEmpty() ? text : ("CC_GodP_Card_Text_" + kind + (veiled ? "_Veiled" : "")).Translate(name.Named("NAME")).Resolve().CapitalizeFirst();
            var look2 = avatar != null && avatar.Spawned ? new LookTargets(avatar) : new LookTargets(at, map);
            pending = new GodLetter { look = look, godName = name, epithet = epithet, kind = kind, veiled = veiled, colorA = a, colorB = b, sign = veiled ? null : sign };
            try
            {
                Tell.Letter(NewsKind.Land, Importance.Important, map, label, body, def, look2, null, "god|" + name + "|" + kind + "|" + GenTicks.TicksGame / 2500);
            }
            finally
            {
                pending = null;
            }
        }

        // ---------------- The card's look ----------------

        public static readonly Color Paper = new Color(0.075f, 0.066f, 0.09f), PaperDeep = new Color(0.045f, 0.04f, 0.055f);
        public static readonly Color Gold = new Color(0.86f, 0.70f, 0.36f), GoldFaint = new Color(0.86f, 0.70f, 0.36f, 0.45f);
        public static readonly Color Ink = new Color(0.93f, 0.89f, 0.80f), InkFaint = new Color(0.93f, 0.89f, 0.80f, 0.62f);
    }

    /// <summary>The letter in the stack for a god's moment: it keeps what the card shows, and opens the card (never the event card).</summary>
    public class GodLetter : RimfolkLetter
    {
        public GodLookDef look;
        public string godName, epithet;
        public PresenceKind kind;
        public bool veiled;
        public Color colorA = Color.white, colorB = Color.white;
        /// <summary>The colony's sign of the god on the card (a god's key, Gods/GodSigns.cs), or null.</summary>
        public string sign;

        public override void Received()
        {
            base.Received();
            var p = GodCard.pending;
            if (p == null || look != null) return;
            look = p.look;
            godName = p.godName;
            epithet = p.epithet;
            kind = p.kind;
            veiled = p.veiled;
            colorA = p.colorA;
            colorB = p.colorB;
            sign = p.sign;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return Option_Close;
                if (lookTargets.IsValid()) yield return Option_JumpToLocation;
            }
        }

        public override void OpenLetter()
        {
            try
            {
                Find.WindowStack.Add(new GodCardWindow(this));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: opening a god's card", e);
                base.OpenLetter();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref look, "look");
            Scribe_Values.Look(ref godName, "godName");
            Scribe_Values.Look(ref epithet, "epithet");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref veiled, "veiled");
            Scribe_Values.Look(ref colorA, "colorA", Color.white);
            Scribe_Values.Look(ref colorB, "colorB", Color.white);
            Scribe_Values.Look(ref sign, "sign");
        }
    }

    /// <summary>A god's card: dark paper and gold, its name in its colour (or none, veiled), the words, and the way to the place.</summary>
    public class GodCardWindow : FramedWindow
    {
        private readonly GodLetter letter;
        private Vector2 scroll;
        private float textH = -1f, laidW = -1f;

        public GodCardWindow(GodLetter l) : base("GodCard")
        {
            letter = l;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            doCloseX = true;
            draggable = true;
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
        }

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        protected override string FrameTitle => letter?.Label.Resolve() ?? "";

        protected override Texture2D FrameIcon => null;

        public override void PostClose()
        {
            base.PostClose();
            // Read: it leaves the stack (it stays in the History tab).
            if (letter != null && Find.LetterStack.LettersListForReading.Contains(letter)) Find.LetterStack.RemoveLetter(letter);
        }

        protected override void DoFramedContents(Rect inRect)
        {
            if (letter == null)
            {
                Close(false);
                return;
            }
            try
            {
                Draw(inRect.ContractedBy(6f));
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void Draw(Rect r)
        {
            // The paper: dark, deeper at its edges, and a double gold border.
            Widgets.DrawBoxSolid(r, GodCard.PaperDeep);
            Widgets.DrawBoxSolid(r.ContractedBy(4f), GodCard.Paper);
            GUI.color = GodCard.Gold;
            Widgets.DrawBox(r.ContractedBy(4f), 2);
            GUI.color = GodCard.GoldFaint;
            Widgets.DrawBox(r.ContractedBy(9f), 1);
            GUI.color = Color.white;
            var inner = r.ContractedBy(22f);
            float y = inner.y;
            var godColor = Color.Lerp(letter.colorA, Color.white, 0.25f);
            y += 18f;
            // Who: the god as the colony knows it, set large in its colour; veiled, nobody can say.
            Text.Anchor = TextAnchor.UpperCenter;
            if (letter.veiled)
            {
                Text.Font = GameFont.Small;
                GUI.color = GodCard.GoldFaint;
                Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "<i>" + "CC_GodP_Card_SomeSay".Translate() + "</i>");
                y += 26f;
            }
            else
            {
                // The colony's sign of it, over its name, in its colours.
                var tex = GodSigns.Plain(letter.sign);
                if (tex != null)
                {
                    GodSigns.Draw(new Rect(inner.center.x - 32f, y - 6f, 64f, 64f), tex);
                    y += 62f;
                }
                // The name measured at the size it is drawn at (the game's measuring strips size tags).
                y += UiKit.SizedLabel(inner.x, y, inner.width, (letter.godName ?? "").CapitalizeFirst(), 28, godColor, TextAnchor.UpperCenter) + 2f;
                if (!letter.epithet.NullOrEmpty())
                {
                    Text.Font = GameFont.Small;
                    GUI.color = GodCard.GoldFaint;
                    Widgets.Label(new Rect(inner.x, y, inner.width, 22f), letter.epithet.CapitalizeFirst());
                    y += 24f;
                }
            }
            // A gold rule, broken in the middle.
            y += 6f;
            GUI.color = GodCard.Gold;
            float mid = inner.center.x;
            Widgets.DrawLineHorizontal(inner.x + 30f, y, mid - inner.x - 38f);
            Widgets.DrawLineHorizontal(mid + 8f, y, inner.xMax - mid - 38f);
            y += 14f;
            // The words, set apart in their own ink.
            var foot = new Rect(inner.x, inner.yMax - 34f, inner.width, 32f);
            var outer = new Rect(inner.x, y, inner.width, foot.y - 10f - y);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            string text = letter.Text.Resolve();
            float w = outer.width - 16f;
            if (Math.Abs(w - laidW) > 0.5f)
            {
                laidW = w;
                textH = Text.CalcHeight(text, w);
            }
            var view = new Rect(0f, 0f, w, Math.Max(textH, outer.height));
            Widgets.BeginScrollView(outer, ref scroll, view);
            GUI.color = GodCard.Ink;
            Widgets.Label(new Rect(0f, 0f, w, textH), text);
            Widgets.EndScrollView();
            GUI.color = Color.white;
            // The way to the place, and closing.
            var go = new Rect(foot.x, foot.y, foot.width / 2f - 6f, foot.height);
            var close = new Rect(foot.center.x + 6f, foot.y, foot.width / 2f - 6f, foot.height);
            if (letter.lookTargets != null && letter.lookTargets.IsValid() && GoldButton(go, "CC_GodP_Card_Go".Translate()))
            {
                CameraJumper.TryJumpAndSelect(letter.lookTargets.TryGetPrimaryTarget());
                Close();
            }
            if (GoldButton(close, "CC_GodP_Card_Close".Translate())) Close();
        }

        private static bool GoldButton(Rect r, string label)
        {
            bool over = Mouse.IsOver(r);
            Widgets.DrawBoxSolid(r, over ? new Color(0.2f, 0.16f, 0.09f) : new Color(0.11f, 0.095f, 0.08f));
            GUI.color = over ? GodCard.Gold : GodCard.GoldFaint;
            Widgets.DrawBox(r, 1);
            GUI.color = over ? GodCard.Gold : GodCard.Ink;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(r, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            if (over) MouseoverSounds.DoRegion(r);
            return Widgets.ButtonInvisible(r);
        }
    }
}
