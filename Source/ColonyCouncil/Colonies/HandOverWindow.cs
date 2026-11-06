using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The hand-over window (docs/design/stage3-many-colonies.md, section 2, "What the player does"): who stays and who
    /// goes, each grown-up's wish shown with why (release 1's list: decision 2A); the head and the head's second, by office
    /// or standing, never by blood (the player may pick others); the people's name by the age's word, renameable; the
    /// terms (decision 9A: free, a defence pact or a trade pact, never fealty); what the colony's prisoners and slaves do;
    /// what stays with the place; the checks (three grown-ups stay, someone of the player's elsewhere, no fight and no
    /// banishment under way); the plain word that a save holding the town then needs Rimfolk (decision 6B); and where those
    /// who go go (another home, a fresh start in the same act, or a caravan the player forms). "Put it to the meeting"
    /// sets it going: the colony's meeting votes first.
    /// </summary>
    public class Dialog_HandOver : Window
    {
        private readonly Map home;
        /// <summary>A fresh start in the same act (the founding window's third choice): its party goes, to its place.</summary>
        private readonly FoundingPlan founding;
        private readonly HashSet<Pawn> going = new HashSet<Pawn>();
        private List<FounderRow> rows = new List<FounderRow>();
        private float rowsAt = -10f;
        private bool dirty = true;
        private Pawn pickedHead, pickedSecond;
        private string peopleName;
        private HandOverTerms terms = HandOverTerms.Free;
        private CaptiveFate captives = CaptiveFate.Free;
        private HandOverWhere where = HandOverWhere.Caravan;
        private int toHome = HomesMath.None;
        private Vector2 scroll, scrollRight;
        private float listH = 400f, rightH = 900f;
        private readonly WorldAge age;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1040f, UI.screenWidth - 40f), Mathf.Min(800f, UI.screenHeight - 40f));

        public Dialog_HandOver(Map home, FoundingPlan founding = null)
        {
            this.home = home;
            this.founding = founding;
            age = Ages.Of(home);
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            peopleName = HandOvers.DefaultName(age, Homes.Name(home));
            if (founding != null)
            {
                where = HandOverWhere.Founding;
                foreach (var p in founding.party) going.Add(p);
            }
            else
            {
                var other = OtherHomes().FirstOrDefault();
                if (other != null)
                {
                    where = HandOverWhere.Home;
                    toHome = Homes.KeyOf(other);
                }
            }
        }

        private List<Map> OtherHomes() => Homes.All.Where(m => m != home && !MapFacts.InVacuum(m) && m.Parent != null).ToList();

        private void Refresh()
        {
            if (!dirty && Time.realtimeSinceStartup - rowsAt < 1f) return;
            rows = Foundings.Rows(home, going);
            rowsAt = Time.realtimeSinceStartup;
            dirty = false;
            going.RemoveWhere(p => rows.All(r => r.pawn != p));
            // Those away from home now, or a guest of another people's, can't stay: they go with those who go.
            foreach (var r in rows.Where(r => r.cant != null)) going.Add(r.pawn);
        }

        private List<Pawn> GrownStaying => rows.Where(r => r.grown && !going.Contains(r.pawn)).Select(r => r.pawn).ToList();

        private Pawn Head => HandOvers.HeadOf(home, GrownStaying, GrownStaying.Contains(pickedHead) ? pickedHead : null);

        private Pawn Second
        {
            get
            {
                var grown = GrownStaying;
                var head = Head;
                return HandOvers.SecondOf(home, grown, head, grown.Contains(pickedSecond) && pickedSecond != head ? pickedSecond : null);
            }
        }

        private void Toggle(FounderRow r)
        {
            if (founding != null || r.cant != null) return;
            if (going.Contains(r.pawn))
            {
                going.Remove(r.pawn);
                foreach (var c in rows.Where(c => !c.grown && Foundings.Kin(r.pawn, c.pawn) && !rows.Any(o => o.grown && o.pawn != r.pawn && going.Contains(o.pawn) && Foundings.Kin(o.pawn, c.pawn))))
                    going.Remove(c.pawn);
            }
            else
            {
                going.Add(r.pawn);
                // Children follow their parents unless moved.
                if (r.grown)
                    foreach (var c in rows.Where(c => !c.grown && c.cant == null && Foundings.Kin(r.pawn, c.pawn)))
                        going.Add(c.pawn);
            }
            dirty = true;
        }

        private bool Newcomers => founding != null && founding.newcomers;

        /// <summary>Why it can't be set going as it stands (in words), or null.</summary>
        private string WhyNot()
        {
            string no = HandOvers.WhyNot(home, rows, going, Newcomers);
            if (no != null) return no;
            string name = HandOverMath.NameWhyNot(peopleName, Find.FactionManager.AllFactionsListForReading.Select(f => f.Name));
            if (name != null) return ("CC_HandOver_Name_" + name).Translate(HandOverMath.MaxNameLength.Named("COUNT")).Resolve();
            if (where == HandOverWhere.Home && Homes.MapOf(toHome) == null) return "CC_HandOver_No_WhereHome".Translate();
            if (terms != HandOverTerms.Free && TermsWhyNot() != null) return TermsWhyNot();
            return null;
        }

        /// <summary>A pact needs a home of the colony's left to be bound to, and the pacts on (Diplomacy).</summary>
        private string TermsWhyNot()
        {
            if (!Pacts.On) return "CC_HandOver_Terms_Off".Translate();
            if (OtherHomes().Count == 0 && founding == null) return "CC_HandOver_Terms_NoHome".Translate();
            return null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (home == null || !Homes.IsHome(home))
            {
                Close();
                return;
            }
            Refresh();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 34f), "CC_HandOver_Title".Translate(Homes.Name(home).Named("HOME")).Resolve());
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(inRect.x, inRect.y + 32f, inRect.width, 22f), "CC_HandOver_Subtitle".Translate(HandOvers.Form(null).word.Named("FORM")).Resolve(), UiKit.Faint);
            float top = inRect.y + 60f;
            const float bottomH = 44f;
            float leftW = Mathf.Clamp(inRect.width * 0.44f, 300f, 460f);
            var left = new Rect(inRect.x, top, leftW, inRect.height - (top - inRect.y) - bottomH);
            var right = new Rect(left.xMax + 14f, top, inRect.width - leftW - 14f, left.height);
            try
            {
                DrawWho(left);
                DrawRight(right);
                DrawBottom(new Rect(inRect.x, inRect.yMax - bottomH + 8f, inRect.width, bottomH - 8f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawWho(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(listH, rect.height));
            Widgets.BeginScrollView(rect, ref scroll, view);
            float y = 0f, w = view.width;
            int staying = rows.Count(r => !going.Contains(r.pawn));
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_Who".Translate(Homes.Name(home).Named("HOME")).Resolve(), Icons.People, null,
                "CC_HandOver_WhoCount".Translate(staying.Named("STAY"), going.Count.Named("GO")).Resolve()) + 2f;
            y += UiKit.Paragraph(4f, y, w - 8f, (founding != null ? "CC_HandOver_WhoLineFounding" : "CC_HandOver_WhoLine").Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            var head = Head;
            var second = Second;
            foreach (var r in rows)
            {
                var row = new Rect(0f, y, w, 40f);
                bool goes = going.Contains(r.pawn);
                if (!goes) Widgets.DrawBoxSolid(row, UiKit.WithAlpha(UiKit.Gold, 0.08f));
                Widgets.DrawHighlightIfMouseover(row);
                UiKit.Portrait(new Rect(row.x + 28f, row.y + 3f, 34f, 34f), r.pawn, goes ? UiKit.Dim : UiKit.Gold);
                Widgets.CheckboxDraw(row.x + 2f, row.y + 8f, !goes, r.cant != null || founding != null, 24f);
                string name = r.pawn.LabelShort + (r.grown ? "" : " " + "CC_Found_Child".Translate().Resolve());
                if (r.pawn == head) name += " " + "CC_HandOver_IsHead".Translate().Resolve();
                else if (r.pawn == second) name += " " + "CC_HandOver_IsSecond".Translate().Resolve();
                UiKit.LabelFit(new Rect(row.x + 68f, row.y + 1f, w - 190f, 20f), name, Color.white);
                string sub = r.cant != null ? r.cant + " " + "CC_HandOver_CantStay".Translate().Resolve()
                    : r.grown ? string.Join(", ", r.reasons.Take(2)) : "CC_Found_ChildFollows".Translate().Resolve();
                UiKit.LabelFit(new Rect(row.x + 68f, row.y + 20f, w - 190f, 18f), sub, UiKit.Faint, GameFont.Tiny);
                var chip = new Rect(row.xMax - 118f, row.y + 9f, 114f, 22f);
                if (r.grown && r.cant == null)
                {
                    bool against = FoundingMath.AgainstWish(r.wish, goes);
                    Color c = r.wish == FoundingWish.Go ? UiKit.Good : r.wish == FoundingWish.Stay ? UiKit.Warn : UiKit.Faint;
                    UiKit.LabelFit(chip, (goes ? "CC_HandOver_Goes" : "CC_HandOver_Stays").Translate().Resolve() + " · " + (against ? "CC_Found_Against" : "CC_Found_Wish_" + r.wish).Translate().Resolve(),
                        against ? UiKit.Warn : c, GameFont.Tiny, TextAnchor.MiddleRight);
                    string tip = ("CC_Found_Wish_" + r.wish).Translate().Resolve().CapitalizeFirst() + (r.reasons.Count > 0 ? ":\n" + string.Join("\n", r.reasons.Select(x => "  - " + x)) : ".");
                    if (against) tip += "\n\n" + (goes ? "CC_HandOver_AgainstSent" : "CC_HandOver_AgainstKept").Translate(r.pawn.Named("PAWN")).Resolve();
                    TooltipHandler.TipRegion(row, tip);
                }
                else UiKit.LabelFit(chip, (goes ? "CC_HandOver_Goes" : "CC_HandOver_Stays").Translate().Resolve(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                // A click on the row moves them between staying and going; nobody is moved but by the player.
                if (r.cant == null && founding == null && Widgets.ButtonInvisible(row)) Toggle(r);
                y += 42f;
            }
            listH = y + 10f;
            Widgets.EndScrollView();
        }

        private void DrawRight(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rightH, rect.height));
            Widgets.BeginScrollView(rect, ref scrollRight, view);
            float y = 0f, w = view.width;
            y = DrawPeople(y, w);
            y = DrawTerms(y, w);
            y = DrawWhere(y, w);
            y = DrawStays(y, w);
            y = DrawChecks(y, w);
            rightH = y + 10f;
            Widgets.EndScrollView();
        }

        /// <summary>The head, the head's second and the people's name.</summary>
        private float DrawPeople(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_PeopleHead".Translate().Resolve(), Icons.Office) + 2f;
            var grown = GrownStaying;
            var head = Head;
            var second = Second;
            float labelW = Mathf.Min(150f, w * 0.35f);
            UiKit.LabelFit(new Rect(4f, y, labelW, 26f), "CC_HandOver_TheHead".Translate(HeadTitle().Named("TITLE")).Resolve(), Color.white);
            if (Widgets.ButtonText(new Rect(labelW + 8f, y, w - labelW - 12f, 26f), head?.LabelShort ?? "CC_HandOver_Nobody".Translate().Resolve(), true, true, grown.Count > 0))
                Find.WindowStack.Add(new FloatMenu(grown.Select(p => new FloatMenuOption(p.LabelShort, () =>
                {
                    pickedHead = p;
                    if (pickedSecond == p) pickedSecond = null;
                })).ToList()));
            y += 28f;
            UiKit.LabelFit(new Rect(4f, y, labelW, 26f), "CC_HandOver_TheSecond".Translate(HandOvers.SecondTitle(age).Named("TITLE")).Resolve(), Color.white);
            if (Widgets.ButtonText(new Rect(labelW + 8f, y, w - labelW - 12f, 26f), second?.LabelShort ?? "CC_HandOver_Nobody".Translate().Resolve(), true, true, grown.Count > 1))
                Find.WindowStack.Add(new FloatMenu(grown.Where(p => p != head).Select(p => new FloatMenuOption(p.LabelShort, () => pickedSecond = p)).ToList()));
            y += 28f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_HeadLine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 6f;
            UiKit.LabelFit(new Rect(4f, y, labelW, 26f), "CC_HandOver_TheName".Translate().Resolve(), Color.white);
            peopleName = Widgets.TextField(new Rect(labelW + 8f, y, w - labelW - 12f, 26f), peopleName ?? "", HandOverMath.MaxNameLength + 10);
            y += 28f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_NameLine".Translate(KindLabel().Named("KIND")).Resolve(), UiKit.Faint, GameFont.Tiny) + 10f;
            return y;
        }

        private string HeadTitle() => HandOverPeoples.DefFor(age)?.leaderTitle ?? "CC_HandOver_HeadWord".Translate().Resolve();

        private string KindLabel() => HandOverPeoples.DefFor(age)?.label ?? "?";

        /// <summary>The terms (decision 9A): free, or a pact for a year, renewed by letter.</summary>
        private float DrawTerms(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_TermsHead".Translate().Resolve(), Icons.Ways) + 2f;
            string termsNo = TermsWhyNot();
            foreach (HandOverTerms t in Enum.GetValues(typeof(HandOverTerms)))
            {
                bool can = t == HandOverTerms.Free || termsNo == null;
                var box = new Rect(4f, y, w - 8f, 24f);
                if (Widgets.RadioButtonLabeled(box, ("CC_HandOver_Terms_" + t).Translate(), terms == t, !can) && can) terms = t;
                if (!can) TooltipHandler.TipRegion(box, termsNo);
                y += 26f;
            }
            y += UiKit.Paragraph(4f, y, w - 8f, ("CC_HandOver_TermsLine_" + terms).Translate().Resolve() + " " + "CC_HandOver_NoFealty".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 10f;
            return y;
        }

        /// <summary>Where those who go go, and the colony's prisoners and slaves.</summary>
        private float DrawWhere(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_WhereHead".Translate().Resolve(), Icons.Away) + 2f;
            if (founding != null)
            {
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_WhereFounding".Translate(founding.place.Named("PLACE"), ("CC_Found_Means_" + founding.means).Translate().Named("MEANS")).Resolve(),
                    Color.white) + 8f;
            }
            else
            {
                foreach (var m in OtherHomes())
                {
                    if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), "CC_HandOver_WhereHome".Translate(Homes.Name(m).Named("HOME")).Resolve(),
                            where == HandOverWhere.Home && toHome == Homes.KeyOf(m)))
                    {
                        where = HandOverWhere.Home;
                        toHome = Homes.KeyOf(m);
                    }
                    y += 26f;
                }
                if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), "CC_HandOver_WhereCaravan".Translate(), where == HandOverWhere.Caravan)) where = HandOverWhere.Caravan;
                y += 26f;
                y += UiKit.Paragraph(4f, y, w - 8f, ("CC_HandOver_WhereLine_" + where).Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
                if (Foundings.On)
                {
                    y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_WhereFoundLine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
                    var b = new Rect(4f, y, Mathf.Min(260f, w - 8f), 26f);
                    string why = Foundings.WhyNot(home);
                    if (Widgets.ButtonText(b, "CC_HandOver_FoundInstead".Translate(), true, true, why == null) && why == null)
                    {
                        Close();
                        FoundingPlace.Pick(home, true);
                    }
                    if (why != null) TooltipHandler.TipRegion(b, why);
                    y += 30f;
                }
            }
            int captives = home.mapPawns.PrisonersOfColony.Count + home.mapPawns.SlavesOfColonySpawned.Count;
            if (captives > 0)
            {
                y += 4f;
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_CaptivesHead".Translate(captives.Named("COUNT")).Resolve(), Color.white) + 2f;
                foreach (CaptiveFate c in Enum.GetValues(typeof(CaptiveFate)))
                {
                    if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), ("CC_HandOver_Captives_" + c).Translate(), this.captives == c)) this.captives = c;
                    y += 26f;
                }
            }
            return y + 8f;
        }

        /// <summary>What stays with the place, listed as the game's own abandon would.</summary>
        private float DrawStays(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_StaysHead".Translate().Resolve(), Icons.Base) + 2f;
            int buildings = home.listerBuildings.allBuildingsColonist.Count;
            int fields = home.zoneManager.AllZones.Count(z => z is Zone_Growing);
            int goods = Mathf.RoundToInt(home.wealthWatcher.WealthItems);
            int animals = home.mapPawns.SpawnedColonyAnimals.Count;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_StaysLine".Translate(buildings.Named("BUILDINGS"), fields.Named("FIELDS"), goods.ToString("#,0").Named("GOODS"),
                animals.Named("ANIMALS")).Resolve(), Color.white) + 2f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_StaysMore".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 10f;
            // release 2b: the colony's layout kept, so the town's map, when it is made again, is the colony as it was left.
            return y;
        }

        /// <summary>The checks, the meeting's vote and the plain word on needing Rimfolk (decision 6B).</summary>
        private float DrawChecks(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_HandOver_ChecksHead".Translate().Resolve(), Icons.Warning) + 2f;
            int grownStaying = rows.Count(r => r.grown && !going.Contains(r.pawn));
            int grownGoing = rows.Count(r => r.grown && going.Contains(r.pawn));
            bool elsewhere = HandOverMath.PlayersElsewhere(grownGoing, HandOvers.ColonistsElsewhere(home, going), Newcomers);
            void Check(bool ok, string text)
            {
                y += UiKit.Paragraph(4f, y, w - 8f, (ok ? "CC_HandOver_CheckOk" : "CC_HandOver_CheckNo").Translate(text.Named("TEXT")).Resolve(), ok ? UiKit.Good : UiKit.Warn, GameFont.Tiny) + 2f;
            }
            Check(grownStaying >= HandOverMath.MinGrownStay, "CC_HandOver_Check_Three".Translate(HandOverMath.MinGrownStay.Named("COUNT"), grownStaying.Named("NOW")).Resolve());
            Check(elsewhere, "CC_HandOver_Check_Elsewhere".Translate().Resolve());
            Check(!HandOvers.Fight(home), "CC_HandOver_Check_Fight".Translate(Homes.Name(home).Named("HOME")).Resolve());
            Check(!HandOvers.Banishing(home), "CC_HandOver_Check_Banishment".Translate().Resolve());
            if (ModsConfig.OdysseyActive)
            {
                if (HandOvers.Anchor(home)) Check(false, "CC_HandOver_Check_Anchor".Translate().Resolve());
                if (HandOvers.Engine(home)) y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_Check_Engine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
            }
            y += 6f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_VoteLine".Translate(Mathf.RoundToInt(-HandOverMath.TrustAgainstVote).Named("TRUST")).Resolve(), UiKit.Faint, GameFont.Tiny) + 6f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_HandOver_NeedsRimfolk".Translate(KindLabel().Named("KIND")).Resolve(), UiKit.Warn, GameFont.Tiny) + 4f;
            return y;
        }

        private void DrawBottom(Rect rect)
        {
            string why = WhyNot();
            var confirm = new Rect(rect.xMax - 240f, rect.y, 240f, rect.height);
            if (why != null)
            {
                TooltipHandler.TipRegion(confirm, why);
                UiKit.LabelFit(new Rect(rect.x, rect.y, rect.width - 440f, rect.height), why, UiKit.Warn, GameFont.Tiny);
            }
            if (Widgets.ButtonText(confirm, "CC_HandOver_Confirm".Translate(), true, true, why == null) && why == null)
            {
                Confirm();
                return;
            }
            if (Widgets.ButtonText(new Rect(confirm.x - 170f, rect.y, 160f, rect.height), "CloseButton".Translate())) Close();
        }

        private void Confirm()
        {
            var goers = rows.Where(r => going.Contains(r.pawn)).Select(r => r.pawn).ToList();
            var stayers = rows.Where(r => !going.Contains(r.pawn)).Select(r => r.pawn).ToList();
            var plan = new HandOverPlan
            {
                home = Homes.KeyOf(home), homeName = Homes.Name(home), age = age, peopleName = peopleName.Trim(), terms = terms, head = Head, second = Second,
                going = goers, staying = stayers, where = where, toHome = where == HandOverWhere.Home ? toHome : HomesMath.None, captives = captives,
                sentAgainst = rows.Where(r => r.grown && r.cant == null && going.Contains(r.pawn) && FoundingMath.AgainstWish(r.wish, true)).Select(r => r.pawn).ToList(),
                keptAgainst = rows.Where(r => r.grown && r.cant == null && !going.Contains(r.pawn) && FoundingMath.AgainstWish(r.wish, false)).Select(r => r.pawn).ToList()
            };
            if (founding != null)
            {
                // The founding sets out once the ceremony is over; its party is those who go, and the hand-over gives their moods.
                founding.party = goers;
                founding.sentAgainst = new List<Pawn>();
                founding.keptAgainst = new List<Pawn>();
                founding.keepOld = false;
                founding.makeSeat = false;
                founding.handOver = true;
                plan.founding = founding;
            }
            Close();
            HandOvers.Begin(plan);
        }
    }
}
