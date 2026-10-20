using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Small badges on the colonist bar's portraits, so the colony's state shows without clicking into each colonist:
    /// close to a mental break (a soft ring around the portrait, or a dot), about to leave the colony, a wish that has
    /// waited long, strain near breaking, wavering faith, a promise at risk. A calm colonist shows nothing. When more
    /// apply than are shown, the most pressing win (<see cref="BarBadgeMath.Priority"/>); the tooltip names the rest.
    ///
    /// Each colonist's badges are worked out at most once a second into a small cache keyed by their id (started over
    /// with each game); drawing reads the cache, a few textures and nothing else, and tooltips are written only while the
    /// mouse is on a badge. Drawn from the game's ColonistBarColonistDrawer.DrawColonist (Patch_ColonistBarBadges);
    /// clicks come through HandleClicks (Patch_ColonistBarBadgeClicks), which takes a left click on a badge before the
    /// bar sees it and lets every other click, and every drag, through untouched.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BarBadges
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // The textures: Rimfolk's icons (Textures/ColonyCouncil/Icons, drawn by Tools/make_icons.py), white and tinted here.
        private static readonly Texture2D RingTex = Load("BarRing");
        private static readonly Texture2D DotTex = Load("BarDot");
        private static readonly Texture2D LeavingTex = Load("Leaving");
        private static readonly Texture2D StrainTex = Load("Strain");

        private static Texture2D Load(string name) => ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/" + name, false);

        private static readonly Color Backing = new Color(0.06f, 0.06f, 0.07f, 0.62f);
        private static readonly Color BackingHover = new Color(0.12f, 0.12f, 0.13f, 0.88f);
        private static readonly Color StrainWorn = new Color(0.94f, 0.62f, 0.68f);

        /// <summary>Seconds between two looks at one colonist.</summary>
        public const float RefreshSeconds = 1f;

        /// <summary>The tooltip slot for the ring (after the kinds').</summary>
        private const int RingTip = 6;

        /// <summary>What applies to one colonist, as last worked out.</summary>
        private sealed class Entry
        {
            public float at = -999f;
            public readonly BarBadge[] badges = new BarBadge[6];
            public int count;
            // Close to a break.
            public BarBreakLevel ring;
            public float closeness, mood, minor, major, extreme;
            public Color ringColor;
            // The facts behind the other badges, for their tooltips and clicks.
            public int leavingTick;
            public float trust;
            public Wish wish;
            public float strain;
            public StrainBand band;
            public float certainty;
            public string faith;
            public GrantWatch watch;
            /// <summary>Written the first time someone hovers, until the next look.</summary>
            public readonly string[] tips = new string[7];
        }

        private static readonly Dictionary<int, Entry> cache = new Dictionary<int, Entry>();
        private static Game cacheGame;

        /// <summary>Clicks reach the badges (their hook applied); otherwise they only show and explain.</summary>
        public static bool Clickable => RimfolkPatches.Applied.Contains(nameof(Patch_ColonistBarBadgeClicks));

        private static bool Wanted(Pawn p) => p != null && !p.Dead && S != null && S.barBadges && RimfolkPatches.On;

        // ---------------- Drawing (every frame) ----------------

        /// <summary>The badges on one portrait. Called by the game's bar for each colonist it draws (repaint only).</summary>
        public static void Draw(Rect rect, Pawn p, Map map, bool reordering)
        {
            if (reordering || !Wanted(p) || Event.current == null || Event.current.type != EventType.Repaint) return;
            try
            {
                var s = S;
                var e = For(p, s, true);
                if (e == null || (e.count == 0 && e.ring == BarBreakLevel.None)) return;
                float alpha = Alpha(rect, map);
                bool ring = s.barBreakMark == BarBreakMark.Ring && e.ring != BarBreakLevel.None;
                if (ring)
                {
                    var c = e.ringColor;
                    GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);
                    if (RingTex != null) GUI.DrawTexture(rect, RingTex);
                    else Widgets.DrawBox(rect, Mathf.Max(1, Mathf.RoundToInt(rect.width / 24f)));
                }
                float scale = rect.width / BarBadgeMath.BaseBarSize;
                float size = BarBadgeMath.SizeFor(s.barBadgeSize, scale);
                float iconRow = BarBadgeMath.GameIconRow * scale;
                int shown = Shown(e, s, rect, size, iconRow);
                bool overBadge = false;
                for (int i = 0; i < shown; i++)
                {
                    var r = SlotRect(i, s, rect, size, iconRow);
                    bool over = Mouse.IsOver(r);
                    GUI.color = WithAlpha(over ? BackingHover : Backing, alpha);
                    if (DotTex != null) GUI.DrawTexture(r, DotTex);
                    var b = e.badges[i];
                    var tex = TextureOf(b.kind);
                    var tint = ColorOf(e, b);
                    GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
                    if (tex != null) GUI.DrawTexture(b.kind == BarBadgeKind.Break ? r.ContractedBy(size * 0.2f) : r.ContractedBy(size * 0.14f), tex);
                    if (over)
                    {
                        overBadge = true;
                        TooltipHandler.TipRegion(r, new TipSignal(TipFor(p, e, i, shown), TipId(p, (int)b.kind)));
                    }
                }
                if (ring && !overBadge && Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, new TipSignal(TipFor(p, e, -1, shown), TipId(p, RingTip)));
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("colonist bar badges", ex);
            }
            finally
            {
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// A left click on a badge opens where it can be dealt with, and is taken so the bar neither selects nor starts a
        /// drag from it. Anything else passes through.
        /// </summary>
        public static void Click(Rect rect, Pawn p)
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.MouseDown || ev.button != 0 || !Wanted(p) || !Mouse.IsOver(rect)) return;
            try
            {
                var s = S;
                var e = For(p, s, false);
                if (e == null || e.count == 0) return;
                float scale = rect.width / BarBadgeMath.BaseBarSize;
                float size = BarBadgeMath.SizeFor(s.barBadgeSize, scale);
                float iconRow = BarBadgeMath.GameIconRow * scale;
                int shown = Shown(e, s, rect, size, iconRow);
                for (int i = 0; i < shown; i++)
                {
                    if (!Mouse.IsOver(SlotRect(i, s, rect, size, iconRow))) continue;
                    ev.Use();
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Open(p, e.badges[i].kind);
                    return;
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("colonist bar badge click", ex);
            }
        }

        /// <summary>One tooltip id per colonist and badge, so a tooltip stays put while its text is written again.</summary>
        private static int TipId(Pawn p, int slot) => Gen.HashCombineInt(p.thingIDNumber, 0x6BA0 + slot);

        /// <summary>How many of a colonist's badges show: the most set, and no more than fit on the portrait.</summary>
        private static int Shown(Entry e, CouncilSettings s, Rect rect, float size, float iconRow) =>
            Mathf.Min(e.count, Mathf.Min(Mathf.Clamp(s.barBadgeMax, 1, BarBadgeMath.MaxShown), BarBadgeMath.Capacity(rect.width, rect.height, size, iconRow)));

        private static Rect SlotRect(int i, CouncilSettings s, Rect rect, float size, float iconRow)
        {
            BarBadgeMath.Slot(i, s.barBadgeCorner, rect.x, rect.y, rect.width, rect.height, size, iconRow, out float x, out float y);
            return new Rect(x, y, size, size);
        }

        /// <summary>The bar's own fading: under a message, or for someone on another map (as the game fades the portrait).</summary>
        private static float Alpha(Rect rect, Map map)
        {
            float alpha = Find.ColonistBar?.GetEntryRectAlpha(rect) ?? 1f;
            bool world = WorldRendererUtility.WorldRendered;
            if (map == null ? !world : map != Find.CurrentMap || world) alpha = Mathf.Min(alpha, ColonistBar.EntryInAnotherMapAlpha);
            return alpha;
        }

        private static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        private static Texture2D TextureOf(BarBadgeKind k)
        {
            switch (k)
            {
                case BarBadgeKind.Break: return DotTex;
                case BarBadgeKind.Leaving: return LeavingTex ?? Icons.Warning;
                case BarBadgeKind.Wish: return Icons.Wishes;
                case BarBadgeKind.Strain: return StrainTex ?? Icons.Heart;
                case BarBadgeKind.Faith: return Icons.CatIdeology;
                case BarBadgeKind.Promise: return Icons.Promise;
                default: return null;
            }
        }

        private static Color ColorOf(Entry e, BarBadge b)
        {
            switch (b.kind)
            {
                case BarBadgeKind.Break: return e.ringColor;
                case BarBadgeKind.Leaving: return UiKit.Bad;
                case BarBadgeKind.Wish: return b.level >= 1 ? UiKit.Warn : UiKit.Gold;
                case BarBadgeKind.Strain: return b.level >= 1 ? UiKit.Bad : StrainWorn;
                case BarBadgeKind.Faith: return b.level >= 1 ? UiKit.Warn : UiKit.Info;
                case BarBadgeKind.Promise: return b.level >= 1 ? UiKit.Bad : UiKit.Warn;
                default: return Color.white;
            }
        }

        // ---------------- Working it out (once a second per colonist) ----------------

        private static Entry For(Pawn p, CouncilSettings s, bool refresh)
        {
            var game = Current.Game;
            if (game != cacheGame)
            {
                cache.Clear();
                cacheGame = game;
            }
            if (!cache.TryGetValue(p.thingIDNumber, out var e))
            {
                if (!refresh) return null;
                // Everyone ever on the bar this game; a long game with many comings and goings starts over now and then.
                if (cache.Count > 300) cache.Clear();
                e = new Entry();
                cache[p.thingIDNumber] = e;
            }
            float now = Time.realtimeSinceStartup;
            if (refresh && (now - e.at >= RefreshSeconds || now < e.at))
            {
                e.at = now;
                try
                {
                    Compute(p, e, s);
                }
                catch (Exception ex)
                {
                    e.count = 0;
                    e.ring = BarBreakLevel.None;
                    CouncilLog.ErrorOnce("colonist bar badges for " + p.LabelShort, ex);
                }
            }
            return e;
        }

        private static void Compute(Pawn p, Entry e, CouncilSettings s)
        {
            e.count = 0;
            e.ring = BarBreakLevel.None;
            e.closeness = 0f;
            e.wish = null;
            e.watch = null;
            e.faith = null;
            for (int i = 0; i < e.tips.Length; i++) e.tips[i] = null;
            if (p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike) return;

            // Close to a break: not while already in one (the game shows that), nor for those who can't break.
            if (s.barBreakMark != BarBreakMark.Off && !p.InMentalState)
            {
                var mood = p.needs?.mood;
                var breaker = p.mindState?.mentalBreaker;
                if (mood != null && breaker != null && breaker.CanDoRandomMentalBreaks)
                {
                    e.mood = mood.CurLevelPercentage;
                    e.minor = breaker.BreakThresholdMinor;
                    e.major = breaker.BreakThresholdMajor;
                    e.extreme = breaker.BreakThresholdExtreme;
                    e.ring = BarBadgeMath.Level(e.mood, e.minor, e.major, e.extreme, s.barBreakMargin);
                    if (e.ring != BarBreakLevel.None)
                    {
                        e.closeness = BarBadgeMath.Closeness(e.mood, e.minor, e.major, e.extreme, s.barBreakMargin);
                        BarBadgeMath.RingColor(e.closeness, out float r, out float g, out float b, out float a);
                        e.ringColor = new Color(r, g, b, a);
                        if (s.barBreakMark == BarBreakMark.Dot) Add(e, BarBadgeKind.Break, (int)e.ring);
                    }
                }
            }

            var comp = CouncilGameComponent.Instance;
            if (comp != null && s.enableWishes)
            {
                // About to leave: the council's own warning, three days before they go (CouncilDynamics.CheckQuitting).
                if (s.barBadgeLeaving)
                {
                    var st = comp.standings?.For(p, false);
                    if (st != null && st.leavingTick > 0)
                    {
                        e.leavingTick = st.leavingTick;
                        e.trust = st.trust;
                        Add(e, BarBadgeKind.Leaving, 0);
                    }
                }
                // Their own open wish that has waited longest, once past the set share of its patience.
                if (s.barBadgeWish)
                {
                    int best = -1;
                    float bestElapsed = -1f;
                    var wishes = comp.wishes;
                    for (int i = 0; i < wishes.Count; i++)
                    {
                        var w = wishes[i];
                        if (w == null || w.state != WishState.Open || w.pawn != p || w.def == null) continue;
                        float elapsed = w.Elapsed;
                        int level = BarBadgeMath.WishLevel(elapsed, s.barWishPatience, w.stage);
                        if (level < 0 || level < best || (level == best && elapsed <= bestElapsed)) continue;
                        best = level;
                        bestElapsed = elapsed;
                        e.wish = w;
                    }
                    if (e.wish != null) Add(e, BarBadgeKind.Wish, best);
                }
            }

            // Strain: worn down or near breaking (Heart/).
            if (s.barBadgeStrain && Heart.On && Heart.Eligible(p))
            {
                var h = Heart.ForView(p);
                if (h != null)
                {
                    int level = BarBadgeMath.StrainLevel((int)h.Band);
                    if (level >= 0)
                    {
                        e.strain = h.strain;
                        e.band = h.Band;
                        Add(e, BarBadgeKind.Strain, level);
                    }
                }
            }

            // Wavering faith (with Ideology and "Faith in the colony" on; Faiths.On checks both).
            if (s.barBadgeFaith && Faiths.Wavering(p))
            {
                e.certainty = Faiths.Certainty(p);
                e.faith = Faiths.Of(p)?.name;
                Add(e, BarBadgeKind.Faith, BarBadgeMath.FaithLevel(e.certainty, FaithMath.WaveringBelow));
            }

            // A promise at risk: something granted is gone (the grace running), or was taken back and can still be made right.
            if (s.barBadgePromise && comp != null && KeptTracker.On && comp.kept?.watches != null)
            {
                int now = Find.TickManager.TicksGame;
                int best = -1;
                var watches = comp.kept.watches;
                for (int i = 0; i < watches.Count; i++)
                {
                    var w = watches[i];
                    if (w == null || w.pawn != p || w.phase == KeptPhase.Kept) continue;
                    int level = w.phase == KeptPhase.TakenBack ? 1 : 0;
                    if (level == 1 && now >= w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay)) continue;
                    if (level <= best) continue;
                    best = level;
                    e.watch = w;
                }
                if (e.watch != null) Add(e, BarBadgeKind.Promise, best);
            }

            BarBadgeMath.Order(e.badges, e.count, BarBadgeMath.MaxShown);
        }

        private static void Add(Entry e, BarBadgeKind kind, int level)
        {
            if (e.count < e.badges.Length) e.badges[e.count++] = new BarBadge(kind, level);
        }

        // ---------------- Clicks ----------------

        private static void Open(Pawn p, BarBadgeKind kind)
        {
            switch (kind)
            {
                case BarBadgeKind.Break:
                case BarBadgeKind.Strain:
                    MoodView.ShowMoodTab(p);
                    break;
                case BarBadgeKind.Faith:
                    ShowTab(p, typeof(ITab_Pawn_Culture));
                    break;
                default:
                    MainTabWindow_Council.OpenTab("Wishes");
                    break;
            }
        }

        /// <summary>Select a colonist and open one of their tabs; if they can't be selected on a map, their council page.</summary>
        private static void ShowTab(Pawn p, Type tab)
        {
            if (Find.MainTabsRoot.OpenTab != null && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect) Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
            if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(tab);
            else MainTabWindow_Council.OpenPerson(p);
        }

        // ---------------- Tooltips (only while hovered) ----------------

        /// <summary>The most important thing first, with its size; what helps; the click; then whatever else applies but isn't shown.</summary>
        private static string TipFor(Pawn p, Entry e, int index, int shown)
        {
            int slot = index < 0 ? RingTip : (int)e.badges[index].kind;
            var cached = e.tips[slot];
            if (cached != null) return cached;
            var sb = new StringBuilder();
            var kind = index < 0 ? BarBadgeKind.Break : e.badges[index].kind;
            int level = index < 0 ? (int)e.ring : e.badges[index].level;
            sb.Append(Lead(p, e, kind, level));
            string help = Help(p, kind, level);
            if (!help.NullOrEmpty()) sb.Append("\n\n").Append(help);
            // What isn't shown: the badges past the most shown (and a ring behind a badge's tooltip is in plain sight).
            var also = new List<string>();
            for (int i = shown; i < e.count; i++) also.Add(Short(e, e.badges[i]));
            if (also.Count > 0) sb.Append("\n\n").Append("CC_Bar_Also".Translate(also.ToCommaList()).Resolve());
            if (index >= 0 && Clickable) sb.Append("\n\n").Append(ClickText(kind).Colorize(ColoredText.SubtleGrayColor));
            string text = sb.ToString();
            e.tips[slot] = text;
            return text;
        }

        private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private static string Points(float v) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(v) * 100f)).ToString();

        private static string Lead(Pawn p, Entry e, BarBadgeKind kind, int level)
        {
            int now = Find.TickManager.TicksGame;
            switch (kind)
            {
                case BarBadgeKind.Break:
                    switch ((BarBreakLevel)level)
                    {
                        case BarBreakLevel.Extreme:
                            return "CC_Bar_Break_Extreme".Translate(p.Named("PAWN"), Pct(e.mood).Named("MOOD"), Pct(e.extreme).Named("LINE")).Resolve();
                        case BarBreakLevel.Major:
                            return "CC_Bar_Break_Major".Translate(p.Named("PAWN"), Pct(e.mood).Named("MOOD"), Pct(e.major).Named("LINE"),
                                Points(e.major - e.mood).Named("GAP"), Pct(e.extreme).Named("NEXT")).Resolve();
                        case BarBreakLevel.Minor:
                            return "CC_Bar_Break_Minor".Translate(p.Named("PAWN"), Pct(e.mood).Named("MOOD"), Pct(e.minor).Named("LINE"),
                                Points(e.minor - e.mood).Named("GAP"), Pct(e.major).Named("NEXT")).Resolve();
                        default:
                            return "CC_Bar_Break_Near".Translate(p.Named("PAWN"), Pct(e.mood).Named("MOOD"), Pct(e.minor).Named("LINE"),
                                Points(e.mood - e.minor).Named("GAP")).Resolve();
                    }
                case BarBadgeKind.Leaving:
                    // They stay once trust climbs back above the council's line (CouncilDynamics.CheckQuitting: -35).
                    return "CC_Bar_Leaving".Translate(p.Named("PAWN"), UiKit.Duration(e.leavingTick - now).Named("LEFT"),
                        Mathf.Max(1, Mathf.CeilToInt(-35f - e.trust + 0.01f)).ToString().Named("TRUST")).Resolve();
                case BarBadgeKind.Wish:
                    return WishLead(p, e.wish, level, now);
                case BarBadgeKind.Strain:
                    return "CC_Bar_Strain".Translate(p.Named("PAWN"), HeartView.BandLabel(e.band).Named("BAND"), Mathf.RoundToInt(e.strain).ToString().Named("STRAIN"),
                        HeartView.BandDesc(e.band).Named("DESC")).Resolve();
                case BarBadgeKind.Faith:
                    return "CC_Bar_Faith".Translate(p.Named("PAWN"), (e.faith ?? "").Named("FAITH"), Pct(e.certainty).Named("CERTAINTY"),
                        Pct(FaithMath.WaveringBelow).Named("WAVERING")).Resolve();
                case BarBadgeKind.Promise:
                    return PromiseLead(e.watch, now);
                default:
                    return "";
            }
        }

        private static string WishLead(Pawn p, Wish w, int level, int now)
        {
            if (w == null) return "";
            var sb = new StringBuilder();
            sb.Append((level >= 1 ? "CC_Bar_WishLastCall" : "CC_Bar_Wish").Translate(p.Named("PAWN"), w.Label.Named("WISH"),
                Pct(Mathf.Clamp01(w.Elapsed)).Named("SHARE"), UiKit.Duration(w.expiresTick - now).Named("LEFT")).Resolve());
            var comp = CouncilGameComponent.Instance;
            if (comp != null) sb.Append("\n").Append("CC_Bar_WishGrant".Translate(comp.RewardText(w)).Resolve());
            // What it costs if it lapses: the ignored (or broken promise) mood at the stakes' strength, and trust.
            var s = S;
            var thought = w.promised ? CouncilDefOf.Council_BrokenPromise : CouncilDefOf.Council_WishIgnored;
            float pen = s?.PenaltyScale ?? 0f;
            if (pen <= 0f || thought?.stages == null || thought.stages.Count == 0) sb.Append("\n").Append("CC_Bar_WishLapseGentle".Translate());
            else
            {
                float mood = thought.stages[0].baseMoodEffect * pen;
                sb.Append("\n").Append((w.promised ? "CC_Bar_WishLapsePromised" : "CC_Bar_WishLapse")
                    .Translate(mood.ToString("+0;-0"), thought.durationDays.ToString("0.#")).Resolve());
            }
            return sb.ToString();
        }

        private static string PromiseLead(GrantWatch w, int now)
        {
            if (w?.pawn == null) return "";
            string lost = KeptTracker.LostText(w);
            if (w.phase == KeptPhase.Missing)
                return lost + "\n" + "CC_Kept_Tip_Missing".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, w.graceUntil - now)).Named("LEFT")).Resolve();
            int left = w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay) - now;
            string text = lost + "\n" + "CC_Kept_Tip_TakenBack".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, left)).Named("LEFT")).Resolve();
            if (w.trustLost > 0.5f) text += " " + "CC_Bar_PromiseCost".Translate(Mathf.RoundToInt(w.trustLost)).Resolve();
            return text;
        }

        private static string Help(Pawn p, BarBadgeKind kind, int level)
        {
            switch (kind)
            {
                case BarBadgeKind.Break: return "CC_Bar_Break_Help".Translate(p.Named("PAWN")).Resolve();
                case BarBadgeKind.Leaving: return "CC_Bar_Leaving_Help".Translate(p.Named("PAWN")).Resolve();
                case BarBadgeKind.Strain: return "CC_Bar_Strain_Help".Translate(p.Named("PAWN")).Resolve();
                case BarBadgeKind.Faith: return "CC_Bar_Faith_Help".Translate(p.Named("PAWN")).Resolve();
                default: return null;
            }
        }

        private static string ClickText(BarBadgeKind kind)
        {
            switch (kind)
            {
                case BarBadgeKind.Break:
                case BarBadgeKind.Strain: return "CC_Bar_Click_Mood".Translate();
                case BarBadgeKind.Faith: return "CC_Bar_Click_Culture".Translate();
                default: return "CC_Bar_Click_Wishes".Translate();
            }
        }

        /// <summary>A few words for a badge that isn't shown, for the "Also" line.</summary>
        private static string Short(Entry e, BarBadge b)
        {
            int now = Find.TickManager.TicksGame;
            switch (b.kind)
            {
                case BarBadgeKind.Break: return "CC_Bar_Short_Break".Translate(Pct(e.mood)).Resolve();
                case BarBadgeKind.Leaving: return "CC_Bar_Short_Leaving".Translate(UiKit.Duration(e.leavingTick - now)).Resolve();
                case BarBadgeKind.Wish:
                    return e.wish != null ? "CC_Bar_Short_Wish".Translate(e.wish.Label, UiKit.Duration(e.wish.expiresTick - now)).Resolve() : "";
                case BarBadgeKind.Strain: return HeartView.BandLabel(e.band);
                case BarBadgeKind.Faith: return "CC_Bar_Short_Faith".Translate(Pct(e.certainty)).Resolve();
                case BarBadgeKind.Promise:
                    return (b.level >= 1 ? "CC_Bar_Short_PromiseTaken" : "CC_Bar_Short_Promise").Translate((e.watch?.label ?? "").CapitalizeFirst()).Resolve();
                default: return "";
            }
        }
    }
}
