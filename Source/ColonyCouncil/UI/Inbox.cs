using System;
using System.Collections.Generic;
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
        public static void Register(string id, InboxSection section, float order, Action<List<WeekRow>> rows, Func<int> needs = null, bool footer = false) =>
            Register(new InboxProvider { id = id, section = section, order = order, rows = rows, needs = needs, footer = footer });

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
        public static void Dirty() => countAt = -1f;

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
