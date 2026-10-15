using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A room of their own, taken away again. A colonist whose own-room wish came true and who is later put back in a
    /// barracks, or in a bedroom with someone who isn't their partner, minds: a mood while it lasts (Council_RoomTaken),
    /// and a little trust lost each day like the council's other letdowns. It ends when they have a room of their own
    /// again (partners welcome); after about 30 days in shared quarters they accept it, and it's over for good. Children,
    /// slaves, prisoners and ascetics don't mind. What's remembered sits on <see cref="PawnStanding"/>.
    /// </summary>
    public static class RoomTaken
    {
        /// <summary>About a month of nights in shared quarters, and they've made their peace with it.</summary>
        public const int AcceptHours = 30 * 24;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Its mood and trust effects are on: wishes, and their downsides.</summary>
        public static bool On => S.enableWishes && S.MoodEffects && S.Negatives;

        /// <summary>Who can mind: free adult colonists with an inner life, not ascetics.</summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && p.IsFreeNonSlaveColonist && !p.IsQuestLodger() && p.needs?.mood != null && People.HasInnerLife(p)
            && p.DevelopmentalStage.Adult() && !(p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false);

        /// <summary>A wish came true: if it was a room of their own, remember when (a fresh start if they'd lost one before).</summary>
        public static void Notify_Granted(Wish w, PawnStanding st)
        {
            if (w?.def == null || st == null || !(w.Worker is WishWorker_OwnRoom)) return;
            st.ownRoomGranted = Find.TickManager.TicksGame;
            st.ownRoomLostHours = 0;
            st.ownRoomLossNoted = false;
        }

        public enum Sleeping
        {
            /// <summary>No bed, away, sleeping outside, or alone somewhere that isn't a bedroom: nothing to say.</summary>
            Unknown,
            /// <summary>A room of their own, alone or with their partners.</summary>
            Own,
            /// <summary>A barracks, or a bedroom shared with someone who isn't their partner.</summary>
            Shared
        }

        /// <summary>Where they sleep now. <paramref name="with"/> is someone they share with who isn't their partner.</summary>
        public static Sleeping Where(Pawn p, out Pawn with)
        {
            with = null;
            var ownership = p?.ownership;
            var bed = ownership?.OwnedBed;
            if (bed == null || !bed.Spawned || bed.Map == null || !bed.Map.IsPlayerHome) return Sleeping.Unknown;
            if (ownership.OwnedRoom != null) return Sleeping.Own;
            var room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return Sleeping.Unknown;
            bool others = false;
            foreach (var b in room.ContainedBeds)
            {
                if (b?.def?.building == null || !b.def.building.bed_humanlike) continue;
                var owners = b.OwnersForReading;
                for (int i = 0; i < owners.Count; i++)
                {
                    var o = owners[i];
                    if (o == null || o == p) continue;
                    others = true;
                    if (with == null && !LovePartnerRelationUtility.LovePartnerRelationExists(p, o)) with = o;
                }
            }
            if (with != null) return Sleeping.Shared;
            // Only their partners in here: that's their own room. Alone among empty bunks is still the barracks.
            if (others) return Sleeping.Own;
            return room.Role == RoomRoleDefOf.Barracks ? Sleeping.Shared : Sleeping.Unknown;
        }

        /// <summary>The room was taken and they mind it right now.</summary>
        public static bool Minds(Pawn p, PawnStanding st) =>
            st != null && st.ownRoomGranted > 0 && On && Eligible(p) && p.Spawned && Where(p, out _) == Sleeping.Shared;

        /// <summary>For the mood's tooltip: where they sleep now.</summary>
        public static string WhyNow(Pawn p)
        {
            if (Where(p, out var with) != Sleeping.Shared) return null;
            return with != null ? "CC_RoomTaken_Why_Shared".Translate(with.LabelShort).Resolve() : "CC_RoomTaken_Why_Barracks".Translate().ToString();
        }

        /// <summary>Every hour: counts the time back in shared quarters, notes the first time, and lets go after a month.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var list = comp.standings.standings;
            for (int i = 0; i < list.Count; i++)
            {
                var st = list[i];
                var p = st?.pawn;
                if (p == null || st.ownRoomGranted <= 0 || !Eligible(p) || !p.Spawned || !p.Map.IsPlayerHome) continue;
                switch (Where(p, out _))
                {
                    case Sleeping.Own:
                        // It's theirs again; if they lose it later, the month starts over.
                        st.ownRoomLostHours = 0;
                        break;
                    case Sleeping.Shared:
                        st.ownRoomLostHours++;
                        if (!st.ownRoomLossNoted) Note(comp, p, st);
                        if (st.ownRoomLostHours >= AcceptHours)
                        {
                            // They've accepted it: the room isn't theirs any more, in their own mind too.
                            st.ownRoomGranted = 0;
                            st.ownRoomLostHours = 0;
                        }
                        break;
                }
            }
        }

        /// <summary>The first time it happens: a line in their journal, and a word to the player while it costs them mood.</summary>
        private static void Note(CouncilGameComponent comp, Pawn p, PawnStanding st)
        {
            st.ownRoomLossNoted = true;
            int had = Math.Max(0, Find.TickManager.TicksGame - st.ownRoomGranted);
            comp.journals.Write(p, "Council_Journal_RoomTaken", null, TextUtil.DaysString(had).Named("DAYS"));
            if (On) News.Post("CC_RoomTaken_Message".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        /// <summary>
        /// Once a day: a little trust lost while they mind it, scaled like the council's other letdowns (the stakes, their
        /// standards and temper). Said in their trust history the first day and once a week after, so it doesn't crowd
        /// out everything else.
        /// </summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (!On) return;
            float pen = S.PenaltyScale;
            if (pen <= 0f) return;
            foreach (var st in comp.standings.standings.ToList())
            {
                var p = st?.pawn;
                if (!Minds(p, st)) continue;
                float delta = -1f * pen * Standards.MoodFactor(p, false) * CouncilGameComponent.Gentleness(p);
                // A share too small to show as a whole point would read "-0" in the history.
                bool say = (st.ownRoomLostHours / 24) % 7 == 0 && delta <= -0.5f;
                comp.standings.Change(p, delta, say ? "CC_Log_RoomTaken".Translate().ToString() : null);
            }
        }
    }

    /// <summary>"My room was taken from me": while they sleep in shared quarters again after the council gave them a room.</summary>
    public class ThoughtWorker_RoomTaken : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            try
            {
                // Most colonists were never given a room: out before anything else is looked at.
                var st = CouncilGameComponent.Instance?.standings.For(p, false);
                if (st == null || st.ownRoomGranted <= 0) return ThoughtState.Inactive;
                return RoomTaken.Minds(p, st) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("room taken mood", e);
                return ThoughtState.Inactive;
            }
        }
    }

    /// <summary>The mood itself: a council letdown in size (stakes and standards), naming when the room was given and where they sleep now.</summary>
    public class Thought_RoomTaken : Thought_Situational, IExplainedThought
    {
        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            return m < 0f ? m * ColonyCouncilMod.Settings.PenaltyScale * Standards.MoodFactor(pawn, false) : m;
        }

        public string Why
        {
            get
            {
                try
                {
                    return RoomTaken.WhyNow(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("room taken cause", e);
                    return null;
                }
            }
        }

        public override string Description
        {
            get
            {
                string raw = CurStage?.description;
                if (raw.NullOrEmpty()) return base.Description;
                string text;
                try
                {
                    var st = CouncilGameComponent.Instance?.standings.For(pawn, false);
                    int ago = st != null && st.ownRoomGranted > 0 ? Math.Max(0, Find.TickManager.TicksGame - st.ownRoomGranted) : 0;
                    text = raw.Formatted(pawn.Named("PAWN"), TextUtil.DaysString(ago).Named("DAYS")).Resolve();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("room taken description", e);
                    text = base.Description;
                }
                return Causes.Append(text, Why);
            }
        }
    }
}
