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
    /// home. The tab grows to fit. Hospitality's facts are read by name.
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

        /// <summary>Below Hospitality's own lines, in its listing.</summary>
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
            UiKit.LabelFit(head, "CC_GuestCheck_Title".Translate(g.Named("PAWN")), UiKit.HeaderColor);
            Text.Font = GameFont.Small;
            float w = listing.ColumnWidth;
            float h = Text.CalcHeight(c.verdictText, w);
            var vr = listing.GetRect(h);
            GUI.color = c.verdictColor;
            Widgets.Label(vr, c.verdictText);
            GUI.color = Color.white;
            if (c.lines.Count > 0)
            {
                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                if (c.lines.Any(l => l.mark != 0))
                {
                    var tr = listing.GetRect(Text.LineHeight);
                    GUI.color = UiKit.Faint;
                    Widgets.Label(tr, "CC_GuestCheck_Takes".Translate(g.Named("PAWN")));
                    GUI.color = Color.white;
                }
                foreach (var line in c.lines)
                {
                    float x = line.indent * 16f + (line.mark != 0 ? 18f : 0f);
                    float lh = Mathf.Max(16f, Text.CalcHeight(line.text, w - x));
                    var r = listing.GetRect(lh);
                    if (line.mark != 0)
                        GUI.DrawTexture(new Rect(r.x + line.indent * 16f, r.y + 1f, 14f, 14f), line.mark > 0 ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                    GUI.color = line.color;
                    Widgets.Label(new Rect(r.x + x, r.y, r.width - x, lh), line.text);
                    GUI.color = Color.white;
                }
            }
            Text.Font = font;
            Text.Anchor = anchor;
            // The tab is as tall as this needs: its listing starts 30 in and ends 10 short of the bottom.
            sizedFor = g;
            wanted = listing.CurHeight + 44f;
            listing.maxOneColumn = oneColumn;
        }

        /// <summary>Before the tab draws: tall enough for the checklist drawn last time for this guest.</summary>
        internal static void Size(ITab tab, Pawn g)
        {
            if (tab == null) return;
            ref Vector2 size = ref TabSize(tab);
            if (baseHeight < 0f) baseHeight = size.y;
            size.y = On && g != null && g == sizedFor ? Mathf.Max(baseHeight, wanted) : baseHeight;
        }

        private static readonly AccessTools.FieldRef<ITab, Vector2> TabSize = AccessTools.FieldRefAccess<ITab, Vector2>("size");
    }

    /// <summary>The checklist under Hospitality's guest tab (GuestCheck.Draw), in its own listing.</summary>
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
            if (broken || !GuestCheck.On) return;
            try
            {
                listingField ??= AccessTools.Field(__instance.GetType(), "listingStandard");
                var listing = listingField?.GetValue(listingField.IsStatic ? null : __instance) as Listing_Standard;
                GuestCheck.Draw(listing, SelPawnGetter?.Invoke(__instance, null) as Pawn);
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
