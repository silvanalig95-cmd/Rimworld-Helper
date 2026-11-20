using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony life's variety, part 2 (the content update, the player's go of 20 November 2026): what is kept between passes
    /// for it. Who first asked for each custom the colony keeps (A22's mark when it becomes a tradition), where a season of
    /// kept promises stands (A15), the children lately given a toy (A11), the giving day under way and the year it was last
    /// kept (A26), the elders' day's year (A28), and the giving day's gifts of the last days (a story, "an unexpected gift").
    /// Saved with the council (CouncilGameComponent.lifeVariety); each list is kept short (DESIGN.md, "Save size").
    /// </summary>
    public class LifeVarietyBook : IExposable
    {
        /// <summary>Who first asked for a custom the colony kept: "defName|ThingID", one per custom.</summary>
        public List<string> firstAskers = new List<string>();
        /// <summary>A season of kept promises (A15): the colony's promises kept when the streak began, and the promises broken last seen.</summary>
        public int keptAtStart = -1, brokenSeen = -1;
        /// <summary>Children lately given a toy by a wish (A11): "ThingID|tick".</summary>
        public List<string> toys = new List<string>();
        /// <summary>The giving day under way (A26), or null.</summary>
        public GivingPlan giving;
        /// <summary>The colony's year (the home's own) the giving day was last kept, so it comes once a winter.</summary>
        public int givingYear = -99;
        /// <summary>The colony's year the elders' day was last kept (A28).</summary>
        public int eldersYear = -99;
        /// <summary>Gifts of the giving day lately handed over: "giverId|receiverId|tick|label", the newest few.</summary>
        public List<string> gifts = new List<string>();

        public const int MaxAskers = 40, MaxToys = 12, MaxGifts = 16;

        public static LifeVarietyBook Instance => CouncilGameComponent.Instance?.lifeVariety;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                toys.RemoveAll(t => !ToyMath.GivenLately(TickOf(t), now, GenDate.TicksPerDay));
                gifts.RemoveAll(g => now - TickOf(g, 2) > ColonyStoryMath.GiftDays * GenDate.TicksPerDay);
            }
            Scribe_Collections.Look(ref firstAskers, "firstAskers", LookMode.Value);
            Scribe_Values.Look(ref keptAtStart, "keptAtStart", -1);
            Scribe_Values.Look(ref brokenSeen, "brokenSeen", -1);
            Scribe_Collections.Look(ref toys, "toys", LookMode.Value);
            Scribe_Deep.Look(ref giving, "giving");
            Scribe_Values.Look(ref givingYear, "givingYear", -99);
            Scribe_Values.Look(ref eldersYear, "eldersYear", -99);
            Scribe_Collections.Look(ref gifts, "gifts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                firstAskers ??= new List<string>();
                toys ??= new List<string>();
                gifts ??= new List<string>();
                if (giving != null && (giving.map == null || giving.pairs.Count == 0)) giving = null;
            }
        }

        /// <summary>The tick in a "...|tick" entry (the part at <paramref name="at"/>), or -1.</summary>
        public static int TickOf(string entry, int at = 1)
        {
            if (entry.NullOrEmpty()) return -1;
            var parts = entry.Split('|');
            return parts.Length > at && int.TryParse(parts[at], out int t) ? t : -1;
        }

        // ---------------- A custom's first asker (A22) ----------------

        /// <summary>The colony kept a custom for the first time: whoever asked for it is its first asker.</summary>
        public void NoteFirstAsker(DilemmaDef def, Pawn asker)
        {
            if (def == null || asker == null || FirstAskerId(def) != null) return;
            firstAskers.Add(def.defName + "|" + asker.ThingID);
            while (firstAskers.Count > MaxAskers) firstAskers.RemoveAt(0);
        }

        public string FirstAskerId(DilemmaDef def)
        {
            if (def == null) return null;
            string prefix = def.defName + "|";
            var e = firstAskers.FirstOrDefault(x => x.StartsWith(prefix));
            return e?.Substring(prefix.Length);
        }

        /// <summary>The custom's first asker, while they are alive and one of the colony.</summary>
        public Pawn FirstAsker(DilemmaDef def)
        {
            string id = FirstAskerId(def);
            if (id == null) return null;
            return PawnsFinder.AllMaps_FreeColonists.FirstOrDefault(p => p.ThingID == id);
        }

        // ---------------- A toy given (A11) ----------------

        public void NoteToy(Pawn child)
        {
            if (child == null) return;
            toys.RemoveAll(t => t.StartsWith(child.ThingID + "|"));
            toys.Add(child.ThingID + "|" + Find.TickManager.TicksGame);
            while (toys.Count > MaxToys) toys.RemoveAt(0);
        }

        public bool ToyLately(Pawn child)
        {
            if (child == null) return false;
            var e = toys.FirstOrDefault(t => t.StartsWith(child.ThingID + "|"));
            return e != null && ToyMath.GivenLately(TickOf(e), Find.TickManager.TicksGame, GenDate.TicksPerDay);
        }

        // ---------------- The giving day's gifts (the story "an unexpected gift") ----------------

        public void NoteGift(Pawn giver, Pawn receiver, string label)
        {
            if (giver == null || receiver == null) return;
            gifts.Add(giver.ThingID + "|" + receiver.ThingID + "|" + Find.TickManager.TicksGame + "|" + (label ?? "").Replace("|", " "));
            while (gifts.Count > MaxGifts) gifts.RemoveAt(0);
        }

        /// <summary>A gift lately handed to <paramref name="receiver"/>: its giver's id and what it was, or false.</summary>
        public bool GiftTo(Pawn receiver, out string giverId, out string label)
        {
            giverId = label = null;
            if (receiver == null) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var g in gifts)
            {
                var parts = g.Split('|');
                if (parts.Length < 4 || parts[1] != receiver.ThingID) continue;
                if (!int.TryParse(parts[2], out int t) || now - t > ColonyStoryMath.GiftDays * GenDate.TicksPerDay) continue;
                giverId = parts[0];
                label = parts[3];
                return true;
            }
            return false;
        }

        internal static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life, part 2: " + what, e);
            }
        }
    }

    /// <summary>The giving day under way at one home (A26): who gives what to whom, and how far each has got.</summary>
    public class GivingPlan : IExposable
    {
        public Map map;
        /// <summary>The home's own day it is kept on (its year times sixty and its day of the year).</summary>
        public int day = -1;
        /// <summary>Only the children are given to.</summary>
        public bool childrenOnly;
        /// <summary>It came with the faith's festive feast (Vanilla Christmas Expanded): the feast was the supper, the gifts follow it at once.</summary>
        public bool afterFeast;
        /// <summary>The supper's plan, while it waits or runs (-1: none).</summary>
        public int supperId = -1;
        /// <summary>The fairy lamps lit over the table already.</summary>
        public bool lit;
        public List<GiftPair> pairs = new List<GiftPair>();
        /// <summary>Told when it is over.</summary>
        public bool told;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref childrenOnly, "childrenOnly");
            Scribe_Values.Look(ref afterFeast, "afterFeast");
            Scribe_Values.Look(ref supperId, "supperId", -1);
            Scribe_Values.Look(ref lit, "lit");
            Scribe_Collections.Look(ref pairs, "pairs", LookMode.Deep);
            Scribe_Values.Look(ref told, "told");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pairs ??= new List<GiftPair>();
                pairs.RemoveAll(p => p == null || p.giver == null || p.receiver == null);
            }
        }
    }

    /// <summary>Where one gift of the giving day stands.</summary>
    public enum GiftState
    {
        /// <summary>Still in the stores, to be taken in the afternoon.</summary>
        ToTake,
        /// <summary>In the giver's pack, to be handed over after supper.</summary>
        Carried,
        /// <summary>Handed over.</summary>
        Given,
        /// <summary>It didn't come about (the gift gone, the giver or the one it was for away or hurt).</summary>
        Lost
    }

    /// <summary>One gift of the giving day: who gives it, to whom, and the thing.</summary>
    public class GiftPair : IExposable
    {
        public Pawn giver;
        public Pawn receiver;
        /// <summary>The stack in the stores it is taken from, then the thing in the giver's pack.</summary>
        public Thing thing;
        public string label;
        public GiftKind kind;
        public GiftState state;
        /// <summary>Walked toward this tick, for giving up on a walk that never ends.</summary>
        public int since;

        public void ExposeData()
        {
            Scribe_References.Look(ref giver, "giver");
            Scribe_References.Look(ref receiver, "receiver");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref since, "since");
        }
    }

    public partial class CouncilGameComponent
    {
        /// <summary>Colony life's variety, part 2: customs' first askers, the promises' season, toys, the giving day (LifeVariety/).</summary>
        public LifeVarietyBook lifeVariety = new LifeVarietyBook();

        private void ExposeLifeVariety()
        {
            Scribe_Deep.Look(ref lifeVariety, "lifeVariety");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) lifeVariety ??= new LifeVarietyBook();
        }
    }
}
