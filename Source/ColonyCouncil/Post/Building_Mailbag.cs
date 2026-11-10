using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mailbag at the gate (Rimfolk_Mailbag, a spot the colony places): the letters written wait in it. Its pane lists
    /// each letter (to whom, what for, written by, how it's likely to leave, or that its writer is bringing it), the
    /// letters that can't go by the post and wait at the gate for a go-between or a messenger of theirs, and the mail for
    /// the colony waiting to be brought; its buttons send a letter with the colony's own rider, take one back, or read one
    /// (those at the gate too).
    /// </summary>
    public class Building_Mailbag : Building
    {
        /// <summary>Lines of each list in the pane at most (the rest as "and N more").</summary>
        private const int PaneLines = 8, GateLines = 4;

        public override string GetInspectString()
        {
            var parts = new List<string>();
            string b = base.GetInspectString();
            if (!b.NullOrEmpty()) parts.Add(b);
            try
            {
                if (!PostBook.On) parts.Add(PostWords.P("CC_Mail_Bag_Off"));
                else if (Map != null)
                {
                    var bag = Outbox.Bag(Map);
                    if (bag != null && bag != this) parts.Add(PostWords.P("CC_Mail_Bag_Other"));
                    var lines = PostWords.OutboxLines(Map);
                    parts.Add(lines.Count == 0 ? PostWords.P("CC_Mail_Bag_Empty") : PostWords.P("CC_Mail_Bag_Head", lines.Count.Named("COUNT")));
                    parts.AddRange(lines.Take(PaneLines));
                    if (lines.Count > PaneLines) parts.Add(PostWords.P("CC_Mail_Bag_More", (lines.Count - PaneLines).Named("COUNT")));
                    int coming = PostBook.Instance?.incoming.Count(i => i.map == Map) ?? 0;
                    if (coming > 0) parts.Add(PostWords.P("CC_Mail_Bag_Coming", coming.Named("COUNT")));
                }
                // Letters that can't go by the post (their people at war, or the post off) wait at the gate: listed here too, to be found and read.
                if (Map != null) parts.AddRange(PostWords.GateLines(Map, GateLines));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the mailbag's pane", e);
            }
            return string.Join("\n", parts);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (Map == null || Faction != Faction.OfPlayer) yield break;
            if (PostBook.On)
            {
                var waiting = Outbox.Waiting(Map).Where(l => !l.ownRider).ToList();
                var rider = new Command_Action
                {
                    defaultLabel = PostWords.P("CC_Mail_Gizmo_Rider"), defaultDesc = PostWords.P("CC_Mail_Gizmo_RiderDesc"), icon = TexCommand.PauseCaravan,
                    action = () => RiderMenu(Map, waiting)
                };
                if (waiting.Count == 0) rider.Disable(PostWords.P("CC_Mail_Gizmo_None"));
                yield return rider;
                var all = Outbox.Waiting(Map, true);
                var back = new Command_Action
                {
                    defaultLabel = PostWords.P("CC_Mail_Gizmo_Back"), defaultDesc = PostWords.P("CC_Mail_Gizmo_BackDesc"), icon = TexCommand.ForbidOn,
                    action = () => Find.WindowStack.Add(new FloatMenu(Outbox.Waiting(Map, true).Select(l => new FloatMenuOption(LetterLabel(l), () => Outbox.TakeBack(l))).ToList()))
                };
                if (all.Count == 0) back.Disable(PostWords.P("CC_Mail_Gizmo_None"));
                yield return back;
            }
            // The letters written out in full, in the bag (or on their way to it) and waiting at the gate for a go-between or a
            // messenger: read one before it goes (Letters/LetterReadWindow.cs).
            var readable = (PostBook.On ? Outbox.Waiting(Map, true) : new List<NeighbourLetter>()).Concat(Outbox.AtGate(Map))
                .Select(l => (l, x: LetterBook.ForLetter(l.id))).Where(t => t.x?.Ours != null).ToList();
            var read = new Command_Action
            {
                defaultLabel = PostWords.P("CC_LetterRead_Gizmo"), defaultDesc = PostWords.P("CC_LetterRead_GizmoDesc"), icon = Icons.Journal,
                action = () => Find.WindowStack.Add(new FloatMenu(readable.Select(t => new FloatMenuOption(ReadLabel(t.l), () => LetterReadWindow.Open(t.x))).ToList()))
            };
            if (readable.Count == 0) read.Disable(PostWords.P("CC_LetterRead_GizmoNone"));
            yield return read;
        }

        private static string LetterLabel(NeighbourLetter l) => PostWords.P("CC_Mail_Gizmo_Letter", (l.toName ?? "?").Named("TO"), l.WriterName.Named("PAWN"));

        /// <summary>A letter to read: one waiting at the gate says so.</summary>
        private static string ReadLabel(NeighbourLetter l) =>
            Outbox.Section(l) == PostMath.BagSection.AtGate ? PostWords.P("CC_Mail_Gizmo_LetterGate", (l.toName ?? "?").Named("TO"), l.WriterName.Named("PAWN")) : LetterLabel(l);

        /// <summary>Which letter, then who rides (each with why not).</summary>
        public static void RiderMenu(Map map, List<NeighbourLetter> letters)
        {
            var opts = new List<FloatMenuOption>();
            foreach (var l in letters)
            {
                var n = NotableBook.Instance?.Get(l.notable);
                string why = OwnRiders.WhyNot(n, map);
                var letter = l;
                opts.Add(why != null ? new FloatMenuOption(LetterLabel(l) + ": " + why, null) : new FloatMenuOption(LetterLabel(l), () => PickRider(map, letter)));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption(PostWords.P("CC_Mail_Gizmo_None"), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void PickRider(Map map, NeighbourLetter l)
        {
            var town = Outbox.TownOf(l, out _);
            var mount = OwnRiders.MountFor(map, null);
            float days = PostReach.RiderDays(map, town, mount != null);
            var opts = OwnRiders.Candidates(map).Select(p => new FloatMenuOption(PostWords.P("CC_Mail_PickRider", p.Named("PAWN"),
                (p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).Named("ANIMALS")), () => OwnRiders.Send(l, p))).ToList();
            if (opts.Count == 0) opts.Add(new FloatMenuOption(PostWords.P("CC_Mail_Why_NoRider"), null));
            Find.WindowStack.Add(new FloatMenu(opts, PostWords.P(mount != null ? "CC_Mail_PickRider_Title" : "CC_Mail_PickRider_TitleFoot", (town?.Name ?? "?").Named("TOWN"),
                PostWords.DaysWords(days).Named("DAYS"))));
        }
    }
}
