using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "About this picture" (docs: DESIGN.md, "Portraits: about this picture, and the variants the game wants"; the
    /// player, 23 November 2026: "one of the right-click options could open kind of a menu where I see the image that is
    /// currently assigned to the pawn, and where I could see ... all the different attributes and characteristics that this
    /// image has assigned in the database ... which of the characteristics were now relevant for why this image was chosen
    /// for this specific colonist, and that I perhaps also could make corrections in there ... And maybe that it also shows
    /// the different variants also of this image", and "the about screen should also clearly mark what is special about a
    /// certain variant and when it becomes active"). From the large portrait's right-click menu, for the painted picture a
    /// person keeps. In Rimfolk's frame (key "PortraitAbout"):
    /// <list type="bullet">
    /// <item>the picture and its record (kind, sex, age band, skin and hair colour, hair, beard, build, clothes, features,
    /// marks with their side, looks, work, headwear, facing, its batch and file, its face);</item>
    /// <item>why it is theirs: how it was kept, each hard rule of the matcher met or not for them, each part of its distance
    /// from their looks and the sum (<see cref="PortraitLibraryMath.Parts"/>), and the next pictures by distance;</item>
    /// <item>its versions: each expression and mark variant, what it shows and when it is shown, the one shown now, and
    /// "Delete this variant for good..."; the versions wanted and not made yet;</item>
    /// <item>corrections picked from lists, taken at once and written to the library folder (<see cref="PortraitCorrect"/>).</item>
    /// </list>
    /// What it shows is read again every two seconds and after a change, outside the drawing.
    /// </summary>
    public class Window_PortraitAbout : FramedWindow
    {
        private readonly Pawn pawn;
        private Vector2 scroll;
        private float height = 900f;
        private float builtAt = -100f;
        private Model m = new Model();

        private const float RebuildEvery = 2f, LabelW = 170f, Gap = 10f;

        private sealed class Row
        {
            public string title, what, when, name, of;
            public PictureFile file;
            public bool shownNow, deletable, face, condition;
        }

        private sealed class Model
        {
            public bool any;
            public int root = -1, shown = -1;
            public PortraitRecord rec, rootRec;
            public FolderPortrait folder, rootFolder;
            public PictureFile pic;
            public PortraitLook look;
            public PortraitLibraryMath.HardRules rules;
            public PortraitLibraryMath.DistanceParts parts;
            public KeptPortrait kept;
            public readonly List<int> next = new List<int>();
            public readonly List<float> nextD = new List<float>();
            public readonly List<Row> rows = new List<Row>();
            public readonly List<KeyValuePair<string, string>> wanted = new List<KeyValuePair<string, string>>();
            public bool own;
            public string whyNot;
        }

        public Window_PortraitAbout(Pawn pawn) : base("PortraitAbout")
        {
            this.pawn = pawn;
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(780f, UI.screenWidth - 40f), Mathf.Min(880f, UI.screenHeight - 40f));

        protected override string FrameTitle => "CC_PortraitAbout_Title".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.ColonistCard;

        /// <summary>The window for a person (one at a time: one already open for them is brought back with what it shows now).</summary>
        public static void Open(Pawn p)
        {
            if (p == null) return;
            foreach (var w in Find.WindowStack.Windows.OfType<Window_PortraitAbout>().ToList()) w.Close(false);
            Find.WindowStack.Add(new Window_PortraitAbout(p));
        }

        public override void WindowOnGUI()
        {
            if (Time.realtimeSinceStartup >= builtAt + RebuildEvery) Build();
            base.WindowOnGUI();
        }

        // ---------------- What it shows ----------------

        private void Build()
        {
            builtAt = Time.realtimeSinceStartup;
            var n = new Model();
            try
            {
                var lib = PortraitLibrary.Source;
                if (pawn == null || pawn.Discarded || !lib.KeptFor(pawn, out n.root, out n.shown))
                {
                    m = n;
                    return;
                }
                n.any = true;
                n.rec = lib.RecordAt(n.shown);
                n.rootRec = lib.RecordAt(n.root);
                n.pic = lib.FileAt(n.shown);
                n.folder = PortraitFolder.RecordNamed(n.rec.name);
                n.rootFolder = PortraitFolder.RecordNamed(n.rootRec.name);
                n.kept = PortraitKeeper.Instance?.Get(pawn.thingIDNumber);
                if (PortraitReader.LookOf(pawn, out var look))
                {
                    n.look = look;
                    n.rules = PortraitLibraryMath.Rules(n.rec, look);
                    n.parts = PortraitLibraryMath.Parts(look, n.rec);
                    foreach (int i in lib.NextNearest(pawn, look, n.root, 5))
                    {
                        n.next.Add(i);
                        n.nextD.Add(PortraitLibraryMath.Distance(look, lib.RecordAt(i)));
                    }
                }
                Versions(n, lib);
                int rank = PortraitVariants.RankOf(pawn, n.kept?.notable ?? false);
                n.own = rank >= 0;
                foreach (var w in PortraitVariants.WantedFor(pawn, rank))
                    n.wanted.Add(new KeyValuePair<string, string>(w.word, PortraitVariantsMath.VariantFile(w.picture, w.word, PortraitFolder.Deleted)));
                n.whyNot = PortraitCorrect.WhyNot(n.rootRec.name) ?? (PortraitCorrect.Running ? "CC_Portrait_CorrectBusy".Translate().Resolve() : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the window about a picture", e);
            }
            m = n;
        }

        /// <summary>The picture's versions: its own expressions, then each mark variant and its expressions.</summary>
        private void Versions(Model n, PortraitLibrary lib)
        {
            void Faces(PortraitRecord of, string ofWord)
            {
                var names = lib.ExpressionNamesOf(of.name);
                var files = lib.ExpressionsOf(of.name);
                foreach (var w in PortraitLifeMath.Faces)
                {
                    if (!names.TryGetValue(w, out string name)) continue;
                    PictureFile file = null;
                    files?.TryGetValue(w, out file);
                    n.rows.Add(new Row
                    {
                        title = ("CC_PortraitAbout_Face_" + w).Translate().Resolve() + (ofWord != null ? " (" + "CC_PortraitAbout_OfVariant".Translate(ofWord.Named("WORD")).Resolve() + ")" : ""),
                        what = ("CC_PortraitAbout_FaceWhat_" + w).Translate().Resolve(), when = ("CC_PortraitAbout_FaceWhen_" + w).Translate().Resolve(), name = name, file = file,
                        shownNow = of == n.rec && FaceEngine.FaceNow(pawn) == w, deletable = PortraitFolder.RecordNamed(name) != null, of = of.name, face = true
                    });
                }
            }
            void Conditions(PortraitRecord of, string ofWord)
            {
                var names = lib.ConditionNamesOf(of.name);
                var files = lib.ConditionsOf(of.name);
                foreach (var w in PortraitLifeMath.Conditions)
                {
                    if (!names.TryGetValue(w, out string name)) continue;
                    PictureFile file = null;
                    files?.TryGetValue(w, out file);
                    n.rows.Add(new Row
                    {
                        title = ("CC_PortraitAbout_Cond_" + w).Translate().Resolve() + (ofWord != null ? " (" + "CC_PortraitAbout_OfVariant".Translate(ofWord.Named("WORD")).Resolve() + ")" : ""),
                        what = ("CC_PortraitAbout_CondWhat_" + w).Translate().Resolve(), when = ("CC_PortraitAbout_CondWhen_" + w).Translate().Resolve(), name = name, file = file,
                        shownNow = of == n.rec && PortraitEffects.PaintedNow(pawn) == w, deletable = PortraitFolder.RecordNamed(name) != null, of = of.name, condition = true
                    });
                }
            }
            Faces(n.rootRec, null);
            Conditions(n.rootRec, null);
            foreach (int v in lib.VariantsOf(n.root))
            {
                var r = lib.RecordAt(v);
                if (r == null) continue;
                string title = VariantTitle(r.variant);
                n.rows.Add(new Row
                {
                    title = title, what = VariantText("MarkWhat", r.variant), when = VariantText("MarkWhen", r.variant), name = r.name, file = lib.FileAt(v), shownNow = v == n.shown,
                    deletable = PortraitFolder.RecordNamed(r.name) != null
                });
                Faces(r, title);
                Conditions(r, title);
            }
        }

        private static string SideWord(PortraitSide side) => (side == PortraitSide.Right ? "CC_PortraitAbout_SideRight" : "CC_PortraitAbout_SideLeft").Translate().Resolve();

        /// <summary>A mark variant's name in plain words ("Lost eye, left").</summary>
        private static string VariantTitle(string word) => VariantText("Mark", word);

        private static string VariantText(string what, string word)
        {
            string mark = PortraitLibraryMath.MarkOfVariant(word, out var side);
            if (mark == null) return word;
            return ("CC_PortraitAbout_" + what + "_" + mark).Translate(SideWord(side).Named("SIDE")).Resolve();
        }

        // ---------------- Drawing ----------------

        protected override void DoFramedContents(Rect inRect)
        {
            // The thumbnails are loaded by the store's pump, whether or not a portrait is drawn now.
            PortraitStore.Pump();
            if (pawn == null || pawn.Discarded)
            {
                Close();
                return;
            }
            string stat = m.any ? FileName(m.folder?.texPath ?? m.rec.name) : null;
            float top = UiKit.DialogHeader(new Rect(inRect.x, inRect.y, inRect.width - (TitleInBar ? 0f : 24f), UiKit.HeaderHeight), TitleInBar, Icons.ColonistCard, FrameTitle,
                "CC_PortraitAbout_Sub".Translate(pawn.Named("PAWN")).Resolve(), stat, UiKit.Faint);
            var outRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y;
            try
            {
                y = Body(view.width);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: drawing the window about a picture", e);
                y = 100f;
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private float Body(float w)
        {
            float y = 0f;
            if (!m.any)
            {
                y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_None".Translate(pawn.Named("PAWN")).Resolve(), UiKit.Ink);
                return y;
            }
            y = Record(w, y) + Gap;
            y = Why(w, y) + Gap;
            y = Next(w, y) + Gap;
            y = VersionsBlock(w, y) + Gap;
            y = Corrections(w, y);
            return y;
        }

        /// <summary>The picture beside its record.</summary>
        private float Record(float w, float y)
        {
            float pw = Mathf.Min(220f, w * 0.34f), ph = pw * 1.3f;
            Thumb(new Rect(0f, y, pw, ph), m.pic);
            float x = pw + 14f, lw = w - x;
            float ly = y + UiKit.SectionTitle(x, y, lw, "CC_PortraitAbout_Record".Translate().Resolve());
            var r = m.rec;
            ly = Line(x, ly, lw, "Kind", KindWords(r));
            ly = Line(x, ly, lw, "Sex", PortraitLibraryMath.Word(r.sex));
            ly = Line(x, ly, lw, "Age", PortraitLibraryMath.Word(r.age));
            ly = Line(x, ly, lw, "Skin", r.skinSet ? Colour(r.skin, true) : None(), r.skinSet ? r.skin : (PortraitColour?)null);
            ly = Line(x, ly, lw, "HairColour", r.hairColourSet ? Colour(r.hairColour, false) : None(), r.hairColourSet ? r.hairColour : (PortraitColour?)null);
            ly = Line(x, ly, lw, "Hair", Low(r.hair));
            ly = Line(x, ly, lw, "Beard", Low(r.beard));
            ly = Line(x, ly, lw, "Build", Low(r.build));
            ly = Line(x, ly, lw, "Clothes", (r.era == PortraitEra.Unknown ? Any() : Low(r.era)) + ", " + (r.dress == PortraitDress.Unknown ? Any() : Low(r.dress)));
            ly = Line(x, ly, lw, "Features", Words(PortraitLibraryMath.Features.Where(r.features.Contains)));
            ly = Line(x, ly, lw, "Marks", MarksWords(r));
            ly = Line(x, ly, lw, "Looks", Words(r.looks.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));
            ly = Line(x, ly, lw, "Role", r.role.Length > 0 ? r.role : None());
            ly = Line(x, ly, lw, "Headwear", YesNo(r.headwear));
            ly = Line(x, ly, lw, "Facing", Low(r.facing));
            ly = Line(x, ly, lw, "File", m.folder != null ? m.folder.texPath.Replace('/', '\\') + ".png" : "CC_PortraitAbout_FileMod".Translate(r.name.Named("NAME")).Resolve());
            ly = Line(x, ly, lw, "Character", r.Character);
            if (r.Pinned) ly = Line(x, ly, lw, "Pinned", r.pawn);
            if (m.shown != m.root)
                ly = Line(x, ly, lw, "Version", "CC_PortraitAbout_VariantOfValue".Translate(FileName(m.rootFolder?.texPath ?? m.rootRec.name).Named("FILE"), VariantTitle(r.variant).Named("WORD")).Resolve());
            return Mathf.Max(y + ph, ly);
        }

        /// <summary>One line of the record: its label, its value (a colour's swatch before it), wrapped where long.</summary>
        private static float Line(float x, float y, float w, string key, string value, PortraitColour? swatch = null)
        {
            Text.Font = GameFont.Small;
            float vx = x + LabelW, vw = w - LabelW;
            if (swatch.HasValue)
            {
                // The record's own colour (its data, not one of the look's).
                var c = swatch.Value;
                var shown = Color.white;
                shown.r = c.r;
                shown.g = c.g;
                shown.b = c.b;
                Widgets.DrawBoxSolidWithOutline(new Rect(vx, y + 4f, 14f, 14f), shown, UiKit.Edge);
                vx += 20f;
                vw -= 20f;
            }
            float h = Mathf.Max(22f, Text.CalcHeight(value ?? "", vw));
            UiKit.LabelFit(new Rect(x, y, LabelW - 6f, 22f), ("CC_PortraitAbout_" + key).Translate().Resolve(), UiKit.Faint);
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(vx, y + 1f, vw, h), value ?? "");
            GUI.color = Color.white;
            return y + h;
        }

        /// <summary>Why it is theirs: how it was kept, the hard rules, the distance's parts, the sum.</summary>
        private float Why(float w, float y)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_PortraitAbout_Why".Translate().Resolve());
            var k = m.kept;
            string how = k != null && k.pinned ? "CC_PortraitAbout_KeptPinned" : k != null && k.byHand ? "CC_PortraitAbout_KeptByHand" : "CC_PortraitAbout_KeptPicked";
            y += UiKit.Paragraph(0f, y, w, how.Translate(pawn.Named("PAWN")).Resolve(), UiKit.Ink) + 4f;
            if (m.shown != m.root) y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_ShownVariant".Translate(VariantTitle(m.rec.variant).Named("WORD")).Resolve(), UiKit.Ink) + 4f;
            if (m.look == null) return y;
            var l = m.look;
            y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_Rules".Translate().Resolve(), UiKit.Heading, GameFont.Tiny) + 2f;
            var ru = m.rules;
            y = Rule(w, y, "CC_PortraitAbout_RuleKind", l.baseliner && l.race == "human" ? "human, baseliner" : l.race + ", " + (l.xenotype.Length > 0 ? l.xenotype : "?"), ru.kind);
            y = Rule(w, y, "CC_PortraitAbout_RuleSex", PortraitLibraryMath.Word(l.sex), ru.sex);
            y = Rule(w, y, "CC_PortraitAbout_RuleAge", PortraitLibraryMath.Word(l.age), ru.age);
            y = Rule(w, y, "CC_PortraitAbout_RuleSkin", Colour(l.skin, true), ru.skin);
            y = Rule(w, y, "CC_PortraitAbout_RuleMarks", PersonMarks(l), ru.marks);
            y = Rule(w, y, "CC_PortraitAbout_RuleLooks", Words(l.looks.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Concat(l.role.Length > 0 ? new[] { l.role } : new string[0])), ru.looks);
            y = Rule(w, y, "CC_PortraitAbout_RuleSpecials", Words(l.specials.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)), ru.specials);
            y = Rule(w, y, "CC_PortraitAbout_RuleHeadwear", YesNo(l.headwear), ru.headwear);
            y = Rule(w, y, "CC_PortraitAbout_RuleEars", (PortraitLibraryMath.HumanLooking(l) ? "CC_PortraitAbout_EarsHuman" : "CC_PortraitAbout_EarsFantasy").Translate().Resolve(), ru.ears);
            if (!ru.All) y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_KeptAnyway".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
            y += 6f;
            y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_Costs".Translate().Resolve(), UiKit.Heading, GameFont.Tiny) + 2f;
            var p = m.parts;
            y = Cost(w, y, "CC_PortraitAbout_CostSkin", p.skin);
            y = Cost(w, y, "CC_PortraitAbout_CostHairColour", p.hairColour);
            y = Cost(w, y, "CC_PortraitAbout_CostHair", p.hair);
            y = Cost(w, y, "CC_PortraitAbout_CostBeard", p.beard);
            y = Cost(w, y, "CC_PortraitAbout_CostBuild", p.build);
            y = Cost(w, y, "CC_PortraitAbout_CostEra", p.era);
            y = Cost(w, y, "CC_PortraitAbout_CostDress", p.dress);
            y = Cost(w, y, "CC_PortraitAbout_CostFeatures", p.features);
            y = Cost(w, y, "CC_PortraitAbout_CostMarks", p.marks);
            y = Cost(w, y, "CC_PortraitAbout_CostLooks", p.looks);
            UiKit.Divider(0f, y + 1f, Mathf.Min(w, 360f));
            y = Cost(w, y + 3f, "CC_PortraitAbout_CostTotal", p.Total, true);
            y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_CloseNote".Translate(PortraitLibraryMath.CloseEnough.ToString("0", CultureInfo.InvariantCulture).Named("MOST")).Resolve(),
                UiKit.Faint, GameFont.Tiny);
            return y;
        }

        private static float Rule(float w, float y, string key, string theirs, bool met)
        {
            Text.Font = GameFont.Small;
            float mw = 80f, tw = w - LabelW - mw - 8f;
            float h = Mathf.Max(22f, Text.CalcHeight(theirs ?? "", tw));
            UiKit.LabelFit(new Rect(0f, y, LabelW + 60f, 22f), key.Translate().Resolve(), UiKit.Faint);
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(LabelW + 64f, y + 1f, tw - 64f, h), "CC_PortraitAbout_Theirs".Translate((theirs ?? "").Named("VALUE")).Resolve());
            GUI.color = Color.white;
            UiKit.LabelFit(new Rect(w - mw, y, mw, 22f), (met ? "CC_PortraitAbout_Met" : "CC_PortraitAbout_NotMet").Translate().Resolve(), met ? UiKit.Good : UiKit.Bad, GameFont.Small,
                TextAnchor.MiddleRight);
            return y + h;
        }

        private static float Cost(float w, float y, string key, float cost, bool total = false)
        {
            float cw = Mathf.Min(w, 360f);
            UiKit.LabelFit(new Rect(0f, y, cw - 70f, 22f), key.Translate().Resolve(), total ? UiKit.Ink : UiKit.Faint);
            UiKit.LabelFit(new Rect(cw - 70f, y, 70f, 22f), cost.ToString("0.00", CultureInfo.InvariantCulture), total ? UiKit.Ink : cost > 0f ? UiKit.Ink : UiKit.Faint, GameFont.Small,
                TextAnchor.MiddleRight);
            return y + 22f;
        }

        /// <summary>The next pictures by distance, small, with their names and distances.</summary>
        private float Next(float w, float y)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_PortraitAbout_Next".Translate().Resolve());
            if (m.next.Count == 0) return y + UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_NextNone".Translate().Resolve(), UiKit.Faint);
            float tw = Mathf.Min(120f, (w - 4f * 8f) / 5f), th = tw * 1.3f;
            var lib = PortraitLibrary.Source;
            for (int k = 0; k < m.next.Count; k++)
            {
                float x = k * (tw + 8f);
                var r = lib.RecordAt(m.next[k]);
                Thumb(new Rect(x, y, tw, th), lib.FileAt(m.next[k]));
                string name = FileName(PortraitFolder.RecordNamed(r?.name)?.texPath ?? r?.name ?? "");
                UiKit.LabelFit(new Rect(x, y + th + 2f, tw, 18f), name, UiKit.Ink, GameFont.Tiny);
                UiKit.LabelFit(new Rect(x, y + th + 18f, tw, 18f), m.nextD[k].ToString("0.00", CultureInfo.InvariantCulture), UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(x, y, tw, th + 36f), name);
            }
            return y + th + 38f;
        }

        /// <summary>Its expressions and mark variants, what each shows and when; those wanted and not made yet.</summary>
        private float VersionsBlock(float w, float y)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_PortraitAbout_Versions".Translate().Resolve());
            bool facesOn = ColonyCouncilMod.Settings?.portraitFaces ?? true;
            if (m.rows.Count == 0 && m.wanted.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_VersionsNone".Translate().Resolve(), UiKit.Faint) + 4f;
            if (m.rows.Any(r => r.face))
                y +=UiKit.Paragraph(0f, y, w, (facesOn ? "CC_PortraitAbout_FacesOn" : "CC_PortraitAbout_FacesOff").Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            if (m.rows.Any(r => r.condition))
                y += UiKit.Paragraph(0f, y, w, (PortraitEffects.On(true) ? "CC_PortraitAbout_CondsOn" : "CC_PortraitAbout_CondsOff").Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            float tw = 64f, th = 83f, bw = 210f;
            foreach (var r in m.rows)
            {
                float x = r.of != null && r.of != m.rootRec.name ? 24f : 0f;
                Thumb(new Rect(x, y, tw, th), r.file);
                float tx = x + tw + 10f, txw = w - tx - bw - 8f;
                float ty = y;
                UiKit.LabelFit(new Rect(tx, ty, txw, 22f), r.title, UiKit.Ink);
                if (r.shownNow) UiKit.Pill(tx + Mathf.Min(txw - 70f, UiKit.KeptWidth(r.title, GameFont.Small) + 8f), ty + 3f, "CC_PortraitAbout_ShownNow".Translate().Resolve(), UiKit.Good);
                ty += 22f;
                ty += UiKit.Paragraph(tx, ty, txw, r.what, UiKit.Ink, GameFont.Tiny);
                ty += UiKit.Paragraph(tx, ty, txw, r.when, UiKit.Faint, GameFont.Tiny);
                if (r.deletable && UiKit.Button(new Rect(w - bw, y, bw, 28f), "CC_PortraitAbout_DeleteVariant".Translate().Resolve(), ButtonKind.Normal, true, null, GameFont.Tiny))
                {
                    PortraitDelete.ConfirmVersion(r.name);
                    builtAt = -100f;
                }
                y = Mathf.Max(y + th, ty) + 8f;
            }
            if (m.wanted.Count > 0)
            {
                y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_Wanted".Translate().Resolve(), UiKit.Heading, GameFont.Tiny) + 2f;
                foreach (var kv in m.wanted)
                {
                    string title = PortraitLifeMath.FaceWord(kv.Key) != null ? ("CC_PortraitAbout_Face_" + kv.Key).Translate().Resolve()
                        : PortraitLifeMath.ConditionWord(kv.Key) != null ? ("CC_PortraitAbout_Cond_" + kv.Key).Translate().Resolve() : VariantTitle(kv.Key);
                    UiKit.LabelFit(new Rect(0f, y, LabelW + 40f, 22f), title, UiKit.Ink);
                    y += Mathf.Max(22f, UiKit.Paragraph(LabelW + 44f, y + 2f, w - LabelW - 44f, "CC_PortraitAbout_WantedLine".Translate(kv.Value.Replace('/', '\\').Named("FILE")).Resolve(),
                        UiKit.Faint, GameFont.Tiny) + 4f);
                }
            }
            else if (!m.own) y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_WantedOnly".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            return y;
        }

        /// <summary>The corrections, each a button opening its list; greyed with the reason where the picture can't be corrected now.</summary>
        private float Corrections(float w, float y)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_PortraitAbout_Correct".Translate().Resolve());
            y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_CorrectNote".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            if (m.shown != m.root || m.rows.Count > 0)
                y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_CorrectRoot".Translate(FileName(m.rootFolder?.texPath ?? m.rootRec.name).Named("FILE")).Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            if (m.whyNot != null) y += UiKit.Paragraph(0f, y, w, "CC_PortraitAbout_CorrectNo".Translate(m.whyNot.Named("WHY")).Resolve(), UiKit.Bad, GameFont.Tiny) + 4f;
            var r = m.rootRec;
            var l = m.look;
            string name = r.name;
            bool on = m.whyNot == null;
            var buttons = new List<(string label, Action open)>
            {
                (Label("Race", r.races.Count > 0 ? string.Join(", ", r.races) : "human"), () => Kinds(name, r, l, true)),
                (Label("Xenotype", r.xenotypes.Count > 0 ? string.Join(", ", r.xenotypes) : "baseliner"), () => Kinds(name, r, l, false)),
                (Label("Sex", PortraitLibraryMath.Word(r.sex)), () => Pick(name, new[] { PortraitSex.Male, PortraitSex.Female, PortraitSex.Any }.Select(s =>
                    (Mine(PortraitLibraryMath.Word(s), l != null && l.sex == s), r.sex == s, (Action<PortraitRecord>)(x => x.sex = s))))),
                (Label("Age", PortraitLibraryMath.Word(r.age)), () => Pick(name, ((PortraitAge[])Enum.GetValues(typeof(PortraitAge))).Select(a =>
                    (Mine(PortraitLibraryMath.Word(a), l != null && l.age == a), r.age == a, (Action<PortraitRecord>)(x => x.age = a))))),
                (Label("Skin", r.skinSet ? Colour(r.skin, true) : None()), () => Colours(name, r, l, true)),
                (Label("HairColour", r.hairColourSet ? Colour(r.hairColour, false) : None()), () => Colours(name, r, l, false)),
                (Label("Hair", Low(r.hair)), () => Pick(name, Values<PortraitHairStyle>().Select(v =>
                    (Mine(Low(v), l != null && l.hair == v), r.hair == v, (Action<PortraitRecord>)(x => x.hair = v))))),
                (Label("Beard", Low(r.beard)), () => Pick(name, Values<PortraitBeard>().Select(v =>
                    (Mine(Low(v), l != null && l.beard == v), r.beard == v, (Action<PortraitRecord>)(x => x.beard = v))))),
                (Label("Build", Low(r.build)), () => Pick(name, Values<PortraitBuild>().Select(v =>
                    (Mine(Low(v), l != null && l.build == v), r.build == v, (Action<PortraitRecord>)(x => x.build = v))))),
                (Label("Era", r.era == PortraitEra.Unknown ? Any() : Low(r.era)), () => Pick(name, Values<PortraitEra>().Select(v =>
                    (Mine(v == PortraitEra.Unknown ? Any() : Low(v), l != null && l.era == v && v != PortraitEra.Unknown), r.era == v, (Action<PortraitRecord>)(x => x.era = v))))),
                (Label("Dress", r.dress == PortraitDress.Unknown ? Any() : Low(r.dress)), () => Pick(name, Values<PortraitDress>().Select(v =>
                    (Mine(v == PortraitDress.Unknown ? Any() : Low(v), l != null && l.dress == v && v != PortraitDress.Unknown), r.dress == v, (Action<PortraitRecord>)(x => x.dress = v))))),
                (Label("Features", Words(PortraitLibraryMath.Features.Where(r.features.Contains))), () => Toggle(name, PortraitLibraryMath.Features, f => r.features.Contains(f),
                    (x, f, add) =>
                    {
                        if (add) x.features.Add(f);
                        else x.features.Remove(f);
                    })),
                (Label("Marks", MarksWords(r)), () => Toggle(name, PortraitLibraryMath.Marks, mk => PortraitCorrectMath.MarkWords(r).Contains(mk),
                    (x, mk, add) => PortraitCorrectMath.Apply(x, new PortraitFix("marks", (add ? "add:" : "remove:") + mk)))),
                (Label("Side", r.side == PortraitSide.None ? "CC_PortraitAbout_SideNone".Translate().Resolve() : SideWord(r.side)), () => Pick(name, new[] { PortraitSide.Left, PortraitSide.Right, PortraitSide.None }
                    .Select(s => (s == PortraitSide.None ? "CC_PortraitAbout_SideNone".Translate().Resolve() : SideWord(s), r.side == s, (Action<PortraitRecord>)(x => x.side = s))))),
                (Label("Looks", Words(r.looks.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))), () => Looks(name, r)),
                (Label("Role", r.role.Length > 0 ? r.role : "CC_PortraitAbout_RoleNone".Translate().Resolve()), () => Pick(name, new[] { "" }.Concat(PortraitLookMath.Roles).Select(role =>
                    (role.Length > 0 ? role : "CC_PortraitAbout_RoleNone".Translate().Resolve(), r.role == role, (Action<PortraitRecord>)(x => x.role = role))))),
                (Label("Headwear", YesNo(r.headwear)), () => Pick(name, new[] { true, false }.Select(b => (YesNo(b), r.headwear == b, (Action<PortraitRecord>)(x => x.headwear = b)))))
            };
            float bw = (w - 8f) / 2f;
            for (int i = 0; i < buttons.Count; i++)
            {
                var rect = new Rect(i % 2 * (bw + 8f), y + i / 2 * 34f, bw, 30f);
                if (UiKit.Button(rect, buttons[i].label, ButtonKind.Normal, on, on ? null : m.whyNot, GameFont.Tiny) && on) buttons[i].open();
            }
            return y + (buttons.Count + 1) / 2 * 34f;
        }

        private static string Label(string key, string value) => ("CC_PortraitAbout_" + key).Translate().Resolve() + ": " + value;

        /// <summary>A list of one value each (the one it has now marked), the one picked taken at once.</summary>
        private void Pick(string picture, IEnumerable<(string label, bool now, Action<PortraitRecord> set)> items)
        {
            var options = new List<FloatMenuOption>();
            foreach (var it in items)
            {
                var set = it.set;
                options.Add(new FloatMenuOption(it.label + (it.now ? " (" + "CC_PortraitAbout_Now".Translate().Resolve() + ")" : ""), it.now ? (Action)null : () => Take(picture, set)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static T[] Values<T>() => (T[])Enum.GetValues(typeof(T));

        /// <summary>A choice's label, marked where it is the person's own.</summary>
        private string Mine(string label, bool theirs) => theirs ? label + " (" + "CC_PortraitAbout_AsTheirs".Translate(pawn.Named("PAWN")).Resolve() + ")" : label;

        /// <summary>
        /// The races (every humanlike race of the game and its mods) or the xenotypes (every xenotype, and the person's own
        /// of their making), by their def names as the records write them: the person's own first, then none (human,
        /// baseliner), then the rest by their names.
        /// </summary>
        private void Kinds(string picture, PortraitRecord r, PortraitLook l, bool race)
        {
            string none = race ? "human" : "baseliner";
            var now = race ? r.races : r.xenotypes;
            var all = new List<(string name, string label)>();
            if (race)
            {
                foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                    if (d.race != null && d.race.Humanlike)
                        all.Add((d.defName.ToLowerInvariant(), KindLabel(d.LabelCap.ToString(), d.defName)));
            }
            else
            {
                foreach (var d in DefDatabase<XenotypeDef>.AllDefsListForReading)
                    all.Add((d.defName.ToLowerInvariant(), KindLabel(d.LabelCap.ToString(), d.defName)));
            }
            string theirs = l == null ? null : race ? l.race : l.baseliner ? none : l.xenotype;
            if (!string.IsNullOrEmpty(theirs) && all.All(k => k.name != theirs)) all.Add((theirs, theirs));
            if (all.All(k => k.name != none)) all.Add((none, none));
            var items = new List<(string label, bool now, Action<PortraitRecord> set)>();
            foreach (var k in all.OrderBy(k => k.name == theirs ? 0 : k.name == none ? 1 : 2).ThenBy(k => k.label, StringComparer.OrdinalIgnoreCase))
            {
                var kinds = k.name == none ? new List<string>() : new List<string> { k.name };
                bool isNow = new HashSet<string>(now, StringComparer.OrdinalIgnoreCase).SetEquals(kinds);
                if (race) items.Add((Mine(k.label, k.name == theirs), isNow, x => x.races = new List<string>(kinds)));
                else items.Add((Mine(k.label, k.name == theirs), isNow, x => x.xenotypes = new List<string>(kinds)));
            }
            Pick(picture, items);
        }

        /// <summary>A race's or xenotype's name in a list: its label, and its def name where that reads otherwise.</summary>
        private static string KindLabel(string label, string defName) =>
            string.IsNullOrEmpty(label) || string.Equals(label.Replace(" ", ""), defName, StringComparison.OrdinalIgnoreCase) ? defName : label + " (" + defName + ")";

        /// <summary>
        /// The skins (the person's own, a sample of each of the eight tones, the unusual ones) or the hair colours (the
        /// person's own, the natural ones, dyed ones, none), each with its swatch.
        /// </summary>
        private void Colours(string picture, PortraitRecord r, PortraitLook l, bool skin)
        {
            bool set = skin ? r.skinSet : r.hairColourSet;
            string nowHex = set ? PortraitLibraryMath.Hex(skin ? r.skin : r.hairColour) : null;
            var options = new List<FloatMenuOption>();
            void Add(string label, PortraitColour c)
            {
                string hex = PortraitLibraryMath.Hex(c);
                bool now = hex == nowHex;
                // The colour itself, as the record's swatch shows it (its data, not one of the look's).
                var shown = Color.white;
                shown.r = c.r;
                shown.g = c.g;
                shown.b = c.b;
                options.Add(new FloatMenuOption(label + (now ? " (" + "CC_PortraitAbout_Now".Translate().Resolve() + ")" : ""), now ? (Action)null : () => Take(picture, x =>
                {
                    if (skin)
                    {
                        x.skin = c;
                        x.skinSet = true;
                    }
                    else
                    {
                        x.hairColour = c;
                        x.hairColourSet = true;
                    }
                }), BaseContent.WhiteTex, shown));
            }
            if (l != null) Add(Mine(Colour(skin ? l.skin : l.hairColour, skin), true), skin ? l.skin : l.hairColour);
            foreach (var (word, hex) in skin ? PortraitCorrectMath.SkinChoices : PortraitCorrectMath.HairChoices)
                if (PortraitLibraryMath.TryColour(hex, out var c))
                    Add(skin ? word + ": " + Colour(c, true) : word + " (#" + hex + ")", c);
            // No hair colour only for a bald picture (any hair needs its colour in the records).
            if (!skin && r.hair == PortraitHairStyle.Bald)
                options.Add(new FloatMenuOption("CC_PortraitAbout_NoHairColour".Translate().Resolve() + (!set ? " (" + "CC_PortraitAbout_Now".Translate().Resolve() + ")" : ""),
                    !set ? (Action)null : () => Take(picture, x => x.hairColourSet = false)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>A list of words to add or take away, one at a time.</summary>
        private void Toggle(string picture, IEnumerable<string> words, Func<string, bool> has, Action<PortraitRecord, string, bool> change)
        {
            var options = new List<FloatMenuOption>();
            foreach (var w in words)
            {
                string word = w;
                bool now = has(word);
                options.Add(new FloatMenuOption((now ? "CC_PortraitAbout_Remove" : "CC_PortraitAbout_Add").Translate(word.Named("WORD")).Resolve(), () => Take(picture, x => change(x, word, !now))));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>The looks: those it shows to take away, then the person's own it doesn't show, then every other look by its word.</summary>
        private void Looks(string picture, PortraitRecord r)
        {
            var options = new List<FloatMenuOption>();
            foreach (var w in r.looks.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                string word = w;
                options.Add(new FloatMenuOption("CC_PortraitAbout_Remove".Translate(word.Named("WORD")).Resolve(),
                    () => Take(picture, x => PortraitCorrectMath.Apply(x, new PortraitFix("looks", "remove:" + word)))));
            }
            var theirs = m.look?.looks ?? new HashSet<string>();
            var others = PortraitLookMath.Book.All.Select(x => x.word).Where(x => !r.looks.Contains(x)).OrderBy(x => theirs.Contains(x) ? 0 : 1).ThenBy(x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var w in others)
            {
                string word = w;
                options.Add(new FloatMenuOption("CC_PortraitAbout_Add".Translate(word.Named("WORD")).Resolve() + (theirs.Contains(word) ? " (" + "CC_PortraitAbout_TheirLook".Translate(pawn.Named("PAWN")).Resolve() + ")" : ""),
                    () => Take(picture, x => PortraitCorrectMath.Apply(x, new PortraitFix("looks", "add:" + word)))));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Take(string picture, Action<PortraitRecord> change)
        {
            PortraitCorrect.Change(picture, change);
            builtAt = -100f;
        }

        // ---------------- Words ----------------

        private static void Thumb(Rect r, PictureFile f)
        {
            Widgets.DrawBoxSolid(r, UiKit.PortraitBack);
            var tex = f != null ? PortraitStore.Get(f) : null;
            if (tex != null)
            {
                var box = r.ContractedBy(2f);
                float shown = Mathf.Min(box.width, box.height * tex.width / Mathf.Max(1f, tex.height));
                GUI.DrawTexture(box, PortraitStore.Sized(tex, shown), ScaleMode.ScaleToFit, true);
            }
            else UiKit.LabelFit(r, (f == null || PortraitStore.Failed(f) ? "CC_PortraitAbout_NoFile" : "CC_PortraitAbout_Loading").Translate().Resolve(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static string FileName(string texPath) => PortraitDeleteMath.StemOf(texPath ?? "") + (texPath != null && texPath.Contains("/") ? ".png" : "");

        private static string None() => "CC_PortraitAbout_NoneWord".Translate().Resolve();

        private static string Any() => "CC_PortraitAbout_Any".Translate().Resolve();

        private static string YesNo(bool b) => (b ? "CC_PortraitAbout_Yes" : "CC_PortraitAbout_No").Translate().Resolve();

        private static string Low<T>(T v) => v.ToString().Substring(0, 1).ToLowerInvariant() + v.ToString().Substring(1);

        private static string Words(IEnumerable<string> words)
        {
            var list = words?.Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();
            return list.Count == 0 ? None() : string.Join(", ", list);
        }

        /// <summary>A record's kind as it names it: its races and xenotypes (none: a human baseliner).</summary>
        private static string KindWords(PortraitRecord r) =>
            (r.races.Count > 0 ? string.Join(", ", r.races) : "human") + ", " + (r.xenotypes.Count > 0 ? string.Join(", ", r.xenotypes) : "baseliner");

        /// <summary>A colour: its hex, its word, and for a skin its tone of the eight.</summary>
        private static string Colour(PortraitColour c, bool skin)
        {
            string hex = "#" + PortraitLibraryMath.Hex(c);
            if (!skin) return hex + ", " + PortraitFolderMath.HairWord(c);
            return "CC_PortraitAbout_Tone".Translate(hex.Named("HEX"), PortraitFolderMath.SkinWord(c).Named("WORD"), (PortraitLibraryMath.SkinTone(c) + 1).Named("TONE")).Resolve();
        }

        /// <summary>A record's marks with their side ("eyePatch (left), scar").</summary>
        private static string MarksWords(PortraitRecord r) =>
            Words(PortraitCorrectMath.MarkWords(r).Select(mk => PortraitLibraryMath.Sided(mk) && r.side != PortraitSide.None ? mk + " (" + SideWord(r.side) + ")" : mk));

        /// <summary>A person's marks with their sides ("lostEye (left, right), scar").</summary>
        private static string PersonMarks(PortraitLook l) =>
            Words(l.marks.OrderBy(x => x, StringComparer.Ordinal).Select(mk =>
            {
                string word = mk == "lostEye" && l.eyePatch ? "eyePatch" : mk;
                var s = l.SidesOf(mk);
                if (s == PortraitSide.None) return word;
                var sides = new List<string>();
                if ((s & PortraitSide.Left) != 0) sides.Add(SideWord(PortraitSide.Left));
                if ((s & PortraitSide.Right) != 0) sides.Add(SideWord(PortraitSide.Right));
                return word + " (" + string.Join(", ", sides) + ")";
            }));
    }
}
