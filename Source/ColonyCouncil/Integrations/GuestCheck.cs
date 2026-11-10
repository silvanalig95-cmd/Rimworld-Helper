using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// On Hospitality's guest tab, under its own lines (the setting "Recruiting, spelled out"): whether this guest can be
    /// recruited and how (GuestCheckMath.Verdict), and a checklist of what it takes to have them join of their own will,
    /// each condition met or not: their people keeping goodwill, a death acidifier (which keeps them from joining of their
    /// own will unless they're related to someone of the colony, and goes off when they're recruited), a title (no
    /// recruiting by force), friends here as Hospitality counts them (royal friends' ranks for the titled) with what their
    /// ties at home ask for more, and with Rimfolk's "Guests who would stay" a reason to stay, room and what they want of a
    /// home. The tab grows to fit, for this and for the guest's helping out above it (GuestHelpTab.cs, drawn from the same
    /// patch). Hospitality's facts are read by name.
    /// </summary>
    public static class GuestCheck
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.guestCheck ?? true) && People.ModActive(Guests.Mod);

        /// <summary>A line of the checklist: met (1), not met (-1) or said (0), and how far in.</summary>
        public struct Line
        {
            public string text;
            public int mark, indent;
            public Color color;
        }

        private class Check
        {
            public int tick;
            public GuestCheckMath.Verdict verdict;
            public string verdictText;
            public Color verdictColor;
            public readonly List<Line> lines = new List<Line>();
        }

        private static readonly Dictionary<Pawn, Check> cache = new Dictionary<Pawn, Check>();

        public static void Reset() => cache.Clear();

        private static Check Of(Pawn g)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(g, out var c) && now - c.tick < 250) return c;
            if (cache.Count > 60) cache.Clear();
            c = new Check { tick = now };
            try
            {
                Build(g, c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's recruiting checklist", e);
                c.lines.Clear();
                c.verdictText = null;
            }
            cache[g] = c;
            return c;
        }

        // ---------------- Hospitality's facts, by name ----------------

        private static MethodInfo minOpinion, seniorityFriends, seniorityNeed, seniorityEnemies;
        private static FieldInfo arrived;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            var gu = GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.GuestUtility");
            if (gu != null)
            {
                minOpinion = AccessTools.Method(gu, "GetMinRecruitOpinion", new[] { typeof(Pawn) });
                seniorityFriends = AccessTools.Method(gu, "GetFriendsSeniorityInColony", new[] { typeof(Pawn) });
                seniorityNeed = AccessTools.Method(gu, "RoyalFriendsSeniorityRequired", new[] { typeof(Pawn) });
                seniorityEnemies = AccessTools.Method(gu, "GetRoyalEnemiesSeniorityInColony", new[] { typeof(Pawn) });
            }
            var comp = GenTypes.GetTypeInAnyAssembly("Hospitality.CompGuest");
            if (comp != null) arrived = AccessTools.Field(comp, "arrived");
        }

        private static int Call(MethodInfo m, Pawn g, int fallback)
        {
            if (m == null) return fallback;
            try
            {
                return Convert.ToInt32(m.Invoke(null, new object[] { g }));
            }
            catch
            {
                return fallback;
            }
        }

        private static bool Settled(Pawn g)
        {
            if (arrived == null) return true;
            var comp = g.AllComps?.FirstOrDefault(c => c != null && arrived.DeclaringType.IsInstanceOfType(c));
            return comp == null || arrived.GetValue(comp) is bool b && b;
        }

        private static readonly HediffDef Acidifier = DefDatabase<HediffDef>.GetNamedSilentFail("DeathAcidifier");

        /// <summary>Related to someone of the colony (any relation, anywhere), as Hospitality has it for the acidifier.</summary>
        private static bool Related(Pawn g) =>
            PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.Any(p => p?.relations != null && p.GetRelations(g).Any());

        // ---------------- The checklist ----------------

        private static void Add(Check c, string text, int mark, int indent = 0) =>
            c.lines.Add(new Line { text = text, mark = mark, indent = indent, color = mark > 0 ? UiKit.Good : mark < 0 ? UiKit.Bad : UiKit.Faint });

        private static void Build(Pawn g, Check c)
        {
            Look();
            var faction = g.Faction;
            string factionName = faction?.Name ?? "?";
            bool goodwill = faction == null || faction.HasGoodwill;
            bool acid = Acidifier != null && g.health?.hediffSet != null && g.health.hediffSet.HasHediff(Acidifier);
            bool related = acid && Related(g);
            var title = g.royalty?.MostSeniorTitle;
            bool titled = title != null;
            bool settled = Settled(g) && !g.InMentalState;
            var will = GuestWill.On ? GuestWill.Of(g) : null;

            // Friends here: Hospitality's count, and what Rimfolk's ties ask for more; royal friends' ranks for the titled.
            int have, need;
            if (titled)
            {
                have = Call(seniorityFriends, g, -1);
                int baseNeed = Call(seniorityNeed, g, -1), enemies = Call(seniorityEnemies, g, 0);
                need = baseNeed < 0 ? -1 : baseNeed + enemies;
            }
            else
            {
                GuestWill.HospitalityFriends(g, out have, out int required);
                need = required < 0 ? -1 : required + (will?.Extra ?? 0);
            }
            bool counted = have >= 0 && need >= 0;
            bool enough = !counted || have >= need;
            bool willing = will == null || will.Willing;

            c.verdict = GuestCheckMath.Of(goodwill, acid, related, titled, settled, enough, willing);
            var who = g.Named("PAWN");
            switch (c.verdict)
            {
                case GuestCheckMath.Verdict.Ready:
                    c.verdictText = "CC_GuestCheck_Ready".Translate(who).Resolve();
                    c.verdictColor = UiKit.Good;
                    break;
                case GuestCheckMath.Verdict.ForceOnly:
                    c.verdictText = (acid ? "CC_GuestCheck_ForceOnlyAcid" : "CC_GuestCheck_ForceOnly").Translate(who).Resolve();
                    c.verdictColor = UiKit.Warn;
                    break;
                case GuestCheckMath.Verdict.NotYet:
                    c.verdictText = "CC_GuestCheck_NotYet".Translate(who, title.Label.Named("TITLE")).Resolve();
                    c.verdictColor = UiKit.Warn;
                    break;
                case GuestCheckMath.Verdict.NotNow:
                    c.verdictText = "CC_GuestCheck_NotNow".Translate(who).Resolve();
                    c.verdictColor = UiKit.Warn;
                    break;
                case GuestCheckMath.Verdict.Never:
                    c.verdictText = "CC_GuestCheck_Never".Translate(who, factionName.Named("FACTION")).Resolve();
                    c.verdictColor = UiKit.Bad;
                    return;
                case GuestCheckMath.Verdict.NeverUnlessRelated:
                    c.verdictText = "CC_GuestCheck_NeverRelated".Translate(who, title.Label.Named("TITLE")).Resolve();
                    c.verdictColor = UiKit.Bad;
                    break;
            }

            // What it takes to join of their own will.
            if (!g.InMentalState && !Settled(g)) Add(c, "CC_GuestCheck_Settling".Translate(who).Resolve(), -1);
            if (g.InMentalState) Add(c, "CC_GuestCheck_Calm".Translate(who).Resolve(), -1);
            if (acid) Add(c, (related ? "CC_GuestCheck_AcidRelated" : "CC_GuestCheck_Acid").Translate(who).Resolve(), related ? 1 : -1);
            if (counted)
            {
                if (titled)
                {
                    Add(c, "CC_GuestCheck_Royal".Translate((have / 100).Named("HAVE"), (need / 100).Named("NEED"), who).Resolve(), enough ? 1 : -1);
                }
                else
                {
                    int opinion = Call(minOpinion, g, -1);
                    string f = "CC_GuestCheck_Friends".Translate(have.Named("HAVE"), need.Named("NEED"), who).Resolve();
                    if (opinion >= 0) f += " " + "CC_GuestCheck_FriendsOpinion".Translate(opinion.Named("OPINION"), who).Resolve();
                    Add(c, f, enough ? 1 : -1);
                    if (will != null && will.ties.Count > 0)
                    {
                        foreach (var tie in will.ties)
                            Add(c, "CC_GuestCheck_Tie".Translate(GuestWillMath.TieWeight(tie.tie).Named("COUNT"), tie.text.Named("TIE")).Resolve(), 0, 1);
                        int off = will.TieWeight - will.Extra;
                        if (off > 0) Add(c, "CC_GuestCheck_TieOff".Translate(off.Named("OFF")).Resolve(), 0, 1);
                    }
                }
            }
            if (will != null)
            {
                if (will.reasons.Count > 0) Add(c, "CC_GuestCheck_Reason".Translate(string.Join("; ", will.reasons).Named("REASONS")).Resolve(), 1);
                else Add(c, "CC_GuestCheck_NoReason".Translate(who).Resolve(), -1);
                if (will.noRoom.Count == 0) Add(c, "CC_GuestCheck_Room".Translate(GuestWillMath.FoodDays.ToString("0").Named("DAYS")).Resolve(), 1);
                else
                    foreach (var r in will.noRoom) Add(c, "CC_GuestCheck_NoRoom".Translate(r.Named("WHAT")).Resolve(), -1);
                foreach (var x in will.wants) Add(c, "CC_GuestCheck_Want".Translate(GuestWill.WantText(x.want, x.now).Named("WANT"), who).Resolve(), x.met ? 1 : -1);
            }

            // By force, and what the acidifier does.
            if (GuestCheckMath.ForceOpen(goodwill, titled)) Add(c, "CC_GuestCheck_Force".Translate(factionName.Named("FACTION")).Resolve(), 0);
            else if (titled) Add(c, "CC_GuestCheck_NoForce".Translate(title.Label.Named("TITLE"), who).Resolve(), 0);
            if (acid) Add(c, "CC_GuestCheck_AcidGoesOff".Translate(who).Resolve(), 0);
        }

        // ---------------- Drawn on the guest tab ----------------

        private static Pawn sizedFor;
        private static float wanted, baseHeight = -1f;
        private static Vector2 scroll;
        private static Pawn scrolledFor;

        /// <summary>
        /// The tallest the guest tab may grow: what's left of the screen above the inspect pane and its row of tab buttons,
        /// so its top never goes off screen (the player's report: a long checklist did). The pane's top is the tab's own
        /// (InspectTabBase.PaneTopY), which follows a taller pane such as RimHUD's; the game's usual pane when unread.
        /// </summary>
        internal static float MaxTabHeight
        {
            get
            {
                float top = paneTop > 0f ? paneTop : UI.screenHeight - 35f - InspectPaneUtility.PaneHeight;
                return Mathf.Max(300f, top - InspectPaneUtility.TabHeight - 12f);
            }
        }

        /// <summary>The least room worth a scrolling area for the checklist; less than that, it opens in its own window.</summary>
        private const float MinScroll = 100f;

        /// <summary>The whole checklist for one guest in a window of its own, for when the tab has no room for it. In Rimfolk's frame (key "GuestCheck").</summary>
        public class Window_GuestCheck : FramedWindow
        {
            private readonly Pawn guest;
            private Vector2 scrollPos;

            public override Vector2 InitialSize => new Vector2(520f, Mathf.Min(620f, UI.screenHeight - 80f));

            protected override string FrameTitle => "CC_GuestCheck_Title".Translate(guest.Named("PAWN")).Resolve();

            protected override Texture2D FrameIcon => Icons.Visitor;

            public Window_GuestCheck(Pawn guest) : base("GuestCheck")
            {
                this.guest = guest;
                doCloseX = true;
                draggable = true;
                closeOnClickedOutside = false;
                absorbInputAroundWindow = false;
                preventCameraMotion = false;
            }

            protected override void DoFramedContents(Rect inRect)
            {
                if (guest == null || guest.Destroyed || guest.Dead)
                {
                    Close();
                    return;
                }
                var c = Of(guest);
                float top = TitleTop(inRect, FrameTitle, 30f, 40f);
                if (c.verdictText.NullOrEmpty()) return;
                var outer = new Rect(0f, top, inRect.width, inRect.height - top);
                var view = new Rect(0f, 0f, outer.width - 16f, BodyHeight(c, guest, outer.width - 16f));
                Widgets.BeginScrollView(outer, ref scrollPos, view);
                DrawBody(view, c, guest);
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
            }
        }

        /// <summary>The inspect pane's top as the guest tab saw it last (-1 until read).</summary>
        private static float paneTop = -1f;
        private static readonly MethodInfo PaneTopGetter = AccessTools.PropertyGetter(typeof(InspectTabBase), "PaneTopY");

        /// <summary>The checklist's own height at this width (the verdict and the lines), as <see cref="DrawBody"/> draws it.</summary>
        private static float BodyHeight(Check c, Pawn g, float w)
        {
            var font = Text.Font;
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(c.verdictText, w);
            if (c.lines.Count > 0)
            {
                h += 4f;
                Text.Font = GameFont.Tiny;
                if (c.lines.Any(l => l.mark != 0)) h += Text.LineHeight;
                foreach (var line in c.lines)
                {
                    float x = line.indent * 16f + (line.mark != 0 ? 18f : 0f);
                    h += Mathf.Max(16f, Text.CalcHeight(line.text, w - x));
                }
            }
            Text.Font = font;
            return h;
        }

        /// <summary>The verdict and the lines, from the top of <paramref name="r"/>.</summary>
        private static void DrawBody(Rect r, Check c, Pawn g)
        {
            float w = r.width;
            float y = r.y;
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(c.verdictText, w);
            GUI.color = c.verdictColor;
            Widgets.Label(new Rect(r.x, y, w, h), c.verdictText);
            GUI.color = Color.white;
            y += h;
            if (c.lines.Count == 0) return;
            y += 4f;
            Text.Font = GameFont.Tiny;
            if (c.lines.Any(l => l.mark != 0))
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(r.x, y, w, Text.LineHeight), "CC_GuestCheck_Takes".Translate(g.Named("PAWN")));
                GUI.color = Color.white;
                y += Text.LineHeight;
            }
            foreach (var line in c.lines)
            {
                float x = line.indent * 16f + (line.mark != 0 ? 18f : 0f);
                float lh = Mathf.Max(16f, Text.CalcHeight(line.text, w - x));
                if (line.mark != 0)
                    GUI.DrawTexture(new Rect(r.x + line.indent * 16f, y + 1f, 14f, 14f), line.mark > 0 ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                GUI.color = line.color;
                Widgets.Label(new Rect(r.x + x, y, w - x, lh), line.text);
                GUI.color = Color.white;
                y += lh;
            }
        }

        /// <summary>
        /// Below Hospitality's own lines, in its listing. The tab grows to fit, up to <see cref="MaxTabHeight"/>; a
        /// checklist longer than that scrolls in its own area (mouse wheel or the bar at its side), and with hardly any
        /// room left it opens in a window of its own from the button beside its title.
        /// </summary>
        internal static void Draw(Listing_Standard listing, Pawn g)
        {
            if (listing == null || g == null || !On) return;
            var c = Of(g);
            if (c.verdictText.NullOrEmpty()) return;
            var font = Text.Font;
            var anchor = Text.Anchor;
            // One column: past the bottom the listing would start a second one out of sight, and its height would read
            // as that column's, so the tab would never grow to show the rest.
            bool oneColumn = listing.maxOneColumn;
            listing.maxOneColumn = true;
            listing.GapLine(8f);
            Text.Font = GameFont.Small;
            var head = listing.GetRect(Text.LineHeight);
            float w = listing.ColumnWidth;
            float body = BodyHeight(c, g, w);
            // The listing starts 30 in and ends 10 short of the bottom: room left in the tallest tab for the checklist.
            float room = MaxTabHeight - 44f - listing.CurHeight;
            bool fits = body <= room;
            // Not all of it fits: a button beside the title opens the whole checklist in a window of its own.
            float buttonW = fits ? 0f : Mathf.Min(170f, head.width * 0.4f);
            UiKit.LabelFit(new Rect(head.x, head.y, head.width - buttonW - (fits ? 0f : 6f), head.height), "CC_GuestCheck_Title".Translate(g.Named("PAWN")), UiKit.HeaderColor);
            if (!fits)
            {
                var b = new Rect(head.xMax - buttonW, head.y, buttonW, head.height);
                if (Widgets.ButtonText(b, "CC_GuestCheck_Open".Translate()))
                {
                    Find.WindowStack.TryRemove(typeof(Window_GuestCheck));
                    Find.WindowStack.Add(new Window_GuestCheck(g));
                }
                TooltipHandler.TipRegion(b, "CC_GuestCheck_Open_Tip".Translate());
            }
            if (fits)
            {
                DrawBody(listing.GetRect(body), c, g);
            }
            else if (room < MinScroll)
            {
                // Hardly any room under Hospitality's own lines (a tall inspect pane, a small screen): the verdict on
                // the title's hover, the rest in the window.
                TooltipHandler.TipRegion(head, c.verdictText);
            }
            else
            {
                if (scrolledFor != g)
                {
                    scrolledFor = g;
                    scroll = Vector2.zero;
                }
                var outer = listing.GetRect(room);
                float inner = BodyHeight(c, g, w - 16f);
                var view = new Rect(0f, 0f, w - 16f, inner);
                Widgets.BeginScrollView(outer, ref scroll, view);
                DrawBody(view, c, g);
                Widgets.EndScrollView();
            }
            Text.Font = font;
            Text.Anchor = anchor;
            listing.maxOneColumn = oneColumn;
        }

        /// <summary>After the tab's own lines and Rimfolk's sections under them: the height they took, for the next frame's size.</summary>
        internal static void Grown(Listing_Standard listing, Pawn g)
        {
            if (listing == null || g == null) return;
            sizedFor = g;
            wanted = listing.CurHeight + 44f;
        }

        /// <summary>Before the tab draws: tall enough for the checklist drawn last time for this guest, never off screen.</summary>
        internal static void Size(ITab tab, Pawn g)
        {
            if (tab == null) return;
            if (PaneTopGetter != null && PaneTopGetter.Invoke(tab, null) is float top) paneTop = top;
            ref Vector2 size = ref TabSize(tab);
            if (baseHeight < 0f) baseHeight = size.y;
            size.y = (On || GuestHelpTab.On) && g != null && g == sizedFor ? Mathf.Min(MaxTabHeight, Mathf.Max(baseHeight, wanted)) : baseHeight;
        }

        private static readonly AccessTools.FieldRef<ITab, Vector2> TabSize = AccessTools.FieldRefAccess<ITab, Vector2>("size");
    }

    /// <summary>
    /// Under Hospitality's guest tab, in its own listing: the guest's helping out (GuestHelpTab.Draw), then the recruiting
    /// checklist (GuestCheck.Draw), each with its setting; the tab grows to what they took.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GuestCheckTab
    {
        private static bool broken;
        private static FieldInfo listingField;
        private static readonly MethodInfo SelPawnGetter = AccessTools.PropertyGetter(typeof(ITab), "SelPawn");

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.ITab_Pawn_Guest") is Type t ? AccessTools.Method(t, "FillTabGuest") : null;

        public static void Postfix(object __instance)
        {
            bool help = GuestHelpTab.On, check = GuestCheck.On;
            if (broken || !help && !check) return;
            try
            {
                listingField ??= AccessTools.Field(__instance.GetType(), "listingStandard");
                var listing = listingField?.GetValue(listingField.IsStatic ? null : __instance) as Listing_Standard;
                var pawn = SelPawnGetter?.Invoke(__instance, null) as Pawn;
                if (help) GuestHelpTab.Draw(listing, pawn);
                if (check) GuestCheck.Draw(listing, pawn);
                GuestCheck.Grown(listing, pawn);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the guest tab's recruiting checklist", e);
            }
        }
    }

    /// <summary>Hospitality's guest tab grows to fit the checklist (GuestCheck.Size).</summary>
    [HarmonyPatch]
    public static class Patch_GuestCheckTabSize
    {
        private static bool broken;
        private static readonly MethodInfo SelPawnGetter = AccessTools.PropertyGetter(typeof(ITab), "SelPawn");

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.ITab_Pawn_Guest") is Type t ? AccessTools.Method(t, "FillTab") : null;

        public static void Prefix(object __instance)
        {
            if (broken) return;
            try
            {
                GuestCheck.Size(__instance as ITab, SelPawnGetter?.Invoke(__instance, null) as Pawn);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the guest tab's size", e);
            }
        }
    }
}
