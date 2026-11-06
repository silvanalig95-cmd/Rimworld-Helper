using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One part of This week: the rows it adds to a section, how many of them wait on the player (for Needs you), and
    /// whether its rows stand at the section's foot whatever is folded (the mood balance).
    /// </summary>
    public sealed class InboxProvider
    {
        public string id;
        public InboxSection section;
        public float order;
        /// <summary>Adds its rows (each with its own order; the section sorts them, provider by provider).</summary>
        public Action<List<WeekRow>> rows;
        /// <summary>
        /// How many things of its wait on the player: the one count adds these up (Needs you only). Cheap, as it is asked
        /// twice a second for the corner and the bottom bar; null counts its rows (built for it).
        /// </summary>
        public Func<int> needs;
        /// <summary>Its rows stand after the section's others, and show when the section is folded.</summary>
        public bool footer;
        /// <summary>Needs you only: the on-screen council panel's part its rows show under (InboxMath.PanelParts); null, the other asks.</summary>
        public string part;
    }

    /// <summary>One provider of Needs you, as last read: how many of its things wait (as the one count counts them) and its rows in their order.</summary>
    public sealed class InboxWaitingPart
    {
        public InboxProvider provider;
        public int count;
        public readonly List<WeekRow> rows = new List<WeekRow>();
    }

    /// <summary>
    /// What waits on the player, as Needs you lists it, read at most once a second for what shows it besides This week:
    /// the on-screen council panel (Hud/HudCouncil.cs) by kind, the colonist bar's badges (ColonistBar/BarBadges.cs) by
    /// colonist. One list, so the three never disagree (the review's decision 16); the game's alerts stay the game's.
    /// </summary>
    public sealed class InboxWaiting
    {
        public int count;
        public readonly List<InboxWaitingPart> parts = new List<InboxWaitingPart>();
        private readonly Dictionary<Pawn, List<WeekRow>> byPawn = new Dictionary<Pawn, List<WeekRow>>();
        private static readonly List<WeekRow> None = new List<WeekRow>();

        /// <summary>The rows about one colonist that wait on the player (their wishes, a promise to them at risk...).</summary>
        public List<WeekRow> Of(Pawn p) => p != null && byPawn.TryGetValue(p, out var list) ? list : None;

        internal void Index()
        {
            byPawn.Clear();
            foreach (var part in parts)
            foreach (var r in part.rows)
            {
                if (!r.waits || r.pawn == null) continue;
                if (!byPawn.TryGetValue(r.pawn, out var list)) byPawn[r.pawn] = list = new List<WeekRow>();
                list.Add(r);
            }
        }
    }

    /// <summary>
    /// The one inbox (docs/design/interface.md, "This week: one inbox"): features add rows to This week's sections
    /// through <see cref="Register(InboxProvider)"/> rather than pages of their own (the design's rule 1, "Where a new
    /// thing goes"). The first providers are what were This week's twelve cards and the region's and diplomacy's lines
    /// (UI/InboxProviders.cs, UI/InboxUnderWay.cs). <see cref="Count"/> is the one count of what waits on the player:
    /// This week's tab, the inbox in every window's corner and the council button show the same number.
    /// </summary>
    public static class Inbox
    {
        internal static readonly InboxRegistry<InboxProvider> registry = new InboxRegistry<InboxProvider>();
        private static bool own;

        /// <summary>Rimfolk's own providers first, so another part's come after them in the same order.</summary>
        private static void Own()
        {
            if (own) return;
            own = true;
            try
            {
                InboxProviders.AddOwn();
                InboxUnderWay.AddOwn();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("this week: its own parts", e);
            }
        }

        /// <summary>Adds a part to This week (one added again under its id replaces the first).</summary>
        public static void Register(InboxProvider p)
        {
            if (p == null || p.rows == null) return;
            Own();
            registry.Add(p.section, p.order, p.id, p);
            Dirty();
        }

        /// <summary>Adds a part to This week: its section, its place there, its rows and, for Needs you, how many wait on the player.</summary>
        public static void Register(string id, InboxSection section, float order, Action<List<WeekRow>> rows, Func<int> needs = null, bool footer = false,
            string part = null) =>
            Register(new InboxProvider { id = id, section = section, order = order, rows = rows, needs = needs, footer = footer, part = part });

        /// <summary>A section's parts in their order.</summary>
        public static List<InboxRegistry<InboxProvider>.Entry> In(InboxSection s)
        {
            Own();
            return registry.In(s);
        }

        // ---------------- The one count ----------------

        private static int count;
        private static float countAt = -1f;
        private static Game countGame;
        private static readonly List<int> parts = new List<int>();
        private static readonly List<WeekRow> scratch = new List<WeekRow>();

        /// <summary>Something waiting changed (a letter answered, a pin): the next ask counts again.</summary>
        public static void Dirty()
        {
            countAt = -1f;
            waitingAt = -1f;
        }

        // ---------------- What waits, for the panel and the badges ----------------

        private static InboxWaiting waiting = new InboxWaiting();
        private static float waitingAt = -1f;
        private static Game waitingGame;

        /// <summary>
        /// What waits on the player, provider by provider of Needs you, each with its count (what the one count adds up)
        /// and its rows in This week's order, and by colonist. Read at most once a second; This week builds its own copy
        /// from the same providers.
        /// </summary>
        public static InboxWaiting Waiting
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (waitingAt >= 0f && now - waitingAt < 1f && now >= waitingAt && waitingGame == Current.Game) return waiting;
                waitingAt = now;
                waitingGame = Current.Game;
                waiting = BuildWaiting();
                return waiting;
            }
        }

        private static InboxWaiting BuildWaiting()
        {
            var w = new InboxWaiting();
            if (Current.Game == null || CouncilGameComponent.Instance == null) return w;
            var counts = new List<int>();
            var rows = new List<WeekRow>();
            foreach (var e in In(InboxSection.Needs))
            {
                var p = e.value;
                rows.Clear();
                try
                {
                    p.rows(rows);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("this week: " + p.id, ex);
                    continue;
                }
                rows.RemoveAll(r => r == null);
                var part = new InboxWaitingPart { provider = p };
                // In This week's order: by the rows' own order, equals as found (a stable sort).
                part.rows.AddRange(rows.OrderBy(r => r.order));
                try
                {
                    part.count = p.needs != null ? p.needs() : part.rows.Count(r => r.waits);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("this week: counting " + p.id, ex);
                }
                counts.Add(part.count);
                w.parts.Add(part);
            }
            w.count = InboxMath.Count(counts);
            w.Index();
            return w;
        }

        /// <summary>
        /// How many things wait on the player: what Needs you's providers count (with Rimfolk's own, 0.83's count of
        /// petitions, open wishes, letters waiting and promises at risk, and since the inbox the region's and diplomacy's
        /// asks). Counted at most twice a second.
        /// </summary>
        public static int Count
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (countAt >= 0f && now - countAt < 0.5f && now >= countAt && countGame == Current.Game) return count;
                countAt = now;
                countGame = Current.Game;
                count = Compute();
                return count;
            }
        }

        private static int Compute()
        {
            if (Current.Game == null || CouncilGameComponent.Instance == null) return 0;
            parts.Clear();
            foreach (var e in In(InboxSection.Needs))
            {
                var p = e.value;
                try
                {
                    if (p.needs != null) parts.Add(p.needs());
                    else
                    {
                        scratch.Clear();
                        p.rows(scratch);
                        parts.Add(scratch.Count);
                    }
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("this week: counting " + p.id, ex);
                }
            }
            scratch.Clear();
            return InboxMath.Count(parts);
        }
    }
}
