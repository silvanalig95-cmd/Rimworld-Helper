using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's alerts in a panel of their own, movable like the others: every alert the game shows right now, the
    /// game's and every mod's (Rimfolk's council advice and wishes too), read from the game's own list
    /// (AlertsReadout.activeAlerts), so nothing is worked out twice. Critical alerts in red, high in orange, medium plain;
    /// in the game's priority and, within it, the game's own order (or the newest first). A count where the game gives one
    /// ("×3"), the game's explanation as the tooltip, and a click does what clicking the game's alert does (go to it, and
    /// on to the next with the next click). New high and critical alerts flash for a moment, as the game's own bounce in.
    ///
    /// While it shows, the game's own list is hidden by default (<see cref="Patch_HudGameAlerts"/>): only its drawing,
    /// so the game keeps every alert up to date. Until it's moved, the panel sits in the game's alert place, its bottom
    /// just above the letters. If the game's list can't be read, the panel says so and the game's list stays.
    ///
    /// Any alert can be hidden in the panel (right-click its row), kept by the alert's class; the panel's options list
    /// them to show again. Rimfolk's own "Colonist wishes" is hidden while the council panel shows the wishes, which it
    /// would only repeat (it can be shown again too). The game's own list, when it shows, is never changed.
    /// </summary>
    public class HudAlerts : HudPanel
    {
        public const string PartCritical = "critical", PartHigh = "high", PartMedium = "medium", PartCount = "count";
        public static readonly string[] Parts = { PartCritical, PartHigh, PartMedium, PartCount };

        /// <summary>How long a new row flashes (seconds).</summary>
        private const float FlashFor = 3f;

        private sealed class Row
        {
            public Alert alert;
            public string label;
            public int priority, order, count;
            public float seen, score;
            public bool showCount;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly Dictionary<Alert, float> firstSeen = new Dictionary<Alert, float>();
        private readonly Dictionary<Alert, int> counts = new Dictionary<Alert, int>();
        private readonly HashSet<Alert> stillActive = new HashSet<Alert>();
        private readonly List<Alert> gone = new List<Alert>();
        private float countsAt = -100f;
        private bool primed, readOk = true;
        private int hidden;
        private string moreText;

        private static AccessTools.FieldRef<AlertsReadout, List<Alert>> activeRef;
        private static MethodInfo onClick;
        private static bool looked, unreadable;

        private static readonly Dictionary<Alert, (float at, string text)> explanations = new Dictionary<Alert, (float, string)>();

        /// <summary>The alerts' labels seen this session, by class, for the options' list of hidden alerts.</summary>
        private static readonly Dictionary<string, string> labelsByClass = new Dictionary<string, string>();

        /// <summary>Rimfolk's colonist wishes alert, which the council panel repeats.</summary>
        public static readonly string WishesAlert = typeof(Alert_CouncilWishes).FullName;

        public HudAlerts() : base(HudLayout.Alerts)
        {
        }

        // ---------------- Reading the game's list ----------------

        /// <summary>The game's active alerts (its own list: don't change it), or null when it can't be read.</summary>
        public static List<Alert> Active()
        {
            if (!looked)
            {
                looked = true;
                try
                {
                    activeRef = AccessTools.FieldRefAccess<AlertsReadout, List<Alert>>("activeAlerts");
                }
                catch (Exception e)
                {
                    unreadable = true;
                    Log.Warning("[Rimfolk] The alerts panel can't read the game's alerts in this version, so the game's own list stays: " + e.Message);
                }
            }
            if (unreadable || activeRef == null) return null;
            var readout = Find.Alerts;
            if (readout == null) return null;
            try
            {
                return activeRef(readout);
            }
            catch (Exception e)
            {
                unreadable = true;
                CouncilLog.ErrorOnce("alerts panel: reading the game's alerts", e);
                return null;
            }
        }

        /// <summary>
        /// Whether the game's own alert list is hidden now: the setting is on, the game hooks are on, and this panel is
        /// open and reading the alerts. Asked by the patch on the game's drawing of its list, each time it would draw.
        /// </summary>
        public static bool HidesGameList()
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.hudHideGameAlerts || !s.hudShown || unreadable || !RimfolkPatches.On) return false;
            if (HudManager.Failed(HudLayout.Alerts)) return false;
            return HudManager.Window(HudLayout.Alerts) is HudAlerts w && w.readOk && w.IsOpen;
        }

        // ---------------- Gathering ----------------

        public override void PostOpen()
        {
            base.PostOpen();
            // What's already up when the panel opens doesn't flash.
            primed = false;
            firstSeen.Clear();
        }

        protected override void Refresh()
        {
            rows.Clear();
            var list = Active();
            readOk = list != null;
            moreText = null;
            hidden = 0;
            if (list == null) return;
            var st = St;
            float now = Time.realtimeSinceStartup;
            bool wantCounts = st.Shows(PartCount) && now - countsAt >= 2f;
            if (wantCounts)
            {
                countsAt = now;
                counts.Clear();
            }
            stillActive.Clear();
            var all = new List<Row>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null) continue;
                stillActive.Add(a);
                if (!firstSeen.TryGetValue(a, out float seen))
                {
                    seen = primed ? now : -100f;
                    firstSeen[a] = seen;
                }
                int prio;
                string label;
                try
                {
                    prio = (int)a.Priority;
                    label = a.Label;
                    if (label.NullOrEmpty()) label = a.GetLabel();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("alerts panel: " + a.GetType().Name, e);
                    continue;
                }
                if (label.NullOrEmpty()) continue;
                string cls = a.GetType().FullName;
                labelsByClass[cls] = label.StripTags().Replace("\n", " ");
                if (Hidden(st, cls)) continue;
                if (!st.Shows(prio >= 2 ? PartCritical : prio == 1 ? PartHigh : PartMedium)) continue;
                if (wantCounts) counts[a] = CountOf(a);
                counts.TryGetValue(a, out int count);
                label = label.StripTags().Replace("\n", " ");
                all.Add(new Row
                {
                    alert = a, label = label, priority = prio, order = i, seen = seen, count = count, score = HudRank.Alert(prio),
                    showCount = st.Shows(PartCount) && HudMath.ShowCount(label, count)
                });
            }
            primed = true;
            // Forget the ones that went.
            gone.Clear();
            foreach (var a in firstSeen.Keys)
                if (!stillActive.Contains(a))
                    gone.Add(a);
            foreach (var a in gone)
            {
                firstSeen.Remove(a);
                counts.Remove(a);
                explanations.Remove(a);
            }
            // The game's order: by priority, then its own order within a priority. Or the newest first.
            if (st.sort == HudSort.Newest) all = all.OrderByDescending(r => r.seen).ThenByDescending(r => r.priority).ThenBy(r => r.order).ToList();
            else all = all.OrderByDescending(r => r.priority).ThenBy(r => r.order).ToList();
            var keep = HudMath.Keep(all.Select(r => r.score).ToList(), st.maxRows, HudRank.LifeFloor, out hidden);
            foreach (int i in keep) rows.Add(all[i]);
            moreText = hidden > 0 ? "CC_Pnl_AndMore".Translate(hidden).Resolve() : null;
        }

        /// <summary>How many things the alert is about (its culprits), 0 when it doesn't say.</summary>
        private static int CountOf(Alert a)
        {
            try
            {
                var r = a.GetReport();
                if (!r.active) return 0;
                if (r.culpritsThings != null) return r.culpritsThings.Count;
                if (r.culpritsPawns != null) return r.culpritsPawns.Count;
                if (r.culpritsCaravans != null) return r.culpritsCaravans.Count;
                if (r.culpritsTargets != null) return r.culpritsTargets.Count;
                return r.culpritTarget.HasValue ? 1 : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ---------------- Place and size ----------------

        protected override bool AutoPlace(Vector2 size, float sw, float sh, out HudBox box)
        {
            float letters = -1f, gameList = 0f;
            try
            {
                letters = Find.LetterStack?.LastTopY ?? -1f;
                // While the game's own list still shows, stand above it rather than on it.
                if (!HidesGameList()) gameList = Find.Alerts?.AlertsHeight ?? 0f;
            }
            catch (Exception)
            {
            }
            box = HudMath.AlertsBox(size.x, size.y, sw, sh, letters, gameList);
            return true;
        }

        private float RowH(float s) => Mathf.Round((St.compact ? 17f : 20f) * s);
        private float Width(float s) => Mathf.Round((St.compact ? 190f : 248f) * s);

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round((readOk ? 150f : 220f) * s), pad * 2f + RowH(s));
            return new Vector2(Width(s), pad * 2f + rows.Count * RowH(s) + more);
        }

        // ---------------- Drawing ----------------

        private static Color ColorOf(int priority) => priority >= 2 ? UiKit.Bad : priority == 1 ? UiKit.Warn : new Color(0.9f, 0.9f, 0.9f);

        protected override void DrawBody(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            float rh = RowH(s);
            if (rows.Count == 0)
            {
                HudKit.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, rh), HudKit.T(readOk ? "CC_Pnl_NoAlerts" : "CC_Pnl_AlertsUnreadable"), UiKit.Faint,
                    HudKit.Minor(s));
                return;
            }
            var font = St.compact ? GameFont.Tiny : HudKit.Main(s);
            float now = Time.realtimeSinceStartup;
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var c = ColorOf(row.priority);
                // The game pulses its critical alerts; high ones have a warm tint.
                if (row.priority >= 2) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Bad, 0.12f + 0.1f * HudKit.Pulse));
                else if (row.priority == 1) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Warn, 0.1f));
                // New: a flash that fades, as the game's alert bounces in.
                float age = now - row.seen;
                if (row.priority >= 1 && row.seen > 0f && age >= 0f && age < FlashFor)
                    Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(c, 0.45f * (1f - age / FlashFor)));
                if (Over(rr)) Widgets.DrawBoxSolid(rr, UiKit.CardHover);
                // A stripe in the priority's colour.
                Widgets.DrawBoxSolid(new Rect(rr.x, rr.y + 2f, 2f, rr.height - 4f), row.priority >= 1 ? c : UiKit.WithAlpha(Color.white, 0.25f));
                float x = rr.x + Mathf.Round(6f * s);
                float right = rr.xMax - 2f;
                if (row.showCount)
                {
                    string n = "×" + row.count;
                    float nw = HudKit.Width(n, GameFont.Tiny) + 4f;
                    HudKit.Label(new Rect(right - nw, rr.y, nw, rh), n, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= nw + 2f;
                }
                HudKit.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rh), row.label, c, font);
                var alert = row.alert;
                Tip(rr, () => Explain(alert), Gen.HashCombineInt(0x5A1E7A, row.order ^ (alert.GetHashCode() & 0xFFFF)));
                if (Clicked(rr))
                {
                    if (Event.current.button == 1) RowMenu(alert);
                    else Click(alert);
                }
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y + rows.Count * rh, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                Tip(mr, HudKit.T("CC_Pnl_MoreAlertsTip"));
            }
        }

        /// <summary>The game's explanation for the alert, as its own info box gives it, kept half a second.</summary>
        private static string Explain(Alert a)
        {
            float now = Time.realtimeSinceStartup;
            if (explanations.TryGetValue(a, out var e) && now - e.at < 0.5f && now >= e.at) return e.text;
            string text;
            try
            {
                text = a.GetExplanation().Resolve();
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("alerts panel: explanation of " + a.GetType().Name, ex);
                text = a.Label;
            }
            string label = a.Label;
            if (!label.NullOrEmpty()) text = label.Colorize(UiKit.HeaderColor) + "\n\n" + text;
            explanations[a] = (now, text);
            return text;
        }

        /// <summary>What clicking the game's alert does (Alert.OnClick: go to what it's about, the next one on the next click).</summary>
        private static void Click(Alert a)
        {
            try
            {
                onClick ??= AccessTools.Method(typeof(Alert), "OnClick");
                if (onClick == null) return;
                onClick.Invoke(a, null);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            catch (TargetInvocationException e)
            {
                CouncilLog.ErrorOnce("alerts panel: clicking " + a.GetType().Name, e.InnerException ?? e);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("alerts panel: clicking " + a.GetType().Name, e);
            }
        }

        // ---------------- Alerts hidden in the panel ----------------

        /// <summary>
        /// Whether an alert (by class) is hidden in the panel: the player hid it, or it's the colonist wishes while the
        /// council panel shows the wishes (unless the player chose to see it).
        /// </summary>
        public static bool Hidden(HudPanelState st, string cls)
        {
            if (st == null || cls.NullOrEmpty()) return false;
            if (st.alertsHidden != null && st.alertsHidden.Contains(cls)) return true;
            return HiddenByDefault(cls) && (st.alertsShown == null || !st.alertsShown.Contains(cls));
        }

        /// <summary>Hidden unless the player asks for it: the colonist wishes, while the council panel shows them.</summary>
        public static bool HiddenByDefault(string cls)
        {
            if (cls != WishesAlert) return false;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.hudShown || HudManager.Failed(HudLayout.Council)) return false;
            var council = HudLayout.State(HudLayout.Council);
            return council.on && council.Shows(HudCouncil.PartWishes);
        }

        public static void Hide(HudPanelState st, string cls)
        {
            st.alertsHidden ??= new List<string>();
            st.alertsShown ??= new List<string>();
            if (!st.alertsHidden.Contains(cls)) st.alertsHidden.Add(cls);
            st.alertsShown.Remove(cls);
            HudManager.Window(HudLayout.Alerts)?.MarkDirty();
            HudLayout.Save();
        }

        public static void Unhide(HudPanelState st, string cls)
        {
            st.alertsHidden ??= new List<string>();
            st.alertsShown ??= new List<string>();
            st.alertsHidden.Remove(cls);
            if (HiddenByDefault(cls) && !st.alertsShown.Contains(cls)) st.alertsShown.Add(cls);
            HudManager.Window(HudLayout.Alerts)?.MarkDirty();
            HudLayout.Save();
        }

        /// <summary>Every alert hidden in the panel now, by class (the player's, and the wishes while the council shows them).</summary>
        public static List<string> HiddenNow(HudPanelState st)
        {
            var list = new List<string>();
            if (st?.alertsHidden != null) list.AddRange(st.alertsHidden);
            if (Hidden(st, WishesAlert) && !list.Contains(WishesAlert)) list.Add(WishesAlert);
            return list;
        }

        public static void ShowAll(HudPanelState st)
        {
            foreach (var cls in HiddenNow(st)) Unhide(st, cls);
        }

        /// <summary>An alert's name for the options: its label when it was seen this session, else its class's name in words.</summary>
        public static string LabelOf(string cls)
        {
            if (cls.NullOrEmpty()) return "?";
            if (labelsByClass.TryGetValue(cls, out var l) && !l.NullOrEmpty()) return l;
            if (cls == WishesAlert) return HudKit.T("CC_Pnl_WishesAlert");
            string name = cls.Substring(cls.LastIndexOf('.') + 1);
            if (name.StartsWith("Alert_")) name = name.Substring(6);
            return GenText.SplitCamelCase(name.Replace("_", " ")).CapitalizeFirst();
        }

        /// <summary>A row's right-click: hide this alert here, then the panel's own menu.</summary>
        private void RowMenu(Alert a)
        {
            string cls = a?.GetType().FullName;
            var first = new List<FloatMenuOption>();
            if (!cls.NullOrEmpty())
            {
                var st = St;
                first.Add(new FloatMenuOption(HudKit.T("CC_Pnl_HideAlert"), () => Hide(st, cls)));
            }
            OpenMenu(first);
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var s = ColonyCouncilMod.Settings;
            options.Add(new FloatMenuOption(HudKit.T(s.hudHideGameAlerts ? "CC_Pnl_ShowGameAlerts" : "CC_Pnl_HideGameAlerts"), () =>
            {
                s.hudHideGameAlerts = !s.hudHideGameAlerts;
                HudLayout.Save();
            }));
            var st = St;
            options.Add(new FloatMenuOption(HudKit.T(st.sort == HudSort.Newest ? "CC_Pnl_SortPriority" : "CC_Pnl_SortNewest"), () =>
            {
                st.sort = st.sort == HudSort.Newest ? HudSort.Risk : HudSort.Newest;
                MarkDirty();
                HudLayout.Save();
            }));
        }
    }
}
