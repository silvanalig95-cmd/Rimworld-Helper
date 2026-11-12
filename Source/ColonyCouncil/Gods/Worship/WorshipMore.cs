using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The worship part's defs (Defs/GodDefs/Rimfolk_Worship*.xml).</summary>
    [DefOf]
    public static class WorshipDefOf
    {
        public static ThoughtDef Rimfolk_Worship_Prayed;
        public static ThoughtDef Rimfolk_Worship_Answered;
        public static ThoughtDef Rimfolk_Worship_Unanswered;
        public static ThoughtDef Rimfolk_Worship_Offered;
        public static ThoughtDef Rimfolk_Worship_Mocked;
        public static ThoughtDef Rimfolk_Worship_Bickered;
        public static ThoughtDef Rimfolk_Worship_VowKept;
        public static ThoughtDef Rimfolk_Worship_VowBroken;
        public static ThoughtDef Rimfolk_Dream_God;

        static WorshipDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(WorshipDefOf));
    }

    // ================= A RimWorld of Magic: mana by the god's favour =================

    /// <summary>
    /// A RimWorld of Magic's casters and the gods (docs/design/gods.md, "A RimWorld of Magic, heavily used": worship): the
    /// mana a caster gains comes back faster while the colony stands well with their god, and slower, or not at all, while
    /// the god is angry (WorshipMath.ManaFactor). Their god is the one their class serves where they have one (the Healer for
    /// a priest or a paladin, the Green for a druid, the Storm-father for the storm born: WorshipMath.ClassDomain), else the
    /// one they pray to; the pull is felt more fully the nearer that god is (GodNear). Read once an hour a caster; the mod is
    /// reached by name only.
    /// </summary>
    public static class MagicFavour
    {
        private static readonly Dictionary<int, (int tick, float factor)> cache = new Dictionary<int, (int, float)>();

        public static float Factor(Pawn p)
        {
            if (p == null || !Worship.On || p.Faction == null || !p.Faction.IsPlayer) return 1f;
            int now = Worship.Now;
            if (cache.TryGetValue(p.thingIDNumber, out var c) && now >= c.tick && now - c.tick < GenDate.TicksPerHour) return c.factor;
            float f = 1f;
            try
            {
                var domain = GodDomain.None;
                var traits = p.story?.traits?.allTraits;
                if (traits != null)
                    foreach (var t in traits)
                    {
                        var d = WorshipMath.ClassDomain(t?.def?.defName);
                        if (d == GodDomain.None) continue;
                        domain = d;
                        break;
                    }
                var g = Worship.GodOf(p, domain);
                if (g != null) f = WorshipMath.ManaFactor(Gods.Favour(g, p.MapHeld), GodNear.Of(g, p.MapHeld));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a caster's god", e);
            }
            if (cache.Count > 300) cache.Clear();
            cache[p.thingIDNumber] = (now, f);
            return f;
        }
    }

    /// <summary>
    /// After A RimWorld of Magic's Need_Mana.GainNeed: what the caster gained this time is scaled by their god's favour
    /// (Gods/Worship/WorshipMore.cs, MagicFavour). Only with the mod; stands down without it.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ManaByFavour
    {
        private static Type needMana;
        private static AccessTools.FieldRef<Need, Pawn> pawnRef;

        public static bool Prepare()
        {
            try
            {
                if (!ModsConfig.IsActive("torann.arimworldofmagic")) return false;
                needMana = AccessTools.TypeByName("TorannMagic.Need_Mana");
                pawnRef = AccessTools.FieldRefAccess<Need, Pawn>("pawn");
                return needMana != null && TargetMethod() != null;
            }
            catch
            {
                return false;
            }
        }

        public static string StandsDownWhy() => needMana == null ? "A RimWorld of Magic isn't loaded" : "";

        public static MethodBase TargetMethod() => needMana == null ? null : AccessTools.Method(needMana, "GainNeed", new[] { typeof(float) });

        public static void Prefix(Need __instance, out float __state) => __state = __instance?.CurLevel ?? -1f;

        public static void Postfix(Need __instance, float __state)
        {
            if (__state < 0f || __instance == null || !RimfolkPatches.On) return;
            try
            {
                float gained = __instance.CurLevel - __state;
                if (gained <= 0f) return;
                float f = MagicFavour.Factor(pawnRef(__instance));
                if (Mathf.Approximately(f, 1f)) return;
                __instance.CurLevel = Mathf.Clamp(__state + gained * f, 0f, __instance.MaxLevel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: mana by favour", e);
            }
        }
    }

    // ================= This week =================

    /// <summary>
    /// Worship in This week (UI/WeekModel.cs): under way, the vows being kept (who, to which god, what) and a god's demand
    /// waiting at its place. The festivals are days on the calendar, so they come with the calendar's own rows.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorshipPanels
    {
        static WorshipPanels()
        {
            Inbox.Register("worship-vows", InboxSection.UnderWay, 62f, Rows);
        }

        private static void Rows(List<WeekRow> rows)
        {
            var b = Worship.Book;
            if (b == null) return;
            foreach (var v in b.vows)
            {
                if (v == null || v.done || v.pawn == null || v.pawn.Dead) continue;
                var g = Gods.ById(v.god);
                if (g == null) continue;
                string text = ("CC_Wsh_Week_Vow_" + v.kind).Translate(WeekModel.Name(v.pawn).Named("PAWN"), Worship.Called(g).Named("GOD"), (v.aboutName ?? "").Named("ABOUT"),
                    (v.beast?.label ?? "").Named("BEAST")).Resolve();
                var target = v.pawn.Spawned ? new GlobalTargetInfo(v.pawn) : GlobalTargetInfo.Invalid;
                rows.Add(new WeekRow
                {
                    pawn = v.pawn,
                    text = text,
                    sub = "CC_Wsh_Week_VowSub".Translate(Worship.Called(g).Named("GOD")).Resolve(),
                    note = "CC_Wsh_Week_Vow".Translate(),
                    noteColor = UiKit.Gold,
                    tip = WeekModel.Tip(("CC_Wsh_Week_VowTip_" + v.kind).Translate(), target.IsValid ? "CC_Week_ClickMap".Translate(v.pawn.LabelShort).Resolve() : null),
                    click = target.IsValid ? WeekModel.Go(target) : null,
                    waits = false,
                    order = v.since
                });
            }
            foreach (var d in b.demands)
            {
                if (d == null || d.done) continue;
                var g = Gods.ById(d.god);
                if (g == null) continue;
                var target = d.shrine != null && d.shrine.Spawned ? new GlobalTargetInfo(d.shrine) : GlobalTargetInfo.Invalid;
                // What the dreamer says the god wants, as they tell it: a person's word, never the god's terms.
                rows.Add(new WeekRow
                {
                    pawn = d.dreamer != null && !d.dreamer.Dead ? d.dreamer : null,
                    text = "CC_Wsh_Week_Demand".Translate(WeekModel.Name(d.dreamer).Named("PAWN"), Worship.Called(g).Named("GOD"), ("CC_Wsh_Kind_" + d.kind).Translate().Named("WHAT"),
                        Worship.ShrineOf(g).Named("SHRINE")).Resolve(),
                    note = "CC_Wsh_Week_DemandNote".Translate(),
                    noteColor = UiKit.Faint,
                    tip = WeekModel.Tip("CC_Wsh_Week_DemandTip".Translate((d.dreamer?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                        target.IsValid ? "CC_Week_ClickMap".Translate(d.shrine.LabelShort).Resolve() : null),
                    click = target.IsValid ? WeekModel.Go(target) : null,
                    waits = false,
                    order = d.until
                });
            }
        }
    }

    // ================= Dev =================

    /// <summary>Dev actions for worship ("Gods: worship ..."), in the dev menu's Rimfolk section.</summary>
    public static class WorshipDebug
    {
        [DebugAction("Rimfolk", "Gods: a prayer now...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Pray(Pawn p)
        {
            if (p == null || !p.IsColonist) return;
            var opts = new List<DebugMenuOption>();
            foreach (PrayerKind k in Enum.GetValues(typeof(PrayerKind)))
            {
                var kind = k;
                opts.Add(new DebugMenuOption(kind.ToString(), DebugMenuOptionMode.Action, () => Prayers.DevPray(p, kind)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(opts));
        }

        [DebugAction("Rimfolk", "Gods: a vow now...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Vow(Pawn p)
        {
            if (p == null || !p.IsColonist) return;
            var opts = new List<DebugMenuOption>();
            foreach (VowKind k in Enum.GetValues(typeof(VowKind)))
            {
                var kind = k;
                opts.Add(new DebugMenuOption(kind.ToString(), DebugMenuOptionMode.Action, () => Vows.DevVow(p, kind)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(opts));
        }

        [DebugAction("Rimfolk", "Gods: a god's dream now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Dream(Pawn p)
        {
            if (p == null || !p.IsColonist) return;
            if (!GodDreams.DevDream(p)) Messages.Message("CC_Wsh_Dev_NoGod".Translate(p.LabelShort), p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Gods: a crisis of faith", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Crisis(Pawn p)
        {
            var b = Worship.Book;
            var g = Worship.GodOf(p);
            if (b == null || g == null) return;
            FaithHeart.TurnAway(b, p, g, p.Map);
            b.AddMark(p, "crisis", g.id);
            CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Unanswered);
        }

        [DebugAction("Rimfolk", "Gods: offerings at every shrine now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OfferNow()
        {
            var b = Worship.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            foreach (var s in Shrines.Of(map, null))
            {
                var g = Gods.ById(s.god);
                if (g == null || !GodWorshipDef.TakesOfferings(g) || b.offerings.Any(o => o != null && o.shrine == s.thing && o.fate == OfferingFate.Waiting)) continue;
                b.offerings.Add(new OfferingRecord { shrine = s.thing, god = g.id, home = Gods.HomeKey(map), chosen = Worship.Now });
            }
            Offerings.Hourly(b, map, Worship.Now);
        }

        [DebugAction("Rimfolk", "Gods: worship's hour now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HourNow() => Worship.Hourly(Worship.Now);

        [DebugAction("Rimfolk", "Gods: worship's day now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DayNow() => Worship.Daily(Worship.Now);

        [DebugAction("Rimfolk", "Gods: the gods' festivals onto the calendar now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FestivalsNow() => ColonyCalendar.InView?.DevGodFestivals(Find.CurrentMap);

        [DebugAction("Rimfolk", "Gods: show worship (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Show()
        {
            var b = Worship.Book;
            if (b == null)
            {
                Log.Message("[Rimfolk] Worship is off.");
                return;
            }
            var sb = new System.Text.StringBuilder("[Rimfolk] Worship:\n");
            var map = Find.CurrentMap;
            sb.AppendLine("Priest: " + (Worship.Priest(map)?.LabelShort ?? "none") + "; honoured this year: " + (Worship.Honoured(map)?.name ?? "none"));
            foreach (var s in b.shrines) sb.AppendLine("  place " + s.thing?.LabelShort + " -> " + Gods.ById(s.god)?.name + (s.pending ? " (waiting)" : "") + (Shrines.IsTemple(s.thing) ? " (temple)" : ""));
            foreach (var o in b.offerings) sb.AppendLine("  offering " + o.label + " to " + Gods.ById(o.god)?.name + ": " + o.fate + (o.set ? " set" : " planned"));
            foreach (var p in b.prayers.Skip(Math.Max(0, b.prayers.Count - 12))) sb.AppendLine("  prayer " + p.pawn?.LabelShort + " " + p.kind + " to " + Gods.ById(p.god)?.name + (p.answered ? " answered" : "") + (p.settled ? " settled" : ""));
            foreach (var v in b.vows) sb.AppendLine("  vow " + v.pawn?.LabelShort + " " + v.kind + " to " + Gods.ById(v.god)?.name + (v.done ? " done" : " open"));
            foreach (var d in b.demands) sb.AppendLine("  demand " + Gods.ById(d.god)?.name + " " + d.kind + (d.done ? (d.met ? " met" : " let go") : " open"));
            foreach (var m in b.marks.Skip(Math.Max(0, b.marks.Count - 12))) sb.AppendLine("  mark " + m.pawn?.LabelShort + " " + m.what + " " + Gods.ById(m.god)?.name);
            foreach (var m in b.homeMarks) sb.AppendLine("  home " + m.what + " " + Gods.ById(m.god)?.name);
            Log.Message(sb.ToString());
        }
    }
}
